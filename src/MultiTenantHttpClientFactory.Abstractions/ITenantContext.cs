using MultiTenantHttpClientFactory.Abstractions.Models;

namespace MultiTenantHttpClientFactory.Abstractions;

/// <summary>
/// Scoped service that holds the resolved tenant for the current request.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// The resolved tenant identifier for the current request.
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// The full tenant configuration for the current request.
    /// </summary>
    TenantConfiguration? Configuration { get; }

    /// <summary>
    /// Indicates whether a tenant has been successfully resolved.
    /// </summary>
    bool IsResolved { get; }
}
