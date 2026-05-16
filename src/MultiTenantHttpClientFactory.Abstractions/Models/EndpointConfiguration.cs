using System;
using System.Collections.Generic;

namespace MultiTenantHttpClientFactory.Abstractions.Models;

/// <summary>
/// Represents one outbound endpoint configuration for a tenant.
/// </summary>
public class EndpointConfiguration
{
    /// <summary>
    /// The base address (URI) for this endpoint.
    /// </summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>
    /// HTTP headers to include on every request to this endpoint,
    /// merged with tenant-level default headers.
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Optional timeout for this endpoint, overriding tenant-level timeout.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Optional certificate configuration for this endpoint,
    /// overriding tenant-level certificate.
    /// </summary>
    public CertificateConfiguration? Certificate { get; set; }
}
