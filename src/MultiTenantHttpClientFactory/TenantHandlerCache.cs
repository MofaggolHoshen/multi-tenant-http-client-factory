using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using MultiTenantHttpClientFactory.Abstractions;
using MultiTenantHttpClientFactory.Abstractions.Models;

namespace MultiTenantHttpClientFactory;

/// <summary>
/// Thread-safe cache for HttpMessageHandler instances keyed by tenant and endpoint.
/// Manages handler lifecycle, expiry, and cleanup.
/// </summary>
internal class TenantHandlerCache : IDisposable
{
    private readonly ConcurrentDictionary<string, Lazy<ActiveHandlerEntry>> _handlers;
    private readonly ITenantConfigurationProvider _configurationProvider;
    private readonly ICertificateProvider _certificateProvider;
    private readonly ILogger<TenantHandlerCache> _logger;
    private readonly TimeSpan _handlerLifetime;
    private readonly Timer? _cleanupTimer;
    private volatile bool _disposed;

    public TenantHandlerCache(
        ITenantConfigurationProvider configurationProvider,
        ICertificateProvider certificateProvider,
        ILogger<TenantHandlerCache> logger,
        TimeSpan? defaultHandlerLifetime = null)
    {
        _handlers = new ConcurrentDictionary<string, Lazy<ActiveHandlerEntry>>();
        _configurationProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
        _certificateProvider = certificateProvider ?? throw new ArgumentNullException(nameof(certificateProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _handlerLifetime = defaultHandlerLifetime ?? TimeSpan.FromMinutes(2);

        // Start cleanup timer
        _cleanupTimer = new Timer(CleanupExpiredHandlers, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
    }

    /// <summary>
    /// Gets or creates a handler for the given tenant and endpoint.
    /// </summary>
    public HttpMessageHandler GetOrCreateHandler(string tenantId, string? endpointName = null)
    {
        ThrowIfDisposed();

        var cacheKey = $"{tenantId}:{endpointName}";
        var lazyHandler = _handlers.GetOrAdd(cacheKey, _ =>
        {
            return new Lazy<ActiveHandlerEntry>(() => CreateHandlerEntry(tenantId, endpointName), LazyThreadSafetyMode.ExecutionAndPublication);
        });

        var entry = lazyHandler.Value;

        // Check if handler has expired
        if (entry.IsExpired)
        {
            // Mark for removal and create a new one
            _handlers.TryRemove(cacheKey, out _);
            return GetOrCreateHandler(tenantId, endpointName);
        }

        entry.IncrementActiveCount();
        return new LifetimeTrackingHandler(entry.Handler, () => entry.DecrementActiveCount());
    }

    /// <summary>
    /// Invalidates all handlers for a given tenant.
    /// Called when tenant configuration changes.
    /// </summary>
    public void InvalidateTenant(string tenantId)
    {
        var keysToRemove = _handlers.Keys.Where(k => k.StartsWith($"{tenantId}:")).ToList();
        foreach (var key in keysToRemove)
        {
            if (_handlers.TryRemove(key, out var lazyEntry))
            {
                try
                {
                    lazyEntry.Value?.Handler?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing handler for key {CacheKey}", key);
                }
            }
        }
    }

    /// <summary>
    /// Invalidates all cached handlers.
    /// </summary>
    public void InvalidateAll()
    {
        var snapshot = _handlers.ToArray();
        _handlers.Clear();

        foreach (var kvp in snapshot)
        {
            try
            {
                kvp.Value.Value?.Handler?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing handler for key {CacheKey}", kvp.Key);
            }
        }
    }

    private ActiveHandlerEntry CreateHandlerEntry(string tenantId, string? endpointName)
    {
        var config = _configurationProvider.GetConfigurationAsync(tenantId).GetAwaiter().GetResult()
            ?? throw new InvalidOperationException($"Tenant configuration not found for {tenantId}");

        var handler = CreateSocketsHttpHandler(config, endpointName);
        var entry = new ActiveHandlerEntry(handler, _handlerLifetime);

        _logger.LogDebug("Created handler for tenant {TenantId}, endpoint {EndpointName}", tenantId, endpointName ?? "default");
        return entry;
    }

    private SocketsHttpHandler CreateSocketsHttpHandler(TenantConfiguration config, string? endpointName)
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2), // DNS rotation
            AutomaticDecompression = System.Net.DecompressionMethods.All
        };

        // Load client certificate if configured
        if (config.Certificate != null)
        {
            var cert = _certificateProvider.GetCertificateAsync(config.Certificate).GetAwaiter().GetResult();
            if (cert != null)
            {
                handler.SslOptions.ClientCertificates ??= new System.Security.Cryptography.X509Certificates.X509Certificate2Collection();
                handler.SslOptions.ClientCertificates.Add(cert);
                _logger.LogDebug("Loaded certificate for tenant");
            }
        }

        return handler;
    }

    private void CleanupExpiredHandlers(object? state)
    {
        if (_disposed)
            return;

        try
        {
            var expiredKeys = _handlers
                .Where(kvp => kvp.Value.IsValueCreated && kvp.Value.Value.IsExpired && kvp.Value.Value.ActiveCount == 0)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                if (_handlers.TryRemove(key, out var lazyEntry))
                {
                    try
                    {
                        lazyEntry.Value?.Handler?.Dispose();
                        _logger.LogDebug("Cleaned up expired handler for key {CacheKey}", key);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error disposing handler during cleanup for key {CacheKey}", key);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during handler cleanup");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cleanupTimer?.Dispose();
        InvalidateAll();
    }

    /// <summary>
    /// Represents an active handler entry with lifetime tracking.
    /// </summary>
    private class ActiveHandlerEntry
    {
        private int _activeCount;
        private readonly DateTime _createdAt;

        public HttpMessageHandler Handler { get; }
        public TimeSpan Lifetime { get; }

        public ActiveHandlerEntry(HttpMessageHandler handler, TimeSpan lifetime)
        {
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
            Lifetime = lifetime;
            _createdAt = DateTime.UtcNow;
            _activeCount = 0;
        }

        public bool IsExpired => DateTime.UtcNow - _createdAt > Lifetime;

        public int ActiveCount => _activeCount;

        public void IncrementActiveCount() => Interlocked.Increment(ref _activeCount);

        public void DecrementActiveCount() => Interlocked.Decrement(ref _activeCount);
    }

    /// <summary>
    /// Wraps a handler to track when it's no longer in use.
    /// </summary>
    private class LifetimeTrackingHandler : DelegatingHandler
    {
        private readonly Action _onDispose;

        public LifetimeTrackingHandler(HttpMessageHandler innerHandler, Action onDispose)
            : base(innerHandler)
        {
            _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _onDispose();
            }
            base.Dispose(disposing);
        }
    }
}
