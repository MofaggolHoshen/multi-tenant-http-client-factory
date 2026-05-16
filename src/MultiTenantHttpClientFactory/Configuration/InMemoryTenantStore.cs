using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using MultiTenantHttpClientFactory.Abstractions;
using MultiTenantHttpClientFactory.Abstractions.Models;

namespace MultiTenantHttpClientFactory.Configuration;

/// <summary>
/// In-memory tenant store backed by a ConcurrentDictionary.
/// Useful for testing and scenarios where configuration comes from code.
/// </summary>
public class InMemoryTenantStore : ITenantStore
{
    private readonly ConcurrentDictionary<string, TenantConfiguration> _tenants;
    private CancellationTokenSource _changeTokenSource;
    private readonly ILogger<InMemoryTenantStore>? _logger;

    public InMemoryTenantStore(ILogger<InMemoryTenantStore>? logger = null)
    {
        _tenants = new ConcurrentDictionary<string, TenantConfiguration>();
        _changeTokenSource = new CancellationTokenSource();
        _logger = logger;
    }

    public InMemoryTenantStore(
        IEnumerable<TenantConfiguration> initialTenants,
        ILogger<InMemoryTenantStore>? logger = null)
        : this(logger)
    {
        if (initialTenants != null)
        {
            foreach (var tenant in initialTenants)
            {
                if (tenant?.TenantId != null)
                    _tenants.TryAdd(tenant.TenantId, tenant);
            }
        }
    }

    public Task<TenantConfiguration?> GetTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        _tenants.TryGetValue(tenantId, out var config);
        return Task.FromResult(config);
    }

    public Task<IReadOnlyList<TenantConfiguration>> GetAllTenantsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<TenantConfiguration>>(_tenants.Values.ToList());
    }

    public IChangeToken GetReloadToken()
    {
        return new CancellationChangeToken(_changeTokenSource.Token);
    }

    /// <summary>
    /// Adds or updates a tenant configuration.
    /// </summary>
    public void AddOrUpdate(TenantConfiguration config)
    {
        if (config?.TenantId == null)
            throw new ArgumentException("Tenant configuration must have a TenantId");

        _tenants.AddOrUpdate(config.TenantId, config, (_, _) => config);
        FireChangeToken();
        _logger?.LogDebug("Tenant {TenantId} added or updated", config.TenantId);
    }

    /// <summary>
    /// Removes a tenant configuration.
    /// </summary>
    public void Remove(string tenantId)
    {
        if (!string.IsNullOrEmpty(tenantId) && _tenants.TryRemove(tenantId, out _))
        {
            FireChangeToken();
            _logger?.LogDebug("Tenant {TenantId} removed", tenantId);
        }
    }

    private void FireChangeToken()
    {
        // Swap in new source BEFORE canceling old one.
        // Callbacks registered on CancellationChangeToken fire synchronously during Cancel().
        // If those callbacks call GetReloadToken(), they must see the fresh (unfired) token
        // to avoid registering on an already-fired token, which would re-fire immediately → stack overflow.
        var oldSource = Interlocked.Exchange(ref _changeTokenSource, new CancellationTokenSource());
        try
        {
            oldSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }
        finally
        {
            oldSource.Dispose();
        }
    }
}
