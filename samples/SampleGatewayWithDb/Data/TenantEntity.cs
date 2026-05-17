using System.Collections.Generic;

namespace SampleGatewayWithDb.Data;

/// <summary>
/// Entity representing a tenant configuration in the database.
/// </summary>
public class TenantEntity
{
    public int Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public string? DefaultEndpointName { get; set; }

    public string? TimeoutSeconds { get; set; }

    public string? HandlerLifetimeSeconds { get; set; }

    // Navigation properties
    public ICollection<EndpointEntity> Endpoints { get; set; } = new List<EndpointEntity>();

    public ICollection<TenantHeaderEntity> DefaultHeaders { get; set; } = new List<TenantHeaderEntity>();

    public CertificateEntity? Certificate { get; set; }
}
