# Admin Share Deletion — Cleanup & Progress Reporting Plan

**Status:** Draft  
**Date:** 2026-06-18  
**Scope:** Ensure stale search documents, orphaned media library sources, and indexed media entities are cleaned up when an admin deletes an admin shared folder. Add progress reporting visible in the admin UI.

---

## Table of Contents

1. [Current State](#current-state)
2. [Goals](#goals)
3. [Architectural Constraints](#architectural-constraints)
4. [Implementation Phases](#implementation-phases)
   - [Phase 1: Search Cleanup Infrastructure](#phase-1-search-cleanup-infrastructure)
   - [Phase 2: Enhanced Delete Flow (Files Module)](#phase-2-enhanced-delete-flow-files-module)
   - [Phase 3: Media Source Cleanup (Core.Server)](#phase-3-media-source-cleanup-coreserver)
   - [Phase 4: Media Entity Cleanup (Core.Server → Module Callbacks)](#phase-4-media-entity-cleanup-coreserver--module-callbacks)
   - [Phase 5: Progress Reporting](#phase-5-progress-reporting)
5. [Verification](#verification)
6. [Key Decisions](#key-decisions)
7. [Relevant Files](#relevant-files)
8. [Edge Cases & Risks](#edge-cases--risks)

---

## Current State

When `AdminSharedFolderService.DeleteSharedFolderAsync()` runs:

| Action                                                                                   | Status                               |
| ---------------------------------------------------------------------------------------- | ------------------------------------ |
| Delete `AdminSharedFolderDefinition` row                                                 | ✅ Done                              |
| Cascade delete `AdminSharedFolderGrants` (EF Core `OnDelete(Cascade)`)                   | ✅ Done                              |
| Cascade delete `MountedNodeEntry` records (EF Core `OnDelete(Cascade)`)                  | ✅ Done                              |
| Remove search index documents for mounted files/folders                                  | ❌ Not done — orphaned               |
| Remove `MediaLibrarySource` entries in UserSettings referencing the share                | ❌ Not done — orphaned               |
| Remove `UserVideo` / `UserTrack` / `Photo` entities indexed from the share               | ❌ Not done — orphaned               |
| Remove orphaned `CanonicalVideo` / `CanonicalTrack` records with no remaining references | ❌ Not done — orphaned               |
| Show admin progress during cleanup                                                       | ❌ Not done — only a toast "deleted" |

The delete implementation (in `AdminSharedFolderService.cs`):

```csharp
public async Task DeleteSharedFolderAsync(Guid sharedFolderId, CallerContext caller, CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(caller);
    var folder = await LoadDefinitionAsync(sharedFolderId, asNoTracking: false, cancellationToken);
    _db.AdminSharedFolders.Remove(folder);
    await _db.SaveChangesAsync(cancellationToken);
}
```

This removes the DB record and lets EF Core cascade-delete grants and mounted-node entries. Nothing else.

---

## Goals

1. **Search cleanup:** All search index documents for the deleted share's mounted files and folders are removed, so users don't see stale search results.
2. **Media source cleanup:** `MediaLibrarySource` records in UserSettings referencing the deleted share are removed from each affected user's settings.
3. **Media entity cleanup:** `UserVideo`, `UserTrack`, and `Photo` entities that were indexed from the deleted share are removed.
4. **Canonical table cleanup:** Orphaned `CanonicalVideo` and `CanonicalTrack` records with no remaining `UserVideo`/`UserTrack` references are deleted.
5. **Progress visibility:** The admin sees a phase-by-phase progress panel in the admin UI during cleanup, not just a toast.

---

## Architectural Constraints

### 1. Process Isolation (gRPC-Only Communication)

The Files module runs as a separate process. It can only communicate with Core.Server and other modules via gRPC. It cannot directly call Core.Server services like `ISearchApiClient`, `IUserDirectory`, or media indexing callbacks.

### 2. `ISearchFtsClient` Lacks `RemoveDocumentAsync`

The Files module's search client (`ISearchFtsClient`) currently only exposes:

```csharp
public interface ISearchFtsClient
{
    bool IsAvailable { get; }
    Task<SearchResultDto?> SearchAsync(...);
    Task<bool> RequestModuleReindexAsync(string moduleId, CancellationToken ct = default);
}
```

However, the Search module's gRPC proto **already** has the `RemoveDocument` RPC:

```protobuf
rpc RemoveDocument (RemoveDocumentRequest) returns (RemoveDocumentResponse);

message RemoveDocumentRequest {
    string module_id = 1;
    string entity_id = 2;
}
```

We need to wrap this in `ISearchFtsClient`.

### 3. Core.Server Has Full Cleanup Capabilities

Core.Server (in-process) already has:

- `ISearchApiClient.RemoveDocumentAsync()` — removes individual search docs
- `IUserDirectory` — iterates all users
- `IUserSettingsService` — reads/writes user settings (where media sources live)
- `IMusicIndexingCallback` / `IVideoIndexingCallback` / `IPhotoIndexingCallback` — media entity cleanup

This makes Core.Server the natural place to orchestrate media cleanup.

### 4. Media Entities Have No Direct FK to Admin Shares

| Entity                      | Has `SharedFolderId`? | Has `FileNodeId`? | Has `OwnerId`?   |
| --------------------------- | --------------------- | ----------------- | ---------------- |
| `CanonicalVideo`            | ❌                    | ❌                | ❌ (shared)      |
| `UserVideo`                 | ❌                    | ✅                | ✅               |
| `CanonicalTrack`            | ❌                    | ❌                | ❌ (shared)      |
| `UserTrack`                 | ❌                    | ✅                | ✅               |
| `Photo`                     | ❌                    | ✅                | ✅               |
| `MediaLibrarySource` (JSON) | ✅ `SharedFolderId?`  | ✅ `FolderId?`    | Per-user setting |

The `FileNodeId` stored in media entities for admin share files is a **deterministic virtual GUID** from `VirtualMountedNodeRegistry.GetMountedNodeId(sharedFolderId, relativePath, isDirectory)`. Since these are computed from the shared folder ID and relative path (not random), we can compute them to find the exact entities to clean up — no filesystem enumeration needed.

### 5. `MountedNodeEntry` Data Is Lost on Delete

`MountedNodeEntry` records are cascade-deleted in the same `SaveChangesAsync` as the definition. All relative path data must be gathered **before** calling `Remove(folder)`.

---

## Implementation Phases

---

### Phase 1: Search Cleanup Infrastructure

**Depends on:** Nothing.  
**Parallel with:** Phase 2 design, Phase 3-4 design.

#### Step 1.1: Add `RemoveDocumentAsync` to `ISearchFtsClient`

**File:** `src/Modules/Search/DotNetCloud.Modules.Search.Client/ISearchFtsClient.cs`

Add the method signature:

```csharp
/// <summary>
/// Removes a single document from the full-text search index.
/// </summary>
/// <param name="moduleId">The module that owns the document (e.g., "files").</param>
/// <param name="entityId">The entity identifier used when the document was indexed.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns><c>true</c> if the document was removed; <c>false</c> if it was not found.</returns>
Task<bool> RemoveDocumentAsync(string moduleId, string entityId, CancellationToken cancellationToken = default);
```

#### Step 1.2: Implement in `SearchFtsClient`

**File:** `src/Modules/Search/DotNetCloud.Modules.Search.Client/SearchFtsClient.cs`

Implement the gRPC call to the existing `SearchService.RemoveDocument` RPC:

```csharp
public async Task<bool> RemoveDocumentAsync(string moduleId, string entityId, CancellationToken cancellationToken = default)
{
    if (!IsAvailable)
        return false;

    var request = new RemoveDocumentRequest
    {
        ModuleId = moduleId,
        EntityId = entityId,
    };

    var response = await _client.RemoveDocumentAsync(request, cancellationToken: cancellationToken);
    return response.Success;
}
```

#### Step 1.3: Inject `ISearchFtsClient` into `AdminSharedFolderService`

**File:** `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/AdminSharedFolderService.cs`

Add constructor parameter:

```csharp
private readonly ISearchFtsClient? _searchClient;

public AdminSharedFolderService(
    FilesDbContext db,
    IAdminSharedFolderPathValidator pathValidator,
    IUserOrganizationResolver? userOrganizationResolver = null,
    IGroupDirectory? groupDirectory = null,
    IAdminSharedFolderMaintenanceScheduler? maintenanceScheduler = null,
    ISearchFtsClient? searchClient = null)  // ← NEW
{
    _db = db;
    _pathValidator = pathValidator;
    _userOrganizationResolver = userOrganizationResolver;
    _groupDirectory = groupDirectory;
    _maintenanceScheduler = maintenanceScheduler;
    _searchClient = searchClient;  // ← NEW
}
```

**File:** `src/Modules/Files/DotNetCloud.Modules.Files.Data/FilesServiceRegistration.cs`

Register `ISearchFtsClient` in the DI container if not already present.

---

### Phase 2: Enhanced Delete Flow (Files Module)

**Depends on:** Phase 1.

#### Step 2.1: Gather `MountedNodeEntry` Data Before Delete

In `DeleteSharedFolderAsync`, **before** the `Remove(folder)` call, query the mounted node entries that will be cascade-deleted:

```csharp
public async Task<DeleteAdminSharedFolderResult> DeleteSharedFolderAsync(
    Guid sharedFolderId, CallerContext caller, CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(caller);

    var folder = await LoadDefinitionAsync(sharedFolderId, asNoTracking: false, cancellationToken);

    // ── Gather data BEFORE cascade delete ──
    var mountedEntries = await _db.MountedNodeEntries
        .Where(e => e.SharedFolderId == sharedFolderId)
        .Select(e => new { e.RelativePath, e.IsDirectory })
        .ToListAsync(cancellationToken);

    var displayName = folder.DisplayName;
    var searchEntityIds = new List<string>(mountedEntries.Count + 1);

    // Root folder entity ID
    searchEntityIds.Add(VirtualMountedNodeRegistry.GetAdminSharedFolderRootId(sharedFolderId).ToString());

    // Each mounted entry entity ID
    foreach (var entry in mountedEntries)
    {
        var id = VirtualMountedNodeRegistry.GetMountedNodeId(
            sharedFolderId, entry.RelativePath, entry.IsDirectory);
        searchEntityIds.Add(id.ToString());
    }

    // ── Delete definition (cascade deletes grants + mounted entries) ──
    _db.AdminSharedFolders.Remove(folder);
    await _db.SaveChangesAsync(cancellationToken);

    // ... continue in 2.2 and 2.4 ...
}
```

#### Step 2.2: Remove Search Documents

After the DB delete, iterate the gathered entity IDs and call `RemoveDocumentAsync` for each:

```csharp
    // ── Remove search documents ──
    var searchRemoved = 0;
    if (_searchClient is { IsAvailable: true })
    {
        foreach (var entityId in searchEntityIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await _searchClient.RemoveDocumentAsync("files", entityId, cancellationToken))
            {
                searchRemoved++;
            }
        }
    }

    _logger.LogInformation(
        "Admin shared folder {SharedFolderId} ('{DisplayName}') deleted. " +
        "Removed {SearchRemoved}/{SearchTotal} search documents.",
        sharedFolderId, displayName, searchRemoved, searchEntityIds.Count);
```

**Performance note:** For shares with thousands of files, consider batching. A future optimization could add `RemoveDocumentsAsync(string moduleId, IReadOnlyCollection<string> entityIds)` to `ISearchFtsClient`. For now, sequential calls are acceptable since typical admin shares are modest in size.

#### Step 2.3: Delete Definition (Existing Behavior)

This is the `_db.AdminSharedFolders.Remove(folder)` + `SaveChangesAsync` call in 2.1. No changes to cascade behavior.

#### Step 2.4: Publish Cleanup Event for Core.Server

Create a new event type so Core.Server can handle media cleanup:

**File:** `src/Core/DotNetCloud.Core/Events/AdminSharedFolderDeletedEvent.cs` (NEW)

```csharp
namespace DotNetCloud.Core.Events;

/// <summary>
/// Published when an admin shared folder definition is deleted.
/// Core.Server subscribers handle media source and entity cleanup.
/// </summary>
public sealed record AdminSharedFolderDeletedEvent : IEvent
{
    /// <summary>The deleted shared folder definition ID.</summary>
    public Guid SharedFolderId { get; init; }

    /// <summary>Display name of the deleted folder (for logging/audit).</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// All relative paths (files and directories) that were mounted under this share.
    /// Used to compute the deterministic virtual FileNodeIds for media entity cleanup.
    /// </summary>
    public IReadOnlyList<MountedEntryInfo> MountedEntries { get; init; } = [];
}

/// <summary>
/// Describes a single mounted entry (file or directory) within an admin shared folder.
/// </summary>
public sealed record MountedEntryInfo
{
    /// <summary>Normalized relative path within the shared folder.</summary>
    public string RelativePath { get; init; } = string.Empty;

    /// <summary>Whether this entry is a directory.</summary>
    public bool IsDirectory { get; init; }
}
```

Publish via `IEventBus` after the delete:

```csharp
    // ── Publish cleanup event ──
    var deleteEvent = new AdminSharedFolderDeletedEvent
    {
        SharedFolderId = sharedFolderId,
        DisplayName = displayName,
        MountedEntries = mountedEntries
            .Select(e => new MountedEntryInfo
            {
                RelativePath = e.RelativePath,
                IsDirectory = e.IsDirectory,
            })
            .ToList(),
    };

    await _eventBus.PublishAsync(deleteEvent, cancellationToken);
```

**Note:** The Files module needs `IEventBus` injected. If the module's event bus doesn't cross process boundaries, this event must be routed through the Files module's gRPC host to Core.Server. Alternatively, Core.Server can expose a gRPC endpoint that the Files module calls directly. See "Edge Cases & Risks" below.

#### Step 2.5: Return Cleanup Status

Change the return type from `void` to a result DTO:

**File:** `src/Modules/Files/DotNetCloud.Modules.Files/Services/IAdminSharedFolderService.cs`

```csharp
/// <summary>Deletes an existing admin shared folder definition and initiates cleanup.</summary>
Task<DeleteAdminSharedFolderResult> DeleteSharedFolderAsync(
    Guid sharedFolderId, CallerContext caller, CancellationToken cancellationToken = default);
```

**File:** `src/Modules/Files/DotNetCloud.Modules.Files/DTOs/DeleteAdminSharedFolderResult.cs` (NEW)

```csharp
public sealed record DeleteAdminSharedFolderResult
{
    public bool Deleted { get; init; }
    public Guid CleanupJobId { get; init; }
    public int PendingSearchRemovals { get; init; }
    public int SearchDocsRemoved { get; init; }
    public bool PendingMediaCleanup { get; init; }
    public IReadOnlyList<MountedEntryInfo> MountedEntries { get; init; } = [];
}
```

**File:** `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/AdminSharedFoldersController.cs`

Update the `DeleteAsync` method to return the result DTO:

```csharp
[HttpDelete("{sharedFolderId:guid}")]
public Task<IActionResult> DeleteAsync(Guid sharedFolderId) => ExecuteAsync(async () =>
{
    var caller = GetAuthenticatedCaller();
    var result = await _adminSharedFolderService.DeleteSharedFolderAsync(
        sharedFolderId, caller, HttpContext.RequestAborted);
    return Ok(Envelope(result));
});
```

---

### Phase 3: Media Source Cleanup (Core.Server)

**Depends on:** Phase 2.4 (event published).  
**Parallel with:** Phase 4.

#### Step 3.1: Create `AdminSharedFolderCleanupService`

**File:** `src/Core/DotNetCloud.Core.Server/Services/AdminSharedFolderCleanupService.cs` (NEW)

This service subscribes to `AdminSharedFolderDeletedEvent` and orchestrates media cleanup:

```csharp
namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Handles cleanup when an admin shared folder is deleted.
/// Removes orphaned media library sources, indexed media entities,
/// and canonical data with no remaining references.
/// </summary>
public sealed class AdminSharedFolderCleanupService : IEventHandler<AdminSharedFolderDeletedEvent>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AdminSharedFolderCleanupService> _logger;
    private readonly ICleanupStatusReporter? _statusReporter;  // For Phase 5

    public AdminSharedFolderCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<AdminSharedFolderCleanupService> logger,
        ICleanupStatusReporter? statusReporter = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _statusReporter = statusReporter;
    }

    public async Task HandleAsync(AdminSharedFolderDeletedEvent evt, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Starting cleanup for deleted admin shared folder {SharedFolderId} ('{DisplayName}')",
            evt.SharedFolderId, evt.DisplayName);

        try
        {
            // Step 3.2: Clean up media library sources
            var affectedUsers = await CleanupMediaSourcesAsync(evt.SharedFolderId, ct);

            // Step 4: Clean up media entities (see Phase 4)
            if (affectedUsers.Count > 0 && evt.MountedEntries.Count > 0)
            {
                await CleanupMediaEntitiesAsync(evt.SharedFolderId, evt.MountedEntries, affectedUsers, ct);
            }

            _logger.LogInformation(
                "Cleanup complete for admin shared folder {SharedFolderId}",
                evt.SharedFolderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Cleanup failed for admin shared folder {SharedFolderId}",
                evt.SharedFolderId);
            throw;
        }
    }

    // ... methods below ...
}
```

#### Step 3.2: Find and Remove Orphaned Media Library Sources

```csharp
private async Task<IReadOnlySet<Guid>> CleanupMediaSourcesAsync(
    Guid sharedFolderId, CancellationToken ct)
{
    using var scope = _scopeFactory.CreateScope();
    var userDirectory = scope.ServiceProvider.GetRequiredService<IUserDirectory>();
    var settingsService = scope.ServiceProvider.GetRequiredService<IUserSettingsService>();

    var affectedUsers = new HashSet<Guid>();
    var mediaTypes = new[] { "photos", "music", "video" };

    // Iterate all users (consider pagination for large user bases)
    var users = await userDirectory.GetAllUsersAsync(ct);
    foreach (var user in users)
    {
        ct.ThrowIfCancellationRequested();
        var userChanged = false;

        foreach (var mediaType in mediaTypes)
        {
            var sources = await MediaLibrarySourceSettings.LoadSourcesAsync(
                settingsService, user.Id, mediaType, ct);

            var before = sources.Count;
            sources = sources
                .Where(s => !(s.SourceKind == MediaLibrarySourceKind.SharedMount
                           && s.SharedFolderId == sharedFolderId))
                .ToList();

            if (sources.Count < before)
            {
                await MediaLibrarySourceSettings.SaveSourcesAsync(
                    settingsService, user.Id, mediaType, sources, ct);
                userChanged = true;

                _logger.LogInformation(
                    "Removed {Count} media source(s) for user {UserId} ({MediaType}) " +
                    "referencing deleted shared folder {SharedFolderId}",
                    before - sources.Count, user.Id, mediaType, sharedFolderId);
            }
        }

        if (userChanged)
        {
            affectedUsers.Add(user.Id);
        }
    }

    return affectedUsers;
}
```

---

### Phase 4: Media Entity Cleanup (Core.Server → Module Callbacks)

**Depends on:** Phase 3.  
**Parallel with:** Phase 5 (status reporting).

#### Step 4.1: Compute Deterministic FileNodeIds

From the event's `MountedEntries`, compute the virtual GUIDs that were stored in `UserVideo.FileNodeId`, `UserTrack.FileNodeId`, and `Photo.FileNodeId`:

```csharp
private static IReadOnlyList<Guid> ComputeFileNodeIds(
    Guid sharedFolderId, IReadOnlyList<MountedEntryInfo> entries)
{
    return entries
        .Select(e => VirtualMountedNodeRegistry.GetMountedNodeId(
            sharedFolderId, e.RelativePath, e.IsDirectory))
        .ToList();
}
```

**Note:** `VirtualMountedNodeRegistry` is in the Files module (`DotNetCloud.Modules.Files.Data`). We need to either:

- Move the `GetMountedNodeId` static method to `DotNetCloud.Core` (shared code)
- Or duplicate the deterministic GUID computation in Core.Server

The algorithm is simple: `SHA256("virtual::admin-shared-entry::{sharedFolderId:D}::{(isDirectory ? "dir" : "file")}::{normalizedPath}")` — safe to duplicate.

#### Step 4.2: Clean Up Media Entities via Module Callbacks

For each affected user, call the existing module callbacks to remove indexed entities:

```csharp
private async Task CleanupMediaEntitiesAsync(
    Guid sharedFolderId,
    IReadOnlyList<MountedEntryInfo> mountedEntries,
    IReadOnlySet<Guid> affectedUserIds,
    CancellationToken ct)
{
    var fileNodeIds = ComputeFileNodeIds(sharedFolderId, mountedEntries);
    if (fileNodeIds.Count == 0) return;

    using var scope = _scopeFactory.CreateScope();

    var musicCallback = scope.ServiceProvider.GetService<IMusicIndexingCallback>();
    var videoCallback = scope.ServiceProvider.GetService<IVideoIndexingCallback>();
    var photoCallback = scope.ServiceProvider.GetService<IPhotoIndexingCallback>();

    foreach (var userId in affectedUserIds)
    {
        ct.ThrowIfCancellationRequested();

        // Music: clean up UserTrack + orphaned CanonicalTrack
        if (musicCallback is not null)
        {
            await musicCallback.CleanupSharedFolderFilesAsync(
                userId, fileNodeIds, ct);
        }

        // Video: clean up UserVideo + orphaned CanonicalVideo
        if (videoCallback is not null)
        {
            await videoCallback.CleanupSharedFolderFilesAsync(
                userId, fileNodeIds, ct);
        }

        // Photos: clean up Photo entities
        if (photoCallback is not null)
        {
            await photoCallback.CleanupSharedFolderFilesAsync(
                userId, fileNodeIds, ct);
        }
    }
}
```

**New callback method on each indexing interface:**

**File:** `src/Core/DotNetCloud.Core/Services/ModuleApis/IMusicIndexingCallback.cs`

```csharp
/// <summary>
/// Cleans up indexed tracks that were part of a deleted admin shared folder.
/// Removes UserTrack records and orphaned CanonicalTrack records.
/// </summary>
Task<int> CleanupSharedFolderFilesAsync(
    Guid ownerId, IReadOnlyCollection<Guid> fileNodeIds,
    CancellationToken cancellationToken = default);
```

Same for `IVideoIndexingCallback` and `IPhotoIndexingCallback`.

**Implementations** — reuse existing delete patterns:

| Module | Implementation File        | Reuses Pattern From        |
| ------ | -------------------------- | -------------------------- |
| Music  | `LibraryScanService.cs`    | `SoftDeleteTracksAsync`    |
| Video  | `VideoIndexingCallback.cs` | `RemoveDeletedVideosAsync` |
| Photos | `PhotoIndexingCallback.cs` | `RemoveDeletedPhotosAsync` |

#### Step 4.3: Delete Orphaned Canonical Records

After removing `UserVideo` / `UserTrack` records, check if the canonical records have any remaining references. If none, delete them.

**Video — CanonicalVideo cleanup:**

```csharp
// In VideoIndexingCallback.CleanupSharedFolderFilesAsync, after removing UserVideos:

var contentHashes = removedVideos
    .Select(v => v.CanonicalContentHash)
    .Distinct()
    .ToList();

foreach (var hash in contentHashes)
{
    var remainingRefs = await _db.UserVideos
        .Where(uv => uv.CanonicalContentHash == hash && !uv.IsDeleted)
        .AnyAsync(ct);

    if (!remainingRefs)
    {
        // FK-safe order: CanonicalVideoEpisode → CanonicalSubtitle →
        //                  CanonicalVideoMetadata → CanonicalVideo
        var episodes = await _db.CanonicalVideoEpisodes
            .Where(e => e.CanonicalVideoHash == hash).ToListAsync(ct);
        _db.CanonicalVideoEpisodes.RemoveRange(episodes);

        var subtitles = await _db.CanonicalSubtitles
            .Where(s => s.CanonicalVideoHash == hash).ToListAsync(ct);
        _db.CanonicalSubtitles.RemoveRange(subtitles);

        var metadata = await _db.CanonicalVideoMetadata
            .Where(m => m.CanonicalVideoHash == hash).ToListAsync(ct);
        _db.CanonicalVideoMetadata.RemoveRange(metadata);

        var canonical = await _db.CanonicalVideos.FindAsync([hash], ct);
        if (canonical is not null)
            _db.CanonicalVideos.Remove(canonical);
    }
}
```

**Music — CanonicalTrack cleanup:**

```csharp
// In LibraryScanService.CleanupSharedFolderFilesAsync, after removing UserTracks:

var contentHashes = removedTracks
    .Select(t => t.CanonicalTrackHash)
    .Distinct()
    .ToList();

foreach (var hash in contentHashes)
{
    var remainingRefs = await _db.UserTracks
        .Where(ut => ut.CanonicalTrackHash == hash && !ut.IsDeleted)
        .AnyAsync(ct);

    if (!remainingRefs)
    {
        // FK-safe order: TrackArtists → TrackGenres → CanonicalTrack
        var trackArtists = await _db.TrackArtists
            .Where(ta => ta.CanonicalTrackHash == hash).ToListAsync(ct);
        _db.TrackArtists.RemoveRange(trackArtists);

        var trackGenres = await _db.TrackGenres
            .Where(tg => tg.CanonicalTrackHash == hash).ToListAsync(ct);
        _db.TrackGenres.RemoveRange(trackGenres);

        var canonical = await _db.CanonicalTracks.FindAsync([hash], ct);
        if (canonical is not null)
            _db.CanonicalTracks.Remove(canonical);
    }
}
```

**Note:** `CanonicalAlbum` and `CanonicalArtist` are NOT cleaned up here. They may be shared across multiple tracks from different sources. Only the direct canonical record (`CanonicalTrack` / `CanonicalVideo`) and its immediate children are cleaned up.

---

### Phase 5: Progress Reporting

**Depends on:** Phase 2.  
**Parallel with:** Phases 3-4.

#### Step 5.1: Add `AdminSharedFolderCleanupStatus` Model

**File:** `src/Modules/Files/DotNetCloud.Modules.Files/Models/AdminSharedFolderCleanupStatus.cs` (NEW)

```csharp
namespace DotNetCloud.Modules.Files.Models;

/// <summary>
/// Tracks the cleanup progress when an admin shared folder is deleted.
/// Persisted in the Files module database so the admin UI can poll for status.
/// </summary>
public sealed class AdminSharedFolderCleanupStatus
{
    public Guid CleanupJobId { get; set; } = Guid.CreateVersion7();
    public Guid SharedFolderId { get; set; }
    public string DisplayName { get; set; } = string.Empty;

    public CleanupPhase Phase { get; set; } = CleanupPhase.DeletingDefinition;

    // Search doc removal progress
    public int SearchDocsRemoved { get; set; }
    public int SearchDocsTotal { get; set; }

    // Media cleanup progress
    public int AffectedUsers { get; set; }
    public int UsersCleaned { get; set; }
    public int MediaEntitiesRemoved { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public bool IsComplete => Phase == CleanupPhase.Complete || Phase == CleanupPhase.Failed;
}

public enum CleanupPhase
{
    DeletingDefinition = 0,
    RemovingSearchDocs = 1,
    CleaningMediaSources = 2,
    CleaningMediaEntities = 3,
    Complete = 4,
    Failed = 5,
}
```

Add to `FilesDbContext`:

```csharp
public DbSet<AdminSharedFolderCleanupStatus> AdminSharedFolderCleanupStatuses
    => Set<AdminSharedFolderCleanupStatus>();
```

#### Step 5.2: Persist and Update Status

In `AdminSharedFolderService.DeleteSharedFolderAsync`, create and persist the status record:

```csharp
var status = new AdminSharedFolderCleanupStatus
{
    CleanupJobId = Guid.CreateVersion7(),
    SharedFolderId = sharedFolderId,
    DisplayName = folder.DisplayName,
    Phase = CleanupPhase.DeletingDefinition,
    SearchDocsTotal = searchEntityIds.Count,
    StartedAt = DateTime.UtcNow,
};
_db.AdminSharedFolderCleanupStatuses.Add(status);
await _db.SaveChangesAsync(cancellationToken);

// ... after delete ...
status.Phase = CleanupPhase.RemovingSearchDocs;
await _db.SaveChangesAsync(cancellationToken);

// ... after search removals ...
status.Phase = CleanupPhase.CleaningMediaSources;
status.SearchDocsRemoved = searchRemoved;
await _db.SaveChangesAsync(cancellationToken);
```

Core.Server's `AdminSharedFolderCleanupService` will also need to update the status for Phases 3-4. This requires Core.Server to have access to the Files module's DB or to communicate status back via gRPC.

**Option A:** Core.Server updates the status record directly (requires a reference to `FilesDbContext` — violates process isolation).  
**Option B:** Core.Server calls a Files module gRPC endpoint to update status.  
**Option C:** Files module polls its own event bus for progress (simpler but less direct).

**Recommendation: Option B** — add a simple gRPC method to the Files module's service:

```protobuf
rpc UpdateCleanupStatus (UpdateCleanupStatusRequest) returns (UpdateCleanupStatusResponse);

message UpdateCleanupStatusRequest {
    string cleanup_job_id = 1;
    string phase = 2;
    int32 affected_users = 3;
    int32 users_cleaned = 4;
    int32 media_entities_removed = 5;
    string error_message = 6;
}
```

#### Step 5.3: Add Cleanup Status Endpoint

**File:** `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/AdminSharedFoldersController.cs`

```csharp
/// <summary>
/// Gets the cleanup status for a deleted admin shared folder.
/// </summary>
[HttpGet("cleanup-status/{cleanupJobId:guid}")]
public Task<IActionResult> GetCleanupStatusAsync(Guid cleanupJobId) => ExecuteAsync(async () =>
{
    var status = await _db.AdminSharedFolderCleanupStatuses
        .AsNoTracking()
        .FirstOrDefaultAsync(s => s.CleanupJobId == cleanupJobId,
            HttpContext.RequestAborted);

    if (status is null)
        return NotFound(ErrorEnvelope("CleanupJobNotFound", "Cleanup job not found."));

    return Ok(Envelope(new
    {
        status.CleanupJobId,
        status.SharedFolderId,
        status.DisplayName,
        Phase = status.Phase.ToString(),
        status.SearchDocsRemoved,
        status.SearchDocsTotal,
        status.AffectedUsers,
        status.UsersCleaned,
        status.MediaEntitiesRemoved,
        status.StartedAt,
        status.CompletedAt,
        status.ErrorMessage,
        status.IsComplete,
    }));
});
```

#### Step 5.4: Update Admin UI

**File:** `src/UI/DotNetCloud.UI.Web.Client/Pages/Admin/FilesSharedFolders.razor`

After the delete API call returns, show a progress panel.

The progress panel should display these phases with visual state:

```
☐ Deleting definition...              ← completed (✓)
☐ Removing search documents... 3/12   ← in progress (spinner)
☐ Cleaning media sources...            ← pending (☐)
☐ Cleaning indexed media files...      ← pending (☐)
```

**Implementation sketch:**

```razor
@* After delete, show progress panel *@
@if (_cleanupJob is not null)
{
    <div class="cleanup-progress-panel">
        <h4>Cleaning up "{_cleanupJob.DisplayName}"...</h4>

        <CleanupPhaseRow Phase="DeletingDefinition" Label="Deleting definition"
                         Status="_cleanupJob.Phase" />
        <CleanupPhaseRow Phase="RemovingSearchDocs" Label="Removing search documents"
                         Status="_cleanupJob.Phase"
                         Progress="$"{_cleanupJob.SearchDocsRemoved}/{_cleanupJob.SearchDocsTotal}"" />
        <CleanupPhaseRow Phase="CleaningMediaSources" Label="Cleaning media sources"
                         Status="_cleanupJob.Phase"
                         Progress="$"{_cleanupJob.UsersCleaned}/{_cleanupJob.AffectedUsers}"" />
        <CleanupPhaseRow Phase="CleaningMediaEntities" Label="Cleaning indexed media files"
                         Status="_cleanupJob.Phase"
                         Progress="_cleanupJob.MediaEntitiesRemoved.ToString()" />

        @if (_cleanupJob.Phase == CleanupPhase.Complete)
        {
            <div class="cleanup-complete">✓ Cleanup complete</div>
            <button @onclick="DismissCleanup">Done</button>
        }
        else if (_cleanupJob.Phase == CleanupPhase.Failed)
        {
            <div class="cleanup-failed">✕ Cleanup failed: @_cleanupJob.ErrorMessage</div>
        }
    </div>
}

@code {
    private CleanupStatusResponse? _cleanupJob;
    private Timer? _pollTimer;

    private async Task DeleteAsync(AdminSharedFolderResponse folder)
    {
        if (!await _confirmDialog.ShowAsync(
            $"Delete shared folder '{folder.DisplayName}'?", "Delete Shared Folder"))
            return;

        _actionInProgress = true;
        try
        {
            var result = await Api.DeleteAdminSharedFolderAsync(folder.Id);
            _cleanupJob = result;  // Contains CleanupJobId + initial stats
            Toast.ShowSuccess($"Shared folder '{folder.DisplayName}' deletion started.");

            // Start polling for cleanup progress
            _pollTimer = new Timer(async _ => await PollCleanupStatusAsync(),
                null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _actionInProgress = false;
        }
    }

    private async Task PollCleanupStatusAsync()
    {
        if (_cleanupJob is null) return;

        try
        {
            _cleanupJob = await Api.GetAdminSharedFolderCleanupStatusAsync(
                _cleanupJob.CleanupJobId);

            if (_cleanupJob.IsComplete)
            {
                _pollTimer?.Dispose();
                _pollTimer = null;
                if (_cleanupJob.Phase == CleanupPhase.Complete)
                {
                    await RefreshAsync();  // Refresh the folder list
                }
            }
            StateHasChanged();
        }
        catch { /* Polling error — will retry */ }
    }
}
```

**Add API client methods:**

**File:** `src/UI/DotNetCloud.UI.Web.Client/Services/DotNetCloudApiClient.cs`

```csharp
public async Task<CleanupStatusResponse> GetAdminSharedFolderCleanupStatusAsync(
    Guid cleanupJobId, CancellationToken ct = default)
{
    var envelope = await _http.GetFromJsonAsync<ApiEnvelope<CleanupStatusResponse>>(
        $"api/v1/files/admin/shared-folders/cleanup-status/{cleanupJobId}", JsonOptions, ct);
    return envelope?.Data ?? new CleanupStatusResponse();
}
```

---

## Verification

### Unit Tests

| Test                                                            | What It Verifies                                                             | Test File                                       |
| --------------------------------------------------------------- | ---------------------------------------------------------------------------- | ----------------------------------------------- |
| `DeleteSharedFolderAsync_GathersMountedEntries_BeforeDelete`    | MountedNodeEntry data is queried before `Remove(folder)`                     | `AdminSharedFolderServiceTests.cs`              |
| `DeleteSharedFolderAsync_RemovesAllSearchDocuments`             | `RemoveDocumentAsync` called with correct entity IDs for root + all children | `AdminSharedFolderServiceTests.cs`              |
| `DeleteSharedFolderAsync_PublishesCleanupEvent`                 | `AdminSharedFolderDeletedEvent` published with correct paths                 | `AdminSharedFolderServiceTests.cs`              |
| `CleanupMediaSourcesAsync_RemovesMatchingSources`               | Sources with matching `SharedFolderId` are removed; others preserved         | `AdminSharedFolderCleanupServiceTests.cs` (new) |
| `CleanupMediaSourcesAsync_NoSources_NoErrors`                   | Empty sources handled gracefully                                             | same                                            |
| `CleanupSharedFolderFiles_Music_RemovesUserTracks`              | `UserTrack` records with matching `FileNodeId` removed                       | `LibraryScanServiceTests.cs`                    |
| `CleanupSharedFolderFiles_Music_RemovesOrphanedCanonicalTracks` | `CanonicalTrack` deleted when no remaining `UserTrack` refs                  | same                                            |
| `CleanupSharedFolderFiles_Video_RemovesUserVideos`              | `UserVideo` records with matching `FileNodeId` removed                       | `VideoIndexingCallbackTests.cs`                 |
| `CleanupSharedFolderFiles_Video_RemovesOrphanedCanonicalVideos` | `CanonicalVideo` deleted when no remaining `UserVideo` refs                  | same                                            |
| `GetCleanupStatus_ReturnsCurrentPhase`                          | Status endpoint returns correct phase and progress counts                    | `AdminSharedFoldersControllerTests.cs`          |

### Integration Tests

| Test                                                                          | What It Verifies          |
| ----------------------------------------------------------------------------- | ------------------------- |
| Delete share → search for share's files returns no results                    | Search index cleaned      |
| Delete share → affected user's media sources no longer reference it           | UserSettings cleaned      |
| Delete share that was a music source → `UserTrack` rows removed               | Music entities cleaned    |
| Delete share that was a video source → `UserVideo` rows removed               | Video entities cleaned    |
| Delete share → `CanonicalTrack` removed if no other user references it        | Canonical cleanup works   |
| Delete share → `CanonicalTrack` preserved if another user still references it | Canonical cleanup is safe |
| Delete share → progress panel shows phases completing                         | UI works                  |
| Delete share → refresh shows share gone from list                             | Basic flow still works    |

### Manual Verification

```powershell
# Build
dotnet build DotNetCloud.CI.slnf

# Run focused tests
dotnet test tests/DotNetCloud.Modules.Files.Tests/ `
    --filter "FullyQualifiedName~AdminSharedFolderServiceTests"

dotnet test tests/DotNetCloud.Modules.Files.Tests/ `
    --filter "FullyQualifiedName~AdminSharedFoldersController"

# Full test suite
dotnet test
```

---

## Key Decisions

### D1: Background Cleanup with Polling

The delete API returns immediately with a `CleanupJobId`. Search and media cleanup run in the background. The admin UI polls for progress. Rationale:

- Large shares with thousands of files could take seconds for search cleanup — synchronous would cause HTTP timeouts
- Media cleanup requires iterating all users and making cross-module gRPC calls — potentially slow
- The admin gets immediate feedback that deletion started, with granular progress

### D2: Core.Server Orchestrates Media Cleanup

Core.Server handles Phase 3 (media source cleanup) and Phase 4 (media entity cleanup), not the Files module. Rationale:

- Core.Server already has `IUserDirectory`, `IUserSettingsService`, and all three `I*IndexingCallback` interfaces
- The Files module would need new gRPC clients to reach Music/Video/Photos modules — violating the principle that modules don't call each other
- Core.Server is the natural mediator between modules

The Files module fires `AdminSharedFolderDeletedEvent`; Core.Server subscribes and handles the rest.

### D3: Deterministic Virtual GUIDs for Entity Lookup

Instead of filesystem enumeration, use `VirtualMountedNodeRegistry.GetMountedNodeId(sharedFolderId, relativePath, isDirectory)` to compute the `FileNodeId`s stored in media entities. Rationale:

- These GUIDs are deterministic (SHA-256 based), so they can be computed without the filesystem
- The relative paths come from `MountedNodeEntry` records (gathered before delete)
- No need to re-scan the (now potentially unavailable) source directory

### D4: Reference-Counted Canonical Cleanup

Only delete `CanonicalVideo` / `CanonicalTrack` when no remaining `UserVideo` / `UserTrack` references exist. Rationale:

- Multiple users may have indexed the same file from different sources
- Canonical data is shared — deleting it prematurely would corrupt other users' libraries
- The reference count check is a simple `AnyAsync` query

---

## Relevant Files

### New Files to Create

| File                                                                                                            | Purpose                           |
| --------------------------------------------------------------------------------------------------------------- | --------------------------------- |
| `src/Core/DotNetCloud.Core/Events/AdminSharedFolderDeletedEvent.cs`                                             | Event payload for cleanup         |
| `src/Core/DotNetCloud.Core.Server/Services/AdminSharedFolderCleanupService.cs`                                  | Core.Server cleanup orchestrator  |
| `src/Modules/Files/DotNetCloud.Modules.Files/Models/AdminSharedFolderCleanupStatus.cs`                          | Cleanup progress model            |
| `src/Modules/Files/DotNetCloud.Modules.Files/DTOs/DeleteAdminSharedFolderResult.cs`                             | Delete API response DTO           |
| `src/Modules/Files/DotNetCloud.Modules.Files.Data/Configuration/AdminSharedFolderCleanupStatusConfiguration.cs` | EF configuration for status table |
| `tests/DotNetCloud.Core.Server.Tests/Services/AdminSharedFolderCleanupServiceTests.cs`                          | Tests for cleanup orchestrator    |

### Existing Files to Modify

| File                                                                                           | Change                                      |
| ---------------------------------------------------------------------------------------------- | ------------------------------------------- |
| `src/Modules/Search/DotNetCloud.Modules.Search.Client/ISearchFtsClient.cs`                     | Add `RemoveDocumentAsync`                   |
| `src/Modules/Search/DotNetCloud.Modules.Search.Client/SearchFtsClient.cs`                      | Implement gRPC `RemoveDocument` call        |
| `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/AdminSharedFolderService.cs`        | Enhanced delete with search cleanup + event |
| `src/Modules/Files/DotNetCloud.Modules.Files/Services/IAdminSharedFolderService.cs`            | Return `DeleteAdminSharedFolderResult`      |
| `src/Modules/Files/DotNetCloud.Modules.Files.Data/FilesServiceRegistration.cs`                 | DI for `ISearchFtsClient`                   |
| `src/Modules/Files/DotNetCloud.Modules.Files.Data/FilesDbContext.cs`                           | Add `AdminSharedFolderCleanupStatus` DbSet  |
| `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/AdminSharedFoldersController.cs` | Return DTO, add status endpoint             |
| `src/UI/DotNetCloud.UI.Web.Client/Pages/Admin/FilesSharedFolders.razor`                        | Progress panel UI                           |
| `src/UI/DotNetCloud.UI.Web.Client/Services/DotNetCloudApiClient.cs`                            | Add status polling method                   |
| `src/Core/DotNetCloud.Core/Services/ModuleApis/IMusicIndexingCallback.cs`                      | Add `CleanupSharedFolderFilesAsync`         |
| `src/Core/DotNetCloud.Core/Services/ModuleApis/IVideoIndexingCallback.cs`                      | Add `CleanupSharedFolderFilesAsync`         |
| `src/Core/DotNetCloud.Core/Services/ModuleApis/IPhotoIndexingCallback.cs`                      | Add `CleanupSharedFolderFilesAsync`         |
| `src/Modules/Music/DotNetCloud.Modules.Music.Data/Services/LibraryScanService.cs`              | Implement cleanup                           |
| `src/Modules/Video/DotNetCloud.Modules.Video.Data/Services/VideoIndexingCallback.cs`           | Implement cleanup                           |
| `src/Modules/Photos/DotNetCloud.Modules.Photos.Data/Services/PhotoIndexingCallback.cs`         | Implement cleanup                           |
| `tests/DotNetCloud.Modules.Files.Tests/Services/AdminSharedFolderServiceTests.cs`              | Add cleanup tests                           |

### Reference Files (Read-Only)

| File                                                                                                         | Purpose                                     |
| ------------------------------------------------------------------------------------------------------------ | ------------------------------------------- |
| `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/VirtualMountedNodeRegistry.cs`                    | Deterministic GUID computation              |
| `src/Core/DotNetCloud.Core.Server/Services/MediaFolderImportService.cs`                                      | Existing scan/cleanup patterns              |
| `src/Modules/Files/DotNetCloud.Modules.Files/Models/MountedNodeEntry.cs`                                     | Entity to gather before delete              |
| `src/Modules/Files/DotNetCloud.Modules.Files.Data/Configuration/MountedNodeEntryConfiguration.cs`            | Cascade delete config                       |
| `src/Modules/Files/DotNetCloud.Modules.Files.Data/Configuration/AdminSharedFolderDefinitionConfiguration.cs` | Cascade delete config                       |
| `src/Modules/Search/DotNetCloud.Modules.Search.Host/Protos/search_service.proto`                             | Existing `RemoveDocument` RPC               |
| `src/Core/DotNetCloud.Core/DTOs/Media/MediaLibrarySource.cs`                                                 | `MediaLibrarySourceSettings` static helpers |

---

## Edge Cases & Risks

### 1. Event Bus Cross-Process Boundary

**Risk:** The Files module's `IEventBus` may be in-process only and not reach Core.Server's subscribers.  
**Mitigation:** Use a gRPC-based event relay or have the Files module call a Core.Server gRPC endpoint directly (`CleanupAdminSharedFolder` RPC) instead of relying on the event bus.  
**Decision needed:** Verify if `IEventBus` crosses process boundaries. If not, add a `CleanupAdminSharedFolder` gRPC method to the Core.Server gRPC service.

### 2. Concurrent Scans During Deletion

**Risk:** An admin deletes a share while a media scan is actively indexing files from it.  
**Impact:** The scan's `GetSearchableDocumentAsync` would return `null` for files that no longer exist — the Search module's existing logic auto-removes these. Media entities would be cleaned up by the subsequent cleanup pass.  
**Mitigation:** Check `AdminSharedFolderDefinition.IsEnabled` (or existence) before processing a source in `MediaFolderImportService.ScanSourcesAsync`. Skip sources referencing deleted shares.

### 3. Large Share Performance

**Risk:** A share with 10,000+ files would generate 10,000 `RemoveDocumentAsync` gRPC calls — slow.  
**Mitigation:** Add a batch removal method `RemoveDocumentsAsync(string moduleId, IReadOnlyCollection<string> entityIds)` to `ISearchFtsClient` and the gRPC proto. The current sequential approach works for typical shares (< 1,000 files).

### 4. Stale Canonical Data from Other Sources

**Risk:** `CanonicalAlbum` and `CanonicalArtist` are not cleaned up. A music album with tracks from both an admin share and a user-owned folder would lose some tracks but keep the album record.  
**Impact:** Minor — the album would show fewer tracks but still be valid. Full cleanup of shared canonical data is complex (requires checking ALL track references across ALL users).  
**Decision:** Out of scope for this plan. Only `CanonicalTrack` and `CanonicalVideo` direct records are cleaned up.

### 5. Progress Status Storage

**Risk:** `AdminSharedFolderCleanupStatus` records accumulate over time.  
**Mitigation:** Add a background cleanup job that deletes status records older than 7 days. Or expose a "dismiss" action in the UI that deletes the record.

### 6. Delete Server-Side Files?

**Risk:** The admin may expect the actual files on disk to be deleted when an admin share is removed.  
**Decision:** **No.** Admin shared folders expose server-local paths as read-only. Deleting the definition removes access, not the files. This is the correct behavior — the source path may contain data used outside DotNetCloud. If the admin wants to delete files, they do it outside the app.
