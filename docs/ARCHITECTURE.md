# Multi-Tenant HttpClientFactory - Architecture & Data Flow

## Overview

The Multi-Tenant HttpClientFactory is a .NET library that extends the standard `IHttpClientFactory` to enable seamless multi-tenant scenarios. It provides tenant-aware HTTP client instantiation with automatic configuration, certificate management, and hot-reload capabilities.

## Core Concept

**Problem**: In multi-tenant applications, different tenants often require different API endpoints, authentication headers, certificates, and timeouts. Managing separate `HttpClient` instances and configurations for each tenant is complex and error-prone.

**Solution**: This library abstracts tenant-specific configuration and provides a unified factory interface that resolves the current tenant and serves pre-configured `HttpClient` instances.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    HTTP Request (ASP.NET Core)              │
└──────────────────────────┬──────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│            TenantResolutionMiddleware                        │
│  (Extracts tenant info from headers, routes, claims, etc.)   │
└──────────────┬───────────────────────────────┬──────────────┘
               │                               │
      ┌────────▼────────┐          ┌──────────▼──────────┐
      │HeaderResolver   │          │   RoutResolver      │
      │ClaimsResolver   │          │ SubdomainResolver   │
      │CompositResolver │          │CompositResolver     │
      └────────┬────────┘          └──────────┬──────────┘
               │                               │
               └───────────────┬───────────────┘
                               │
                               ▼
                    ┌──────────────────────┐
                    │  TenantContext       │
                    │  (Scoped Service)    │
                    │  - TenantId          │
                    │  - Configuration     │
                    │  - IsResolved        │
                    └──────────┬───────────┘
                               │
                               ▼
                ┌──────────────────────────────────┐
                │ TenantHttpClientFactory.         │
                │ CreateClient()                   │
                └──────────┬───────────────────────┘
                           │
         ┌─────────────────┼─────────────────┐
         │                 │                 │
         ▼                 ▼                 ▼
    ┌─────────┐    ┌──────────────┐   ┌─────────────┐
    │Explicit │    │ Fetch from   │   │Get/Create   │
    │Tenant ID│    │ TenantContext│   │Handler from │
    │from arg │    │(Implicit)    │   │Cache        │
    └────┬────┘    └──────┬───────┘   └────┬────────┘
         │                │                │
         └────────────────┼────────────────┘
                          │
                          ▼
          ┌───────────────────────────────┐
          │TenantConfigurationProvider    │
          │(Caching Layer - MemoryCache)  │
          └───────────┬───────────────────┘
                      │
         ┌────────────┴────────────┐
         │                         │
    ┌────▼─────┐            ┌──────▼──────┐
    │  Cache   │  MISS      │ TenantStore │
    │  HIT     │◄───────────│(SQL/JSON/   │
    │Return    │            │ CosmosDB)   │
    │Config    │            └──────┬──────┘
    └────┬─────┘                   │
         │         ┌───────────────┴──────────┐
         │         │                          │
         │    ┌────▼──────┐           ┌──────▼────┐
         │    │JsonFile   │           │InMemory   │
         │    │TenantStore│           │TenantStore│
         │    └───────────┘           └───────────┘
         │
              ▼
         ┌──────────────────────────────┐
         │TenantConfiguration            │
         │- TenantId                     │
         │- DefaultEndpointName          │
         │- Named Endpoints              │
         │- DefaultHeaders               │
         │- Certificate Config           │
         │- Timeout                      │
         │- HandlerLifetime              │
         └──────────┬───────────────────┘
               │
               ▼
    ┌──────────────────────────────┐
    │Certificate Provider          │
    │ - Load certificate from:     │
    │   - File (.pfx)              │
    │   - Windows Store            │
    │   - Base64 string            │
    │   - Azure Key Vault          │
    │   - Composite (try multiple) │
    └──────────┬───────────────────┘
               │
               ▼
    ┌──────────────────────────────┐
    │TenantHandlerCache            │
    │ - ConcurrentDictionary       │
    │ - SocketsHttpHandler         │
    │ - Certificate attached       │
    │ - Handler lifetime mgmt      │
    └──────────┬───────────────────┘
               │
               ▼
    ┌──────────────────────────────┐
    │HttpClient (Created)          │
    │- BaseAddress: tenant-specific│
    │- Timeout: tenant-specific    │
    │- Headers: tenant + endpoint  │
    │- Handler: cached & reused    │
    │- Certificate: applied        │
    └──────────┬───────────────────┘
               │
               ▼
    ┌──────────────────────────────┐
    │Caller Service                │
    │(Controller/Service)          │
    │Uses HttpClient to call       │
    │tenant-specific API           │
    └──────────────────────────────┘
```

---

## Data Flow - Step by Step

### Phase 1: Tenant Resolution

When an HTTP request arrives at the ASP.NET Core application:

1. **TenantResolutionMiddleware** intercepts the request
2. **Composite Tenant Resolver** attempts to resolve the tenant using multiple strategies (in order):
   - **HeaderTenantResolver**: Looks for tenant ID in HTTP headers (e.g., `X-Tenant-Id`)
   - **ClaimsTenantResolver**: Extracts tenant from JWT claims (e.g., `sub` claim)
   - **RouteTenantResolver**: Extracts from route parameters (e.g., `/api/{tenantId}/resources`)
   - **SubdomainTenantResolver**: Extracts from subdomain (e.g., `tenant-a.api.example.com`)

3. **Resolved tenant ID** is stored in the scoped **TenantContext** service

### Phase 2: Configuration Loading

When `ITenantHttpClientFactory.CreateClient()` is called:

1. **Factory** checks if tenant resolution is explicit (passed as argument) or implicit (from `TenantContext`)
2. **Factory** requests configuration from **TenantConfigurationProvider**
3. **Provider** checks **MemoryCache** for cached configuration:
   - **Cache HIT**: Returns cached config (5-minute TTL with 1-minute sliding expiration)
   - **Cache MISS**: Queries **ITenantStore** for configuration
4. **TenantStore** retrieves configuration (implementation-dependent):
   - **JsonFileTenantStore**: Reads from `appsettings.json`
   - **InMemoryTenantStore**: Returns hardcoded configurations
   - **Custom Implementation**: Could query SQL database, CosmosDB, Redis, or external API
5. Configuration is cached and returned

### Phase 3: Certificate Loading

As part of configuration retrieval:

1. **CertificateConfiguration** is extracted from tenant config
2. **Certificate Provider** attempts to load certificate based on type:
   - **File**: Loads `.pfx` file from disk with password
   - **Store**: Loads from Windows Certificate Store
   - **Base64**: Decodes and loads from Base64-encoded string
   - **KeyVault**: Retrieves from Azure Key Vault
   - **Composite**: Tries multiple providers in sequence

3. Loaded certificate is attached to **SocketsHttpHandler**

### Phase 4: Handler Pooling

The factory uses **TenantHandlerCache** (ConcurrentDictionary-based):

1. Cache key: `{tenantId}:{endpointName}`
2. If handler exists and not expired: Reuse it
3. If handler expired or doesn't exist:
   - Create new **SocketsHttpHandler**
   - Attach certificate (if configured)
   - Cache with configurable lifetime (default: 5 minutes)
   - Old handlers are gracefully disposed

**Benefits**:

- Connection pooling (DNS resolution, TCP connections reused)
- Reduced memory allocation
- Efficient certificate management
- Configurable handler lifetime for DNS rotation

### Phase 5: HttpClient Creation

Factory constructs the **HttpClient** instance:

1. **Handler**: Retrieved from cache (step 4)
2. **BaseAddress**: Set from endpoint configuration
3. **Timeout**: Applied (endpoint-level > tenant-level > global default)
4. **Headers**: Applied in order (tenant defaults + endpoint-specific overrides)
5. **HttpClient** is returned (not cached — lightweight object)

---

## Configuration Model

```json
{
  "Tenants": {
    "tenant-a": {
      "Endpoints": {
        "default": {
          "BaseAddress": "https://api.tenant-a.example.com",
          "Headers": {
            "X-Api-Key": "secret-key-a",
            "X-Custom-Header": "value"
          }
        },
        "webhook": {
          "BaseAddress": "https://webhooks.tenant-a.example.com",
          "Headers": {
            "Authorization": "Bearer token"
          }
        }
      },
      "DefaultEndpointName": "default",
      "Certificate": {
        "Type": "File",
        "Path": "certs/tenant-a.pfx",
        "Password": "cert-password"
      },
      "Timeout": "00:00:30",
      "HandlerLifetime": "00:05:00"
    },
    "tenant-b": {
      "Endpoints": {
        "default": {
          "BaseAddress": "https://api.tenant-b.example.com"
        }
      },
      "DefaultEndpointName": "default",
      "Certificate": {
        "Type": "Store",
        "Thumbprint": "ABCD1234..."
      }
    }
  }
}
```

---

## Hot-Reload Flow

The library supports configuration hot-reload without application restart:

```
┌─────────────────────────────┐
│ TenantStore detects change  │
│ (file updated, DB changed)  │
└──────────────┬──────────────┘
               │
               ▼
    ┌──────────────────────────┐
    │ Fires IChangeToken       │
    │ (e.g., FileSystemWatcher)│
    └──────────────┬───────────┘
                   │
                   ▼
    ┌──────────────────────────────┐
    │ TenantConfigurationProvider  │
    │ receives change notification │
    └──────────────┬───────────────┘
                   │
    ┌──────────────┴──────────────┐
    │                             │
    ▼                             ▼
┌─────────────────┐    ┌──────────────────────┐
│ Invalidate      │    │Cancel change token   │
│ MemoryCache     │    │Create new one        │
│ for tenant      │    │(Subscribers notified)│
└─────────────────┘    └──────────────────────┘
    │                             │
    └──────────────┬──────────────┘
                   │
                   ▼
    ┌──────────────────────────────┐
    │ TenantHandlerCleanupService  │
    │ Disposes old handlers        │
    │ (Connection cleanup)         │
    └──────────────────────────────┘

Next call to CreateClient():
- Configuration reloaded from store
- New handler created
- Old cached handler replaced
```

---

## Extension Points

The library is designed with Strategy and Composite patterns for extensibility:

### 1. **Tenant Resolution** (ITenantResolver)

```csharp
public interface ITenantResolver
{
    Task<string?> ResolveAsync(HttpContext context);
}
```

**Examples**: Header-based, claims-based, subdomain-based, custom logic

### 2. **Tenant Storage** (ITenantStore)

```csharp
public interface ITenantStore
{
    Task<TenantConfiguration?> GetTenantAsync(string tenantId);
    IChangeToken GetReloadToken();
}
```

**Examples**: JSON file, in-memory, SQL database, CosmosDB, Redis, external API

### 3. **Certificate Loading** (ICertificateProvider)

```csharp
public interface ICertificateProvider
{
    Task<X509Certificate2?> GetCertificateAsync(CertificateConfiguration config);
}
```

**Examples**: File, Windows Store, Base64, Azure Key Vault, custom sources

---

## Key Components

| Component                       | Purpose                            | Lifecycle                       |
| ------------------------------- | ---------------------------------- | ------------------------------- |
| **TenantResolutionMiddleware**  | Extracts tenant from HTTP context  | Per request                     |
| **ITenantResolver**             | Strategy for determining tenant ID | Per request (stateless)         |
| **TenantContext**               | Scoped container for tenant info   | Per request                     |
| **ITenantHttpClientFactory**    | Main public API                    | Singleton                       |
| **TenantHttpClientFactory**     | Implementation of factory          | Singleton                       |
| **ITenantStore**                | Data source for configurations     | Singleton (or scoped)           |
| **TenantConfigurationProvider** | Caching layer + change tracking    | Singleton                       |
| **TenantHandlerCache**          | Handler pooling & reuse            | Singleton                       |
| **SocketsHttpHandler**          | Underlying HTTP handler            | Cached (reused across requests) |
| **ICertificateProvider**        | Certificate loading strategy       | Singleton                       |
| **TenantContext**               | Scoped tenant state                | Per request                     |

---

## Performance Considerations

1. **Handler Pooling**: Connection reuse significantly reduces latency
2. **Configuration Caching**: 5-minute TTL + 1-minute sliding expiration
3. **Memory Cache**: Fast in-process caching (no network hop)
4. **Lazy Certificate Loading**: Loaded on-demand, cached in handler
5. **Handler Lifetime**: Configurable to enable DNS rotation (default 5 minutes)

---

## Error Handling

| Scenario                        | Exception                    | Handling                              |
| ------------------------------- | ---------------------------- | ------------------------------------- |
| Tenant not resolved             | `TenantNotResolvedException` | Middleware failed to determine tenant |
| Tenant not found in store       | `TenantNotFoundException`    | Store returned null                   |
| No endpoint configuration       | `InvalidOperationException`  | Missing default or named endpoint     |
| Certificate loading failed      | `CertificateLoadException`   | All certificate providers failed      |
| Configuration provider disposed | `ObjectDisposedException`    | Attempting use after disposal         |

---

## Typical Usage Flow

```csharp
// 1. Register in DI container
services.AddMultiTenantHttpClientFactory()
    .WithTenantStore<JsonFileTenantStore>()
    .AddTenantResolver<HeaderTenantResolver>()
    .AddTenantResolver<ClaimsTenantResolver>();

// 2. Middleware intercepts request & resolves tenant
app.UseMultiTenantHttpClientFactory();

// 3. In controller/service, inject factory
public class MyController
{
    private readonly ITenantHttpClientFactory _factory;

    public MyController(ITenantHttpClientFactory factory) => _factory = factory;

    public async Task DoWork()
    {
        // Implicit: Uses tenant from TenantContext (resolved by middleware)
        var client = _factory.CreateClient();

        // Or explicit: Specify tenant directly (for background jobs)
        var client = _factory.CreateClient("tenant-xyz");

        // Make request with pre-configured client
        var response = await client.GetAsync("/api/resource");
    }
}
```

---

## Conclusion

The Multi-Tenant HttpClientFactory provides a clean abstraction for managing tenant-specific HTTP communication in multi-tenant .NET applications. Through pluggable resolvers, stores, and certificate providers, it adapts to diverse deployment scenarios while maintaining performance through strategic caching and connection pooling.
