# Multi-Tenant HttpClientFactory - Documentation Index

Welcome! This folder contains comprehensive documentation for the Multi-Tenant HttpClientFactory project.

## 📚 Reading Order

### For New Users

1. **Start with [README.md](../README.md)** — Project overview and quick start
2. Then read **[OVERVIEW.md](./OVERVIEW.md)** — 30-second understanding with diagrams

### For Understanding How It Works

1. **[ARCHITECTURE.md](./ARCHITECTURE.md)** — Complete architecture and components
2. **[DATA_FLOW.md](./DATA_FLOW.md)** — Request lifecycle step-by-step

### For Implementation

- **[README.md Quick Start](../README.md#quick-start)** — Setup in 3 steps
- **ARCHITECTURE.md Extension Points** — How to implement custom resolvers, stores, and certificate providers

---

## 📄 Document Guide

### **README.md** (Project Root)

- **Purpose**: Project overview, feature list, quick start guide
- **Who Should Read**: Everyone - start here!
- **Covers**:
  - Problem statement
  - Features and benefits
  - Project structure
  - Quick start (3 simple steps)
  - Configuration format
  - Performance notes
  - Development status

### **OVERVIEW.md** (This Folder)

- **Purpose**: High-level explanation with visual diagrams
- **Who Should Read**: Anyone who wants to understand the concept without deep technical details
- **Covers**:
  - The problem & solution
  - 5-step workflow overview
  - 3-layer architecture
  - 3 extensibility points
  - Performance features
  - Common scenarios
  - Getting started checklist

**Read Time**: ~10 minutes | **Visual Diagrams**: Yes

### **ARCHITECTURE.md** (This Folder)

- **Purpose**: Deep technical dive into all components
- **Who Should Read**: Developers implementing custom resolvers/stores/certificates, and architects
- **Covers**:
  - Complete component diagram
  - 5 data flow phases (resolution, config loading, certificate, handler, client creation)
  - Configuration model details
  - Hot-reload mechanism
  - Extension points (3 main interfaces)
  - All components with lifecycles
  - Error handling scenarios
  - Typical usage flow
  - Performance considerations

**Read Time**: ~25 minutes | **Diagrams**: Yes | **Code Examples**: Multiple

### **DATA_FLOW.md** (This Folder)

- **Purpose**: Detailed walkthrough of every step in a request lifecycle
- **Who Should Read**: Developers debugging issues, or those wanting to understand the exact sequence of operations
- **Covers**:
  - Request entry point
  - Explicit vs. implicit client creation
  - Configuration loading (with cache hits/misses)
  - Certificate loading (5 types)
  - Handler pooling mechanics
  - HttpClient creation phase
  - Request execution phase
  - Data transformations at each step

**Read Time**: ~30 minutes | **Diagrams**: Very detailed | **Step-by-Step**: Yes

---

## 🎯 Quick Navigation by Use Case

### "I want to understand what this does"

→ **[README.md](../README.md)** + **[OVERVIEW.md](./OVERVIEW.md)**

### "I want to integrate this into my project"

→ **[README.md Quick Start](../README.md#quick-start)** + **[ARCHITECTURE.md Extension Points](./ARCHITECTURE.md#extension-points)**

### "I want to implement a custom ITenantStore"

→ **[ARCHITECTURE.md Extension Points](./ARCHITECTURE.md#extension-points)** + **[README.md Configuration Model](../README.md#configuration-model)**

### "I want to implement a custom ITenantResolver"

→ **[ARCHITECTURE.md - Tenant Resolution Flow](./ARCHITECTURE.md#phase-1-tenant-resolution)**

### "I want to implement a custom ICertificateProvider"

→ **[ARCHITECTURE.md - Certificate Loading Flow](./ARCHITECTURE.md#phase-3-certificate-loading)**

### "Something's not working, help me debug"

→ **[DATA_FLOW.md](./DATA_FLOW.md)** (trace through every step) + **[ARCHITECTURE.md Error Handling](./ARCHITECTURE.md#error-handling)**

### "I want to understand performance characteristics"

→ **[ARCHITECTURE.md Performance Considerations](./ARCHITECTURE.md#performance-considerations)** + **[README.md Performance Notes](../README.md#-performance-notes)**

### "I need to set up hot-reload"

→ **[ARCHITECTURE.md Hot-Reload Flow](./ARCHITECTURE.md#hot-reload-flow)**

---

## 🔑 Key Concepts Explained Across Docs

| Concept               | Where to Learn                                                             |
| --------------------- | -------------------------------------------------------------------------- |
| Tenant Resolution     | [ARCHITECTURE.md Phase 1](./ARCHITECTURE.md#phase-1-tenant-resolution)     |
| Configuration Loading | [ARCHITECTURE.md Phase 2](./ARCHITECTURE.md#phase-2-configuration-loading) |
| Certificate Loading   | [ARCHITECTURE.md Phase 3](./ARCHITECTURE.md#phase-3-certificate-loading)   |
| Handler Pooling       | [ARCHITECTURE.md Phase 4](./ARCHITECTURE.md#phase-4-handler-pooling)       |
| HttpClient Creation   | [ARCHITECTURE.md Phase 5](./ARCHITECTURE.md#phase-5-httpclient-creation)   |
| Extension Points      | [ARCHITECTURE.md Extension Points](./ARCHITECTURE.md#extension-points)     |
| Component Lifecycle   | [ARCHITECTURE.md Key Components](./ARCHITECTURE.md#key-components)         |
| Complete Request Flow | [DATA_FLOW.md](./DATA_FLOW.md) (detailed step-by-step)                     |
| Hot-Reload Mechanism  | [ARCHITECTURE.md Hot-Reload Flow](./ARCHITECTURE.md#hot-reload-flow)       |
| Performance Tuning    | [README.md Performance Notes](../README.md#-performance-notes)             |

---

## 📊 Document Size Reference

| Document        | Size   | Read Time | Visual    | Code     |
| --------------- | ------ | --------- | --------- | -------- |
| README.md       | ~6 KB  | 15 min    | Yes       | Yes      |
| OVERVIEW.md     | ~11 KB | 10 min    | Yes       | Limited  |
| ARCHITECTURE.md | ~19 KB | 25 min    | Yes       | Multiple |
| DATA_FLOW.md    | ~28 KB | 30 min    | Extensive | Limited  |

---

## 💡 Tips for Reading

1. **Progressive Complexity**: Start with OVERVIEW.md, then move to ARCHITECTURE.md, then DATA_FLOW.md
2. **Diagrams First**: Each document has visual diagrams. Skim those first to get the big picture
3. **Use Bookmarks**: Open the doc in your editor and use Ctrl+F to search for specific keywords
4. **Cross-Reference**: Links between documents let you jump to related sections
5. **Experiment**: After reading, review the source code files referenced in the docs

---

## 🗂️ Source Code Structure (Mentioned in Docs)

From **[ARCHITECTURE.md](./ARCHITECTURE.md#-project-structure)**:

```
src/MultiTenantHttpClientFactory.Abstractions/     ← Extension point interfaces
  ├── ITenantResolver.cs
  ├── ITenantStore.cs
  ├── ICertificateProvider.cs
  └── Models/                                       ← Configuration models

src/MultiTenantHttpClientFactory/                  ← Implementation
  ├── TenantHttpClientFactory.cs
  ├── TenantConfigurationProvider.cs
  ├── TenantHandlerCache.cs
  ├── TenantResolution/                           ← Built-in resolvers
  ├── Configuration/                              ← Built-in stores
  ├── Certificates/                               ← Built-in providers
  └── DependencyInjection/                        ← DI registration
```

---

## ❓ FAQ

**Q: Where do I start?**
A: Read the README.md first, then OVERVIEW.md

**Q: How long does it take to understand this?**
A: 15-30 minutes for a solid understanding, 1-2 hours for deep expertise

**Q: Do I need to read all documents?**
A: No. Read based on your needs (see "Quick Navigation by Use Case" above)

**Q: Can I skim the documents?**
A: Yes! Start with headings and diagrams, then read details as needed

**Q: What if I find an error in the docs?**
A: Create an issue or submit a PR to improve them

---

## 🔗 External References

- [Microsoft HttpClientFactory Documentation](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests)
- [IChangeToken Documentation](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.primitives.ichangetoken)
- [SocketsHttpHandler Documentation](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.socketshandler)

---

**Last Updated**: 2026-05-16

For the latest information, see [README.md](../README.md) in the project root.
