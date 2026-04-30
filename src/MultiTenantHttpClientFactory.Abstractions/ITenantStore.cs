using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using MultiTenantHttpClientFactory.Abstractions.Models;

namespace MultiTenantHttpClientFactory.Abstractions;

/// <summary>
/// The primary abstraction consumers implement to plug in any tenant storage backend.
/// </summary>
public interface ITenantStore
{
    /// <summary>
    /// Fetches configuration for a single tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tenant configuration, or null if the tenant does not exist.</returns>
    Task<TenantConfiguration?> GetTenantAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enumerates all tenant configurations (for preloading or admin scenarios).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All tenant configurations.</returns>
    Task<IReadOnlyList<TenantConfiguration>> GetAllTenantsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a change token that fires when tenant data changes in storage.
    /// Enables hot-reload of tenant configuration.
    /// </summary>
    IChangeToken GetReloadToken();
}
