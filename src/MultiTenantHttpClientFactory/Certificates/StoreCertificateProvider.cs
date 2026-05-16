using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MultiTenantHttpClientFactory.Abstractions;
using MultiTenantHttpClientFactory.Abstractions.Models;

namespace MultiTenantHttpClientFactory.Certificates;

/// <summary>
/// Certificate provider that loads certificates from the Windows certificate store.
/// </summary>
internal class StoreCertificateProvider : ICertificateProvider
{
    private readonly StoreName _storeName;
    private readonly StoreLocation _storeLocation;
    private readonly ILogger<StoreCertificateProvider> _logger;

    public StoreCertificateProvider(
        StoreName storeName = StoreName.My,
        StoreLocation storeLocation = StoreLocation.LocalMachine,
        ILogger<StoreCertificateProvider>? logger = null)
    {
        _storeName = storeName;
        _storeLocation = storeLocation;
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<StoreCertificateProvider>();
    }

    public Task<X509Certificate2?> GetCertificateAsync(CertificateConfiguration config, CancellationToken cancellationToken = default)
    {
        if (config?.Type != CertificateType.Store || string.IsNullOrEmpty(config.Thumbprint))
            return Task.FromResult<X509Certificate2?>(null);

        try
        {
            using var store = new X509Store(_storeName, _storeLocation);
            store.Open(OpenFlags.ReadOnly);

            var certs = store.Certificates.Find(X509FindType.FindByThumbprint, config.Thumbprint, false);
            if (certs.Count == 0)
                throw new InvalidOperationException($"No certificate found with thumbprint: {config.Thumbprint}");

            var cert = certs[0];
            _logger.LogDebug("Loaded certificate from store with thumbprint: {Thumbprint}", config.Thumbprint);
            return Task.FromResult<X509Certificate2?>(cert);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load certificate from store with thumbprint: {Thumbprint}", config.Thumbprint);
            throw new CertificateLoadException($"Failed to load certificate from store: {ex.Message}", ex, null, config.Thumbprint);
        }
    }
}

/// <summary>
/// Null logger factory for when logger is not provided.
/// </summary>
internal class NullLoggerFactory : ILoggerFactory
{
    public static readonly NullLoggerFactory Instance = new();

    public ILogger CreateLogger(string categoryName) => NullLogger.Instance;

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }
}

/// <summary>
/// Null logger implementation.
/// </summary>
internal class NullLogger : ILogger
{
    public static readonly NullLogger Instance = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => false;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
    }
}
