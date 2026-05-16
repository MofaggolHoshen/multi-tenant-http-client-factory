using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using MultiTenantHttpClientFactory.Abstractions.Models;
using MultiTenantHttpClientFactory.Certificates;
using MultiTenantHttpClientFactory.Configuration;

namespace MultiTenantHttpClientFactory.Tests;

public class HandlerCacheLifecycleTests
{
    private TenantHandlerCache BuildCache(
        string tenantId = "t1",
        TimeSpan? lifetime = null,
        TimeSpan? gracePeriod = null)
    {
        var store = new InMemoryTenantStore(new[]
        {
            new TenantConfiguration
            {
                TenantId = tenantId,
                DefaultEndpoint = new EndpointConfiguration
                {
                    BaseAddress = new Uri("https://example.com")
                }
            }
        });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var configProvider = new TenantConfigurationProvider(
            store, cache, NullLogger<TenantConfigurationProvider>.Instance);

        var certProvider = new CompositeCertificateProvider(
            NullLogger<CompositeCertificateProvider>.Instance);

        return new TenantHandlerCache(
            configProvider,
            certProvider,
            NullLogger<TenantHandlerCache>.Instance,
            defaultHandlerLifetime: lifetime ?? TimeSpan.FromMinutes(2),
            defaultGracePeriod: gracePeriod ?? TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void GetOrCreateHandler_CreatesHandler_OnFirstCall()
    {
        using var cache = BuildCache();

        var handler = cache.GetOrCreateHandler("t1");

        Assert.NotNull(handler);
    }

    [Fact]
    public void GetOrCreateHandler_ReturnsSameWrappedHandler_WithinLifetime()
    {
        using var cache = BuildCache(lifetime: TimeSpan.FromMinutes(10));

        var h1 = cache.GetOrCreateHandler("t1");
        var h2 = cache.GetOrCreateHandler("t1");

        // Both LifetimeTrackingHandlers wrap the same underlying SocketsHttpHandler,
        // so their InnerHandler should be the same reference.
        var inner1 = ((System.Net.Http.DelegatingHandler)h1).InnerHandler;
        var inner2 = ((System.Net.Http.DelegatingHandler)h2).InnerHandler;

        Assert.Same(inner1, inner2);
    }

    [Fact]
    public void GetOrCreateHandler_CreatesNewHandler_AfterExpiry()
    {
        // Use a very short lifetime so it expires immediately
        using var cache = BuildCache(lifetime: TimeSpan.FromMilliseconds(1));

        var h1 = cache.GetOrCreateHandler("t1");
        var inner1 = ((System.Net.Http.DelegatingHandler)h1).InnerHandler;

        Thread.Sleep(50); // Let the handler expire

        var h2 = cache.GetOrCreateHandler("t1");
        var inner2 = ((System.Net.Http.DelegatingHandler)h2).InnerHandler;

        Assert.NotSame(inner1, inner2);
    }

    [Fact]
    public void InvalidateTenant_ForcesNewHandlerNextCall()
    {
        using var cache = BuildCache(lifetime: TimeSpan.FromMinutes(10));

        var h1 = cache.GetOrCreateHandler("t1");
        var inner1 = ((System.Net.Http.DelegatingHandler)h1).InnerHandler;

        cache.InvalidateTenant("t1");

        var h2 = cache.GetOrCreateHandler("t1");
        var inner2 = ((System.Net.Http.DelegatingHandler)h2).InnerHandler;

        Assert.NotSame(inner1, inner2);
    }

    [Fact]
    public async Task GetOrCreateHandler_IsConcurrentlySafe()
    {
        using var cache = BuildCache(lifetime: TimeSpan.FromMinutes(10));

        // Concurrently request handlers and verify no exceptions
        var tasks = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => cache.GetOrCreateHandler("t1")))
            .ToArray();

        var handlers = await Task.WhenAll(tasks);
        Assert.All(handlers, h => Assert.NotNull(h));

        // All concurrent requests get the same underlying handler
        var firstInner = ((System.Net.Http.DelegatingHandler)handlers[0]).InnerHandler;
        Assert.All(handlers, h => Assert.Same(firstInner, ((System.Net.Http.DelegatingHandler)h).InnerHandler));
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var cache = BuildCache();
        cache.GetOrCreateHandler("t1"); // create at least one

        var ex = Record.Exception(() => cache.Dispose());

        Assert.Null(ex);
    }

    [Fact]
    public void GetOrCreateHandler_Throws_AfterDispose()
    {
        var cache = BuildCache();
        cache.Dispose();

        Assert.Throws<ObjectDisposedException>(() => cache.GetOrCreateHandler("t1"));
    }
}
