namespace SampleGatewayWithDb.Data;

/// <summary>
/// Database initializer for seeding sample tenant data.
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(ApplicationDbContext context)
    {
        // Skip if tenants already exist
        if (context.Tenants.Any())
        {
            return;
        }

        // Create Tenant A
        var tenantA = new TenantEntity
        {
            TenantId = "tenant-a",
            DefaultEndpointName = "default",
            TimeoutSeconds = "30",
            HandlerLifetimeSeconds = "120",
            Endpoints = new List<EndpointEntity>
            {
                new()
                {
                    Name = "default",
                    BaseAddress = "https://api.tenant-a.example.com",
                    TimeoutSeconds = "30",
                    Certificate = new CertificateEntity
                    {
                        Type = "File",
                        Path = "./certs/tenant-a.pfx",
                        Password = "test-password",
                    }
                }
            },
            DefaultHeaders = new List<TenantHeaderEntity>
            {
                new() { Key = "X-Tenant-Version", Value = "v1" }
            }
        };

        // Create Tenant B
        var tenantB = new TenantEntity
        {
            TenantId = "tenant-b",
            DefaultEndpointName = "payments",
            TimeoutSeconds = "30",
            HandlerLifetimeSeconds = "120",
            Endpoints = new List<EndpointEntity>
            {
                new()
                {
                    Name = "payments",
                    BaseAddress = "https://payments.tenant-b.example.com",
                    TimeoutSeconds = "30"
                },
                new()
                {
                    Name = "notifications",
                    BaseAddress = "https://notifications.tenant-b.example.com",
                    TimeoutSeconds = "30"
                }
            },
            DefaultHeaders = new List<TenantHeaderEntity>
            {
                new() { Key = "X-Tenant-Version", Value = "v2" },
                new() { Key = "X-API-Key", Value = "tenant-b-key" }
            },
            Certificate = new CertificateEntity
            {
                Type = "Base64",
                Data = "MIICljCCAX4CCQCKz0Jq..."
            }
        };

        // Create Tenant C
        var tenantC = new TenantEntity
        {
            TenantId = "tenant-c",
            DefaultEndpointName = "default",
            TimeoutSeconds = "30",
            HandlerLifetimeSeconds = "120",
            Endpoints = new List<EndpointEntity>
            {
                new()
                {
                    Name = "default",
                    BaseAddress = "https://api.tenant-c.example.com",
                    TimeoutSeconds = "30"
                }
            },
            DefaultHeaders = new List<TenantHeaderEntity>
            {
                new() { Key = "X-Tenant-Version", Value = "v1" },
                new() { Key = "X-Client-ID", Value = "tenant-c-client" }
            }
        };

        context.Tenants.AddRange(tenantA, tenantB, tenantC);
        await context.SaveChangesAsync();
    }
}
