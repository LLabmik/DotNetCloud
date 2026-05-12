# Virtual File Syncing Plan — Files On-Demand for SyncTray

**Date:** 2026-05-12  
**Status:** Phase 1 complete ✅  
**Based on:** `docs/development/SYNC_IMPROVEMENT_PLAN.md` Appendix C (Virtual Filesystem)  
**Handoff Process:** `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md`  
**Related:** `docs/SYNCSERVICE_SYNCTRAY_MERGE_PLAN.md` (assumed complete — single-process SyncTray)

---

## Table of Contents

1. [Overview & Motivation](#overview--motivation)
2. [Current Architecture & Integration Points](#current-architecture--integration-points)
3. [Target Architecture](#target-architecture)
4. [Phase 1 — Server-Side Prerequisites](#phase-1--server-side-prerequisites)
5. [Phase 2 — Core Abstraction Layer](#phase-2--core-abstraction-layer)
6. [Phase 3 — Windows Implementation](#phase-3--windows-implementation)
7. [Phase 4 — Linux Implementation](#phase-4--linux-implementation)
8. [Phase 5 — SyncTray UI Integration](#phase-5--synctray-ui-integration)
9. [Phase 6 — Testing & Validation](#phase-6--testing--validation)
10. [File Manifest](#file-manifest)
11. [Handoff Sequence](#handoff-sequence)
12. [Verification Checklist](#verification-checklist)
13. [Decisions & Rationale](#decisions--rationale)

---

## Overview & Motivation

**Concept:** Files appear in the local filesystem with names, sizes, and timestamps — but their content is NOT downloaded until a user or application actually opens/reads them. This dramatically reduces local disk usage and initial sync time for large accounts.

**Key benefits:**

- ☐ Users with 500 GB on the server do not need 500 GB of local disk
- ☐ Initial sync setup becomes near-instant (metadata tree download only)
- ☐ Linux FUSE support is a competitive differentiator (Nextcloud lacks it)
- ☐ Modern cloud storage clients (OneDrive, Dropbox, Google Drive) all offer this

**Platform strategy:**

| Platform | Technology | Shell Integration |
|----------|-----------|-------------------|
| Windows | Cloud Filter API (`cfapi.dll`) — same API as OneDrive | Cloud icon overlays (☁), right-click pin/free-up, status column in Explorer |
| Linux | FUSE (`Tmds.Fuse`) — filesystem in userspace | File manager displays full directory listing; tray app shows VFS status |
| macOS | File Provider Extension | Deferred to future macOS contributor |

**Non-goals for initial release:**

- Prefetch heuristics (batch-download small files in a directory on access)
- Thumbnail/preview support on Windows
- macOS implementation

---

## Current Architecture & Integration Points

### Existing infrastructure (reusable as-is)

| Component | File | Role in VFS |
|-----------|------|-------------|
| `ISyncEngine` / `SyncEngine` | `src/Clients/DotNetCloud.Client.Core/Sync/` | Central sync coordinator — will be wrapped by `VirtualFileSyncEngine` |
| `IChunkedTransferClient` | `src/Clients/DotNetCloud.Client.Core/Transfer/` | Chunked delta downloads — reused for on-demand hydration |
| `ILocalStateDb` | `src/Clients/DotNetCloud.Client.Core/LocalState/` | SQLite metadata store — becomes the VFS metadata cache |
| `LocalFileRecord` | `src/Clients/DotNetCloud.Client.Core/LocalState/Entities/` | Per-file tracking — gains `HydrationState` field |
| `IDotNetCloudApiClient` | `src/Clients/DotNetCloud.Client.Core/Api/` | HTTP client for server communication — chunk download with Range support |
| `ISelectiveSyncConfig` | `src/Clients/DotNetCloud.Client.Core/SelectiveSync/` | Per-folder sync toggles — VFS is a superset: everything visible, only accessed files downloaded |
| `SettingsViewModel` | `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/` | Settings UI — gains "Storage mode" toggle |
| `App.axaml.cs` | `src/Clients/DotNetCloud.Client.SyncTray/` | DI root + lifecycle — wires VFS provider |

### Server API endpoints (already exist)

| Endpoint | Purpose for VFS |
|----------|----------------|
| `GET /api/v1/sync/tree` | Full metadata tree → placeholder creation |
| `GET /api/v1/sync/changes?cursor=...` | Cursor-based delta → update placeholder metadata |
| `GET /api/v1/files/{nodeId}/chunks/{hash}` | Chunk download → on-demand hydration (needs Range header verification) |
| `POST /api/v1/files/upload/initiate` | Chunked upload → works normally (file is hydrated when user modifies it) |

---

## Target Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                     SyncTray (Avalonia)                           │
│                                                                    │
│  SettingsViewModel ──> StorageMode toggle (DownloadAll | OnDemand)│
│  TrayViewModel ──> VFS status indicators (cached / cloud-only)   │
│  App.axaml.cs ──> DI: IVirtualFileProvider per platform          │
│                   ──> Uses VirtualFileSyncEngine instead of       │
│                       plain SyncEngine when StorageMode==OnDemand │
└──────────────────────────┬───────────────────────────────────────┘
                           │
┌──────────────────────────▼───────────────────────────────────────┐
│                Client.Core (shared library)                        │
│                                                                    │
│  IVirtualFileProvider (interface)                                  │
│   ├─ InitializeAsync()          Register sync root / mount FUSE   │
│   ├─ CreatePlaceholdersAsync()   Metadata-only files from tree    │
│   ├─ HydrateFileAsync()          Download content on demand       │
│   ├─ DehydrateFileAsync()        Free space, back to placeholder  │
│   ├─ PinFileAsync()              "Always keep on this device"     │
│   ├─ UnpinFileAsync()            Allow dehydration                │
│   ├─ IsHydratedAsync()           Check hydration state            │
│   └─ ShutdownAsync()             Unregister sync root / unmount   │
│                                                                    │
│  VirtualFileSyncEngine                                            │
│   ├─ Wraps ISyncEngine                                            │
│   ├─ Metadata-only sync mode    (StorageMode == FilesOnDemand)    │
│   ├─ On-demand hydration dispatch → IVirtualFileProvider          │
│   ├─ Mode switch: DownloadAll ↔ FilesOnDemand                     │
│   └─ LRU cache policy (Linux)                                     │
│                                                                    │
│  LocalFileRecord.HydrationState                                    │
│   ├─ CloudOnly    Metadata only, no local content                 │
│   ├─ Hydrated     Content downloaded and cached                   │
│   ├─ Pinned       Always kept locally, exempt from eviction       │
│   └─ Downloading  Content being fetched right now                 │
│                                                                    │
│  VirtualFileSettings                                              │
│   ├─ StorageMode enum          DownloadAll / FilesOnDemand        │
│   ├─ MaxCacheSizeBytes         LRU cache cap (default: 10% free)  │
│   └─ PinList                   Pinned file paths                  │
└──────────────┬────────────────────────────────┬──────────────────┘
               │                                │
┌──────────────▼──────────────┐  ┌──────────────▼──────────────────┐
│  Windows (Client.Core)       │  │  Linux (Client.Core)             │
│  Platform/Windows/           │  │  Platform/Linux/                 │
│                              │  │                                  │
│  CloudFilterSyncProvider     │  │  FuseSyncFilesystem              │
│   : IVirtualFileProvider     │  │   : IVirtualFileProvider         │
│                              │  │                                  │
│  CloudFilterCallbacks         │  │  DotNetCloudFuseOperations       │
│   ├─ CF_CALLBACK_FETCH_DATA  │  │   ├─ getattr() → LocalStateDb   │
│   ├─ CF_CALLBACK_VALIDATE    │  │   ├─ readdir() → LocalStateDb   │
│   ├─ CF_CALLBACK_PLACEHOLDERS│  │   ├─ open()/read() → hydrate    │
│   ├─ CF_CALLBACK_NOTIFY_OPEN │  │   ├─ write()/create() → upload  │
│   └─ CF_CALLBACK_NOTIFY_DEL  │  │   └─ unlink()/rename() → delete │
│                              │  │                                  │
│  CfApi/ (P/Invoke)            │  │  LruCacheManager                │
│   ├─ CfRegisterSyncRoot       │  │   ├─ Content-addressed chunks  │
│   ├─ CfCreatePlaceholders     │  │   ├─ Access-time tracking      │
│   ├─ CfExecute (hydrate)      │  │   ├─ LRU eviction              │
│   ├─ CfSetPinState            │  │   └─ Pinned-file exemption    │
│   └─ CfUpdatePlaceholder      │  │                                  │
│                              │  │  Cache dir:                      │
│  Shell integration:           │  │  ~/.local/share/dotnetcloud/    │
│   ├─ Cloud icon overlays     │  │    cache/                       │
│   └─ Context menu extensions │  │                                  │
└──────────────────────────────┘  └─────────────────────────────────┘
```

---

## Phase 1 — Server-Side Prerequisites

**Machine:** `mint22`  
**Depends on:** nothing  
**Blocks:** Phase 2 (core abstractions)  
**Handoff target:** `mint22`

### Step 1.1 — Verify/Implement Range Header Support on Chunk Download

**Goal:** The chunk download endpoint must support HTTP `Range` headers so the VFS layer can request partial file content during streaming hydration (e.g., a media player seeking within a large file).

**What to verify:**
```bash
curl -I -H "Range: bytes=0-1023" \
  "https://mint22:5443/api/v1/files/{nodeId}/chunks/{chunkHash}"
# Expected: HTTP 206 Partial Content, Content-Range header present
```

**If missing:** Enable range processing via ASP.NET Core's `EnableRangeProcessing` on the `PhysicalFileResult` or `FileStreamResult` returned by the chunk-serving action.

**Files to inspect/modify:**

- ✓ Server-side chunk download endpoint (FilesController.DownloadChunkByHashAsync)

**Deliverables:**

- ✓ Chunk download endpoint returns `206 Partial Content` for `Range` requests
- ✓ `Content-Range` and `Accept-Ranges: bytes` headers present in response
- ✓ Full-file download (no Range header) still returns `200 OK` unchanged

### Step 1.2 — Add `?metadataOnly=true` to Tree Endpoint (Optional Optimization)

**Goal:** When `metadataOnly=true`, the `GET /api/v1/sync/tree` endpoint skips `ContentHash` fields, reducing payload size for placeholder creation. Placeholders only need names, sizes, timestamps, and node IDs — not content hashes.

**Note:** This is an optimization, not a blocker. The full tree response works fine for VFS; this just makes initial placeholder creation faster on large accounts.

**Files to inspect/modify:**

- ✓ Server-side sync tree endpoint (SyncController.GetTreeAsync + ISyncService.GetFolderTreeAsync + SyncService.BuildTreeNodeAsync)

**Deliverables:**

- ✓ `GET /api/v1/sync/tree?metadataOnly=true` returns tree without `contentHash` fields

---

## Phase 2 — Core Abstraction Layer

**Machine:** `Windows11-TestDNC`  
**Depends on:** Phase 1 (server ready)  
**Blocks:** Phase 3 (Windows), Phase 4 (Linux)

### Step 2.1 — Define `IVirtualFileProvider` Interface

**File:** `src/Clients/DotNetCloud.Client.Core/VirtualFiles/IVirtualFileProvider.cs`

```csharp
namespace DotNetCloud.Client.Core.VirtualFiles;

/// <summary>
/// Platform-specific virtual file system provider.
/// Implementations: CloudFilterSyncProvider (Windows), FuseSyncFilesystem (Linux).
/// </summary>
public interface IVirtualFileProvider : IAsyncDisposable
{
    /// <summary>
    /// Initializes the provider — registers the sync root with the OS (Windows)
    /// or mounts the FUSE filesystem (Linux).
    /// </summary>
    Task InitializeAsync(SyncContext context, CancellationToken ct = default);

    /// <summary>
    /// Creates metadata-only placeholder files from the server folder tree.
    /// Called during initial sync when StorageMode == FilesOnDemand.
    /// </summary>
    Task CreatePlaceholdersAsync(SyncTreeNodeResponse tree, CancellationToken ct = default);

    /// <summary>
    /// Downloads file content on demand and hydrates the placeholder.
    /// Called when a user/application opens a cloud-only file.
    /// </summary>
    Task HydrateFileAsync(string localPath, Guid nodeId, CancellationToken ct = default);

    /// <summary>
    /// Replaces hydrated file content with a placeholder, freeing local disk space.
    /// Pinned files are not dehydrated.
    /// </summary>
    Task DehydrateFileAsync(string localPath, CancellationToken ct = default);

    /// <summary>
    /// Pins a file — marks it as "always keep on this device."
    /// Pinned files are exempt from dehydration and LRU eviction.
    /// </summary>
    Task PinFileAsync(string localPath, CancellationToken ct = default);

    /// <summary>
    /// Unpins a file — allows it to be dehydrated or evicted.
    /// </summary>
    Task UnpinFileAsync(string localPath, CancellationToken ct = default);

    /// <summary>
    /// Returns true if the file has local content (Hydrated or Pinned state).
    /// </summary>
    Task<bool> IsHydratedAsync(string localPath, CancellationToken ct = default);

    /// <summary>
    /// Shuts down the provider — unregisters the sync root (Windows)
    /// or unmounts the FUSE filesystem (Linux).
    /// </summary>
    Task ShutdownAsync(CancellationToken ct = default);
}
```

**Deliverables:**

- ☐ `IVirtualFileProvider` interface in `VirtualFiles/` namespace
- ☐ XML doc comments on all members

### Step 2.2 — Add `HydrationState` to `LocalFileRecord`

**File:** `src/Clients/DotNetCloud.Client.Core/LocalState/Entities/LocalFileRecord.cs`

Add a new property and enum:

```csharp
/// <summary>Virtual file hydration state. Defaults to Hydrated for backward compatibility.</summary>
public HydrationState HydrationState { get; set; } = HydrationState.Hydrated;
```

```csharp
namespace DotNetCloud.Client.Core.LocalState;

/// <summary>
/// Tracks whether a local file has its content downloaded (hydrated)
/// or exists only as a metadata placeholder (cloud-only).
/// </summary>
public enum HydrationState
{
    /// <summary>Content is downloaded and available locally.</summary>
    Hydrated = 0,

    /// <summary>Metadata-only placeholder. Content downloads on first access.</summary>
    CloudOnly = 1,

    /// <summary>Content is downloaded and pinned — exempt from dehydration/eviction.</summary>
    Pinned = 2,

    /// <summary>Content is being downloaded right now.</summary>
    Downloading = 3,
}
```

**Schema evolution (in `LocalStateDb.RunSchemaEvolutionAsync`):**
```csharp
// Add HydrationState column to FileRecords for virtual file support
var fileRecordColumns = await GetColumnNamesAsync(conn, "FileRecords", cancellationToken);
if (!fileRecordColumns.Contains("HydrationState"))
    await ExecuteNonQueryAsync(conn,
        "ALTER TABLE FileRecords ADD COLUMN HydrationState INTEGER NOT NULL DEFAULT 0",
        cancellationToken);
```

**Files to modify:**

- ☐ `src/Clients/DotNetCloud.Client.Core/LocalState/Entities/LocalFileRecord.cs` — add `HydrationState` property
- ☐ `src/Clients/DotNetCloud.Client.Core/LocalState/LocalStateDb.cs` — add schema evolution step

**Deliverables:**

- ☐ `HydrationState` enum defined
- ☐ `LocalFileRecord.HydrationState` property added (default `Hydrated` for backward compat)
- ☐ Schema evolution adds `HydrationState` column to existing databases

### Step 2.3 — Create `VirtualFileSettings`

**File:** `src/Clients/DotNetCloud.Client.Core/VirtualFiles/VirtualFileSettings.cs`

```csharp
namespace DotNetCloud.Client.Core.VirtualFiles;

/// <summary>
/// User-configurable settings for virtual file syncing.
/// Persisted alongside other local settings JSON.
/// </summary>
public sealed class VirtualFileSettings
{
    /// <summary>Download all files eagerly, or use placeholders with on-demand hydration.</summary>
    public VirtualFileStorageMode StorageMode { get; set; } = VirtualFileStorageMode.DownloadAll;

    /// <summary>Maximum size of the local content cache in bytes. 0 = no limit.</summary>
    public long MaxCacheSizeBytes { get; set; }

    /// <summary>Set of file paths pinned for offline access (case-insensitive).</summary>
    public HashSet<string> PinList { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Controls how files are stored locally.
/// </summary>
public enum VirtualFileStorageMode
{
    /// <summary>All files are downloaded eagerly. Current default behavior.</summary>
    DownloadAll = 0,

    /// <summary>Files are metadata-only placeholders. Content downloads on first access.</summary>
    FilesOnDemand = 1,
}
```

**Deliverables:**

- ☐ `VirtualFileSettings` class with `StorageMode`, `MaxCacheSizeBytes`, `PinList`
- ☐ `VirtualFileStorageMode` enum
- ☐ JSON-serializable for persistence alongside existing settings

### Step 2.4 — Create `VirtualFileSyncEngine`

**File:** `src/Clients/DotNetCloud.Client.Core/VirtualFiles/VirtualFileSyncEngine.cs`

This wraps `ISyncEngine` and delegates to `IVirtualFileProvider` for VFS-specific operations. When `StorageMode == DownloadAll`, it passes through to the plain `SyncEngine` unchanged.

**Key behaviors:**

| Scenario | Behavior |
|----------|----------|
| `SyncAsync()` with `FilesOnDemand` | Calls `IVirtualFileProvider.CreatePlaceholdersAsync()` instead of downloading content. Metadata sync only. |
| File opened by user (cloud-only) | `IVirtualFileProvider` callback → `HydrateFileAsync()` → `ChunkedTransferClient` downloads → placeholder hydrated |
| File modified by user (hydrated) | Normal upload via `SyncEngine` — file is already local |
| Mode switch: `FilesOnDemand` → `DownloadAll` | Hydrates all non-pinned, non-hydrated files |
| Mode switch: `DownloadAll` → `FilesOnDemand` | Dehydrates all un-pinned files (replaces with placeholders) |
| Server delete of cloud-only file | Remove placeholder (no content to delete) |
| Server update of cloud-only file | Update placeholder metadata (size, timestamp) — no content download |
| Server update of hydrated file | Invalidate hydration state → re-download on next access, or download immediately |

**Dependencies injected:**
- `ISyncEngine` — the underlying sync engine (wrapped)
- `IVirtualFileProvider` — platform-specific VFS operations
- `IChunkedTransferClient` — chunk download for hydration
- `ILocalStateDb` — query/update hydration state
- `VirtualFileSettings` — storage mode and cache config
- `ILogger<VirtualFileSyncEngine>`

**Deliverables:**

- ☐ `VirtualFileSyncEngine` class wrapping `ISyncEngine`
- ☐ Metadata-only sync pass when `StorageMode == FilesOnDemand`
- ☐ `HydrateFileAsync()` using `ChunkedTransferClient`
- ☐ Mode switch logic (`DownloadAll` ↔ `FilesOnDemand`)
- ☐ Unit test coverage for mode switching

### Step 2.5 — Register VFS Services in DI

**File:** `src/Clients/DotNetCloud.Client.Core/ClientCoreServiceExtensions.cs`

Add to `AddDotNetCloudClientCore()`:

```csharp
// Virtual file system
services.AddSingleton<VirtualFileSettings>();

// Platform-specific IVirtualFileProvider
if (OperatingSystem.IsWindows())
    services.AddSingleton<IVirtualFileProvider, CloudFilterSyncProvider>();
else if (OperatingSystem.IsLinux())
    services.AddSingleton<IVirtualFileProvider, FuseSyncFilesystem>();
else
    services.AddSingleton<IVirtualFileProvider, NoOpVirtualFileProvider>(); // macOS stub

services.AddSingleton<VirtualFileSyncEngine>();
```

**Deliverables:**

- ☐ `IVirtualFileProvider` registered per platform
- ☐ `VirtualFileSettings` registered as singleton
- ☐ `VirtualFileSyncEngine` registered as singleton
- ☐ `NoOpVirtualFileProvider` stub for unsupported platforms (macOS for now)

---

## Phase 3 — Windows Implementation

**Machine:** `Windows11-TestDNC`  
**Depends on:** Phase 2 (core abstractions)  
**Parallel with:** Phase 4 (Linux)

### Step 3.1 — P/Invoke Wrappers for Cloud Filter API (`cfapi.dll`)

**Files:**
- `src/Clients/DotNetCloud.Client.Core/Platform/Windows/CfApi/CfApiNative.cs`
- `src/Clients/DotNetCloud.Client.Core/Platform/Windows/CfApi/CfApiTypes.cs`

The Cloud Filter API is a native Windows API in `cfapi.dll`. We wrap the essential functions via `[DllImport]`. These are the same APIs OneDrive uses internally.

**Key functions to wrap:**

| Function | Purpose |
|----------|---------|
| `CfRegisterSyncRoot` | Register the sync folder as a sync root with the Windows shell |
| `CfUnregisterSyncRoot` | Remove the sync root registration |
| `CfCreatePlaceholders` | Create metadata-only placeholder files from a directory tree |
| `CfUpdatePlaceholder` | Update placeholder metadata (size, timestamps) |
| `CfExecute` | Execute operations on placeholder files (transfer data, dehydrate, etc.) |
| `CfSetPinState` | Set pin state (pinned / unpinned) for a placeholder |
| `CfGetPlaceholderInfo` | Query placeholder state and attributes |
| `CfConnectSyncRoot` | Connect to an existing sync root (for reconnection after restart) |
| `CfDisconnectSyncRoot` | Disconnect from a sync root |

**Key types/enums:**

```csharp
// Sync root registration flags
[Flags]
enum CF_CONNECT_FLAGS : uint
{
    NONE = 0,
    REQUIRE_PROCESS_INFO = 0x00000002,
    REQUIRE_FULL_FILE_PATH = 0x00000004,
    BLOCK_SELF = 0x00000008,
}

// Placeholder creation flags
[Flags]
enum CF_PLACEHOLDER_CREATE_FLAGS : uint
{
    NONE = 0,
    DISABLE_ON_DEMAND_POPULATION = 0x00000001,
    MARK_IN_SYNC = 0x00000002,
}

// Hydration policy
enum CF_HYDRATION_POLICY : uint
{
    FULL = 2,    // Hydrate on first access
    PROGRESSIVE = 3, // Partial hydration for streaming
}

// Pin state
enum CF_PIN_STATE : uint
{
    UNSPECIFIED = 0,
    PINNED = 1,
    UNPINNED = 2,
    EXCLUDED = 3,
    INHERIT = 4,
}

// Callback types (the core of on-demand hydration)
enum CF_CALLBACK_TYPE : uint
{
    FETCH_DATA = 0,           // Download content for a placeholder
    VALIDATE_DATA = 1,        // Verify local content matches server
    FETCH_PLACEHOLDERS = 2,   // Enumerate directory placeholders
    CANCEL_FETCH_DATA = 3,    // Cancel an in-progress fetch
    NOTIFY_FILE_OPEN_COMPLETION = 4,
    NOTIFY_FILE_CLOSE_COMPLETION = 5,
    NOTIFY_DEHYDRATE = 6,
    NOTIFY_DEHYDRATE_COMPLETION = 7,
    NOTIFY_DELETE = 8,
    NOTIFY_DELETE_COMPLETION = 9,
    NOTIFY_RENAME = 10,
    NOTIFY_RENAME_COMPLETION = 11,
}

// Operation types for CfExecute
enum CF_OPERATION_TYPE : uint
{
    TRANSFER_DATA = 0,      // Write downloaded data to placeholder
    RETRIEVE_DATA = 1,      // Read data from placeholder
    ACK_DATA = 2,           // Acknowledge data received
    RESTART_HYDRATION = 3,  // Restart a failed hydration
    DEHYDRATE = 5,          // Remove content, leave placeholder
}
```

**Implementation notes:**
- Use `Microsoft.Windows.CsWin32` source generator for automatic P/Invoke generation if available at implementation time. Otherwise, manual `[DllImport("cfapi.dll")]` declarations.
- All callbacks must be implemented as managed delegates pinned via `GCHandle` to prevent garbage collection while native code holds references.
- The `CF_CALLBACK_REGISTRATION` struct defines which callbacks we register and must remain alive for the lifetime of the sync root connection.

**Deliverables:**

- ☐ `CfApiNative.cs` — all P/Invoke declarations
- ☐ `CfApiTypes.cs` — all structs, enums, flags, and callback delegate types
- ☐ Build succeeds on Windows with `#if WINDOWS_BUILD` conditional compilation

### Step 3.2 — Implement `CloudFilterSyncProvider : IVirtualFileProvider`

**Files:**
- `src/Clients/DotNetCloud.Client.Core/Platform/Windows/CloudFilterSyncProvider.cs`
- `src/Clients/DotNetCloud.Client.Core/Platform/Windows/CloudFilterCallbacks.cs`

`CloudFilterSyncProvider` manages the lifecycle of a Windows Cloud Filter sync root. It implements `IVirtualFileProvider` and delegates callback handling to `CloudFilterCallbacks`.

**`CloudFilterSyncProvider` key methods:**

| Method | Implementation |
|--------|---------------|
| `InitializeAsync` | `CfConnectSyncRoot` (if reconnecting) or `CfRegisterSyncRoot` with callback table |
| `CreatePlaceholdersAsync` | Walk `SyncTreeNodeResponse` tree → build `CF_PLACEHOLDER_CREATE_INFO[]` → `CfCreatePlaceholders` |
| `HydrateFileAsync` | Download chunks via `ChunkedTransferClient` → `CfExecute(CF_OPERATION_TYPE_TRANSFER_DATA)` |
| `DehydrateFileAsync` | `CfExecute(CF_OPERATION_TYPE_DEHYDRATE)` |
| `PinFileAsync` | `CfSetPinState(CF_PIN_STATE_PINNED)` + update `VirtualFileSettings.PinList` |
| `UnpinFileAsync` | `CfSetPinState(CF_PIN_STATE_UNPINNED)` + remove from `VirtualFileSettings.PinList` |
| `IsHydratedAsync` | `CfGetPlaceholderInfo` → check if `CF_PLACEHOLDER_STATE` includes hydrated |
| `ShutdownAsync` | `CfDisconnectSyncRoot` + `CfUnregisterSyncRoot` |

**`CloudFilterCallbacks` — the core on-demand machinery:**

The callbacks are invoked by Windows when it needs data or notifies us of events.

| Callback | Trigger | Action |
|----------|---------|--------|
| `FETCH_DATA` | User/app opens a cloud-only file | Download requested byte range from server → write via `CfExecute(TRANSFER_DATA)` |
| `VALIDATE_DATA` | Windows wants to verify local content | Compare local content hash against server's `ContentHash` |
| `FETCH_PLACEHOLDERS` | Explorer enumerates a directory | Query `LocalStateDb` for children → return placeholder metadata |
| `NOTIFY_FILE_OPEN_COMPLETION` | File opened (read or write) | Update access time for LRU tracking |
| `NOTIFY_FILE_CLOSE_COMPLETION` | File closed | If modified, queue for upload; update hydration state |
| `NOTIFY_DELETE` | User deletes a placeholder | Queue delete operation for server propagation |
| `NOTIFY_RENAME` | User renames a placeholder | Queue rename operation for server propagation |
| `NOTIFY_DEHYDRATE` | Windows wants to free space | Allow dehydration if not pinned |

**Error handling in callbacks:**
- Network errors during `FETCH_DATA`: report failure via `CfExecute(ACK_DATA)` with error → Windows shows "File unavailable" to the user
- Server unreachable: fail gracefully, do not crash the sync root

**Deliverables:**

- ☐ `CloudFilterSyncProvider` implementing `IVirtualFileProvider`
- ☐ `CloudFilterCallbacks` with all callback implementations
- ☐ Sync root registration with Windows shell
- ☐ Placeholder creation from server tree
- ☐ On-demand hydration with streaming chunk download
- ☐ Pin/unpin support
- ☐ Error handling for network/server failures
- ☐ Conditional compilation: `#if WINDOWS_BUILD`

### Step 3.3 — Shell Integration (Icon Overlays + Context Menu)

Windows Cloud Files API provides shell integration automatically through sync root registration flags. No custom shell extension DLL is needed.

**Automatic shell integration from CfApi:**

- ☐ Cloud icon overlay (☁) on cloud-only files — handled by `CF_PLACEHOLDER_CREATE_FLAGS`
- ☐ Green checkmark (✓) on hydrated files — handled by `CF_PLACEHOLDER_STATE`
- ☐ "Status" column in Explorer shows: "Available online-only", "Available offline", "Synced"
- ☐ Right-click context menu: "Always keep on this device" / "Free up space" — handled by `CF_HYDRATION_POLICY`

**Registry keys (set by `CfRegisterSyncRoot`):**
- The sync root provider identity is registered under `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\SyncRootManager\`
- Icon resources are specified in the sync root registration

**Deliverables:**

- ☐ Explorer shows cloud icon overlay on cloud-only files
- ☐ Explorer status column shows "Available online-only" for placeholders
- ☐ Right-click "Always keep on this device" / "Free up space" functional
- ☐ Custom DotNetCloud icon registered (requires `.ico` file in assets)

---

## Phase 4 — Linux Implementation

**Machine:** `mint-dnc-client`  
**Depends on:** Phase 2 (core abstractions)  
**Parallel with:** Phase 3 (Windows)

### Step 4.1 — FUSE Dependency & Project Setup

**Add NuGet package:**

```xml
<!-- In DotNetCloud.Client.Core.csproj, conditioned on Linux -->
<ItemGroup Condition="'$(OS)' == 'Unix' Or '$([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform($([System.Runtime.InteropServices.OSPlatform]::Linux)))' == 'true'">
    <PackageReference Include="Tmds.Fuse" Version="*" />
</ItemGroup>
```

**Dependency check (in installer/startup):**

The tray app should check for `fusermount3` on startup and show a clear error if missing:
```bash
which fusermount3 || echo "Please install fuse3: sudo apt install fuse3"
```

User must be in the `fuse` group:
```bash
groups | grep -q fuse || echo "Please add yourself to the fuse group: sudo usermod -a -G fuse $USER"
```

**Files to modify:**

- ☐ `src/Clients/DotNetCloud.Client.Core/DotNetCloud.Client.Core.csproj` — add Tmds.Fuse (Linux-conditional)
- ☐ `scripts/install.sh` or equivalent — add `fuse3` dependency check

**Deliverables:**

- ☐ Tmds.Fuse package referenced (Linux only)
- ☐ `fuse3` dependency check in installer
- ☐ User-friendly error message if `fuse3` is missing

### Step 4.2 — Implement `FuseSyncFilesystem : IVirtualFileProvider`

**Files:**
- `src/Clients/DotNetCloud.Client.Core/Platform/Linux/FuseSyncFilesystem.cs`
- `src/Clients/DotNetCloud.Client.Core/Platform/Linux/DotNetCloudFuseOperations.cs`

`FuseSyncFilesystem` mounts a FUSE filesystem at the sync folder path. The FUSE filesystem presents the server's file tree as a local directory — metadata comes from `LocalStateDb`, content downloads on `read()`.

**Mount strategy:** Mount FUSE directly at the sync folder path. The sync folder *is* the virtual filesystem — no separate mount point. This matches user expectation: "my sync folder just works."

**`FuseSyncFilesystem` lifecycle:**

| Method | Implementation |
|--------|---------------|
| `InitializeAsync` | Ensure sync folder exists, mount FUSE at that path via `Tmds.Fuse` |
| `CreatePlaceholdersAsync` | Store full tree metadata in `LocalStateDb` (files appear via `getattr`/`readdir`) |
| `HydrateFileAsync` | Download content via `ChunkedTransferClient`, write to cache, update `HydrationState` |
| `DehydrateFileAsync` | Remove cached content chunks, set `HydrationState = CloudOnly` |
| `PinFileAsync` | Set `HydrationState = Pinned`, add to `VirtualFileSettings.PinList` |
| `UnpinFileAsync` | Set `HydrationState = Hydrated`, remove from `VirtualFileSettings.PinList` |
| `IsHydratedAsync` | Check `LocalFileRecord.HydrationState != CloudOnly` |
| `ShutdownAsync` | Unmount FUSE via `fusermount -u` |

**`DotNetCloudFuseOperations` — FUSE callbacks:**

| FUSE Operation | Implementation |
|---------------|---------------|
| `getattr(path)` | Query `LocalStateDb` for file metadata → return `Stat` struct (size, mode, timestamps, uid/gid) |
| `readdir(path)` | Query `LocalStateDb` for children of directory → return `DirectoryEntry[]` |
| `open(path, flags)` | If file is `CloudOnly` and `flags` include `O_RDONLY` or `O_RDWR`, trigger hydration |
| `read(path, offset, size, fileInfo)` | If hydrated, read from local cache; if cloud-only, hydrate first then read |
| `write(path, offset, data, fileInfo)` | Write to local cache, mark file as locally modified → upload on next sync |
| `create(path, mode)` | Create placeholder, queue `PendingOperation` for upload |
| `unlink(path)` | Queue delete for server propagation, remove from `LocalStateDb` |
| `rename(oldPath, newPath)` | Queue rename for server propagation, update `LocalStateDb` |
| `mkdir(path, mode)` | Create directory in `LocalStateDb`, queue create for server |
| `rmdir(path)` | Remove directory from `LocalStateDb`, queue delete for server |
| `truncate(path, size)` | Resize cached content, mark as locally modified |
| `utimens(path, atime, mtime)` | Update timestamps in `LocalStateDb` |

**Thread safety:** FUSE operations are called from multiple kernel threads. All `LocalStateDb` access must be thread-safe (it already is — each operation creates a new `DbContext` instance). File downloads during `read()` must be serialized per file to avoid duplicate downloads.

**Error mapping:**

| Condition | FUSE error |
|-----------|-----------|
| File not in metadata cache | `ENOENT` (No such file) |
| Server unreachable during hydration | `EIO` (I/O error) |
| Permission denied (restricted folder) | `EACCES` |
| Disk full during hydration | `ENOSPC` |
| File is downloading | `EAGAIN` (try again — transient) |

**Deliverables:**

- ☐ `FuseSyncFilesystem` implementing `IVirtualFileProvider`
- ☐ `DotNetCloudFuseOperations` with all FUSE callbacks
- ☐ Mount/unmount lifecycle (with `fusermount -u` on shutdown)
- ☐ On-demand hydration on `read()` syscall
- ☐ Write → upload propagation via `SyncEngine`
- ☐ Error mapping to POSIX error codes
- ☐ Conditional compilation: `#if !WINDOWS_BUILD` (Linux path)

### Step 4.3 — Local Content Cache with LRU Eviction

**File:** `src/Clients/DotNetCloud.Client.Core/VirtualFiles/LruCacheManager.cs`

Since FUSE `read()` is called synchronously by the kernel, content must be served from a local cache. The cache stores downloaded chunks (same SHA-256 content-addressed scheme as `ChunkedTransferClient`) and evicts least-recently-used chunks when the total cache size exceeds `VirtualFileSettings.MaxCacheSizeBytes`.

**Cache directory:** `~/.local/share/dotnetcloud/cache/`

**Cache structure:**
```
~/.local/share/dotnetcloud/cache/
├── chunks/
│   ├── a1/
│   │   └── a1b2c3d4... (SHA-256 hash as filename, first two chars as subdirectory)
│   └── ...
├── lru.json          # Access-time tracking (serialized on shutdown, loaded on startup)
└── cache_size.txt    # Current total size in bytes (fast lookup without directory scan)
```

**Key methods:**

| Method | Purpose |
|--------|---------|
| `GetChunkAsync(hash)` | Return cached chunk stream, update access time. Return `null` if not cached. |
| `PutChunkAsync(hash, stream)` | Store chunk in cache, update total size. Trigger eviction if over limit. |
| `EvictAsync(targetBytes)` | Evict least-recently-used non-pinned chunks until cache ≤ `targetBytes`. |
| `RemoveFileAsync(nodeId)` | Remove all chunks belonging to a file (on dehydration). |

**Eviction policy:**
1. Collect all cached chunks with their last-access times
2. Exclude chunks belonging to pinned files
3. Sort by ascending last-access time
4. Evict oldest chunks until cache size ≤ `MaxCacheSizeBytes`
5. Run eviction in background (do not block `read()`)

**Default `MaxCacheSizeBytes`:** 10% of free disk space on the cache partition, clamped between 1 GB and 100 GB.

**Deliverables:**

- ☐ Content-addressed chunk cache in `~/.local/share/dotnetcloud/cache/`
- ☐ LRU eviction when cache exceeds `MaxCacheSizeBytes`
- ☐ Pinned-file chunks exempt from eviction
- ☐ Cache persistence across restarts
- ☐ Default `MaxCacheSizeBytes` from free disk space

### Step 4.4 — Installer Integration

**File:** `scripts/install.sh` (or equivalent install script)

Add pre-flight checks:
```bash
# Check for fuse3
if ! command -v fusermount3 &> /dev/null; then
    echo "⚠ fuse3 is required for Files On-Demand. Install with:"
    echo "  sudo apt install fuse3        # Debian/Ubuntu"
    echo "  sudo dnf install fuse3        # Fedora"
fi

# Check user is in fuse group
if ! groups | grep -q '\bfuse\b'; then
    echo "⚠ Add your user to the fuse group:"
    echo "  sudo usermod -a -G fuse $USER"
    echo "  (log out and back in for this to take effect)"
fi
```

**Deliverables:**

- ☐ `fuse3` availability check in installer
- ☐ `fuse` group membership check
- ☐ Clear instructions for user if dependencies missing

---

## Phase 5 — SyncTray UI Integration

**Machine:** `Windows11-TestDNC` (primary), `mint-dnc-client` (verification)  
**Depends on:** Phase 3 + Phase 4 (platform providers ready)

### Step 5.1 — Add "Storage Mode" Setting to SettingsViewModel

**Files:**
- `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/SettingsViewModel.cs`
- `src/Clients/DotNetCloud.Client.SyncTray/Views/SettingsWindow.axaml`

**New properties on `SettingsViewModel`:**

```csharp
private VirtualFileStorageMode _storageMode = VirtualFileStorageMode.DownloadAll;
private long _maxCacheSizeMb;

public VirtualFileStorageMode StorageMode
{
    get => _storageMode;
    set
    {
        if (SetProperty(ref _storageMode, value))
            _ = PersistVirtualFileSettingsAsync();
    }
}

public long MaxCacheSizeMb
{
    get => _maxCacheSizeMb;
    set
    {
        if (SetProperty(ref _maxCacheSizeMb, value))
            _ = PersistVirtualFileSettingsAsync();
    }
}
```

**UI elements in SettingsWindow.axaml:**

- "Storage mode" section in the General or Sync tab:
  - Radio buttons: "Download all files" / "Files on-demand"
  - Help text explaining the difference
  - Warning when switching from On-Demand → Download All: "This will download all files. Continue?"
  - Warning when switching from Download All → On-Demand: "Unpinned files will become online-only. Continue?"
- "Cache size limit" slider/numeric input (Linux only, or shown on both with note that Windows manages cache automatically)
- "Pinned files" list (read-only, managed via right-click in file manager)

**Deliverables:**

- ☐ `StorageMode` property with persistence
- ☐ `MaxCacheSizeMb` property with persistence
- ☐ Radio buttons for storage mode in Settings UI
- ☐ Confirmation dialogs for mode switches
- ☐ Cache size setting (Linux-focused)
- ☐ Pinned files display

### Step 5.2 — Wire VFS Lifecycle in App.axaml.cs

**File:** `src/Clients/DotNetCloud.Client.SyncTray/App.axaml.cs`

**Startup changes:**

```csharp
// After loading contexts and before starting sync engines:
if (_virtualFileSettings.StorageMode == VirtualFileStorageMode.FilesOnDemand)
{
    foreach (var context in contexts)
    {
        await _virtualFileProvider.InitializeAsync(context, ct);
    }
}

// Use VirtualFileSyncEngine instead of plain SyncEngine:
// VirtualFileSyncEngine wraps SyncEngine internally when StorageMode == FilesOnDemand
```

**Shutdown changes:**
```csharp
// Before disposing other services:
await _virtualFileProvider.ShutdownAsync(ct);
```

**DI wiring (in `BuildServices`):**
```csharp
services.AddSingleton<VirtualFileSyncEngine>();
// VirtualFileSyncEngine replaces direct ISyncEngine usage when VFS is active
```

**Deliverables:**

- ☐ `IVirtualFileProvider.InitializeAsync()` called on startup when `FilesOnDemand`
- ☐ `IVirtualFileProvider.ShutdownAsync()` called on graceful shutdown
- ☐ `VirtualFileSyncEngine` used as the active sync engine when VFS enabled
- ☐ POSIX signal handlers (`SIGTERM`/`SIGINT`) trigger VFS shutdown

### Step 5.3 — VFS Status in TrayViewModel

**File:** `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/TrayViewModel.cs`

Add properties for VFS status display:

```csharp
private int _cloudOnlyFileCount;
private int _hydratedFileCount;
private long _cacheSizeBytes;
private bool _isHydrating;
private string? _hydrationFileName;

public int CloudOnlyFileCount { get => _cloudOnlyFileCount; set => SetProperty(ref _cloudOnlyFileCount, value); }
public int HydratedFileCount { get => _hydratedFileCount; set => SetProperty(ref _hydratedFileCount, value); }
public long CacheSizeBytes { get => _cacheSizeBytes; set => SetProperty(ref _cacheSizeBytes, value); }
public bool IsHydrating { get => _isHydrating; set => SetProperty(ref _isHydrating, value); }
public string? HydrationFileName { get => _hydrationFileName; set => SetProperty(ref _hydrationFileName, value); }
```

**Tray tooltip integration:**
- When VFS is active, show "☁ 1,234 files online | ✓ 56 files local"
- During hydration: "⬇ Downloading vacation-photos.zip..."
- Update counts periodically (every 30 seconds) by querying `LocalStateDb`

**Deliverables:**

- ☐ VFS status properties on `TrayViewModel`
- ☐ Tray tooltip shows cloud-only vs hydrated file counts
- ☐ Transient hydration progress in tray tooltip
- ☐ Periodic refresh of file counts

### Step 5.4 — Optional: "Virtual Files" Tab in Settings

**Stretch goal** — a dedicated "Virtual Files" tab in the Settings window showing:

- Cache usage bar (used / max)
- List of recently hydrated files
- Buttons: "Download all files now" / "Free up space now"
- "Clear cache" button

**Deliverables (optional):**

- ☐ Virtual Files tab in Settings window
- ☐ Cache usage visualization
- ☐ Manual "download all" / "free up space" triggers

---

## Phase 6 — Testing & Validation

**Machines:** All (`mint22`, `Windows11-TestDNC`, `mint-dnc-client`)  
**Depends on:** Phase 5 (UI integration)

### Step 6.1 — Unit Tests

**Location:** `tests/DotNetCloud.Client.Core.Tests/VirtualFiles/`

**Test files to create:**

- ☐ `VirtualFileSyncEngineTests.cs` — mode switching, placeholder creation, hydration dispatch
- ☐ `VirtualFileSettingsTests.cs` — serialization, pin list management
- ☐ `LruCacheManagerTests.cs` — cache put/get, eviction, pin exemption
- ☐ `CloudFilterSyncProviderTests.cs` — with mocked CfApi (unit-testable logic only)
- ☐ `FuseSyncFilesystemTests.cs` — with mocked FUSE layer (unit-testable logic only)

**Key test scenarios:**

| Test | Description |
|------|-------------|
| `SyncAsync_FilesOnDemand_CreatesPlaceholdersNotDownloads` | When mode is OnDemand, sync pass creates placeholders instead of downloading |
| `SyncAsync_DownloadAll_DownloadsAllContent` | When mode is DownloadAll, behavior unchanged from current |
| `ModeSwitch_DownloadAllToOnDemand_DehydratesUnpinned` | Switching modes dehydrates files not in pin list |
| `ModeSwitch_OnDemandToDownloadAll_HydratesAll` | Switching modes downloads all files |
| `HydrateFile_CloudOnly_DownloadsContent` | Hydration downloads content via ChunkedTransferClient |
| `HydrateFile_AlreadyHydrated_SkipsDownload` | No redundant download for hydrated files |
| `DehydrateFile_Pinned_ThrowsOrNoOps` | Pinned files cannot be dehydrated |
| `LruCache_EvictsOldestFirst` | Eviction removes least-recently-used chunks |
| `LruCache_PinnedExempt` | Pinned file chunks survive eviction |

**Deliverables:**

- ☐ 15+ unit tests covering VirtualFileSyncEngine, LruCacheManager, settings
- ☐ All tests pass: `dotnet test tests/DotNetCloud.Client.Core.Tests/`

### Step 6.2 — Windows Integration Tests

**Environment-gated:** Requires Windows 10 1709+ (Build 16299). Must run on `Windows11-TestDNC`.

**Manual test scenarios:**

- ☐ **TC-VFS-W1:** Register sync root → Explorer shows sync folder with DotNetCloud branding
- ☐ **TC-VFS-W2:** Initial sync with OnDemand → folder populated with cloud-only placeholders
- ☐ **TC-VFS-W3:** Open a cloud-only text file → content downloads, file opens normally
- ☐ **TC-VFS-W4:** Open a large file (>100 MB) → streaming hydration, file opens before full download
- ☐ **TC-VFS-W5:** Edit and save a hydrated file → uploads to server on next sync pass
- ☐ **TC-VFS-W6:** Right-click "Free up space" → file returns to cloud-only placeholder
- ☐ **TC-VFS-W7:** Right-click "Always keep on this device" → pin state persists across restarts
- ☐ **TC-VFS-W8:** Server-side file update → placeholder metadata updates on next sync
- ☐ **TC-VFS-W9:** Server-side file delete → placeholder removed locally
- ☐ **TC-VFS-W10:** Mode switch DownloadAll → OnDemand → DownloadAll (round-trip)
- ☐ **TC-VFS-W11:** Offline mode — open cloud-only file without server → graceful error

**Deliverables:**

- ☐ All Windows integration tests documented and passed

### Step 6.3 — Linux Integration Tests

**Environment-gated:** Requires Linux with `fuse3`. Must run on `mint-dnc-client`.

**Manual test scenarios:**

- ☐ **TC-VFS-L1:** Mount FUSE at sync folder → `ls` shows server file tree
- ☐ **TC-VFS-L2:** `cat cloud-only-file.txt` → file downloads and displays correctly
- ☐ **TC-VFS-L3:** `echo "new" > newfile.txt` → file created, uploads to server
- ☐ **TC-VFS-L4:** `rm existing-file.txt` → file deleted locally and on server
- ☐ **TC-VFS-L5:** `mv oldname.txt newname.txt` → rename propagates to server
- ☐ **TC-VFS-L6:** Large file read → streaming hydration, file content correct
- ☐ **TC-VFS-L7:** Cache eviction → oldest cached chunks removed when over limit
- ☐ **TC-VFS-L8:** Pinned file survives eviction
- ☐ **TC-VFS-L9:** Unmount on shutdown → `fusermount -u` succeeds, directory returns to normal
- ☐ **TC-VFS-L10:** Restart with existing cache → cache loads, counts correct
- ☐ **TC-VFS-L11:** `fuse3` missing → clear error message, app continues without VFS

**Deliverables:**

- ☐ All Linux integration tests documented and passed

### Step 6.4 — Cross-Machine End-to-End Tests

All three machines participate. Coordination via `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md`.

**Test scenarios:**

- ☐ **TC-VFS-E2E-1:** Windows creates file → server receives → Linux sees placeholder → Linux opens → downloads correctly
- ☐ **TC-VFS-E2E-2:** Linux creates file → server receives → Windows sees placeholder → Windows opens → downloads correctly
- ☐ **TC-VFS-E2E-3:** Conflict: both clients edit same file offline → conflict resolution on sync
- ☐ **TC-VFS-E2E-4:** Large binary file (500 MB) → streaming hydration on both platforms → content integrity verified (SHA-256)

**Deliverables:**

- ☐ All E2E tests documented and passed

### Step 6.5 — Build Validation

- ☐ `dotnet build` succeeds on Windows (`Windows11-TestDNC`)
- ☐ `dotnet build` succeeds on Linux (`mint-dnc-client`)
- ☐ `dotnet test` — all tests pass on both platforms
- ☐ `dotnet build -c Release` — Release build succeeds on both platforms
- ☐ No new warnings introduced

---

## File Manifest

### New Files (16 total)

**Client.Core — VirtualFiles namespace (`src/Clients/DotNetCloud.Client.Core/VirtualFiles/`):**

| File | Purpose |
|------|---------|
| `IVirtualFileProvider.cs` | Platform-agnostic VFS interface |
| `VirtualFileSyncEngine.cs` | Wraps SyncEngine, delegates VFS ops to IVirtualFileProvider |
| `VirtualFileSettings.cs` | Storage mode, cache size, pin list |
| `LruCacheManager.cs` | LRU content chunk cache for Linux |

**Client.Core — Windows platform (`src/Clients/DotNetCloud.Client.Core/Platform/Windows/`):**

| File | Purpose |
|------|---------|
| `CfApi/CfApiNative.cs` | P/Invoke declarations for `cfapi.dll` |
| `CfApi/CfApiTypes.cs` | CF_* structs, enums, flags, callback delegates |
| `CloudFilterSyncProvider.cs` | `IVirtualFileProvider` implementation for Windows |
| `CloudFilterCallbacks.cs` | CF callback implementations (FETCH_DATA, VALIDATE_DATA, etc.) |

**Client.Core — Linux platform (`src/Clients/DotNetCloud.Client.Core/Platform/Linux/`):**

| File | Purpose |
|------|---------|
| `FuseSyncFilesystem.cs` | `IVirtualFileProvider` implementation for Linux (mount/umount) |
| `DotNetCloudFuseOperations.cs` | FUSE operation implementations (getattr, readdir, read, write, etc.) |

**Client.Core — Stub (`src/Clients/DotNetCloud.Client.Core/Platform/`):**

| File | Purpose |
|------|---------|
| `NoOpVirtualFileProvider.cs` | Throws `PlatformNotSupportedException` — for macOS/unsupported |

**Tests (`tests/DotNetCloud.Client.Core.Tests/VirtualFiles/`):**

| File | Purpose |
|------|---------|
| `VirtualFileSyncEngineTests.cs` | Mode switching, hydration, dehydration logic |
| `VirtualFileSettingsTests.cs` | Settings serialization, pin list |
| `LruCacheManagerTests.cs` | Cache eviction, pin exemption |
| `CloudFilterSyncProviderTests.cs` | Mock-backed unit tests for Windows provider logic |
| `FuseSyncFilesystemTests.cs` | Mock-backed unit tests for Linux provider logic |

### Modified Files (10 total)

**Client.Core:**

| File | Change |
|------|--------|
| `LocalState/Entities/LocalFileRecord.cs` | Add `HydrationState` property |
| `LocalState/LocalStateDb.cs` | Add `HydrationState` column to schema evolution |
| `ClientCoreServiceExtensions.cs` | Register `IVirtualFileProvider`, `VirtualFileSettings`, `VirtualFileSyncEngine`, `LruCacheManager` |
| `DotNetCloud.Client.Core.csproj` | Add `Tmds.Fuse` package reference (Linux-conditional) |

**SyncTray:**

| File | Change |
|------|--------|
| `App.axaml.cs` | Wire VFS lifecycle (init on start, shutdown on exit), use `VirtualFileSyncEngine` |
| `ViewModels/SettingsViewModel.cs` | Add `StorageMode`, `MaxCacheSizeMb` properties, mode-switch commands |
| `Views/SettingsWindow.axaml` | Storage mode radio buttons, cache size slider, pinned files list |
| `ViewModels/TrayViewModel.cs` | VFS status properties (cloud-only count, hydration progress) |

**Server (handoff to `mint22`):**

| File | Change |
|------|--------|
| `FilesController` or chunk-serving endpoint | Verify/add `Range` header support for `206 Partial Content` |
| Sync tree endpoint | Optionally add `?metadataOnly=true` to skip content hashes |

**Scripts:**

| File | Change |
|------|--------|
| `scripts/install.sh` | Add `fuse3` dependency check and user group instructions |

---

## Handoff Sequence

Phases are ordered by dependency. Phases 3 and 4 are independent and can run in parallel.

```
Phase 1 (server: mint22)
  │
  │  Handoff: "Phase 1 complete. Pull main, verify Range headers."
  │
  ▼
Phase 2 (core abstractions: Windows11-TestDNC)
  │
  ├─────────────────────────────────────────┐
  │                                         │
  ▼                                         ▼
Phase 3 (Windows: Windows11-TestDNC)       Phase 4 (Linux: mint-dnc-client)
  │                                         │
  │  Handoff: "Phase 3 complete."           │  Handoff: "Phase 4 complete."
  │                                         │
  └─────────────────────────────────────────┘
                    │
                    ▼
          Phase 5 (UI: Windows11-TestDNC)
                    │
                    ▼
          Phase 6 (testing: all machines)
```

**Handoff trigger format** (per `CLIENT_SERVER_MEDIATION_HANDOFF.md`):

```
<commit-hash> — New handoff update for <target-machine>.
Pull and check docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md Active Handoff.
```

---

## Verification Checklist

### Server (Phase 1)

- ☐ `curl -H "Range: bytes=0-1023" https://mint22:5443/api/v1/files/{nodeId}/chunks/{hash}` returns `206 Partial Content`
- ☐ `Accept-Ranges: bytes` header present on chunk endpoint
- ☐ Full download (no Range) still returns `200 OK`

### Windows (Phase 3)

- ☐ Explorer shows sync folder with DotNetCloud branding
- ☐ Cloud-only files show cloud icon (☁) overlay
- ☐ Opening a cloud-only file triggers download and file opens correctly
- ☐ Right-click "Always keep on this device" pins file
- ☐ Right-click "Free up space" dehydrates file back to placeholder
- ☐ Explorer "Status" column shows correct state for each file

### Linux (Phase 4)

- ☐ `ls -la ~/synctray/` lists all server files
- ☐ `cat ~/synctray/file.txt` triggers download and displays content
- ☐ Creating/deleting/renaming files propagates to server
- ☐ Cache eviction works when over size limit
- ☐ `fusermount -u` unmounts cleanly on shutdown
- ☐ App shows clear error if `fuse3` is missing

### UI (Phase 5)

- ☐ Settings → Storage mode toggle switches between DownloadAll and FilesOnDemand
- ☐ Mode switch shows confirmation dialog
- ☐ Tray tooltip shows "☁ N files online" when VFS active
- ☐ Tray tooltip shows "⬇ Downloading filename..." during hydration

### Build & Test (Phase 6)

- ☐ `dotnet build` — succeeds on Windows and Linux
- ☐ `dotnet build -c Release` — succeeds on both platforms
- ☐ `dotnet test` — all tests pass, no regressions
- ☐ No new build warnings

---

## Decisions & Rationale

| Decision | Rationale |
|----------|-----------|
| **FUSE library: `Tmds.Fuse` first** | Pure .NET bindings, no native compilation. If insufficient for production, fall back to `libfuse3` P/Invoke. `IVirtualFileProvider` insulates from this choice. |
| **Mount FUSE at sync folder path** | Matches user expectation — "my sync folder just works." No separate virtual mount point to explain. |
| **`StorageMode` defaults to `DownloadAll`** | Preserves backward compatibility. Existing users see no change. VFS is opt-in. |
| **`HydrationState` defaults to `Hydrated`** | Backward compatible — existing records in `LocalStateDb` have content downloaded. |
| **LRU cache: 10% of free disk (1 GB–100 GB range)** | Conservative default that scales with disk size. User-adjustable. |
| **`VirtualFileSyncEngine` wraps `SyncEngine`** | Does not duplicate sync logic. All existing sync features (cursor, conflict resolution, chunked transfer) are reused. |
| **Windows CF API via manual P/Invoke** | No mature community NuGet package for Cloud Filter API. `Microsoft.Windows.CsWin32` can auto-generate if available. |
| **Callback delegates pinned via `GCHandle`** | Native CF API holds function pointers indefinitely. Managed delegates must not be garbage collected while sync root is connected. |
| **Pinned files exempt from eviction and dehydration** | User explicitly requested "always keep on this device." This is the contract. |
| **Network errors during `read()` return `EIO`** | Standard POSIX behavior for I/O errors. Applications handle this gracefully (show error dialog, not crash). |
| **No macOS in initial scope** | `NoOpVirtualFileProvider` stub throws `PlatformNotSupportedException`. macOS File Provider Extension is a separate, sandboxed app extension that requires a dedicated contributor. |

---

## Further Considerations (Future)

1. **Prefetch heuristics:** When a user opens a directory, batch-download small files in that directory to reduce round-trip latency. Requires telemetry on access patterns first.

2. **Thumbnail/preview support (Windows):** `CF_CALLBACK_TYPE_FETCH_PLACEHOLDERS` can return thumbnail data for rich Explorer preview. Requires server-side thumbnail generation.

3. **Offline mode hardening:** If the server is unreachable, cloud-only files should show as "unavailable" without crashing the sync root or FUSE mount. Graceful degradation.

4. **macOS File Provider Extension:** Apple's equivalent to Cloud Filter API. Requires a sandboxed app extension target. Architecture maps directly — same server APIs, same `IVirtualFileProvider` pattern.

5. **Progressive hydration (Windows):** `CF_HYDRATION_POLICY_PROGRESSIVE` allows streaming hydration where the file is usable before full download. Useful for media files. Requires `Range`-aware chunk download (Phase 1).

6. **Cache warming:** Pre-download frequently accessed files during idle time based on usage heuristics.

7. **Per-folder storage mode:** Allow individual folders to be "Download all" while the rest is "Files on-demand." Builds on existing `ISelectiveSyncConfig`.
