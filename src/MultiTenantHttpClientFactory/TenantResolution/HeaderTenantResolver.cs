using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MultiTenantHttpClientFactory.Abstractions;

namespace MultiTenantHttpClientFactory.TenantResolution;

/// <summary>
/// Resolves tenant from an HTTP header (default: X-Tenant-Id).
/// </summary>
public class HeaderTenantResolver : ITenantResolver
{
    private readonly string _headerName;
    private readonly ILogger<HeaderTenantResolver>? _logger;

    public HeaderTenantResolver(string headerName = "X-Tenant-Id", ILogger<HeaderTenantResolver>? logger = null)
    {
        _headerName = headerName ?? throw new ArgumentNullException(nameof(headerName));
        _logger = logger;
    }

    public Task<string?> ResolveAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        if (context?.Request == null)
        {
            _logger?.LogWarning("HttpContext or Request is null, cannot resolve tenant from header {HeaderName}", _headerName);
            return Task.FromResult<string?>(null);
        }

        if (context.Request.Headers.TryGetValue(_headerName, out var tenantId))
        {
            var value = tenantId.ToString();
            if (!string.IsNullOrEmpty(value))
            {
                _logger?.LogDebug("Tenant resolved from header {HeaderName}: {TenantId}", _headerName, value);
                return Task.FromResult<string?>(value);
            }

            _logger?.LogWarning("Header {HeaderName} is present but empty", _headerName);
            return Task.FromResult<string?>(null);
        }

        _logger?.LogWarning("Required header {HeaderName} is missing from request", _headerName);
        return Task.FromResult<string?>(null);
    }
}
