using System;
using System.Collections.Generic;

namespace MultiTenantHttpClientFactory.Abstractions.Models;

/// <summary>
/// Represents a single tenant's HTTP client configuration.
/// </summary>
public class TenantConfiguration
{
    /// <summary>
    /// The unique tenant identifier (primary key).
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Named endpoints for this tenant, keyed by logical name.
    /// </summary>
    public Dictionary<string, EndpointConfiguration> Endpoints { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The default endpoint to use when no specific endpoint name is requested.
    /// </summary>
    public EndpointConfiguration? DefaultEndpoint { get; set; }

    /// <summary>
    /// Certificate configuration at the tenant level (default for all endpoints).
    /// Can be overridden per endpoint.
    /// </summary>
    public CertificateConfiguration? Certificate { get; set; }

    /// <summary>
    /// HTTP headers to include on every request for this tenant.
    /// </summary>
    public Dictionary<string, string> DefaultHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Optional timeout for HTTP requests for this tenant.
    /// Can be overridden per endpoint.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Optional handler lifetime for the pooled HttpMessageHandler.
    /// Controls how long a handler instance is reused before being rotated.
    /// </summary>
    public TimeSpan? HandlerLifetime { get; set; }
}
