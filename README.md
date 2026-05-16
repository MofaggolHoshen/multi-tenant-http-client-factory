# Multi-Tenant HttpClientFactory

A .NET library that extends `IHttpClientFactory` to support multi-tenant scenarios — resolving the current tenant from incoming requests, loading tenant-specific configuration (endpoints, certificates, headers, timeouts), and serving pooled `HttpClient` instances per tenant with hot-reload support.

## 🎯 What This Does

In multi-tenant applications, different tenants often need to communicate with different APIs, use different authentication, apply different timeouts, and even use different client certificates. This library automates all of that:

```csharp
// Without this library: Complex, error-prone setup per tenant
// With this library: Just call one method!
var client = factory.CreateClient();  // Tenant-specific HttpClient ready
await client.GetAsync("/users");      // Uses tenant's API endpoint & config
```

## ✨ Features

- **Tenant-aware HttpClient creation** — each tenant gets its own pre-configured `HttpClient` with correct base address, headers, timeout, and client certificate
- **Pluggable tenant resolution** — resolve tenants from HTTP headers, route values, subdomains, or JWT claims (composite/chainable)
- **Pluggable tenant storage** — implement `ITenantStore` for any backend (SQL, CosmosDB, Redis, external API)
- **Multiple certificate sources** — File (.pfx), Windows Certificate Store, Base64-encoded, Azure Key Vault
- **Hot-reload** — configuration changes are picked up at runtime via `IChangeToken` without restart
- **Handler pooling** — `ConcurrentDictionary`-based handler cache with configurable lifetime and graceful disposal
- **Builder pattern DI** — fluent registration API for `IServiceCollection`
- **Performance optimized** — connection pooling, configuration caching, lazy certificate loading

## 🏗️ How It Works

**Data Flow**: HTTP Request → Tenant Resolution → Configuration Loading → Handler Pooling → HttpClient Creation

```
Request with tenant info (header/claim/route/subdomain)
    ↓
Tenant Resolver extracts tenant ID
    ↓
TenantContext stores resolved tenant (scoped to request)
    ↓
Configuration loaded from TenantStore (with caching)
    ↓
Certificate loaded and attached to handler
    ↓
Handler reused from pool (SocketsHttpHandler)
    ↓
HttpClient created with tenant-specific config
    ↓
Service uses HttpClient for API calls
    ↓
All subsequent calls reuse same handler & config (performance!)
```

**For detailed architecture & data flow**, see:

- 📋 [**ARCHITECTURE.md**](docs/ARCHITECTURE.md) — Complete architecture overview with all components
- 🔄 [**DATA_FLOW.md**](docs/DATA_FLOW.md) — Step-by-step request lifecycle with diagrams

## 📁 Project Structure

```
src/
├── MultiTenantHttpClientFactory.Abstractions/   (netstandard2.0)
│   ├── ITenantHttpClientFactory.cs              Main factory interface
│   ├── ITenantStore.cs                          Primary extensibility point (implement this!)
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
    ├── HotReload/                               Change detection & handler cleanup
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

## 🔑 Key Design Decisions

- **Strategy pattern** — All extensibility points (resolution, storage, certificates) use pluggable interfaces
- **Composite pattern** — Chain multiple resolvers and certificate providers to try fallbacks
- **`netstandard2.0` abstractions** — Maximum compatibility across .NET Framework and modern .NET
- **`SocketsHttpHandler`** — High-performance handler with built-in connection pooling and DNS rotation
- **Lazy loading** — Certificates and handlers created on-demand, cached for reuse
- **Separate NuGet packages** — Abstractions, Core, AzureKeyVault (future)

## 📊 Development Status

| Phase | Description                                     | Status         |
| ----- | ----------------------------------------------- | -------------- |
| 1     | Abstractions & Models                           | ✅ Complete    |
| 2     | Core Implementation (factory, cache, resolvers) | ✅ Complete    |
| 3     | Configuration & Certificate Providers           | ✅ Complete    |
| 4     | DI & Builder Pattern                            | ✅ Complete    |
| 5     | Hot-Reload & Lifecycle                          | ✅ Complete    |
| 6     | Samples & Tests                                 | 📝 In Progress |

## 🚀 Building & Testing

```bash
dotnet build
dotnet test
```

## 📚 Documentation

- **[ARCHITECTURE.md](docs/ARCHITECTURE.md)** — Complete component overview, design patterns, and extension points
- **[DATA_FLOW.md](docs/DATA_FLOW.md)** — Detailed request lifecycle with visual diagrams
- **[QUICK_START.md](docs/QUICK_START.md)** — Step-by-step setup guide (see Quick Start section below)

## 📋 Performance Notes

- **Handler pooling**: SocketsHttpHandler instances are cached and reused across requests (DNS rotation configurable)
- **Configuration caching**: 5-minute TTL with 1-minute sliding expiration in MemoryCache
- **Lazy loading**: Certificates loaded only when first needed, then cached
- **Connection reuse**: HttpClientHandler maintains a pool of TCP connections per endpoint
- **Zero allocation on hot path**: Handler cache lookup is O(1) concurrent dictionary access

## 📄 License

Private — experimental project.
