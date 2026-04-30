using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace MultiTenantHttpClientFactory.Abstractions;

/// <summary>
/// Contract for extracting tenant identity from an incoming HTTP request.
/// </summary>
public interface ITenantResolver
{
    /// <summary>
    /// Attempts to resolve a tenant identifier from the HTTP context.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tenant identifier, or null if this resolver cannot determine the tenant.</returns>
    Task<string?> ResolveAsync(HttpContext context, CancellationToken cancellationToken = default);
}
