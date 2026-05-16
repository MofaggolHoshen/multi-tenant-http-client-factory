using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using MultiTenantHttpClientFactory.Abstractions.Models;
using MultiTenantHttpClientFactory.Configuration;

namespace MultiTenantHttpClientFactory.Tests;

public class ConfigurationProviderTests
{
    // ─── InMemoryTenantStore ──────────────────────────────────────────────────

    [Fact]
    public async Task InMemoryStore_ReturnsTenant_WhenAdded()
    {
        var store = new InMemoryTenantStore();
        var config = new TenantConfiguration { TenantId = "tenant-1" };
        store.AddOrUpdate(config);

        var result = await store.GetTenantAsync("tenant-1");

        Assert.NotNull(result);
        Assert.Equal("tenant-1", result.TenantId);
    }

    [Fact]
    public async Task InMemoryStore_ReturnsNull_WhenTenantNotFound()
    {
        var store = new InMemoryTenantStore();

        var result = await store.GetTenantAsync("unknown");

        Assert.Null(result);
    }

    [Fact]
    public async Task InMemoryStore_UpdatesTenant()
    {
        var store = new InMemoryTenantStore();
        var original = new TenantConfiguration { TenantId = "t1", DefaultEndpoint = new EndpointConfiguration { BaseAddress = new Uri("https://v1.example.com") } };
        store.AddOrUpdate(original);

        var updated = new TenantConfiguration { TenantId = "t1", DefaultEndpoint = new EndpointConfiguration { BaseAddress = new Uri("https://v2.example.com") } };
        store.AddOrUpdate(updated);

        var result = await store.GetTenantAsync("t1");
        Assert.Equal(new Uri("https://v2.example.com"), result!.DefaultEndpoint!.BaseAddress);
    }

    [Fact]
    public async Task InMemoryStore_RemovesTenant()
    {
        var store = new InMemoryTenantStore();
        store.AddOrUpdate(new TenantConfiguration { TenantId = "t1" });
        store.Remove("t1");

        var result = await store.GetTenantAsync("t1");
        Assert.Null(result);
    }

    [Fact]
    public async Task InMemoryStore_ReturnsAllTenants()
    {
        var store = new InMemoryTenantStore(new[]
        {
            new TenantConfiguration { TenantId = "a" },
            new TenantConfiguration { TenantId = "b" },
        });

        var all = await store.GetAllTenantsAsync();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, t => t.TenantId == "a");
        Assert.Contains(all, t => t.TenantId == "b");
    }

    [Fact]
    public void InMemoryStore_ChangeToken_FiresOnAddOrUpdate()
    {
        var store = new InMemoryTenantStore();
        var token = store.GetReloadToken();
        bool fired = false;
        token.RegisterChangeCallback(_ => fired = true, null);

        store.AddOrUpdate(new TenantConfiguration { TenantId = "new" });

        Assert.True(fired);
    }

    [Fact]
    public void InMemoryStore_ChangeToken_FiresOnRemove()
    {
        var store = new InMemoryTenantStore(new[] { new TenantConfiguration { TenantId = "t1" } });
        var token = store.GetReloadToken();
        bool fired = false;
        token.RegisterChangeCallback(_ => fired = true, null);

        store.Remove("t1");

        Assert.True(fired);
    }

    [Fact]
    public void InMemoryStore_ChangeToken_IsConsumedOnce()
    {
        // A CancellationChangeToken fires once; the next GetReloadToken() returns a fresh token
        var store = new InMemoryTenantStore();
        var token1 = store.GetReloadToken();
        store.AddOrUpdate(new TenantConfiguration { TenantId = "t1" });

        Assert.True(token1.HasChanged);

        var token2 = store.GetReloadToken();
        Assert.False(token2.HasChanged);
    }

    // ─── TenantConfigurationProvider ──────────────────────────────────────────

    [Fact]
    public async Task ConfigProvider_ReturnsTenant_FromStore()
    {
        var store = new InMemoryTenantStore(new[] { new TenantConfiguration { TenantId = "cached-tenant" } });
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new TenantConfigurationProvider(store, cache, NullLogger<TenantConfigurationProvider>.Instance);

        var result = await provider.GetConfigurationAsync("cached-tenant");

        Assert.NotNull(result);
        Assert.Equal("cached-tenant", result.TenantId);
    }

    [Fact]
    public async Task ConfigProvider_CachesSecondCall()
    {
        var callCount = 0;
        var trackingStore = new TrackingTenantStore(
            new TenantConfiguration { TenantId = "t1" },
            onGet: () => callCount++);

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new TenantConfigurationProvider(trackingStore, cache, NullLogger<TenantConfigurationProvider>.Instance);

        await provider.GetConfigurationAsync("t1");
        await provider.GetConfigurationAsync("t1");

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task ConfigProvider_InvalidatesOnStoreChange()
    {
        var ep1 = new EndpointConfiguration { BaseAddress = new Uri("https://v1.example.com") };
        var ep2 = new EndpointConfiguration { BaseAddress = new Uri("https://v2.example.com") };
        var store = new InMemoryTenantStore(new[] { new TenantConfiguration { TenantId = "t1", DefaultEndpoint = ep1 } });
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new TenantConfigurationProvider(store, cache, NullLogger<TenantConfigurationProvider>.Instance);

        var first = await provider.GetConfigurationAsync("t1");
        Assert.Equal(new Uri("https://v1.example.com"), first!.DefaultEndpoint!.BaseAddress);

        // Update the store (fires change token)
        store.AddOrUpdate(new TenantConfiguration { TenantId = "t1", DefaultEndpoint = ep2 });

        // Give change token callbacks a moment to propagate
        await Task.Delay(50);

        var second = await provider.GetConfigurationAsync("t1");
        Assert.Equal(new Uri("https://v2.example.com"), second!.DefaultEndpoint!.BaseAddress);
    }

    [Fact]
    public async Task ConfigProvider_ReturnsNull_ForUnknownTenant()
    {
        var store = new InMemoryTenantStore();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new TenantConfigurationProvider(store, cache, NullLogger<TenantConfigurationProvider>.Instance);

        var result = await provider.GetConfigurationAsync("unknown");

        Assert.Null(result);
    }

    [Fact]
    public async Task ConfigProvider_ThrowsArgumentException_WhenTenantIdEmpty()
    {
        var store = new InMemoryTenantStore();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new TenantConfigurationProvider(store, cache, NullLogger<TenantConfigurationProvider>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetConfigurationAsync(""));
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private class TrackingTenantStore : MultiTenantHttpClientFactory.Abstractions.ITenantStore
    {
        private readonly TenantConfiguration _config;
        private readonly Action _onGet;

        public TrackingTenantStore(TenantConfiguration config, Action onGet)
        {
            _config = config;
            _onGet = onGet;
        }

        public Task<TenantConfiguration?> GetTenantAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            _onGet();
            return Task.FromResult<TenantConfiguration?>(
                tenantId == _config.TenantId ? _config : null);
        }

        public Task<IReadOnlyList<TenantConfiguration>> GetAllTenantsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TenantConfiguration>>(new[] { _config });

        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken()
            => new Microsoft.Extensions.Primitives.CancellationChangeToken(new CancellationToken(false));
    }
}
