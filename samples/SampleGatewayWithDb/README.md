# SampleGatewayWithDb

A comprehensive sample project demonstrating the **Multi-Tenant HTTP Client Factory** using **Entity Framework Core** to store and retrieve tenant configurations from a SQL Server database.

## Overview

This project showcases:

- **Database-backed tenant configuration**: Tenant settings stored in SQL Server instead of configuration files
- **EF Core integration**: Complete data layer using Entity Framework Core
- **ITenantStore implementation**: Custom `DatabaseTenantStore` providing tenant resolution from the database
- **Multi-tenant API Gateway**: HTTP proxy endpoints that route requests through tenant-specific HTTP clients
- **Swagger/OpenAPI documentation**: Interactive API explorer for testing

## Architecture

### Data Models

The project includes EF Core entities for managing tenant configurations:

- **TenantEntity**: Core tenant configuration
  - TenantId (unique identifier)
  - Default endpoint
  - Timeout settings
  - Handler lifetime
  - Navigation to endpoints, headers, and certificates

- **EndpointEntity**: Named service endpoints for each tenant
  - Name (logical identifier)
  - BaseAddress (URI)
  - Timeout (optional override)
  - Certificate configuration (optional override)

- **TenantHeaderEntity**: HTTP headers to send with every request for a tenant
  - Key-value pairs (e.g., X-API-Key, X-Tenant-Version)

- **CertificateEntity**: Client certificate configurations
  - Type (File, Store, Base64, KeyVault)
  - Configuration details (path, password, data, etc.)

### DatabaseTenantStore

Implements `ITenantStore` interface to provide tenant configurations from the database:

```csharp
public class DatabaseTenantStore : ITenantStore
{
    public async Task<TenantConfiguration?> GetTenantAsync(string tenantId, ...)
    public async Task<IReadOnlyList<TenantConfiguration>> GetAllTenantsAsync(...)
    public IChangeToken GetReloadToken()
}
```

## Getting Started

### Prerequisites

- .NET 8.0 SDK
- SQL Server (LocalDB for development)

### Installation

1. **Clone the repository**

   ```bash
   git clone https://github.com/yourusername/multi-tenant-http-client-factory.git
   cd samples/SampleGatewayWithDb
   ```

2. **Restore dependencies**

   ```bash
   dotnet restore
   ```

3. **Build the project**

   ```bash
   dotnet build
   ```

4. **Run database migrations**
   The application automatically applies migrations on startup:
   ```bash
   dotnet run
   ```

### Configuration

The database connection string is configured in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=MultiTenantDb;Trusted_Connection=true;"
  }
}
```

To use a different database server, update the connection string.

## API Endpoints

### List All Tenants

```http
GET /tenants
```

**Response:**

```json
[
  {
    "tenantId": "tenant-a",
    "endpointCount": 1,
    "defaultEndpoint": "https://api.tenant-a.example.com"
  }
]
```

### Get Specific Tenant

```http
GET /tenants/{tenantId}
```

**Example:**

```http
GET /tenants/tenant-a
```

### Proxy Request

```http
POST /proxy/{tenantId}/{*path}
```

**Example:**

```http
POST /proxy/tenant-a/api/users
Content-Type: application/json

{ "name": "John Doe" }
```

Routes the request through the tenant's configured HTTP client.

## Sample Data

The application seeds three sample tenants on first run:

### Tenant A

- **ID**: `tenant-a`
- **Endpoints**:
  - `default` → https://api.tenant-a.example.com
- **Headers**: X-Tenant-Version: v1
- **Certificate**: File-based (./certs/tenant-a.pfx)

### Tenant B

- **ID**: `tenant-b`
- **Endpoints**:
  - `payments` → https://payments.tenant-b.example.com
  - `notifications` → https://notifications.tenant-b.example.com
- **Headers**: X-Tenant-Version: v2, X-API-Key: tenant-b-key
- **Certificate**: Base64-encoded

### Tenant C

- **ID**: `tenant-c`
- **Endpoints**:
  - `default` → https://api.tenant-c.example.com
- **Headers**: X-Tenant-Version: v1, X-Client-ID: tenant-c-client

To modify sample data, edit `DbInitializer.cs`.

## Project Structure

```
SampleGatewayWithDb/
├── Data/
│   ├── ApplicationDbContext.cs          # EF Core DbContext
│   ├── ApplicationDbContextFactory.cs   # Design-time factory for migrations
│   ├── DatabaseTenantStore.cs           # ITenantStore implementation
│   ├── DbInitializer.cs                 # Seed data initialization
│   ├── Migrations/                      # EF Core migrations
│   ├── TenantEntity.cs
│   ├── EndpointEntity.cs
│   ├── TenantHeaderEntity.cs
│   └── CertificateEntity.cs
├── Program.cs                           # Application entry point
├── appsettings.json                     # Configuration
└── SampleGatewayWithDb.csproj           # Project file
```

## Key Features

### 1. **Database-Driven Configuration**

Tenant configurations are managed entirely through the database, enabling:

- Runtime tenant provisioning
- No application restarts required (with cache invalidation)
- Audit trail of tenant changes
- Integration with admin portals

### 2. **EF Core Integration**

- Full ORM support
- Type-safe queries
- Automatic schema management
- Migration-based version control

### 3. **Multi-Tenant Support**

- Isolate HTTP client configurations per tenant
- Support multiple endpoints per tenant
- Tenant-level and endpoint-level customization

### 4. **Extensibility**

The `DatabaseTenantStore` can be extended to:

- Add custom cache strategies
- Implement change notifications
- Support dynamic tenant loading

## Development

### Running Tests

```bash
dotnet test
```

### Running the Application

```bash
dotnet run
```

Access Swagger UI: https://localhost:7001/swagger

### Creating New Migrations

After modifying the data models, create a migration:

```bash
dotnet ef migrations add MigrationName
```

## Advanced Usage

### Custom Cache Invalidation

The `DatabaseTenantStore` provides cache invalidation:

```csharp
// After updating tenant configuration
var store = serviceProvider.GetRequiredService<DatabaseTenantStore>();
store.InvalidateCache();
```

### Implementing Database-Backed Tenant Management

Example of adding a tenant via the database context:

```csharp
var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

var newTenant = new TenantEntity
{
    TenantId = "new-tenant",
    DefaultEndpointName = "default",
    Endpoints = new List<EndpointEntity>
    {
        new EndpointEntity
        {
            Name = "default",
            BaseAddress = "https://api.new-tenant.example.com"
        }
    }
};

context.Tenants.Add(newTenant);
await context.SaveChangesAsync();
```

## License

This project is part of the Multi-Tenant HTTP Client Factory library.

## Support

For issues or questions, please refer to the [main project repository](https://github.com/yourusername/multi-tenant-http-client-factory).
