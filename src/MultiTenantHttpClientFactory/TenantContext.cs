using MultiTenantHttpClientFactory.Abstractions;
using MultiTenantHttpClientFactory.Abstractions.Models;

namespace MultiTenantHttpClientFactory;

/// <summary>
/// Scoped service that holds the resolved tenant for the current request.
/// </summary>
internal class TenantContext : ITenantContext
{
    /// <summary>
    /// The resolved tenant identifier for the current request.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// The full tenant configuration for the current request.
    /// </summary>
    public TenantConfiguration? Configuration { get; set; }

    /// <summary>
    /// Indicates whether a tenant has been successfully resolved.
    /// </summary>
    public bool IsResolved => !string.IsNullOrEmpty(TenantId) && Configuration != null;
}
