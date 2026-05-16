using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using MultiTenantHttpClientFactory.Abstractions;
using MultiTenantHttpClientFactory.Certificates;
using MultiTenantHttpClientFactory.Configuration;

namespace MultiTenantHttpClientFactory.DependencyInjection;

/// <summary>
/// Fluent builder for configuring multi-tenant HttpClient factory.
/// </summary>
public class MultiTenantHttpClientBuilder
{
    private readonly IServiceCollection _services;
    private readonly List<ITenantResolver> _resolvers;
    private readonly Dictionary<string, Action<SocketsHttpHandler>> _handlerConfigurations;
    private TimeSpan? _defaultHandlerLifetime;
    private readonly List<Type> _delegatingHandlerTypes;

    internal MultiTenantHttpClientBuilder(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _resolvers = new List<ITenantResolver>();
        _handlerConfigurations = new Dictionary<string, Action<SocketsHttpHandler>>();
        _delegatingHandlerTypes = new List<Type>();
    }

    /// <summary>
    /// Adds a tenant resolver to the resolution chain.
    /// </summary>
    public MultiTenantHttpClientBuilder AddTenantResolver<T>(T resolver) where T : class, ITenantResolver
    {
        if (resolver == null)
            throw new ArgumentNullException(nameof(resolver));

        _resolvers.Add(resolver);
        return this;
    }

    /// <summary>
    /// Registers a custom tenant store implementation.
    /// </summary>
    public MultiTenantHttpClientBuilder WithTenantStore<T>() where T : class, ITenantStore
    {
        _services.AddSingleton(typeof(ITenantStore), typeof(T));
        return this;
    }

    /// <summary>
    /// Registers an in-memory tenant store pre-populated with the given tenants.
    /// Ideal for testing and static configuration scenarios.
    /// </summary>
    public MultiTenantHttpClientBuilder WithInMemoryStore(
        IEnumerable<MultiTenantHttpClientFactory.Abstractions.Models.TenantConfiguration>? initialTenants = null)
    {
        var store = initialTenants != null
            ? new InMemoryTenantStore(initialTenants)
            : new InMemoryTenantStore();

        _services.AddSingleton<ITenantStore>(store);
        _services.AddSingleton(store); // Also register as concrete type for mutation
        return this;
    }

    /// <summary>
    /// Registers JSON configuration-based tenant store.
    /// </summary>
    public MultiTenantHttpClientBuilder WithJsonConfiguration(string sectionName = "Tenants")
    {
        _services.Configure<Dictionary<string, MultiTenantHttpClientFactory.Abstractions.Models.TenantConfiguration>>(
            sectionName, options => { });

        _services.AddSingleton<ITenantStore, JsonFileTenantStore>();
        return this;
    }

    /// <summary>
    /// Registers a certificate provider for a specific type.
    /// </summary>
    public MultiTenantHttpClientBuilder WithCertificateProvider<T>(
        MultiTenantHttpClientFactory.Abstractions.Models.CertificateType type)
        where T : class, ICertificateProvider
    {
        _services.AddSingleton<T>();

        // Get or create the composite provider and register this one
        _services.AddSingleton(sp =>
        {
            var composite = sp.GetRequiredService<CompositeCertificateProvider>();
            var provider = sp.GetRequiredService<T>();
            composite.Register(type, provider);
            return composite;
        });

        return this;
    }

    /// <summary>
    /// Configures the default SocketsHttpHandler.
    /// </summary>
    public MultiTenantHttpClientBuilder ConfigureDefaultHandler(Action<SocketsHttpHandler> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        _handlerConfigurations["_default"] = configure;
        return this;
    }

    /// <summary>
    /// Sets the default handler lifetime.
    /// </summary>
    public MultiTenantHttpClientBuilder SetDefaultHandlerLifetime(TimeSpan lifetime)
    {
        _defaultHandlerLifetime = lifetime;
        return this;
    }

    /// <summary>
    /// Adds a delegating handler to the handler pipeline.
    /// </summary>
    public MultiTenantHttpClientBuilder AddDelegatingHandler<T>() where T : DelegatingHandler
    {
        _services.AddTransient<T>();
        _delegatingHandlerTypes.Add(typeof(T));
        return this;
    }

    internal List<ITenantResolver> GetResolvers() => _resolvers;
    internal TimeSpan? GetDefaultHandlerLifetime() => _defaultHandlerLifetime;
}
