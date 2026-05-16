using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MultiTenantHttpClientFactory.Abstractions;
using MultiTenantHttpClientFactory.Abstractions.Models;
using MultiTenantHttpClientFactory.Certificates;
using MultiTenantHttpClientFactory.Configuration;
using MultiTenantHttpClientFactory.DependencyInjection;

namespace MultiTenantHttpClientFactory.Tests;

public class FactoryIntegrationTests
{
    private static (ITenantHttpClientFactory factory, InMemoryTenantStore store) BuildFactory(
        params TenantConfiguration[] tenants)
    {
        var store = new InMemoryTenantStore(tenants);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var configProvider = new TenantConfigurationProvider(
            store, cache, NullLogger<TenantConfigurationProvider>.Instance);

        var certProvider = new CompositeCertificateProvider(
            NullLogger<CompositeCertificateProvider>.Instance);

        var handlerCache = new TenantHandlerCache(
            configProvider, certProvider, NullLogger<TenantHandlerCache>.Instance);

        var tenantContext = new TenantContext();

        var factory = new TenantHttpClientFactory(
            configProvider, handlerCache, tenantContext, NullLogger<TenantHttpClientFactory>.Instance);

        return (factory, store);
    }

    [Fact]
    public void CreateClient_ReturnsClient_WithCorrectBaseAddress()
    {
        var tenant = new TenantConfiguration
        {
            TenantId = "acme",
            DefaultEndpoint = new EndpointConfiguration
            {
                BaseAddress = new Uri("https://api.acme.example.com")
            }
        };
        var (factory, _) = BuildFactory(tenant);

        using var client = factory.CreateClient("acme", null);

        Assert.Equal(new Uri("https://api.acme.example.com"), client.BaseAddress);
    }

    [Fact]
    public void CreateClient_AppliesDefaultHeaders()
    {
        var tenant = new TenantConfiguration
        {
            TenantId = "acme",
            DefaultEndpoint = new EndpointConfiguration
            {
                BaseAddress = new Uri("https://api.acme.example.com")
            },
            DefaultHeaders = new Dictionary<string, string>
            {
                ["X-Custom-Header"] = "custom-value"
            }
        };
        var (factory, _) = BuildFactory(tenant);

        using var client = factory.CreateClient("acme", null);

        Assert.True(client.DefaultRequestHeaders.Contains("X-Custom-Header"));
        Assert.Equal("custom-value", client.DefaultRequestHeaders.GetValues("X-Custom-Header").First());
    }

    [Fact]
    public void CreateClient_UsesNamedEndpoint()
    {
        var tenant = new TenantConfiguration
        {
            TenantId = "acme",
            Endpoints = new Dictionary<string, EndpointConfiguration>
            {
                ["payments"] = new EndpointConfiguration { BaseAddress = new Uri("https://payments.acme.com") },
                ["notifications"] = new EndpointConfiguration { BaseAddress = new Uri("https://notif.acme.com") }
            }
        };
        var (factory, _) = BuildFactory(tenant);

        using var client = factory.CreateClient("acme", "payments");

        Assert.Equal(new Uri("https://payments.acme.com"), client.BaseAddress);
    }

    [Fact]
    public void CreateClient_Throws_ForUnknownTenant()
    {
        var (factory, _) = BuildFactory();

        Assert.Throws<TenantNotFoundException>(() => factory.CreateClient("unknown", null));
    }

    [Fact]
    public void CreateClient_Throws_WhenNoEndpointConfigured()
    {
        var tenant = new TenantConfiguration
        {
            TenantId = "no-endpoint"
            // No endpoints or default endpoint set
        };
        var (factory, _) = BuildFactory(tenant);

        Assert.Throws<InvalidOperationException>(() => factory.CreateClient("no-endpoint", null));
    }

    [Fact]
    public void CreateClient_AppliesCustomTimeout()
    {
        var tenant = new TenantConfiguration
        {
            TenantId = "t1",
            DefaultEndpoint = new EndpointConfiguration
            {
                BaseAddress = new Uri("https://example.com"),
                Timeout = TimeSpan.FromSeconds(15)
            }
        };
        var (factory, _) = BuildFactory(tenant);

        using var client = factory.CreateClient("t1", null);

        Assert.Equal(TimeSpan.FromSeconds(15), client.Timeout);
    }

    [Fact]
    public void CreateClient_FallsBackToTenantTimeout_WhenEndpointTimeoutNotSet()
    {
        var tenant = new TenantConfiguration
        {
            TenantId = "t1",
            Timeout = TimeSpan.FromSeconds(42),
            DefaultEndpoint = new EndpointConfiguration
            {
                BaseAddress = new Uri("https://example.com")
                // No endpoint timeout
            }
        };
        var (factory, _) = BuildFactory(tenant);

        using var client = factory.CreateClient("t1", null);

        Assert.Equal(TimeSpan.FromSeconds(42), client.Timeout);
    }

    // ─── DI integration via builder ───────────────────────────────────────────

    [Fact]
    public void DI_Registration_ResolvesFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();

        services.AddMultiTenantHttpClientFactory()
            .WithInMemoryStore(new[]
            {
                new TenantConfiguration
                {
                    TenantId = "di-tenant",
                    DefaultEndpoint = new EndpointConfiguration { BaseAddress = new Uri("https://di.example.com") }
                }
            });

        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<ITenantHttpClientFactory>();

        Assert.NotNull(factory);
        using var client = factory.CreateClient("di-tenant", null);
        Assert.Equal(new Uri("https://di.example.com"), client.BaseAddress);
    }
}
