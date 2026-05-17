namespace SampleGatewayWithDb.Data;

/// <summary>
/// Entity representing an endpoint configuration for a tenant.
/// </summary>
public class EndpointEntity
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string BaseAddress { get; set; } = string.Empty;

    public string? TimeoutSeconds { get; set; }

    // Navigation property
    public TenantEntity? Tenant { get; set; }

    public CertificateEntity? Certificate { get; set; }
}
