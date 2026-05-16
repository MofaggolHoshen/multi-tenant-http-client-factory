# Multi-Tenant HttpClientFactory - Request Data Flow

This document outlines the complete data flow from an incoming HTTP request to a fully configured `HttpClient` instance.

---

## Request Entry Point

```
┌─────────────────────────────────────────────────┐
│ HTTP Request arrives at ASP.NET Core pipeline   │
│ Example: GET https://api.example.com/api/users │
│          Header: X-Tenant-Id: tenant-abc        │
└────────────────┬────────────────────────────────┘
                 │
                 ▼
       ┌─────────────────────┐
       │  HttpContext        │
       │  - Request Headers  │
       │  - Route Values     │
       │  - User Claims      │
       │  - Subdomain        │
       └─────────┬───────────┘
                 │
                 ▼
    ┌───────────────────────────────────┐
    │ TenantResolutionMiddleware        │
    │ Processes HttpContext             │
    └─────────┬───────────────────────┬─┘
              │                       │
         ┌────▼────┐            ┌────▼──────┐
         │ Resolves│            │ Stores in │
         │ Tenant  │            │ TenantCtx │
         │   ID    │            │ (Scoped)  │
         └────┬────┘            └────┬──────┘
              │                      │
              └──────────┬───────────┘
                         │
                         ▼
                 ┌──────────────────┐
                 │ TenantContext    │
                 │                  │
                 │ TenantId: abc    │◄─── Resolved from headers
                 │ IsResolved: true │
                 │ Configuration: ? │
                 └────────┬─────────┘
                          │
                          ▼
            ┌──────────────────────────────┐
            │ Request processed by         │
            │ Controller/Service           │
            └──────────┬───────────────────┘
                       │
                       ▼
        ┌──────────────────────────────────┐
        │ Service injects ITenantHttpClient│
        │ Factory & calls CreateClient()   │
        └────────────┬─────────────────────┘
                     │
                     ▼
```

---

## Factory Resolution Phase

### Implicit Client Creation (Most Common)

```
Service calls: var client = factory.CreateClient();

    ┌────────────────────────────────────┐
    │ ITenantHttpClientFactory           │
    │ .CreateClient(endpointName: null)  │
    └─────────┬───────────────────────┬──┘
              │                       │
              ▼                       ▼
         ┌──────────┐           ┌─────────────┐
         │ TenantCtx│           │ Extract     │
         │ Instance │           │ TenantId    │
         │ (scoped) │           │ from context│
         └──────┬───┘           └──────┬──────┘
                │                      │
                └──────────┬───────────┘
                           │
                    TenantId: "abc"
                           │
                           ▼
        ┌──────────────────────────────┐
        │ Tenant exists in context?    │
        └─┬───────────────────────────┬┘
          │                           │
      NO  │   YES (Typical path)      │
          │                           │
          ▼                           ▼
    Throw Error           ┌───────────────────┐
                          │ Is Configuration  │
                          │ already loaded?   │
                          └┬──────────────────┘
                           │
                       YES  │  NO
                           │
                    ┌──────▼─────┐
                    │ Use cached  │
                    │ config      │
                    │ (jump ahead)│
                    └──────┬──────┘
                           │
                           ▼
```

### Explicit Client Creation (Background Jobs)

```
Service calls: var client = factory.CreateClient("tenant-xyz");

    ┌──────────────────────────────────┐
    │ ITenantHttpClientFactory         │
    │ .CreateClient(tenantId: "xyz")   │
    └─────────────────────┬────────────┘
                          │
                          ▼
            ┌─────────────────────────┐
            │ Validate tenantId       │
            │ not null/empty          │
            └──────────┬──────────────┘
                       │
                       ▼
        ┌──────────────────────────┐
        │ Skip context lookup      │
        │ Use provided tenantId    │
        └──────────┬───────────────┘
                   │
              (continues with
               configuration
               loading phase)
```

---

## Configuration Loading Phase

```
TenantId: "abc"
│
▼
┌─────────────────────────────────────────┐
│ TenantConfigurationProvider             │
│ .GetConfigurationAsync(tenantId: "abc") │
└──────┬────────────────────────────────┬─┘
       │                                │
       ▼                                ▼
   ┌──────────┐              ┌─────────────────┐
   │ Check    │              │ Create cache    │
   │ disposed │              │ key: "tenant-   │
   │ state    │              │ config:abc"     │
   └───┬──────┘              └────────┬────────┘
       │                             │
       ▼                             ▼
 ┌─────────────┐          ┌──────────────────┐
 │ Not disposed│          │ Query MemoryCache│
 │ (continue)  │          │ for key          │
 └─────────────┘          └────┬─────────┬──┘
                               │         │
                           HIT │    MISS │
                               │         │
                        ┌──────▼┐       │
                        │ CACHE │       │
                        │  HIT  │       │
                        └──┬────┘       │
                           │           ▼
                           │    ┌───────────────────┐
                           │    │ Query TenantStore │
                           │    └──────┬────────────┘
                           │           │
                           │      ┌────▼─────┐
                           │      │ Store    │
                           │      │ decides: │
                           │      │ impl     │
                           │      └──────────┘
                           │        │ │ │ │ │ │
                           │   ┌────┴─┴─┴─┴─┴─┴──┐
                           │   │                  │
                   ┌───────▼───▼────┐  ┌──────────▼──┐
                   │JsonFileTenantStoreInMemoryStore │
                   │   (Load JSON)  │  │ (Hardcoded) │
                   └───────┬────────┘  └──────┬──────┘
                           │                  │
              ┌────────────▼─────────────────▼┐
              │  ┌──────────────────────────┐ │
              │  │TenantConfiguration {     │ │
              │  │  TenantId: "abc"         │ │
              │  │  DefaultEndpoint: {      │ │
              │  │    BaseAddress: "https://│ │
              │  │     api.abc.com"         │ │
              │  │    Headers: {            │ │
              │  │      "X-Api-Key": "k123"│ │
              │  │    }                     │ │
              │  │  }                       │ │
              │  │  Endpoints: {            │ │
              │  │    "webhook": {...}      │ │
              │  │  }                       │ │
              │  │  Certificate: {...}      │ │
              │  │  Timeout: 30s            │ │
              │  │  HandlerLifetime: 5min   │ │
              │  │}                         │ │
              │  └──────────────────────────┘ │
              └──┬──────────────────────────┬─┘
                 │                          │
           ┌─────▼──────┐         ┌────────▼──┐
           │ Cache for  │         │ Return    │
           │ 5 minutes  │         │ directly  │
           │ (MISS path)│         │ (HIT path)│
           └─────┬──────┘         └────────┬──┘
                 │                        │
                 └────────────┬───────────┘
                              │
                    TenantConfiguration loaded
                              │
                              ▼
```

---

## Certificate Loading Phase

```
TenantConfiguration contains:
  Certificate: {
    Type: "File",
    Path: "certs/abc.pfx",
    Password: "secret"
  }

    │
    ▼
┌──────────────────────────────────┐
│ Extract CertificateConfiguration │
└──────┬──────────────────────────┬┘
       │                          │
       ▼                          ▼
  ┌─────────┐          ┌────────────────┐
  │ Type:   │          │ FileCertificate│
  │ "File"  │          │ Provider       │
  └─────────┘          │                │
                       │ 1. Read .pfx   │
                       │    file        │
                       │ 2. Load into   │
                       │    X509Cert    │
                       │ 3. With passwd │
                       │ 4. Return cert │
                       └────┬───────────┘
                            │
                    ┌───────▼────────┐
                    │ X509Certificate│
                    │     (Loaded)   │
                    └────────┬───────┘
                             │
                             ▼
             (Used by TenantHandlerCache
              to configure SocketsHttpHandler)

Alternative certificate providers (by type):
- "Store": StoreCertificateProvider (Windows Cert Store)
- "Base64": Base64CertificateProvider (Embedded cert)
- "KeyVault": (Azure Key Vault provider)
- Composite: Try multiple providers sequentially
```

---

## Handler Pooling Phase

```
Need handler for tenant "abc", endpoint "default"

    ┌────────────────────────────────────────┐
    │ TenantHandlerCache                     │
    │ .GetOrCreateHandler("abc", "default")  │
    └──────────┬─────────────────────┬───────┘
               │                     │
               ▼                     ▼
        ┌────────────┐         ┌─────────────┐
        │ Create     │         │ Check cache │
        │ cache key: │         │ for key     │
        │ "abc:      │         └─────┬────┬──┘
        │ default"   │               │    │
        └────────────┘          HIT  │    MISS
                                 │    │
                         ┌──────────┐│
                         │ Handler  ││
                         │ exists & ││
                         │ not      ││
                         │ expired? ││
                         └────┬─────┘│
                              │      │
                              YES NO YES
                              │   │ │
                    ┌─────────▼┐  │ │
                    │  Reuse   │  │ │
                    │ existing │  │ │
                    │ handler  │  │ │
                    └───┬──────┘  │ │
                        │        ┌▼─▼──────────────┐
                        │        │ Create new      │
                        │        │ SocketsHttpHandle
                        │        │                 │
                        │        │ 1. Instantiate  │
                        │        │ 2. Set properties│
                        │        │ 3. Attach cert  │
                        │        │ 4. Add to cache │
                        │        │ 5. Set lifetime │
                        │        └────┬────────────┘
                        │             │
                        │        ┌────▼──────────────────┐
                        │        │ SocketsHttpHandler    │
                        │        │ {                     │
                        │        │   Certificate: ...,   │
                        │        │   PooledConnections   │
                        │        │   Creation: now       │
                        │        │   Lifetime: 5 min     │
                        │        │ }                     │
                        │        └────┬─────────────────┘
                        │             │
                        └─────────┬───┘
                                  │
                        SocketsHttpHandler ready
```

---

## HttpClient Creation Phase

```
Have:
- TenantConfiguration
- SocketsHttpHandler (from cache)
- Endpoint name (or null for default)

    │
    ▼
┌──────────────────────────────────────┐
│ Select endpoint configuration         │
└──────┬──────────────────────────────┬─┘
       │                              │
       ▼                              ▼
  ┌────────────────┐          ┌──────────────┐
  │ Endpoint name? │          │ Named endpoint
  │ (null or named)│          │ specified    │
  └────────────────┘          └──────┬───────┘
           │                         │
           NO                      YES
           │                         │
   ┌───────▼────────┐         ┌──────▼──────────┐
   │ Use default    │         │ Look up named   │
   │ endpoint from  │         │ endpoint in     │
   │ config         │         │ config.Endpoints
   │                │         │ dictionary      │
   └───────┬────────┘         └──────┬──────────┘
           │                         │
           │            ┌────────────┤
           │            │            │
           │       FOUND    NOT FOUND
           │        │         │
    ┌──────▼──┐  ┌──▼──┐  ┌───▼──┐
    │Fallback │  │Use  │  │Error │
    │to default  │it   │  └──────┘
    └──────────┐ └──┬──┘
               │    │
         ┌─────▼────▼──────────────────┐
         │ EndpointConfiguration {     │
         │   BaseAddress,              │
         │   Headers,                  │
         │   Timeout (optional)        │
         │ }                           │
         └──────┬───────────────────┬──┘
                │                   │
                ▼                   ▼
        ┌──────────────┐   ┌─────────────────┐
        │ Has base     │   │ Resolve timeout │
        │ address?     │   │ priority:       │
        │              │   │ 1. Endpoint     │
        │ Validate not │   │ 2. Tenant       │
        │ null         │   │ 3. Default(100s)
        └──────┬───────┘   └────────┬────────┘
               │                    │
               │            Resolved timeout
               │                    │
               └──────────┬─────────┘
                          │
                          ▼
        ┌──────────────────────────────────┐
        │ Create HttpClient instance:      │
        │                                  │
        │ new HttpClient(                  │
        │   handler,                       │
        │   disposeHandler: false          │
        │ )                                │
        └──────┬─────────────────────────┬─┘
               │                         │
               ▼                         ▼
        ┌────────────┐         ┌───────────────┐
        │ Set        │         │ Set           │
        │ BaseAddress│         │ Timeout       │
        │            │         │               │
        │ https://   │         │ 30s (or from  │
        │ api.abc.   │         │ config)       │
        │ com        │         └───────┬───────┘
        └────┬───────┘                 │
             │                         │
             └───────────┬─────────────┘
                         │
                         ▼
        ┌──────────────────────────────────┐
        │ Apply tenant-level headers:      │
        │                                  │
        │ config.DefaultHeaders:           │
        │   "X-Api-Key": "k123"            │
        │   "X-Custom": "value"            │
        │ (Add to httpClient.              │
        │  DefaultRequestHeaders)          │
        └──────┬──────────────────────────┘
               │
               ▼
        ┌──────────────────────────────────┐
        │ Apply endpoint-level headers:    │
        │ (Override tenant defaults)       │
        │                                  │
        │ endpointConfig.Headers:          │
        │   "Authorization": "Bearer ..."  │
        │ (Replace existing if present,    │
        │  add if new)                     │
        └──────┬──────────────────────────┘
               │
               ▼
        ┌──────────────────────────────────┐
        │ HttpClient fully configured:     │
        │                                  │
        │ {                                │
        │   BaseAddress: https://api....   │
        │   Timeout: 30s                   │
        │   DefaultRequestHeaders: {       │
        │     X-Api-Key: k123,             │
        │     X-Custom: value,             │
        │     Authorization: Bearer ...    │
        │   }                              │
        │   HttpMessageHandler:            │
        │     SocketsHttpHandler (pooled)  │
        │ }                                │
        └──────┬──────────────────────────┘
               │
               ▼
        ┌──────────────────────────────────┐
        │ Return HttpClient to caller      │
        └──────────────────────────────────┘
```

---

## Request Execution Phase

```
Caller receives HttpClient:
    │
    ▼
┌────────────────────────────────────────┐
│ Caller executes request:               │
│                                        │
│ await httpClient.GetAsync("/users")    │
└────┬─────────────────────────────────┬─┘
     │                                 │
     ▼                                 ▼
  ┌──────────┐              ┌────────────────┐
  │ Build    │              │ HttpMessageHandler
  │ request  │              │ (SocketsHttpHandler)
  └──┬───────┘              │                    │
     │                      │ 1. Combines       │
     │                      │    - BaseAddr     │
     │                      │    - Route        │
     │                      │ 2. Applies headers│
     │                      │ 3. Applies timeout│
     │                      │ 4. Uses pooled    │
     │                      │    connection    │
     │                      │ 5. Uses cert if   │
     │                      │    mTLS required  │
     │                      └────┬─────────────┘
     │                           │
     │                      ┌────▼────────┐
     │                      │ HTTP Request │
     │                      │ sent to:     │
     │                      │ https://     │
     │                      │ api.abc.com/ │
     │                      │ users        │
     │                      └────┬────────┘
     │                           │
     │                    ┌──────▼──────┐
     │                    │ Tenant's API│
     │                    │ receives    │
     │                    │ request with│
     │                    │ cert & hdrs │
     │                    └────┬───────┘
     │                         │
     │                    ┌────▼──────┐
     │                    │ Response   │
     │                    │ returned   │
     │                    └────┬───────┘
     │                         │
     └───────────┬─────────────┘
                 │
                 ▼
        ┌─────────────────────┐
        │ HttpResponseMessage │
        │ {                   │
        │   StatusCode: 200,  │
        │   Headers: {...},   │
        │   Content: {...}    │
        │ }                   │
        └────────┬────────────┘
                 │
                 ▼
        ┌──────────────────────┐
        │ Caller processes     │
        │ response             │
        └──────────────────────┘
```

---

## Summary

**Key Data Transforms**:

1. HTTP Request → Tenant ID (via Resolver)
2. Tenant ID → Tenant Configuration (via Store + Cache)
3. Configuration → Certificate (via CertificateProvider)
4. Configuration + Certificate → SocketsHttpHandler (via Cache)
5. Handler + Configuration → HttpClient instance
6. HttpClient → HTTP Request to tenant's API

**Caching Layers**:

- Configuration cache: 5 minutes
- Handler cache: Configurable (default 5 minutes)
- Connection pooling: Built into SocketsHttpHandler

**Performance Optimizations**:

- Handlers are reused across requests
- Certificates are attached once and reused
- Configuration loaded once and cached
- DNS resolution can be rotated via handler lifetime
