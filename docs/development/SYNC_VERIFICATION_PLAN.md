# Sync Implementation Verification Plan

**Purpose:** Systematically verify that every deliverable from the [Sync Implementation Guide](SYNC_IMPLEMENTATION_GUIDE.md) was completed as specified. Each task has specific acceptance criteria, code-level checks, and test requirements.

**Date:** 2026-03-09

**Reference:** [SYNC_IMPLEMENTATION_GUIDE.md](SYNC_IMPLEMENTATION_GUIDE.md) | [SYNC_IMPROVEMENT_PLAN.md](SYNC_IMPROVEMENT_PLAN.md)

---

## Verification Method

For each task, verification is split into three categories:

| Category | What it checks |
|----------|---------------|
| **Code** | Does the code exist, in the right location, with the right structure? |
| **Build** | Does the solution compile with no errors after all changes? |
| **Test** | Do automated tests cover the feature? Do manual acceptance tests pass? |

---

## Status Legend

- ☐ Not yet verified
- ✓ Verified complete
- ⚠ Partially complete — see notes
- ✗ Missing or not implemented

---

## Batch 1 — Foundation

### Task 1.1: Sync Service Logging (Client)

**Status per plan:** ✅ COMPLETE (commit `c69aeac`)

**Code verification:**
- ☐ Serilog packages (`Serilog.AspNetCore`, `Serilog.Sinks.File`, `Serilog.Formatting.Compact`) in `DotNetCloud.Client.SyncService.csproj`
- ☐ `Program.cs` configures Serilog with `CompactJsonFormatter`, daily rolling, 30-day retention, 50MB limit
- ☐ `sync-settings.json` has `"logging"` section with `retentionDays`, `maxFileSizeMB`, `minimumLevel`
- ☐ Log statements present in: `SyncEngine` (sync pass start/complete/errors), `ChunkedTransferClient` (upload/download start/complete), `ConflictResolver` (conflict detection), `OAuth2Service` (token refresh — never logs actual token), `IpcServer` (IPC commands), `SyncWorker` (FSW events)
- ☐ On Linux: log file permissions set to `600` via `File.SetUnixFileMode()` after creating the log directory

**Build verification:**
- ☐ `dotnet build src/Clients/SyncService/DotNetCloud.Client.SyncService/` succeeds

**Test verification:**
- ☐ Run sync service on Windows → verify `logs/sync-service.log` appears with JSON entries
- ☐ Trigger a sync → verify events are logged (sync start, file operations, sync complete with duration + file count)
- ☐ Verify daily file rotation naming works

---

### Task 1.1b: Sync Audit Logging (Server)

**Status per plan:** ✅ COMPLETE (commit `c585dae`)

**Code verification:**
- ☐ `ILogger<T>` present in: `ChunkedUploadService`, `DownloadService`, `FileService`, `SyncService`
- ☐ Structured log events for: `file.uploaded` (with NodeId, FileName, FileSize, UserId, ClientIp, RequestId), `file.downloaded`, `file.deleted`, `file.moved`/`file.renamed`, `sync.reconcile.completed`
- ☐ Dedicated audit log file sink targeting `{DOTNETCLOUD_DATA_DIR}/logs/audit-sync.log` with daily rolling, 30-day retention

**Build verification:**
- ☐ `dotnet build src/Modules/Files/DotNetCloud.Modules.Files/` succeeds

**Test verification:**
- ☐ Upload, download, and delete a file via API → check `audit-sync.log` for structured entries
- ☐ Verify each log entry includes structured fields (not just string messages)

---

### Task 1.2: Request Correlation IDs

**Status per plan:** ✅ COMPLETE (Server `16dd7df`, Client `97afdd8`)

**Code verification:**
- ☐ `RequestCorrelationMiddleware` exists in `src/Core/DotNetCloud.Core.ServiceDefaults/` (or `Core.Server`)
  - ☐ Reads `X-Request-ID` from request headers, generates GUID if absent
  - ☐ Sets `X-Request-ID` on response headers
  - ☐ Pushes `RequestId` into Serilog `LogContext`
- ☐ Middleware registered early in pipeline (before auth, before controllers)
- ☐ Client: `CorrelationIdHandler` (or equivalent `DelegatingHandler`) in `DotNetCloud.Client.Core`
  - ☐ Generates `X-Request-ID` and adds to outgoing request headers
  - ☐ Logs request ID on API call initiation and on errors

**Build verification:**
- ☐ Both server and client projects build

**Test verification:**
- ☐ Make an API call from client → verify server log and client log share the same `RequestId`

---

### Task 1.3: Server Rate Limiting

**Status per plan:** ✅ COMPLETE (commit `4570c16`)

**Code verification:**
- ☐ `AddRateLimiter()` configured in `Program.cs` with:
  - ☐ `sync-standard` policy: 60 req/min sliding window
  - ☐ `sync-heavy` policy: 300 req/min sliding window
  - ☐ `sync-tree` policy: 10 req/min sliding window
- ☐ `app.UseRateLimiter()` in pipeline
- ☐ Rate limit attributes applied per the guide's table:
  - ☐ `GET sync/changes` → `sync-standard` (60/min)
  - ☐ `GET sync/tree` → `sync-tree` (10/min)
  - ☐ `POST sync/reconcile` → `sync-standard` (30/min)
  - ☐ `POST files/upload/initiate` → `sync-standard` (30/min)
  - ☐ Chunk upload/download → `sync-heavy` (300/min)
  - ☐ File download → `sync-heavy` (120/min)
- ☐ `Retry-After` header in rejection response via `options.OnRejected`
- ☐ Rejection status code: `429`

**Build verification:**
- ☐ `dotnet build` on server succeeds

**Test verification:**
- ☐ Send 61 requests to `sync/changes` within 1 minute → 61st returns `429`
- ☐ Verify `Retry-After` header present in 429 response

---

### Task 1.4: Chunk Integrity Verification on Download

**Status per plan:** ✅ COMPLETE (commit on Windows, 55 tests)

**Code verification:**
- ☐ In `ChunkedTransferClient.DownloadAsync()`: after receiving chunk bytes, SHA-256 hash is computed and compared to expected
- ☐ Retry loop: up to 3 attempts on hash mismatch
- ☐ After 3 failures: throws exception with request ID
- ☐ Uses existing `ContentHasher.ComputeHash()`

**Build verification:**
- ☐ Client project builds on Windows

**Test verification:**
- ☐ Unit test exists for hash verification (happy path — match)
- ☐ Unit test exists for hash mismatch detection and retry behavior
- ☐ All related tests pass: `dotnet test tests/DotNetCloud.Client.Core.Tests/`

---

### Task 1.5: Per-Chunk Retry with Exponential Backoff

**Status per plan:** ✅ COMPLETE (commit `1aa6b18`, 64 tests)

**Code verification:**
- ☐ Each chunk upload/download wrapped in retry loop (max 3 attempts)
- ☐ Exponential backoff: `2^(attempt-1)` seconds + random jitter (0-500ms)
- ☐ Only retries on: `HttpRequestException`, 5xx status codes, timeouts
- ☐ Does NOT retry on: 4xx status codes, 429 (handled separately)
- ☐ `ChunkTransferResult` record (or equivalent) exists: `Hash`, `Success`, `Attempts`, `Error?`
- ☐ Final result logged for each chunk

**Build verification:**
- ☐ Client project builds

**Test verification:**
- ☐ Tests cover: successful transfer, retry on failure, max retries exceeded
- ☐ All tests pass

---

### Task 1.6: SQLite WAL Mode + Corruption Recovery

**Status per plan:** ✅ COMPLETE (commit `1aa6b18`, 64 tests)

**Code verification:**
- ☐ `Journal Mode=Wal` in SQLite connection string
- ☐ On startup `InitializeAsync()`: runs `PRAGMA integrity_check`
- ☐ If result != "ok":
  - ☐ Logs error
  - ☐ Renames corrupt DB with timestamp suffix (e.g., `.corrupt.20260309120000`)
  - ☐ Also renames `-wal` and `-shm` files if present
  - ☐ Recreates fresh DB via `EnsureCreatedAsync()`
  - ☐ Sets flag for full re-sync (`_needsFullResync`)
  - ☐ Notifies user via notification service
- ☐ After each complete sync pass: runs `PRAGMA wal_checkpoint(TRUNCATE)`

**Build verification:**
- ☐ Client project builds

**Test verification:**
- ☐ Test: WAL mode active (check for `.db-wal` file)
- ☐ Test: corruption detection and recovery

---

### Task 1.7: Operation Retry Queue with Backoff

**Status per plan:** ✅ COMPLETE (commit `1aa6b18`, 64 tests)

**Code verification:**
- ☐ `PendingOperationDbRow` has `NextRetryAt` (DateTime?) and `LastError` (string?) columns
- ☐ On failure: `RetryCount` incremented, `NextRetryAt` computed per backoff schedule:
  - 1 → +1 min, 2 → +5 min, 3 → +15 min, 4 → +1 hour, 5-9 → +6 hours, 10+ → move to failed
- ☐ `FailedOperationDbRow` table exists (same schema as pending + `FailedAt`)
- ☐ `GetPendingOperationsAsync()` filters: `WHERE NextRetryAt IS NULL OR NextRetryAt <= @now`
- ☐ On success: resets `RetryCount = 0`, `NextRetryAt = null`, `LastError = null`

**Build verification:**
- ☐ Client project builds, EF migration applied

**Test verification:**
- ☐ Tests cover: escalating backoff, max retries → move to failed table, success resets counters

---

### Task 1.8: Secure Temp File Handling (Server)

**Status per plan:** ✅ COMPLETE (commit `82ca53b`)

**Code verification:**
- ☐ Server startup creates `{DOTNETCLOUD_DATA_DIR}/tmp/` directory
- ☐ On Linux: tmp directory permissions set to `700` (UserRead | UserWrite | UserExecute)
- ☐ `DownloadService` uses the new temp dir (not `Path.GetTempPath()`)
- ☐ `IHostedService` (or startup code): deletes files in `{DATA_DIR}/tmp/` older than 1 hour on startup

**Build verification:**
- ☐ Server builds

**Test verification:**
- ☐ Upload/download a file → verify temp files created in `{DATA_DIR}/tmp/`, not `/tmp/`
- ☐ Restart server → verify old temp files cleaned up

---

### Task 1.9: File Scanning Interface + Execution Prevention (Server)

**Status per plan:** ✅ COMPLETE (commit `82ca53b`)

**Code verification:**
- ☐ In `LocalFileStorageEngine` (or chunk write path): chunk files written with `UserRead | UserWrite` only (no execute)
- ☐ Download response headers include:
  - ☐ `X-Content-Type-Options: nosniff`
  - ☐ `Content-Disposition: attachment; filename="..."` with safe filename
- ☐ `IFileScanner` interface exists with `ScanAsync(Stream, string, CancellationToken)` → `ScanResult`
- ☐ `ScanResult` record: `IsClean`, `ThreatName?`, `ScannerName?`
- ☐ `NoOpFileScanner : IFileScanner` always returns `ScanResult(true)`
- ☐ Registered in DI: `services.AddSingleton<IFileScanner, NoOpFileScanner>()`
- ☐ `FileScanStatus` enum on `FileVersion`: `NotScanned = 0`, `Clean = 1`, `Threat = 2`, `Error = 3`
- ☐ EF migration adds `ScanStatus` column
- ☐ Max file size config: `FileUpload:MaxFileSizeBytes = 16106127360` (15 GB)
- ☐ `InitiateUploadAsync()` rejects files exceeding max size

**Build verification:**
- ☐ Server builds

**Test verification:**
- ☐ Upload a file → verify chunk files on disk have no execute permission (`ls -la`)
- ☐ Download a file → verify `nosniff` and `attachment` headers
- ☐ Attempt upload > 15GB → verify rejection

---

## Batch 2 — Efficiency

### Task 2.1: Content-Defined Chunking (CDC)

**Status per plan:** ✅ COMPLETE (Server `3a7e0ae`, Client `bc9e08a`)

**Code verification — Server:**
- ☐ `ContentHasher.ChunkAndHashCdcAsync()` exists with parameters: `stream`, `avgSize=4MB`, `minSize=512KB`, `maxSize=16MB`
- ☐ `CdcChunkInfo` record: `Hash`, `Offset`, `Size`
- ☐ `FileVersionChunk` has `Offset` (long) and `ChunkSize` (int) columns (EF migration)
- ☐ `InitiateUploadDto` has `ChunkSizes` (`int[]?`) property
- ☐ `CompleteUploadAsync()` uses `Offset + ChunkSize` from manifest for reassembly
- ☐ Backward compat: works without `ChunkSizes` (assumes fixed 4MB)

**Code verification — Client:**
- ☐ `ChunkedTransferClient` uses CDC-based splitting (not fixed-size)
- ☐ Sends `ChunkSizes` array in initiate upload request
- ☐ On download: reads chunk manifest with sizes, reassembles using offset+size
- ☐ Sends header `X-Sync-Capabilities: cdc` on requests

**Build verification:**
- ☐ Both server and client build

**Test verification:**
- ☐ Edit 1 byte in the middle of a large file → re-upload → verify only 1-2 chunks uploaded
- ☐ Verify legacy fixed-size clients still work (backward compat)
- ☐ Tests for CDC algorithm: produces deterministic chunks, respects min/max boundaries

---

### Task 2.2: Streaming Chunk Pipeline

**Status per plan:** ✅ COMPLETE (commit `2e0788c`)

**Code verification:**
- ☐ Upload uses `Channel.CreateBounded<>` with capacity ~8
- ☐ Producer: reads file → splits (CDC) → hashes → writes to channel
- ☐ 4 parallel consumer tasks: upload chunks from channel
- ☐ Peak memory bounded to ~32MB (8 slots × 4MB avg) regardless of file size
- ☐ Download: chunks downloaded to temp files on disk, then concatenated (not held in memory)
- ☐ Temp chunk files cleaned up after assembly

**Build verification:**
- ☐ Client project builds

**Test verification:**
- ☐ Upload a large file → verify memory stays bounded (not proportional to file size)
- ☐ Tests for channel-based pipeline

---

### Task 2.3: Compression for Chunk Transfers

**Status per plan:** ✅ COMPLETE (Server `032f6a2`, Client `Windows11-TestDNC`)

**Code verification — Server:**
- ☐ `AddResponseCompression()` in `Program.cs` with Brotli + Gzip providers
- ☐ `EnableForHttps = true`
- ☐ Includes `application/octet-stream` in MIME types
- ☐ `app.UseResponseCompression()` placed before `UseRouting()`

**Code verification — Client:**
- ☐ `AutomaticDecompression = DecompressionMethods.All` set on `HttpClientHandler`
- ☐ Chunk uploads wrapped in `GZipStream` with `Content-Encoding: gzip`
- ☐ ⚠ **VERIFY:** Skip compression for already-compressed MIME types (`.jpg`, `.jpeg`, `.png`, `.gif`, `.zip`, `.gz`, `.bz2`, `.xz`, `.7z`, `.rar`, `.mp4`, `.mp3`, `.mkv`, `.avi`, `.webm`)

**Build verification:**
- ☐ Both projects build

**Test verification:**
- ☐ Upload a large text file → verify network traffic is smaller than raw file size
- ☐ Upload a `.zip` file → verify it is NOT gzip-compressed (sent raw)

---

### Task 2.4: Server-Issued Sync Cursor

**Status per plan:** ✅ COMPLETE (Server `c81495d`, Client `1a9c4c6`)

**Code verification — Server:**
- ☐ `UserSyncCounter` entity: `UserId` (PK) + `CurrentSequence` (long)
- ☐ `SyncSequence` column (long?) on `FileNode` (EF migration)
- ☐ On every file mutation: sequence incremented, `SyncSequence` set on node
- ☐ `SyncController` accepts `cursor` query param
  - ☐ Cursor format: `{userId}:{sequenceNumber}` (base64 encoded)
  - ☐ Query: `WHERE SyncSequence > sequenceNumber`
  - ☐ Returns `nextCursor` (base64 of `{userId}:{maxSequenceInResults}`)
  - ☐ Falls back to timestamp-based query if no cursor and `since` param provided

**Code verification — Client:**
- ☐ `GetChangesSinceAsync()` accepts optional `cursor` parameter
- ☐ Sends `cursor` query param instead of `since` when available
- ☐ `SyncCheckpointRow` (or equivalent) stores cursor string
- ☐ First sync: no cursor → gets everything + first cursor
- ☐ Subsequent syncs: sends cursor → gets delta + next cursor

**Build verification:**
- ☐ Both projects build

**Test verification:**
- ☐ Sync once → create file on server → sync again → only new file returned
- ☐ Verify no clock skew issues
- ☐ `SyncServiceTests` cover cursor-based changes

---

### Task 2.5: Paginated Change Responses

**Status per plan:** ✅ COMPLETE (Server `c81495d`, Client `1a9c4c6`)

**Code verification — Server:**
- ☐ `GET /api/v1/sync/changes` accepts `limit` query param (default: 500, max: 5000)
- ☐ Response envelope: `{ changes: [...], nextCursor: "...", hasMore: true/false }`
- ☐ `hasMore = true` when more changes exist beyond the page

**Code verification — Client:**
- ☐ `SyncEngine.ApplyRemoteChangesAsync()` loops:
  - ☐ Calls `GetChangesSinceAsync(cursor, limit: 500)`
  - ☐ Processes changes from page
  - ☐ Saves cursor after each page (crash resilience)
  - ☐ Continues while `hasMore == true`

**Build verification:**
- ☐ Both projects build

**Test verification:**
- ☐ Create 1500+ files on server → client syncs from scratch → verify multiple paginated calls
- ☐ Verify cursor saved after each page

---

### Task 2.6: ETag / If-None-Match for Chunk Downloads

**Status per plan:** ✅ COMPLETE (Server `c81495d`, Client `1a9c4c6`)

**Code verification — Server:**
- ☐ On `GET /api/v1/files/chunks/{hash}`: response includes `ETag: "{hash}"`
- ☐ If `If-None-Match` header matches → returns `304 Not Modified`

**Code verification — Client:**
- ☐ ⚠ **VERIFY:** `DownloadChunkByHashAsync()` sends `If-None-Match: "{chunkHash}"` header before downloading
- ☐ ⚠ **VERIFY:** Handles `HttpStatusCode.NotModified` (304) → uses local copy, skips download

**Build verification:**
- ☐ Both projects build

**Test verification:**
- ☐ Download a file → re-sync same file → verify chunks return 304
- ☐ Verify no unnecessary re-downloads

> **⚠ ATTENTION:** Initial code survey did not find client-side `If-None-Match` header handling in `DotNetCloudApiClient.DownloadChunkByHashAsync()`. This needs specific verification — may be implemented in `ChunkedTransferClient` instead, or may be missing.

---

## Batch 3 — User Experience

### Task 3.1: .syncignore with UI Support

**Status per plan:** ✅ COMPLETE (commit `a9c6812`)

**Code verification — Core:**
- ☐ `Microsoft.Extensions.FileSystemGlobbing` NuGet package installed
- ☐ `SyncIgnoreParser` class exists with:
  - ☐ Built-in patterns hardcoded: `.DS_Store`, `Thumbs.db`, `desktop.ini`, `*.swp`, `*~`, `*.tmp`, `*.temp`, `~$*`, `.git/`, `.svn/`, `.hg/`, `node_modules/`, `.npm/`, `.yarn/`, `.pnp.*`, `packages/`, `.nuget/`, `.directory`
  - ☐ Parses `.syncignore` file from sync root (gitignore-style: `*`, `?`, `**`, `!` for negation, `#` for comments, `/` suffix for dirs)
  - ☐ `bool IsIgnored(string relativePath)` method
  - ☐ User rules override built-in (e.g., `!*.tmp` un-ignores `.tmp`)
- ☐ `SyncEngine` calls `IsIgnored()` before: queuing uploads from FSW events, applying remote changes, comparing in periodic scans

**Code verification — UI:**
- ☐ Settings panel: "Ignored Files" section
  - ☐ "System defaults" section (gray/italic, not editable)
  - ☐ "Your rules" section (editable list with Add/Remove buttons)
  - ☐ "Test a path" input field: shows "✓ Ignored by rule: `*.tmp`" or "✗ Not ignored"
  - ☐ Save writes to `.syncignore` in sync root

**Build verification:**
- ☐ Client projects build

**Test verification:**
- ☐ `SyncIgnoreParserTests` pass (built-ins, user patterns, negation, file loading)
- ☐ Add `*.log` to `.syncignore` → create `.log` file → verify NOT uploaded

---

### Task 3.2: Persistent Upload Sessions

**Status per plan:** ✅ COMPLETE (commit `4243328`)

**Code verification:**
- ☐ `ActiveUploadSessionRecord` entity with: `Id`, `ServerSessionId`, `NodeId`, `FilePath`, `FileSize`, `TotalChunks`, `UploadedChunks`/`UploadedChunkHashesJson`, `StartedAt`/`CreatedAt`, `LastActivityAt`/`FileModifiedAt`
- ☐ `ChunkedTransferClient.UploadAsync()`: saves session after initiate, updates after each chunk, deletes after complete
- ☐ On `SyncEngine.StartAsync()`: queries incomplete sessions, checks validity, resumes or re-initiates
- ☐ Records older than 48 hours cleaned up

**Build verification:**
- ☐ Client builds

**Test verification:**
- ☐ Start large upload → kill sync service → restart → verify upload resumes

---

### Task 3.3: Locked File Handling

**Status per plan:** ✅ COMPLETE

**Code verification:**
- ☐ **Tier 1 (Shared-read):** All `File.OpenRead()` calls replaced with `FileShare.ReadWrite | FileShare.Delete`
- ☐ **Tier 2 (Retry):** `IOException` with `HResult == 0x80070020` retries up to 3 times, 2 seconds apart
- ☐ **Tier 3 (VSS):**
  - ☐ `ILockedFileReader` interface with `TryReadLockedFileAsync(string, CancellationToken)`
  - ☐ `VssLockedFileReader` (Windows): creates VSS snapshot, reads from shadow copy
  - ☐ `NoOpLockedFileReader` (Linux/macOS): returns `null`
  - ☐ DI registration: Windows → `VssLockedFileReader`, else → `NoOpLockedFileReader`
  - ☐ One snapshot per sync pass, used for all locked files, disposed in `finally`
- ☐ **Tier 4 (Defer):**
  - ☐ `SyncStateTag.Deferred` (or string "Deferred") exists
  - ☐ If all tiers fail: marks file as deferred, logs warning, shows tray notification
  - ☐ On next sync pass: retries deferred files from Tier 1
- ☐ Linux: consistency check — compare file size before/after read, defer if changed

**Build verification:**
- ☐ Client builds

**Test verification:**
- ☐ Open a locked file → trigger sync → verify Tier 1 or Tier 3 succeeds
- ☐ Verify deferred files retry on next pass

---

### Task 3.4: Per-File Transfer Progress in Tray UI

**Status per plan:** ✅ COMPLETE

**Code verification — SyncService:**
- ☐ `ChunkedTransferClient`'s `IProgress<TransferProgress>` wired to IPC event publishing
- ☐ `transfer-progress` IPC events include: `fileName`, `direction`, `bytesTransferred`, `totalBytes`, `speedBps`
- ☐ `transfer-complete` IPC event on file finish
- ☐ Progress events throttled: max 2/sec per file (500ms minimum interval)

**Code verification — SyncTray:**
- ☐ `ActiveTransferViewModel` exists with: file name, direction (↑/↓), progress %, speed, ETA
- ☐ Observable collection of current transfers in tray popup
- ☐ Completed transfer shown briefly (~5 seconds), then removed

**Build verification:**
- ☐ Client builds

**Test verification:**
- ☐ Upload a large file → tray UI shows file name, progress bar, speed
- ☐ `ActiveTransferViewModel` tests pass (speed calculation, ETA estimation, display formatting)

---

### Task 3.5a: ConflictRecord Entity

**Status per plan:** ✅ COMPLETE (part of 3.5 commit `8508afc`)

**Code verification:**
- ☐ `ConflictRecord` entity with all required fields: `Id`, `OriginalPath`, `ConflictCopyPath`, `NodeId`, `LocalModifiedAt`, `RemoteModifiedAt`, `DetectedAt`, `ResolvedAt?`, `Resolution?`, `BaseContentHash?`, `AutoResolved`
- ☐ `DbSet<ConflictRecord>` in `LocalStateDbContext`
- ☐ `ConflictResolver` saves `ConflictRecord` on conflict detection
- ☐ IPC commands: `list-conflicts`, `resolve-conflict`

**Test verification:**
- ☐ `ConflictResolverTests` cover conflict record creation

---

### Task 3.5b: Auto-Resolution Pipeline

**Status per plan:** ✅ COMPLETE (part of 3.5)

**Code verification:**
- ☐ `ConflictResolver` (or `AutoConflictResolver`) implements all 5 strategies in order:
  - ☐ **Strategy 1 (Identical):** SHA-256 comparison → `"auto-identical"`
  - ☐ **Strategy 2 (Fast-forward):** Compare against `BaseContentHash` → `"auto-fast-forward"`
  - ☐ **Strategy 3 (Clean text merge):** Three-way diff using DiffPlex, non-overlapping changes → `"auto-merged"`
  - ☐ **Strategy 4 (Newer wins):** Same user, timestamps > 5 min apart, configurable → `"auto-newer-wins"`
  - ☐ **Strategy 5 (Append-only):** Prefix detection, ≥ 90% shared prefix for multi-user → `"auto-append"` / `"auto-append-combined"`
- ☐ Pipeline stops on first success
- ☐ `DiffPlex` NuGet package installed
- ☐ Settings in `sync-settings.json`: `"conflictResolution": { "autoNewerWins": true, "notificationSound": true, "reminderIntervalMinutes": 240 }`

**Test verification:**
- ☐ `ConflictResolverTests` cover each strategy
- ☐ Auto-resolved conflicts logged with strategy name

---

### Task 3.5c: Conflict Notifications (Psychologically Loud)

**Status per plan:** ✅ COMPLETE (part of 3.5)

**Code verification:**
- ☐ **Tray icon change:** Normal cloud icon swapped to warning variant (cloud + exclamation) when unresolved conflicts > 0
- ☐ **Badge count:** Conflict count overlaid on tray icon (red circle with number)
- ☐ **Persistent toast:** Non-auto-dismissing notification on first conflict detection
- ☐ **Tooltip change:** Normal → "DotNetCloud — ⚠ {n} unresolved conflicts"
- ☐ **Tray menu:** "Conflicts ({n})" at TOP of context menu when conflicts > 0
- ☐ **Recurring reminder:** Re-show toast if unresolved > 24 hours (once per day)
- ☐ **First-conflict education:** Longer explanatory notification on very first conflict ever

**Build verification:**
- ☐ SyncTray builds

**Test verification:**
- ☐ `TrayViewModelTests` cover conflict state changes
- ☐ Manual: trigger conflict → verify icon change, tooltip change, notification

---

### Task 3.5d: Conflicts Panel UI

**Status per plan:** ✅ COMPLETE (part of 3.5)

**Code verification:**
- ☐ Conflicts panel accessible from tray menu or settings
- ☐ `ConflictViewModel` with commands: `KeepLocalCommand`, `KeepServerCommand`, `KeepBothCommand`, `OpenFolderCommand`
- ☐ Unresolved conflicts list: file name, path, local/server timestamps + sizes
- ☐ "History" tab: resolved conflicts from last 30 days
- ☐ "Keep local" → uploads conflict copy, marks `Resolution = "kept-local"`
- ☐ "Keep server" → deletes conflict copy, keeps server version, marks `Resolution = "kept-server"`
- ☐ "Keep both" → leaves both files, marks `Resolution = "kept-both"`
- ☐ "Open folder" → opens containing directory with platform-correct command

**Build verification:**
- ☐ SyncTray builds

**Test verification:**
- ☐ Manual: create conflict → resolve via each button → verify correct behavior

---

### Task 3.5e: Three-Pane Merge Editor

**Status per plan:** ✅ COMPLETE (part of 3.5)

> **⚠ ATTENTION:** No `MergeEditorWindow` or `MergeEditorView` file was found during code survey. This is the most complex UI deliverable and needs specific verification.

**Code verification:**
- ☐ ⚠ **VERIFY:** Merge editor window exists (separate window, not embedded in settings)
- ☐ ⚠ **VERIFY:** 4 panes: Left (local, read-only) | Center (base, read-only) | Right (server, read-only) | Bottom (merged result, editable)
- ☐ ⚠ **VERIFY:** `DiffPlex` integration: line-level diff with colors (green=added, red=removed, yellow=conflict)
- ☐ ⚠ **VERIFY:** Auto-merge non-conflicting hunks; conflict markers (`<<<<<<<` / `=======` / `>>>>>>>`) for conflicting regions
- ☐ ⚠ **VERIFY:** Interactions: click hunk to apply, "Accept all local"/"Accept all server", "Reset merge", "Save & resolve", "Cancel"
- ☐ ⚠ **VERIFY:** Text file types supported (`.txt`, `.md`, `.json`, `.yaml`, `.cs`, `.py`, etc.)
- ☐ ⚠ **VERIFY:** Binary files show only Keep/Both buttons (no merge editor)
- ☐ **XML merge support:**
  - ☐ ⚠ **VERIFY:** `Microsoft.XmlDiffPatch` NuGet package installed
  - ☐ ⚠ **VERIFY:** Tree-based diffing for XML files (`.xml`, `.csproj`, `.fsproj`, `.props`, `.targets`, `.xaml`, `.svg`, `.xslt`)
  - ☐ ⚠ **VERIFY:** Post-merge validation via `XDocument.Parse()`
  - ☐ ⚠ **VERIFY:** In-editor help panel ("How XML merging works") on first XML merge
  - ☐ ⚠ **VERIFY:** Node-level conflict view for XML

**Build verification:**
- ☐ SyncTray builds

**Test verification:**
- ☐ Manual: create text file conflict → open merge editor → verify 4-pane layout
- ☐ Manual: create XML file conflict → verify tree-based diff view

---

### Task 3.6: Idempotent Operations

**Status per plan:** ✅ COMPLETE (commit `3504932`, 119 tests)

**Code verification:**
- ☐ Before upload in `SyncEngine.ApplyLocalChangesAsync()`:
  - ☐ Checks server node's `ContentHash` against local file hash
  - ☐ If identical: skips upload, logs "server already has this version", updates LocalStateDb
- ☐ Prevents duplicate versions when crash happens after upload but before DB update

**Build verification:**
- ☐ Client builds

**Test verification:**
- ☐ Tests cover: skip upload when server has same hash, upload when hash differs

---

## Batch 4 — Cross-Platform Hardening

### Task 4.1: Case-Sensitivity Conflict Detection

**Status per plan:** ✅ COMPLETE

**Code verification — Server:**
- ☐ In `FileService.CreateFolderAsync()`, `RenameAsync()`, `ChunkedUploadService.CompleteUploadAsync()`:
  - ☐ Case-insensitive name check: `EF.Functions.ILike(n.Name, newName)` or equivalent
  - ☐ Returns `Conflict` response if match found
- ☐ Config: `FileSystem:EnforceCaseInsensitiveUniqueness` (default: true)

**Code verification — Client:**
- ☐ Before applying remote changes: check case-insensitive match against existing files
- ☐ On Windows/macOS: renames with `(case conflict)` suffix
- ☐ Uses `StringComparer.OrdinalIgnoreCase` on Windows/macOS, `StringComparer.Ordinal` on Linux

**Build verification:**
- ☐ Both projects build

**Test verification:**
- ☐ Create `Report.docx` and `report.docx` in same folder → server rejects second
- ☐ Client handles collisions gracefully

---

### Task 4.2: File Permission Metadata Sync

**Status per plan:** ✅ COMPLETE (Server `fa097bf`, Client `c70bd47`)

**Code verification — Server:**
- ☐ `FileNode` has `PosixMode` (int?) and `PosixOwnerHint` (string?)
- ☐ `FileVersion` has `PosixMode` (int?)
- ☐ Included in all DTOs: `FileNodeDto`, `SyncChangeDto`, `SyncTreeNodeDto`
- ☐ Preservation rule: Windows upload copies previous version's `PosixMode` to new version

**Code verification — Client (Linux):**
- ☐ On upload: reads `File.GetUnixFileMode(filePath)` → sends as `PosixMode`
- ☐ On download: applies `File.SetUnixFileMode(filePath, (UnixFileMode)posixMode)`
  - ☐ If `PosixMode` null (from Windows): defaults to `0o644` for files, `0o755` for dirs
- ☐ setuid/setgid: stored but NOT applied on download (logged)
- ☐ Detects permission-only changes in periodic scan → queues metadata-only sync

**Code verification — Client (Windows):**
- ☐ Sends `PosixMode = null`, `PosixOwnerHint = null` on upload
- ☐ Ignores both fields on download

**Build verification:**
- ☐ Both projects build

**Test verification:**
- ☐ Linux → server → Linux: execute bit preserved
- ☐ Windows: no crashes or breakage on permission fields

---

### Task 4.3: Symbolic Link Policy

**Status per plan:** ✅ COMPLETE (Server `d3a6422`, Client `1cd594a`)

**Code verification:**
- ☐ **Default (ignore):** `SyncEngine` checks `FileInfo.LinkTarget != null` → skips + logs
  - ☐ Also checks `FileAttributes.ReparsePoint` on Windows
- ☐ Config in `sync-settings.json`: `"symlinks": { "mode": "ignore" }` (options: `"ignore"`, `"sync-as-link"`)
- ☐ Server: `FileNodeType.SymbolicLink` enum value, `LinkTarget` column (nullable string) on `FileNode`
- ☐ When `"sync-as-link"`:
  - ☐ Client upload: sends link target as metadata only (no content/chunks)
  - ☐ Client download: `File.CreateSymbolicLink()` on Linux; Windows requires admin/dev mode
  - ☐ Only syncs **relative** symlinks within sync root (rejects absolute and escaping links)
- ☐ Settings UI: dropdown "Symbolic links" → "Ignore" / "Sync as links"

**Build verification:**
- ☐ Both projects build

**Test verification:**
- ☐ Create symlink in sync folder → verify it is ignored by default
- ☐ Enable "sync-as-link" → verify metadata synced correctly

---

### Task 4.4: inotify Watch Limits + inode Monitoring

**Status per plan:** ✅ COMPLETE (Server `d3a6422`, Client `1cd594a`)

**Code verification — Client (Linux):**
- ☐ On `SyncEngine.StartAsync()` (Linux only):
  - ☐ Reads `max_user_watches` from `/proc/sys/fs/inotify/max_user_watches`
  - ☐ Counts subdirectories, computes target (max of 524288 or 1.5× count)
  - ☐ If current < target: shows notification with "Fix automatically" option
- ☐ If `FileSystemWatcher` raises `Error` event: falls back to fast periodic scan (30 seconds)
- ☐ inode check (via `df -i` or `statvfs`):
  - ☐ < 5% free: warning notification
  - ☐ < 1% free: critical notification

**Code verification — Server (Linux):**
- ☐ On startup: reads inotify limits, logs, warns if low
- ☐ inode check on startup + every 30 minutes:
  - ☐ ≥ 10%: healthy
  - ☐ 2–10%: degraded
  - ☐ < 2%: unhealthy
- ☐ inode status included in `/health/ready` endpoint

**Build verification:**
- ☐ Both projects build

**Test verification:**
- ☐ Linux client: verify inotify limit detection on startup
- ☐ Server: verify health check includes inode status

---

### Task 4.5: Path Length + Filename Limit Handling

**Status per plan:** ✅ COMPLETE (Server `d3a6422`, Client `1cd594a`)

**Code verification — Client (Windows):**
- ☐ `<longPathAware>true</longPathAware>` in app manifest
- ☐ On first run: checks registry `HKLM\...\LongPathsEnabled`
  - ☐ If 0: shows notification to enable long paths
- ☐ Before writing files > 259 chars: tries `\\?\` prefix
  - ☐ If fails: marks file `SyncStateTag.PathTooLong`, logs, notifies
- ☐ `SyncStateTag = "PathTooLong"` value exists

**Code verification — Client (Linux):**
- ☐ Before writing: checks `Encoding.UTF8.GetByteCount(filename) > 255`
  - ☐ If too long: truncates preserving extension + UTF-8 validity, appends `~{4-char-hash}.{ext}`

**Code verification — Server:**
- ☐ Filename > 255 chars → reject with 400
- ☐ Path > 250 chars → accept but add `X-Path-Warning: path-length-exceeds-windows-limit` header
- ☐ Filename contains `\ / : * ? " < > |` or control chars → reject with 400
- ☐ Filename matches reserved names (`CON`, `PRN`, `AUX`, `NUL`, `COM1`–`COM9`, `LPT1`–`LPT9`) → reject with 400
- ☐ Config: `FileSystem:MaxPathWarningThreshold: 250`

**Build verification:**
- ☐ Both projects build

**Test verification:**
- ☐ Create file with >255 char name → server rejects
- ☐ Create file with invalid chars → server rejects
- ☐ Client handles `PathTooLong` state gracefully

---

## Batch 5 — Polish

### Task 5.1: Bandwidth Throttling

**Status per plan:** ✅ COMPLETE (commit `bbf8c6e`)

**Code verification:**
- ☐ `ThrottledStream` class with token bucket algorithm
  - ☐ `bytesPerSecond <= 0` → no throttling (pass-through)
  - ☐ Read and write throttling support
- ☐ `ThrottledHttpHandler : DelegatingHandler`
  - ☐ Wraps request content stream (upload throttle) and response content stream (download throttle)
- ☐ Reads limits from `SyncContext` config (`UploadLimitKbps`, `DownloadLimitKbps`)
- ☐ Wired into `HttpClientFactory` setup
- ☐ Settings UI fields connected to save/load from config

**Build verification:**
- ☐ Client builds

**Test verification:**
- ☐ Set download limit to 100 KB/s → download 10MB file → verify ~100 second duration
- ☐ Set 0 → verify no throttling

---

### Task 5.2: Selective Sync Folder Browser

**Status per plan:** ✅ COMPLETE (commit `bbf8c6e`)

**Code verification:**
- ☐ `FolderBrowserViewModel`:
  - ☐ Fetches folder tree from `GET /api/v1/sync/tree`
  - ☐ Tree with checkboxes (checked = sync, unchecked = exclude)
  - ☐ Partial/indeterminate check state for mixed children
  - ☐ Lazy-load children on expand
- ☐ `FolderBrowserView` using Avalonia `TreeView` with `CheckBox` template
- ☐ Accessible from: add-account flow + Settings → account → "Choose folders to sync"
- ☐ Saves selections to `SelectiveSyncConfig`
- ☐ When selections change: deletes locally excluded files, downloads newly included files

**Build verification:**
- ☐ Client builds

**Test verification:**
- ☐ `FolderBrowserViewModelTests` pass
- ☐ `SelectiveSyncConfigTests` pass
- ☐ Manual: uncheck folder → verify files deleted locally; re-check → verify re-downloaded

---

## High-Risk Items Requiring Extra Attention

These items had inconsistencies between the guide specification and the code survey. They should be verified first.

### 1. Three-Pane Merge Editor (Task 3.5e) — **HIGH PRIORITY**

**Concern:** No `MergeEditorWindow`, `MergeEditorView`, or merge editor UI file was found in `src/Clients/DotNetCloud.Client.SyncTray/Views/`. The `ConflictViewModel` has Keep/Both/OpenFolder commands but no visible Merge command. DiffPlex is a package dependency (through Client.Core), but no UI usage was found.

**Verification steps:**
1. Search for any merge-related views or windows: `Get-ChildItem -Recurse -Filter "*merge*" src/Clients/`
2. Check if `ConflictViewModel` has a merge/open editor command
3. Check if there's a `Merge` button in the conflicts panel XAML
4. Check for `Microsoft.XmlDiffPatch` NuGet package in SyncTray `.csproj`
5. If missing: this is the largest unimplemented deliverable

### 2. Client-Side ETag Support (Task 2.6) — **MEDIUM PRIORITY**

**Concern:** Server has full ETag/If-None-Match support in `FilesController.cs`. Client's `DownloadChunkByHashAsync()` appears to be a simple GET without `If-None-Match` headers. May be handled at a different layer (e.g., `ChunkedTransferClient`).

**Verification steps:**
1. Read `DotNetCloudApiClient.DownloadChunkByHashAsync()` fully
2. Search for `If-None-Match` or `IfNoneMatch` in all client code
3. Check `ChunkedTransferClient` download logic for local chunk cache checks
4. If missing: client always re-downloads chunks it already has

### 3. Compression Skip for Already-Compressed Types (Task 2.3) — **LOW PRIORITY**

**Concern:** Client compresses all upload chunks with GZip. The guide specifies skipping compression for already-compressed MIME types (`.jpg`, `.zip`, `.mp4`, etc.).

**Verification steps:**
1. Check `UploadChunkAsync()` for MIME type or extension check before gzip wrapping
2. If always compressed: minor inefficiency but not a correctness issue

---

## Build Verification (Global)

After all code-level checks, run:

```bash
# Full solution build
dotnet build DotNetCloud.sln

# All tests
dotnet test DotNetCloud.sln

# Specific test projects for sync
dotnet test tests/DotNetCloud.Client.Core.Tests/
dotnet test tests/DotNetCloud.Client.SyncService.Tests/
dotnet test tests/DotNetCloud.Client.SyncTray.Tests/
dotnet test tests/DotNetCloud.Modules.Files.Tests/
```

All must pass with zero errors before marking the sync implementation as fully verified.

---

## Execution Approach

**Phase 1 — High-risk items first (same session):**
Verify items 1–3 from the "High-Risk Items" section above. These are the most likely gaps.

**Phase 2 — Server-side code walkthroughs:**
Walk through each Batch 1–5 server task checking code existence and correctness.

**Phase 3 — Client-side code walkthroughs:**
Walk through each Batch 1–5 client task checking code existence and correctness.

**Phase 4 — Build + test pass:**
Full solution build and test run.

**Phase 5 — Manual integration testing:**
Cross-machine sync tests between mint22 (server) and Windows11-TestDNC (client).
