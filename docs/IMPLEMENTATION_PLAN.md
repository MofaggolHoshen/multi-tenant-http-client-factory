# Plan: Multi-Tenant HttpClientFactory Library

## TL;DR

Build a .NET library that extends `IHttpClientFactory` to support multi-tenant scenarios — resolving the current tenant from incoming requests, loading tenant-specific configuration (endpoints, certificates, headers, timeouts), and serving pooled `HttpClient` instances per tenant with hot-reload support.

## Architecture Overview

**Core pattern**: Wrap the built-in `IHttpClientFactory` using **named clients** where each tenant maps to a named client registered dynamically. A `ITenantHttpClientFactory` facade resolves the tenant, looks up config, and returns the appropriate pre-configured `HttpClient`.

**Key design decisions**:

- Target `netstandard2.0` (abstractions) + `net8.0;net10.0` (implementation) for broad compat
- Strategy pattern for all extensibility points (tenant resolution, cert providers, config providers)
- `ConcurrentDictionary`-based handler cache with `IChangeToken` for hot-reload invalidation
- Builder pattern for DI registration

---

## Project Structure

```
MultiTenantHttpClientFactory.sln
├── src/
│   ├── MultiTenantHttpClientFactory.Abstractions/   (netstandard2.0)
│   │   ├── ITenantHttpClientFactory.cs
│   │   ├── ITenantResolver.cs
│   │   ├── ITenantConfigurationProvider.cs
│   │   ├── ICertificateProvider.cs
│   │   ├── Models/
│   │   │   ├── TenantConfiguration.cs
│   │   │   ├── EndpointConfiguration.cs
│   │   │   └── CertificateConfiguration.cs
│   │   └── TenantResolutionContext.cs
│   │
│   ├── MultiTenantHttpClientFactory/               (net8.0;net10.0)
│   │   ├── TenantHttpClientFactory.cs              (main factory)
│   │   ├── TenantHandlerCache.cs                   (pooled handler management)
│   │   ├── TenantResolution/
│   │   │   ├── HeaderTenantResolver.cs
│   │   │   ├── RouteTenantResolver.cs
│   │   │   ├── SubdomainTenantResolver.cs
│   │   │   ├── ClaimsTenantResolver.cs
│   │   │   └── CompositeTenantResolver.cs
│   │   ├── Configuration/
│   │   │   ├── JsonTenantConfigurationProvider.cs
│   │   │   ├── DatabaseTenantConfigurationProvider.cs (interface + EF impl)
│   │   │   └── InMemoryTenantConfigurationProvider.cs
│   │   ├── Certificates/
│   │   │   ├── FileCertificateProvider.cs           (.pfx)
│   │   │   ├── StoreCertificateProvider.cs          (Windows cert store)
│   │   │   ├── KeyVaultCertificateProvider.cs       (Azure Key Vault)
│   │   │   └── Base64CertificateProvider.cs         (inline config)
│   │   └── DependencyInjection/
│   │       ├── MultiTenantHttpClientBuilder.cs      (builder pattern)
│   │       └── ServiceCollectionExtensions.cs
│   │
│   └── MultiTenantHttpClientFactory.AzureKeyVault/ (optional, net8.0;net10.0)
│       └── KeyVaultCertificateProvider.cs
│
├── samples/
│   └── SampleGateway/                              (ASP.NET Core Web API)
│       ├── Program.cs
│       └── appsettings.json
│
└── tests/
    └── MultiTenantHttpClientFactory.Tests/
        ├── TenantHttpClientFactoryTests.cs
        ├── TenantResolverTests.cs
        └── CertificateProviderTests.cs
```

---

## Phases

---

### Phase 1: Abstractions & Models — ✅ Complete

> **Goal**: Define the public API surface and data models that all consumers and implementations depend on. This phase produces zero runtime behavior — only contracts and DTOs.

| #    | Task                                            | Description                                                                                                                                                                                                                                                                                                                                                                                                                                                                              | Status |
| ---- | ----------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------ |
| 1.1  | Create solution & project scaffolding           | Run `dotnet new sln`, create `MultiTenantHttpClientFactory.Abstractions` (netstandard2.0), `MultiTenantHttpClientFactory` (net8.0;net10.0), `SampleGateway` (webapi), and `MultiTenantHttpClientFactory.Tests` (xunit). Wire project references and NuGet dependencies.                                                                                                                                                                                                                  | ✅     |
| 1.2  | Define `TenantConfiguration` model              | The root model representing a single tenant's HTTP config. Contains `TenantId` (string, primary key), `Endpoints` (Dictionary mapping logical name → `EndpointConfiguration`), `DefaultEndpoint` (fallback when no endpoint name specified), `Certificate` (`CertificateConfiguration`), `DefaultHeaders` (Dictionary<string,string> applied to every request), `Timeout` (nullable TimeSpan), `HandlerLifetime` (nullable TimeSpan, controls pooled handler rotation).                  | ✅     |
| 1.3  | Define `EndpointConfiguration` model            | Represents one outbound endpoint for a tenant: `BaseAddress` (Uri), `Headers` (Dictionary<string,string> merged with tenant-level defaults), `Timeout` (nullable, overrides tenant-level), `Certificate` (nullable, overrides tenant-level cert for this endpoint specifically). This allows one tenant to talk to multiple downstream services with different certs.                                                                                                                    | ✅     |
| 1.4  | Define `CertificateConfiguration` model         | Describes how to obtain a client certificate. Contains `Type` enum (`File`, `Store`, `Base64`, `KeyVault`), `Path` (for file-based, relative or absolute), `Password` (for .pfx), `Thumbprint` (for store lookup), `Base64Data` (inline cert), `KeyVaultUri` + `CertificateName` (for Azure Key Vault). Only the fields relevant to the selected `Type` are used.                                                                                                                        | ✅     |
| 1.5  | Define `ITenantHttpClientFactory` interface     | The main entry point consumers inject. Two overloads: (a) `HttpClient CreateClient(string tenantId, string? endpointName = null)` — explicit, works in background jobs, event handlers, fan-out scenarios; (b) `HttpClient CreateClient(string? endpointName = null)` — implicit, resolves tenant from current HTTP context (throws `TenantNotResolvedException` if no context). Both validate tenant exists in store and throw `TenantNotFoundException` for unknown tenant IDs.        | ✅     |
| 1.6  | Define `ITenantStore` interface                 | **The primary abstraction consumers implement to plug in any storage.** Methods: `Task<TenantConfiguration?> GetTenantAsync(string tenantId, CancellationToken)` — fetch one tenant; `Task<IReadOnlyList<TenantConfiguration>> GetAllTenantsAsync(CancellationToken)` — enumerate all tenants (for preloading/admin); `IChangeToken GetReloadToken()` — fires when tenant data changes in storage (enables hot-reload). Consumers implement for SQL, CosmosDB, Redis, external API, etc. | ✅     |
| 1.7  | Define `ITenantResolver` interface              | Contract for extracting tenant identity from an incoming HTTP request. Single method: `Task<string?> ResolveAsync(HttpContext context, CancellationToken)`. Returns `null` if this resolver can't determine the tenant (chain continues to next resolver). Multiple resolvers are composed in priority order.                                                                                                                                                                            | ✅     |
| 1.8  | Define `ITenantConfigurationProvider` interface | Internal orchestrator that sits between the factory and the store. Methods: `Task<TenantConfiguration?> GetConfigurationAsync(string tenantId)` (with in-memory caching), `IChangeToken GetChangeToken(string tenantId)`. This interface exists to decouple the factory from caching/reload mechanics — most consumers won't implement this directly.                                                                                                                                    | ✅     |
| 1.9  | Define `ICertificateProvider` interface         | Contract for loading an `X509Certificate2` from a `CertificateConfiguration`. Single method: `Task<X509Certificate2?> GetCertificateAsync(CertificateConfiguration config, CancellationToken)`. Implementations handle one `CertificateType` each. A composite provider routes to the correct implementation based on `config.Type`.                                                                                                                                                     | ✅     |
| 1.10 | Define `ITenantContext` interface               | Scoped service that holds the resolved tenant for the current request. Properties: `string? TenantId`, `TenantConfiguration? Configuration`, `bool IsResolved`. Set by middleware, consumed by the implicit `CreateClient()` overload and any other request-scoped code that needs tenant info.                                                                                                                                                                                          | ✅     |
| 1.11 | Define exception types                          | `TenantNotFoundException` (thrown when requested tenantId doesn't exist in store), `TenantNotResolvedException` (thrown when implicit resolution fails — no middleware, or no resolver matched), `CertificateLoadException` (wraps cert loading failures with context about which tenant/config caused it).                                                                                                                                                                              | ✅     |

---

### Phase 2: Core Implementation — ✅ Complete

> **Goal**: Build the runtime engine — handler pooling, factory logic, and tenant resolution chain. After this phase, the library is functional with in-memory configuration.

| #    | Task                                    | Description                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    | Status |
| ---- | --------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------ |
| 2.1  | Implement `TenantHandlerCache`          | Thread-safe cache for `HttpMessageHandler` instances keyed by `"{tenantId}:{endpointName}"`. Uses `ConcurrentDictionary<string, Lazy<ActiveHandlerEntry>>` where `ActiveHandlerEntry` tracks: the handler, creation timestamp, expiry time, and a reference count. When a handler expires (based on `HandlerLifetime`), it's marked for cleanup but not disposed until all active `HttpClient` instances using it are GC'd (via weak reference tracking, mirroring `DefaultHttpClientFactory` internal behavior). A background `Timer` periodically scans for expired entries. | ✅     |
| 2.2  | Implement handler creation logic        | Factory method inside `TenantHandlerCache` that creates a new `SocketsHttpHandler` configured with: `PooledConnectionLifetime` (for DNS rotation), `SslOptions.ClientCertificates` (loaded from `ICertificateProvider`), proxy settings if configured. Returns the handler wrapped in any tenant-specific `DelegatingHandler` pipeline.                                                                                                                                                                                                                                        | ✅     |
| 2.3  | Implement `TenantConfigurationProvider` | Default implementation of `ITenantConfigurationProvider`. Wraps an `ITenantStore`, adds `MemoryCache` layer with sliding expiration. Subscribes to `ITenantStore.GetReloadToken()` — when fired, evicts all cached entries and fires its own per-tenant change tokens. Exposes `GetChangeToken(tenantId)` using `CancellationTokenSource`-backed `CancellationChangeToken`.                                                                                                                                                                                                    | ✅     |
| 2.4  | Implement `TenantHttpClientFactory`     | The main `ITenantHttpClientFactory` implementation. Explicit path: validates tenantId against store (throws `TenantNotFoundException`), gets `TenantConfiguration`, resolves endpoint config (default or named), calls `TenantHandlerCache.GetOrCreateHandler(...)`, creates `HttpClient` with `BaseAddress`, applies `DefaultHeaders` + endpoint headers, sets `Timeout`. Implicit path: reads `ITenantContext` from scoped service provider, extracts `TenantId`, delegates to explicit path.                                                                                | ✅     |
| 2.5  | Implement `HeaderTenantResolver`        | Reads tenant from a configurable HTTP header (default: `X-Tenant-Id`). Constructor accepts header name as parameter. Returns header value or `null` if header missing. Case-insensitive header lookup.                                                                                                                                                                                                                                                                                                                                                                         | ✅     |
| 2.6  | Implement `RouteTenantResolver`         | Extracts tenant from a route segment. Configurable route parameter name (default: `tenantId`). Reads from `HttpContext.Request.RouteValues`. Returns `null` if route value not present.                                                                                                                                                                                                                                                                                                                                                                                        | ✅     |
| 2.7  | Implement `SubdomainTenantResolver`     | Parses tenant from the first subdomain segment. E.g., `tenantA.api.example.com` → `"tenantA"`. Configurable: which segment index to use (default: 0), base domain to strip. Returns `null` for bare domains or IP addresses.                                                                                                                                                                                                                                                                                                                                                   | ✅     |
| 2.8  | Implement `ClaimsTenantResolver`        | Reads tenant from authenticated user's claims. Configurable claim type (default: `"tenant_id"`). Reads from `HttpContext.User.Claims`. Returns `null` if user not authenticated or claim not present.                                                                                                                                                                                                                                                                                                                                                                          | ✅     |
| 2.9  | Implement `CompositeTenantResolver`     | Orchestrates multiple `ITenantResolver` instances in registration order. Iterates through all resolvers, calling `ResolveAsync` on each. Returns the first non-null result. If all return null, returns null (middleware will decide whether to throw or allow anonymous). Logs which resolver succeeded for diagnostics.                                                                                                                                                                                                                                                      | ✅     |
| 2.10 | Implement `TenantContext`               | Simple scoped class implementing `ITenantContext`. Properties with get/set. Set by middleware after resolution. If accessed before middleware runs, `IsResolved` returns `false`.                                                                                                                                                                                                                                                                                                                                                                                              | ✅     |
| 2.11 | Implement `TenantResolutionMiddleware`  | ASP.NET Core middleware that runs early in the pipeline. Calls `CompositeTenantResolver.ResolveAsync(context)`, looks up `TenantConfiguration` from provider, populates the scoped `ITenantContext`. Configurable behavior when resolution fails: throw 400, pass through (anonymous), or invoke a custom handler.                                                                                                                                                                                                                                                             | ✅     |

---

### Phase 3: Configuration & Certificate Providers — 🔲 Not Started

> **Goal**: Implement built-in storage and certificate providers so the library works out-of-the-box with common setups (JSON config, file certificates). These are all implementations of the Phase 1 abstractions.

| #   | Task                                     | Description                                                                                                                                                                                                                                                                                                                                                                                                        | Status                                                                                                                                                                                           |
| --- | ---------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | --- |
| 3.1 | Implement `JsonFileTenantStore`          | Reads tenant configuration from `IConfiguration` (typically backed by `appsettings.json`). Binds section (configurable, default: `"Tenants"`) to `Dictionary<string, TenantConfiguration>`. Uses `IOptionsMonitor<Dictionary<string, TenantConfiguration>>` internally for hot-reload. `GetReloadToken()` returns the monitor's change token — fires automatically when the JSON file changes on disk.             | 🔲                                                                                                                                                                                               |
| 3.2 | Implement `InMemoryTenantStore`          | Wraps a `ConcurrentDictionary<string, TenantConfiguration>` passed at construction. Useful for testing and scenarios where config comes from code. `GetReloadToken()` returns a manually-triggered `CancellationChangeToken`. Exposes `AddOrUpdate(TenantConfiguration)` and `Remove(string tenantId)` methods that fire the change token.                                                                         | 🔲                                                                                                                                                                                               |
| 3.3 | Implement `FileCertificateProvider`      | Handles `CertificateType.File`. Loads certificate from `config.Path` using `new X509Certificate2(path, config.Password, X509KeyStorageFlags.MachineKeySet                                                                                                                                                                                                                                                          | EphemeralKeySet)`. Validates file exists before loading. Caches loaded certs by path+thumbprint to avoid repeated disk I/O. Throws `CertificateLoadException` with file path context on failure. | 🔲  |
| 3.4 | Implement `StoreCertificateProvider`     | Handles `CertificateType.Store`. Opens `X509Store(StoreName.My, StoreLocation.LocalMachine)` (configurable). Finds certificate by `config.Thumbprint` using `X509FindType.FindByThumbprint`. Returns first match or throws. Works on Windows; on Linux, reads from OpenSSL store path.                                                                                                                             | 🔲                                                                                                                                                                                               |
| 3.5 | Implement `Base64CertificateProvider`    | Handles `CertificateType.Base64`. Decodes `config.Base64Data` from base64 → byte array → `new X509Certificate2(bytes, config.Password)`. Useful for Docker/K8s secrets or environment variables where certs are injected as strings.                                                                                                                                                                               | 🔲                                                                                                                                                                                               |
| 3.6 | Implement `KeyVaultCertificateProvider`  | Handles `CertificateType.KeyVault`. Uses `Azure.Security.KeyVault.Certificates.CertificateClient` to download certificate by `config.CertificateName` from `config.KeyVaultUri`. Authenticates via `DefaultAzureCredential`. Caches downloaded cert with configurable TTL (default 1 hour) to avoid hitting Key Vault rate limits. Lives in separate NuGet package (`MultiTenantHttpClientFactory.AzureKeyVault`). | 🔲                                                                                                                                                                                               |
| 3.7 | Implement `CompositeCertificateProvider` | Routes to the correct `ICertificateProvider` based on `config.Type`. Maintains a `Dictionary<CertificateType, ICertificateProvider>` populated from DI. Throws `NotSupportedException` for unregistered types with a clear message about which provider package to install.                                                                                                                                        | 🔲                                                                                                                                                                                               |

---

### Phase 4: DI & Builder Pattern — 🔲 Not Started

> **Goal**: Create a fluent, discoverable API for registering all components in `IServiceCollection`. Consumers should be able to get started with 2-3 lines of code and progressively customize.

| #   | Task                                     | Description                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               | Status |
| --- | ---------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------ |
| 4.1 | Implement `MultiTenantHttpClientBuilder` | Fluent builder class returned by the entry-point extension method. Stores configuration actions to apply during service registration. Methods: `AddTenantResolver<T>()` (registers resolver in priority order), `WithTenantStore<T>()` (registers custom store implementation), `WithJsonConfiguration(string sectionName)` (registers `JsonFileTenantStore`), `WithCertificateProvider<T>()` (registers cert provider for a specific type), `ConfigureDefaultHandler(Action<SocketsHttpHandler>)` (global handler customization), `SetDefaultHandlerLifetime(TimeSpan)`, `AddDelegatingHandler<T>()` (adds to pipeline for all tenants). | 🔲     |
| 4.2 | Implement `ServiceCollectionExtensions`  | Entry point: `services.AddMultiTenantHttpClientFactory()` that registers core services (`ITenantHttpClientFactory` as singleton, `ITenantConfigurationProvider`, `TenantHandlerCache`, `ITenantContext` as scoped, `CompositeTenantResolver`, `CompositeCertificateProvider`) and returns the builder for chaining.                                                                                                                                                                                                                                                                                                                       | 🔲     |
| 4.3 | Implement `ApplicationBuilderExtensions` | Entry point: `app.UseTenantResolution()` that inserts `TenantResolutionMiddleware` into the pipeline. Overload accepting `TenantResolutionOptions` for configuring failure behavior (return 400, pass-through, custom delegate).                                                                                                                                                                                                                                                                                                                                                                                                          | 🔲     |
| 4.4 | Implement options validation             | Uses `IValidateOptions<T>` to validate configuration at startup: at least one tenant resolver registered, at least one tenant exists in store (optional warning), cert providers registered for all cert types used in config. Fail-fast on misconfiguration with actionable error messages.                                                                                                                                                                                                                                                                                                                                              | 🔲     |

---

### Phase 5: Hot-Reload & Lifecycle — 🔲 Not Started

> **Goal**: Ensure the library responds gracefully to configuration changes at runtime — new tenants, removed tenants, rotated certificates — without requiring a restart and without leaking resources.

| #   | Task                                              | Description                                                                                                                                                                                                                                                                                                                                                                                                        | Status |
| --- | ------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------ |
| 5.1 | Wire change-token propagation                     | Connect `ITenantStore.GetReloadToken()` → `TenantConfigurationProvider` cache eviction → `TenantHandlerCache` handler invalidation. When store signals a change: (a) clear the config cache, (b) identify affected tenants, (c) mark their cached handlers as expired. New requests get fresh handlers with updated config/certs.                                                                                  | 🔲     |
| 5.2 | Implement graceful handler disposal               | Expired handlers must not be disposed immediately — active `HttpClient` instances may still reference them. Implement a two-phase cleanup: Phase 1 marks handler as "expired" (no new clients will use it). Phase 2 disposes handler after a configurable grace period (default: 5 minutes) or when no active references remain (tracked via `WeakReference`). Log warnings for handlers that exceed grace period. | 🔲     |
| 5.3 | Implement `PollingChangeToken`                    | A reusable `IChangeToken` implementation for stores that don't support push notifications (e.g., database, external API). Accepts a `Func<CancellationToken, Task<bool>>` that checks for changes on a configurable interval (default: 30 seconds). Fires `HasChanged` when the delegate returns `true`. Used by database/API store implementations.                                                               | 🔲     |
| 5.4 | Implement certificate rotation handling           | When a tenant's cert config changes (detected via change token): load the new cert, create a new handler with the new cert, mark old handler as expired. If new cert fails to load: log error, keep old handler active (don't break existing traffic), fire a health check warning.                                                                                                                                | 🔲     |
| 5.5 | Implement `IHostedService` for background cleanup | A background service (`TenantHandlerCleanupService`) that runs on a timer (default: every 60 seconds). Scans `TenantHandlerCache` for expired entries past their grace period and disposes them. Also logs metrics: active handlers count, expired-but-not-yet-disposed count, total tenants tracked.                                                                                                              | 🔲     |

---

### Phase 6: Sample & Tests — 🔲 Not Started

> **Goal**: Demonstrate the library in a realistic gateway scenario and validate correctness with comprehensive tests.

| #   | Task                                   | Description                                                                                                                                                                                                                                                                                                                                        | Status |
| --- | -------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------ |
| 6.1 | Build `SampleGateway` Program.cs       | ASP.NET Core Minimal API that registers multi-tenant HttpClient factory with 3 tenants (configured in appsettings.json). Exposes a `POST /proxy/{tenantId}/{*path}` endpoint that creates a client for the specified tenant and forwards the request to the tenant's configured backend. Demonstrates both explicit and implicit resolution.       | 🔲     |
| 6.2 | Build `SampleGateway` appsettings.json | Configure 3 tenants: (a) TenantA — file-based .pfx cert, single endpoint; (b) TenantB — base64 inline cert, multiple endpoints (payments, notifications); (c) TenantC — no cert (plain HTTPS), custom headers. Show all model properties in use.                                                                                                   | 🔲     |
| 6.3 | Unit tests: Tenant resolvers           | Test each resolver independently with mocked `HttpContext`. Verify header resolver reads correct header, route resolver extracts route value, subdomain parser handles edge cases (no subdomain, IP address, multi-level), claims resolver works with authenticated/unauthenticated user. Test composite resolver priority ordering.               | 🔲     |
| 6.4 | Unit tests: Configuration provider     | Test `JsonFileTenantStore` with in-memory `IConfiguration`. Verify correct deserialization of all model properties. Test `InMemoryTenantStore` add/remove/change-token firing. Test `TenantConfigurationProvider` caching behavior (second call doesn't hit store, change token evicts cache).                                                     | 🔲     |
| 6.5 | Unit tests: Certificate providers      | Test `FileCertificateProvider` with a test .pfx file. Test `Base64CertificateProvider` with a base64-encoded test cert. Test `CompositeCertificateProvider` routing. Verify `CertificateLoadException` thrown for invalid paths/data. Mock `CertificateClient` for Key Vault provider test.                                                        | 🔲     |
| 6.6 | Unit tests: Factory integration        | End-to-end test with `InMemoryTenantStore` + mock handler (using `MockHttpMessageHandler` or custom `DelegatingHandler`). Verify: correct base address applied, correct headers set, correct cert attached to handler's SSL options, `TenantNotFoundException` for unknown tenant, handler reuse within lifetime.                                  | 🔲     |
| 6.7 | Unit tests: Handler cache lifecycle    | Test with a controlled `ISystemClock` (or `TimeProvider` on .NET 8+). Verify: handler created on first access, same handler returned within lifetime, new handler created after expiry, expired handler not disposed immediately (grace period), disposed after grace period. Test concurrent access returns same handler (no duplicate creation). | 🔲     |
| 6.8 | Integration tests: Hot-reload          | Start with config A, create client, modify config (simulate file change), verify next `CreateClient` call picks up new base address. Test cert rotation: swap cert in config, verify new handler uses new cert. Test tenant removal: remove tenant from store, verify `TenantNotFoundException`.                                                   | 🔲     |

---

## Relevant Files (to create)

- `src/MultiTenantHttpClientFactory.Abstractions/ITenantStore.cs` — **primary abstraction for any tenant storage**
- `src/MultiTenantHttpClientFactory.Abstractions/ITenantHttpClientFactory.cs` — main factory interface
- `src/MultiTenantHttpClientFactory.Abstractions/ITenantResolver.cs` — tenant resolution contract
- `src/MultiTenantHttpClientFactory.Abstractions/ITenantConfigurationProvider.cs` — config provider with change tokens
- `src/MultiTenantHttpClientFactory.Abstractions/ICertificateProvider.cs` — cert loading contract
- `src/MultiTenantHttpClientFactory.Abstractions/Models/TenantConfiguration.cs` — core model
- `src/MultiTenantHttpClientFactory/TenantHttpClientFactory.cs` — main implementation
- `src/MultiTenantHttpClientFactory/TenantHandlerCache.cs` — handler pooling + expiry
- `src/MultiTenantHttpClientFactory/DependencyInjection/ServiceCollectionExtensions.cs` — DI entry point
- `samples/SampleGateway/Program.cs` — usage example
- `samples/SampleGateway/appsettings.json` — multi-tenant config example

---

## Verification

1. `dotnet build` compiles all projects without errors
2. Unit tests pass: `dotnet test` — covering resolver chain, config loading, cert attachment, factory integration
3. Run SampleGateway, send requests with different `X-Tenant-Id` headers → verify different base URLs/certs used (observable via logging)
4. Modify `appsettings.json` while running → verify new config picked up without restart (hot-reload)
5. Verify handler cache evicts expired handlers (unit test with controlled clock)
6. Verify no certificate/connection leaks under concurrent load (integration test with many tenants)

---

## Decisions

- **netstandard2.0 for abstractions** — maximizes reusability across .NET Framework and modern .NET
- **SocketsHttpHandler preferred** — better performance, `PooledConnectionLifetime` for DNS rotation
- **Named-client pattern over custom factory** — leverages existing `IHttpClientFactory` infrastructure where possible, custom cache only for dynamic tenant scenarios
- **Composite pattern for resolvers and cert providers** — allows stacking strategies without code changes
- **Excluded from scope**: OAuth token management per tenant (can be added as delegating handler), response caching, retry policies (Polly integration is orthogonal)

---

## Further Considerations

1. **Thread safety for cert rotation**: When a cert is renewed in Key Vault, the old handler must be gracefully drained. Recommend implementing a reference-counting approach on handlers, similar to how `DefaultHttpClientFactory` does it internally.

2. **NuGet packaging**: Should the library ship as a single package or split into `Abstractions` + `Core` + `AzureKeyVault`? Recommend split for optional Azure dependency.

3. **Polly integration**: Should resilience policies (retry, circuit-breaker) be tenant-configurable? Could be a Phase 2 enhancement with `AddPolicyHandler` per tenant config.
