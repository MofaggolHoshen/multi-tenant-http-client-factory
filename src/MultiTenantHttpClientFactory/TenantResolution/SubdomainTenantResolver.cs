using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MultiTenantHttpClientFactory.Abstractions;

namespace MultiTenantHttpClientFactory.TenantResolution;

/// <summary>
/// Resolves tenant from the first subdomain segment.
/// E.g., tenantA.api.example.com → "tenantA"
/// </summary>
public class SubdomainTenantResolver : ITenantResolver
{
    private readonly int _segmentIndex;
    private readonly string? _baseDomain;
    private readonly ILogger<SubdomainTenantResolver>? _logger;

    public SubdomainTenantResolver(int segmentIndex = 0, string? baseDomain = null, ILogger<SubdomainTenantResolver>? logger = null)
    {
        _segmentIndex = segmentIndex;
        _baseDomain = baseDomain;
        _logger = logger;
    }

    public Task<string?> ResolveAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        if (context?.Request?.Host == null)
            return Task.FromResult<string?>(null);

        var host = context.Request.Host.Host;

        // Skip IPv6, IPv4 addresses, and single-segment hosts (no subdomain possible)
        if (host.Contains(':') || !host.Contains('.') || System.Net.IPAddress.TryParse(host, out _))
            return Task.FromResult<string?>(null);

        var segments = host.Split('.');

        // If a base domain is specified, check if this host ends with it and strip it
        if (!string.IsNullOrEmpty(_baseDomain))
        {
            var baseDomainSegments = _baseDomain.Split('.');
            if (segments.Length <= baseDomainSegments.Length)
                return Task.FromResult<string?>(null);

            // Check if the end of segments matches baseDomain
            for (int i = 0; i < baseDomainSegments.Length; i++)
            {
                if (segments[segments.Length - baseDomainSegments.Length + i] != baseDomainSegments[i])
                    return Task.FromResult<string?>(null);
            }

            // Remove base domain segments
            Array.Resize(ref segments, segments.Length - baseDomainSegments.Length);
        }

        // Check if the requested segment index is valid
        if (_segmentIndex >= segments.Length)
            return Task.FromResult<string?>(null);

        var tenantId = segments[_segmentIndex];
        if (!string.IsNullOrEmpty(tenantId))
        {
            _logger?.LogDebug("Tenant resolved from subdomain segment {SegmentIndex}: {TenantId}", _segmentIndex, tenantId);
            return Task.FromResult<string?>(tenantId);
        }

        return Task.FromResult<string?>(null);
    }
}
