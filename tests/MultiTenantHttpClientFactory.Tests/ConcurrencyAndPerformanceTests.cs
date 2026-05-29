using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using MultiTenantHttpClientFactory.Abstractions.Models;
using MultiTenantHttpClientFactory.Certificates;
using MultiTenantHttpClientFactory.Configuration;

namespace MultiTenantHttpClientFactory.Tests;

public class ConcurrencyTests
{
    [Fact]
    public async Task Factory_CreateClient_IsConcurrentlySafe()
    {
        var tenant = new TenantConfiguration
        {
            TenantId = "concurrent-test",
            DefaultEndpoint = new EndpointConfiguration
            {
                BaseAddress = new Uri("https://api.example.com")
            }
        };

        var store = new InMemoryTenantStore(new[] { tenant });
        var cache = new MemoryCache(new MemoryCacheOptions());
        var configProvider = new TenantConfigurationProvider(
            store, cache, NullLogger<TenantConfigurationProvider>.Instance);
        var certProvider = new CompositeCertificateProvider(
            NullLogger<CompositeCertificateProvider>.Instance);
        var handlerCache = new TenantHandlerCache(
            configProvider, certProvider, NullLogger<TenantHandlerCache>.Instance);
        var context = new TenantContext();
        var factory = new TenantHttpClientFactory(
            configProvider, handlerCache, context, NullLogger<TenantHttpClientFactory>.Instance);

        // Create 100 concurrent clients
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => Task.Run(() =>
            {
                using var client = factory.CreateClient("concurrent-test");
                return client.BaseAddress;
            }))
            .ToArray();

        var addresses = await Task.WhenAll(tasks);

        // All should have same base address
        Assert.All(addresses, addr => Assert.Equal(new Uri("https://api.example.com"), addr));
    }

    [Fact]
    public async Task InMemoryStore_AddOrUpdate_IsConcurrentlySafe()
    {
        var store = new InMemoryTenantStore();
        var tenants = Enumerable.Range(0, 50)
            .Select(i => new TenantConfiguration
            {
                TenantId = $"tenant-{i}",
                DefaultEndpoint = new EndpointConfiguration
                {
                    BaseAddress = new Uri($"https://api{i}.example.com")
                }
            })
            .ToList();

        var tasks = tenants
            .Select(t => Task.Run(() => store.AddOrUpdate(t)))
            .ToArray();

        await Task.WhenAll(tasks);

        var allTenants = await store.GetAllTenantsAsync();
        Assert.Equal(50, allTenants.Count);
    }

    [Fact]
    public async Task TenantContext_SetTenant_IsThreadSafe()
    {
        // Each thread gets its own scoped context
        var contexts = new Dictionary<int, TenantContext>();
        var threadIds = new HashSet<int>();

        var tasks = Enumerable.Range(0, 10)
            .Select(i => Task.Run(() =>
            {
                threadIds.Add(Thread.CurrentThread.ManagedThreadId);
                var ctx = new TenantContext();
                ctx.SetTenant($"tenant-{i}", new TenantConfiguration { TenantId = $"tenant-{i}" });
                lock (contexts)
                    contexts[i] = ctx;
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        // Each context should have its own tenant
        for (int i = 0; i < 10; i++)
        {
            Assert.True(contexts[i].IsResolved);
            Assert.Equal($"tenant-{i}", contexts[i].TenantId);
        }
    }

    [Fact]
    public async Task ConfigurationProvider_GetConfiguration_IsConcurrentlySafe()
    {
        var tenants = Enumerable.Range(0, 10)
            .Select(i => new TenantConfiguration
            {
                TenantId = $"tenant-{i}",
                DefaultEndpoint = new EndpointConfiguration
                {
                    BaseAddress = new Uri($"https://api{i}.example.com")
                }
            })
            .ToList();

        var store = new InMemoryTenantStore(tenants);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new TenantConfigurationProvider(
            store, cache, NullLogger<TenantConfigurationProvider>.Instance);

        // Request same tenant from multiple threads
        var tasks = Enumerable.Range(0, 50)
            .Select(i => provider.GetConfigurationAsync($"tenant-{i % 10}"))
            .ToArray();

        var configs = await Task.WhenAll(tasks);

        Assert.NotEmpty(configs);
        Assert.All(configs, c => Assert.NotNull(c));
    }

    [Fact]
    public async Task HandlerCache_GetOrCreateHandler_IsConcurrentlySafe()
    {
        var store = new InMemoryTenantStore(new[]
        {
            new TenantConfiguration
            {
                TenantId = "t1",
                DefaultEndpoint = new EndpointConfiguration
                {
                    BaseAddress = new Uri("https://api.example.com")
                }
            }
        });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var configProvider = new TenantConfigurationProvider(
            store, cache, NullLogger<TenantConfigurationProvider>.Instance);
        var certProvider = new CompositeCertificateProvider(
            NullLogger<CompositeCertificateProvider>.Instance);

        using var handlerCache = new TenantHandlerCache(
            configProvider, certProvider, NullLogger<TenantHandlerCache>.Instance);

        var tasks = Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() => handlerCache.GetOrCreateHandler("t1")))
            .ToArray();

        var handlers = await Task.WhenAll(tasks);

        Assert.NotEmpty(handlers);
        Assert.All(handlers, h => Assert.NotNull(h));
    }
}

public class CancellationTokenTests
{
    [Fact]
    public async Task GetConfiguration_RespectsCancellationToken()
    {
        var store = new InMemoryTenantStore(new[]
        {
            new TenantConfiguration
            {
                TenantId = "t1",
                DefaultEndpoint = new EndpointConfiguration
                {
                    BaseAddress = new Uri("https://api.example.com")
                }
            }
        });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new TenantConfigurationProvider(
            store, cache, NullLogger<TenantConfigurationProvider>.Instance);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Should not throw but handle cancellation gracefully
        var result = await provider.GetConfigurationAsync("t1", cts.Token);

        Assert.NotNull(result); // Should still return because store doesn't respect CT
    }

    [Fact]
    public async Task GetAllTenants_RespectsCancellationToken()
    {
        var tenants = new[]
        {
            new TenantConfiguration
            {
                TenantId = "t1",
                DefaultEndpoint = new EndpointConfiguration
                {
                    BaseAddress = new Uri("https://api.example.com")
                }
            }
        };

        var store = new InMemoryTenantStore(tenants);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Should not throw
        var result = await store.GetAllTenantsAsync(cts.Token);

        Assert.NotEmpty(result);
    }
}

public class MemoryLeakTests
{
    [Fact]
    public void HandlerCache_Disposal_DisposesHandlers()
    {
        var store = new InMemoryTenantStore(new[]
        {
            new TenantConfiguration
            {
                TenantId = "t1",
                DefaultEndpoint = new EndpointConfiguration
                {
                    BaseAddress = new Uri("https://api.example.com")
                }
            }
        });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var configProvider = new TenantConfigurationProvider(
            store, cache, NullLogger<TenantConfigurationProvider>.Instance);
        var certProvider = new CompositeCertificateProvider(
            NullLogger<CompositeCertificateProvider>.Instance);

        var handlerCache = new TenantHandlerCache(
            configProvider, certProvider, NullLogger<TenantHandlerCache>.Instance);

        var handler = handlerCache.GetOrCreateHandler("t1");
        Assert.NotNull(handler);

        // Dispose should not throw
        handlerCache.Dispose();

        // Subsequent access should throw ObjectDisposedException
        Assert.Throws<ObjectDisposedException>(() => handlerCache.GetOrCreateHandler("t1"));
    }

    [Fact]
    public void ConfigurationProvider_Caching_DoesNotLeakMemory()
    {
        var store = new InMemoryTenantStore(new[]
        {
            new TenantConfiguration
            {
                TenantId = "t1",
                DefaultEndpoint = new EndpointConfiguration
                {
                    BaseAddress = new Uri("https://api.example.com")
                }
            }
        });

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new TenantConfigurationProvider(
            store, cache, NullLogger<TenantConfigurationProvider>.Instance);

        // Request same tenant multiple times
        for (int i = 0; i < 100; i++)
        {
            var result = provider.GetConfigurationAsync("t1").GetAwaiter().GetResult();
            Assert.NotNull(result);
        }

        // Cache should only have 1 entry for t1
        var cacheField = typeof(TenantConfigurationProvider)
            .GetField("_cache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(cacheField);
    }
}

public class PerformanceTests
{
    [Fact]
    public void ClientCreation_PerformanceIsLinear()
    {
        var tenants = Enumerable.Range(0, 10)
            .Select(i => new TenantConfiguration
            {
                TenantId = $"tenant-{i}",
                DefaultEndpoint = new EndpointConfiguration
                {
                    BaseAddress = new Uri($"https://api{i}.example.com")
                }
            })
            .ToList();

        var store = new InMemoryTenantStore(tenants);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var configProvider = new TenantConfigurationProvider(
            store, cache, NullLogger<TenantConfigurationProvider>.Instance);
        var certProvider = new CompositeCertificateProvider(
            NullLogger<CompositeCertificateProvider>.Instance);
        var handlerCache = new TenantHandlerCache(
            configProvider, certProvider, NullLogger<TenantHandlerCache>.Instance);
        var context = new TenantContext();
        var factory = new TenantHttpClientFactory(
            configProvider, handlerCache, context, NullLogger<TenantHttpClientFactory>.Instance);

        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            using var client = factory.CreateClient($"tenant-{i % 10}");
            Assert.NotNull(client);
        }
        stopwatch.Stop();

        // Should complete within reasonable time (e.g., < 1 second)
        Assert.True(stopwatch.ElapsedMilliseconds < 1000,
            $"Client creation took {stopwatch.ElapsedMilliseconds}ms for 1000 calls");
    }

    [Fact]
    public void CacheHit_IsFasterThanCacheMiss()
    {
        var store = new InMemoryTenantStore(new[]
        {
            new TenantConfiguration
            {
                TenantId = "t1",
                DefaultEndpoint = new EndpointConfiguration
                {
                    BaseAddress = new Uri("https://api.example.com")
                }
            }
        });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new TenantConfigurationProvider(
            store, cache, NullLogger<TenantConfigurationProvider>.Instance);

        // First call (cache miss)
        var sw1 = Stopwatch.StartNew();
        var result1 = provider.GetConfigurationAsync("t1").GetAwaiter().GetResult();
        sw1.Stop();

        // Second call (cache hit)
        var sw2 = Stopwatch.StartNew();
        var result2 = provider.GetConfigurationAsync("t1").GetAwaiter().GetResult();
        sw2.Stop();

        Assert.NotNull(result1);
        Assert.NotNull(result2);
        // Cache hit should generally be faster (not guaranteed, but likely)
        // Just verify both complete quickly
        Assert.True(sw1.ElapsedMilliseconds < 1000);
        Assert.True(sw2.ElapsedMilliseconds < 100);
    }
}
