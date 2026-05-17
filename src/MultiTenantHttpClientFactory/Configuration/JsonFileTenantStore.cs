using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using MultiTenantHttpClientFactory.Abstractions;
using MultiTenantHttpClientFactory.Abstractions.Models;

namespace MultiTenantHttpClientFactory.Configuration;

/// <summary>
/// Tenant store that reads configuration from IConfiguration (typically appsettings.json).
/// </summary>
internal class JsonFileTenantStore : ITenantStore
{
    private readonly IOptionsMonitor<JsonTenantStoreOptions> _optionsMonitor;
    private readonly ILogger<JsonFileTenantStore> _logger;
    private CancellationTokenSource _changeTokenSource;
    private IDisposable? _changeSubscription;

    public JsonFileTenantStore(
        IOptionsMonitor<JsonTenantStoreOptions> optionsMonitor,
        ILogger<JsonFileTenantStore> logger)
    {
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _changeTokenSource = new CancellationTokenSource();

        // Subscribe to configuration changes
        _changeSubscription = optionsMonitor.OnChange((options, name) =>
        {
            _logger.LogInformation("Tenant configuration changed");
            FireChangeToken();
        });
    }

    public Task<TenantConfiguration?> GetTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var tenants = _optionsMonitor.CurrentValue.Tenants;
        if (!tenants.TryGetValue(tenantId, out var config))
            return Task.FromResult<TenantConfiguration?>(null);

        return Task.FromResult<TenantConfiguration?>(config);
    }

    public Task<IReadOnlyList<TenantConfiguration>> GetAllTenantsAsync(CancellationToken cancellationToken = default)
    {
        var tenants = _optionsMonitor.CurrentValue.Tenants;
        return Task.FromResult<IReadOnlyList<TenantConfiguration>>(tenants.Values.ToList());
    }

    public IChangeToken GetReloadToken()
    {
        return new CancellationChangeToken(_changeTokenSource.Token);
    }

    private void FireChangeToken()
    {
        try
        {
            _changeTokenSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }

        _changeTokenSource = new CancellationTokenSource();
    }

    public void Dispose()
    {
        _changeSubscription?.Dispose();
        _changeTokenSource?.Dispose();
    }
}
