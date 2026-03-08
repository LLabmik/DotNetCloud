# Sync Improvement Implementation Plan

**Date:** 2026-03-08
**Status:** Approved — Ready for Implementation
**Based on:** [SYNC_IMPROVEMENT_PROPOSALS.md](SYNC_IMPROVEMENT_PROPOSALS.md)
**Handoff Process:** [CLIENT_SERVER_MEDIATION_HANDOFF.md](CLIENT_SERVER_MEDIATION_HANDOFF.md)

---

## Environment

| Role | Machine | OS | Notes |
|------|---------|-----|-------|
| Server | `mint22` | Linux Mint 22 | `https://mint22:15443/` |
| Client (primary) | `Windows11-TestDNC` | Windows 11 | Sync dir: `C:\Users\benk\Documents\synctray` |
| Client (future) | `mint-dnc-client` | Linux Mint | For Linux client testing |
| Client (future) | — | macOS | Third-party contributor will supply Apple implementation |

## Rules

1. **Server changes** → implemented directly on `mint22`
2. **Client changes** → implemented on `Windows11-TestDNC`, described in handoff doc
3. **Both-side changes** → server first (provides the contract), then client
4. **Linux readiness** → all client code must account for Linux differences; platform-specific code behind `RuntimeInformation` / `OperatingSystem` checks
5. **macOS awareness** → design interfaces and abstractions so a future macOS contributor can plug in without restructuring (no Windows-only assumptions baked into core logic)
6. **All file types sync** — this is a backup system; never block file extensions. Security is enforced via server-side execution prevention and scanning, not upload rejection.

---

## Batch 1 — Foundation: Reliability + Security Baseline

**Goal:** Make what we have today robust and debuggable before adding new capabilities.

### 1.1 — Sync Service Logging (Client + Server)

**Approved Proposal:** 3.6 (Audit Logging) + user requirement for client-side sync service logs

**Problem:** The sync service on the client has no structured logging. The server has no sync-specific audit trail. When something goes wrong, there's no way to investigate.

**Scope:**

**Server (mint22):**
- Add structured Serilog logging to all sync/file operations in `ChunkedUploadService`, `DownloadService`, `FileService`, `SyncService`
- Log events: `file.uploaded`, `file.downloaded`, `file.deleted`, `file.moved`, `file.shared`, `sync.reconcile.completed`
- Structured fields per event: `Timestamp`, `UserId`, `ClientIp`, `RequestId`, `NodeId`, `FileName`, `Action`, `Result`, `FileSize`, `ClientVersion`
- Dedicated audit log sink (separate from application log): `{DOTNETCLOUD_DATA_DIR}/logs/audit-sync.log`
- Same rolling-file config as existing server logs

**Client (Windows11-TestDNC):**
- Add Serilog to `DotNetCloud.Client.SyncService` with structured JSON logging
- Log to: `{DataRoot}/logs/sync-service.log` (Windows: `%APPDATA%\DotNetCloud\logs\`, Linux: `~/.local/share/DotNetCloud/logs/`)
- Events to log:
  - Sync pass start/complete/error (with duration, files processed, conflicts)
  - Each file upload/download start/complete/error (with file name, size, duration, chunk stats)
  - Conflict detection (original path, conflict path, reason)
  - Auth token refresh (success/failure, no token values)
  - IPC commands received/responded
  - FileSystemWatcher events that trigger sync
- **Configurable rotation:** Default 30-day retention, rolling daily files, configurable via `sync-settings.json`:
  ```json
  {
    "logging": {
      "retentionDays": 30,
      "maxFileSizeMB": 50,
      "rollingInterval": "Day",
      "minimumLevel": "Information"
    }
  }
  ```
- Config changes take effect on service restart
- Log level overridable per-category for debugging (e.g., set `Transfer` to `Debug` for chunk-level detail)

**Linux/macOS Considerations:**
- Same logging infrastructure; only the path differs (already platform-aware via `GetSystemDataRoot()`)
- Log file permissions: `600` on Linux (owner-only read/write)

**Deliverables:**
- ☐ Server: Serilog structured audit logging in sync/file service classes
- ☐ Server: Dedicated `audit-sync.log` rolling file sink
- ☐ Client: Serilog integration in SyncService with rolling file sink
- ☐ Client: Structured log events for all sync lifecycle operations
- ☐ Client: `sync-settings.json` logging configuration section
- ☐ Client: Log rotation with configurable retention (default 30 days)
- ☐ Client: Platform-appropriate log directory and file permissions

**Side:** Both
**Complexity:** Medium

---

### 1.2 — Request Correlation IDs

**Approved Proposal:** 3.2

**Problem:** No way to match a client-side error to the server-side log entry that caused it.

**Scope:**

**Server (mint22):**
- Add middleware (in `ServiceDefaults` or `Program.cs`) that:
  - Reads `X-Request-ID` header from incoming request (or generates a new GUID if absent)
  - Pushes it into Serilog `LogContext` as `RequestId` property
  - Adds `X-Request-ID` to response headers
- All log entries within that request automatically include the correlation ID

**Client (Windows11-TestDNC):**
- `DotNetCloudApiClient`: Generate `Guid.NewGuid()` for each API call, attach as `X-Request-ID` header
- Log the request ID alongside every API call start/complete/error
- On error, include the request ID in the user-facing error message and log (makes support easier: "Please share your sync log and reference request ID `abc-123`")

**Linux/macOS Considerations:** None — pure HTTP/log concern, fully cross-platform.

**Deliverables:**
- ☐ Server: `RequestCorrelationMiddleware` reads/generates `X-Request-ID`, pushes to Serilog context, returns in response
- ☐ Client: `DotNetCloudApiClient` generates and sends `X-Request-ID` on every request
- ☐ Client: Request ID logged with every API operation

**Side:** Both
**Complexity:** Low

---

### 1.3 — Server-Side Rate Limiting on Sync Endpoints

**Approved Proposal:** 3.1

**Problem:** No rate limiting on sync endpoints. A misbehaving client can DoS the server.

**Scope:**

**Server (mint22):**
- Configure ASP.NET Core rate limiting middleware (`AddRateLimiter()`) with sliding-window policies keyed on authenticated user ID:

  | Endpoint Pattern | Limit | Window |
  |-----------------|-------|--------|
  | `/api/v1/sync/changes` | 60 req | 1 min |
  | `/api/v1/sync/tree` | 10 req | 1 min |
  | `/api/v1/sync/reconcile` | 30 req | 1 min |
  | `/api/v1/files/upload/initiate` | 30 req | 1 min |
  | `/api/v1/files/upload/*/chunks/*` | 300 req | 1 min |
  | `/api/v1/files/*/download` | 120 req | 1 min |
  | `/api/v1/files/chunks/*` | 300 req | 1 min |

- Return `429 Too Many Requests` with `Retry-After` header
- Configurable limits via `appsettings.json` (admins can adjust for their environment)

**Client (Windows11-TestDNC):**
- `DotNetCloudApiClient` already handles 429 with retry — verify `Retry-After` header is respected in backoff logic
- Log when rate-limited (useful for debugging aggressive sync intervals)

**Linux/macOS Considerations:** None — server-side only.

**Deliverables:**
- ☐ Server: Rate limiting middleware configured with per-user sliding-window policies
- ☐ Server: `[EnableRateLimiting]` attributes on sync/file controllers
- ☐ Server: Configurable limits in `appsettings.json`
- ☐ Client: Verify 429 + `Retry-After` handling; add logging on rate-limit events

**Side:** Server (+ client verification)
**Complexity:** Low

---

### 1.4 — Chunk Integrity Verification on Download

**Approved Proposal:** 3.3

**Problem:** Client doesn't verify downloaded chunk data matches the expected SHA-256 hash. Corrupted downloads are silently accepted.

**Scope:**

**Client (Windows11-TestDNC):**
- In `ChunkedTransferClient.DownloadAsync()`, after downloading each chunk:
  1. Compute SHA-256 of received bytes
  2. Compare to expected hash from the chunk manifest
  3. If mismatch: log warning, discard, retry download (up to 3 times)
  4. If still mismatched after retries: fail the file download, log error with request ID
- Reuse existing `ContentHasher.ComputeHash()` 

**Linux/macOS Considerations:** None — pure in-memory SHA-256 comparison, fully cross-platform.

**Deliverables:**
- ☐ Client: Post-download SHA-256 verification for every chunk
- ☐ Client: Retry on hash mismatch (up to 3 attempts)
- ☐ Client: Clear error logging with request ID on persistent corruption

**Side:** Client only
**Complexity:** Low

---

### 1.5 — Per-Chunk Retry with Exponential Backoff

**Approved Proposal:** 2.2

**Problem:** A single chunk failure aborts the entire file transfer.

**Scope:**

**Client (Windows11-TestDNC):**
- Wrap each chunk upload/download call in a retry loop:
  - Max retries: 3
  - Backoff: 1s → 2s → 4s + random jitter (0–500ms)
  - Retry on: network errors, 5xx responses, timeout
  - Do NOT retry on: 4xx responses (client error), 429 (handle separately via `Retry-After`)
- If a chunk fails after max retries:
  - Upload: mark transfer as partial; on next sync pass, `InitiateUpload` dedup will skip already-uploaded chunks
  - Download: fail the file (integrity can't be guaranteed with missing chunks)
- Add `ChunkTransferResult` record to track per-chunk outcomes for logging

**Linux/macOS Considerations:** None — HTTP retry logic, fully cross-platform.

**Deliverables:**
- ☐ Client: Per-chunk retry loop with exponential backoff + jitter
- ☐ Client: `ChunkTransferResult` tracking per-chunk success/failure/retries
- ☐ Client: Detailed logging per chunk (hash, attempt number, duration, error)

**Side:** Client only
**Complexity:** Low-Medium

---

### 1.6 — SQLite WAL Mode + Corruption Recovery

**Approved Proposal:** 2.4

**Problem:** Client SQLite uses slow DELETE journal mode with no corruption detection.

**Scope:**

**Client (Windows11-TestDNC):**
- Enable WAL mode in `LocalStateDbContext` connection string: `Journal Mode=Wal`
- On `InitializeAsync()`:
  1. Run `PRAGMA integrity_check` — if it returns anything other than `"ok"`:
     - Log the corruption details
     - Rename `state.db` → `state.db.corrupt.{timestamp}`
     - Also rename `state.db-wal` and `state.db-shm` if they exist
     - Create fresh DB via `EnsureCreatedAsync()`
     - Set flag to trigger full sync on next pass
     - Send notification to tray: "Sync database was corrupted and has been reset. A full re-sync will occur."
  2. If integrity check passes, proceed normally
- After each complete sync pass: run `PRAGMA wal_checkpoint(TRUNCATE)` to prevent WAL growth

**Linux/macOS Considerations:**
- WAL mode works identically on all platforms with SQLite
- Same corruption recovery logic
- File rename operations use `File.Move()` (cross-platform)

**Deliverables:**
- ☐ Client: WAL journal mode enabled
- ☐ Client: Startup integrity check with automatic recovery
- ☐ Client: Corrupt DB preservation (renamed, not deleted) for post-mortem
- ☐ Client: Post-sync WAL checkpoint
- ☐ Client: User notification on corruption recovery

**Side:** Client only
**Complexity:** Low

---

### 1.7 — Operation Retry Queue with Backoff

**Approved Proposal:** 2.6

**Problem:** `RetryCount` field exists but is never used. Failed operations retry every 5 minutes indefinitely without backoff.

**Scope:**

**Client (Windows11-TestDNC):**
- Add columns to `PendingOperationDbRow`: `NextRetryAt` (DateTime?), `LastError` (string?)
- In `SyncEngine.ExecutePendingOperationAsync()`:
  - On failure: increment `RetryCount`, set `LastError` to exception message, compute `NextRetryAt`:
    - Retry 1: now + 1 min
    - Retry 2: now + 5 min
    - Retry 3: now + 15 min
    - Retry 4: now + 1 hour
    - Retry 5–9: now + 6 hours
    - Retry 10+: move to `FailedOperationDbRow` table, stop retrying
  - On success: clear `RetryCount`, `NextRetryAt`, `LastError`
- In `GetPendingOperationsAsync()`: filter `WHERE NextRetryAt IS NULL OR NextRetryAt <= @now`
- Failed operations visible in tray UI (future: manual retry button)
- Log each retry attempt and final failure

**Linux/macOS Considerations:** None — SQLite schema + logic, fully cross-platform.

**Deliverables:**
- ☐ Client: `NextRetryAt` and `LastError` columns on pending operations
- ☐ Client: Exponential backoff schedule in `ExecutePendingOperationAsync()`
- ☐ Client: `FailedOperationDbRow` table for permanently failed operations
- ☐ Client: Filter pending operations by `NextRetryAt` eligibility
- ☐ Client: Logging of retry attempts and final failures

**Side:** Client only
**Complexity:** Low

---

### 1.8 — Secure Temp File Handling

**Approved Proposal:** 3.4

**Problem:** Server uses system temp dir for download reconstruction. Temp files are world-readable on Linux and persist on crash.

**Scope:**

**Server (mint22):**
- Create dedicated temp directory: `{DOTNETCLOUD_DATA_DIR}/tmp/`
- Set permissions on creation: `chmod 700` (Linux), restricted ACL (Windows)
- Modify `DownloadService.cs` to use this directory instead of `Path.GetTempPath()`
- Add startup cleanup: `IHostedService` that deletes all files in `{DATA_DIR}/tmp/` older than 1 hour on startup
- Continue using `FileOptions.DeleteOnClose` as primary cleanup mechanism

**Linux/macOS Considerations:**
- `chmod 700` via `File.SetUnixFileMode()` on directory creation
- Avoids shared `/tmp/` security risk

**Deliverables:**
- ☐ Server: Dedicated temp directory under `DOTNETCLOUD_DATA_DIR`
- ☐ Server: Restrictive permissions on temp directory creation
- ☐ Server: `DownloadService` uses app-specific temp dir
- ☐ Server: Startup cleanup `IHostedService` for stale temp files

**Side:** Server only
**Complexity:** Low

---

### 1.9 — Server-Side File Scanning (Replaces Extension Blocklist)

**Approved Proposal:** 3.5 (modified per user feedback — no extension blocking)

**Problem (revised):** This is a backup system — ALL file types must sync, including `.exe`, `.sh`, `.bat`, etc. But the server must never execute uploaded content, and ideally should detect obviously malicious uploads.

**Scope:**

**Server (mint22):**
- **Execution prevention (mandatory):**
  - Stored files have NO execute permission: `chmod 600` (or `644` max) on chunk storage directory
  - Storage engine enforced: `LocalFileStorageEngine.WriteChunkAsync()` sets `UnixFileMode.None` for execute bits on written files
  - Chunk storage paths are content-addressed hashes (no user-supplied filenames in storage layer) — already the case, confirm and document
  - `X-Content-Type-Options: nosniff` header on all download responses (prevents browser MIME sniffing)
  - `Content-Disposition: attachment` on file download responses (prevents inline execution in browser)
- **Optional ClamAV scanning (future-ready interface):**
  - Define `IFileScanner` interface: `Task<ScanResult> ScanAsync(Stream content, string fileName, CancellationToken ct)`
  - `ScanResult`: `{IsClean, ThreatName?, ScannerName}`
  - Default implementation: `NoOpFileScanner` (passes everything)
  - Future ClamAV implementation: scans on `CompleteUploadAsync()`, quarantines threats (marks in DB, doesn't serve to clients), notifies admin
  - Scan results stored on `FileVersion`: nullable `ScanStatus` enum (`NotScanned`, `Clean`, `Threat`, `Error`)
  - Admin API to view quarantined files and release false positives
- **Max file size enforcement:**
  - Configurable via `appsettings.json`: `"FileUpload:MaxFileSizeBytes": 16106127360` (15GB default)
  - Checked on `InitiateUploadAsync()` — reject before any chunks transfer
  - Per-user overrides possible via admin API (future)

**Linux/macOS Considerations:**
- `File.SetUnixFileMode()` for chunk permission enforcement on Linux/macOS servers
- Windows servers: rely on ACL-based restrictions (no execute for IIS app pool identity)

**Deliverables:**
- ☐ Server: Chunk storage file permissions enforced (no execute bits)
- ☐ Server: Confirm content-addressed storage paths (no user filenames on disk)
- ☐ Server: `X-Content-Type-Options: nosniff` + `Content-Disposition: attachment` headers
- ☐ Server: `IFileScanner` interface + `NoOpFileScanner` default
- ☐ Server: `ScanStatus` field on `FileVersion` model (nullable, for future use)
- ☐ Server: Configurable max file size with pre-upload rejection

**Side:** Server only
**Complexity:** Medium

---

## Batch 2 — Efficiency: Bandwidth Savings

**Goal:** Reduce bandwidth consumption significantly for common workflows (editing existing files, re-syncing after partial failure).

### 2.1 — Content-Defined Chunking (CDC)

**Approved Proposal:** 1.1

**Problem:** Fixed 4MB chunks mean a 1-byte edit can force re-upload of all subsequent chunks.

**Scope:**

**Server (mint22):**
- Add FastCDC implementation to `ContentHasher.cs`:
  - New method: `ChunkAndHashCdcAsync(Stream, avgSize: 4MB, minSize: 512KB, maxSize: 16MB)`
  - Returns: `List<ChunkInfo>` with `{Hash, Offset, Size}` (vs current `{Hash, Data}`)
- Update `FileVersionChunk` model: add `Offset` (long) and `ChunkSize` (int) columns
- Update `InitiateUploadDto` / gRPC `InitiateUploadRequest`: add `ChunkSizes` array alongside `ChunkHashes`
- Backward compatibility: if client sends no chunk sizes, assume fixed-size (4MB) — legacy clients still work
- `ChunkedUploadService.InitiateUploadAsync()`: accept chunk sizes, store in manifest
- `ChunkedUploadService.CompleteUploadAsync()`: use offset+size for file reconstruction

**Client (Windows11-TestDNC):**
- Replace `SplitIntoChunksAsync()` in `ChunkedTransferClient` with FastCDC-based splitting
- Send chunk sizes alongside hashes in `InitiateUploadAsync()`
- Download: use chunk manifest with sizes for proper reassembly
- Advertise CDC capability via `X-Sync-Capabilities: cdc` header (server can use this for feature negotiation)

**Linux/macOS Considerations:** CDC algorithm is pure computation — fully cross-platform. No file system dependencies.

**Deliverables:**
- ☐ Server: FastCDC implementation in `ContentHasher`
- ☐ Server: `Offset` + `ChunkSize` fields on `FileVersionChunk`
- ☐ Server: Updated `InitiateUploadDto` with chunk sizes
- ☐ Server: Backward-compatible upload/download with legacy fixed-size clients
- ☐ Client: FastCDC-based `SplitIntoChunksAsync()` replacement
- ☐ Client: Chunk sizes sent in upload initiation
- ☐ Client: `X-Sync-Capabilities` header for feature negotiation

**Side:** Both
**Complexity:** Medium

---

### 2.2 — Streaming Chunk Pipeline

**Approved Proposal:** 1.2

**Problem:** All chunks buffered in memory simultaneously — OOM risk on large files.

**Scope:**

**Client (Windows11-TestDNC):**
- Refactor `ChunkedTransferClient.UploadAsync()`:
  - Use `System.Threading.Channels.Channel<ChunkData>` with bounded capacity (8 slots)
  - Producer task: reads file → splits via CDC → hashes → pushes to channel
  - Consumer tasks: 4 parallel uploaders pulling from channel → uploading → releasing
  - Peak memory: ~32MB (8 × 4MB average) regardless of file size
- Refactor download similarly:
  - Download chunks to temp files on disk (not in-memory assembly)
  - Final assembly: concatenate temp chunk files into target file
  - Clean up temp chunk files after assembly
- Progress reporting: pipe `ChunkData` count through progress callback (already exists)

**Linux/macOS Considerations:**
- `Channel<T>` is fully cross-platform (.NET API)
- Temp file paths: use platform-appropriate directory (`Path.GetTempPath()` or app-specific)

**Deliverables:**
- ☐ Client: `Channel<ChunkData>`-based producer-consumer pipeline for uploads
- ☐ Client: Disk-based chunk assembly for downloads (not in-memory)
- ☐ Client: Bounded memory usage regardless of file size
- ☐ Client: Temp chunk cleanup after assembly

**Side:** Client only
**Complexity:** Medium

---

### 2.3 — Compression for Chunk Transfers

**Approved Proposal:** 1.5

**Problem:** Raw bytes transferred even for highly compressible content (text, code, documents).

**Scope:**

**Server (mint22):**
- Enable response compression in `Program.cs` or `ServiceDefaults`:
  ```csharp
  builder.Services.AddResponseCompression(options => {
      options.EnableForHttps = true;
      options.Providers.Add<BrotliCompressionProvider>();
      options.Providers.Add<GzipCompressionProvider>();
      options.MimeTypes = ResponseCompressionDefaults.MimeTypes
          .Concat(new[] { "application/octet-stream" });
  });
  ```
- Apply to chunk download + file download endpoints
- Skip compression for known-compressed MIME types (JPEG, PNG, ZIP, etc.) via `CompressionProviderOptions`

**Client (Windows11-TestDNC):**
- Ensure `HttpClientHandler.AutomaticDecompression = DecompressionMethods.All` is set (handles Brotli + Gzip transparently)
- For uploads: wrap chunk stream in `GZipStream`, set `Content-Encoding: gzip` header
- Skip compression for chunks where MIME type of the containing file is already compressed (check against known list)

**Linux/macOS Considerations:** None — Brotli/Gzip is built into .NET runtime on all platforms.

**Deliverables:**
- ☐ Server: Response compression middleware with Brotli + Gzip
- ☐ Server: MIME type filtering (skip pre-compressed formats)
- ☐ Client: Automatic decompression enabled on HttpClient
- ☐ Client: Gzip compression on chunk uploads with Content-Encoding header
- ☐ Client: Skip compression for already-compressed content

**Side:** Both
**Complexity:** Low

---

### 2.4 — Server-Issued Sync Cursor

**Approved Proposal:** 1.3

**Problem:** Timestamp-based delta sync is vulnerable to clock skew, timezone bugs, and missed changes at millisecond resolution.

**Scope:**

**Server (mint22):**
- New model: `SyncSequence` table — `{Id, UserId, SequenceNumber (long), CreatedAt}`
- New model: `UserSyncCounter` — `{UserId (PK), CurrentSequence (long)}` — monotonic counter per user
- On every file mutation (`Create`, `Update`, `Delete`, `Move`, `Copy`, `Rename`):
  - Increment `UserSyncCounter.CurrentSequence`
  - Store `SequenceNumber` on the `FileNode` (new column: `SyncSequence long?`)
- Modify `SyncController`:
  - Accept `cursor` query param (base64-encoded `{userId}:{sequenceNumber}`)
  - Return all `FileNodes` where `SyncSequence > cursor.sequenceNumber`
  - Include `nextCursor` in response (base64 of `{userId}:{maxSequenceNumber}`)
  - **Backward compat:** If `since` param provided (no cursor), fall back to current timestamp logic
- Modify `SyncService.GetChangesSinceAsync()`:
  - Overload accepting cursor string
  - Query by `SyncSequence > N` instead of `UpdatedAt >= datetime`

**Client (Windows11-TestDNC):**
- `DotNetCloudApiClient.GetChangesSinceAsync()`:
  - New overload: `GetChangesSinceAsync(string? cursor, ...)`
  - First sync (no cursor): omit param → server returns all + initial cursor
  - Subsequent syncs: send cursor → receive delta + next cursor
- `LocalStateDb`: replace `SyncCheckpointRow.LastSyncedAt` with `SyncCursor` (string)
- Migration path: if `SyncCursor` is null, send without cursor (full sync), receive first cursor

**Linux/macOS Considerations:** None — pure data model + API change.

**Deliverables:**
- ☐ Server: `UserSyncCounter` table + per-mutation increment logic
- ☐ Server: `SyncSequence` column on `FileNode`
- ☐ Server: Cursor-based `GetChangesSinceAsync()` overload
- ☐ Server: `SyncController` cursor parameter + backward compat
- ☐ Server: Cursor encoding/decoding (base64)
- ☐ Client: API client cursor support
- ☐ Client: `LocalStateDb` cursor storage (replacing timestamp)
- ☐ Client: Migration from timestamp to cursor on first sync

**Side:** Both
**Complexity:** Medium-High

---

### 2.5 — Paginated Change Responses

**Approved Proposal:** 1.4

**Problem:** All changes returned in a single response. Large deltas after long offline periods = slow/huge payloads.

**Scope:**

**Server (mint22):**
- Modify `GET /api/v1/sync/changes`:
  - Accept `limit` query param (default: 500, max: 5000)
  - Return response:
    ```json
    {
      "changes": [...],
      "nextCursor": "...",
      "hasMore": true
    }
    ```
  - If `hasMore == true`, client must call again with `nextCursor`
  - Pairs naturally with cursor-based sync (Batch 2.4)

**Client (Windows11-TestDNC):**
- `SyncEngine.ApplyRemoteChangesAsync()`: loop calling `GetChangesSinceAsync()` until `hasMore == false`
- Store intermediate cursor after each page (crash resilience — don't lose progress)

**Linux/macOS Considerations:** None.

**Deliverables:**
- ☐ Server: `limit` parameter on changes endpoint
- ☐ Server: `hasMore` + `nextCursor` in response envelope
- ☐ Client: Pagination loop in `ApplyRemoteChangesAsync()`
- ☐ Client: Intermediate cursor persistence per page

**Side:** Both
**Complexity:** Low

---

### 2.6 — ETag / If-None-Match for Chunk Downloads

**Approved Proposal:** 1.6

**Problem:** Re-downloading chunks the client already has (e.g., after partial sync failure retry).

**Scope:**

**Server (mint22):**
- `GET /api/v1/files/chunks/{hash}`:
  - Add `ETag: "{hash}"` response header
  - Check `If-None-Match` request header — if matches, return `304 Not Modified`

**Client (Windows11-TestDNC):**
- Before downloading a chunk, check if local chunk file/cache exists with matching hash
- Send `If-None-Match: "{hash}"` header
- Handle `304` response: skip download, use local copy

**Linux/macOS Considerations:** None.

**Deliverables:**
- ☐ Server: `ETag` header on chunk download responses
- ☐ Server: `If-None-Match` → `304` handling
- ☐ Client: `If-None-Match` header on chunk download requests
- ☐ Client: Handle `304` gracefully

**Side:** Both
**Complexity:** Low

---

## Batch 3 — User Experience

**Goal:** Make sync intuitive, informative, and forgiving for everyday users.

### 3.1 — .syncignore with UI Support

**Approved Proposal:** 4.1 + user requirement for client-side UI

**Problem:** No way to ignore OS junk files, temp files, build artifacts. Current selective sync is folder-level only.

**Scope:**

**Client (Windows11-TestDNC) — Core Logic:**
- New `SyncIgnoreParser` class in `DotNetCloud.Client.Core`:
  - Parse `.gitignore`-style patterns: `*`, `?`, `**`, `!` (negation), `/` (directory marker), `#` (comments)
  - Use .NET's `FileSystemGlobbing` library (Microsoft.Extensions.FileSystemGlobbing) for glob matching
  - Ship with built-in defaults (compiled in, always active):
    ```
    # OS generated
    .DS_Store
    Thumbs.db
    desktop.ini
    *.swp
    *~
    
    # Temp files
    *.tmp
    *.temp
    ~$*
    
    # Version control
    .git/
    .svn/
    .hg/
    
    # Package managers (re-downloadable)
    node_modules/
    .npm/
    .yarn/
    .pnp.*
    packages/
    .nuget/
    ```
  - User `.syncignore` file in sync root: merged with built-in rules (user rules take priority)
  - `.syncignore` file IS synced (shared across all clients)
- `SyncEngine`: check `SyncIgnoreParser.IsIgnored(relativePath)` before:
  - Queuing uploads from FileSystemWatcher events
  - Applying remote changes (don't download ignored files)
  - Running periodic scan comparisons
- `FileSystemWatcher` performance: pre-filter events for common patterns (`.tmp`, `~$*`) to reduce event noise

**Client (Windows11-TestDNC) — UI (SyncTray):**
- New "Ignored Files" panel in Settings window:
  - Display combined rules (built-in defaults + user `.syncignore`)
  - Built-in defaults shown in gray/italic (not editable, labeled "System defaults")
  - User rules editable via:
    - "Add pattern" button → text input field (e.g., `*.log`, `build/`)
    - "Remove pattern" button for user-added rules
    - "Edit .syncignore" button → opens `.syncignore` file in system text editor for advanced users
  - Show preview: "Test a path" input — user types a file path, sees whether it would be ignored and by which rule
  - Pattern validation: show error if pattern syntax is invalid
- After editing rules, save to `.syncignore` in sync root → automatically synced to other clients

**Linux/macOS Considerations:**
- `.syncignore` parsing is cross-platform (text file with newline-delimited patterns)
- `FileSystemGlobbing` works identically on all platforms
- Linux-specific default: add `.directory` (KDE metadata file)
- macOS-specific default: add `.Spotlight-V100/`, `.Trashes/`, `._*` (resource forks)
- All platform-specific defaults compiled into the built-in list (present on all platforms — won't hurt if the file doesn't exist)

**Deliverables:**
- ☐ Client (Core): `SyncIgnoreParser` with `.gitignore`-compatible pattern matching
- ☐ Client (Core): Built-in default ignore patterns (OS junk, temp files, VCS dirs)
- ☐ Client (Core): `.syncignore` file loading from sync root
- ☐ Client (Core): Rule merging (built-in + user, user overrides)
- ☐ Client (SyncEngine): Ignore check before upload queue / download apply / periodic scan
- ☐ Client (SyncTray): "Ignored Files" settings panel with add/remove/edit
- ☐ Client (SyncTray): Pattern preview ("Test a path" feature)
- ☐ Client (SyncTray): Built-in defaults displayed as non-editable system rules

**Side:** Client only
**Complexity:** Medium

---

### 3.2 — Persistent Upload Sessions (Crash-Resilient Resumption)

**Approved Proposal:** 2.1

**Problem:** Client crash during upload = entire file must be re-uploaded.

**Scope:**

**Client (Windows11-TestDNC):**
- New `ActiveUploadSessionRecord` entity in `LocalStateDb`:
  ```
  Id (auto)
  SessionId (string — server's upload session ID)
  LocalPath (string)
  NodeId (Guid? — null for new files)
  TotalChunks (int)
  UploadedChunkHashes (string — JSON array of completed hashes)
  CreatedAt (DateTime)
  ```
- In `ChunkedTransferClient.UploadAsync()`:
  - After `InitiateUploadAsync()`: persist session to `ActiveUploadSessionRecord`
  - After each successful chunk: update `UploadedChunkHashes` in DB
  - After `CompleteUploadAsync()`: delete the `ActiveUploadSessionRecord`
  - On failure: leave the record (it will be resumed)
- In `SyncEngine.StartAsync()` (on startup):
  - Query `ActiveUploadSessionRecord` for incomplete sessions
  - For each: call `GET /upload/{sessionId}` to check server session status
    - If still valid → resume (upload only chunks not in `UploadedChunkHashes`)
    - If expired → re-initiate with same file → server dedup skips already-present chunks
    - If file has changed since session started → cancel old session, start fresh
- Clean up stale sessions older than 48 hours (server TTL is 24h, give buffer)

**Linux/macOS Considerations:** None — SQLite + HTTP, fully cross-platform.

**Deliverables:**
- ☐ Client: `ActiveUploadSessionRecord` entity + table
- ☐ Client: Session persistence in upload flow (create/update/delete)
- ☐ Client: Startup resume logic with server session validation
- ☐ Client: Stale session cleanup
- ☐ Client: Handle re-initiation when server session expired (dedup-aware)

**Side:** Client only
**Complexity:** Medium

---

### 3.3 — Locked File Handling

**Approved Proposal:** 2.3

**Problem:** Files locked by other processes cause immediate sync failure on Windows. Common with Office documents, databases, and other apps that hold exclusive locks.

**Scope:**

**Client (Windows11-TestDNC) — Tiered approach (try cheapest first):**

**Tier 1: Shared-read open (most files)**
- Replace `File.OpenRead()` with explicit share mode:
  ```csharp
  new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)
  ```
- Many apps (including modern Office) allow shared reads even while editing
- This alone will fix the majority of locked file situations at zero cost

**Tier 2: Retry with backoff (transient locks)**
- If Tier 1 throws `IOException` with `HResult == unchecked((int)0x80070020)` (sharing violation):
  - Retry up to 3 times with 2-second delays
  - Apps like antivirus scanners or indexers hold brief exclusive locks that release quickly

**Tier 3: Volume Shadow Copy / VSS (Windows-only, stubborn locks)**
- If Tier 2 exhausts retries and `OperatingSystem.IsWindows()`:
  - Use **AlphaVSS** library (MIT, mature .NET wrapper around COM VSS APIs) to create a volume shadow copy
  - Optimization: create ONE shadow copy per sync pass, read ALL locked files from it, release when done
  - Shadow copy creation: ~2-3 seconds, copy-on-write (negligible ongoing cost)
  - Read the file from the shadow path: `\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy{N}\{originalPath}`
  - SyncService runs as a Windows Service (SYSTEM account) — has the privileges required for VSS
  - Clean up shadow copy after sync pass completes (or on error/crash via `finally` block)
- Implementation:
  - New `ILockedFileReader` interface: `Task<Stream?> TryReadLockedFileAsync(string path, CancellationToken ct)`
  - `VssLockedFileReader` (Windows): manages shadow copy lifecycle
  - `NoOpLockedFileReader` (Linux/macOS): returns `null` (falls through to Tier 4)
  - Register via DI with platform detection

**Tier 4: Defer (last resort)**
- If all tiers fail (or non-Windows without VSS): mark file as `SyncStateTag.Deferred` in `LocalStateDb`
- Skip the file, log user-visible warning
- On next sync pass: retry deferred files from Tier 1 again
- Tray notification: "Skipped syncing `report.docx` — file is in use by another application. Will retry automatically."

- Add `SyncStateTag.Deferred` enum value

**Linux/macOS Considerations:**
- Linux: file locking is advisory. `File.OpenRead()` (Tier 1) generally succeeds even if another process has the file open. Risk is reading inconsistent data during active writes.
- Best-effort consistency check on Linux: compare file size before and after read. If changed during read, defer.
- macOS: similar to Linux (BSD advisory locks). APFS snapshots (`tmutil localsnapshot`) could serve as a macOS VSS equivalent — left for future macOS contributor.
- Tiers 1, 2, and 4 are fully cross-platform. Only Tier 3 (VSS) is Windows-specific.

**Deliverables:**
- ☐ Client: Tier 1 — `FileShare.ReadWrite | FileShare.Delete` on all file reads
- ☐ Client: Tier 2 — Sharing violation retry loop (3 attempts, 2s delay)
- ☐ Client: Tier 3 — `ILockedFileReader` interface + `VssLockedFileReader` (Windows, AlphaVSS)
- ☐ Client: Tier 3 — Per-sync-pass shadow copy lifecycle (create once, read many, release)
- ☐ Client: Tier 4 — `SyncStateTag.Deferred` state + deferred file tracking in `LocalStateDb`
- ☐ Client: Auto-retry of deferred files on subsequent sync passes
- ☐ Client: User notification when file is skipped due to lock
- ☐ Client: `NoOpLockedFileReader` for Linux/macOS (graceful fallback)

**Side:** Client only
**Complexity:** Medium

---

### 3.4 — Per-File Transfer Progress in Tray UI

**Approved Proposal:** 4.2

**Problem:** Users see "Syncing" but don't know which files or how far along transfers are.

**Scope:**

**Client (SyncService → SyncTray):**
- Wire `ChunkedTransferClient`'s existing `IProgress<TransferProgress>` to IPC event publishing
- New IPC event: `transfer-progress`:
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
      "percentComplete": 4.9,
      "speedBytesPerSec": 5242880
    }
  }
  ```
- New IPC event: `transfer-complete` (per file, with final stats)
- Throttle progress events: max 2 per second per file (avoid IPC flood)

**Client (SyncTray):**
- New `ActiveTransfersViewModel` — list of current transfers with:
  - File name, direction (↑/↓), progress bar, speed, ETA
- Show in expandable section of tray popup (or dedicated "Transfers" tab in Settings)
- Completed transfers: show briefly (5 seconds) then fade from list

**Linux/macOS Considerations:** None — IPC protocol + Avalonia UI, fully cross-platform.

**Deliverables:**
- ☐ Client (SyncService): Wire `TransferProgress` to IPC events
- ☐ Client (SyncService): `transfer-progress` and `transfer-complete` IPC events
- ☐ Client (SyncService): Progress event throttling (max 2/sec/file)
- ☐ Client (SyncTray): `ActiveTransfersViewModel` with progress bars
- ☐ Client (SyncTray): Speed and ETA calculation
- ☐ Client (SyncTray): Auto-dismiss completed transfers

**Side:** Client only
**Complexity:** Low-Medium

---

### 3.5 — Conflict Resolution UI

**Approved Proposal:** 4.3

**Problem:** Conflicts silently create copies that users may not notice or know how to resolve.

**Scope:**

**Client (SyncService + SyncTray):**
- New `ConflictRecord` entity in `LocalStateDb`:
  ```
  Id (auto)
  OriginalPath (string)
  ConflictCopyPath (string)
  NodeId (Guid)
  LocalModifiedAt (DateTime)
  RemoteModifiedAt (DateTime)
  DetectedAt (DateTime)
  ResolvedAt (DateTime?) — null until resolved
  Resolution (string?) — "kept-local", "kept-server", "kept-both", "merged",
                         "auto-identical", "auto-fast-forward", "auto-merged",
                         "auto-newer-wins", "auto-append"
  BaseContentHash (string?) — hash of common ancestor version (for three-way merge)
  AutoResolved (bool) — true if resolved without user intervention
  ```
- `ConflictResolver`: save conflict record when creating conflict copy, raise `ConflictDetected` event (already done)
- New IPC commands: `list-conflicts`, `resolve-conflict`
- Tray icon: show badge/different color when unresolved conflicts exist

**Client (Core) — Auto-Resolution Engine (runs before user ever sees a conflict):**

The goal is to resolve the vast majority of conflicts automatically, so users only deal with genuinely ambiguous situations. Auto-resolution runs as a pipeline of strategies, tried in order. If any strategy produces a clean resolution, the conflict is resolved silently (logged, but no user action needed).

- **Strategy 1: Identical content (hash match)**
  - Both sides changed the file, but the final content is byte-identical (same SHA-256)
  - Example: two clients both ran the same code formatter, or both saved the same edit
  - Resolution: keep either (they're the same), mark `Resolution = "auto-identical"`
  - Confidence: 100% — guaranteed safe

- **Strategy 2: One side unchanged (fast-forward)**
  - Compare local and server content hashes against the base version (common ancestor)
  - If only one side actually changed from the base → that side wins (the other is stale)
  - Example: user edited on laptop, but desktop client was just slow to sync (hadn't changed the file)
  - Resolution: keep the changed version, mark `Resolution = "auto-fast-forward"`
  - Confidence: 100% — equivalent to a git fast-forward merge
  - Requires: base version hash stored on `ConflictRecord.BaseContentHash`

- **Strategy 3: Non-overlapping text merge (three-way merge)**
  - For text-based files only (see mergeable types list below)
  - Perform three-way diff using base version:
    1. Diff base → local = local changes (set of hunks)
    2. Diff base → server = server changes (set of hunks)
    3. Check for overlapping line ranges between the two hunk sets
    4. If NO overlaps: merge is clean — apply both sets of changes to base
  - Example: user A edited function at line 10, user B edited function at line 200
  - Resolution: write merged content, upload as new version, mark `Resolution = "auto-merged"`
  - Confidence: High — same algorithm git uses for clean merges
  - Requires: base version content (fetched from server version history)
  - Falls through to manual if: any hunks overlap, or base version unavailable

- **Strategy 4: Timestamp + single-user heuristic**
  - If the conflict is between two devices owned by the same user (same `UserId`):
    - The more recently modified version is likely the intended one
    - Auto-resolve if modification timestamps differ by more than 5 minutes (not a race condition)
  - Resolution: keep newer, mark `Resolution = "auto-newer-wins"`
  - Confidence: Medium — correct in most single-user scenarios
  - Configurable: user can disable this via settings (`"conflictResolution": { "autoNewerWins": true }`)
  - **UI:** Checkbox in Settings → Sync → Conflict Resolution: "Automatically keep the newer version when the same account edits on multiple devices" (checked by default)
  - Does NOT apply to multi-user conflicts (different users editing same file — always manual)

- **Strategy 5: Append-only file detection**
  - Detect if one version is a prefix/subset of the other (common with log files, journals, CSV data)
  - **Single-user:** if server content starts with local content (or vice versa), take the longer version
  - **Multi-user:** if both versions share the same base prefix but each appended different content, concatenate both appendages onto the base (local appendage first, then server appendage, separated by a newline)
    - Only auto-resolve if the shared prefix is ≥ 90% of the shorter version's length (high confidence the file is genuinely append-only)
    - If shared prefix is < 90%: not a clean append pattern → fall through to manual
  - Resolution: mark `Resolution = "auto-append"` (single-user) or `"auto-append-combined"` (multi-user)
  - Confidence: High for single-user; medium-high for multi-user with ≥ 90% prefix match

- **Auto-resolution pipeline flow:**
  ```
  ConflictDetected
    → Strategy 1 (identical?) → resolved ✓
    → Strategy 2 (fast-forward?) → resolved ✓
    → Strategy 3 (clean text merge?) → resolved ✓
    → Strategy 4 (newer wins, same user?) → resolved ✓
    → Strategy 5 (append-only?) → resolved ✓
    → All strategies failed → create ConflictRecord → notify user
  ```
- Auto-resolved conflicts logged with strategy name, confidence level, and file details
- Tray notification for auto-resolutions: subtle toast "Auto-resolved conflict: `config.json` (clean merge)" — dismisses automatically
- Auto-resolution history visible in "Conflicts" → "History" tab (user can review and undo within 24 hours)
- **Undo for auto-resolutions:** both versions kept in version history on server, so user can always revert if auto-resolution was wrong

**Client (SyncTray):**
- New "Conflicts" panel (accessible from tray menu or settings):

**Conflict awareness — make it impossible to ignore:**
- **Tray icon state change:** Replace normal cloud icon with a warning variant (cloud + orange/red exclamation triangle) when unresolved conflicts exist. This is the primary visual cue — users who glance at their system tray should immediately see something is wrong.
- **Badge count:** Overlay conflict count on tray icon (e.g., red circle with "3")
- **Persistent toast notification** on first detection: "Sync conflict detected: `report.docx` — two versions exist. Click to resolve." Does NOT auto-dismiss — stays until clicked or explicitly dismissed.
- **Tooltip change:** Normal tooltip "DotNetCloud — All files synced" → "DotNetCloud — ⚠ 3 unresolved conflicts" with orange/red styling
- **Tray menu highlight:** "Conflicts (3)" menu item shown at TOP of tray context menu (above "Open folder", "Settings", etc.) with warning icon, only when conflicts exist
- **Recurring reminder:** If conflicts remain unresolved for > 24 hours, re-show toast notification once per day: "You still have 3 unresolved file conflicts. Oldest: 2 days ago."
- **First-run education:** On the very first conflict a user ever encounters, show a slightly longer notification explaining what a conflict is and how to resolve it: "Two versions of `report.docx` exist — one from this device and one from the server. Click to choose which to keep or merge them."

**Conflicts panel contents:**
  - List of unresolved conflicts with:
    - File name and path
    - "Local version" timestamp + size
    - "Server version" timestamp + size
    - Action buttons:
      - **Keep server version** — delete conflict copy, keep original (already has server version)
      - **Keep local version** — upload conflict copy as new version, delete conflict copy, rename back
      - **Keep both** — mark as resolved, leave both files
      - **Merge** (text files only) — opens three-pane merge editor (see below)
      - **Open folder** — open containing directory in file manager
  - Resolved conflicts: shown in "History" tab (last 30 days)
  - Count badge: "3 conflicts" on tray icon tooltip

**Client (SyncTray) — Three-Pane Merge Editor (text files only):**
- Available when the conflicting file is a text-based format (determined by extension + content sniffing)
- **Mergeable file types (line-based diffing works well):** `.txt`, `.md`, `.json`, `.yaml`, `.yml`, `.csv`, `.tsv`, `.html`, `.css`, `.js`, `.ts`, `.cs`, `.py`, `.java`, `.c`, `.cpp`, `.h`, `.sh`, `.ps1`, `.sql`, `.ini`, `.cfg`, `.conf`, `.toml`, `.env`, `.log`, `.gitignore`, `.dockerignore`, and other plain-text extensions
- **Mergeable with XML-aware engine (XML family):** `.xml`, `.csproj`, `.fsproj`, `.props`, `.targets`, `.xaml`, `.svg`, `.xslt`
  - Uses **`Microsoft.XmlDiffPatch`** (Microsoft-maintained NuGet) for structural tree-based diffing instead of line-based
  - **Three-way tree merge:**
    1. Parse base, local, and server into DOM trees
    2. Diff base → local = tree operations (node added/removed/moved, attribute changed, text changed)
    3. Diff base → server = tree operations
    4. Check for conflicting tree edits (same node/attribute modified differently by both sides)
    5. If no tree-level conflicts → apply both operation sets to produce merged result (auto-merge)
    6. If conflicts → show conflicting **nodes** (not lines) in the merge editor
  - **Advantages over line-based:** handles attribute reordering, whitespace/indentation changes, namespace prefix differences, and node moves without false conflicts
  - **Merge editor XML mode:** when viewing an XML conflict, the editor highlights conflicting nodes in a tree view alongside the text view. User can click a node to accept the local or server version of that specific node.
  - **Post-merge validation:** `XDocument.Parse()` on the merged result — blocks saving if the result isn't well-formed XML
  - **In-editor help panel:** When the merge editor opens for an XML file, show a collapsible "How XML merging works" sidebar with:
    - "XML files are merged by comparing their structure (elements, attributes, text content), not line-by-line. This means formatting and attribute order changes won't cause false conflicts."
    - "Conflicting nodes are highlighted in yellow. Click a node to choose the local or server version."
    - "If both sides added different child elements to the same parent, both are kept (no conflict)."
    - "If both sides changed the same attribute or text content to different values, that's a real conflict — pick one or edit the result manually."
    - "The merged result must be valid XML. If it isn't, you'll see an error and won't be able to save until it's fixed."
    - Link to full docs: "Learn more about conflict resolution" → opens user guide in browser
  - **Help panel behavior:** shown by default on first XML merge, then collapsed with a "?" button to re-open. User preference remembered.
- **Non-mergeable (binary/structured):** Images, Office docs (`.docx`, `.xlsx`, `.pptx`), PDFs, archives, media files, databases — these show only the Keep/Both buttons
- **Layout:** Three vertical panes + result pane:
  - **Left pane:** "Local version" (your changes) — read-only
  - **Center pane:** "Base version" (common ancestor, if available) — read-only
  - **Right pane:** "Server version" (remote changes) — read-only
  - **Bottom pane:** "Merged result" — editable, starts with an auto-merged attempt
- **Diff engine:**
  - Use a line-level diff algorithm (e.g., `DiffPlex` library — MIT, .NET native, works with Avalonia)
  - Syntax highlighting via line-level coloring: green = added, red = removed, yellow = conflict regions
  - Auto-merge non-conflicting hunks (changes that don't overlap)
  - Mark true conflicts (both sides changed the same lines) with `<<<<<<<` / `=======` / `>>>>>>>` markers in the merged result for manual resolution
- **Interactions:**
  - Click a hunk in left or right pane → applies that version to the merged result
  - "Accept all local" / "Accept all server" quick buttons
  - "Reset merge" — re-runs auto-merge from scratch
  - "Save & resolve" — writes merged result to disk, marks conflict as resolved with `Resolution = "merged"`
  - "Cancel" — returns to conflict list without changes
- **Base version strategy:**
  - If server provides version history: fetch the common ancestor version (last version both sides agreed on)
  - If no base available: two-pane mode (local vs server), auto-merge is best-effort without a base
  - Store `BaseContentHash` on `ConflictRecord` if available
- **Window:** Opens as a separate resizable window (not embedded in settings), sized to ~80% of screen

**Linux/macOS Considerations:** 
- "Open folder" action: `xdg-open` (Linux), `open` (macOS), `explorer.exe` (Windows) — already handled by Avalonia/platform detection
- File operations (rename, delete) are cross-platform via `System.IO`
- Three-pane merge editor: Avalonia renders identically on all platforms. `DiffPlex` is a pure .NET library — fully cross-platform.

**Deliverables:**
- ☐ Client (Core): `ConflictRecord` entity + table in `LocalStateDb` (with `BaseContentHash`, `AutoResolved`)
- ☐ Client (Core): `ConflictResolver` persists records to DB
- ☐ Client (Core): Auto-resolution pipeline (5 strategies: identical, fast-forward, text merge, newer-wins, append-only)
- ☐ Client (Core): Three-way diff/merge algorithm for clean text merges (using `DiffPlex`)
- ☐ Client (Core): File type classification (mergeable text vs non-mergeable binary)
- ☐ Client (Core): Auto-resolution undo support (24-hour window via server version history)
- ☐ Client (SyncService): `list-conflicts` and `resolve-conflict` IPC commands
- ☐ Client (SyncTray): "Conflicts" panel with conflict list and action buttons
- ☐ Client (SyncTray): Tray icon warning state (cloud + exclamation) when conflicts exist
- ☐ Client (SyncTray): Badge count overlay on tray icon
- ☐ Client (SyncTray): Persistent (non-auto-dismiss) toast on conflict detection
- ☐ Client (SyncTray): "Conflicts (N)" at top of tray context menu with warning icon
- ☐ Client (SyncTray): 24-hour recurring reminder for stale unresolved conflicts
- ☐ Client (SyncTray): First-conflict educational notification
- ☐ Client (SyncTray): Three-pane merge editor window (local | base | server + merged result)
- ☐ Client (SyncTray): Diff engine integration (`DiffPlex`) with auto-merge and conflict markers
- ☐ Client (SyncTray): XML-aware merge engine (`Microsoft.XmlDiffPatch`) with tree-level diffing
- ☐ Client (SyncTray): XML merge editor node-level conflict view + in-editor help panel
- ☐ Client (SyncTray): Hunk-level accept/reject interactions
- ☐ Client (SyncTray): Conflict history (last 30 days)

**Side:** Client only
**Complexity:** Medium

---

### 3.6 — Idempotent Operations

**Approved Proposal:** 2.5

**Problem:** Crash after upload but before local DB update → duplicate version created on server.

**Scope:**

**Client (Windows11-TestDNC):**
- In `SyncEngine.ApplyLocalChangesAsync()`, before executing a `PendingUpload`:
  1. If `NodeId` is known (existing file update): call `GetNodeAsync(nodeId)` to get current `ContentHash`
  2. Compute local file's content hash
  3. If hashes match → server already has this version. Skip upload, update `LocalStateDb`
  4. If hashes differ → proceed with upload as normal
- For new files (no `NodeId`): proceed with upload (name collision handled by server)
- Additional optimization: `InitiateUploadAsync` already returns which chunks exist. If ALL chunks in manifest exist, `CompleteUploadAsync()` is essentially free (no chunk transfers), so this is already partially idempotent. Formalize and document this behavior.

**Linux/macOS Considerations:** None.

**Deliverables:**
- ☐ Client: Pre-upload content hash comparison for existing files
- ☐ Client: Skip upload when server hash matches local hash
- ☐ Client: Log "skipped upload (already synced)" for visibility

**Side:** Client only
**Complexity:** Low

---

## Batch 4 — Cross-Platform Hardening (Before Linux Client Launch)

**Goal:** Ensure sync works correctly when Linux and Windows clients share the same server account.

### 4.1 — Case-Sensitivity Conflict Detection

**Approved Proposal:** 5.5

**Problem:** Linux allows `Report.docx` and `report.docx` in the same folder. Windows treats them as the same file → data loss on sync.

**Scope:**

**Server (mint22):**
- In `FileService.CreateFolderAsync()`, `RenameAsync()`, and `ChunkedUploadService.CompleteUploadAsync()` (new file creation):
  - Query same parent for case-insensitive name match:
    ```sql
    WHERE ParentId = @parentId AND LOWER(Name) = LOWER(@newName) AND Id != @currentId
    ```
  - If match found: return error `409 Conflict` with message: `"A file with a case-insensitively matching name '{existingName}' already exists. This would cause conflicts on case-insensitive file systems (Windows, macOS)."`
  - Configurable: `"FileSystem:EnforceCaseInsensitiveUniqueness": true` in `appsettings.json` (default: true)
  - Admin can disable if all clients are on case-sensitive file systems

**Client (Windows11-TestDNC):**
- Before applying remote changes:
  - Build set of existing file names in target directory (case-insensitive comparison on Windows)
  - If incoming file name matches existing file case-insensitively but not case-sensitively: rename the incoming file with `(case conflict)` suffix
  - Log the conflict clearly
- Windows-specific: `StringComparer.OrdinalIgnoreCase` for file name comparison
- Linux-specific: `StringComparer.Ordinal` (case-sensitive — conflicts only detected server-side)

**macOS Consideration:** macOS uses HFS+ or APFS which is case-insensitive by default (like Windows). Same client logic as Windows applies. A future macOS contributor would use `OrdinalIgnoreCase`.

**Deliverables:**
- ☐ Server: Case-insensitive uniqueness check on file creation/rename
- ☐ Server: `409 Conflict` response with clear error message
- ☐ Server: Configurable enforcement flag in `appsettings.json`
- ☐ Client: Case conflict detection before applying remote changes
- ☐ Client: Automatic rename with `(case conflict)` suffix

**Side:** Both
**Complexity:** Low-Medium

---

### 4.2 — File Permission Metadata Sync

**Approved Proposal:** 5.2

**Problem:** Linux executable scripts lose execute bit when synced through Windows. Read-only config files become writable. Linux-to-Linux transfers through the server should preserve full POSIX permissions — Linux must not be a second-class citizen.

**Scope:**

**Server (mint22):**
- Add `PosixMode` column (nullable `int`) to `FileNode` model — stores the full `UnixFileMode` bitmask (e.g., `0o755` = `493`)
- Add `PosixOwnerHint` column (nullable `string`) to `FileNode` — stores `"user:group"` as a hint (not enforced, since UIDs differ across machines — see below)
- Include `PosixMode` and `PosixOwnerHint` in `FileNodeDto`, `SyncChangeDto`, `SyncTreeNodeDto`
- Include in gRPC `FileNodeInfo` message
- Server-side enforcement: if the server itself runs on Linux, apply `PosixMode` to chunk storage files when writing (defense-in-depth — chunks shouldn't be executable regardless)
- **Permission history:** `PosixMode` stored per `FileVersion`, so version restore also restores permissions

**Client (Linux):**
- **On upload:**
  - Read `File.GetUnixFileMode()` → send as `PosixMode`
  - Read file owner/group via `stat` interop or `/proc` → send as `PosixOwnerHint` (e.g., `"benk:developers"`)
  - Detect and preserve: execute bits, setuid/setgid (stored but applied with caution — see below), sticky bit, read-only
- **On download:**
  - Apply `File.SetUnixFileMode(mode)` with the stored `PosixMode`
  - If `PosixMode` is `null` (file uploaded from Windows): apply sensible Linux default (`0o644` for files, `0o755` for directories)
  - **Owner/group hint handling:**
    - If `PosixOwnerHint` matches a local user/group → apply via `chown` (requires appropriate privileges)
    - If no match (different machine, different users) → keep current user ownership, log info: "Owner hint `benk:developers` not applicable on this machine — using current user"
    - Never fail a sync because of ownership mismatch
  - **Special bits policy:**
    - `setuid`/`setgid` bits: stored in DB for completeness but **NOT applied on download** by default (security risk — a file from one machine shouldn't get elevated privileges on another). Logged as info: "setuid/setgid bit present but not applied for security. Use `chmod u+s` manually if needed."
    - Configurable override: `"filePermissions": { "applySetuidBits": false }` in `sync-settings.json` — advanced users who understand the risk can enable it
- **Permission change detection:**
  - `SyncEngine` periodic scan: compare current `UnixFileMode` against `LocalStateDb` stored mode
  - If permissions changed (but content didn't): queue a metadata-only sync operation (no re-upload of file content, just update `PosixMode` on server)
  - `FileSystemWatcher` doesn't fire for permission changes on Linux — periodic scan is the only detection mechanism
- **Directory permissions:** directories also carry `PosixMode` — ensure `0o755` minimum on download so the directory is traversable

**Client (Windows):**
- On upload: send `PosixMode = null`, `PosixOwnerHint = null`
- On download: ignore `PosixMode` and `PosixOwnerHint` entirely (don't crash if present, just don't apply)
- **Preservation rule:** if a Windows client uploads a new version of a file that previously had `PosixMode` set (by a Linux client), the server retains the **previous version's** `PosixMode` on the new `FileVersion` — so Linux permissions survive a round-trip through Windows editing

**Client (macOS — future):** Same as Linux. macOS uses BSD permissions which map directly to POSIX mode bits.

**EF Migration:** 
- Add nullable `PosixMode int?` column to `FileNode`. Existing rows default to `null`.
- Add nullable `PosixOwnerHint string?` column to `FileNode`. Existing rows default to `null`.
- Add `PosixMode int?` to `FileVersion` for per-version permission tracking.

**Deliverables:**
- ☐ Server: `PosixMode` + `PosixOwnerHint` columns on `FileNode` + `FileVersion` + migration
- ☐ Server: `PosixMode` + `PosixOwnerHint` in all DTOs and gRPC messages
- ☐ Server: Preserve previous `PosixMode` when Windows client uploads new version
- ☐ Client (Core): `PosixMode` + `PosixOwnerHint` properties in upload/download DTOs
- ☐ Client (Windows): Pass `null` on upload, ignore on download, preserve on re-upload
- ☐ Client (Linux): Read/send full `UnixFileMode` + owner hint on upload
- ☐ Client (Linux): Apply `UnixFileMode` on download with sensible defaults for Windows-originated files
- ☐ Client (Linux): Owner/group hint best-effort application with graceful fallback
- ☐ Client (Linux): setuid/setgid safety policy (store but don't apply by default)
- ☐ Client (Linux): Permission change detection in periodic scan (metadata-only sync)
- ☐ Client (Linux): Directory permission enforcement (minimum `0o755`)

**Side:** Both (server schema + client platform logic)
**Complexity:** Low

---

### 4.3 — Symbolic Link Policy

**Approved Proposal:** 5.3

**Problem:** FileSystemWatcher follows symlinks on Linux, causing loops or syncing unintended directories. Naively following symlinks would duplicate content on the server and risk infinite recursion with circular links.

**Scope:**

**Default behavior: Ignore symlinks (safe default)**

**Client (all platforms):**
- In `SyncEngine` file watcher event handlers, before processing any file/directory:
  1. Check `FileAttributes.ReparsePoint` (Windows) or `FileSystemInfo.LinkTarget != null` (.NET 7+)
  2. If symlink detected: skip, log info: "Skipped symbolic link: {path} → {target}"
- In periodic scan: similarly skip symlinks when enumerating directories
- Document symlink behavior in user docs

**Opt-in: Sync symlinks as metadata (no duplicate storage)**

Configurable via `sync-settings.json`: `"symlinks": { "mode": "ignore" }` (default) or `"mode": "sync-as-link"`

When `"sync-as-link"` is enabled:
- **Store the link, not the target** — symlinks are synced as a lightweight metadata entry, not a copy of what they point to. Zero duplicate storage, zero recursion. Same approach git uses.
- **Server model:**
  - Add `NodeType` value: `SymbolicLink` (alongside existing `File`, `Folder`)
  - Add `LinkTarget` column (nullable `string`) on `FileNode` — stores the relative target path (e.g., `../shared/config.json`)
  - Symlink `FileNode` has NO chunks, NO content hash, NO file version — it's pure metadata
  - Storage cost: one DB row per symlink, no blob storage consumed
- **Upload rules (client):**
  - Detect symlink → read `LinkTarget` → validate → send as metadata-only create/update
  - **Only sync relative symlinks** that resolve to a path within the sync root. Reject:
    - Absolute symlinks (`/usr/bin/python3`) — machine-specific, meaningless on another machine
    - Relative symlinks that escape the sync root (`../../etc/passwd`) — security: path traversal
    - Circular chains — detect by following the chain (max depth 20) before syncing
  - Rejected symlinks: logged with reason, skipped (same as ignore mode)
- **Download rules (client):**
  - Receive `NodeType.SymbolicLink` + `LinkTarget` from server
  - **Linux/macOS:** recreate as actual symlink via `File.CreateSymbolicLink(path, target)`
  - **Windows:** recreate as symlink IF running with admin/developer mode privilege (Windows requires `SeCreateSymbolicLinkPrivilege`). If unprivileged: skip, log warning: "Cannot create symlink `{path}` — requires developer mode or admin privileges on Windows. The target file `{target}` will still sync normally."
  - Before creating: validate that `LinkTarget` doesn't escape the sync root (defense-in-depth — don't trust server blindly)
- **Change detection:**
  - If a symlink's target changes (re-pointed to a different file): detect in periodic scan, sync updated `LinkTarget`
  - If a symlink is replaced with a regular file (or vice versa): detect `NodeType` change, sync appropriately
- **Conflict handling:** Symlink conflicts (both sides changed the target) → auto-resolve with newer-wins (symlinks are cheap metadata, not worth a merge editor)

**UI:**
- Settings → Sync: "Symbolic links" dropdown: "Ignore (default)" / "Sync as links"
- Tooltip: "When set to 'Sync as links', symbolic links are synced as pointers, not copies. No duplicate storage. Only works for relative links within your sync folder."

**Linux/macOS Considerations:** This is primarily a Linux/macOS concern. Windows symlinks require admin privileges and are rare. Implementation is identical across platforms using .NET API (`FileSystemInfo.LinkTarget`, `File.CreateSymbolicLink()`).

**Deliverables:**
- ☐ Client: Symlink detection in FileSystemWatcher handlers and periodic scan
- ☐ Client: Default ignore behavior with clear logging
- ☐ Client: Opt-in `"sync-as-link"` mode in `sync-settings.json`
- ☐ Client: Symlink validation (relative-only, within sync root, no circular chains)
- ☐ Client: Metadata-only upload for symlinks (no content/chunks)
- ☐ Client: Symlink recreation on download (Linux native, Windows privilege-aware)
- ☐ Client: Settings UI dropdown for symlink mode
- ☐ Server: `NodeType.SymbolicLink` enum value
- ☐ Server: `LinkTarget` column on `FileNode` + migration
- ☐ Server: Symlink-aware DTOs and gRPC messages

**Side:** Client only
**Complexity:** Low

---

### 4.4 — inotify Watch Limit + inode Awareness (Linux/macOS)

**Approved Proposal:** 5.4

**Problem:** Linux inotify has a per-user watch limit (default 8192). Large sync folders can exceed this, causing silent failures. Affects both the client (watching sync folder) and the server (watching storage directories). Additionally, running out of inodes on ext4/XFS prevents new file creation even when disk space remains — a silent killer for storage-heavy servers.

**Scope:**

**Client (Linux-specific):**
- On `SyncEngine.StartAsync()`, if `OperatingSystem.IsLinux()`:
  1. Read `/proc/sys/fs/inotify/max_user_watches` (current limit)
  2. Read `/proc/sys/fs/inotify/max_user_instances` (max watcher instances)
  3. Track actual watches in use: read `/proc/{pid}/fdinfo/` or count active `FileSystemWatcher` instances internally
  4. Log on startup: "inotify: limit={limit}, instances={instances}, estimated_needed={estimate}"
  5. Estimate required watches: count subdirectories in sync folder
  6. **Compute dynamic target limit:**
     - `target = max(524288, estimated_needed * 1.5)` — at least 524K, or 150% of what's actually needed (headroom for folder growth)
     - **RAM-aware cap:** `max_safe = (total_ram_bytes * 0.05) / 1024` — don't allocate more than ~5% of system RAM to inotify watches (~1KB per watch in kernel memory)
     - `final_target = min(target, max_safe)`
     - If `estimated_needed > max_safe`: warn user that their folder tree is too deep for available RAM, graceful fallback to periodic scan for excess directories
  7. If current limit < `final_target`:
     - Show actionable notification: "File watching needs more system resources. Current limit: {current}. Needed: {final_target} (you have {subdirCount} folders). **[Fix automatically]** [Dismiss]"
     - **"Fix automatically" action:**
       - Prompt for sudo/polkit authentication (standard Linux privilege escalation UI)
       - Write `fs.inotify.max_user_watches={final_target}` to `/etc/sysctl.d/50-dotnetcloud.conf`
       - Run `sysctl --system` to apply immediately (no reboot required)
       - Log the change and notify: "Watch limit increased to {final_target}. File watching is now fully operational."
     - If user dismisses: fall back to increased periodic scan (see below), remember dismissal for 7 days (don't nag every startup)
  8. If current limit is sufficient for estimated need: proceed silently
  9. **Re-evaluate on folder growth:** if a new large directory tree is added to the sync folder (detected via FileSystemWatcher or periodic scan), re-count and re-check. If needed watches now exceed the current limit → offer auto-fix again with the new target.
- If `FileSystemWatcher` raises `Error` event (watch limit hit at runtime):
  - Log error
  - Fall back to increased periodic scan frequency (30 seconds instead of 5 minutes)
  - Re-compute dynamic target based on current folder count
  - Show persistent notification: "File watching limited — changes may take up to 30 seconds to detect. **[Fix automatically to {new_target}]** [Dismiss]"
  - Same auto-fix flow as above if user clicks "Fix automatically"
- **Graceful degradation principle:** DotNetCloud must NEVER fail due to OS-level limits. If inotify watches are exhausted and the user declines to fix:
  - Switch affected directories to periodic scan (30s interval) — sync still works, just with slightly higher latency
  - Log which directories are monitored via inotify vs periodic scan
  - Continue operating without data loss or errors

**Server (mint22 — Linux-specific):**
- On server startup, if `OperatingSystem.IsLinux()`:
  1. Read `/proc/sys/fs/inotify/max_user_watches` (current limit)
  2. Read `/proc/sys/fs/inotify/max_user_instances` (max watcher instances)
  3. Log on startup: "inotify: limit={limit}, instances={instances}"
  4. Compute dynamic target same as client: `max(524288, estimated_needed * 1.5)` capped by RAM
  5. If current < target: log warning with auto-fix command: "inotify watch limit is {current}, recommended: {target}. Run: `echo 'fs.inotify.max_user_watches={target}' | sudo tee /etc/sysctl.d/50-dotnetcloud.conf && sudo sysctl --system`"
  6. Include in health check endpoint (`/health/ready`): report `inotify_watches` status as `degraded` if below target
- **Install script (`install.sh`):** compute and set appropriate `max_user_watches` during server installation (with user confirmation in interactive mode, silently in unattended mode)
- Server doesn't use `FileSystemWatcher` heavily today (content-addressed storage doesn't need watching), but this future-proofs for:
  - Module hot-reload (watching module directories)
  - Server-side file scanning (ClamAV integration watching upload temp dir)
  - Direct storage import features

**Windows/macOS Considerations:**
- **inotify watches:** Not applicable on non-Linux platforms. macOS uses FSEvents (no watch limit). Windows uses ReadDirectoryChangesW (no per-directory limit).
- **inodes:** macOS APFS uses dynamic inode allocation — no fixed limit, so inode exhaustion is not a practical concern. HFS+ (legacy) had fixed limits but is rarely used. Windows NTFS uses MFT entries which are dynamically allocated. **inode monitoring is Linux-specific** (ext4, XFS, Btrfs all have fixed inode limits set at filesystem creation).

**inode Monitoring (Linux — Client + Server):**

Running out of inodes means no new files can be created even with plenty of disk space. For a cloud storage system, this is critical.

**Client (Linux):**
- On `SyncEngine.StartAsync()`, if `OperatingSystem.IsLinux()`:
  - Run `statvfs()` (via P/Invoke or `df -i` parsing) on the sync folder's mount point
  - Read: total inodes, used inodes, free inodes, percentage used
  - Log on startup: "inodes: total={total}, used={used}, free={free} ({percentUsed}%)"
  - If free inodes < 5%: show warning notification: "Your filesystem is running low on inodes ({free} remaining, {percentUsed}% used). New files may fail to sync. Consider cleaning up small files or expanding the filesystem."
  - If free inodes < 1%: show critical notification + log error
- During sync: if a file write fails with `ENOSPC` (`IOException`) but disk space is available → detect inode exhaustion, show specific notification: "Cannot create new files — filesystem inode limit reached. Disk has {freeSpace} available but no room for new file entries."

**Server (Linux):**
- On server startup, if `OperatingSystem.IsLinux()`:
  - Run `statvfs()` on `DOTNETCLOUD_DATA_DIR` mount point
  - Read + log: total inodes, used, free, percentage
  - If free inodes < 10%: log warning
  - If free inodes < 2%: log error
- **Health check:** include inode status in `/health/ready`:
  - `healthy`: free inodes ≥ 10%
  - `degraded`: free inodes 2–10%
  - `unhealthy`: free inodes < 2%
- **Periodic monitoring:** check inode status every 30 minutes during runtime (not just startup)
  - If status transitions to `degraded` or `unhealthy`: log alert-level message
  - Admin notification (future): email/webhook alert when inodes critically low
- **Chunk storage consideration:** content-addressed storage creates many small files (one per chunk). Large deployments can burn through inodes fast. Document recommended `mkfs` options: `mkfs.ext4 -i 8192` (one inode per 8KB — double the default density) for the storage partition.

**macOS (future):**
- APFS: inode exhaustion is not a concern (dynamic allocation). Skip inode checks on macOS.
- If somehow running on HFS+: same `statvfs()` approach as Linux would work.

**Deliverables:**
- ☐ Client: inotify limit, instances, and usage tracking on Linux startup
- ☐ Client: Startup log with current limit, instances, and estimated need
- ☐ Client: Actionable notification with "Fix automatically" button
- ☐ Client: Automatic `sysctl.d` configuration with polkit privilege escalation
- ☐ Client: Dismissal memory (don't re-nag for 7 days)
- ☐ Client: Fallback to fast periodic scan on `FileSystemWatcher.Error`
- ☐ Client: inode usage check on Linux startup via `statvfs()`
- ☐ Client: Warning/critical notifications for low inode availability
- ☐ Client: Detect inode exhaustion on `ENOSPC` write failures
- ☐ Server: inotify limit + instances tracking on Linux startup with log output
- ☐ Server: inode usage check on Linux startup with threshold logging
- ☐ Server: inode status in `/health/ready` (healthy/degraded/unhealthy)
- ☐ Server: Periodic inode monitoring every 30 minutes
- ☐ Server: Health check `degraded` status if inotify limit is low
- ☐ Server: `install.sh` sets recommended inotify limit during installation
- ☐ Docs: `sysctl` instructions in Linux install/setup documentation
- ☐ Docs: Recommended `mkfs` inode density for storage partitions

**Side:** Both (Linux-specific)
**Complexity:** Low-Medium

---

### 4.5 — Path Length + Filename Limit Handling

**Problem:** Windows has a historical 260-character total path limit (`MAX_PATH`). Linux allows 4,096-character paths and 255-character filenames. macOS allows 255-character filenames with ~1,024-character paths. Files created on Linux with deep nesting or long names will fail to sync to Windows clients.

**Filesystem limits reference:**

| OS/FS | Max filename | Max full path | Notes |
|-------|-------------|---------------|-------|
| Windows (NTFS, default) | 255 chars | 260 chars (`MAX_PATH`) | Includes drive letter, separators, null terminator |
| Windows (NTFS, long path enabled) | 255 chars | 32,767 chars | Requires Windows 10 1607+, registry/group policy opt-in |
| Linux (ext4/XFS/Btrfs) | 255 **bytes** (not chars — UTF-8 multi-byte matters) | 4,096 bytes | Per-component 255B limit |
| macOS (APFS) | 255 chars | ~1,024 chars | HFS+ was 255 chars |

**Scope:**

**Client (Windows) — Enable long paths + graceful fallback:**
- **App manifest:** Ship with `<longPathAware>true</longPathAware>` in the application manifest — .NET 8+ respects this. Allows DotNetCloud itself to handle paths > 260 chars.
- **First-run check:** On startup, check `HKLM\SYSTEM\CurrentControlSet\Control\FileSystem\LongPathsEnabled`:
  - If `0` (disabled, the default): show one-time notification: "Windows limits file paths to 260 characters. Files with longer paths from other devices won't sync. **[Enable long paths]** [Dismiss]"
  - **"Enable long paths" action:** 
    - Request UAC elevation
    - Set registry value `LongPathsEnabled = 1` (DWORD)
    - Log the change: "Windows long path support enabled. Paths up to 32,767 characters are now supported."
    - No reboot required for new processes; existing Explorer windows may need restart
  - If user dismisses: remember, don't nag again. Fall back to path truncation strategy (see below).
- **Path-too-long handling (when long paths NOT enabled):**
  - Before writing a file, check if `syncRoot.Length + relativePath.Length > 259`:
    - Try `\\?\` prefix: `\\?\C:\Users\benk\Documents\synctray\very\deep\path\file.txt` — bypasses MAX_PATH for NTFS operations (supported by .NET `FileStream` APIs)
    - If `\\?\` prefix also fails (some APIs don't support it): mark file as `SyncStateTag.PathTooLong` in `LocalStateDb`
    - Log warning: "Cannot sync `{relativePath}` — path exceeds Windows 260-character limit ({actualLength} chars). Enable long paths in Windows settings or shorten the path on the source device."
    - Tray notification (first occurrence only): "Some files couldn't sync because their paths are too long for Windows. **[Enable long paths]** [Learn more]"

**Client (Linux/macOS) — Filename byte-length awareness:**
- Linux ext4/XFS: max 255 **bytes** per filename component, not 255 characters. A filename with emoji or CJK characters (3–4 bytes each in UTF-8) can exceed 255 bytes while appearing < 255 chars.
- Before writing a file, check `Encoding.UTF8.GetByteCount(filename) > 255`:
  - If too long: truncate the filename to fit 255 bytes while preserving the extension and UTF-8 validity
  - Append a short hash suffix to avoid collisions: `{truncated_name}~{hash4}.{ext}`
  - Log warning: "Filename `{original}` exceeds 255-byte filesystem limit. Renamed to `{truncated}`."
  - Store the original name in `LocalStateDb` so the server's canonical name is preserved for other clients

**Server (mint22) — Cross-platform path validation:**
- On file creation/rename, validate the path against all connected platform limits:
  - **Filename component:** reject if > 255 characters (strictest common limit)
  - **Total relative path:** warn (not reject) if > 250 characters — clients on default Windows won't be able to sync it
  - Response header: `X-Path-Warning: path-length-exceeds-windows-limit` on affected operations
  - Admin configurable: `"FileSystem:MaxPathWarningThreshold": 250` in `appsettings.json`
- **Illegal character validation:** reject filenames containing characters invalid on Windows: `\ / : * ? " < > |` and control characters (0x00–0x1F)
  - Return `400 Bad Request` with clear message: "Filename contains characters not supported on all platforms: `{chars}`. Please rename."
  - This prevents creating files on Linux that can never sync to Windows
- **Reserved names:** reject Windows reserved device names regardless of extension: `CON`, `PRN`, `AUX`, `NUL`, `COM1`–`COM9`, `LPT1`–`LPT9`
  - Case-insensitive check: `con.txt`, `CON.TXT`, `Con.txt` all rejected
  - Return `400 Bad Request`: "Filename `{name}` is a reserved name on Windows and cannot be synced to all clients."

**Linux/macOS Considerations:**
- Linux/macOS clients generally don't hit path length issues (4,096 / 1,024 limit)
- Byte-length filename check is Linux-specific (255 bytes vs 255 chars)
- Character validation benefits all platforms — the server prevents the problem at the source

**Deliverables:**
- ☐ Client (Windows): App manifest with `longPathAware` enabled
- ☐ Client (Windows): First-run long path check + "Enable long paths" auto-fix with UAC
- ☐ Client (Windows): `\\?\` prefix fallback for paths > 260 chars
- ☐ Client (Windows): `SyncStateTag.PathTooLong` for unsyncable files with user notification
- ☐ Client (Linux): UTF-8 byte-length filename validation (255-byte limit)
- ☐ Client (Linux): Filename truncation with hash suffix for oversized names
- ☐ Server: Filename character validation (reject Windows-illegal characters)
- ☐ Server: Reserved name validation (reject `CON`, `PRN`, etc.)
- ☐ Server: Path length warning header when path > 250 chars
- ☐ Server: Configurable path length warning threshold

**Side:** Both
**Complexity:** Medium

---

## Batch 5 — Polish

**Goal:** Quality-of-life improvements that round out the sync experience.

### 5.1 — Bandwidth Throttling

**Approved Proposal:** 4.4

**Problem:** Settings UI has upload/download limit fields but they're not implemented.

**Scope:**

**Client (Windows11-TestDNC):**
- New `ThrottledStream` class: wraps a `Stream`, enforces bytes-per-second limit using a token bucket algorithm
- New `ThrottledHttpHandler` (`DelegatingHandler`): wraps request/response content streams in `ThrottledStream`
- Read limits from `SyncContext` configuration (already has `UploadLimitKbps`, `DownloadLimitKbps` fields)
- Wire into `HttpClientFactory` setup in `SyncContextManager.CreateEngine()`
- Default: 0 (unlimited)
- Settings UI: already has the fields — wire them to `SyncContext` config and trigger engine reconfiguration on change

**Linux/macOS Considerations:** Token bucket algorithm + stream wrapper = fully cross-platform.

**Deliverables:**
- ☐ Client: `ThrottledStream` with token bucket rate limiting
- ☐ Client: `ThrottledHttpHandler` DelegatingHandler
- ☐ Client: Wire to `HttpClientFactory` + `SyncContext` config
- ☐ Client: Settings UI connected to throttle values

**Side:** Client only
**Complexity:** Medium

---

### 5.2 — Selective Sync Folder Browser

**Approved Proposal:** 4.5

**Problem:** Users must know folder names to configure selective sync. No browsable view.

**Scope:**

**Client (SyncTray):**
- New `FolderBrowserView` + `FolderBrowserViewModel`:
  - Fetch folder tree from server: `GET /api/v1/sync/tree`
  - Display as treeview with checkboxes (Avalonia `TreeView` with `CheckBox` template)
  - Checked = included, unchecked = excluded
  - Partial check (indeterminate) for folders with mixed children
  - Lazy-load children on expand (for performance with deep trees)
- Accessible from:
  - Add-account flow (after authentication)
  - Settings → account → "Choose folders to sync"
- Save selections as `SelectiveSyncConfig` rules
- Changes trigger re-sync (delete locally excluded files, download newly included)

**Linux/macOS Considerations:** Avalonia `TreeView` renders natively on all platforms. API calls are cross-platform.

**Deliverables:**
- ☐ Client (SyncTray): `FolderBrowserView` with tree + checkboxes
- ☐ Client (SyncTray): Lazy-load children for deep trees
- ☐ Client (SyncTray): Integration in add-account flow
- ☐ Client (SyncTray): Settings → account → folder selection
- ☐ Client (Core): `SelectiveSyncConfig` update from browser selections

**Side:** Client only
**Complexity:** Medium

---

## Appendix A: Platform Abstraction Guidelines

All client code should follow these conventions to ensure clean multi-platform support:

| Concern | Approach |
|---------|----------|
| Path handling | Always use `Path.Combine()`, never hardcode separators |
| File permissions | Use `#if` or `RuntimeInformation` check; Windows ignores POSIX, Linux ignores ACL |
| IPC transport | Already abstracted: `NamedPipe` (Windows) / `UnixDomainSocket` (Linux/macOS) |
| Notifications | Already abstracted: `WindowsNotificationService` / `LinuxNotificationService` / `NoOpNotificationService` |
| Service management | Already abstracted: `AddWindowsService()` / `AddSystemd()` |
| File watcher quirks | Platform check for inotify limits (Linux only) |
| Temp directories | Use `Path.GetTempPath()` for client; app-specific dir for server |
| File locking errors | Platform-specific `HResult` check (Windows) vs `errno` check (Linux) |
| Case sensitivity | `StringComparer.OrdinalIgnoreCase` (Windows/macOS) vs `Ordinal` (Linux) |
| Shell operations | `Process.Start()` with platform-detected command (`explorer`, `xdg-open`, `open`) |

**For future macOS contributors:** All platform-specific code should be behind `OperatingSystem.IsWindows()` / `OperatingSystem.IsLinux()` / `OperatingSystem.IsMacOS()` checks or use the existing notification/IPC abstractions. Adding macOS support should require implementing the platform-specific interfaces, not changing core sync logic.

---

## Appendix B: Handoff Checklist Per Item

For each work item above, the implementation handoff follows this process:

1. **Server-side work:** Implement and test on `mint22`. Commit and push. Post commit hash in handoff doc.
2. **Client-side work:** Describe what changed server-side in handoff doc. Client agent (on `Windows11-TestDNC`) implements matching client changes. Post commit hash back.
3. **Verification:** Trigger end-to-end sync test between `mint22` and `Windows11-TestDNC`.
4. **Linux verification (Batch 4+):** Repeat tests on `mint-dnc-client`.
5. **Update tracking:** Mark deliverables `✓` in this document. Update `IMPLEMENTATION_CHECKLIST.md` and `MASTER_PROJECT_PLAN.md`.

---

## Appendix C: Future Phase — Virtual Filesystem (On-Demand Files)

> **Not in scope for first release.** This is a next-phase feature to implement after Batches 1–5 are stable and the basic sync client is working reliably. Documenting here for architectural awareness — design decisions in Batches 1–5 should not preclude this feature.

**Concept:** Files appear in the local filesystem with names, sizes, and timestamps — but their content is NOT downloaded until a user or application actually opens/reads them. This dramatically reduces local disk usage and initial sync time for large accounts. Nextcloud calls this "Virtual Files"; OneDrive calls it "Files On-Demand."

**Why this matters for DotNetCloud:**
- Users with 500GB on the server shouldn't need 500GB of local disk to use the sync client
- Initial setup becomes near-instant (download metadata tree only, not all content)
- Nextcloud does NOT support this on Linux — implementing it on Linux would be a significant competitive differentiator
- Modern cloud storage clients (OneDrive, Dropbox, Google Drive) all offer this on Windows

### Windows: Cloud Files API (`cfapi`)

Windows 10 1709+ provides the **Cloud Filter API** (also called Cloud Files API / `CfApi`) — the same native API that OneDrive uses for Files On-Demand.

**How it works:**
- Register DotNetCloud as a **sync root provider** with the Windows shell via `CfRegisterSyncRoot()`
- Files are represented as **placeholders** — they appear in Explorer with full metadata (name, size, date, attributes) but have no local content. Explorer shows a cloud icon (☁) overlay.
- When a user opens a placeholder file, Windows calls back into DotNetCloud's **fetch callback** (`CF_CALLBACK_TYPE_FETCH_DATA`) — the client downloads the content on-demand and hydrates the file
- **File states** (managed by the OS, visible in Explorer's "Status" column):
  - ☁ **Cloud-only:** metadata only, no local content
  - ⬇ **Downloading:** being fetched right now
  - ✓ **Available offline / Pinned:** fully downloaded, kept locally
  - ✓ (green checkmark) **Synced:** locally available and up-to-date with server
- Users can right-click → "Always keep on this device" (pin) or "Free up space" (dehydrate back to placeholder)
- **Streaming hydration:** content is fetched in ranges, so large files can start opening before the full download completes
- Smart prefetch: if a user opens a folder with many small files, batch-fetch them to reduce round trips

**Implementation approach (Windows):**
- .NET P/Invoke wrappers for `CfApi` — Microsoft's `Microsoft.Windows.CsWin32` source generator can produce these, or use the existing community library **`CloudFilterApi.Net`** (if mature enough at implementation time)
- New `CloudFilterSyncProvider` class implementing the callback interface:
  - `FetchDataCallback`: download chunks from server on demand, write to placeholder via `CfExecute(CF_OPERATION_TRANSFER_DATA)`
  - `FetchPlaceholdersCallback`: enumerate server folder contents, create placeholder entries
  - `NotifyFileOpenCompletion` / `NotifyCloseCompletion`: track which files are hydrated
  - `NotifyDelete` / `NotifyRename`: propagate local changes to server
- Integration with existing `SyncEngine`:
  - Initial sync: create placeholder tree (metadata only) instead of downloading all files
  - Periodic sync: update placeholder metadata (size, timestamp) when server files change; invalidate hydrated files if server version changed
  - Upload: works as normal — file is already hydrated (user modified it locally)
- **User mode toggle:** Settings → Sync → "Storage mode":
  - "Download all files" (current behavior, default for first release)
  - "Files on-demand" (virtual filesystem — download only when accessed)
  - Switching modes: "Download all" → "On-demand" = dehydrate un-pinned files; "On-demand" → "Download all" = hydrate everything

### Linux: FUSE (Filesystem in Userspace)

Linux has no native equivalent to Windows Cloud Files API, but **FUSE** (Filesystem in Userspace) enables the same experience. This would make DotNetCloud one of the very few cloud sync clients to offer virtual files on Linux.

**How it works:**
- Create a custom FUSE filesystem that presents the server's file tree as a mounted directory
- File metadata (names, sizes, timestamps, permissions) is served from a local metadata cache (populated from server sync)
- File content is fetched on `read()` syscall — transparent to all applications
- The FUSE mount point can be the user's sync folder, or a parallel virtual view alongside the traditional sync folder

**Implementation approach (Linux):**
- Use **`Tmds.Fuse`** (.NET FUSE bindings) or **`libfuse3`** via P/Invoke
- New `FuseSyncFilesystem` class implementing the FUSE operations:
  - `getattr()`: return file metadata from local cache (instant, no network)
  - `readdir()`: list directory contents from local cache
  - `open()` / `read()`: fetch file content from server on demand, cache locally
  - `write()` / `create()` / `unlink()` / `rename()`: propagate changes to server
- **Local cache layer:**
  - Downloaded content cached in `~/.local/share/dotnetcloud/cache/` (content-addressed, same as chunk cache)
  - LRU eviction when cache exceeds configurable size (default: 10% of free disk space, or user-specified max)
  - Pinned files exempt from eviction
- **Metadata cache:**
  - Full directory tree with sizes, timestamps, permissions cached in `LocalStateDb`
  - Refreshed on periodic sync interval (same cursor/delta as regular sync)
  - `readdir()` and `getattr()` served entirely from local cache — no network latency for file browsing
- **Visual indicators:**
  - FUSE can't provide the same shell integration as Windows Cloud Files (no cloud icon overlays in file managers natively)
  - Alternative: `.desktop` file metadata or custom Nautilus/Dolphin extensions (stretch goal — file manager plugins for GNOME and KDE that show cloud status icons)
  - DotNetCloud tray app shows a "Virtual files" status section listing which files are cached vs cloud-only
- **Requirements:**
  - `fuse3` package installed (most Linux distros ship it or it's one apt/dnf command away)
  - User must be in `fuse` group (or `allow_other` mount option for system-wide access)
  - `install.sh` can check and prompt for these


### macOS: File Provider Extension

macOS has **File Provider** framework (NSFileProviderExtension) — Apple's equivalent to Windows Cloud Files. This requires a sandboxed app extension.

**Status:** Left for future macOS contributor. The architecture described above (metadata cache + on-demand fetch + local content cache) maps directly to macOS File Provider. The server-side APIs (sync tree, chunk download) are the same — only the OS integration layer differs.

### Server-Side Requirements

The server already has most of what's needed:
- `GET /api/v1/sync/tree` — provides the full metadata tree for placeholder creation
- Chunked download APIs — support range requests for streaming hydration
- Sync cursor/delta — efficiently tells the client what changed since last check

**Additional server work (minimal):**
- **Range-based chunk reads:** support `Range` header on chunk download for partial/streaming hydration (may already work via ASP.NET Core static file middleware, needs verification)
- **Lightweight metadata endpoint:** `GET /api/v1/sync/tree?metadataOnly=true` — skip content hashes if the client only needs names/sizes/dates for placeholder creation (optimization, not strictly required)

### Architectural Considerations for Batches 1–5

To avoid painting ourselves into a corner, keep these in mind during current implementation:

| Current Decision | Virtual FS Impact |
|-----------------|-------------------|
| `LocalStateDb` stores file metadata | ✓ Reusable as the metadata cache for virtual FS |
| Content-addressed chunk storage | ✓ Natural cache layer — same chunks serve both regular sync and on-demand fetch |
| `SyncEngine` as the central coordinator | ✓ Virtual FS provider calls into the same sync engine for uploads/downloads |
| Cursor-based delta sync (Batch 2.4) | ✓ Essential for efficiently updating placeholder metadata |
| `ChunkedTransferClient` downloads | ✓ Reusable for on-demand hydration callbacks |
| Selective sync (Batch 5.2) | ✓ Virtual FS is a superset — selective sync chooses folders; virtual FS makes everything visible but only downloads what's accessed |
| File conflict resolution (Batch 3.5) | ✓ Same conflict model applies — hydrated files that were modified locally need the same merge/resolution flow |

### Why Not First Release

- Core sync must be rock-solid before adding a virtualization layer on top
- Windows Cloud Files API has a significant learning curve and debugging complexity
- FUSE on Linux adds a hard dependency and potential permission complications
- Need real-world usage data from traditional sync to inform caching/prefetch strategies
- The current selective sync (Batch 5.2) covers 80% of the use case for users with large accounts

### Deliverables (Future Phase)

- ☐ Client (Windows): `CfApi` P/Invoke wrappers or managed library integration
- ☐ Client (Windows): `CloudFilterSyncProvider` with fetch/notify callbacks
- ☐ Client (Windows): Sync root registration + placeholder creation from server tree
- ☐ Client (Windows): On-demand hydration with streaming chunk download
- ☐ Client (Windows): Pin/unpin support (right-click "Always keep" / "Free up space")
- ☐ Client (Windows): Storage mode toggle in Settings UI
- ☐ Client (Linux): FUSE filesystem implementation (`FuseSyncFilesystem`)
- ☐ Client (Linux): Local content cache with LRU eviction
- ☐ Client (Linux): Metadata cache served from `LocalStateDb`
- ☐ Client (Linux): `fuse3` dependency check in installer
- ☐ Server: Range-based chunk download support (verify/implement)
- ☐ Server: Metadata-only tree endpoint optimization
- ☐ Docs: User guide for virtual filesystem setup (Windows + Linux)
