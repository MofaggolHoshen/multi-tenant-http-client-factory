using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using MultiTenantHttpClientFactory.Abstractions;
using MultiTenantHttpClientFactory.Abstractions.Models;
using MultiTenantHttpClientFactory.Certificates;
using MultiTenantHttpClientFactory.Configuration;
using MultiTenantHttpClientFactory.HotReload;

namespace MultiTenantHttpClientFactory.Tests;

public class HotReloadIntegrationTests
{
    private static (ITenantHttpClientFactory factory, InMemoryTenantStore store, MemoryCache cache) BuildStack()
    {
        var store = new InMemoryTenantStore();
        var cache = new MemoryCache(new MemoryCacheOptions());

        var configProvider = new TenantConfigurationProvider(
            store, cache, NullLogger<TenantConfigurationProvider>.Instance);

        var certProvider = new CompositeCertificateProvider(
            NullLogger<CompositeCertificateProvider>.Instance);

        var handlerCache = new TenantHandlerCache(
            configProvider, certProvider, NullLogger<TenantHandlerCache>.Instance,
            defaultHandlerLifetime: TimeSpan.FromHours(1));

        var tenantContext = new TenantContext();

        var factory = new TenantHttpClientFactory(
            configProvider, handlerCache, tenantContext, NullLogger<TenantHttpClientFactory>.Instance);

        return (factory, store, cache);
    }

    [Fact]
    public async Task HotReload_UpdatedBaseAddress_AppliedAfterChange()
    {
        var (factory, store, cache) = BuildStack();

        store.AddOrUpdate(new TenantConfiguration
        {
            TenantId = "t1",
            DefaultEndpoint = new EndpointConfiguration { BaseAddress = new Uri("https://v1.example.com") }
        });

        using var client1 = factory.CreateClient("t1", null);
        Assert.Equal(new Uri("https://v1.example.com"), client1.BaseAddress);

        // Simulate config change
        store.AddOrUpdate(new TenantConfiguration
        {
            TenantId = "t1",
            DefaultEndpoint = new EndpointConfiguration { BaseAddress = new Uri("https://v2.example.com") }
        });

        // Give callbacks time to fire
        await Task.Delay(100);

        using var client2 = factory.CreateClient("t1", null);
        Assert.Equal(new Uri("https://v2.example.com"), client2.BaseAddress);
    }

    [Fact]
    public async Task HotReload_RemovedTenant_ThrowsTenantNotFound()
    {
        var (factory, store, _) = BuildStack();

        store.AddOrUpdate(new TenantConfiguration
        {
            TenantId = "removable",
            DefaultEndpoint = new EndpointConfiguration { BaseAddress = new Uri("https://example.com") }
        });

        // Confirm it works before removal
        using var client1 = factory.CreateClient("removable", null);
        Assert.NotNull(client1);

        // Remove the tenant
        store.Remove("removable");
        await Task.Delay(100);

        Assert.Throws<TenantNotFoundException>(() => factory.CreateClient("removable", null));
    }

    [Fact]
    public void PollingChangeToken_FiresCallback_WhenDelegatReturnsTrue()
    {
        var callCount = 0;
        var shouldFire = false;
        bool fired = false;

        using var token = new PollingChangeToken(
            async ct =>
            {
                callCount++;
                await Task.Delay(1, ct);
                return shouldFire;
            },
            pollIntervalMs: 10); // Very fast polling for test

        token.RegisterChangeCallback(_ => fired = true, null);

        Thread.Sleep(50); // Wait for a poll cycle
        Assert.False(fired);

        shouldFire = true;

        // Wait for it to detect the change
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!fired && DateTime.UtcNow < deadline)
            Thread.Sleep(20);

        Assert.True(fired);
        Assert.True(token.HasChanged);
    }

    [Fact]
    public void PollingChangeToken_StopsPolling_AfterFiring()
    {
        var callCount = 0;

        using var token = new PollingChangeToken(
            async ct =>
            {
                callCount++;
                await Task.Delay(1, ct);
                return true; // Always returns change
            },
            pollIntervalMs: 20);

        token.RegisterChangeCallback(_ => { }, null);

        // Wait for the token to fire once
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!token.HasChanged && DateTime.UtcNow < deadline)
            Thread.Sleep(10);

        var countAfterFiring = callCount;
        Thread.Sleep(100); // Further waiting

        // Should not have polled significantly more after firing
        Assert.True(callCount <= countAfterFiring + 2, $"Poll count grew from {countAfterFiring} to {callCount} after firing");
    }
}
