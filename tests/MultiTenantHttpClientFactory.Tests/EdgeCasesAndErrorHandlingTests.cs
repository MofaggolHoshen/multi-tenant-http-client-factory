using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using MultiTenantHttpClientFactory.Abstractions;
using MultiTenantHttpClientFactory.Abstractions.Models;
using MultiTenantHttpClientFactory.Certificates;
using MultiTenantHttpClientFactory.Configuration;

namespace MultiTenantHttpClientFactory.Tests;

public class TenantContextTests
{
    [Fact]
    public void TenantContext_InitiallyNotResolved()
    {
        var context = new TenantContext();

        Assert.False(context.IsResolved);
        Assert.Null(context.TenantId);
        Assert.Null(context.Configuration);
    }

    [Fact]
    public void TenantContext_SetTenantId_MarksAsResolved()
    {
        var context = new TenantContext();
        var config = new TenantConfiguration { TenantId = "test-tenant" };

        context.SetTenant("test-tenant", config);

        Assert.True(context.IsResolved);
        Assert.Equal("test-tenant", context.TenantId);
        Assert.Same(config, context.Configuration);
    }

    [Fact]
    public void TenantContext_ClearTenant_ResetState()
    {
        var context = new TenantContext();
        context.SetTenant("test-tenant", new TenantConfiguration { TenantId = "test-tenant" });

        context.Clear();

        Assert.False(context.IsResolved);
        Assert.Null(context.TenantId);
        Assert.Null(context.Configuration);
    }

    [Fact]
    public void TenantContext_CanBeReusedAcrossDifferentTenants()
    {
        var context = new TenantContext();

        context.SetTenant("tenant-1", new TenantConfiguration { TenantId = "tenant-1" });
        Assert.Equal("tenant-1", context.TenantId);

        context.SetTenant("tenant-2", new TenantConfiguration { TenantId = "tenant-2" });
        Assert.Equal("tenant-2", context.TenantId);

        context.Clear();
        Assert.False(context.IsResolved);
    }

    [Fact]
    public void TenantContext_ThrowsWhenCreatingClientWithoutResolution()
    {
        var context = new TenantContext();
        var store = new InMemoryTenantStore();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var configProvider = new TenantConfigurationProvider(store, cache, NullLogger<TenantConfigurationProvider>.Instance);
        var certProvider = new CompositeCertificateProvider(NullLogger<CompositeCertificateProvider>.Instance);
        var handlerCache = new TenantHandlerCache(configProvider, certProvider, NullLogger<TenantHandlerCache>.Instance);
        var factory = new TenantHttpClientFactory(configProvider, handlerCache, context, NullLogger<TenantHttpClientFactory>.Instance);

        Assert.Throws<TenantNotResolvedException>(() => factory.CreateClient());
    }
}

public class EndpointConfigurationTests
{
    [Fact]
    public void EndpointConfiguration_WithBaseAddress()
    {
        var endpoint = new EndpointConfiguration
        {
            BaseAddress = new Uri("https://api.example.com")
        };

        Assert.Equal(new Uri("https://api.example.com"), endpoint.BaseAddress);
    }

    [Fact]
    public void EndpointConfiguration_WithHeaders()
    {
        var endpoint = new EndpointConfiguration
        {
            BaseAddress = new Uri("https://api.example.com"),
            Headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer token",
                ["X-Custom"] = "value"
            }
        };

        Assert.Equal("Bearer token", endpoint.Headers["Authorization"]);
        Assert.Equal("value", endpoint.Headers["X-Custom"]);
    }

    [Fact]
    public void EndpointConfiguration_WithTimeout()
    {
        var endpoint = new EndpointConfiguration
        {
            BaseAddress = new Uri("https://api.example.com"),
            Timeout = TimeSpan.FromSeconds(30)
        };

        Assert.Equal(TimeSpan.FromSeconds(30), endpoint.Timeout);
    }

    [Fact]
    public void EndpointConfiguration_WithCertificate()
    {
        var cert = new CertificateConfiguration { Type = CertificateType.File, Path = "cert.pfx" };
        var endpoint = new EndpointConfiguration
        {
            BaseAddress = new Uri("https://api.example.com"),
            Certificate = cert
        };

        Assert.Same(cert, endpoint.Certificate);
    }

    [Fact]
    public void EndpointConfiguration_HeadersCaseInsensitive()
    {
        var endpoint = new EndpointConfiguration
        {
            BaseAddress = new Uri("https://api.example.com"),
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-Custom-Header"] = "value"
            }
        };

        Assert.True(endpoint.Headers.TryGetValue("x-custom-header", out var value));
        Assert.Equal("value", value);
    }
}

public class TenantConfigurationTests
{
    [Fact]
    public void TenantConfiguration_DefaultEndpoint_SetterPopulatesEndpoints()
    {
        var config = new TenantConfiguration { TenantId = "t1" };
        var endpoint = new EndpointConfiguration { BaseAddress = new Uri("https://api.example.com") };

        config.DefaultEndpoint = endpoint;

        Assert.Equal(endpoint, config.Endpoints["default"]);
        Assert.Equal("default", config.DefaultEndpointName);
    }

    [Fact]
    public void TenantConfiguration_DefaultEndpoint_GetterReturnsSetEndpoint()
    {
        var config = new TenantConfiguration { TenantId = "t1" };
        var endpoint = new EndpointConfiguration { BaseAddress = new Uri("https://api.example.com") };
        config.DefaultEndpoint = endpoint;

        var retrieved = config.DefaultEndpoint;

        Assert.Same(endpoint, retrieved);
    }

    [Fact]
    public void TenantConfiguration_DefaultEndpoint_NullSetterRemovesEndpoint()
    {
        var config = new TenantConfiguration { TenantId = "t1" };
        config.DefaultEndpoint = new EndpointConfiguration { BaseAddress = new Uri("https://api.example.com") };

        config.DefaultEndpoint = null;

        Assert.DoesNotContain("default", config.Endpoints.Keys);
        Assert.Null(config.DefaultEndpointName);
    }

    [Fact]
    public void TenantConfiguration_DefaultEndpoint_GetterReturnsNull_WhenNoEndpoints()
    {
        var config = new TenantConfiguration { TenantId = "t1" };

        Assert.Null(config.DefaultEndpoint);
    }

    [Fact]
    public void TenantConfiguration_DefaultEndpoint_GetterReturnsSingleEndpoint()
    {
        var config = new TenantConfiguration { TenantId = "t1" };
        var endpoint = new EndpointConfiguration { BaseAddress = new Uri("https://api.example.com") };
        config.Endpoints["custom"] = endpoint;

        var retrieved = config.DefaultEndpoint;

        Assert.Same(endpoint, retrieved);
    }

    [Fact]
    public void TenantConfiguration_DefaultHeaders_IsCaseInsensitive()
    {
        var config = new TenantConfiguration { TenantId = "t1" };
        config.DefaultHeaders["X-Custom-Header"] = "value";

        Assert.True(config.DefaultHeaders.TryGetValue("x-custom-header", out var value));
        Assert.Equal("value", value);
    }

    [Fact]
    public void TenantConfiguration_WithTimeout()
    {
        var config = new TenantConfiguration
        {
            TenantId = "t1",
            Timeout = TimeSpan.FromSeconds(42)
        };

        Assert.Equal(TimeSpan.FromSeconds(42), config.Timeout);
    }

    [Fact]
    public void TenantConfiguration_WithHandlerLifetime()
    {
        var config = new TenantConfiguration
        {
            TenantId = "t1",
            HandlerLifetime = TimeSpan.FromMinutes(5)
        };

        Assert.Equal(TimeSpan.FromMinutes(5), config.HandlerLifetime);
    }
}

public class TenantExceptionTests
{
    [Fact]
    public void TenantNotFoundException_ContainsMessage()
    {
        var ex = new TenantNotFoundException("missing-tenant");

        Assert.Contains("missing-tenant", ex.Message);
    }

    [Fact]
    public void TenantNotResolvedException_ContainsDefaultMessage()
    {
        var ex = new TenantNotResolvedException();

        Assert.NotEmpty(ex.Message);
    }

    [Fact]
    public void TenantNotResolvedException_ContainsCustomMessage()
    {
        var ex = new TenantNotResolvedException("Custom message");

        Assert.Contains("Custom message", ex.Message);
    }
}

public class FactoryErrorHandlingTests
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
    public void CreateClient_ThrowsTenantNotFoundException_WithNullTenantId()
    {
        var (factory, _) = BuildFactory();

        Assert.Throws<ArgumentException>(() => factory.CreateClient(null!));
    }

    [Fact]
    public void CreateClient_ThrowsTenantNotFoundException_WithEmptyTenantId()
    {
        var (factory, _) = BuildFactory();

        Assert.Throws<ArgumentException>(() => factory.CreateClient(""));
    }

    [Fact]
    public void CreateClient_ThrowsTenantNotFoundException_WhenTenantDoesNotExist()
    {
        var (factory, _) = BuildFactory();

        Assert.Throws<TenantNotFoundException>(() => factory.CreateClient("nonexistent"));
    }

    [Fact]
    public void CreateClient_ThrowsInvalidOperationException_WhenNoEndpointConfigured()
    {
        var tenant = new TenantConfiguration { TenantId = "t1" };
        var (factory, _) = BuildFactory(tenant);

        Assert.Throws<InvalidOperationException>(() => factory.CreateClient("t1"));
    }

    [Fact]
    public void CreateClient_ThrowsInvalidOperationException_WhenEndpointNotFound()
    {
        var tenant = new TenantConfiguration
        {
            TenantId = "t1",
            Endpoints = new Dictionary<string, EndpointConfiguration>
            {
                ["api"] = new EndpointConfiguration { BaseAddress = new Uri("https://api.example.com") }
            },
            DefaultEndpointName = "api"
        };
        var (factory, _) = BuildFactory(tenant);

        Assert.Throws<InvalidOperationException>(() => factory.CreateClient("t1", "nonexistent"));
    }

    [Fact]
    public void CreateClient_ThrowsInvalidOperationException_WhenEndpointHasNoBaseAddress()
    {
        var tenant = new TenantConfiguration
        {
            TenantId = "t1",
            Endpoints = new Dictionary<string, EndpointConfiguration>
            {
                ["broken"] = new EndpointConfiguration { } // No BaseAddress
            },
            DefaultEndpointName = "broken"
        };
        var (factory, _) = BuildFactory(tenant);

        Assert.Throws<InvalidOperationException>(() => factory.CreateClient("t1"));
    }

    [Fact]
    public void CreateClient_AppliesEndpointSpecificHeaders_OverridingTenantHeaders()
    {
        var tenant = new TenantConfiguration
        {
            TenantId = "t1",
            DefaultHeaders = new Dictionary<string, string>
            {
                ["X-Custom"] = "tenant-value"
            },
            Endpoints = new Dictionary<string, EndpointConfiguration>
            {
                ["api"] = new EndpointConfiguration
                {
                    BaseAddress = new Uri("https://api.example.com"),
                    Headers = new Dictionary<string, string>
                    {
                        ["X-Custom"] = "endpoint-value"
                    }
                }
            },
            DefaultEndpointName = "api"
        };
        var (factory, _) = BuildFactory(tenant);

        using var client = factory.CreateClient("t1");

        var headerValues = client.DefaultRequestHeaders.GetValues("X-Custom");
        Assert.Single(headerValues);
        Assert.Equal("endpoint-value", headerValues.First());
    }
}
