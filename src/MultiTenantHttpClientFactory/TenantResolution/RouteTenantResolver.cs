using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using MultiTenantHttpClientFactory.Abstractions;

namespace MultiTenantHttpClientFactory.TenantResolution;

/// <summary>
/// Resolves tenant from a route parameter (default: tenantId).
/// </summary>
internal class RouteTenantResolver : ITenantResolver
{
    private readonly string _routeParameterName;
    private readonly ILogger<RouteTenantResolver>? _logger;

    public RouteTenantResolver(string routeParameterName = "tenantId", ILogger<RouteTenantResolver>? logger = null)
    {
        _routeParameterName = routeParameterName ?? throw new ArgumentNullException(nameof(routeParameterName));
        _logger = logger;
    }

    public Task<string?> ResolveAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        if (context == null)
            return Task.FromResult<string?>(null);

        var routeData = context.GetRouteData();
        if (routeData?.Values != null && routeData.Values.TryGetValue(_routeParameterName, out var tenantId))
        {
            var value = tenantId?.ToString();
            if (!string.IsNullOrEmpty(value))
            {
                _logger?.LogDebug("Tenant resolved from route parameter {ParameterName}: {TenantId}", _routeParameterName, value);
                return Task.FromResult<string?>(value);
            }
        }

        return Task.FromResult<string?>(null);
    }
}
