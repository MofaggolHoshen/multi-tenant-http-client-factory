using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using MultiTenantHttpClientFactory.Abstractions;
using MultiTenantHttpClientFactory.Certificates;
using MultiTenantHttpClientFactory.TenantResolution;

namespace MultiTenantHttpClientFactory.DependencyInjection;

/// <summary>
/// Extension methods for IServiceCollection to register multi-tenant HttpClient factory.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the multi-tenant HttpClient factory with core services.
    /// </summary>
    public static MultiTenantHttpClientBuilder AddMultiTenantHttpClientFactory(
        this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        // Register core services
        services.AddMemoryCache();

        // Register configuration provider
        services.AddSingleton<ITenantConfigurationProvider, TenantConfigurationProvider>();

        // Register handler cache
        services.AddSingleton<TenantHandlerCache>();

        // Register factory as scoped (it depends on scoped ITenantContext)
        services.AddScoped<ITenantHttpClientFactory, TenantHttpClientFactory>();

        // Register tenant context as scoped
        services.AddScoped<ITenantContext, TenantContext>();

        // Register composite tenant resolver - it will resolve all registered ITenantResolver instances
        services.AddSingleton<CompositeTenantResolver>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CompositeTenantResolver>>();
            var resolvers = sp.GetServices<ITenantResolver>()
                .Where(r => r is not CompositeTenantResolver)
                .ToList();
            return new CompositeTenantResolver(resolvers, logger);
        });
        services.AddSingleton<ITenantResolver>(sp => sp.GetRequiredService<CompositeTenantResolver>());

        // Register basic certificate providers
        services.AddSingleton<FileCertificateProvider>();
        services.AddSingleton<StoreCertificateProvider>();
        services.AddSingleton<Base64CertificateProvider>();

        // Register certificate provider  
        services.AddSingleton<CompositeCertificateProvider>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CompositeCertificateProvider>>();
            var composite = new CompositeCertificateProvider(logger);

            // Register built-in providers
            composite.Register(Abstractions.Models.CertificateType.File, sp.GetRequiredService<FileCertificateProvider>());
            composite.Register(Abstractions.Models.CertificateType.Store, sp.GetRequiredService<StoreCertificateProvider>());
            composite.Register(Abstractions.Models.CertificateType.Base64, sp.GetRequiredService<Base64CertificateProvider>());

            return composite;
        });
        services.AddSingleton<ICertificateProvider>(sp => sp.GetRequiredService<CompositeCertificateProvider>());

        return new MultiTenantHttpClientBuilder(services);
    }
}
