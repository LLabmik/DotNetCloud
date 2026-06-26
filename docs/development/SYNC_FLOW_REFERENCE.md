# Sync Flow Reference

> Complete reference for the DotNetCloud bidirectional sync architecture — all sync cases, mechanisms, and data flows.

## Architecture Overview

```
┌──────────────────────┐     ┌───────────────────────────────────┐     ┌──────────────────────┐
│    Client A          │     │           Server                  │     │    Client B          │
│  (SyncTray Engine)   │     │  (Files Module)                   │     │  (SyncTray Engine)   │
│                      │     │                                   │     │                      │
│  FileSystemWatcher   │     │  REST API + SSE                  │     │  FileSystemWatcher   │
│  LocalStateDb (SQLite)│    │  FileNode (soft-delete)           │     │  LocalStateDb (SQLite)│
│  Periodic Full Scan  │     │  UserSyncCounter (sequences)      │     │  Periodic Full Scan  │
│  SyncStreamListener  │     │  SyncDeviceCursor (per-device)    │     │  SyncStreamListener  │
│  ConflictResolver    │     │  TrashCleanupService (purge)      │     │  ConflictResolver    │
└──────────────────────┘     └───────────────────────────────────┘     └──────────────────────┘
```

### Sync Protocol

Sync is **REST-only** (no gRPC). The protocol uses cursor-based delta sync for the fast path and full tree reconciliation as a safety net.

| Endpoint                                                   | Method | Purpose                                           |
| ---------------------------------------------------------- | ------ | ------------------------------------------------- |
| `GET /api/v1/files/sync/changes?cursor={cursor}&limit=500` | GET    | Paginated cursor-based delta sync                 |
| `GET /api/v1/files/sync/tree`                              | GET    | Full folder tree snapshot with content hashes     |
| `POST /api/v1/files/sync/reconcile`                        | POST   | Client sends local state → server returns actions |
| `GET /api/v1/files/sync/stream`                            | GET    | Server-Sent Events push notifications             |
| `POST /api/v1/files/sync/ack`                              | POST   | Acknowledge processed sequence (device cursor)    |
| `GET /api/v1/files/sync/device-cursor`                     | GET    | Recovery: get server-side cursor for a device     |

### Core Concepts

| Concept                 | Description                                                                                                                                           |
| ----------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| **SyncSequence**        | Monotonic gap-free integer assigned atomically on every file mutation. Stored in `UserSyncCounter.CurrentSequence`. Used for cursor-based delta sync. |
| **SyncCursor**          | Base64-encoded `"{userId}:{sequence}"` string. Allows clients to paginate through changes since a point in time.                                      |
| **Device Cursor**       | Server-side per-device cursor (`SyncDeviceCursor`). Allows cursor recovery after local DB loss. Updated via `POST /sync/ack`.                         |
| **Echo Suppression**    | Changes carry `OriginatingDeviceId`. Clients skip changes from their own device to avoid re-downloading self-uploaded files.                          |
| **Soft-Delete**         | `FileNode.IsDeleted=true` + `DeletedAt` + `SyncSequence`. Deletions appear in the changes feed. `TrashCleanupService` hard-purges after 30 days.      |
| **Tree Reconciliation** | Safety net after changes feed is exhausted. Client fetches full server tree and cross-checks every local tracked record.                              |

---

## Sync Engine Flow (Single Sync Pass)

```
SyncAsync()
  │
  ├── 1. ApplyRemoteChangesAsync()
  │      ├── a. Recover/server cursor (no local cursor)
  │      ├── b. GET /sync/tree → full server tree
  │      ├── c. Paginated loop: GET /sync/changes?cursor=X
  │      │      ├── Server-deleted → HandleRemoteDeletionAsync()
  │      │      ├── Server-modified → HandleRemoteUpdateAsync()
  │      │      ├── New server file → queue PendingDownload
  │      │      └── Persist cursor after each page
  │      └── d. ReconcileServerTreeAsync()
  │             ├── Walk full tree → queue missing downloads
  │             └── Reverse reconciliation → detect remote deletions
  │
  ├── 2. ScanLocalDirectoryAsync()
  │      ├── Walk local filesystem
  │      ├── New/modified files → queue PendingUpload
  │      ├── Deleted tracked files → queue PendingDelete
  │      └── Stale empty directory cleanup
  │
  ├── 3. ApplyLocalChangesAsync()
  │      ├── Execute PendingUploads (chunked, retry up to 10x)
  │      ├── Execute PendingDownloads (chunked, resume-capable)
  │      ├── Execute PendingDeletes (DELETE /api/v1/files/{nodeId})
  │      └── Parallel with MaxDegreeOfParallelism=3
  │
  ├── 4. AcknowledgeCursorToServerAsync()
  │      └── POST /sync/ack with last processed sequence
  │
  └── 5. Checkpoint SQLite WAL
```

---

## Case-by-Case Analysis

### Case 1 — Client A creates file → syncs to Client B

```
Client A                   Server                     Client B
   │                         │                          │
   │──[FSW Created]─────────→│                          │
   │──[ScanLocalDirectory]──→│                          │
   │   Queues PendingUpload  │                          │
   │                         │                          │
   │──[ApplyLocalChanges]───→│                          │
   │   Chunked upload        │                          │
   │   POST /api/v1/files/   │                          │
   │                         │──[Assign SyncSequence]───│
   │                         │   Soft: create FileNode  │
   │                         │   Increment UserCounter  │
   │                         │                          │
   │                         │──[SSE: sync-changed]────→│
   │                         │                          │──[GET /sync/changes]
   │                         │                          │   Filter: echo suppression
   │                         │                          │
   │                         │                          │──[HandleRemoteUpdateAsync]
   │                         │                          │   → queue PendingDownload
   │                         │                          │
   │                         │                          │──[Download chunks]
   │                         │                          │
   │                         │                          │ ✓ test1.txt on Client B
```

**Mechanisms:** FSW → Upload → SyncSequence → SSE → Changes feed → Download  
**Status:** ✅ Working

---

### Case 2 — Client B edits existing file → syncs to Client A

```
Client A                   Server                     Client B
   │                         │                          │
   │                         │                          │──[FSW changed]
   │                         │                          │──[ScanLocalDirectory]
   │                         │                          │   mtime changed → upload
   │                         │                          │
   │                         │←──[Chunked upload]───────│
   │                         │   (same NodeId, update)  │
   │                         │                          │
   │                         │──[Assign SyncSequence]───│
   │                         │   Update FileNode        │
   │                         │   Increment UserCounter  │
   │                         │                          │
   │                         │──[SSE: sync-changed]────→│──[Wait... echo suppression]
   │                         │                          │   OriginatingDeviceId == self
   │                         │                          │   → Skip (no re-download)
   │                         │                          │
   │──[SSE: sync-changed]───→│                          │
   │                         │                          │
   │──[GET /sync/changes]────→│                         │
   │   OriginatingDeviceId != │                         │
   │   this device → download │                         │
   │                         │                          │
   │──[HandleRemoteUpdateAsync]                         │
   │   → queue PendingDownload                          │
   │   → download updated content                       │
   │                         │                          │
   │ ✓ test1.txt updated     │                          │
```

**Mechanisms:** FSW → Upload → SyncSequence → SSE → Changes feed → Echo suppression → Download  
**Echo suppression detail:** Client B's own changes are filtered out (`OriginatingDeviceId == DeviceId`). Only Client A downloads.

**Status:** ✅ Working

---

### Case 3 — Client A edits file → syncs to Client B

Symmetric to Case 2. Roles reversed. Identical mechanisms.

**Status:** ✅ Working

---

### Case 4 — Client B deletes file → syncs to Client A

```
Client A                   Server                     Client B
   │                         │                          │
   │                         │                          │──[FSW deleted]
   │                         │                          │──[ScanLocalDirectory]
   │                         │                          │   Tracked file missing
   │                         │                          │   → queue PendingDelete
   │                         │                          │
   │                         │←──[DEL /files/{nodeId}]──│
   │                         │                          │
   │                         │──[Soft-delete FileNode]──│
   │                         │   IsDeleted = true       │
   │                         │   DeletedAt = now        │
   │                         │   SyncSequence assigned  │
   │                         │   Increment UserCounter  │
   │                         │                          │
   │                         │──[SSE: sync-changed]────→│
   │                         │                          │
   │──[SSE: sync-changed]───→│                          │
   │                         │                          │
   │──[GET /sync/changes]────→│                         │
   │   change.IsDeleted=true  │                         │
   │                         │                          │
   │──[HandleRemoteDeletionAsync]                       │
   │   → File.Exists? Check mtime vs record             │
   │   → Not modified → File.Delete + clean up record   │
   │                         │                          │
   │ ✓ test1.txt deleted     │                          │
```

**Conflict scenario:** If Client A modified the file locally AFTER the server deletion, `HandleRemoteDeletionAsync` detects `IsLocallyModified` → keeps the local file and queues a PendingUpload (local wins).

**Status:** ✅ Working

---

### Case 5 — Client A creates test2.txt

Identical to Case 1 with a different filename. Same mechanisms.

**Status:** ✅ Working

---

### Case 6 — Server UI deletes file → both clients sync

```
Server UI                  Server                     Client A & B
   │                         │                          │
   │──[UI Delete]───────────→│                          │
   │                         │──[Soft-delete FileNode]──│
   │                         │   IsDeleted = true       │
   │                         │   SyncSequence assigned  │
   │                         │                          │
   │                         │──[SSE: sync-changed]────→│──[to both connected clients]
   │                         │                          │
   │                         │                          │──[GET /sync/changes]
   │                         │                          │   change.IsDeleted=true
   │                         │                          │   → HandleRemoteDeletionAsync
   │                         │                          │   → delete local file
   │                         │                          │
   │                         │                          │ ✓ test2.txt deleted on both
```

**Mechanisms:** Soft-delete → SyncSequence → SSE (all connected clients) → Changes feed → `HandleRemoteDeletionAsync`  
**Key:** Both clients get the SSE notification (if connected). If a client was offline, they get the deletion when they reconnect and poll changes.

**Status:** ✅ Working

---

### Case 7 — Client reconnects after 30+ days offline

This is the most complex case. The system uses **three layers** of protection.

#### Scenario

- Client A was online 30+ days ago. Had files synced.
- While offline: server creates files, deletes files, modifies files
- `TrashCleanupService` purges soft-deleted `FileNode` rows older than 30 days
- Client A reconnects

#### Layer 1 — Changes Feed (Fast Path)

```
Client A                   Server
   │                         │
   │──[GET /sync/changes?cursor=<old cursor>]
   │                         │
   │──[Changes within retention window]
   │   • Files created while offline → Download
   │   • Files modified while offline → Download
   │   • Files deleted within 30 days → Delete locally
   │                         │
   │   ── But deletions older than 30 days ──
   │   have been purged by TrashCleanupService
```

The changes feed catches everything within the 30-day soft-delete retention window. Deletions older than 30 days are no longer in the DB.

#### Layer 2 — Tree Reconciliation (Safety Net)

```
   │                         │
   │──[GET /sync/tree]──────→│
   │   Returns current tree  │
   │                         │
   │──[Walk all tracked local records]
   │   For each record:
   │     Is NodeId in server tree?
   │     ├── YES → skip (file still exists)
   │     └── NO  → file was deleted on server
   │             → Check local content:
   │                ├── Unmodified → Delete locally ✓
   │                └── Modified  → Keep, re-upload as new ✓
```

The tree reconciliation catches **everything** the changes feed missed — including deletions purged by `TrashCleanupService`.

#### Layer 3 — Reverse Reconciliation (Edge Cases)

```csharp
// Walk all tracked records, check NodeId against server tree
// Catches:
// - Files whose deletion was between paginated cursor pages
// - Files whose cursor entry was suppressed by echo filter
// - Race conditions between changes feed and tree state
```

#### Summary for Case 7

| Mechanism                  | What it catches                               |
| -------------------------- | --------------------------------------------- |
| **Changes feed**           | All changes within 30-day retention window    |
| **Tree reconciliation**    | Deletions older than 30 days (purged from DB) |
| **Reverse reconciliation** | Edge cases missed by cursor-based pagination  |

**Result:** All deletions are properly propagated regardless of how long the client was offline. Modified local files are preserved and re-uploaded.

**Key insight:** A client coming back after 30 days WILL see its own local files that were deleted on the server get deleted locally (unmodified) or re-uploaded (modified). It will NOT upload orphaned files that were never tracked — those are genuine new local files.

**Status:** ✅ Working (via combined changes feed + tree reconciliation)

---

### Case 8 — Rename/move sync between clients

#### Current Behavior

Server API supports rename (`PUT /api/v1/files/{nodeId}/rename`) and move (`PUT /api/v1/files/{nodeId}/move`). Client FSW detects `Renamed` event and triggers a sync pass, but the sync pass treats it as delete+create:

- File at old path missing → PendingDelete
- File at new path appears → PendingUpload
- Other client sees a delete + create of a new file (different NodeId)

#### Desired Behavior

When Client A renames a file:

1. Client detects rename locally → calls server `RenameAsync` (updates FileNode name)
2. Server assigns SyncSequence → SSE push to Client B
3. Client B gets the change → detects NodeId is same but name/path differs → renames local file

**Status:** ⚠️ Needs implementation (Phase 2 of plan)

---

### Case 9 — True concurrent edits (conflict)

```
Client A           Server          Client B
   │                 │                 │
   │──[Edit offline]─│──[Edit offline]─│
   │                 │                 │
   │──[Upload]──────→│←──[Upload]─────│
   │                 │                 │
   │                 │──[Conflict!]───│
   │                 │  Same NodeId,  │
   │                 │  different hash│
   │                 │  same timestamp│
   │                 │                 │
   │←[Conflict copy]─│──[Conflict]────→│
```

**Resolution pipeline** (5 strategies, tried in order):

1. **Identical content** — hashes match → do nothing
2. **Fast-forward** — one side unchanged (matches base hash) → other side wins
3. **Non-overlapping text merge** — DiffPlex three-way diff for text files
4. **Newer-wins** — timestamps > threshold apart → newer version wins
5. **Conflict copy** — rename local file with `(conflict - user - date)` suffix

**Status:** ✅ Working. UX enhancement planned (Phase 3).

---

### Case 10 — Delete + recreate (same name, different file)

```
Client A                      Server
   │                            │
   │──[Delete test.txt]────────→│── Soft-delete NodeId=X
   │                            │
   │──[Create new test.txt]────→│── Creates NodeId=Y (new)
   │                            │
   │ Tree reconciliation:       │
   │ NodeId=X not in tree       │
   │ → other clients delete     │
   │ NodeId=Y is new file       │
   │ → other clients download   │
```

The old `NodeId` is soft-deleted. The new file gets a brand new `NodeId`. No collision.

**Status:** ✅ Working

---

### Case 11 — Local SQLite DB loss

#### Scenario

Client's `local_state.db` is lost/corrupted. This means:

- No local cursor → `RecoverCursorFromServerAsync` runs on startup
- No tracked file records
- Local files are on disk but "forgotten" by the sync engine

#### Recovery Flow

```
StartAsync()
  │
  ├── RecoverCursorFromServerAsync()
  │     ├── Local cursor? NO
  │     ├── Server cursor exists? → Restore it ✓
  │     └── No server cursor? → Start from sequence 0
  │
  ├── ApplyRemoteChangesAsync()
  │     ├── GET /sync/tree → full server tree
  │     ├── Paginated changes since cursor
  │     │   (if cursor=0, ALL changes since beginning of time)
  │     └── ReconcileServerTreeAsync()
  │           → Downloads all server files not present locally
  │
  ├── ScanLocalDirectoryAsync()
  │     └── Local files not in server tree → queue uploads
  │
  └── ApplyLocalChangesAsync()
        └── Execute all queued operations
```

**Current limitation:** No user-visible progress during this process. The sync can take a while (especially with many files) without indicating what's happening.

**Planned improvement:** Add progress reporting (Phase 4).

**Status:** ⚠️ Needs progress reporting (Phase 4)
**Status:** ⚠️ Needs progress reporting implementation

---

### Case 12 — Both clients delete same file offline

#### Scenario

Client A and Client B both delete the same file while offline. When they reconnect:

```
Client A                   Server                     Client B
   │                         │                          │
   │──[DELETE /files/{id}]──→│                          │
   │                         │──[Soft-delete]           │
   │                         │   ✓ Success (200)        │
   │                         │                          │
   │                         │←──[DELETE /files/{id}]───│
   │                         │                          │
   │                         │──[File already deleted]  │
   │                         │   ✗ 404 Not Found        │
   │                         │                          │
   │                         │←──[Handled gracefully]───│
   │                         │   Remove pending op      │
   │                         │   Clean up local record  │
```

The second DELETE returns 404. The client catches `HttpRequestException` with `IsNotFoundHttp` explicitly in `ApplyLocalChangesAsync` → removes the pending operation and local record.

**Status:** ✅ Already implemented in `SyncEngine.ApplyLocalChangesAsync`

---

### Case 13 — Symlink changes

Symlinks are tracked with `LinkTarget` field. Uploaded as SymbolicLink node type with zero bytes. Handled by `HandleRemoteSymlinkAsync` on the receiving end.

**Status:** ✅ Working

---

## Soft-Delete Lifecycle

```
File created → [active FileNode]
                │
File deleted → [IsDeleted=true, DeletedAt=now, SyncSequence=N]
                │
                ├── Within 30 days: changes feed includes deletion
                │   Tree reconciliation still shows node
                │
                └── After 30 days: TrashCleanupService hard-purges
                    (configurable via TrashRetentionOptions.RetentionDays)
                    → FileNode, shares, tags, comments, versions deleted
                    → Chunk GC: decrements refcounts, purges orphaned chunks
                    → No longer in changes feed
                    → No longer in server tree
                    → Tree reconciliation catches: "NodeId not found → delete locally"
```

### Configuration

`TrashRetentionOptions` (`src/Modules/Files/DotNetCloud.Modules.Files/Options/TrashRetentionOptions.cs`):

| Property                | Default | Description                           |
| ----------------------- | ------- | ------------------------------------- |
| `RetentionDays`         | 30      | Days before auto-purge. `0` disables. |
| `CleanupInterval`       | 6 hours | How often the background service runs |
| `OrganizationOverrides` | empty   | Per-org retention overrides           |

---

## Change Detection Mechanisms

| Mechanism              | Speed             | Reliability               | Details                                                                           |
| ---------------------- | ----------------- | ------------------------- | --------------------------------------------------------------------------------- |
| **FileSystemWatcher**  | Instant (< 500ms) | Medium (buffer overflows) | `Created`/`Changed`/`Deleted`/`Renamed` events. 500ms debounce. 64KB buffer.      |
| **SSE Push**           | Near-real-time    | High (auto-reconnect)     | Server pushes `sync-changed` on every mutation. Exponential backoff reconnection. |
| **Periodic Full Scan** | Every 5 min       | High (safety net)         | Full directory walk + comparison against tracked records.                         |
| **Polling Fallback**   | Every 30 sec      | Low (fallback)            | Only active when FSW fails (inotify limit, permissions).                          |

---

## LocalStateDb Schema (SQLite per context)

| Table                    | Purpose                                             |
| ------------------------ | --------------------------------------------------- |
| `local_files`            | Path → NodeId, content hash, mtime, sync state tag  |
| `pending_operations`     | Upload/Download/Delete queue with retry tracking    |
| `sync_cursor`            | Persisted server-issued cursor for crash resilience |
| `active_upload_sessions` | In-progress uploads (resume on restart)             |
| `failed_operations`      | Permanently failed operations                       |
| `conflict_records`       | Unresolved/resolved conflict history                |
| `sync_settings`          | Per-context settings                                |

---

## Verification Matrix

| Case | Test Scenario                    | Expected Result                       | Status              |
| ---- | -------------------------------- | ------------------------------------- | ------------------- |
| 1    | A creates file → synced to B     | File appears on B                     | ✅                  |
| 2    | B edits file → synced to A       | File updated on A                     | ✅                  |
| 3    | A edits file → synced to B       | File updated on B                     | ✅                  |
| 4    | B deletes file → synced to A     | File deleted on A                     | ✅                  |
| 5    | A creates file (server UI check) | File on server + B                    | ✅                  |
| 6    | Server UI deletes file           | Both clients delete                   | ✅                  |
| 7    | Client offline 30+ days          | All changes applied correctly         | ✅                  |
| 8    | Rename file on one client        | Other sees rename (not delete+create) | ⚠️ Needs Phase 2    |
| 9    | Concurrent edit conflict         | Conflict copy created + user notified | ✅ (UX: ⚠️ Phase 3) |
| 10   | Delete + recreate same name      | New NodeId, no collision              | ✅                  |
| 11   | Local DB corruption/loss         | Auto-recovery + progress shown        | ⚠️ Needs Phase 4    |
| 12   | Both clients delete same file    | No crash, clean 404 handling          | ✅                  |
| 13   | Symlink create/update            | Symlink synced correctly              | ✅                  |
