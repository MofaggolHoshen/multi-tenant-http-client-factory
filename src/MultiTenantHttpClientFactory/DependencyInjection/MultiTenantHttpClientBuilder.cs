using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
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
        _services.AddSingleton<ITenantResolver>(resolver);
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
    /// Reads tenant configuration from the specified section in appsettings.json.
    /// IConfiguration must be registered in the service collection before building the host.
    /// Uses array format where each item includes TenantId for consistency with other stores (e.g., database).
    /// </summary>
    public MultiTenantHttpClientBuilder WithJsonConfiguration(string sectionName = "Tenants")
    {
        _services.AddOptions<JsonTenantStoreOptions>()
            .Configure<Microsoft.Extensions.Configuration.IConfiguration>((options, configuration) =>
            {
                var section = configuration.GetSection(sectionName);
                var tenants = new List<MultiTenantHttpClientFactory.Abstractions.Models.TenantConfiguration>();
                section.Bind(tenants);

                options.Tenants.Clear();

                foreach (var tenant in tenants)
                {
                    if (string.IsNullOrWhiteSpace(tenant.TenantId))
                    {
                        continue;
                    }

                    var normalizedTenantId = tenant.TenantId.Trim();
                    if (options.Tenants.ContainsKey(normalizedTenantId))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate TenantId '{normalizedTenantId}' found in configuration section '{sectionName}'. TenantId values must be unique.");
                    }

                    tenant.TenantId = normalizedTenantId;

                    // Validate and normalize the tenant configuration
                    try
                    {
                        TenantConfigurationValidator.ValidateAndNormalize(tenant);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new InvalidOperationException(
                            $"Configuration validation failed for tenant '{normalizedTenantId}' in section '{sectionName}': {ex.Message}",
                            ex);
                    }

                    options.Tenants[normalizedTenantId] = tenant;
                }
            });

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
