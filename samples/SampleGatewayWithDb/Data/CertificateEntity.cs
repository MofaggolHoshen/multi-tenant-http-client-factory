namespace SampleGatewayWithDb.Data;

/// <summary>
/// Entity representing certificate configuration.
/// </summary>
public class CertificateEntity
{
    public int Id { get; set; }

    public string Type { get; set; } = string.Empty; // "File" or "Base64"

    public string? Path { get; set; }

    public string? Password { get; set; }

    public string? Data { get; set; }

    public int? TenantId { get; set; }

    public int? EndpointId { get; set; }

    // Navigation properties
    public TenantEntity? Tenant { get; set; }

    public EndpointEntity? Endpoint { get; set; }
}
