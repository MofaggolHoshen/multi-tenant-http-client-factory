using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MultiTenantHttpClientFactory.Abstractions;
using MultiTenantHttpClientFactory.Abstractions.Models;

namespace MultiTenantHttpClientFactory.Certificates;

/// <summary>
/// Certificate provider that routes to the correct provider based on certificate type.
/// </summary>
internal class CompositeCertificateProvider : ICertificateProvider
{
    private readonly Dictionary<CertificateType, ICertificateProvider> _providers;
    private readonly ILogger<CompositeCertificateProvider>? _logger;

    public CompositeCertificateProvider(ILogger<CompositeCertificateProvider>? logger = null)
    {
        _providers = new Dictionary<CertificateType, ICertificateProvider>();
        _logger = logger;
    }

    /// <summary>
    /// Registers a certificate provider for a specific type.
    /// </summary>
    public void Register(CertificateType type, ICertificateProvider provider)
    {
        if (provider == null)
            throw new ArgumentNullException(nameof(provider));

        _providers[type] = provider;
        _logger?.LogDebug("Registered certificate provider for type: {CertificateType}", type);
    }

    public async Task<X509Certificate2?> GetCertificateAsync(CertificateConfiguration config, CancellationToken cancellationToken = default)
    {
        if (config == null)
            return null;

        if (!_providers.TryGetValue(config.Type, out var provider))
        {
            var message = $"No certificate provider registered for type: {config.Type}";
            _logger?.LogError(message);
            throw new NotSupportedException($"{message}. Ensure the corresponding NuGet package is installed and the provider is registered.");
        }

        return await provider.GetCertificateAsync(config, cancellationToken);
    }
}
