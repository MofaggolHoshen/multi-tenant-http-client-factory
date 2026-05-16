# Quick Reference Card - Multi-Tenant HttpClientFactory

## One-Minute Summary

This library creates **tenant-specific HttpClient instances** with automatic configuration, certificate management, and connection pooling.

```csharp
// One line to get fully configured HttpClient
var client = factory.CreateClient();
```

---

## 5-Step Data Flow

```
Request → Resolve Tenant → Load Config → Load Cert → Pool Handler → HttpClient
```

| Step                 | Action                               | Performance   |
| -------------------- | ------------------------------------ | ------------- |
| 1️⃣ Resolve Tenant    | Extract from headers/claims/routes   | Per-request   |
| 2️⃣ Load Config       | Query TenantStore (cached)           | 5-min cache   |
| 3️⃣ Load Certificate  | Load from file/store/base64/keyvault | Lazy loaded   |
| 4️⃣ Pool Handler      | Reuse SocketsHttpHandler             | Highly reused |
| 5️⃣ Create HttpClient | Apply config to HttpClient           | Per-request   |

---

## Key Interfaces (Implement These)

### 1. ITenantStore - Where tenant configs come from

```csharp
public interface ITenantStore
{
    Task<TenantConfiguration?> GetTenantAsync(string tenantId);
    Task<IReadOnlyList<TenantConfiguration>> GetAllTenantsAsync();
    IChangeToken GetReloadToken();  // For hot-reload
}
```

### 2. ITenantResolver - How to extract tenant from request

```csharp
public interface ITenantResolver
{
    Task<string?> ResolveAsync(HttpContext context);
}
```

### 3. ICertificateProvider - Where certificates come from

```csharp
public interface ICertificateProvider
{
    Task<X509Certificate2?> GetCertificateAsync(CertificateConfiguration config);
}
```

---

## Built-in Implementations

### Resolvers

- `HeaderTenantResolver` - From HTTP header
- `ClaimsTenantResolver` - From JWT claim
- `RouteTenantResolver` - From route parameter
- `SubdomainTenantResolver` - From subdomain
- `CompositeTenantResolver` - Try multiple (with fallback)

### Stores

- `JsonFileTenantStore` - From `appsettings.json`
- `InMemoryTenantStore` - Hardcoded configs
- Implement `ITenantStore` for: SQL, CosmosDB, Redis, API

### Providers

- `FileCertificateProvider` - Load `.pfx` from disk
- `StoreCertificateProvider` - Windows Certificate Store
- `Base64CertificateProvider` - Embedded base64 cert
- `CompositeCertificateProvider` - Try multiple sources

---

## Setup in 3 Steps

### 1. Register Services

```csharp
services.AddMultiTenantHttpClientFactory()
    .WithTenantStore<JsonFileTenantStore>()
    .AddTenantResolver<HeaderTenantResolver>()
    .AddTenantResolver<ClaimsTenantResolver>();
```

### 2. Add Middleware

```csharp
app.UseMultiTenantHttpClientFactory();
```

### 3. Use in Services

```csharp
var client = factory.CreateClient();  // Implicit (from context)
var client = factory.CreateClient("tenant-id");  // Explicit (background job)
```

---

## Configuration Format

```json
{
  "Tenants": {
    "tenant-id": {
      "DefaultEndpoint": {
        "BaseAddress": "https://api.tenant.com",
        "Headers": { "X-Api-Key": "secret" }
      },
      "Endpoints": {
        "webhook": {
          "BaseAddress": "https://webhooks.tenant.com"
        }
      },
      "Certificate": {
        "Type": "File",
        "Path": "certs/tenant.pfx",
        "Password": "pwd"
      },
      "Timeout": "00:00:30",
      "HandlerLifetime": "00:05:00"
    }
  }
}
```

---

## Common Use Cases

| Scenario             | Code                                                          |
| -------------------- | ------------------------------------------------------------- |
| **Web Request**      | `var client = factory.CreateClient();`                        |
| **Background Job**   | `var client = factory.CreateClient("tenant-xyz");`            |
| **Named Endpoint**   | `var client = factory.CreateClient("webhook");`               |
| **Explicit + Named** | `var client = factory.CreateClient("tenant-xyz", "webhook");` |

---

## Caching Strategy

| What          | Where                 | TTL              | How                                    |
| ------------- | --------------------- | ---------------- | -------------------------------------- |
| Configuration | MemoryCache           | 5 min            | Automatic invalidation on IChangeToken |
| Handler       | ConcurrentDictionary  | 5 min            | Reused across requests                 |
| Certificate   | In SocketsHttpHandler | Handler lifetime | Attached once, reused                  |
| Connections   | SocketsHttpHandler    | Configurable     | Built-in TCP pooling                   |

---

## Performance Characteristics

- **Handler Reuse**: ✅ Connections pooled & reused
- **Config Caching**: ✅ 5-minute TTL with sliding expiration
- **Lazy Loading**: ✅ Certificates loaded only when needed
- **Zero Allocation**: ✅ Handler cache lookup O(1)
- **Scalability**: ✅ Handles 100s of tenants efficiently

---

## Extensibility Pattern

All extension points use **Strategy + Composite patterns**:

```csharp
// Chain multiple resolvers (try in order)
.AddTenantResolver<HeaderTenantResolver>()
.AddTenantResolver<ClaimsTenantResolver>()
.AddTenantResolver<RouteTenantResolver>()

// Try multiple certificate sources
.AddCertificateProvider<FileCertificateProvider>()
.AddCertificateProvider<StoreCertificateProvider>()
.AddCertificateProvider<Base64CertificateProvider>()
```

---

## Lifecycle of Components

| Component                | Lifetime                         | Created When                    |
| ------------------------ | -------------------------------- | ------------------------------- |
| TenantContext            | Per Request (Scoped)             | Request arrives                 |
| ITenantHttpClientFactory | Application lifetime (Singleton) | DI registration                 |
| MemoryCache              | Application lifetime (Singleton) | DI registration                 |
| SocketsHttpHandler       | When first needed, cached        | First CreateClient() for tenant |
| HttpClient               | Per request                      | Each CreateClient() call        |
| X509Certificate          | When needed, reused in handler   | First request needing cert      |

---

## Hot-Reload Support

```
Config file changes
    ↓
TenantStore fires IChangeToken
    ↓
MemoryCache invalidated
    ↓
Old handlers cleaned up
    ↓
Next request loads fresh config
    ↓
No restart needed! ✨
```

---

## Error Scenarios

| Exception                    | When                            | Handling                       |
| ---------------------------- | ------------------------------- | ------------------------------ |
| `TenantNotResolvedException` | Middleware couldn't find tenant | Check tenant resolution config |
| `TenantNotFoundException`    | Tenant doesn't exist in store   | Check tenant ID & store        |
| `InvalidOperationException`  | No valid endpoint configured    | Check configuration model      |
| `CertificateLoadException`   | All cert providers failed       | Check cert config & paths      |

---

## Documentation Files

| File            | Purpose             | Read Time |
| --------------- | ------------------- | --------- |
| README.md       | Quick start         | 10-15 min |
| INDEX.md        | Navigation guide    | 5 min     |
| OVERVIEW.md     | High-level overview | 10 min    |
| ARCHITECTURE.md | Deep dive           | 25 min    |
| DATA_FLOW.md    | Request lifecycle   | 30 min    |

---

## Typical Request Flow (Implicit)

```
1. Browser: GET /api/users, Header: X-Tenant-Id: acme
2. Middleware: Extract tenant "acme" → TenantContext
3. Controller: Injects ITenantHttpClientFactory
4. Controller: var client = factory.CreateClient()
5. Factory: Gets TenantContext, retrieves config
6. Provider: Loads config from cache (or store)
7. Handler Cache: Gets/creates SocketsHttpHandler
8. Factory: Creates HttpClient with config
9. Controller: Uses client for API call
10. Result: Tenant-specific request sent with correct auth & cert
```

---

## Target Frameworks

- **Abstractions**: `netstandard2.0` (broad compatibility)
- **Implementation**: `net8.0` / `net10.0` (modern .NET)

---

## Quick Checklist

- [ ] Read README.md
- [ ] Implement or choose ITenantStore
- [ ] Choose tenant resolvers (Header, Claims, etc.)
- [ ] Register in DI container
- [ ] Add middleware
- [ ] Create configuration
- [ ] Test with factory.CreateClient()
- [ ] Monitor performance (caching working)
- [ ] Set up hot-reload if needed

---

**Start Here**: [README.md](../README.md)
**Full Index**: [INDEX.md](./INDEX.md)
