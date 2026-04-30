using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using MultiTenantHttpClientFactory.Abstractions.Models;

namespace MultiTenantHttpClientFactory.Abstractions;

/// <summary>
/// Internal orchestrator between the factory and the tenant store.
/// Provides caching and change token support.
/// </summary>
public interface ITenantConfigurationProvider
{
    /// <summary>
    /// Gets the configuration for a tenant, with caching.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <returns>The tenant configuration, or null if not found.</returns>
    Task<TenantConfiguration?> GetConfigurationAsync(string tenantId);

    /// <summary>
    /// Returns a change token for a specific tenant's configuration.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    IChangeToken GetChangeToken(string tenantId);
}
