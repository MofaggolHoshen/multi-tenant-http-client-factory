using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MultiTenantHttpClientFactory.Abstractions;

namespace MultiTenantHttpClientFactory.TenantResolution;

/// <summary>
/// Resolves tenant from an authenticated user's claims.
/// Default claim type: "tenant_id"
/// </summary>
public class ClaimsTenantResolver : ITenantResolver
{
    private readonly string _claimType;
    private readonly ILogger<ClaimsTenantResolver>? _logger;

    public ClaimsTenantResolver(string claimType = "tenant_id", ILogger<ClaimsTenantResolver>? logger = null)
    {
        _claimType = claimType ?? throw new ArgumentNullException(nameof(claimType));
        _logger = logger;
    }

    public Task<string?> ResolveAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        if (context?.User == null)
            return Task.FromResult<string?>(null);

        var claim = context.User.FindFirst(_claimType);
        if (claim != null && !string.IsNullOrEmpty(claim.Value))
        {
            _logger?.LogDebug("Tenant resolved from claim {ClaimType}: {TenantId}", _claimType, claim.Value);
            return Task.FromResult<string?>(claim.Value);
        }

        return Task.FromResult<string?>(null);
    }
}
