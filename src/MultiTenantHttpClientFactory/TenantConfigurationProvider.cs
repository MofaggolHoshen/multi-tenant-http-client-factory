using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using MultiTenantHttpClientFactory.Abstractions;
using MultiTenantHttpClientFactory.Abstractions.Models;

namespace MultiTenantHttpClientFactory;

/// <summary>
/// Default implementation of ITenantConfigurationProvider.
/// Wraps an ITenantStore with a MemoryCache layer and change token subscription.
/// </summary>
internal class TenantConfigurationProvider : ITenantConfigurationProvider, IDisposable
{
    private readonly ITenantStore _store;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TenantConfigurationProvider> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _changeTokenSources;
    private readonly ConcurrentDictionary<string, Action> _invalidationCallbacks;
    private IDisposable? _storeChangeSubscription;
    private volatile bool _disposed;

    public TenantConfigurationProvider(
        ITenantStore store,
        IMemoryCache cache,
        ILogger<TenantConfigurationProvider> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _changeTokenSources = new ConcurrentDictionary<string, CancellationTokenSource>();
        _invalidationCallbacks = new ConcurrentDictionary<string, Action>();

        // Subscribe to store reload token
        SubscribeToStoreChanges();
    }

    /// <summary>
    /// Gets the configuration for a tenant, with caching.
    /// </summary>
    public async Task<TenantConfiguration?> GetConfigurationAsync(string tenantId)
    {
        ThrowIfDisposed();

        if (string.IsNullOrEmpty(tenantId))
            throw new ArgumentException("Tenant ID cannot be null or empty", nameof(tenantId));

        var cacheKey = $"tenant-config:{tenantId}";

        if (_cache.TryGetValue(cacheKey, out TenantConfiguration? cachedConfig))
        {
            _logger.LogDebug("Tenant configuration retrieved from cache for {TenantId}", tenantId);
            return cachedConfig;
        }

        var config = await _store.GetTenantAsync(tenantId, CancellationToken.None);

        if (config != null)
        {
            // Cache for 5 minutes with sliding expiration
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(1)
            };

            _cache.Set(cacheKey, config, cacheOptions);
            _logger.LogDebug("Tenant configuration retrieved from store and cached for {TenantId}", tenantId);
        }
        else
        {
            _logger.LogDebug("Tenant configuration not found for {TenantId}", tenantId);
        }

        return config;
    }

    /// <summary>
    /// Returns a change token for a specific tenant's configuration.
    /// </summary>
    public IChangeToken GetChangeToken(string tenantId)
    {
        ThrowIfDisposed();

        if (string.IsNullOrEmpty(tenantId))
            throw new ArgumentException("Tenant ID cannot be null or empty", nameof(tenantId));

        var tokenSource = _changeTokenSources.GetOrAdd(tenantId, _ => new CancellationTokenSource());
        return new CancellationChangeToken(tokenSource.Token);
    }

    /// <summary>
    /// Registers a callback to be invoked when a tenant's configuration changes.
    /// </summary>
    internal void RegisterInvalidationCallback(string tenantId, Action callback)
    {
        ThrowIfDisposed();

        if (string.IsNullOrEmpty(tenantId))
            throw new ArgumentException("Tenant ID cannot be null or empty", nameof(tenantId));

        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        _invalidationCallbacks.AddOrUpdate(tenantId, callback, (_, _) => callback);
    }

    /// <summary>
    /// Invalidates the configuration cache for a specific tenant.
    /// </summary>
    internal void InvalidateTenant(string tenantId)
    {
        _logger.LogDebug("Invalidating configuration cache for tenant {TenantId}", tenantId);
        _cache.Remove($"tenant-config:{tenantId}");
        InvalidateChangeToken(tenantId);

        // Fire registered invalidation callbacks
        if (_invalidationCallbacks.TryRemove(tenantId, out var callback))
        {
            try
            {
                callback();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error executing invalidation callback for tenant {TenantId}", tenantId);
            }
        }
    }

    /// <summary>
    /// Invalidates all cached configurations.
    /// </summary>
    internal void InvalidateAll()
    {
        _logger.LogDebug("Invalidating all configuration caches");
        _cache.Dispose();

        foreach (var source in _changeTokenSources.Values)
        {
            source?.Cancel();
            source?.Dispose();
        }
        _changeTokenSources.Clear();
    }

    private void InvalidateChangeToken(string tenantId)
    {
        if (_changeTokenSources.TryRemove(tenantId, out var tokenSource))
        {
            try
            {
                tokenSource.Cancel();
                tokenSource.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error canceling change token for tenant {TenantId}", tenantId);
            }
        }
    }

    private void SubscribeToStoreChanges()
    {
        try
        {
            var reloadToken = _store.GetReloadToken();
            _storeChangeSubscription = reloadToken.RegisterChangeCallback(_ =>
            {
                _logger.LogInformation("Store reload token fired, invalidating all caches");
                InvalidateAll();
                // Resubscribe
                SubscribeToStoreChanges();
            }, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error subscribing to store changes");
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
        _storeChangeSubscription?.Dispose();

        foreach (var source in _changeTokenSources.Values)
        {
            try
            {
                source?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing change token source");
            }
        }
        _changeTokenSources.Clear();
    }
}
