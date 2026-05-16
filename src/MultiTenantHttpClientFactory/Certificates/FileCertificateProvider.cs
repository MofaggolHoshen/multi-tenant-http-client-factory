using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MultiTenantHttpClientFactory.Abstractions;
using MultiTenantHttpClientFactory.Abstractions.Models;

namespace MultiTenantHttpClientFactory.Certificates;

/// <summary>
/// Certificate provider that loads certificates from the file system.
/// </summary>
internal class FileCertificateProvider : ICertificateProvider
{
    private readonly ConcurrentDictionary<string, X509Certificate2> _cache;
    private readonly ILogger<FileCertificateProvider> _logger;

    public FileCertificateProvider(ILogger<FileCertificateProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = new ConcurrentDictionary<string, X509Certificate2>();
    }

    public Task<X509Certificate2?> GetCertificateAsync(CertificateConfiguration config, CancellationToken cancellationToken = default)
    {
        if (config?.Type != CertificateType.File || string.IsNullOrEmpty(config.Path))
            return Task.FromResult<X509Certificate2?>(null);

        var cacheKey = config.Path;
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            _logger.LogDebug("Returned cached certificate from file: {FilePath}", config.Path);
            return Task.FromResult<X509Certificate2?>(cached);
        }

        try
        {
            if (!File.Exists(config.Path))
                throw new FileNotFoundException($"Certificate file not found: {config.Path}");

            var cert = new X509Certificate2(config.Path, config.Password, 
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);

            _cache.TryAdd(cacheKey, cert);
            _logger.LogDebug("Loaded certificate from file: {FilePath}", config.Path);
            return Task.FromResult<X509Certificate2?>(cert);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load certificate from file: {FilePath}", config.Path);
            throw new CertificateLoadException($"Failed to load certificate from file: {config.Path}", ex, null, config.Path);
        }
    }
}
