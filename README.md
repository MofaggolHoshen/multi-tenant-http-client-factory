# Multi-Tenant HttpClientFactory

A .NET library that extends `IHttpClientFactory` to support multi-tenant scenarios — resolving the current tenant from incoming requests, loading tenant-specific configuration (endpoints, certificates, headers, timeouts), and serving pooled `HttpClient` instances per tenant with hot-reload support.

## Features

- **Tenant-aware HttpClient creation** — each tenant gets its own pre-configured `HttpClient` with correct base address, headers, timeout, and client certificate
- **Pluggable tenant resolution** — resolve tenants from HTTP headers, route values, subdomains, or JWT claims (composite/chainable)
- **Pluggable tenant storage** — implement `ITenantStore` for any backend (SQL, CosmosDB, Redis, external API)
- **Multiple certificate sources** — File (.pfx), Windows Certificate Store, Base64-encoded, Azure Key Vault
- **Hot-reload** — configuration changes are picked up at runtime via `IChangeToken` without restart
- **Handler pooling** — `ConcurrentDictionary`-based handler cache with configurable lifetime and graceful disposal
- **Builder pattern DI** — fluent registration API for `IServiceCollection`

## Project Structure

```
src/
├── MultiTenantHttpClientFactory.Abstractions/   (netstandard2.0)
│   ├── ITenantHttpClientFactory.cs              Main factory interface
│   ├── ITenantStore.cs                          Primary extensibility point
│   ├── ITenantResolver.cs                       Tenant resolution contract
│   ├── ITenantConfigurationProvider.cs          Config caching layer
│   ├── ICertificateProvider.cs                  Certificate loading contract
│   ├── ITenantContext.cs                        Scoped tenant state
│   ├── Exceptions.cs                            Custom exceptions
│   └── Models/
│       ├── TenantConfiguration.cs
│       ├── EndpointConfiguration.cs
│       └── CertificateConfiguration.cs
│
└── MultiTenantHttpClientFactory/                (net8.0 / net10.0)
    ├── TenantResolution/                        Header, Route, Subdomain, Claims resolvers
    ├── Configuration/                           JSON, InMemory tenant stores
    ├── Certificates/                            File, Store, Base64, KeyVault providers
    └── DependencyInjection/                     Builder + ServiceCollection extensions

samples/
└── SampleGateway/                               ASP.NET Core Web API demo

tests/
└── MultiTenantHttpClientFactory.Tests/          xUnit tests
```

## Quick Start

### 1. Implement `ITenantStore`

```csharp
public class MyTenantStore : ITenantStore
{
    public Task<TenantConfiguration?> GetTenantAsync(string tenantId, CancellationToken ct) { /* ... */ }
    public Task<IReadOnlyList<TenantConfiguration>> GetAllTenantsAsync(CancellationToken ct) { /* ... */ }
    public IChangeToken GetReloadToken() { /* ... */ }
}
```

### 2. Register Services

```csharp
services.AddMultiTenantHttpClientFactory()
    .WithTenantStore<MyTenantStore>()
    .AddTenantResolver<HeaderTenantResolver>()
    .AddTenantResolver<ClaimsTenantResolver>();
```

### 3. Use the Factory

```csharp
// Explicit — specify tenant ID directly (background jobs, fan-out)
var client = tenantHttpClientFactory.CreateClient("tenant-abc");

// Implicit — resolves tenant from current HTTP context
var client = tenantHttpClientFactory.CreateClient();
```

## Configuration Model

```json
{
  "Tenants": {
    "tenant-a": {
      "DefaultEndpoint": {
        "BaseAddress": "https://api.tenant-a.example.com",
        "Headers": { "X-Api-Key": "key-a" }
      },
      "Certificate": {
        "Type": "File",
        "Path": "certs/tenant-a.pfx",
        "Password": "secret"
      },
      "Timeout": "00:00:30",
      "HandlerLifetime": "00:05:00"
    }
  }
}
```

## Target Frameworks

| Package                                     | Target               |
| ------------------------------------------- | -------------------- |
| `MultiTenantHttpClientFactory.Abstractions` | `netstandard2.0`     |
| `MultiTenantHttpClientFactory`              | `net8.0` / `net10.0` |

## Key Design Decisions

- **Strategy pattern** for all extensibility points (resolution, storage, certificates)
- **Composite pattern** for chaining multiple resolvers and certificate providers
- **`netstandard2.0` abstractions** for maximum compatibility across .NET Framework and modern .NET
- **`SocketsHttpHandler`** for performance and `PooledConnectionLifetime` DNS rotation
- **Separate NuGet packages** planned: Abstractions, Core, AzureKeyVault

## Status

| Phase | Description                                     | Status         |
| ----- | ----------------------------------------------- | -------------- |
| 1     | Abstractions & Models                           | ✅ Complete    |
| 2     | Core Implementation (factory, cache, resolvers) | 🔲 Not Started |
| 3     | Configuration & Certificate Providers           | 🔲 Not Started |
| 4     | DI & Builder Pattern                            | 🔲 Not Started |
| 5     | Hot-Reload & Lifecycle                          | 🔲 Not Started |
| 6     | Samples & Tests                                 | 🔲 Not Started |

## Building

```bash
dotnet build
dotnet test
```

## License

Private — experimental project.
