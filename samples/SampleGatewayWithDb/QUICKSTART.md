# SampleGatewayWithDb - Quick Start

## Overview

A ready-to-use sample project showing how to use the Multi-Tenant HTTP Client Factory with **Entity Framework Core** and **SQL Server** for database-backed tenant configurations.

## Quick Start

### 1. Prerequisites

```
- .NET 8.0 SDK
- SQL Server or LocalDB (automatically created)
```

### 2. Run the Application

```bash
cd samples/SampleGatewayWithDb
dotnet run
```

**Output:**

- Database automatically created
- Migrations applied
- Sample tenants seeded
- API running on https://localhost:7001

### 3. Test the API

#### List Tenants

```bash
curl -k https://localhost:7001/tenants
```

**Response:**

```json
[
  {
    "tenantId": "tenant-a",
    "endpointCount": 1,
    "defaultEndpoint": "https://api.tenant-a.example.com"
  },
  {
    "tenantId": "tenant-b",
    "endpointCount": 2,
    "defaultEndpoint": "https://payments.tenant-b.example.com"
  },
  {
    "tenantId": "tenant-c",
    "endpointCount": 1,
    "defaultEndpoint": "https://api.tenant-c.example.com"
  }
]
```

#### Get Specific Tenant

```bash
curl -k https://localhost:7001/tenants/tenant-a
```

**Response:**

```json
{
  "tenantId": "tenant-a",
  "endpointCount": 1,
  "defaultEndpoint": "https://api.tenant-a.example.com"
}
```

#### View API Documentation

Open browser: https://localhost:7001/swagger

## Project Structure

### Data Layer

- **ApplicationDbContext**: EF Core DbContext
- **DatabaseTenantStore**: ITenantStore implementation
- **Entities**: TenantEntity, EndpointEntity, TenantHeaderEntity, CertificateEntity
- **Migrations**: Auto-generated database schema

### API Layer

- **Program.cs**: Endpoints for listing/proxying tenants
- **appsettings.json**: Configuration (DB connection string)

## Sample Tenants

### Tenant A

```
ID: tenant-a
Endpoints:
  - default: https://api.tenant-a.example.com
Headers:
  - X-Tenant-Version: v1
Certificate: File-based (./certs/tenant-a.pfx)
```

### Tenant B

```
ID: tenant-b
Endpoints:
  - payments: https://payments.tenant-b.example.com
  - notifications: https://notifications.tenant-b.example.com
Headers:
  - X-Tenant-Version: v2
  - X-API-Key: tenant-b-key
Certificate: Base64-encoded
```

### Tenant C

```
ID: tenant-c
Endpoints:
  - default: https://api.tenant-c.example.com
Headers:
  - X-Tenant-Version: v1
  - X-Client-ID: tenant-c-client
```

## Adding New Tenants

Edit `Data/DbInitializer.cs`:

```csharp
var newTenant = new TenantEntity
{
    TenantId = "tenant-d",
    DefaultEndpointName = "default",
    TimeoutSeconds = "30",
    HandlerLifetimeSeconds = "120",
    Endpoints = new List<EndpointEntity>
    {
        new()
        {
            Name = "default",
            BaseAddress = "https://api.tenant-d.example.com",
        }
    },
    DefaultHeaders = new List<TenantHeaderEntity>
    {
        new() { Key = "X-Tenant-Version", Value = "v1" }
    }
};

context.Tenants.Add(newTenant);
```

Then restart the application.

## Architecture Highlights

### 1. Database-Backed Configuration

- Tenants stored in SQL Server
- No configuration files needed
- Runtime provisioning possible

### 2. Type-Safe Mapping

- Database entities → TenantConfiguration objects
- Automatic type conversion (string to Uri, TimeSpan, etc.)
- Full C# type safety

### 3. Multi-Tenant Support

- Unique tenant IDs
- Multiple endpoints per tenant
- Tenant-level and endpoint-level customization

### 4. Production-Ready

- Change token support for caching
- Eager loading for performance
- Proper error handling

## Key Files

| File                           | Purpose                            |
| ------------------------------ | ---------------------------------- |
| `Program.cs`                   | API endpoints and DI configuration |
| `Data/ApplicationDbContext.cs` | EF Core DbContext                  |
| `Data/DatabaseTenantStore.cs`  | Tenant resolution from DB          |
| `Data/DbInitializer.cs`        | Seed sample data                   |
| `Data/Migrations/`             | Database schema                    |

## Common Tasks

### View Generated Database Schema

```bash
cd samples/SampleGatewayWithDb
dotnet ef migrations list
```

### Modify Database Connection

Edit `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "your-connection-string"
  }
}
```

### Reset Database

```bash
dotnet ef database drop -f
dotnet run
```

### Create New Migration After Model Changes

```bash
dotnet ef migrations add MigrationName
```

## Performance Considerations

1. **Query Optimization**: Uses `AsNoTracking()` for read-only queries
2. **Eager Loading**: Related entities loaded with `.Include()`
3. **Change Tokens**: Support cache invalidation strategies
4. **Connection Pooling**: Automatic pooled connection lifetime

## Testing

Test with sample requests:

```bash
# Get all tenants
curl -k https://localhost:7001/tenants

# Get specific tenant
curl -k https://localhost:7001/tenants/tenant-b

# Proxy a request (to test)
curl -k -X POST https://localhost:7001/proxy/tenant-a/api/test \
  -H "Content-Type: application/json" \
  -d '{"test":"data"}'
```

## Troubleshooting

### "Cannot connect to database"

- Ensure LocalDB is installed
- Check connection string in appsettings.json
- Verify SQL Server is running: `sqllocaldb info`

### "Migration not found"

- Delete database: `dotnet ef database drop -f`
- Restart application to recreate

### Port Already in Use

```bash
netstat -ano | findstr :7001
taskkill /PID <PID> /F
```

## Next Steps

1. **Test the API** using Swagger UI (https://localhost:7001/swagger)
2. **Examine the code** in `Data/` folder to understand the implementation
3. **Add custom tenants** by modifying `DbInitializer.cs`
4. **Extend functionality** with admin CRUD operations
5. **Implement caching** for production use

## Related Documentation

- [Multi-Tenant HTTP Client Factory](../README.md)
- [SampleGateway](../SampleGateway) - Configuration-based sample
- [Main Library Docs](../../README.md)
