namespace SampleGatewayWithDb.Data;

/// <summary>
/// Entity representing a default HTTP header for a tenant.
/// </summary>
public class TenantHeaderEntity
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    // Navigation property
    public TenantEntity? Tenant { get; set; }
}
