# Sync System Improvement Proposals

**Date:** 2026-03-08  
**Status:** Draft — For Discussion  
**Scope:** Server (`mint22`) ↔ Client (`Windows11-TestDNC`), with Linux client (`mint-dnc-client`) considerations  
**References:** [CLIENT_SERVER_MEDIATION_HANDOFF.md](CLIENT_SERVER_MEDIATION_HANDOFF.md), [ARCHITECTURE.md](../architecture/ARCHITECTURE.md)

---

## Table of Contents

1. [Efficiency Improvements](#1-efficiency-improvements)
2. [Reliability & Resilience](#2-reliability--resilience)
3. [Security Hardening](#3-security-hardening)
4. [Ease of Use](#4-ease-of-use)
5. [Cross-Platform (Linux Client) Readiness](#5-cross-platform-linux-client-readiness)
6. [Summary Matrix](#6-summary-matrix)

---

## 1. Efficiency Improvements

### 1.1 Content-Defined Chunking (CDC) — Replace Fixed 4MB Chunks

**Problem:** The current fixed 4MB chunking strategy means that inserting or deleting a single byte in the middle of a file shifts all subsequent chunk boundaries. For a 500MB file with a 1-byte edit, every chunk after the edit point has a new hash and must be re-uploaded — potentially hundreds of megabytes for a trivial change.

**Proposal:** Replace fixed-size chunking with **Content-Defined Chunking (CDC)** using a rolling hash (e.g., Rabin fingerprint or the faster **FastCDC** algorithm). CDC determines chunk boundaries based on content patterns, so inserting a byte only affects the chunk where the insertion occurs — all other boundaries remain stable.

**Specifics:**
- Target average chunk size: 4MB (same as now), with min 512KB / max 16MB bounds
- Use **FastCDC** (well-documented, patent-free, used by restic/borgbackup)
- Content hash per chunk remains SHA-256 (no change to dedup model)
- Manifest format changes: include chunk offsets + sizes alongside hashes

**Impact:**
- Editing a 500MB file → re-upload ~4MB instead of ~250MB+
- Negligible CPU overhead vs fixed chunking (FastCDC is O(n) with minimal lookups)
- Chunk deduplication across files still works (identical regions produce identical chunks regardless of position)

**Where:**
- **Server:** `ContentHasher.cs` — add CDC alternative to `ChunkAndHashAsync()`. Update `FileVersionChunk` to store offset+size. Backward compat: keep fixed chunking as fallback if client doesn't advertise CDC support.
- **Client:** `ChunkedTransferClient.cs` — use CDC in `SplitIntoChunksAsync()`. `InitiateUploadDto` already sends chunk hashes; add chunk sizes.

**Estimated Complexity:** Medium  
**Side:** Both (server + client)

---

### 1.2 Streaming Chunk Pipeline — Eliminate Full-File Memory Buffering

**Problem:** `ChunkedTransferClient.SplitIntoChunksAsync()` reads the entire file into a `List<ChunkData>` before uploading. For a 10GB file, this creates 2,560 chunk objects all held in memory simultaneously (~10GB peak). This will cause `OutOfMemoryException` on many machines.

**Proposal:** Replace the buffer-all-then-upload pattern with a **producer-consumer pipeline** using `Channel<ChunkData>`:

```
 [File Reader] --chunk--> [Channel<ChunkData>] --chunk--> [4x Parallel Uploaders]
```

- Reader reads one chunk at a time, computes hash, pushes to bounded channel (capacity: 8)
- Uploaders pull from channel, upload, release memory
- Peak memory: ~32MB (8 buffered × 4MB) regardless of file size

**Where:**
- **Client:** `ChunkedTransferClient.cs` — refactor `UploadAsync()` to use streaming pipeline. Same for download (stream chunks to disk instead of assembling in memory).

**Estimated Complexity:** Medium  
**Side:** Client only

---

### 1.3 Server-Issued Sync Cursor — Replace Timestamp-Based Delta Sync

**Problem:** The current `GET /sync/changes?since=<datetime>` approach has several issues:
1. **Clock skew:** If the client clock is ahead of the server, changes are missed silently
2. **Resolution:** Two changes at the same millisecond may be partially missed
3. **Time zones:** Client must convert to UTC correctly; bugs here cause silent data loss
4. **No ordering guarantee:** `UpdatedAt` isn't monotonic across concurrent writes

**Proposal:** Replace the timestamp parameter with a **server-issued opaque sync cursor** (similar to Dropbox's cursor or MS Graph's delta token):

- After each successful sync, server returns a cursor string (e.g., base64-encoded sequence number or WAL position)
- Client stores the cursor and sends it back on next `GET /sync/changes?cursor=<token>`
- Server resolves cursor → returns exactly the changes since that point
- No client-side timestamp handling at all

**Implementation:**
- Add a `SyncCursor` table on the server: `{UserId, CursorToken, SequenceNumber, CreatedAt}`
- Each file mutation increments a per-user monotonic sequence counter
- `GetChangesSince` accepts cursor, resolves to sequence number, returns changes with `SequenceNumber > saved`
- Response includes `nextCursor` for the client to store

**Where:**
- **Server:** New `SyncCursor` model + table. Modify `SyncService.GetChangesSinceAsync()` and `SyncController`. File mutations (create/update/delete/move) increment sequence.
- **Client:** `DotNetCloudApiClient.GetChangesSinceAsync()` sends cursor instead of timestamp. `LocalStateDb` stores cursor instead of `LastSyncedAt` datetime.

**Estimated Complexity:** Medium-High  
**Side:** Both

---

### 1.4 Paginated Change Responses

**Problem:** `GetChangesSinceAsync()` returns ALL changes since the cursor in a single response. After a long offline period, this could be thousands of records in one JSON payload, causing slow responses and high memory on both sides.

**Proposal:** Add **cursor-based pagination** to the changes endpoint:

```
GET /api/v1/sync/changes?cursor=abc123&limit=500

Response:
{
  "changes": [...],          // up to 500 items
  "nextCursor": "def456",   // null if no more
  "hasMore": true
}
```

Client iterates until `hasMore == false`, then stores `nextCursor`.

**Where:**
- **Server:** `SyncService` + `SyncController` — add `limit` param, return `nextCursor` + `hasMore`
- **Client:** `SyncEngine.ApplyRemoteChangesAsync()` — loop until `hasMore == false`

**Estimated Complexity:** Low  
**Side:** Both

---

### 1.5 Compression for Chunk Transfers

**Problem:** Chunk uploads/downloads transfer raw bytes over HTTPS. Text-heavy files (code, documents, logs) are highly compressible, yet the current implementation sends uncompressed data.

**Proposal:** Enable **gzip/brotli compression** at the HTTP transport level:

- Server: Add `app.UseResponseCompression()` with Brotli + Gzip providers for chunk download endpoints
- Client: Set `Accept-Encoding: br, gzip` on download requests; `Content-Encoding: gzip` on upload requests
- Skip compression for already-compressed formats (JPEG, PNG, ZIP, etc.) by checking MIME type

**Where:**
- **Server:** `Program.cs` or `ServiceDefaults` — enable response compression. Chunk download endpoints return `Content-Type: application/octet-stream` which will be compressed automatically.
- **Client:** `HttpClient` handler already supports decompression via `HttpClientHandler.AutomaticDecompression`. For uploads, wrap chunk streams in `GZipStream` and set header.

**Estimated Complexity:** Low  
**Side:** Both (mostly server config + minor client changes)

---

### 1.6 ETag / If-None-Match for Chunk Downloads

**Problem:** During sync, the client downloads chunks it may already have locally (e.g., after a partial sync failure and retry). The server re-sends the full chunk even if the client already has it.

**Proposal:** Use HTTP **ETag** headers on chunk download responses. The ETag is simply the chunk's SHA-256 hash:

- Server: `GET /chunks/{hash}` returns `ETag: "{hash}"` header
- Client: On retry/re-download, sends `If-None-Match: "{hash}"` header
- Server returns `304 Not Modified` if match → zero-byte transfer

**Where:**
- **Server:** Chunk download endpoint — add `ETag` response header, handle `If-None-Match` check
- **Client:** `DotNetCloudApiClient.DownloadChunkByHashAsync()` — send `If-None-Match`, handle 304

**Estimated Complexity:** Low  
**Side:** Both

---

## 2. Reliability & Resilience

### 2.1 Persistent Upload Sessions — Crash-Resilient Resumption

**Problem:** If the client crashes or loses power mid-upload, the upload session ID is lost (it's only held in memory). On restart, the entire file must be re-uploaded from scratch. For large files on slow connections, this is a serious UX problem.

**Proposal:** Persist upload session state to the local SQLite database:

- When `InitiateUploadAsync` returns, save `{sessionId, localPath, nodeId, chunkManifest, uploadedChunks[]}` to a new `ActiveUploadSession` table
- As each chunk completes, update `uploadedChunks` list
- On startup, check for incomplete sessions: call server's `GET /upload/{sessionId}` to verify session is still valid → resume from where we left off
- If server session expired (24h TTL), re-initiate with same manifest → server dedup means already-uploaded chunks are skipped anyway

**Where:**
- **Client:** New `ActiveUploadSession` entity in `LocalStateDb`. `ChunkedTransferClient.UploadAsync()` writes progress. `SyncEngine` checks for incomplete sessions on startup.
- **Server:** No changes needed — existing `GET /upload/{sessionId}` status endpoint + manifest dedup handles this.

**Estimated Complexity:** Medium  
**Side:** Client only

---

### 2.2 Per-Chunk Retry with Exponential Backoff

**Problem:** If a single chunk upload/download fails (transient network error, server 500), the entire transfer aborts. The `DotNetCloudApiClient` has retries at the HTTP level (3 retries with exponential backoff), but `ChunkedTransferClient` treats any chunk failure as a complete transfer failure.

**Proposal:** Add retry logic at the chunk level:

- Each chunk: up to 3 retries with exponential backoff (1s, 2s, 4s) + jitter
- If a chunk fails after max retries, mark it as failed and continue with remaining chunks
- At the end, report partial completion (X of Y chunks uploaded; Z failed)
- `SyncEngine` can re-queue partial uploads on next pass

**Where:**
- **Client:** `ChunkedTransferClient` — wrap individual chunk upload/download calls in retry loop. Add `ChunkTransferResult` to track per-chunk outcomes.

**Estimated Complexity:** Low-Medium  
**Side:** Client only

---

### 2.3 Locked File Handling (Retry with Backoff)

**Problem:** If a file is open by another process (e.g., Word document, database file), `File.OpenRead()` throws `IOException` immediately, failing the sync for that file. Common on Windows with Office files.

**Proposal:**
- Detect `IOException` with `HRESULT` check for sharing violation (`0x80070020`)
- Retry up to 3 times with 2-second delays
- If still locked after retries, skip the file, mark it as `SyncStateTag.Deferred`, and log a user-visible notification
- On the next sync pass, retry deferred files
- Optional: Use **Volume Shadow Copy (VSS)** on Windows to snapshot locked files (advanced, deferred to future)

**Linux Consideration:** Linux doesn't enforce advisory file locks the same way. `flock()` is advisory only — reads generally succeed. However, consistency isn't guaranteed for files being actively written. Consider checking `lsof` or `/proc/locks` as a best-effort check.

**Where:**
- **Client:** `SyncEngine` — wrap `File.OpenRead()` in retry loop. Add `Deferred` state to `SyncStateTag` enum. Add deferred-file tracking to `LocalStateDb`.

**Estimated Complexity:** Low-Medium  
**Side:** Client only

---

### 2.4 SQLite WAL Mode + Corruption Recovery

**Problem:** The client's SQLite database (`state.db`) uses the default DELETE journal mode, which is slower and less resilient to crashes. There's no corruption detection or recovery.

**Proposal:**
- **WAL mode:** Enable WAL journal mode for better concurrent read/write performance and crash resilience:
  ```csharp
  optionsBuilder.UseSqlite($"Data Source={dbPath};Journal Mode=Wal");
  ```
- **Corruption detection on startup:** Run `PRAGMA integrity_check` on startup. If it fails:
  1. Log the corruption
  2. Rename the corrupt DB to `state.db.corrupt.<timestamp>`
  3. Create a fresh DB
  4. Trigger a full sync (re-download metadata from server)
  5. Notify user via tray notification
- **Checkpoint after sync:** Run `PRAGMA wal_checkpoint(TRUNCATE)` after each complete sync pass to keep the WAL file from growing unbounded

**Where:**
- **Client:** `LocalStateDbContext` connection string. `LocalStateDb.InitializeAsync()` for integrity check. `SyncEngine` for post-sync checkpoint.

**Estimated Complexity:** Low  
**Side:** Client only

---

### 2.5 Idempotent Operations — Prevent Duplicate Uploads After Crash

**Problem:** If the client crashes after uploading a file but before updating `LocalStateDb`, the next sync pass will re-upload the same file. The file exists on the server, but the client doesn't know — it creates a duplicate version.

**Proposal:** Use the **content hash** as an idempotency key:

- Before uploading, check: does the server's node already have this content hash? (via `GetNodeAsync` or reconcile response)
- If the server already has the same content hash for this node, skip the upload and just update the local state DB
- The reconcile endpoint already compares content hashes — leverage this to avoid redundant uploads

**Where:**
- **Client:** `SyncEngine.ApplyLocalChangesAsync()` — before executing `PendingUpload`, check server state. `ChunkedTransferClient.InitiateUploadAsync` already returns existing chunks — if ALL chunks exist, `CompleteUpload` is essentially free.

**Estimated Complexity:** Low  
**Side:** Client only (server already handles this via chunk dedup)

---

### 2.6 Operation Retry Queue with Backoff

**Problem:** The `PendingOperationRecord.RetryCount` field exists in the schema but is never incremented. Failed operations are simply retried on the next sync pass (every 5 minutes) without any backoff, potentially hammering the server with the same failing request.

**Proposal:**
- Increment `RetryCount` on each failure
- Apply exponential backoff: skip operations where `RetryCount > 0 && nextRetryTime > now`
  - Retry schedule: 1 min, 5 min, 15 min, 1 hour, 6 hours, then cap
- After max retries (e.g., 10), move the operation to a `FailedOperations` table and notify the user
- User can manually retry failed operations from the tray UI

**Where:**
- **Client:** `SyncEngine.ExecutePendingOperationAsync()` — increment retry count and set next retry time. `LocalStateDb` — add `NextRetryAt` and `LastError` columns to `PendingOperationDbRow`.

**Estimated Complexity:** Low  
**Side:** Client only

---

## 3. Security Hardening

### 3.1 Server-Side Rate Limiting on Sync Endpoints

**Problem:** The sync endpoints (`/sync/changes`, `/sync/tree`, `/sync/reconcile`) have no rate limiting. A misbehaving or compromised client could flood the server, causing denial of service for other users.

**Proposal:** Add per-user rate limiting using ASP.NET Core's built-in rate limiting middleware:

- `/sync/changes`: 60 requests/minute per user (allows 1/sec sustained)
- `/sync/tree`: 10 requests/minute per user (heavy query, rarely needed)
- `/sync/reconcile`: 30 requests/minute per user
- `/upload/initiate`: 30 requests/minute per user
- `/upload/{sessionId}/chunks/*`: 300 requests/minute per user (allows burst uploads)
- Return `429 Too Many Requests` with `Retry-After` header

**Where:**
- **Server:** `Program.cs` — configure `AddRateLimiter()` with fixed-window or sliding-window policies keyed on the authenticated user ID. Apply via `[EnableRateLimiting("sync")]` attribute on controllers.

**Estimated Complexity:** Low  
**Side:** Server only

---

### 3.2 Request Correlation IDs

**Problem:** When debugging sync failures, there's no way to correlate a client-side error with the corresponding server-side log entry. The client sees "500 Internal Server Error" with no trace ID.

**Proposal:** Add request correlation via `X-Request-ID` header:

- Client: Generate a GUID for each API request, send as `X-Request-ID` header, log it alongside the error
- Server: Read `X-Request-ID` from request (or generate if missing), include in Serilog log context, return in response header
- Both sides log the same ID → easy cross-reference in log aggregation

**Where:**
- **Server:** Middleware in `ServiceDefaults` — read/generate `X-Request-ID`, push into Serilog `LogContext`, add to response headers.
- **Client:** `DotNetCloudApiClient` — generate and attach `X-Request-ID` on each request. Log it with errors.

**Estimated Complexity:** Low  
**Side:** Both

---

### 3.3 Chunk Integrity Verification on Download

**Problem:** When the client downloads a chunk, it doesn't verify that the received data matches the expected hash. A corrupted download (bit flip, truncated response, MITM tampering beyond TLS) would be written to disk silently.

**Proposal:** After downloading each chunk, compute its SHA-256 hash and compare with the expected hash from the manifest:

- If mismatch: discard the chunk, retry download (up to 3 times)
- If still mismatched after retries: fail the file download, report corruption error
- Combined with per-chunk retry (Proposal 2.2), this provides end-to-end integrity

The server already does this for uploads (verifies chunk hash against declared hash). The client should do the same for downloads.

**Where:**
- **Client:** `ChunkedTransferClient.DownloadAsync()` — after downloading each chunk, verify hash. Already have `ContentHasher` available.

**Estimated Complexity:** Low  
**Side:** Client only

---

### 3.4 Secure Temp File Handling for Downloads

**Problem:** The server's `DownloadService` reconstructs files from chunks using temp files in `Path.GetTempPath()`. These temp files contain user data and are world-readable on Linux (default `umask`). If the server crashes mid-download, temp files persist with sensitive content.

**Proposal:**
- Use a dedicated temp directory within the DotNetCloud data directory (not system temp)
- Set restrictive permissions: `700` on Linux, ACL on Windows
- On server startup, clean up any stale temp files from previous crashes
- Use `FileOptions.DeleteOnClose` (already done) as primary cleanup; startup sweep as backup

**Linux Consideration:** `Path.GetTempPath()` returns `/tmp/` which may be a tmpfs (RAM-backed) — good for performance but shared across all users. A dedicated `{DOTNETCLOUD_DATA_DIR}/tmp/` with `chmod 700` is safer.

**Where:**
- **Server:** `DownloadService.cs` — use app-specific temp dir. Add startup cleanup in `Program.cs` or `IHostedService`.

**Estimated Complexity:** Low  
**Side:** Server only

---

### 3.5 File Type / Extension Blocklist

**Problem:** There's no mechanism to prevent syncing of dangerous or unwanted file types. A compromised client could upload executables, scripts, or other potentially harmful files that other clients would then sync down.

**Proposal:** Add a configurable server-side blocklist:

- Default blocked extensions: `.exe`, `.bat`, `.cmd`, `.ps1`, `.sh`, `.vbs`, `.scr`, `.pif`, `.com` (configurable by admin)
- Check on `InitiateUploadAsync` — reject before any chunks are transferred
- Configurable per-user override for power users (admin setting)
- Also enforce max file size per upload (configurable, default 10GB)

**Where:**
- **Server:** `ChunkedUploadService.InitiateUploadAsync()` — check filename extension against blocklist. Add `FileUploadPolicy` configuration section.

**Estimated Complexity:** Low  
**Side:** Server only (client receives 400/403 rejection)

---

### 3.6 Audit Logging for Sync Operations

**Problem:** There's no audit trail for sync operations. If a file is corrupted or deleted via sync, there's no way to determine what happened, when, or which client caused it.

**Proposal:** Log key sync operations to a structured audit log:

- Events: `file.uploaded`, `file.downloaded`, `file.deleted`, `file.moved`, `file.shared`, `sync.reconcile.completed`
- Fields: `timestamp`, `userId`, `clientIp`, `requestId`, `nodeId`, `fileName`, `action`, `result`, `clientVersion`
- Storage: Serilog sink to dedicated audit log file (auditing already partially exists; extend to sync-specific events)

**Where:**
- **Server:** `ChunkedUploadService`, `DownloadService`, `FileService`, `SyncService` — emit `ILogger.LogInformation()` with structured properties for audit-relevant operations.

**Estimated Complexity:** Low  
**Side:** Server only

---

## 4. Ease of Use

### 4.1 Ignore Patterns — .syncignore File Support

**Problem:** The current `SelectiveSyncConfig` only supports folder-level include/exclude rules. Users can't ignore common nuisance files (`.DS_Store`, `Thumbs.db`, `*.tmp`, `node_modules/`, `.git/`) without manually configuring each one.

**Proposal:** Support a `.syncignore` file (similar to `.gitignore`) in the sync root:

- Parse gitignore-style patterns: `*`, `?`, `**`, `!` (negation), `/` (directory), `#` (comments)
- Ship with sensible defaults (built-in patterns for OS junk files and common dev artifacts)
- Evaluated at sync time — matching files are never uploaded or downloaded
- Changes to `.syncignore` take effect on next sync pass
- `.syncignore` file itself IS synced (so all clients share the same rules)

**Built-in Default Patterns:**
```
# OS generated files
.DS_Store
Thumbs.db
desktop.ini
*.swp
*~

# Temp files
*.tmp
*.temp
~$*

# Build artifacts
node_modules/
__pycache__/
bin/
obj/
.git/
```

**Where:**
- **Client:** New `SyncIgnoreParser` class in `DotNetCloud.Client.Core`. `SyncEngine` checks ignore rules before queuing uploads. FileSystemWatcher filters ignored paths.

**Estimated Complexity:** Medium  
**Side:** Client only (server-side enforcement optional — could add a `.syncignore` check on `InitiateUpload` as well)

---

### 4.2 Progress Reporting — Per-File Transfer Progress

**Problem:** The tray UI shows aggregate sync status ("Syncing: 2↑ 1↓") but doesn't show which specific files are being transferred or their individual progress. For large files, users see no feedback.

**Proposal:** Extend the IPC event protocol to include per-file transfer progress:

```json
{
  "type": "event",
  "event": "transfer-progress",
  "contextId": "...",
  "data": {
    "fileName": "large-video.mp4",
    "direction": "upload",
    "bytesTransferred": 52428800,
    "totalBytes": 1073741824,
    "chunksCompleted": 12,
    "chunksTotal": 256,
    "percentComplete": 4.9
  }
}
```

The tray UI can then show a list of active transfers with individual progress bars.

**Where:**
- **Client (SyncService):** `ChunkedTransferClient` already has `IProgress<TransferProgress>` — wire it to IPC event publishing.
- **Client (SyncTray):** Add `ActiveTransfersViewModel` to show current file transfers.

**Estimated Complexity:** Low-Medium  
**Side:** Client only

---

### 4.3 Conflict Resolution UI

**Problem:** When a conflict occurs, the client silently creates a conflict copy (e.g., `report (conflict - Ben - 2026-03-08).docx`) but the user may not notice or know what to do with it, especially as conflict copies accumulate.

**Proposal:** Add a **Conflicts panel** to the tray/settings UI:

- List all unresolved conflicts with:
  - Original file name and path
  - Local version timestamp
  - Server version timestamp
  - "Keep local" / "Keep server" / "Keep both" / "Open diff" actions
- After resolution, clean up the conflict copy
- Badge the tray icon when unresolved conflicts exist
- Persist conflict records in `LocalStateDb`

**Where:**
- **Client:** New `ConflictRecord` entity in `LocalStateDb`. `ConflictResolver` saves records. New `ConflictsViewModel` in SyncTray. IPC command `list-conflicts` / `resolve-conflict`.

**Estimated Complexity:** Medium  
**Side:** Client only

---

### 4.4 Bandwidth Throttling

**Problem:** The settings UI has fields for `UploadLimitKbps` and `DownloadLimitKbps` but they're not implemented. On metered or slow connections, sync can saturate the network, disrupting other applications.

**Proposal:** Implement bandwidth throttling using a **token bucket** rate limiter:

- Applied at the `HttpClient` handler level (wraps request/response streams)
- Configurable per-account via settings UI (already has fields)
- Separate upload and download limits
- Schedulable: e.g., unlimited during night (advanced, future)
- Default: unlimited (no throttle)

**Where:**
- **Client:** New `ThrottledHttpHandler` (DelegatingHandler) in `DotNetCloud.Client.Core`. Wire into `HttpClientFactory` named client setup. Read limits from `SyncContext` config.

**Estimated Complexity:** Medium  
**Side:** Client only

---

### 4.5 Selective Sync from Server-Side Folder List

**Problem:** To configure selective sync (which folders to sync), the user must know the folder structure. Currently there's no way to browse server folders from the settings UI to pick which ones to include/exclude.

**Proposal:** Add a **folder browser** in the settings/add-account flow:

- After authentication, fetch the folder tree from server (`GET /sync/tree`)
- Display as a treeview with checkboxes
- User checks/unchecks folders to include/exclude
- Saves as `SelectiveSyncConfig` rules
- Can be revisited later from settings

**Where:**
- **Client (SyncTray):** New `FolderBrowserViewModel` + `FolderBrowserView`. Uses `DotNetCloudApiClient.GetFolderTreeAsync()`.

**Estimated Complexity:** Medium  
**Side:** Client only

---

## 5. Cross-Platform (Linux Client) Readiness

### 5.1 Linux File System Differences — Prepare Now

**Issues to Address:**
| Aspect | Windows | Linux | Action Needed |
|--------|---------|-------|---------------|
| Path separators | `\` | `/` | Already handled by .NET `Path.Combine()` ✓ |
| Case sensitivity | Case-insensitive | Case-sensitive | **Must handle:** file `Report.docx` and `report.docx` are different on Linux, same on Windows |
| File permissions | ACLs, read-only attribute | POSIX mode bits (rwx) | **Must handle:** See 5.2 |
| Symbolic links | Partial support, requires admin | Native support | **Must handle:** See 5.3 |
| File locking | Mandatory (NTFS) | Advisory (flock/fcntl) | Different retry strategies needed |
| Max path length | 260 chars (default) | 4096 chars | Windows client needs long-path support |
| Illegal characters | `\/:*?"<>\|` | Only `/` and `\0` | Files valid on Linux may be invalid on Windows |
| Extended attributes | NTFS ADS, no xattr | xattr supported | Consider for future metadata sync |
| File watcher | `ReadDirectoryChangesW` | inotify (not recursive by default!) | **Must handle:** See 5.4 |
| Notifications | Toast API | D-Bus/libnotify | Already handled ✓ |
| IPC | Named Pipe | Unix domain socket | Already handled ✓ |
| Service management | Windows Service | systemd | Already handled ✓ |

**Where:** No single action — this is a reference table. Specific items addressed in sub-proposals below.

---

### 5.2 File Permission Metadata Sync

**Problem:** When syncing between Windows and Linux, file permissions are lost. An executable script (`chmod 755`) synced to Windows and back loses its execute bit. A read-only config file becomes writable.

**Proposal (Phase 1 — Minimal):**
- Store POSIX permission mode in file metadata on the server: Add `PosixMode` (nullable `int`) field to `FileNode`
- Linux client: Read file mode via `File.GetUnixFileMode()` on upload, apply on download
- Windows client: Ignore `PosixMode` (not applicable) — don't strip it either so it round-trips

**Proposal (Phase 2 — Future):**
- Map Windows read-only attribute to POSIX `444` mode
- Support ACL preservation for advanced use cases

**Where:**
- **Server:** Add `PosixMode` nullable int to `FileNode` model. Include in `FileNodeDto`. No behavioral change — just stores the value.
- **Client (Linux):** Set/get `UnixFileMode` during upload/download.
- **Client (Windows):** Pass `null` for `PosixMode` on upload. Ignore on download.

**Estimated Complexity:** Low  
**Side:** Both (schema change on server, behavior on Linux client)

---

### 5.3 Symbolic Link Policy

**Problem:** The `FileSystemWatcher` follows symbolic links on Linux, which can cause:
- Infinite loops (symlink pointing to ancestor)
- Syncing unintended directories (symlink to `/etc/`)
- Security issues (symlink to sensitive files)

**Proposal:** Define a clear symlink policy:

1. **Default: Skip symlinks entirely** — don't follow them, don't sync them
2. Detect symlinks via `FileAttributes.ReparsePoint` (Windows) or `FileSystemInfo.LinkTarget` (.NET 7+)
3. Log a warning when a symlink is encountered
4. Future option: sync symlinks as metadata (store target path, recreate on other clients)

**Where:**
- **Client:** `SyncEngine` file watcher event handlers — check for symlink attribute before processing. `FileSystemWatcher` filter to exclude reparse points.

**Estimated Complexity:** Low  
**Side:** Client only

---

### 5.4 Recursive inotify Watcher for Linux

**Problem:** .NET's `FileSystemWatcher` on Linux uses inotify, which IS recursive starting with .NET 6 (it manually adds watches for subdirectories). However, inotify has a per-user watch limit (`/proc/sys/fs/inotify/max_user_watches`, default 8192 on many distros). A sync folder with more than 8192 subdirectories will silently stop watching new directories.

**Proposal:**
- On startup, check `max_user_watches` and warn if it's below a threshold (e.g., 65536)
- Provide a setup script or first-run dialog that recommends increasing the limit:
  ```bash
  echo 'fs.inotify.max_user_watches=524288' | sudo tee /etc/sysctl.d/40-dotnetcloud.conf
  sudo sysctl -p /etc/sysctl.d/40-dotnetcloud.conf
  ```
- If watch limit is hit at runtime, fall back to increased periodic scan frequency (every 30 seconds instead of 5 minutes)
- Log the situation clearly

**Where:**
- **Client (Linux):** Platform check in `SyncEngine.StartAsync()`. Read `/proc/sys/fs/inotify/max_user_watches`. Show warning via notification service.

**Estimated Complexity:** Low  
**Side:** Client only (Linux-specific)

---

### 5.5 Case-Sensitivity Conflict Detection

**Problem:** A Linux user can create `Report.docx` and `report.docx` in the same directory. When these sync to a Windows client, they map to the same file, causing data loss.

**Proposal:**
- **Server-side:** On file creation/rename, check if another node in the same parent has a case-insensitively matching name. If so, reject with a clear error message: "Cannot create file: 'report.docx' conflicts with existing 'Report.docx' on case-insensitive clients"
- **Configurable:** Admin can disable this check if all clients are Linux
- **Client-side:** Before applying remote changes, detect case conflicts and rename one with a suffix (e.g., `report (case conflict).docx`)

**Where:**
- **Server:** `FileService.CreateFolderAsync()`, `RenameAsync()` — add case-insensitive duplicate check. Configuration flag in `appsettings.json`.
- **Client (Windows):** `SyncEngine.ApplyRemoteChangesAsync()` — detect case conflicts before writing.

**Estimated Complexity:** Low-Medium  
**Side:** Both

---

## 6. Summary Matrix

| # | Proposal | Priority | Complexity | Side | Category |
|---|----------|----------|------------|------|----------|
| 1.1 | Content-Defined Chunking (CDC) | High | Medium | Both | Efficiency |
| 1.2 | Streaming Chunk Pipeline | High | Medium | Client | Efficiency |
| 1.3 | Server-Issued Sync Cursor | High | Med-High | Both | Efficiency |
| 1.4 | Paginated Change Responses | Medium | Low | Both | Efficiency |
| 1.5 | Compression for Chunk Transfers | Medium | Low | Both | Efficiency |
| 1.6 | ETag for Chunk Downloads | Low | Low | Both | Efficiency |
| 2.1 | Persistent Upload Sessions | High | Medium | Client | Reliability |
| 2.2 | Per-Chunk Retry with Backoff | High | Low-Med | Client | Reliability |
| 2.3 | Locked File Handling | High | Low-Med | Client | Reliability |
| 2.4 | SQLite WAL + Corruption Recovery | Medium | Low | Client | Reliability |
| 2.5 | Idempotent Operations | Medium | Low | Client | Reliability |
| 2.6 | Operation Retry Queue | Medium | Low | Client | Reliability |
| 3.1 | Rate Limiting on Sync Endpoints | High | Low | Server | Security |
| 3.2 | Request Correlation IDs | Medium | Low | Both | Security |
| 3.3 | Chunk Integrity on Download | High | Low | Client | Security |
| 3.4 | Secure Temp File Handling | Medium | Low | Server | Security |
| 3.5 | File Type Blocklist | Medium | Low | Server | Security |
| 3.6 | Audit Logging for Sync Ops | Medium | Low | Server | Security |
| 4.1 | .syncignore File Support | High | Medium | Client | Ease of Use |
| 4.2 | Per-File Transfer Progress | Medium | Low-Med | Client | Ease of Use |
| 4.3 | Conflict Resolution UI | Medium | Medium | Client | Ease of Use |
| 4.4 | Bandwidth Throttling | Medium | Medium | Client | Ease of Use |
| 4.5 | Selective Sync Folder Browser | Low | Medium | Client | Ease of Use |
| 5.1 | Linux FS Differences Reference | — | — | — | Cross-Platform |
| 5.2 | File Permission Metadata Sync | Medium | Low | Both | Cross-Platform |
| 5.3 | Symbolic Link Policy | Medium | Low | Client | Cross-Platform |
| 5.4 | Recursive inotify Limits | Medium | Low | Client | Cross-Platform |
| 5.5 | Case-Sensitivity Conflicts | High | Low-Med | Both | Cross-Platform |

### Recommended Implementation Order (Suggested Phases)

**Batch 1 — Foundation (reliability + security baseline):**
- 3.1 Rate Limiting *(server)*
- 3.3 Chunk Integrity on Download *(client)*
- 2.2 Per-Chunk Retry *(client)*
- 2.4 SQLite WAL + Corruption Recovery *(client)*
- 2.6 Operation Retry Queue *(client)*
- 3.2 Request Correlation IDs *(both)*

**Batch 2 — Efficiency (bandwidth savings):**
- 1.1 Content-Defined Chunking *(both)*
- 1.2 Streaming Chunk Pipeline *(client)*
- 1.5 Compression *(both)*
- 1.3 Server-Issued Sync Cursor *(both)*

**Batch 3 — User experience:**
- 4.1 .syncignore Support *(client)*
- 2.1 Persistent Upload Sessions *(client)*
- 2.3 Locked File Handling *(client)*
- 4.2 Per-File Progress *(client)*

**Batch 4 — Cross-platform prep (before Linux client launch):**
- 5.5 Case-Sensitivity Conflicts *(both)*
- 5.2 File Permission Metadata *(both)*
- 5.3 Symbolic Link Policy *(client)*
- 5.4 inotify Limits *(client)*

**Batch 5 — Polish:**
- 1.4 Paginated Changes *(both)*
- 1.6 ETag for Chunks *(both)*
- 3.4 Secure Temp Files *(server)*
- 3.5 File Type Blocklist *(server)*
- 3.6 Audit Logging *(server)*
- 4.3 Conflict Resolution UI *(client)*
- 4.4 Bandwidth Throttling *(client)*
- 4.5 Selective Sync Browser *(client)*
- 2.5 Idempotent Operations *(client)*

---

## Handoff Process Reminder

Once we agree on which proposals to implement:

1. **Server changes** → implemented directly on `mint22`
2. **Client changes** → described in [CLIENT_SERVER_MEDIATION_HANDOFF.md](CLIENT_SERVER_MEDIATION_HANDOFF.md) and implemented on `Windows11-TestDNC`
3. **Both-side changes** → server first, then client (server provides the contract)
4. **Linux client** → tested on `mint-dnc-client` after Windows client is stable

Each approved proposal becomes a numbered issue in the handoff doc following the established relay template.
