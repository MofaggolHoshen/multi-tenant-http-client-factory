# Multi-Tenant HttpClientFactory - Quick Overview

## 📍 The Problem

In a multi-tenant SaaS application, different customers (tenants) often need to:

- Call different APIs (different base URLs)
- Use different authentication (API keys, certificates)
- Apply different timeouts
- Send tenant-specific headers
- Use client certificates for mTLS

Managing this manually for each tenant is complex, error-prone, and doesn't scale.

## ✅ The Solution

This library provides a **factory that creates tenant-specific HttpClient instances** with all the right configuration automatically.

```csharp
// One simple line - everything is configured!
var client = factory.CreateClient();

// Then use it like a normal HttpClient
var response = await client.GetAsync("/api/users");
```

---

## 🔄 How It Works in 5 Steps

### Step 1: Request Arrives

```
Browser/Client sends HTTP request
    ↓
Request contains tenant identifier:
  - HTTP header: X-Tenant-Id: acme-corp
  - URL route: /acme-corp/api/users
  - JWT claim: sub: acme-corp
  - Subdomain: acme-corp.api.example.com
```

### Step 2: Tenant Resolution

```
TenantResolutionMiddleware extracts tenant ID
    ↓
Stored in scoped TenantContext service
    ↓
Result: TenantId = "acme-corp"
```

### Step 3: Configuration Loading

```
Factory requests configuration from TenantStore
    ↓
Checked against MemoryCache first (fast!)
    ↓
Cache MISS → Load from store (JSON file, DB, API, etc.)
    ↓
Result: TenantConfiguration {
  BaseAddress: "https://api.acme-corp.example.com",
  Headers: { "X-Api-Key": "secret123" },
  Timeout: 30 seconds,
  Certificate: { Type: "File", Path: "certs/acme.pfx" }
}
```

### Step 4: Certificate & Handler Setup

```
Certificate loaded based on configuration
    ↓
Attached to SocketsHttpHandler (pooled and reused)
    ↓
Handler cached with configurable lifetime
    ↓
Result: Ready-to-use HTTP handler with certificate
```

### Step 5: HttpClient Creation

```
HttpClient created using cached handler
    ↓
BaseAddress set to tenant's endpoint
    ↓
Headers applied (tenant defaults + endpoint overrides)
    ↓
Timeout applied
    ↓
Result: Fully configured HttpClient ready to use!
```

---

## 🏗️ Architecture - 3 Key Layers

```
┌─────────────────────────────────────────────────┐
│ Resolution Layer (Per Request)                  │
│ - Extracts tenant from: headers/claims/routes   │
│ - Stores in scoped TenantContext                │
└─────────────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────┐
│ Configuration Layer (Cached)                    │
│ - Loads tenant config from pluggable store      │
│ - Caches for 5 minutes                          │
│ - Loads certificates as needed                  │
└─────────────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────┐
│ Handler & Client Layer (Pooled)                 │
│ - Creates/reuses SocketsHttpHandlers            │
│ - Pooled HTTP connections                       │
│ - Creates HttpClient instances                  │
└─────────────────────────────────────────────────┘
```

---

## 📋 Key Extensibility Points

### 1. **Tenant Resolution** - Where does the tenant ID come from?

- HeaderTenantResolver (HTTP headers)
- ClaimsTenantResolver (JWT claims)
- RouteTenantResolver (URL routes)
- SubdomainTenantResolver (subdomains)
- Or implement ITenantResolver for custom logic

### 2. **Tenant Storage** - Where are configurations stored?

- JsonFileTenantStore (config file)
- InMemoryTenantStore (hardcoded)
- Or implement ITenantStore for:
  - SQL Server / PostgreSQL
  - CosmosDB
  - Redis
  - External REST API
  - Anything!

### 3. **Certificate Loading** - How are certificates obtained?

- FileCertificateProvider (.pfx files)
- StoreCertificateProvider (Windows Cert Store)
- Base64CertificateProvider (embedded)
- AzureKeyVaultCertificateProvider (Azure Key Vault)
- Or implement ICertificateProvider for custom sources

---

## ⚡ Performance Features

### Connection Pooling

```
Handler is cached and reused across requests
    ↓
TCP connections pooled (DNS resolved once)
    ↓
Significant latency reduction
```

### Configuration Caching

```
First load: Query TenantStore
    ↓
Cache for 5 minutes with sliding expiration
    ↓
Subsequent requests hit in-memory cache
    ↓
Zero I/O cost for repeated requests
```

### Lazy Certificate Loading

```
Certificate loaded only when needed
    ↓
Cached in the SocketsHttpHandler
    ↓
Not reloaded on every request
```

---

## 🔄 Hot-Reload Support

Configuration changes take effect without restart:

```
Admin updates tenant configuration
    ↓
TenantStore fires IChangeToken
    ↓
MemoryCache invalidated
    ↓
Old handlers cleaned up gracefully
    ↓
Next request loads fresh configuration
```

---

## 📊 Component Interaction

```
                 ┌─────────────────┐
                 │ HTTP Request    │
                 │ (with tenant)   │
                 └────────┬────────┘
                          │
                          ▼
          ┌───────────────────────────────┐
          │ TenantResolutionMiddleware    │
          │ Extract tenant from context   │
          └────────┬────────────────────┬─┘
                   │                    │
              ┌────▼────┐          ┌────▼──┐
              │Resolver │          │TenantCtx
              │(header/ │          │(Scoped)
              │claim)   │          └────┬──┘
              └────┬────┘               │
                   │                    │
                   └──────────┬─────────┘
                              │
                              ▼
                 ┌─────────────────────────────┐
                 │ TenantHttpClientFactory     │
                 │ .CreateClient()             │
                 └────┬────────────────────┬──┘
                      │                    │
                ┌─────▼─────┐      ┌──────▼──────┐
                │Config     │      │Handler      │
                │Provider   │      │Cache        │
                │(caching)  │      │             │
                └─────┬─────┘      └──────┬──────┘
                      │                   │
              ┌───────▼──────┐   ┌────────▼────┐
              │TenantStore   │   │Certificate  │
              │(pluggable)   │   │Provider     │
              └──────────────┘   └─────────────┘
                      │                   │
                      └────────┬──────────┘
                               │
                               ▼
                    ┌──────────────────────┐
                    │ HttpClient (ready)   │
                    │ - BaseAddress set    │
                    │ - Headers applied    │
                    │ - Timeout set        │
                    │ - Handler pooled     │
                    │ - Certificate ready  │
                    └──────────────────────┘
                               │
                               ▼
                    ┌──────────────────────┐
                    │ Caller service/      │
                    │ controller uses it   │
                    │ for API calls        │
                    └──────────────────────┘
```

---

## 🎓 Common Usage Scenarios

### Scenario 1: Web API Gateway

```
Browser request with X-Tenant-Id header
    ↓
Gateway resolves tenant from header
    ↓
Creates HttpClient for that tenant
    ↓
Forwards request to tenant's backend API
    ↓
Response returned to browser
```

### Scenario 2: Background Jobs

```
Job needs to call tenant-specific API
    ↓
Explicitly specifies tenant ID
    ↓
factory.CreateClient("tenant-xyz")
    ↓
Makes request with tenant config
```

### Scenario 3: Microservices

```
Service A calls Service B (different tenant endpoints)
    ↓
Injects ITenantHttpClientFactory
    ↓
Gets auto-configured HttpClient
    ↓
Different tenant = different Service B endpoint
```

---

## 📈 Configuration Example

```json
{
  "Tenants": {
    "acme-corp": {
      "Endpoints": {
        "default": {
          "BaseAddress": "https://api.acme.example.com",
          "Headers": {
            "X-Api-Key": "acme-secret-key",
            "Authorization": "Bearer token123"
          }
        },
        "webhook": {
          "BaseAddress": "https://webhooks.acme.example.com",
          "Headers": {
            "Authorization": "Bearer webhook-token"
          }
        }
      },
      "DefaultEndpointName": "default",
      "Certificate": {
        "Type": "File",
        "Path": "certs/acme.pfx",
        "Password": "cert-password"
      },
      "Timeout": "00:00:30",
      "HandlerLifetime": "00:05:00"
    },
    "globex-inc": {
      "Endpoints": {
        "default": {
          "BaseAddress": "https://api.globex.example.com"
        }
      },
      "DefaultEndpointName": "default"
      // ... more config
    }
  }
}
```

---

## 🚀 Getting Started

1. **Implement ITenantStore** — Where your tenant configs come from
2. **Register in DI** — Use the builder API
3. **Add Middleware** — Resolve tenant from requests
4. **Inject Factory** — Use in your services
5. **Call CreateClient()** — Get tenant-specific HttpClient!

For detailed setup steps, see [README.md Quick Start](../README.md#quick-start).

---

## 📚 Documentation Files

- **ARCHITECTURE.md** — Deep dive into components and design patterns
- **DATA_FLOW.md** — Step-by-step request lifecycle with diagrams
- **README.md** — Quick start and feature overview

## 🎯 Summary

This library turns multi-tenant HTTP client management from complex to **simple, declarative, and efficient**. Define tenant configurations once, the library handles the rest — resolution, caching, certificate loading, connection pooling, and hot-reload.

**Key Benefits:**

- ✅ Reduced boilerplate code
- ✅ Better performance through pooling & caching
- ✅ Hot-reload without restarts
- ✅ Pluggable everywhere
- ✅ Scales to hundreds of tenants
