using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using MultiTenantHttpClientFactory.Abstractions.Models;

namespace MultiTenantHttpClientFactory.Abstractions;

/// <summary>
/// Contract for loading an X509Certificate2 from a certificate configuration.
/// </summary>
public interface ICertificateProvider
{
    /// <summary>
    /// Loads a certificate based on the provided configuration.
    /// </summary>
    /// <param name="config">The certificate configuration describing how to obtain the cert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded certificate, or null if not applicable.</returns>
    Task<X509Certificate2?> GetCertificateAsync(CertificateConfiguration config, CancellationToken cancellationToken = default);
}
