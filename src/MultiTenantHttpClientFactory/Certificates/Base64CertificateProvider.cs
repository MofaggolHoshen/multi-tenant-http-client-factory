using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MultiTenantHttpClientFactory.Abstractions;
using MultiTenantHttpClientFactory.Abstractions.Models;

namespace MultiTenantHttpClientFactory.Certificates;

/// <summary>
/// Certificate provider that loads certificates from base64-encoded data.
/// </summary>
internal class Base64CertificateProvider : ICertificateProvider
{
    private readonly ILogger<Base64CertificateProvider>? _logger;

    public Base64CertificateProvider(ILogger<Base64CertificateProvider>? logger = null)
    {
        _logger = logger;
    }

    public Task<X509Certificate2?> GetCertificateAsync(CertificateConfiguration config, CancellationToken cancellationToken = default)
    {
        if (config?.Type != CertificateType.Base64 || string.IsNullOrEmpty(config.Base64Data))
            return Task.FromResult<X509Certificate2?>(null);

        try
        {
            var certData = Convert.FromBase64String(config.Base64Data);
            var cert = new X509Certificate2(certData, config.Password);
            
            _logger?.LogDebug("Loaded certificate from base64 data");
            return Task.FromResult<X509Certificate2?>(cert);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load certificate from base64 data");
            throw new CertificateLoadException("Failed to load certificate from base64 data", ex, null, "base64");
        }
    }
}
