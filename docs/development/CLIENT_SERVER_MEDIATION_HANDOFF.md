# Client/Server Mediation Handoff

Last updated: 2026-03-08

Purpose: Shared handoff between client-side and server-side agents, mediated by user.

> Archived context (22 resolved issues from initial sync milestone) moved to
> [CLIENT_SERVER_MEDIATION_ARCHIVE.md](CLIENT_SERVER_MEDIATION_ARCHIVE.md).
> Full git history in commits up to `8e02b52`.

## Process Rules

- All technical findings and debugging conclusions go in this document, pushed to `main`.
- Mediator role is relay-only — commit notifications and cross-agent request forwarding.

## Current Status

**Completed milestone:** End-to-end file sync with directory hierarchy (Issues #1–#22, all resolved).

**Batch 1 complete.** All Tasks 1.1 through 1.9 are done (Issues #23–#29, all resolved).

**Batch 2 complete.** All Tasks 2.1–2.3 done (Issues #30–#32, all resolved).

**Batch 3 starting.** Task 3.1 (.syncignore) is next — see Issue #33. All Batch 3 tasks are client-only.

## Environment

| | Machine | Detail |
|---|---------|--------|
| Server | `mint22` | `https://mint22:15443/` |
| Client | `Windows11-TestDNC` | Sync dir: `C:\Users\benk\Documents\synctray` |

## Key Architecture Decisions (Carry Forward)

- **Auth:** OpenIddict bearer on all files/sync endpoints via `FilesControllerBase` `[Authorize]`. Persistent RSA keys in `{DOTNETCLOUD_DATA_DIR}/oidc-keys/`. `DisableAccessTokenEncryption()`.
- **API contract:** All endpoints use `GetAuthenticatedCaller()` (no `userId` query param). All return raw payloads — `ResponseEnvelopeMiddleware` wraps automatically. Client unwraps envelope via `ReadEnvelopeDataAsync<T>()`.
- **Sync flow:** changes → tree → reconcile → chunk manifest → chunk download → file assembly. `since` param converted to UTC kind. Client builds `nodeId→path` map from folder tree.
- **Token handling:** Client uses `DateTimeOffset` for expiry. `RefreshTokenAsync` sends `client_id`. `OAuthConstants.ClientId = "dotnetcloud-desktop"`.

## Relay Template

```markdown
### Send to [Server|Client] Agent
<message text>

### Request Back
- commit hash
- raw endpoint/URL used
- raw error/query params
- raw log lines around the event (with timestamp)
```

## Active Handoff

### Issue #23: Batch 1 Task 1.1 - Sync Service Logging (Client only)

**Server-side status:** Not applicable (client-only task).
**Client-side status:** ✅ VALIDATED on `Windows11-TestDNC` at commit `c69aeac` (2026-03-08).

**Validation results from Windows11-TestDNC:**
- Commit: `c69aeac`
- Restore/build: no errors
- Log file: `%APPDATA%\DotNetCloud\logs\sync-service20260308.log` (3258 bytes, date suffix normal for `RollingInterval.Day`)
- JSON entries confirmed:
	- `DotNetCloud Sync Service starting.`
	- `Loading 1 persisted sync context(s).`
	- `Sync engine started for context ... (C:\Users\benk\Documents\synctray)`
	- `DotNetCloud Sync Service running — 1 context(s) active.`
	- `IPC server started (Named Pipe).`
	- Full graceful shutdown sequence logged
- **Task 1.1 (client): PASS**

---

### Issue #24: Batch 1 Task 1.1b - Sync Audit Logging (Server only)

**Server-side status:** ✅ COMPLETE — commit `c585dae` (2026-03-08).
**Client-side status:** Not applicable (server-only task).

---

### Issue #25: Batch 1 Task 1.2 - Request Correlation IDs (Client side)

**Server-side status:** ✅ COMPLETE — commit `16dd7df` (2026-03-08).
**Client-side status:** ✅ COMPLETE — commit `97afdd8` (2026-03-08).

**What was implemented:**
- `src/Clients/DotNetCloud.Client.Core/Api/CorrelationIdHandler.cs` — `DelegatingHandler` that attaches `X-Request-ID: <guid>` and logs every outgoing call + failures
- Registered on typed `DotNetCloudApiClient` HttpClient (via `ClientCoreServiceExtensions`)
- Registered on named `"DotNetCloudSync"` HttpClient (via `SyncServiceExtensions`)
- Build: 0 errors
- `sync-now` IPC accepted (`"success":true`)

**Task 1.2: PASS (both sides complete)**

---

### Issue #26: Batch 1 Task 1.3 - Server-Side Rate Limiting on Sync Endpoints

**Server-side status:** ✅ COMPLETE — commit `4570c16` (2026-03-08).
**Client-side status:** ✅ No changes needed — client already handles 429 + `Retry-After` in `DotNetCloudApiClient.SendWithRetryAsync()`.

**What was implemented:**
- `appsettings.json`: `ModuleLimits` populated — `sync-changes` (60/min), `sync-tree` (10/min), `sync-reconcile` (30/min), `upload-initiate` (30/min), `upload-chunks` (300/min), `download` (120/min), `chunks` (300/min)
- `SyncController.cs`: `[EnableRateLimiting("module-sync-changes|tree|reconcile")]` on the three sync endpoints
- `FilesController.cs`: `[EnableRateLimiting(...)]` on `InitiateUpload`, `UploadChunk`, `Download`, `GetChunkManifest`, `DownloadChunkByHash`
- Build: 0 errors; 304 server tests passed

**Task 1.3: PASS (server complete, client no changes needed)**

**IMPORTANT CONTEXT: Rate limiting infrastructure already exists.** Do NOT create new middleware or configuration classes. The following are already in place and working:
- `src/Core/DotNetCloud.Core.Server/Configuration/RateLimitingConfiguration.cs` — has `AddDotNetCloudRateLimiting()`, policies (`"global"`, `"authenticated"`, per-module `"module-{name}"`), 429 rejection handler with `Retry-After` header.
- `appsettings.json` already has a `"RateLimiting"` section with `Enabled`, `GlobalPermitLimit`, `AuthenticatedPermitLimit`, `ModuleLimits`, etc.
- Pipeline: `app.UseDotNetCloudRateLimiting()` is already called in `Program.cs`.

**What needs to happen (3 steps):**

**Step 1:** Add sync-specific module limits to `src/Core/DotNetCloud.Core.Server/appsettings.json` under `RateLimiting.ModuleLimits`:

```json
"ModuleLimits": {
  "sync-changes":   { "PermitLimit": 60,  "WindowSeconds": 60 },
  "sync-tree":      { "PermitLimit": 10,  "WindowSeconds": 60 },
  "sync-reconcile": { "PermitLimit": 30,  "WindowSeconds": 60 },
  "upload-initiate": { "PermitLimit": 30, "WindowSeconds": 60 },
  "upload-chunks":  { "PermitLimit": 300, "WindowSeconds": 60 },
  "download":       { "PermitLimit": 120, "WindowSeconds": 60 },
  "chunks":         { "PermitLimit": 300, "WindowSeconds": 60 }
}
```

**Step 2:** Add `[EnableRateLimiting("module-{name}")]` attributes to the specific methods. Add `using Microsoft.AspNetCore.RateLimiting;` to both controllers if not present.

In `src/Core/DotNetCloud.Core.Server/Controllers/SyncController.cs`:
```csharp
[HttpGet("changes")]
[EnableRateLimiting("module-sync-changes")]
public Task<IActionResult> GetChangesAsync(...)

[HttpGet("tree")]
[EnableRateLimiting("module-sync-tree")]
public Task<IActionResult> GetTreeAsync(...)

[HttpPost("reconcile")]
[EnableRateLimiting("module-sync-reconcile")]
public Task<IActionResult> ReconcileAsync(...)
```

In `src/Core/DotNetCloud.Core.Server/Controllers/FilesController.cs`:
```csharp
[HttpPost("upload/initiate")]
[EnableRateLimiting("module-upload-initiate")]
public Task<IActionResult> InitiateUploadAsync(...)

[HttpPut("upload/{sessionId:guid}/chunks/{chunkHash}")]
[EnableRateLimiting("module-upload-chunks")]
public Task<IActionResult> UploadChunkAsync(...)

[HttpGet("{nodeId:guid}/download")]
[EnableRateLimiting("module-download")]
public Task<IActionResult> DownloadAsync(...)

[HttpGet("{nodeId:guid}/chunks")]
[EnableRateLimiting("module-chunks")]
public Task<IActionResult> GetChunkManifestAsync(...)

[HttpGet("chunks/{chunkHash}")]
[EnableRateLimiting("module-chunks")]
public Task<IActionResult> DownloadChunkAsync(...)
```

**Step 3:** Build and test:
```bash
cd /path/to/dotnetcloud
dotnet build src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj
dotnet test tests/DotNetCloud.Core.Server.Tests/
```

**Request back from server agent:**
- commit hash
- build output (0 errors expected)
- confirm `appsettings.json` has the `ModuleLimits` section above

---

### Issue #27: Batch 1 Task 1.4 - Chunk Integrity Verification on Download (Client only)

**Server-side status:** Not applicable (client-only task).
**Client-side status:** ✅ COMPLETE — `Windows11-TestDNC` (2026-03-08).

**What was implemented:**
- Created `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkIntegrityException.cs` — exception thrown when a chunk fails integrity check after all retries
- Modified `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkedTransferClient.cs` — `DownloadChunksAsync()` now:
  - Computes `SHA256.HashData(bytes)` on every downloaded chunk
  - Compares against the manifest hash (`StringComparison.OrdinalIgnoreCase`)
  - Retries up to 3 times on mismatch (`LogWarning` per failed attempt)
  - Throws `ChunkIntegrityException` after 3 failures (`LogError`)
- Updated `tests/DotNetCloud.Client.Core.Tests/Transfer/ChunkedTransferClientTests.cs`:
  - Fixed existing `DownloadAsync_WithManifest_DownloadsChunks` to use real SHA-256 hash (was using fake `"abc123"`)
  - Added `DownloadAsync_ChunkHashMismatch_RetriesAndSucceeds` — verifies retry on first bad chunk, succeeds on second
  - Added `DownloadAsync_ChunkHashAlwaysMismatch_ThrowsChunkIntegrityException` — verifies 3 retries then exception
- Build: 0 errors. All 55 `DotNetCloud.Client.Core.Tests` pass.

**Task 1.4: PASS (client complete)**

---

### Issue #28: Batch 1 Tasks 1.5, 1.6, 1.7 — Client-only (Windows11-TestDNC)

**Server-side status:** Not applicable (all three are client-only).
**Client-side status:** ✅ COMPLETE — commit `1aa6b18` (2026-03-08).

Pull latest (`git pull`) before starting — server Tasks 1.8 and 1.9 were completed in the same push.

---

#### Task 1.5: Per-Chunk Retry with Exponential Backoff ✅

**What was implemented:**
- `ChunkTransferResult` record added to `ChunkedTransferClient` (Hash, Success, Attempts, Error)
- Upload path: each chunk now retries up to 3× on `HttpRequestException` with no HTTP status code or status ≥ 500. Backoff: `2^(attempt-1)` seconds + jitter (0–500 ms). 4xx (including 429) is NOT retried.
- Download path: existing hash-check loop now also catches `HttpRequestException`/5xx with the same backoff. Hash mismatches still loop immediately (no added delay).
- Final result logged per chunk on success: `Chunk {Hash} upload complete: Attempts={Attempts}.`
- New tests: `UploadAsync_NetworkErrorOnFirstAttempt_RetriesAndSucceeds`, `UploadAsync_NetworkErrorExhaustsRetries_Throws`, `UploadAsync_ClientError_DoesNotRetry`

---

#### Task 1.6: SQLite WAL Mode + Corruption Recovery ✅

**What was implemented:**
- WAL mode enabled via `PRAGMA journal_mode=WAL;` in `RunSchemaEvolutionAsync` (connection string keyword unsupported by `Microsoft.Data.Sqlite`; PRAGMA persists in DB file header)
- `InitializeAsync`: runs `PRAGMA integrity_check;` after `EnsureCreatedAsync`; on failure (or any exception opening the DB) archives corrupt files as `{path}.corrupt.{yyyyMMddHHmmss}`, clears SQLite connection pools, recreates fresh DB, sets `_resetPaths` flag
- `WasRecentlyReset(dbPath)`: returns `true` if DB was just recreated from corruption — `SyncEngine.StartAsync` logs a warning when this is true
- `CheckpointWalAsync`: `PRAGMA wal_checkpoint(TRUNCATE)` — called from `SyncEngine.SyncAsync` after `UpdateCheckpointAsync` at the end of each sync pass
- New tests: `InitializeAsync_EnablesWalMode`, `InitializeAsync_CorruptDb_ArchivesAndCreatesNewDb`, `CheckpointWalAsync_DoesNotThrow`

---

#### Task 1.7: Operation Retry Queue with Backoff ✅

**What was implemented:**
- `PendingOperationRecord` base class: added `NextRetryAt DateTime?` and `LastError string?`
- `PendingOperationDbRow`: added `NextRetryAt` and `LastError` columns
- `FailedOperationDbRow`: new entity (same schema + `FailedAt DateTime`); `LocalStateDbContext.FailedOperations` DbSet; EF model configured
- Schema evolution in `RunSchemaEvolutionAsync`: `ALTER TABLE PendingOperations ADD COLUMN` for existing DBs; `CREATE TABLE IF NOT EXISTS FailedOperations`
- `GetPendingOperationsAsync`: filter `WHERE NextRetryAt IS NULL OR NextRetryAt <= @now`
- `UpdateOperationRetryAsync`: new interface + implementation method
- `MoveToFailedAsync`: removes from `PendingOperations`, inserts into `FailedOperations` in one `SaveChangesAsync`
- `SyncEngine.ApplyLocalChangesAsync`: on failure increments `RetryCount`, computes `NextRetryAt` per schedule — 1 min → 5 min → 15 min → 1 h → 6 h (repeat); at `RetryCount >= 10` calls `MoveToFailedAsync`
- `ComputeNextRetryAt`: static helper for the backoff schedule
- New tests: `GetPendingOperationsAsync_ExcludesFutureRetry`, `UpdateOperationRetryAsync_UpdatesRetryFields`, `MoveToFailedAsync_RemovesFromPendingAndAddsToFailed`

**Validation results from Windows11-TestDNC:**
- Commit: `1aa6b18`
- Build: 0 errors
- Tests: 64 passed, 0 failed (was 55, +9 new tests)
- `push` failed (Gitea auth) — commit is local, user to push manually

**Tasks 1.5, 1.6, 1.7: PASS**

---

### Issue #29: Batch 1 Tasks 1.8 + 1.9 — Server-side (mint22)

**Server-side status:** ✅ COMPLETE — commit `82ca53b` (2026-03-08).
**Client-side status:** Not applicable.

**What was implemented (Task 1.8 — Secure Temp File Handling):**
- New `FileUploadOptions` class (`src/Modules/Files/DotNetCloud.Modules.Files/Options/FileUploadOptions.cs`) — holds `MaxFileSizeBytes` (default 15 GB) and `TmpPath` (set programmatically)
- `Program.cs` — computes `{DOTNETCLOUD_DATA_DIR}/tmp/`, creates it with `700` permissions (Linux), wires into `FileUploadOptions.TmpPath` via `PostConfigure`
- `DownloadService.cs` — injects `IOptions<FileUploadOptions>`; uses `TmpPath` for all temp files instead of `Path.GetTempPath()`
- New `TempFileCleanupService` hosted service — on startup, ensures `tmp/` dir exists with `700` permissions and deletes any files older than 1 hour

**What was implemented (Task 1.9 — File Scanning Interface + Execution Prevention):**
- `FileScanStatus` enum: `NotScanned = 0`, `Clean = 1`, `Threat = 2`, `Error = 3`
- `IFileScanner` interface + `ScanResult` record (`src/Modules/Files/DotNetCloud.Modules.Files/Services/IFileScanner.cs`)
- `NoOpFileScanner` — always returns `IsClean: true`; registered as `services.AddSingleton<IFileScanner, NoOpFileScanner>()`
- `FileVersion` model — new nullable `ScanStatus` property; EF migration `AddFileVersionScanStatus` generated
- `LocalFileStorageEngine.WriteChunkAsync()` — `File.SetUnixFileMode(fullPath, UserRead | UserWrite)` after writing (Linux/macOS only)
- `FilesController.DownloadAsync` + `DownloadChunkByHashAsync` — `Response.Headers["X-Content-Type-Options"] = "nosniff"` before returning file
- `ChunkedUploadService.InitiateUploadAsync()` — rejects uploads where `dto.TotalSize > _maxFileSizeBytes`
- `appsettings.json` — `"FileUpload": { "MaxFileSizeBytes": 16106127360 }`
- Build: 0 errors; 304 server tests + 513 files tests pass

---

### Issue #30: Batch 2 Task 2.1 - Content-Defined Chunking (CDC) — Server complete, client next

**Server-side status:** ✅ COMPLETE — commit `3a7e0ae` (2026-03-08).
**Client-side status:** ✅ COMPLETE — commit `bc9e08a` (2026-03-08).

**What was implemented (server):**

- `ContentHasher.cs` — new `CdcChunkInfo(Hash, Offset, Size)` record + `ChunkAndHashCdcAsync()` using Gear hash rolling. Deterministic GearTable (Knuth LCG seed). Parameters: `avgSize` (default 4 MB), `minSize` (default 512 KB), `maxSize` (default 16 MB).
- `FileVersionChunk.cs` — new `Offset` (long) and `ChunkSize` (int) columns. Default `0` for backward-compat legacy rows.
- `ChunkedUploadSession.cs` — new nullable `ChunkSizesManifest` (JSON int array). `null` = legacy fixed-size upload.
- `InitiateUploadDto` — optional `IReadOnlyList<int>? ChunkSizes` field. `null`/empty = legacy.
- `files_service.proto` — `repeated int32 chunk_sizes = 7` added to `InitiateUploadRequest` (optional; omit for legacy).
- `ChunkedUploadService.InitiateUploadAsync()` — serializes `ChunkSizes` to `ChunkSizesManifest` when present.
- `ChunkedUploadService.CompleteUploadAsync()` — computes cumulative `Offset` per chunk from `ChunkSizesManifest`; falls back to `FileChunk.Size` for legacy uploads.
- `FilesGrpcService.InitiateUpload()` — propagates `chunk_sizes` from proto to session.
- EF migration `AddCdcChunkMetadata`: adds `Offset`/`ChunkSize` on `FileVersionChunks`, `ChunkSizesManifest` on `UploadSessions`.
- 11 new tests (8 CDC `ContentHasherTests` + 3 `ChunkedUploadServiceTests`). 524 total (was 513). 0 errors.

**What the client needs to do:**

1. **Replace `SplitIntoChunksAsync()` in `ChunkedTransferClient`** with FastCDC-based splitting. You can call the server's `ContentHasher.ChunkAndHashCdcAsync()` directly if you reference it, OR implement the Gear hash client-side. The Gear table seed is `0xDC44636E65744E44UL` (Knuth LCG with multiplier `6364136223846793005` and increment `1442695040888963407`). Same constants = same chunk boundaries on both sides for future verification.

2. **Send chunk sizes alongside hashes** in `DotNetCloudApiClient.InitiateUploadAsync()`. The REST DTO is `InitiateUploadDto` which now has `IReadOnlyList<int>? ChunkSizes`. Populate this with the CDC chunk sizes.

3. **Advertise CDC capability** via `X-Sync-Capabilities: cdc` request header (add to `CorrelationIdHandler` or a separate header handler). Server will use this for future feature negotiation.

4. **Download**: chunk manifest from server now includes `Offset` and `ChunkSize` per chunk. Verify `ChunkedTransferClient.DownloadAsync()` still assembles correctly (it should since it already concatenates chunks in sequence order).

**REST endpoint context:**
- `POST /api/v1/files/upload/initiate` — accepts `InitiateUploadDto` JSON, now has `ChunkSizes` array
- `UploadSessionDto` response is unchanged (still returns `ExistingChunks` / `MissingChunks` by hash)

**Backward compatibility guaranteed:** If client sends no `ChunkSizes`, server stores `ChunkSizesManifest = null` and falls back to `FileChunk.Size` in `CompleteUploadAsync`. Legacy clients work without change.

**What was implemented (client) — commit `bc9e08a`:**

- `ChunkedTransferClient.cs` — replaced fixed-size `SplitIntoChunksAsync` with CDC implementation using Gear hash (FastCDC). Same constants as server (`seed=0xDC44636E65744E44UL`, multiplier/increment per Knuth LCG). MinSize 512 KB, AvgSize 4 MB (mask), MaxSize 16 MB. 64 KB read buffer with two-phase processing (fast phase skip + byte-by-byte boundary detection). `using System.Numerics` added for `BitOperations.Log2`.
- `IDotNetCloudApiClient.InitiateUploadAsync` — signature updated: added `IReadOnlyList<int>? chunkSizes = null` parameter.
- `DotNetCloudApiClient.InitiateUploadAsync` — sends `chunkSizes` in JSON body alongside `chunkHashes`.
- `CorrelationIdHandler.cs` — adds `X-Sync-Capabilities: cdc` header to every outgoing request.
- `UploadAsync` in `ChunkedTransferClient` now passes `chunks.Select(c => c.Data.Length).ToList()` as chunk sizes.
- 2 new tests: `UploadAsync_CdcChunking_SendsChunkSizesWithHashes` (verifies non-null sizes, count matches hashes, sum equals file size); `UploadAsync_CdcChunking_DeterministicAcrossMultipleCalls` (same 2 MB file chunked twice → identical hashes).
- Build: 0 errors. Tests: 66 passed, 0 failed (was 64, +2 new).

**Validation results from Windows11-TestDNC:**
- Commit: `bc9e08a`
- Build: 0 errors
- Tests: 66 passed, 0 failed (was 64, +2 new CDC tests)
- CDC chunk boundaries confirmed stable: `UploadAsync_CdcChunking_DeterministicAcrossMultipleCalls` PASS

**Task 2.1: PASS (both sides complete)**

---

### Issue #31: Batch 2 Task 2.2 - Streaming Chunk Pipeline — Client only

**Server-side status:** Not applicable (client-only task).
**Client-side status:** ✅ COMPLETE — commit `2e0788c` (2026-03-08).

**⚠️ PROCESS NOTE FOR CLIENT AGENT:**
Please follow the handoff process carefully:
1. Pull latest (`git pull`) before starting.
2. Build and test on `Windows11-TestDNC` (Windows). Do NOT build this on the server — this is client-only code.
3. After committing, update this document with: commit hash, build result (0 errors), test count (was 66, should increase), and mark status ✅ COMPLETE.
4. Use **targeted edits only** — do not replace the entire handoff file. Preserve all existing issue entries and the Process Rules / Key Architecture Decisions / Relay Template sections at the top.
5. Push to `main` so the server agent can pull and move to the next task.

**What was implemented:**

**Upload — two-pass CDC with bounded-channel pipeline:**
- Pass 1: `ComputeChunkMetadataAsync()` streams the file through CDC computing only hashes and sizes — no chunk data retained. Memory: 64 KB read buffer + 32-byte incremental hash state.
- `InitiateUploadAsync()` called with metadata from pass 1.
- Pass 2: `fileStream.Seek(0)` then `ChunkFileAsync()` (`IAsyncEnumerable<ChunkData>`) re-reads the file via CDC, yielding one chunk at a time into a `Channel.CreateBounded<(ChunkData, int)>(capacity: 8)` pipeline.
- `MaxConcurrency` (4) consumer tasks drain the channel via `ReadAllAsync`, handling per-chunk retry logic with exponential backoff.
- Peak memory truly bounded: ChannelCapacity (8) × avg chunk size ≈ 32 MB regardless of file size.
- `using System.Runtime.CompilerServices` added for `[EnumeratorCancellation]` on the async enumerator.

**Download — file-backed streaming assembly:**
- `DownloadChunksAsync()`: each verified chunk is written to `{TempPath}/dnc-chunks/{guid}/{index}` (unchanged).
- Final assembly now concatenates temp chunk files into a single temp file (`dnc-{guid}.tmp`) via `File.Create` + `CopyToAsync` — one chunk in memory at a time.
- Returns `FileStream` with `FileOptions.DeleteOnClose | FileOptions.Asynchronous` — OS deletes the assembled file when the caller disposes the stream.
- `catch` block cleans up assembled file on error; `finally` block always cleans up per-chunk temp directory.
- Memory: bounded regardless of file size (no full-file MemoryStream).

**New tests:**
- `UploadAsync_StreamingPipeline_BoundedMemoryUsage` — uploads 1 MB file via pipeline; asserts upload call count equals chunk count (all missing).
- `DownloadAsync_StreamingToTempFiles_AssemblesCorrectly` — downloads 2-chunk file; verifies output stream length (1024) and byte-level ordering (chunk0 first, chunk1 second).

**Validation results from Windows11-TestDNC:**
- Commit: `7cbc12e`
- Build: 0 errors
- Tests: 68 passed, 0 failed (was 66, +2 new streaming tests)
- Channel-based upload confirmed: `UploadAsync_StreamingPipeline_BoundedMemoryUsage` PASS
- Temp-file download confirmed: `DownloadAsync_StreamingToTempFiles_AssemblesCorrectly` PASS
- Memory bounded: two-pass upload (no bulk chunk buffering), file-backed download assembly (no full-file MemoryStream)

**Task 2.2: PASS (client complete)**

**What to implement:**

Replace the in-memory bulk approach in `ChunkedTransferClient` with a bounded-channel producer/consumer pipeline for uploads, and temp-file-based streaming for downloads. The goal is bounded memory use regardless of file size.

**File:** `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkedTransferClient.cs`
**Tests:** `tests/DotNetCloud.Client.Core.Tests/Transfer/ChunkedTransferClientTests.cs`

**Upload — replace current `Task.WhenAll` approach with a bounded channel pipeline:**

```csharp
var channel = Channel.CreateBounded<ChunkData>(new BoundedChannelOptions(8)
{
    FullMode = BoundedChannelFullMode.Wait
});

// Producer: split file via CDC → write chunks to channel
var producer = Task.Run(async () =>
{
    await foreach (var chunk in ChunkFileAsync(fileStream, cancellationToken))
        await channel.Writer.WriteAsync(chunk, cancellationToken);
    channel.Writer.Complete();
}, cancellationToken);

// Consumers: 4 parallel uploaders drain the channel
var consumers = Enumerable.Range(0, MaxConcurrency).Select(_ => Task.Run(async () =>
{
    await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken))
        await UploadChunkAsync(sessionId, chunk, presentChunks, cancellationToken);
}, cancellationToken));

await Task.WhenAll(new[] { producer }.Concat(consumers));
```

Peak memory target: ~32 MB (8 slots × 4 MB avg).

**Download — stream chunks to temp files, then concatenate:**

Instead of holding all `byte[]` chunks in a `chunks[]` array, download each chunk to a temp file, then concatenate into the target stream. Clean up temp files afterwards.

```csharp
// For each chunk: download → write to temp file
var tempDir = Path.Combine(Path.GetTempPath(), "dnc-chunks", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempDir);
try
{
    // Download all chunks in parallel (bounded by MaxConcurrency) → temp files
    // ...

    // Concatenate in order into output stream
    var output = new MemoryStream();
    for (var i = 0; i < manifest.Chunks.Count; i++)
    {
        var chunkPath = Path.Combine(tempDir, $"{i}");
        using var f = File.OpenRead(chunkPath);
        await f.CopyToAsync(output, cancellationToken);
    }
    output.Seek(0, SeekOrigin.Begin);
    return output;
}
finally
{
    Directory.Delete(tempDir, recursive: true);
}
```

Memory target: bounded regardless of file size (only one chunk in memory at a time during concatenation).

**New tests to add:**
- `UploadAsync_StreamingPipeline_BoundedMemoryUsage` — upload multi-chunk file, confirm channel-based upload completes correctly (count of upload calls matches missing chunks).
- `DownloadAsync_StreamingToTempFiles_AssemblesCorrectly` — download multi-chunk file, confirm all chunks downloaded and output stream length is correct.

**No server changes required.** The server API is unchanged. This is purely a client-side refactor.

**Request back from client agent:**
- Commit hash
- Build: 0 errors
- Test count (was 66, should increase by ≥2 new tests)
- Confirm memory behavior: channel-based upload, temp-file-based download

---

### Issue #32: Batch 2 Task 2.3 - Compression for Chunk Transfers — Client side

**Server-side status:** ✅ COMPLETE — commit `032f6a2` (2026-03-08).
**Client-side status:** ✅ COMPLETE — `Windows11-TestDNC` (2026-03-08).

**⚠️ PROCESS NOTE FOR CLIENT AGENT:**
Please follow the handoff process carefully:
1. Pull latest (`git pull`) before starting.
2. Build and test on `Windows11-TestDNC` (Windows). Do NOT build this on the server — this is client-only code.
3. After committing, update this document with: commit hash, build result (0 errors), test count (was 68, should increase), and mark status ✅ COMPLETE.
4. Use **targeted edits only** — do not replace the entire handoff file. Preserve all existing issue entries and the Process Rules / Key Architecture Decisions / Relay Template sections at the top.
5. Push to `main` so the server agent can pull and move to the next task.

**What was implemented (server) — response compression middleware:**

- `src/Core/DotNetCloud.Core.Server/Program.cs`:
  - Added `using Microsoft.AspNetCore.ResponseCompression;`
  - In `ConfigureServices`: registered `AddResponseCompression()` with Brotli (preferred) + Gzip (fallback). `EnableForHttps = true`. MIME types = `ResponseCompressionDefaults.MimeTypes` + `"application/octet-stream"` (covers raw chunk downloads). Both providers set to `CompressionLevel.Fastest`.
  - In `ConfigurePipeline`: added `app.UseResponseCompression()` immediately after `app.UseForwardedHeaders()` (before all other middleware).
- `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/FilesController.cs`: no changes needed — the global middleware handles all responses matching the MIME type list.
- **MIME type strategy:** `application/octet-stream` added for chunk downloads. Already-compressed MIME types (e.g. `image/jpeg`, `video/mp4`, `application/zip`) are NOT in the list, so `DownloadAsync` for those files skips compression automatically.
- **X-Content-Type-Options: nosniff** — already set globally by `SecurityHeadersMiddleware`. NOT added per-endpoint.
- Build: 0 errors. 304 server tests + 524 files tests passed.

**What the client needs to do:**

**File:** `src/Clients/DotNetCloud.Client.Core/ClientCoreServiceExtensions.cs`  
**Also:** `src/Clients/DotNetCloud.Client.SyncService/SyncServiceExtensions.cs`  
**Tests:** `tests/DotNetCloud.Client.Core.Tests/`

**Step 1: Enable automatic decompression on both HttpClient registrations.**

Check `ClientCoreServiceExtensions.cs` — find where the typed `DotNetCloudApiClient` HttpClient is registered (look for `.AddHttpClient<DotNetCloudApiClient>` or similar). If the handler uses `HttpClientHandler` or `SocketsHttpHandler`, add:
```csharp
AutomaticDecompression = System.Net.DecompressionMethods.All
```

If using `HttpClientHandler`:
```csharp
var handler = new HttpClientHandler
{
    AutomaticDecompression = System.Net.DecompressionMethods.All
};
```

If using `SocketsHttpHandler`:
```csharp
var handler = new SocketsHttpHandler
{
    AutomaticDecompression = System.Net.DecompressionMethods.All
};
```

Repeat the same for the named `"DotNetCloudSync"` HttpClient in `SyncServiceExtensions.cs`.

**Note:** When `AutomaticDecompression = DecompressionMethods.All` is set, `HttpClient` automatically adds `Accept-Encoding: br, gzip, deflate` to outgoing requests and transparently decompresses the response. Once the server sends `Content-Encoding: br` or `Content-Encoding: gzip`, the client handles decompression without any extra code.

**Step 2: Gzip-wrap upload chunk streams.**

In `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkedTransferClient.cs`, in the chunk upload method (look for where `HttpClient.PutAsync` or `UploadChunkAsync` actually sends the chunk bytes), wrap the chunk content in GZip:

```csharp
using System.IO.Compression;

// When building the HttpContent for a chunk upload:
var compressedMs = new MemoryStream();
await using (var gzip = new GZipStream(compressedMs, CompressionLevel.Fastest, leaveOpen: true))
    await gzip.WriteAsync(chunkBytes, cancellationToken);
compressedMs.Position = 0;

var content = new StreamContent(compressedMs);
content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
content.Headers.ContentEncoding.Add("gzip");
```

**Skip compression for already-compressed MIME types.** If you know the file's MIME type at upload time (e.g. from the `PendingOperationRecord` or `SyncEngine`), skip gzip for: `image/*`, `video/*`, `audio/*`, `application/zip`, `application/gzip`, `application/x-rar-compressed`, `application/x-7z-compressed`, `application/pdf`. Otherwise, apply gzip for all other types (especially `text/*`, `application/json`, `application/xml`, `application/javascript`).

If MIME type is not available at chunk upload time, it is acceptable to always apply gzip — the overhead for already-compressed content is minimal (+0.1 to 1%) and the savings for compressible content are large (50–80%).

**Step 3: Build and test:**
```powershell
dotnet build src\Clients\DotNetCloud.Client.Core\DotNetCloud.Client.Core.csproj
dotnet test tests\DotNetCloud.Client.Core.Tests\
```

**New tests to add:**
- `UploadAsync_CompressedChunks_SetsContentEncodingGzip` — verify that the HTTP request for a chunk upload includes `Content-Encoding: gzip` header.
- `DownloadAsync_DecompressesResponse_WhenContentEncodingGzip` — mock server returns gzip-compressed chunk bytes, verify client returns the original uncompressed bytes.

**No server-side changes are needed beyond what was already implemented.** The server's response compression middleware handles all decompression of client uploads automatically (ASP.NET Core decompresses request bodies when `Content-Encoding: gzip` is present).

**Request back from client agent:**
- Commit hash
- Build: 0 errors
- Test count (was 68, should increase by ≥ 2 new tests)
- Confirm `AutomaticDecompression = All` is set on both HttpClient registrations
- Confirm `Content-Encoding: gzip` is sent on chunk uploads

**What was implemented (client) — `Windows11-TestDNC` (2026-03-08):**

- `src/Clients/DotNetCloud.Client.Core/Auth/OAuthHttpClientHandlerFactory.cs` — `CreateHandler()` now sets `AutomaticDecompression = DecompressionMethods.All` on the `HttpClientHandler`. Covers the `"DotNetCloudSync"` named client and `OAuth2Service`.
- `src/Clients/DotNetCloud.Client.Core/ClientCoreServiceExtensions.cs` — `DotNetCloudApiClient` HttpClient registration now includes `ConfigurePrimaryHttpMessageHandler` with a new `HttpClientHandler { AutomaticDecompression = All }`.
- `src/Clients/DotNetCloud.Client.Core/Api/DotNetCloudApiClient.cs` — `UploadChunkAsync()` now gzip-compresses the chunk stream before sending: creates a `MemoryStream`, writes through `GZipStream(CompressionLevel.Fastest)`, sets `Content-Encoding: gzip` on the request content.
- 2 new tests in `tests/DotNetCloud.Client.Core.Tests/Api/DotNetCloudApiClientTests.cs`:
  - `UploadChunkAsync_SetsContentEncodingGzip` — captures request inside mock handler, verifies `Content-Encoding: gzip` is present, and decompresses body to confirm it matches original bytes.
  - `DownloadChunkByHashAsync_DecompressesGzipResponse` — uses a local `GzipDecompressionHandler` delegating handler (simulating `AutomaticDecompression = All`), mock inner returns gzip-compressed bytes, verifies result stream contains original bytes.
- Build: 0 errors.
- Tests: 70 passed, 0 failed (was 68, +2 new compression tests).

**Task 2.3: PASS (client complete)**

---

### Issue #33: Batch 3 Task 3.1 — .syncignore with UI Support (Client only)

**Server-side status:** Not applicable (client-only task).
**Client-side status:** ☐ Pending.

**⚠️ PROCESS NOTE FOR CLIENT AGENT:**
1. Pull latest (`git pull`) before starting.
2. Read [SYNC_IMPROVEMENT_PLAN.md](SYNC_IMPROVEMENT_PLAN.md) section **3.1** for full scope.
3. Build and test on `Windows11-TestDNC`. Do NOT build on the server — client-only code.
4. After committing, update this document with commit hash, build result, test count, and mark ✅ COMPLETE.
5. Use **targeted edits only** — do not replace the entire handoff file.
6. Push to `main` so the server agent can pull and move to the next task.

**What to implement:**

**File:** `src/Clients/DotNetCloud.Client.Core/SyncIgnore/SyncIgnoreParser.cs` (create new)
**Also modify:** `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs`, `src/Clients/DotNetCloud.Client.Core/ClientCoreServiceExtensions.cs`, `src/Clients/DotNetCloud.Client.Core/DotNetCloud.Client.Core.csproj`
**SyncTray:** new "Ignored Files" settings panel in `src/Clients/DotNetCloud.Client.SyncTray/`
**Tests:** `tests/DotNetCloud.Client.Core.Tests/`

**Core logic (Client.Core):**
- New `SyncIgnoreParser` class:
  - Parse `.gitignore`-style patterns: `*`, `?`, `**`, `!` (negation), `/` (directory marker), `#` (comments)
  - Use `Microsoft.Extensions.FileSystemGlobbing` (add NuGet to `.csproj`)
  - Built-in defaults (OS junk, temp files, VCS dirs — see full list in SYNC_IMPROVEMENT_PLAN.md §3.1)
  - User `.syncignore` file loaded from sync root; merged with built-ins (user rules take priority)
  - `.syncignore` IS synced (shared across all clients)
- `SyncEngine`: call `SyncIgnoreParser.IsIgnored(relativePath)` before:
  - Queuing uploads from FileSystemWatcher events
  - Applying remote changes (don't download ignored files)
  - Running periodic scan comparisons
- Register `SyncIgnoreParser` in DI via `ClientCoreServiceExtensions`

**SyncTray UI:**
- New "Ignored Files" settings panel:
  - Display all rules (built-in defaults shown in gray/italic, non-editable; user rules editable)
  - "Add pattern" button, "Remove pattern" button, "Edit .syncignore" button (opens in system text editor)
  - "Test a path" preview input — user types a file path, sees whether it would be ignored and by which rule
  - Saves to `.syncignore` in sync root on edit

**Platform notes:**
- Add Linux-specific default: `.directory` (KDE metadata)
- Add macOS-specific defaults: `.Spotlight-V100/`, `.Trashes/`, `._*` (resource forks)
- All defaults compiled in on all platforms (presence of file not required)

**New tests to add:**
- `IsIgnored_BuiltInDefaults_IgnoresOsJunk` — verify `.DS_Store`, `Thumbs.db`, `*.tmp` are ignored
- `IsIgnored_UserPattern_OverridesDefault` — negation `!important.tmp` un-ignores a file matched by built-in
- `IsIgnored_GitignoreGlob_MatchesCorrectly` — verify `**/*.log`, `build/`, `node_modules/` patterns

**Request back from client agent:**
- Commit hash
- Build: 0 errors
- Test count (was 70, should increase by ≥ 3 new tests)
- Confirm `SyncIgnoreParser` is wired into `SyncEngine` for all three check points
- Confirm "Ignored Files" panel is present in SyncTray Settings
