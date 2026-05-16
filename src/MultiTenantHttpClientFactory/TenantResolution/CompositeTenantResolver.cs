using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MultiTenantHttpClientFactory.Abstractions;

namespace MultiTenantHttpClientFactory.TenantResolution;

/// <summary>
/// Orchestrates multiple ITenantResolver instances in registration order.
/// Returns the first non-null result.
/// </summary>
internal class CompositeTenantResolver : ITenantResolver
{
    private readonly IReadOnlyList<ITenantResolver> _resolvers;
    private readonly ILogger<CompositeTenantResolver> _logger;

    public CompositeTenantResolver(IEnumerable<ITenantResolver> resolvers, ILogger<CompositeTenantResolver> logger)
    {
        _resolvers = resolvers?.ToList() ?? throw new ArgumentNullException(nameof(resolvers));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_resolvers.Count == 0)
        {
            _logger.LogWarning("CompositeTenantResolver initialized with no resolvers");
        }
    }

    public async Task<string?> ResolveAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        if (context == null)
            return null;

        for (int i = 0; i < _resolvers.Count; i++)
        {
            try
            {
                var tenantId = await _resolvers[i].ResolveAsync(context, cancellationToken);
                if (!string.IsNullOrEmpty(tenantId))
                {
                    _logger.LogDebug("Tenant resolved by resolver {ResolverType} (index {Index}): {TenantId}",
                        _resolvers[i].GetType().Name, i, tenantId);
                    return tenantId;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in resolver {ResolverType} (index {Index})", _resolvers[i].GetType().Name, i);
            }
        }

        _logger.LogDebug("No resolver could determine the tenant");
        return null;
    }
}
