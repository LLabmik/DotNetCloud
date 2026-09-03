# Search Realtime Indexing Bridge — Implementation Plan

> **Branch:** `fix/search-notes`
> **Status:** 🟢 Implemented + real-time indexing **live-verified working** for Notes (2026-09-03).
> Two post-implementation fixes were required before real-time writes persisted (see
> Implementation Notes): (1) attach the `module-id` gRPC metadata header, (2) add
> `.AsTracking()` to the real-time upsert (global `NoTracking` made updates a silent no-op).
> Remaining: real-time E2E for files/bookmarks/calendar/music/video, and note that file-body
> search (words inside `.docx`/`.odt`) is a separate pre-existing content-extraction gap.
> **Goal:** Make global search update in near-real-time when searchable content changes (create / update / delete) across all searchable modules, instead of relying solely on the scheduled full reindex.

## 1. Background & Root Cause

Global search is a **core capability**. Core.Server owns the search index (`SearchIndexEntry` in `CoreDbContext`) and queries it directly. Indexing is pull-based:

1. **Full reindex** (startup + every 24h + admin trigger) — `SearchReindexHostedService` pulls every document from each module over gRPC via `IModuleSearchDocumentClient.GetAllSearchableDocumentsAsync()`.
2. **Incremental (intended, currently unwired)** — modules publish `SearchIndexRequestEvent` on CRUD; Core.Server should enqueue it into `SearchIndexingService` (a bounded `Channel<T>` queue).

**Why incremental indexing doesn't work today:**

- Module hosts run as **separate processes**. Each has its own `IEventBus` registered as `InProcessEventBus` (module-local).
- `NoteService` (and other module services) publish `SearchIndexRequestEvent` on that **local** bus. There is no subscriber on the module host and no cross-process transport, so the event is silently dropped — it never reaches Core.Server's `SearchEventSubscriber` / `SearchIndexingService`.
- Calendar already solved this class of problem for SignalR: `CalendarEventBroadcastSubscriber` subscribes the local `IEventBus` and forwards to Core.Server via `CoreCapabilities.CoreCapabilitiesClient` (gRPC over `DOTNETCLOUD_CORE_ENDPOINT`). This plan generalizes that pattern for search indexing.

**Secondary bug (Notes only):** `NotesGrpcService.GetSearchableDocuments` / `GetSearchableDocument` route through owner-scoped `INoteService` methods with a system caller (`UserId = Guid.Empty`), so they return nothing. This breaks the pull path (full reindex + incremental doc fetch) for Notes specifically. Fixed in Part A.

### Searchable modules (authoritative)

Core.Server registers exactly **7** `IModuleSearchDocumentClient` implementations
(`src/Core/DotNetCloud.Core.Server/Program.cs`):

| #   | ModuleId    | Host project (add the bridge line here)                               |
| --- | ----------- | --------------------------------------------------------------------- |
| 1   | `files`     | `src/Modules/Files/DotNetCloud.Modules.Files.Host/Program.cs`         |
| 2   | `notes`     | `src/Modules/Notes/DotNetCloud.Modules.Notes.Host/Program.cs`         |
| 3   | `calendar`  | `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/Program.cs`   |
| 4   | `bookmarks` | `src/Modules/Bookmarks/DotNetCloud.Modules.Bookmarks.Host/Program.cs` |
| 5   | `email`     | `src/Modules/Email/DotNetCloud.Modules.Email.Host/Program.cs`         |
| 6   | `music`     | `src/Modules/Music/DotNetCloud.Modules.Music.Host/Program.cs`         |
| 7   | `video`     | `src/Modules/Video/DotNetCloud.Modules.Video.Host/Program.cs`         |

Chat / Contacts / Photos / Tracks are **excluded** — they are no longer pulled by the
reindexer and have no registered document client.

### Publisher status

Modules whose CRUD/sync services already publish `SearchIndexRequestEvent` on their local
`IEventBus` (verified by existing tests):

| Module    | Publishes `SearchIndexRequestEvent`? | Evidence                                                                       |
| --------- | ------------------------------------ | ------------------------------------------------------------------------------ |
| Files     | ✓                                    | `FileService` imports `DotNetCloud.Core.Events.Search`                         |
| Notes     | ✓                                    | `NoteService` create/update/delete + `NoteServiceSearchIndexTests`             |
| Calendar  | ✓                                    | `CalendarEventServiceSearchIndexTests`                                         |
| Bookmarks | ✓                                    | `BookmarkServiceTests`                                                         |
| Music     | ✓                                    | `TrackServiceSearchIndexTests`                                                 |
| Video     | ✓                                    | `VideoServiceSearchIndexTests`                                                 |
| Email     | ✓ (verified during implementation)   | `GmailEmailProvider` + `ImapSmtpEmailProvider` publish during sync (Phase 6.8) |

> **Action:** ✅ resolved during implementation — confirmed both `ImapSmtpEmailProvider` and
> `GmailEmailProvider` publish `SearchIndexRequestEvent` on thread create/update during sync,
> so real-time email indexing fires once the bridge is deployed.

---

## 2. Architecture

```
Module CRUD (e.g. NoteService.CreateNoteAsync)
        │
        ▼ publishes SearchIndexRequestEvent on local IEventBus
Module Host InProcessEventBus
        │
        ▼ SearchIndexEventBridgeSubscriber (IHostedService, subscribed at host startup)
SearchIndexEventBridgeHandler
        │
        ▼ gRPC: CoreCapabilities.SubmitSearchIndexRequest  (DOTNETCLOUD_CORE_ENDPOINT)
Core.Server CoreCapabilitiesServiceImpl
        │
        ▼ SearchIndexingService.EnqueueAsync (bounded Channel, capacity 1000)
SearchIndexingService.ProcessRequestAsync
        │  (Index action) pulls fresh document back over gRPC:
        ▼ IModuleSearchDocumentClient.GetSearchableDocumentAsync(entityId)
Core.Server ISearchProvider.IndexDocumentAsync  →  SearchIndexEntries table
```

For `SearchIndexAction.Remove`, `ProcessRequestAsync` skips the document pull and removes
`(ModuleId, EntityId)` directly.

---

## 3. Part A — Fix Notes gRPC search document pull (prerequisite)

**File:** `src/Modules/Notes/DotNetCloud.Modules.Notes.Host/Services/NotesGrpcService.cs`

**Why:** without this, the Notes bridge (and full reindex) still fetch zero documents.

### 3.1 Add usings

At the top of the file, after the existing usings, add:

```csharp
using DotNetCloud.Modules.Notes.Data;
using Microsoft.EntityFrameworkCore;
```

### 3.2 Inject `NotesDbContext`

Add a field next to the other fields:

```csharp
private readonly NotesDbContext _db;
```

Change the constructor to accept and assign `NotesDbContext db`:

```csharp
public NotesGrpcService(
    INoteService noteService,
    INoteFolderService folderService,
    INoteShareService shareService,
    IMarkdownRenderer markdownRenderer,
    NotesDbContext db,
    ILogger<NotesGrpcService> logger)
{
    _noteService = noteService;
    _folderService = folderService;
    _shareService = shareService;
    _markdownRenderer = markdownRenderer;
    _db = db;
    _logger = logger;
}
```

### 3.3 Replace `GetSearchableDocuments`

Replace the existing method body with a direct DbContext query (Chat pattern):

```csharp
/// <inheritdoc />
public override async Task GetSearchableDocuments(
    GetSearchableDocumentsRequest request,
    IServerStreamWriter<SearchableDocument> responseStream,
    ServerCallContext context)
{
    var notes = await _db.Notes
        .AsNoTracking()
        .Include(n => n.Tags)
        .Where(n => !n.IsDeleted)
        .OrderBy(n => n.Id)
        .ToListAsync(context.CancellationToken);

    foreach (var note in notes)
    {
        await responseStream.WriteAsync(
            MapNoteToSearchableDocument(note), context.CancellationToken);
    }
}
```

### 3.4 Replace `GetSearchableDocument`

```csharp
/// <inheritdoc />
public override async Task<SearchableDocumentResponse> GetSearchableDocument(
    GetSearchableDocumentRequest request, ServerCallContext context)
{
    if (!Guid.TryParse(request.EntityId, out var entityId))
        return new SearchableDocumentResponse { Found = false };

    var note = await _db.Notes
        .AsNoTracking()
        .Include(n => n.Tags)
        .FirstOrDefaultAsync(n => n.Id == entityId && !n.IsDeleted,
            context.CancellationToken);

    return note is null
        ? new SearchableDocumentResponse { Found = false }
        : new SearchableDocumentResponse { Found = true, Document = MapNoteToSearchableDocument(note) };
}
```

### 3.5 Replace `MapNoteToSearchableDocument` (accept `Note` instead of `NoteDto`)

```csharp
private static SearchableDocument MapNoteToSearchableDocument(Note note)
{
    var doc = new SearchableDocument
    {
        ModuleId = "notes",
        EntityId = note.Id.ToString(),
        EntityType = "Note",
        Title = note.Title,
        Content = note.Content,
        Summary = note.Content.Length > 200
            ? note.Content[..200] + "..."
            : note.Content,
        OwnerId = note.OwnerId.ToString(),
        CreatedAt = note.CreatedAt.ToString("O"),
        UpdatedAt = note.UpdatedAt.ToString("O")
    };

    doc.Metadata["Format"] = note.Format.ToString();
    doc.Metadata["FolderId"] = note.FolderId?.ToString() ?? string.Empty;
    if (note.Tags.Count > 0)
        doc.Metadata["Tags"] = string.Join(",", note.Tags.Select(t => t.Tag));

    return doc;
}
```

> `Note` (entity, `DotNetCloud.Modules.Notes.Models`) has `Id`, `OwnerId`, `FolderId`,
> `Title`, `Content`, `Format` (`NoteContentFormat`), `IsDeleted`, `CreatedAt`, `UpdatedAt`,
> and `ICollection<NoteTag> Tags` (`NoteTag.Tag` is the string). All fields above exist.

---

## 4. Part B — Core-side gRPC RPC

### 4.1 Proto

**File:** `src/Core/DotNetCloud.Core.Grpc/Protos/module_capabilities.proto`

Inside the `service CoreCapabilities { ... }` block, add (e.g. after `rpc PublishEvent ...;`):

```proto
  // Submits a search-index request from a process-isolated module (real-time indexing).
  rpc SubmitSearchIndexRequest (SubmitSearchIndexRequest) returns (SubmitSearchIndexResponse);
```

Append at the end of the file (after `BroadcastRealtimeEventResponse`):

```proto
// --- Search Indexing (real-time bridge) ---

message SubmitSearchIndexRequest {
  // Owning module ID (e.g. "notes", "files").
  string module_id = 1;
  // Entity ID that changed (GUID string).
  string entity_id = 2;
  // DotNetCloud.Core.Events.Search.SearchIndexAction: 0 = Index, 1 = Remove.
  int32 action = 3;
}

message SubmitSearchIndexResponse {
  bool success = 1;
}
```

> gRPC codegen is automatic at build time via `Grpc.Tools` — no manual code generation needed.

### 4.2 Implement the RPC

**File:** `src/Core/DotNetCloud.Core.Server/Grpc/Services/GrpcHealthServiceImpl.cs`
(class `CoreCapabilitiesServiceImpl`)

Add the using:

```csharp
using DotNetCloud.Core.Events.Search;
```

Add a field and constructor parameter for the indexing service:

```csharp
private readonly SearchIndexingService _indexingService;
```

```csharp
public CoreCapabilitiesServiceImpl(
    ILogger<CoreCapabilitiesServiceImpl> logger,
    IServiceProvider serviceProvider,
    SearchIndexingService indexingService)
{
    _logger = logger;
    _serviceProvider = serviceProvider;
    _indexingService = indexingService;
}
```

Add the RPC method (e.g. right after `PublishEvent`):

```csharp
/// <summary>
/// Enqueues a real-time search-index request from a process-isolated module.
/// </summary>
public override async Task<SubmitSearchIndexResponse> SubmitSearchIndexRequest(
    SubmitSearchIndexRequest request, ServerCallContext context)
{
    if (request.Action is < 0 or > 1)
    {
        _logger.LogWarning(
            "SubmitSearchIndexRequest: invalid action {Action} for {ModuleId}/{EntityId} from module {Caller}",
            request.Action, request.ModuleId, request.EntityId, GetModuleId(context));
        return new SubmitSearchIndexResponse { Success = false };
    }

    await _indexingService.EnqueueAsync(new SearchIndexRequestEvent
    {
        EventId = Guid.CreateVersion7(),
        CreatedAt = DateTime.UtcNow,
        ModuleId = request.ModuleId,
        EntityId = request.EntityId,
        Action = (SearchIndexAction)request.Action
    });

    return new SubmitSearchIndexResponse { Success = true };
}
```

Notes:

- `SearchIndexingService` is already registered as a singleton in Core.Server
  (`Program.cs`: `builder.Services.AddSingleton<SearchIndexingService>();`), so constructor
  injection resolves it. `CoreCapabilitiesServiceImpl` is also a singleton.
- `SearchIndexingService` is in namespace `DotNetCloud.Core.Server.Services` — the file
  already has `using DotNetCloud.Core.Server.Services;`.
- `SearchIndexRequestEvent` and `SearchIndexAction` are in
  `DotNetCloud.Core.Events.Search` (enum order: `Index = 0`, `Remove = 1`).

---

## 5. Part C — Shared bridge library (one-time)

All new files go in **`src/Core/DotNetCloud.Core.Grpc/`** (namespace `DotNetCloud.Core.Grpc`).
This project is already referenced by every module host and already hosts the
`CoreCapabilities` proto + `AddAuditLogger()` extension.

### 5.1 Add package reference

**File:** `src/Core/DotNetCloud.Core.Grpc/DotNetCloud.Core.Grpc.csproj`

Add inside the existing `<ItemGroup>` with the other `PackageReference`s:

```xml
<PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" />
```

> Version is centrally managed — `Microsoft.Extensions.Hosting.Abstractions` (10.0.10) is
> already declared in `Directory.Packages.props`. No version attribute needed.

### 5.2 `SearchIndexEventBridgeHandler.cs` (NEW)

```csharp
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Core.Grpc.Capabilities;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Grpc;

/// <summary>
/// Forwards a <see cref="SearchIndexRequestEvent"/> to Core.Server so the search index
/// updates in near-real-time when a module's searchable content changes.
/// </summary>
internal sealed class SearchIndexEventBridgeHandler : IEventHandler<SearchIndexRequestEvent>
{
    private readonly CoreCapabilities.CoreCapabilitiesClient _coreClient;
    private readonly ILogger<SearchIndexEventBridgeHandler> _logger;

    /// <summary>Initializes a new instance of the <see cref="SearchIndexEventBridgeHandler"/> class.</summary>
    public SearchIndexEventBridgeHandler(
        CoreCapabilities.CoreCapabilitiesClient coreClient,
        ILogger<SearchIndexEventBridgeHandler> logger)
    {
        _coreClient = coreClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(SearchIndexRequestEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _coreClient.SubmitSearchIndexRequestAsync(
                new SubmitSearchIndexRequest
                {
                    ModuleId = @event.ModuleId,
                    EntityId = @event.EntityId,
                    Action = (int)@event.Action
                },
                cancellationToken: cancellationToken);

            if (!response.Success)
            {
                _logger.LogWarning(
                    "Core.Server rejected search index request for {ModuleId}/{EntityId} action {Action}",
                    @event.ModuleId, @event.EntityId, @event.Action);
            }
        }
        catch (Exception ex)
        {
            // Real-time indexing must never break module CRUD operations.
            _logger.LogWarning(ex,
                "Failed to submit search index request for {ModuleId}/{EntityId} action {Action}",
                @event.ModuleId, @event.EntityId, @event.Action);
        }
    }
}
```

### 5.3 `SearchIndexEventBridgeSubscriber.cs` (NEW)

```csharp
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Core.Grpc.Capabilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Grpc;

/// <summary>
/// Subscribes the module host's local <see cref="IEventBus"/> to
/// <see cref="SearchIndexRequestEvent"/> and forwards each event to Core.Server over gRPC.
/// </summary>
internal sealed class SearchIndexEventBridgeSubscriber : IHostedService
{
    private readonly IEventBus _eventBus;
    private readonly SearchIndexEventBridgeHandler _handler;

    /// <summary>Initializes a new instance of the <see cref="SearchIndexEventBridgeSubscriber"/> class.</summary>
    public SearchIndexEventBridgeSubscriber(
        IEventBus eventBus,
        CoreCapabilities.CoreCapabilitiesClient coreClient,
        ILogger<SearchIndexEventBridgeHandler> logger)
    {
        _eventBus = eventBus;
        _handler = new SearchIndexEventBridgeHandler(coreClient, logger);
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _eventBus.SubscribeAsync<SearchIndexRequestEvent>(_handler, cancellationToken);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _eventBus.UnsubscribeAsync<SearchIndexRequestEvent>(_handler, cancellationToken);
    }
}
```

### 5.4 `SearchIndexBridgeServiceCollectionExtensions.cs` (NEW)

```csharp
using DotNetCloud.Core.Grpc.Capabilities;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotNetCloud.Core.Grpc;

/// <summary>
/// DI registration for the real-time search indexing bridge used by process-isolated
/// module hosts. Mirrors <see cref="AuditLoggerServiceCollectionExtensions"/>.
/// </summary>
public static class SearchIndexBridgeServiceCollectionExtensions
{
    /// <summary>
    /// Registers a <see cref="CoreCapabilities.CoreCapabilitiesClient"/> and a hosted-service
    /// subscriber that forwards <see cref="DotNetCloud.Core.Events.Search.SearchIndexRequestEvent"/>
    /// to Core.Server. No-op when <c>DOTNETCLOUD_CORE_ENDPOINT</c> is absent (standalone/test host).
    /// </summary>
    public static IServiceCollection AddSearchIndexBridge(this IServiceCollection services)
    {
        var coreEndpoint = Environment.GetEnvironmentVariable("DOTNETCLOUD_CORE_ENDPOINT");
        if (string.IsNullOrWhiteSpace(coreEndpoint))
            return services;

        // TryAddSingleton so hosts that already register a CoreCapabilitiesClient
        // (e.g. Calendar) don't end up with two clients.
        services.TryAddSingleton(_ =>
        {
            var channel = GrpcChannel.ForAddress(coreEndpoint);
            return new CoreCapabilities.CoreCapabilitiesClient(channel);
        });

        services.AddHostedService<SearchIndexEventBridgeSubscriber>();
        return services;
    }
}
```

---

## 6. Part D — Register the bridge in all 7 module hosts

For each file below, add **one line** immediately after the existing
`builder.Services.AddAuditLogger();` call:

```csharp
builder.Services.AddSearchIndexBridge();
```

Files to edit:

| #   | File                                                                  |
| --- | --------------------------------------------------------------------- |
| 1   | `src/Modules/Files/DotNetCloud.Modules.Files.Host/Program.cs`         |
| 2   | `src/Modules/Notes/DotNetCloud.Modules.Notes.Host/Program.cs`         |
| 3   | `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/Program.cs`   |
| 4   | `src/Modules/Bookmarks/DotNetCloud.Modules.Bookmarks.Host/Program.cs` |
| 5   | `src/Modules/Email/DotNetCloud.Modules.Email.Host/Program.cs`         |
| 6   | `src/Modules/Music/DotNetCloud.Modules.Music.Host/Program.cs`         |
| 7   | `src/Modules/Video/DotNetCloud.Modules.Video.Host/Program.cs`         |

> All 15 module hosts already call `builder.Services.AddAuditLogger();`
> (see IMPLEMENTATION_CHECKLIST line ~6459), and that call is inside namespace
> `DotNetCloud.Core.Grpc`, so the new `AddSearchIndexBridge()` extension is available
> without extra usings (the hosts already have `using DotNetCloud.Core.Grpc;`).

### 6.1 Calendar special case

Calendar's `Program.cs` already registers a `CoreCapabilities.CoreCapabilitiesClient` inside:

```csharp
var coreEndpoint = Environment.GetEnvironmentVariable("DOTNETCLOUD_CORE_ENDPOINT");
if (!string.IsNullOrEmpty(coreEndpoint))
{
    _ = builder.Services.AddSingleton(_ =>
    {
        var channel = GrpcChannel.ForAddress(coreEndpoint);
        return new CoreCapabilities.CoreCapabilitiesClient(channel);
    });
    builder.Services.AddHostedService<CalendarReminderEventSubscriber>();
    builder.Services.AddHostedService<CalendarEventBroadcastSubscriber>();
}
```

Because `AddSearchIndexBridge()` uses `TryAddSingleton`, if Calendar's existing
`AddSingleton` runs **first**, the bridge's `TryAddSingleton` is a no-op (one client, fine).
To be order-independent, change Calendar's existing `AddSingleton` to `TryAddSingleton`:

```csharp
    _ = builder.Services.TryAddSingleton(_ =>   // changed: AddSingleton → TryAddSingleton
    {
        var channel = GrpcChannel.ForAddress(coreEndpoint);
        return new CoreCapabilities.CoreCapabilitiesClient(channel);
    });
```

(`System` / `Microsoft.Extensions.DependencyInjection.Extensions` is already imported in
Calendar's Program.cs; if not, add `using Microsoft.Extensions.DependencyInjection.Extensions;`.)

### 6.2 Files special case (no action needed)

Files.Data has its own **different** `CoreCapabilitiesClient : ICoreCapabilitiesClient`
(used for `CleanupAdminSharedFolder`). That is a distinct type/interface from the generated
`CoreCapabilities.CoreCapabilitiesClient` registered by the bridge — no conflict.

---

## 7. Build, test, and verification order

1. ✓ Implement Part A (Notes gRPC pull fix).
2. ✓ Implement Part B (proto + `CoreCapabilitiesServiceImpl` RPC).
3. ✓ Implement Part C (3 new files + csproj package ref).
4. ✓ Implement Part D (7 hosts + Calendar `TryAddSingleton` tweak).
5. ✓ Build: `dotnet build DotNetCloud.CI.slnf -c Release` succeeds (use the CI solution
   filter — the full `.sln` requires the Android SDK).
   - Also built touched projects directly in Debug during development
     (`src/Core/DotNetCloud.Core.Grpc`, `src/Core/DotNetCloud.Core.Server`, and each of the
     7 module Host projects).
6. ✓ Tests: `dotnet test tests/DotNetCloud.Modules.Notes.Tests/` plus the test projects for
   the other touched modules — all pass (Notes 128, Core.Server 622, Files 757, Calendar 179,
   Music 387, Video 209). New tests added:
   - ✓ `SearchIndexEventBridgeHandlerTests` (4 tests — mock `CoreCapabilitiesClient`; asserts
     `SubmitSearchIndexAsync` is called with the right `ModuleId`/`EntityId`/`Action`, and that
     rejections/exceptions are swallowed).
   - ✓ `CoreCapabilitiesSubmitSearchIndexTests` (3 tests — real `SearchIndexingService`;
     asserts enqueue + `Success=true` for valid actions, `Success=false` for invalid).
   - ✓ `NotesGrpcServiceSearchDocumentTests` (4 tests — regression for the Part A pull fix).
7. ✓ Deploy + live-verify (Notes) — deployed via `scripts/deploy.sh --force`; after the
   `module-id` header fix and the `NoTracking`/`AsTracking` fix, editing a note title to a
   unique word (`giraffe`) is indexed and searchable immediately (verified in
   `[core].[SearchIndexEntries]`, `IndexedAt` updates on edit). Remaining:
   real-time E2E for files/bookmarks/calendar/music/video (§8).

---

## 8. Deploy & live verification

Deploy is done with `scripts/deploy.sh` (never by hand). For a module-host-only change, the
module DLLs must also be copied to their module subdirectories — follow
`/memories/repo/video-module-deploy-locations.md` and `deploy-publish.md` conventions.

1. ☐ Build + publish Core.Server and the 7 module Host projects.
2. ☐ Stop service, copy Core.Server DLLs + each changed module host DLL to
   `/opt/dotnetcloud/server/modules/dotnetcloud.<name>/`.
3. ☐ Restart service; wait 1–2 minutes for all modules to register healthy.
4. ☐ **Real-time check (no reindex wait):**
   - Create a note → immediately search global (`GET /api/v1/search?q=...`) → result appears.
   - Edit the note title/content → search reflects new text.
   - Delete the note → it disappears from search.
   - Repeat for a file, bookmark, calendar event, music track, and video.
5. ☐ **Full reindex check:** trigger admin reindex; confirm `SearchIndexEntries` contains
   rows for all 7 modules (including `ModuleId = "notes"`).

---

## 9. Complete file checklist

Core changes:

- ✓ `src/Core/DotNetCloud.Core.Grpc/Protos/module_capabilities.proto` — add RPC + 2 messages
  (RPC is named `SubmitSearchIndex` — see Implementation Notes below)
- ✓ `src/Core/DotNetCloud.Core.Server/Grpc/Services/GrpcHealthServiceImpl.cs` — add using, field, ctor param, RPC method
- ✓ `src/Core/DotNetCloud.Core.Grpc/DotNetCloud.Core.Grpc.csproj` — add `Microsoft.Extensions.Hosting.Abstractions` ref
- ✓ `src/Core/DotNetCloud.Core.Grpc/SearchIndexEventBridgeHandler.cs` — NEW
- ✓ `src/Core/DotNetCloud.Core.Grpc/SearchIndexEventBridgeSubscriber.cs` — NEW
- ✓ `src/Core/DotNetCloud.Core.Grpc/SearchIndexBridgeServiceCollectionExtensions.cs` — NEW

Notes fix:

- ✓ `src/Modules/Notes/DotNetCloud.Modules.Notes.Host/Services/NotesGrpcService.cs` — Part A (usings, ctor, 2 RPCs, mapper)

Module host registrations (add `builder.Services.AddSearchIndexBridge();` after `AddAuditLogger()`):

- ✓ `src/Modules/Files/DotNetCloud.Modules.Files.Host/Program.cs`
- ✓ `src/Modules/Notes/DotNetCloud.Modules.Notes.Host/Program.cs`
- ✓ `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/Program.cs` (+ `TryAddSingleton` tweak)
- ✓ `src/Modules/Bookmarks/DotNetCloud.Modules.Bookmarks.Host/Program.cs`
- ✓ `src/Modules/Email/DotNetCloud.Modules.Email.Host/Program.cs`
- ✓ `src/Modules/Music/DotNetCloud.Modules.Music.Host/Program.cs`
- ✓ `src/Modules/Video/DotNetCloud.Modules.Video.Host/Program.cs`

Tests (new):

- ✓ `tests/DotNetCloud.Core.Server.Tests/Services/SearchIndexEventBridgeHandlerTests.cs`
- ✓ `tests/DotNetCloud.Core.Server.Tests/Services/CoreCapabilitiesSubmitSearchIndexTests.cs`
- ✓ `tests/DotNetCloud.Core.Server.Tests/Services/TestServerCallContext.cs` (test double)
- ✓ `tests/DotNetCloud.Modules.Notes.Tests/NotesGrpcServiceSearchDocumentTests.cs`

Deploy / live verification (pending):

- ☐ Deploy Core.Server + 7 module hosts via `scripts/deploy.sh` (module DLLs to `modules/dotnetcloud.<name>/`)
- ☐ Live real-time check + full-reindex check (see §8)

## 10. Implementation Notes (deviations from this plan as written)

1. **Proto RPC renamed `SubmitSearchIndexRequest` → `SubmitSearchIndex`.** protoc rejects an
   RPC whose name matches the request message type referenced in its own signature
   (`"SubmitSearchIndexRequest" is not a message type`) — verified empirically by building.
   The messages keep the planned names (`SubmitSearchIndexRequest`/`SubmitSearchIndexResponse`),
   so `CoreCapabilitiesServiceImpl.SubmitSearchIndex(...)` and
   `client.SubmitSearchIndexAsync(...)` are the generated APIs.
2. **`TryAddSingleton` returns `void`** — Calendar's tweak drops the `_ =` discard prefix that
   the original `AddSingleton` line had (`_ = builder.Services.AddSingleton(...)`), because
   `_ = voidExpr` is a compile error (CS8209).
3. **`SearchIndexingService` is sealed** (not mockable), so the `CoreCapabilitiesServiceImpl`
   unit test uses a real instance (unstarted) and asserts `PendingCount` to verify enqueue,
   rather than mocking the service.
4. **Email publisher gap is resolved** — both `GmailEmailProvider` and `ImapSmtpEmailProvider`
   publish `SearchIndexRequestEvent` during sync, so real-time email indexing will fire once
   the bridge is deployed.
5. **`module-id` metadata header is REQUIRED** — Core.Server's `AuthenticationInterceptor`
   rejects every `CoreCapabilities` gRPC call that lacks a `module-id` header
   (`Unauthenticated: Missing module-id metadata header`). The first deployed bridge silently
   dropped every event (the handler swallowed the `RpcException`). Fixed in
   `SearchIndexEventBridgeHandler` by attaching `new Metadata { { "module-id", … } }` from the
   host's `DOTNETCLOUD_MODULE_ID` env var (set by ProcessSupervisor) — same pattern as Chat's
   module gRPC clients. Added `SearchIndexEventBridgeHandlerTests.HandleAsync_AttachesModuleIdHeader`.
6. **Global `NoTracking` made real-time updates a silent no-op** — `CoreDbContext` sets
   `QueryTrackingBehavior.NoTracking`, so `IndexDocumentAsync` loading an existing
   `SearchIndexEntry` untracked and mutating it wrote nothing on `SaveChanges` (no error, no
   lock). The full reindex only worked because it deletes-then-inserts (INSERTs attach
   explicitly). Fixed by adding `.AsTracking()` to the upsert load in
   `SqlServerSearchProvider`/`PostgreSqlSearchProvider.IndexDocumentAsync` (same bug class as
   `/memories/repo/notracking-persistence-fix.md`); regression test
   `IndexDocumentAsync_NoTrackingContext_ExistingDocument_StillUpdatesEntry`.
7. **File-body search is a separate pre-existing gap** — every file `SearchIndexEntry` row has
   empty `Content` (modules don't supply extracted file text to the index), so words _inside_ a
   `.docx`/`.odt`/`.json` are not searchable. Unrelated to this bridge; needs file text-
   extraction wiring into the search document (out of scope here).

---

## 11. Risks & notes

1. **Chatty round-trip:** after Core receives the request, `SearchIndexingService` calls back
   to the module (`IModuleSearchDocumentClient.GetSearchableDocumentAsync`) to fetch the doc.
   A future optimization can inline the document payload into `SubmitSearchIndexRequest`.
2. **Startup window:** `SearchIndexingService.Start()` is invoked by
   `SearchReindexHostedService` after a 1-minute delay. Events enqueued during that first
   minute queue up (channel capacity 1000) and process once started. If needed later, start
   the channel eagerly.
3. **Email publisher gap — resolved ✓:** both `GmailEmailProvider` and `ImapSmtpEmailProvider`
   publish `SearchIndexRequestEvent` during sync (verified during implementation), so
   real-time email indexing fires once the bridge is deployed.
4. **Other modules' pull paths:** this plan only fixes the Notes pull bug. Spot-check the
   other 6 modules' `GetSearchableDocuments`/`GetSearchableDocument` return real docs while
   testing (a similar owner-scoping bug may exist elsewhere).
5. **Never break CRUD:** the bridge handler catches all exceptions so indexing failures can
   never fail a note/file/bookmark/etc. write.
