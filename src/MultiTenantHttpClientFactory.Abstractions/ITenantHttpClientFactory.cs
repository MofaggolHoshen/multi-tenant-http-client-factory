using System.Net.Http;

namespace MultiTenantHttpClientFactory.Abstractions;

/// <summary>
/// Factory for creating tenant-specific HttpClient instances.
/// </summary>
public interface ITenantHttpClientFactory
{
    /// <summary>
    /// Creates an HttpClient configured for the specified tenant and optional endpoint.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="endpointName">Optional named endpoint. Uses default endpoint if null.</param>
    /// <returns>A configured HttpClient instance.</returns>
    /// <exception cref="TenantNotFoundException">Thrown when the tenant does not exist in the store.</exception>
    HttpClient CreateClient(string tenantId, string? endpointName = null);

    /// <summary>
    /// Creates an HttpClient for the current tenant resolved from the HTTP context.
    /// </summary>
    /// <param name="endpointName">Optional named endpoint. Uses default endpoint if null.</param>
    /// <returns>A configured HttpClient instance.</returns>
    /// <exception cref="TenantNotResolvedException">Thrown when no tenant could be resolved from context.</exception>
    HttpClient CreateClient(string? endpointName = null);
}
