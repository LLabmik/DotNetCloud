# Move Search into Core (out-of-process extraction) — Implementation Plan

**Status:** Approved — ready for implementation
**Branch:** `task/move-search-into-core`
**Audience:** implementation agent (assumes knowledge of .NET/EF Core/gRPC, not of this repo's Search history)

---

## 1. Purpose

Search is currently implemented as a **process-isolated module** (`dotnetcloud.search`). Search is not a user-facing domain — it is cross-cutting infrastructure that only reads/aggregates other modules' data (same category as auth, audit, and notifications, which already live in core). This plan moves the Search **index + query engine + REST API** into core-owned code, while keeping **content extraction** (PDF/DOCX/XLSX parsing) in an **out-of-process gRPC worker** so third-party parser libraries never load into the core process.

### Locked decisions (do not change)

| #   | Decision                                                                        | Rationale                                                                                         |
| --- | ------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| 1   | Search data folds into `CoreDbContext` (single migration pipeline)              | Search tables already live in the `core` schema; a second migration pipeline caused past failures |
| 2   | Query/index logic goes in a new **`DotNetCloud.Core.Search`** class library     | Mirrors `DotNetCloud.Core.Auth`                                                                   |
| 3   | Extraction runs in an **out-of-process gRPC worker** (`dotnetcloud.extraction`) | Parser libs (PdfPig, NPOI, OpenXml) never load in core                                            |
| 4   | The 7 per-module gRPC document-pull clients **stay**                            | Modules remain separate processes; core must still pull documents over gRPC                       |
| 5   | `api/v1/search*` URLs are unchanged                                             | Zero UI/client changes                                                                            |

### Boundary rule (also write into `CLAUDE.md` and `.github/copilot-instructions.md`)

> A concern is a **core capability**, not a module, when it has no user-owned domain of its own and exists only to read/aggregate other modules' data (search, notifications, audit, auth).

---

## 2. Current-state inventory (read this first)

### Live code paths (today)

1. **Startup full index**: `Core.Server/Services/SearchEventSubscriber.PerformInitialIndexAsync` → 7 `IModuleSearchDocumentClient` gRPC clients → `SearchGrpcApiClient.IndexDocumentAsync` (gRPC) → Search host `SearchGrpcService.IndexDocument` → `ISearchProvider.IndexDocumentAsync` → `SearchDbContext`.
2. **Admin shared-folder reindex**: `Core.Server/Services/InProcessAdminSharedFolderReindexDispatcher` → `ISearchApiClient.ReindexModuleAsync` (gRPC).
3. **Query/suggest/admin**: UI → YARP proxy `api/v1/search` → Search host `SearchController` → `SearchQueryService` → `ISearchProvider` → `SearchDbContext`.

### Dead / vestigial code (safe to delete — not reachable in production)

- `SearchModule`, `SearchModuleManifest`, `SearchLifecycleService`, `InProcessEventBus` (host-local event bus with no publishers).
- Module `SearchIndexRequestEventHandler`, module `SearchIndexingService` (depends on `ISearchableModule`, which is not registered in the host).
- Module `SearchReindexBackgroundService` (iterates `ISearchableModule`, which is empty in the host → no-ops).
- `ContentExtractionService` + 10 `IContentExtractor` implementations: **no callers in `src`** (only unit tests). `SearchIndexingService.EnrichDocumentFromStreamAsync` / `TryEnrichWithContentExtraction` are never invoked.

### Not wired (do not "fix" during this work unless asked)

- `SearchIndexRequestEvent` has **no publishers** anywhere in `src`. Real-time incremental indexing is half-built (only the core-side handler exists). Keep the handler, but do not add publishers.

### What stays in `DotNetCloud.Core` (SDK, unchanged)

- `DTOs/Search/*` — `SearchDocument`, `SearchQuery`, `SearchResultDto`, `SearchResultItem`, `SearchSortOrder`, `ExtractedContent`, `SearchVisibilityMetadata`.
- `Capabilities/ISearchProvider.cs`, `Capabilities/IContentExtractor.cs`.
- `Events/Search/SearchIndexRequestEvent.cs`, `SearchIndexCompletedEvent.cs`.

### What is removed

- `Capabilities/ISearchableModule.cs` (dead after this work).
- `Services/ModuleApis/ISearchApiClient.cs` (replaced by direct `ISearchProvider` calls).

---

## 3. Target project structure

```
src/Core/
  DotNetCloud.Core.Search/                       NEW (class library)
    SearchQueryService.cs
    SearchQueryParser.cs
    ParsedSearchQuery.cs
    SnippetGenerator.cs
    SearchVisibilityFilterBuilder.cs
    SqlServerSearchProvider.cs                    (uses CoreDbContext)
    PostgreSqlSearchProvider.cs                   (uses CoreDbContext)
    SearchServiceRegistration.cs                  (AddCoreSearchServices)
    IExtractionService.cs                         (abstraction over the worker)
  DotNetCloud.Core.Search.Extraction/             NEW (class library — parser NuGets live ONLY here)
    ContentExtractionService.cs
    Extractors/*.cs                               (10 extractors)
  DotNetCloud.Core.Search.Extraction.Contracts/   NEW (proto + generated gRPC types)
    Protos/extraction_service.proto
  DotNetCloud.Core.Search.Extraction.Host/        NEW (worker process, exe)
    Program.cs
    Services/ExtractionGrpcService.cs
    Services/ExtractionLifecycleService.cs
    Services/ExtractionHealthCheck.cs
    manifest.json
src/Core/DotNetCloud.Core.Data/
  Entities/Search/SearchIndexEntry.cs             MOVED (verbatim)
  Entities/Search/IndexingJob.cs                  MOVED (verbatim, includes enums)
  Configuration/Search/SearchIndexEntryConfiguration.cs   MOVED (+ ToTable schema)
  Configuration/Search/IndexingJobConfiguration.cs        MOVED (+ ToTable schema)
src/Core/DotNetCloud.Core.Server/
  Controllers/SearchController.cs                 MOVED
  Controllers/SearchControllerBase.cs             MOVED
  Services/SearchEventSubscriber.cs               REWRITTEN (ISearchProvider)
  Services/ModuleSearchDocumentClients.cs         UNCHANGED (kept)
  Services/SearchReindexHostedService.cs          NEW (replaces module reindex service)
  Services/SearchIndexingService.cs               NEW (channel queue, admin-status parity)
  Grpc/Clients/ExtractionGrpcClient.cs            NEW
```

Deleted projects: `DotNetCloud.Modules.Search`, `.Search.Client`, `.Search.Data`, `.Search.Data.SqlServer`, `.Search.Host`, and test project `DotNetCloud.Modules.Search.Tests`.

---

## 4. Phase 1 — Data layer (foundational)

### 4.1 Move entities

Move verbatim (change only the namespace to `DotNetCloud.Core.Data.Entities.Search`):

- `src/Modules/Search/DotNetCloud.Modules.Search.Data/Models/SearchIndexEntry.cs` → `src/Core/DotNetCloud.Core.Data/Entities/Search/SearchIndexEntry.cs`
- `src/Modules/Search/DotNetCloud.Modules.Search.Data/Models/IndexingJob.cs` → `src/Core/DotNetCloud.Core.Data/Entities/Search/IndexingJob.cs`

`IndexingJob.cs` contains `IndexingJob`, `IndexJobType`, and `IndexJobStatus` — move all three.

### 4.2 Move configurations (add schema mapping)

Move:

- `src/Modules/Search/DotNetCloud.Modules.Search.Data/Configuration/SearchIndexEntryConfiguration.cs` → `src/Core/DotNetCloud.Core.Data/Configuration/Search/SearchIndexEntryConfiguration.cs`
- `src/Modules/Search/DotNetCloud.Modules.Search.Data/Configuration/IndexingJobConfiguration.cs` → `src/Core/DotNetCloud.Core.Data/Configuration/Search/IndexingJobConfiguration.cs`

Change namespace to `DotNetCloud.Core.Data.Configuration.Search` and **add a `ToTable` call with the `core` schema hardcoded** (do NOT use the naming strategy — the search module used PascalCase table/column names on BOTH providers, and production data depends on those exact names):

```csharp
// In SearchIndexEntryConfiguration.Configure(...), after existing property config:
builder.ToTable("SearchIndexEntries", "core");
```

```csharp
// In IndexingJobConfiguration.Configure(...), after existing property config:
builder.ToTable("IndexingJobs", "core");
```

Keep every other line (keys, max lengths, `HasDatabaseName("ix_...")`) exactly as-is. The lowercase `ix_*` index names must be preserved — they exist in production.

### 4.3 Wire into `CoreDbContext`

Edit `src/Core/DotNetCloud.Core.Data/Context/CoreDbContext.cs`:

1. Add usings for `DotNetCloud.Core.Data.Entities.Search` and `DotNetCloud.Core.Data.Configuration.Search`.
2. Add DbSets (property names MUST be exactly `SearchIndexEntries` and `IndexingJobs` to reproduce production table names):

```csharp
/// <summary>The centralized full-text search index entries.</summary>
public DbSet<SearchIndexEntry> SearchIndexEntries => Set<SearchIndexEntry>();

/// <summary>Search reindex job tracking records.</summary>
public DbSet<IndexingJob> IndexingJobs => Set<IndexingJob>();
```

3. In `OnModelCreating`, after `ConfigureAuditModels(modelBuilder);`, add `ConfigureSearchModels(modelBuilder);`.
4. Add the method near the other `ConfigureXxxModels` methods:

```csharp
/// <summary>
/// Configures the full-text search index entities.
/// </summary>
private void ConfigureSearchModels(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfiguration(new SearchIndexEntryConfiguration());
    modelBuilder.ApplyConfiguration(new IndexingJobConfiguration());
}
```

> The `ToTable("...", "core")` added in 4.2 is what places these in the `core` schema. Do NOT use `ApplyTableName<...>` — that helper applies `ITableNamingStrategy.GetColumnName`, which snake_cases table names on PostgreSQL and would break production compatibility.

### 4.4 Migrations (CRITICAL — idempotent)

Production already contains `core.SearchIndexEntries` and `core.IndexingJobs` (created by the old Search module migrations). A normal `dotnet ef migrations add` would emit `CreateTable` that fails on existing tables.

1. Generate the migration for **both** providers. Production is **SQL Server** — the SQL Server migration assembly is `DotNetCloud.Core.Data.SqlServer` (see repo convention). Generate:

```bash
# SQL Server (production)
dotnet ef migrations add AddSearchIndex \
  --project src/Core/DotNetCloud.Core.Data.SqlServer \
  --context 'DotNetCloud.Core.Data.Context.CoreDbContext' \
  --output-dir Migrations

# PostgreSQL (dev/local)
dotnet ef migrations add AddSearchIndex \
  --project src/Core/DotNetCloud.Core.Data \
  --context 'DotNetCloud.Core.Data.Context.CoreDbContext'
```

2. Hand-edit **both** `Up()` methods to guard every `CreateTable` / `CreateIndex` with an existence check, so applying on a database that already has the tables is a no-op. Example shape for SQL Server:

```csharp
migrationBuilder.Sql(@"
IF OBJECT_ID(N'core.SearchIndexEntries', N'U') IS NULL
BEGIN
    -- create table + indexes here (copy the generated CreateTable/CreateIndex calls)
END
");
```

For PostgreSQL use `IF NOT EXISTS (SELECT FROM pg_tables WHERE schemaname = 'core' AND tablename = 'SearchIndexEntries')`.

> **Verification gate:** after generating, confirm the generated table/column/index names exactly match the production reference in section 9. If any name differs, your entity/config mapping is wrong — fix it before touching the migration.

3. The core schema-creation path (`DbContextSchemaProvider.EnsureSchemaAsync`) already tolerates pre-existing tables and records pending migrations as applied — but the idempotent migration above is still required so `MigrateAsync` does not throw.

### 4.5 Delete old data projects

Delete the directories `src/Modules/Search/DotNetCloud.Modules.Search.Data/` and `src/Modules/Search/DotNetCloud.Modules.Search.Data.SqlServer/`.

**Gate:** `dotnet build src/Core/DotNetCloud.Core.Data/DotNetCloud.Core.Data.csproj` and the SQL Server project build cleanly; the two migrations generate with zero diff from the production names in section 9.

---

## 5. Phase 2 — `DotNetCloud.Core.Search` library

### 5.1 Create project

`src/Core/DotNetCloud.Core.Search/DotNetCloud.Core.Search.csproj` (model after `DotNetCloud.Core.Auth`):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>DotNetCloud.Core.Search</RootNamespace>
    <AssemblyName>DotNetCloud.Core.Search</AssemblyName>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../DotNetCloud.Core/DotNetCloud.Core.csproj" />
    <ProjectReference Include="../DotNetCloud.Core.Data/DotNetCloud.Core.Data.csproj" />
  </ItemGroup>
</Project>
```

Package versions come from central `Directory.Packages.props` (already present).

### 5.2 Move files (change namespace to `DotNetCloud.Core.Search.Services` / `.Extractors` as appropriate)

Move from `src/Modules/Search/DotNetCloud.Modules.Search/Services/`:

- `SearchQueryService.cs`
- `SearchQueryParser.cs`
- `ParsedSearchQuery.cs`
- `SnippetGenerator.cs`
- `SearchVisibilityFilterBuilder.cs`
- `SqlServerSearchProvider.cs`
- `PostgreSqlSearchProvider.cs`

Move from `src/Modules/Search/DotNetCloud.Modules.Search/Extractors/` — **do NOT move here**; extractors go to Phase 3. (This is deliberate: keeping parser NuGets out of `Core.Search` keeps them out of the core process.)

### 5.3 Retarget providers to `CoreDbContext`

In `SqlServerSearchProvider.cs` and `PostgreSqlSearchProvider.cs`:

- Remove `using DotNetCloud.Modules.Search.Data;` and `using DotNetCloud.Modules.Search.Data.Models;`.
- Add `using DotNetCloud.Core.Data.Context;` and `using DotNetCloud.Core.Data.Entities.Search;`.
- Change constructor parameter type `SearchDbContext` → `CoreDbContext`, and field type accordingly.
- Keep every query/upsert line unchanged (`_db.SearchIndexEntries`, `_db.IndexingJobs` work because the DbSet names are identical).

### 5.4 `SearchVisibilityFilterBuilder` retarget

Change `using DotNetCloud.Modules.Search.Data.Models;` → `using DotNetCloud.Core.Data.Entities.Search;`. Nothing else changes (it references `SearchIndexEntry`).

### 5.5 `IExtractionService` abstraction

New file `src/Core/DotNetCloud.Core.Search/IExtractionService.cs`:

```csharp
using DotNetCloud.Core.DTOs.Search;

namespace DotNetCloud.Core.Search;

/// <summary>
/// Abstraction over the out-of-process content extraction worker.
/// </summary>
public interface IExtractionService
{
    /// <summary>
    /// Extracts plain text from binary document content.
    /// Returns null if no extractor supports the MIME type or extraction fails.
    /// </summary>
    Task<ExtractedContent?> ExtractAsync(byte[] content, string mimeType, CancellationToken cancellationToken = default);
}
```

### 5.6 DI registration

New file `src/Core/DotNetCloud.Core.Search/SearchServiceRegistration.cs` (replaces the old module `SearchServiceRegistration`). It must:

- Register `ISearchProvider` by database provider (`SqlServerSearchProvider` for SQL Server, `PostgreSqlSearchProvider` otherwise).
- Register `SearchQueryService`, `SearchQueryParser`, `SnippetGenerator`, `SearchVisibilityFilterBuilder`.

Use the same provider-detection logic as the old `SearchServiceRegistration.ResolveDatabaseProvider` (reads `Database:Provider` or `databaseProvider`; SQL Server when it contains "sqlserver"/"sql server", else PostgreSQL). Expose:

```csharp
public static IServiceCollection AddCoreSearchServices(this IServiceCollection services, IConfiguration? configuration = null)
```

**Gate:** `dotnet build src/Core/DotNetCloud.Core.Search/DotNetCloud.Core.Search.csproj`.

---

## 6. Phase 3 — Extraction worker (out-of-process)

### 6.1 `DotNetCloud.Core.Search.Extraction` library

Create `src/Core/DotNetCloud.Core.Search.Extraction/DotNetCloud.Core.Search.Extraction.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>DotNetCloud.Core.Search.Extraction</RootNamespace>
    <AssemblyName>DotNetCloud.Core.Search.Extraction</AssemblyName>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <AcceptNPOIOSMFLicense>true</AcceptNPOIOSMFLicense>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="NPOI" />
    <PackageReference Include="PdfPig" />
    <PackageReference Include="DocumentFormat.OpenXml" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../DotNetCloud.Core/DotNetCloud.Core.csproj" />
  </ItemGroup>
</Project>
```

Move here (namespace `DotNetCloud.Core.Search.Extraction` / `.Extractors`):

- `src/Modules/Search/DotNetCloud.Modules.Search/Services/ContentExtractionService.cs`
- All 10 files from `src/Modules/Search/DotNetCloud.Modules.Search/Extractors/`

The 10 extractors: `PdfContentExtractor`, `DocxContentExtractor`, `XlsxContentExtractor`, `PptxContentExtractor`, `OdfContentExtractor`, `XlsContentExtractor`, `RtfContentExtractor`, `HtmlContentExtractor`, `MarkdownContentExtractor`, `PlainTextExtractor`.

Only `ContentExtractionService` needs a small addition — a byte-array entry point the worker's gRPC service will call:

```csharp
public async Task<ExtractedContent?> ExtractAsync(byte[] content, string mimeType, CancellationToken ct = default)
{
    using var ms = new MemoryStream(content, writable: false);
    return await ExtractAsync(ms, mimeType, ct);
}
```

### 6.2 `DotNetCloud.Core.Search.Extraction.Contracts` (proto + client types)

Create `src/Core/DotNetCloud.Core.Search.Extraction.Contracts/DotNetCloud.Core.Search.Extraction.Contracts.csproj` (model after the old `DotNetCloud.Modules.Search.Client`):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>DotNetCloud.Core.Search.Extraction.Contracts</RootNamespace>
    <AssemblyName>DotNetCloud.Core.Search.Extraction.Contracts</AssemblyName>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Google.Protobuf" />
    <PackageReference Include="Grpc.Net.Client" />
    <PackageReference Include="Grpc.Tools">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <Protobuf Include="..\DotNetCloud.Core.Search.Extraction.Host\Protos\extraction_service.proto"
              GrpcServices="Client"
              Link="Protos\extraction_service.proto" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../DotNetCloud.Core/DotNetCloud.Core.csproj" />
  </ItemGroup>
</Project>
```

### 6.3 Proto

`src/Core/DotNetCloud.Core.Search.Extraction.Host/Protos/extraction_service.proto`:

```proto
syntax = "proto3";

option csharp_namespace = "DotNetCloud.Core.Search.Extraction.Host.Protos";

package dotnetcloud.extraction;

service ExtractionService {
  rpc Extract (ExtractRequest) returns (ExtractResponse);
}

message ExtractRequest {
  bytes content = 1;
  string mime_type = 2;
}

message ExtractResponse {
  bool success = 1;
  string text = 2;
  string error_message = 3;
  map<string, string> metadata = 4;
}
```

### 6.4 Worker host `DotNetCloud.Core.Search.Extraction.Host`

Create `src/Core/DotNetCloud.Core.Search.Extraction.Host/DotNetCloud.Core.Search.Extraction.Host.csproj` (model after `DotNetCloud.Modules.Search.Host`, but WITHOUT auth/DataProtection/controllers):

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>DotNetCloud.Core.Search.Extraction.Host</RootNamespace>
    <AssemblyName>dotnetcloud.extraction</AssemblyName>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <PublishWithPackageReferences>false</PublishWithPackageReferences>
    <PublishReadyToCompile>false</PublishReadyToCompile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Grpc.AspNetCore" />
  </ItemGroup>
  <ItemGroup>
    <Protobuf Include="Protos\extraction_service.proto" GrpcServices="Server" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\DotNetCloud.Core.Search.Extraction\DotNetCloud.Core.Search.Extraction.csproj" />
    <ProjectReference Include="..\DotNetCloud.Core.Search.Extraction.Contracts\DotNetCloud.Core.Search.Extraction.Contracts.csproj" />
    <ProjectReference Include="..\DotNetCloud.Core.Grpc\DotNetCloud.Core.Grpc.csproj" />
  </ItemGroup>
</Project>
```

**`Program.cs`** — minimal (mirror the gRPC-endpoint + lifecycle parts of the old Search host `Program.cs`, but drop config loading, DataProtection, cookie auth, DbContext, controllers, and health check DB wiring):

1. Bind Kestrel to `DOTNETCLOUD_GRPC_ENDPOINT` (set by `ProcessSupervisor`) exactly as the old Search host did.
2. Register `ContentExtractionService` + all 10 extractors as `IContentExtractor` singletons.
3. `builder.Services.AddGrpc();`
4. `app.MapGrpcService<ExtractionGrpcService>(); app.MapGrpcService<ExtractionLifecycleService>();`
5. Keep `public partial class Program;` at the end (WebApplicationFactory pattern).

**`Services/ExtractionGrpcService.cs`** — implements `ExtractionService.ExtractionServiceBase`; calls `ContentExtractionService.ExtractAsync(bytes, mimeType)` and maps `ExtractedContent` → `ExtractResponse`.

**`Services/ExtractionLifecycleService.cs`** — implement `DotNetCloud.Core.Grpc.Lifecycle.ModuleLifecycle.ModuleLifecycleBase` (copy the shape of the old `SearchLifecycleService`, but initialize/start/stop are no-ops returning `Success = true`, and `GetManifest` returns id `dotnetcloud.extraction`, name `Extraction`, version `1.0.0`, empty capabilities/events).

**`Services/ExtractionHealthCheck.cs`** — trivial `IHealthCheck` returning Healthy.

**`manifest.json`**:

```json
{
  "id": "dotnetcloud.extraction",
  "name": "Extraction",
  "version": "1.0.0",
  "description": "Out-of-process document content extraction worker for full-text search indexing.",
  "author": "DotNetCloud",
  "requiredCapabilities": [],
  "publishedEvents": [],
  "subscribedEvents": [],
  "minCoreVersion": "1.0.0"
}
```

> The worker is discovered and launched by `ProcessSupervisor` automatically because it will be published into the `modules/dotnetcloud.extraction/` directory (Phase 5). It needs no `RequiredModules` entry (it has no DbContext/schema).

### 6.5 Core.Server gRPC client

`src/Core/DotNetCloud.Core.Server/Grpc/Clients/ExtractionGrpcClient.cs` — implement `DotNetCloud.Core.Search.IExtractionService`. Resolve the worker endpoint via `ModuleEndpointProvider.GetEndpoint("dotnetcloud.extraction")` (same pattern as the old `SearchGrpcApiClient` used `GetEndpoint("dotnetcloud.search")`). Add a `<Protobuf>` include for `extraction_service.proto` with `GrpcServices="Client"` via a project reference to `...Extraction.Contracts` (the Contracts project already compiles the proto as Client).

**Gate:** worker project + contracts + client build; manual gRPC call to `Extract` on a PDF returns text.

---

## 7. Phase 4 — `DotNetCloud.Core.Server` integration

### 7.1 Move REST controllers

Move `src/Modules/Search/DotNetCloud.Modules.Search.Host/Controllers/SearchController.cs` and `SearchControllerBase.cs` → `src/Core/DotNetCloud.Core.Server/Controllers/`.

- Namespace → `DotNetCloud.Core.Server.Controllers`.
- `SearchControllerBase` depends on `DotNetCloud.Core.Authorization`, `DotNetCloud.Core.Errors` — already referenced by Core.Server.
- `SearchController` keeps its constructor deps (`SearchQueryService`, `SearchDbContext`→`CoreDbContext`, `ILogger`, optional `IGroupDirectory`, `SearchReindexHostedService`, `SearchIndexingService`). Retarget `SearchDbContext` → `CoreDbContext` and `DotNetCloud.Modules.Search.Data.Models` → `DotNetCloud.Core.Data.Entities.Search`.
- Preserve routes exactly: `GET api/v1/search`, `GET api/v1/search/suggest`, `GET api/v1/search/stats`, `POST api/v1/search/admin/reindex`, `POST api/v1/search/admin/reindex/{moduleId}`, `GET api/v1/search/admin/status`.

### 7.2 Rewrite `SearchEventSubscriber` / `SearchIndexEventHandler`

In `src/Core/DotNetCloud.Core.Server/Services/SearchEventSubscriber.cs`:

- Remove dependency on `ISearchApiClient` (`DotNetCloud.Core.Services.ModuleApis`).
- Inject `ISearchProvider` instead.
- `SearchIndexEventHandler.HandleAsync`: on `Remove` → `_searchProvider.RemoveDocumentAsync(...)`; on `Index` → resolve `IModuleSearchDocumentClient` by module id, `GetSearchableDocumentAsync`, and call `_searchProvider.IndexDocumentAsync(...)` (or `RemoveDocumentAsync` when the doc is null). Keep the same null/not-found semantics.
- Keep `IModuleSearchDocumentClient` (7 clients in `ModuleSearchDocumentClients.cs`) completely unchanged.

### 7.3 New `SearchReindexHostedService`

`src/Core/DotNetCloud.Core.Server/Services/SearchReindexHostedService.cs` — a `BackgroundService` replacing the old module `SearchReindexBackgroundService`. It:

- On startup (after a 1-minute delay), runs a full reindex, then every 24h.
- Supports `TriggerFullReindex()` and `TriggerModuleReindex(moduleId)` (called by `SearchController`).
- Pulls documents via `IModuleSearchDocumentClient` (instead of the old `ISearchableModule`) and indexes via `ISearchProvider`.
- Tracks progress in `IndexingJob` rows (via `CoreDbContext`) and exposes `IsReindexing`, `CurrentModuleId`, `ReindexDocumentsProcessed/Total`, `ReindexStartedAt` — same public surface the admin status endpoint reads.

> This unifies the old module reindex service AND the initial-index logic in `SearchEventSubscriber`. Consider removing the duplicate initial-index loop from `SearchEventSubscriber` in favor of the hosted service's startup reindex; if you keep both, they must not both run full reindexes at startup (pick one). Recommended: move initial-index responsibility entirely into `SearchReindexHostedService`.

### 7.4 New slim `SearchIndexingService`

`src/Core/DotNetCloud.Core.Server/Services/SearchIndexingService.cs` — a channel-backed queue (`Channel<SearchIndexRequestEvent>`) for backpressure, to preserve admin-status fields (`pendingQueueCount`, `realtimeProcessed`, `realtimeFailed`). It:

- Uses `IServiceScopeFactory` to resolve `ISearchProvider` and `IExtractionService`.
- For `Index` actions, pulls the document via `IModuleSearchDocumentClient`, then — if `Content` is empty/whitespace and metadata contains `MimeType` and a content byte source is available — calls `IExtractionService.ExtractAsync` to enrich the document.
- For `Remove` actions, calls `ISearchProvider.RemoveDocumentAsync`.

> If a module does not supply raw file bytes today (it does not), the extraction call stays dormant — matching current behavior. The worker capability is still available and tested.

### 7.5 Rewrite `InProcessAdminSharedFolderReindexDispatcher`

In `src/Core/DotNetCloud.Core.Server/Services/InProcessAdminSharedFolderReindexDispatcher.cs`, replace the `ISearchApiClient` dependency with the new reindex service (`SearchReindexHostedService.TriggerModuleReindex("files")` or a direct `SearchQueryService.ReindexModuleAsync("files")` call).

### 7.6 `Program.cs` changes

In `src/Core/DotNetCloud.Core.Server/Program.cs`:

1. **Remove** the `api/v1/search` entry from the `moduleMappings` dictionary in `MapModuleApiProxies` (leave `api/v1/contacts` etc. intact).
2. **Remove** `SearchGrpcClientOptions` configuration and `builder.Services.AddSingleton<...ISearchApiClient, ...SearchGrpcApiClient>()`.
3. **Remove** the commented-out `AddSearchFtsClient` lines if present.
4. **Add** `builder.Services.AddCoreSearchServices(builder.Configuration);`.
5. **Add** `builder.Services.AddSingleton<IExtractionService, ExtractionGrpcClient>();`.
6. **Add** `builder.Services.AddHostedService<SearchReindexHostedService>();` and register `SearchIndexingService` as a singleton (started by the hosted service).
7. **Keep** the 7 `IModuleSearchDocumentClient` singleton registrations (Files, Notes, Calendar, Bookmarks, Email, Music, Video) exactly as-is.

### 7.7 `DbContextSchemaProvider.cs`

Remove `["dotnetcloud.search"] = typeof(SearchDbContext)` from `ModuleDbContextTypes`, and remove the `using DotNetCloud.Modules.Search.Data;`. The search schema is now owned by `CoreDbContext` and handled by the core's own migration path.

**Gate:** `dotnet build src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj`; a manual in-process search query works.

---

## 8. Phase 5 — Cleanup and project/solution updates

### 8.1 Delete projects

Delete these directories:

- `src/Modules/Search/DotNetCloud.Modules.Search/`
- `src/Modules/Search/DotNetCloud.Modules.Search.Client/`
- `src/Modules/Search/DotNetCloud.Modules.Search.Host/`
- `src/Modules/Search/DotNetCloud.Modules.Search.Data/`
- `src/Modules/Search/DotNetCloud.Modules.Search.Data.SqlServer/`
- `src/Modules/Search/manifest.json`

### 8.2 Remove obsolete core types (verify zero references first)

- `src/Core/DotNetCloud.Core/Capabilities/ISearchableModule.cs`
- `src/Core/DotNetCloud.Core/Services/ModuleApis/ISearchApiClient.cs`
- `src/Core/DotNetCloud.Core.Server/Grpc/Clients/SearchGrpcApiClient.cs` (and its `SearchGrpcClientOptions` if defined in the same file)

Run a workspace-wide search for `ISearchableModule`, `ISearchApiClient`, `SearchGrpcApiClient`, `DotNetCloud.Modules.Search` before deleting; fix or remove every hit.

### 8.3 Solution files

- `DotNetCloud.sln`: remove the 6 Search projects (Search, Search.Data, Search.Host, Search.Tests, Search.Client, Search.Data.SqlServer); add `DotNetCloud.Core.Search`, `DotNetCloud.Core.Search.Extraction`, `DotNetCloud.Core.Search.Extraction.Contracts`, `DotNetCloud.Core.Search.Extraction.Host`.
- `DotNetCloud.CI.slnf`: remove the 5 Search module projects (note: `DotNetCloud.Modules.Search.Tests` is NOT in the slnf — do not add it); add the 4 new projects plus the new test project(s) from Phase 6.

Use `dotnet sln` to add/remove, or edit the files directly (they are plain text).

### 8.4 Project references

- `src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj`: remove references to `DotNetCloud.Modules.Search.Data` / `.Data.SqlServer` / `.Search.Client`; add `DotNetCloud.Core.Search` and `DotNetCloud.Core.Search.Extraction.Contracts`.
- `src/CLI/DotNetCloud.CLI/DotNetCloud.CLI.csproj`: remove the `DotNetCloud.Modules.Search.Data.SqlServer` reference.
- `src/CLI/DotNetCloud.CLI/Infrastructure/ServiceProviderFactory.cs`: remove `using DotNetCloud.Modules.Search.Data;`, the `SearchMigrationsAssembly` const, and the `services.AddDbContext<SearchDbContext>(...)` block.

### 8.5 Supervisor and module registry

- `src/Core/DotNetCloud.Core.Server/Supervisor/ProcessSupervisor.cs`: remove `ResolveSearchModuleEndpoint()` and any other `dotnetcloud.search` special-casing. No special-casing needed for `dotnetcloud.extraction` (generic discovery handles it).
- `src/Core/DotNetCloud.Core/Modules/RequiredModules.cs`: remove `"dotnetcloud.search"` from `ModuleIds`. Verify nothing else calls `IsRequired("search")` / `GetSchemaName("search")`.

### 8.6 Deploy and scripts

- `scripts/deploy.sh`: in the `MODULES=(...)` list, replace `Search` with `Extraction` (or add the extraction host to the module publish list per how other module hosts are deployed).
- `scripts/publish-module-hosts.ps1`: replace the `dotnetcloud.search` entry with `dotnetcloud.extraction` → `src/Core/DotNetCloud.Core.Search.Extraction.Host/DotNetCloud.Core.Search.Extraction.Host.csproj`.
- `scripts/soc2-compliance-scan.ps1` and `scripts/soc2-compliance-scan.sh`: update the module id lists (`$ModuleIds` / the `files chat search contacts...` list) — remove `search`, and if the scan enumerates running processes, account for `dotnetcloud.extraction` as an internal worker (or exclude it if the scan is about user-facing modules).
- Deploy cleanup: after the new deploy, remove the old `modules/dotnetcloud.search/` directory on the server (add a cleanup step to the deploy script or do it once manually).

### 8.7 Boundary rule documentation

Add the boundary rule from section 1 to both `CLAUDE.md` and `.github/copilot-instructions.md`.

**Gate:** full `dotnet build DotNetCloud.CI.slnf -c Release`; grep for `DotNetCloud.Modules.Search`, `ISearchApiClient`, `ISearchableModule`, `SearchGrpcApiClient`, `dotnetcloud.search` returns zero hits (except intentional docs).

---

## 9. Production schema reference (for migration correctness)

The existing tables (which the new idempotent migration must reproduce exactly, and which must be preserved):

- Schema: **`core`** (both providers)
- Table **`SearchIndexEntries`** (PascalCase on both providers), columns: `Id` (bigint identity), `ModuleId` (nvarchar(50)/varchar(50), required), `EntityId` (nvarchar(64)/varchar(64), required), `EntityType` (nvarchar(100)/varchar(100), required), `Title` (nvarchar(500)/varchar(500), required), `Content` (nvarchar(max)/varchar(102400), required), `Summary` (nvarchar(1000)/varchar(1000), nullable), `OwnerId` (uniqueidentifier/uuid, required), `OrganizationId` (uniqueidentifier/uuid, nullable), `CreatedAt` (datetimeoffset/timestamptz, required), `UpdatedAt` (required), `IndexedAt` (required), `MetadataJson` (nvarchar(4000)/varchar(4000), nullable).
- PK: `PK_SearchIndexEntries` (`Id`). Unique index `ix_search_index_module_entity` (`ModuleId`,`EntityId`). Indexes `ix_search_index_owner_id`, `ix_search_index_organization_id`, `ix_search_index_module_id`, `ix_search_index_entity_type`, `ix_search_index_updated_at`.
- Table **`IndexingJobs`** (PascalCase), columns: `Id` (Guid), `ModuleId` (nvarchar(50), nullable), `Type` (nvarchar(20), required — enum-as-string), `Status` (nvarchar(20), required), `StartedAt`, `CompletedAt` (datetimeoffset, nullable), `DocumentsProcessed` (int), `DocumentsTotal` (int), `ErrorMessage` (nvarchar(2000), nullable).
- PK: `PK_IndexingJobs` (`Id`). Indexes `ix_indexing_jobs_status`, `ix_indexing_jobs_module_id`.

> Note the search module historically used **PascalCase** column/table names on PostgreSQL too (unusual for this repo) — that is exactly why the configs in Phase 4.2 hardcode `ToTable("...", "core")` and why you must not route these through the snake_case naming strategy.

### Timestamp interceptor check

`CoreDbContext.OnConfiguring` adds a `TimestampInterceptor` that auto-manages `CreatedAt`/`UpdatedAt`. `SearchIndexEntry.CreatedAt`/`UpdatedAt` hold **source-entity timestamps**, not row timestamps, and the providers set them explicitly. **Verify** the interceptor does not overwrite or block these values; if it does, exclude the Search entities from the interceptor (or ensure providers always set non-default values so the interceptor leaves them alone).

---

## 10. Phase 6 — Tests

### 10.1 New test projects

- `tests/DotNetCloud.Core.Search.Tests/` — move and adapt:
  - `SearchQueryParserTests`, `ParsedSearchQueryTests`, `SnippetGeneratorTests`, `SearchQueryServiceTests` (retarget namespaces).
  - `SqlServerSearchProviderTests` → retarget to `CoreDbContext` (use the existing `Core.Data.Tests` DbContext test-factory pattern; see repo memory `test-factory-db-provider-fix.md`).
  - `ContentExtractionServiceTests` + all extractor tests (`PdfContentExtractorTests`, `DocxContentExtractorTests`, `XlsxContentExtractorTests`, `PptxContentExtractorTests`, `OdfContentExtractorTests`, `XlsContentExtractorTests`, `RtfContentExtractorTests`, `HtmlContentExtractorTests`, `MarkdownContentExtractorTests`, `PlainTextExtractorTests`) → these test the extraction library; either keep them in `Core.Search.Tests` (add a reference to `...Extraction`) or a separate `tests/DotNetCloud.Core.Search.Extraction.Tests/`.
  - `SearchDbContextTests` → adapt to `CoreDbContext`.
- Delete tests for removed dead code: `SearchModuleTests`, `SearchModuleManifestTests`, `SearchIndexRequestEventHandlerTests`, `SearchIndexingServiceTests` (module version), `SearchGrpcServiceTests`, `Phase6/*` (SearchFtsClient*, SearchClientServiceExtensions, SearchGrpcService, SearchControllerTests→move/adapt to Core.Server), `Phase4/*`, `Phase5/_`, `Phase8/_` where they reference removed types.
- Delete the `tests/DotNetCloud.Modules.Search.Tests/` project entirely once tests are relocated.

### 10.2 Update solution

Add the new test project(s) to `DotNetCloud.sln` (and `DotNetCloud.CI.slnf` if you want them in CI). Remove `DotNetCloud.Modules.Search.Tests` from `DotNetCloud.sln`.

### 10.3 Reference-update sweep

Search all of `tests/` for `using DotNetCloud.Modules.Search` and retarget each to `DotNetCloud.Core.Search` / `.Core.Data.Entities.Search` / `.Core.Search.Extraction` as appropriate.

**Gate:** `dotnet test` on the new/affected test projects passes (or only pre-existing, unrelated failures remain).

---

## 11. Phase 7 — Build, verify, deploy

Per repo conventions (`/memories/repo/` and user memory):

1. **Build** (use the CI solution filter to avoid the Android SDK): `dotnet build DotNetCloud.CI.slnf -c Release`. Wait for completion — never kill a build.
2. **Test**: `dotnet test` on the affected test projects.
3. **Deploy**: `sudo ./scripts/deploy.sh` (the deploy script handles publish, module DLLs, certs, service restart, hash verification). After restart, wait 1–2 minutes for modules to register before health-checking.
4. **Verify deployed DLLs** match build output (md5sum) per deploy memory.
5. **DB check**: confirm `core.SearchIndexEntries` / `core.IndexingJobs` still exist with their data; confirm the new core migration was recorded as applied in `__EFMigrationsHistory` (CoreDbContext's history table).
6. **Manual end-to-end**:
   - `GET /api/v1/search?q=...` returns results.
   - `GET /api/v1/search/suggest?q=...` returns suggestions.
   - `GET /api/v1/search/admin/status` returns stats + reindex progress.
   - `POST /api/v1/search/admin/reindex` and `/reindex/{module}` queue reindexes.
   - Admin shared-folder reindex still works.
   - Extraction: trigger extraction of a PDF/DOCX and confirm text is returned; confirm a corrupt file does not crash the core process (the worker absorbs it).
   - `ProcessSupervisor` health shows `dotnetcloud.extraction` running.

---

## 12. Acceptance criteria

- [ ] `dotnet build DotNetCloud.CI.slnf -c Release` succeeds.
- [ ] Affected test projects pass.
- [ ] Zero remaining references to `DotNetCloud.Modules.Search`, `ISearchApiClient`, `ISearchableModule`, `SearchGrpcApiClient`, `dotnetcloud.search` (except docs).
- [ ] `SearchIndexEntry` / `IndexingJob` are in `CoreDbContext`; migration is idempotent; production tables and data are preserved.
- [ ] `api/v1/search*` endpoints work unchanged (no UI/client changes required).
- [ ] Extraction runs out-of-process via `dotnetcloud.extraction`; parser libraries are not referenced by `DotNetCloud.Core.Server` or `DotNetCloud.Core.Search`.
- [ ] Docs updated: `docs/IMPLEMENTATION_CHECKLIST.md` and `docs/MASTER_PROJECT_PLAN.md` (targeted edits, `✓`/`☐` checkbox format), `docs/architecture/ARCHITECTURE.md`, boundary rule in `CLAUDE.md` + instructions file.

---

## 13. Out of scope / follow-ups (do NOT do in this task)

- Wiring real-time incremental indexing publishers (no module publishes `SearchIndexRequestEvent` today).
- Changing the pull-based document retrieval model (push is a separate design).
- Adding per-module file-stream extraction into the live index flow (modules provide `Content` directly today).
- Removing the per-module `GetSearchableDocument(s)` gRPC RPCs.

---

## 14. Documentation tracking (required by repo rules)

After implementation, update with targeted edits:

1. `docs/IMPLEMENTATION_CHECKLIST.md` — mark search-related tasks `✓`/`☐`.
2. `docs/MASTER_PROJECT_PLAN.md` — update Quick Status Summary table and the relevant step's Status/Deliverables/Notes.
3. `docs/architecture/ARCHITECTURE.md` — search is now a core capability; extraction is an out-of-process worker.
4. `CLAUDE.md` + `.github/copilot-instructions.md` — add the boundary rule.
