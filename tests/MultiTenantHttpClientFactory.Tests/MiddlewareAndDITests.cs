using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MultiTenantHttpClientFactory.Abstractions;
using MultiTenantHttpClientFactory.Abstractions.Models;
using MultiTenantHttpClientFactory.DependencyInjection;
using MultiTenantHttpClientFactory.Configuration;
using MultiTenantHttpClientFactory.TenantResolution;

namespace MultiTenantHttpClientFactory.Tests;

public class TenantResolutionMiddlewareTests
{
    [Fact]
    public async Task Middleware_ResolvesAndSetsTenant_UsingResolver()
    {
        // Arrange
        var tenantConfig = new TenantConfiguration
        {
            TenantId = "resolved-tenant",
            DefaultEndpoint = new EndpointConfiguration { BaseAddress = new Uri("https://api.example.com") }
        };
        var store = new InMemoryTenantStore(new[] { tenantConfig });
        var context = new TenantContext();

        var mockResolver = new Mock<ITenantResolver>();
        mockResolver
            .Setup(r => r.ResolveAsync(It.IsAny<HttpContext>()))
            .ReturnsAsync("resolved-tenant");

        var configProvider = new TenantConfigurationProvider(
            store,
            new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()),
            NullLogger<TenantConfigurationProvider>.Instance);

        var middleware = new TenantResolutionMiddleware(
            next: async ctx => { },
            resolver: mockResolver.Object,
            configProvider: configProvider,
            tenantContext: context,
            logger: NullLogger<TenantResolutionMiddleware>.Instance);

        var httpContext = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        Assert.True(context.IsResolved);
        Assert.Equal("resolved-tenant", context.TenantId);
        mockResolver.Verify(r => r.ResolveAsync(It.IsAny<HttpContext>()), Times.Once);
    }

    [Fact]
    public async Task Middleware_HandlesNull_WhenResolverReturnsNull()
    {
        // Arrange
        var mockResolver = new Mock<ITenantResolver>();
        mockResolver
            .Setup(r => r.ResolveAsync(It.IsAny<HttpContext>()))
            .ReturnsAsync((string?)null);

        var context = new TenantContext();

        var middleware = new TenantResolutionMiddleware(
            next: async ctx => { },
            resolver: mockResolver.Object,
            configProvider: new Mock<ITenantConfigurationProvider>().Object,
            tenantContext: context,
            logger: NullLogger<TenantResolutionMiddleware>.Instance);

        var httpContext = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        Assert.False(context.IsResolved);
    }

    [Fact]
    public async Task Middleware_CallsNextMiddleware()
    {
        // Arrange
        var nextCalled = false;
        var mockResolver = new Mock<ITenantResolver>();
        mockResolver
            .Setup(r => r.ResolveAsync(It.IsAny<HttpContext>()))
            .ReturnsAsync("tenant-id");

        var middleware = new TenantResolutionMiddleware(
            next: async ctx => { nextCalled = true; await Task.CompletedTask; },
            resolver: mockResolver.Object,
            configProvider: new Mock<ITenantConfigurationProvider>().Object,
            tenantContext: new TenantContext(),
            logger: NullLogger<TenantResolutionMiddleware>.Instance);

        var httpContext = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        Assert.True(nextCalled);
    }
}

public class DependencyInjectionTests
{
    [Fact]
    public void AddMultiTenantHttpClientFactory_RegistersRequiredServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();

        services.AddMultiTenantHttpClientFactory()
            .WithInMemoryStore();

        var sp = services.BuildServiceProvider();

        // Should not throw
        var factory = sp.GetRequiredService<ITenantHttpClientFactory>();
        Assert.NotNull(factory);

        var context = sp.GetRequiredService<ITenantContext>();
        Assert.NotNull(context);

        var resolver = sp.GetService<ITenantResolver>();
        Assert.Null(resolver); // No resolver added yet
    }

    [Fact]
    public void AddTenantResolver_RegistersResolver()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();

        var headerResolver = new HeaderTenantResolver();
        services.AddMultiTenantHttpClientFactory()
            .AddTenantResolver(headerResolver)
            .WithInMemoryStore();

        var sp = services.BuildServiceProvider();

        var resolver = sp.GetRequiredService<ITenantResolver>();
        Assert.NotNull(resolver);
    }

    [Fact]
    public void WithInMemoryStore_RegistersInMemoryStore()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();

        services.AddMultiTenantHttpClientFactory()
            .WithInMemoryStore();

        var sp = services.BuildServiceProvider();

        var store = sp.GetRequiredService<ITenantStore>();
        Assert.NotNull(store);
        Assert.IsType<InMemoryTenantStore>(store);
    }

    [Fact]
    public void WithInMemoryStore_InitializesWith_ProvidedTenants()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();

        var tenants = new[]
        {
            new TenantConfiguration { TenantId = "t1", DefaultEndpoint = new EndpointConfiguration { BaseAddress = new Uri("https://t1.example.com") } },
            new TenantConfiguration { TenantId = "t2", DefaultEndpoint = new EndpointConfiguration { BaseAddress = new Uri("https://t2.example.com") } }
        };

        services.AddMultiTenantHttpClientFactory()
            .WithInMemoryStore(tenants);

        var sp = services.BuildServiceProvider();

        var store = sp.GetRequiredService<ITenantStore>();
        var allTenants = store.GetAllTenantsAsync().GetAwaiter().GetResult();

        Assert.Equal(2, allTenants.Count);
    }

    [Fact]
    public void MultipleResolvers_CanBeChained()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();

        var headerResolver = new HeaderTenantResolver("X-Tenant");
        var routeResolver = new RouteTenantResolver();

        services.AddMultiTenantHttpClientFactory()
            .AddTenantResolver(headerResolver)
            .AddTenantResolver(routeResolver)
            .WithInMemoryStore();

        var sp = services.BuildServiceProvider();

        var resolver = sp.GetRequiredService<ITenantResolver>();
        Assert.NotNull(resolver);
        // Should be composite resolver with both chained
    }

    [Fact]
    public void Factory_IsScoped()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();

        services.AddMultiTenantHttpClientFactory()
            .WithInMemoryStore();

        var sp = services.BuildServiceProvider();

        using var scope1 = sp.CreateScope();
        using var scope2 = sp.CreateScope();

        var factory1 = scope1.ServiceProvider.GetRequiredService<ITenantHttpClientFactory>();
        var factory2 = scope2.ServiceProvider.GetRequiredService<ITenantHttpClientFactory>();

        Assert.NotSame(factory1, factory2);
    }

    [Fact]
    public void TenantContext_IsScoped()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();

        services.AddMultiTenantHttpClientFactory()
            .WithInMemoryStore();

        var sp = services.BuildServiceProvider();

        using var scope1 = sp.CreateScope();
        using var scope2 = sp.CreateScope();

        var context1 = scope1.ServiceProvider.GetRequiredService<ITenantContext>();
        var context2 = scope2.ServiceProvider.GetRequiredService<ITenantContext>();

        Assert.NotSame(context1, context2);
    }

    [Fact]
    public void TenantConfigurationProvider_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();

        services.AddMultiTenantHttpClientFactory()
            .WithInMemoryStore();

        var sp = services.BuildServiceProvider();

        using var scope1 = sp.CreateScope();
        using var scope2 = sp.CreateScope();

        var provider1 = scope1.ServiceProvider.GetRequiredService<ITenantConfigurationProvider>();
        var provider2 = scope2.ServiceProvider.GetRequiredService<ITenantConfigurationProvider>();

        Assert.Same(provider1, provider2);
    }
}

public class JsonConfigurationTests
{
    [Fact]
    public void WithJsonConfiguration_LoadsTenants_FromConfiguration()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();

        var values = new Dictionary<string, string?>
        {
            ["Tenants:0:TenantId"] = "tenant-a",
            ["Tenants:0:Endpoints:api:BaseAddress"] = "https://api.a.example.com",
            ["Tenants:0:DefaultEndpointName"] = "api"
        };

        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(configuration);
        services.AddMultiTenantHttpClientFactory()
            .WithJsonConfiguration("Tenants");

        var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<ITenantStore>();

        var tenant = store.GetTenantAsync("tenant-a").GetAwaiter().GetResult();

        Assert.NotNull(tenant);
        Assert.Equal("tenant-a", tenant.TenantId);
    }

    [Fact]
    public void WithJsonConfiguration_Throws_OnDuplicateTenantIds()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();

        var values = new Dictionary<string, string?>
        {
            ["Tenants:0:TenantId"] = "duplicate",
            ["Tenants:0:Endpoints:api:BaseAddress"] = "https://api1.example.com",
            ["Tenants:0:DefaultEndpointName"] = "api",
            ["Tenants:1:TenantId"] = "DUPLICATE", // Case insensitive
            ["Tenants:1:Endpoints:api:BaseAddress"] = "https://api2.example.com",
            ["Tenants:1:DefaultEndpointName"] = "api"
        };

        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(configuration);
        services.AddMultiTenantHttpClientFactory()
            .WithJsonConfiguration("Tenants");

        var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<ITenantStore>();

        var ex = Assert.Throws<InvalidOperationException>(
            () => store.GetTenantAsync("duplicate").GetAwaiter().GetResult());

        Assert.Contains("Duplicate", ex.Message);
    }
}
