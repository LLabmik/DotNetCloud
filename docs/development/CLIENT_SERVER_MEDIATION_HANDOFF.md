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

Open issue: Sync Improvement Batch 1 — Tasks 1.1 through 1.4 complete. Task 1.5 (per-chunk retry with exponential backoff) is next.

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
**Client-side status:** Pending implementation on `Windows11-TestDNC`.

Pull latest (`git pull`) before starting — server Tasks 1.8 and 1.9 were completed in the same push.

---

#### Task 1.5: Per-Chunk Retry with Exponential Backoff

**File:** `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkedTransferClient.cs`

Wrap each chunk download/upload in a retry loop. Only retry on `HttpRequestException` and 5xx status codes; do NOT retry on 4xx or 429.

Add a `ChunkTransferResult` record and log the final result per chunk:

```csharp
record ChunkTransferResult(string Hash, bool Success, int Attempts, string? Error);
```

```csharp
int maxRetries = 3;
for (int attempt = 1; attempt <= maxRetries; attempt++)
{
    try
    {
        await UploadChunkAsync(chunk, cancellationToken);
        break;
    }
    catch (HttpRequestException ex) when (attempt < maxRetries)
    {
        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1))
                    + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500));
        _logger.LogWarning(ex, "Chunk {Hash} attempt {Attempt} failed. Retrying in {Delay}ms.",
            chunk.Hash, attempt, delay.TotalMilliseconds);
        await Task.Delay(delay, cancellationToken);
    }
}
```

**Done when:** Each chunk retries independently with jittered backoff. A single chunk failure does not abort the full transfer.

---

#### Task 1.6: SQLite WAL Mode + Corruption Recovery

**File:** `LocalStateDb` / `LocalStateDbContext` in Client Core.

1. Add `Journal Mode=Wal` to the SQLite connection string.
2. On `InitializeAsync()` (or equivalent startup), run `PRAGMA integrity_check;`. If result ≠ `"ok"`, log error, rename the corrupt files with a timestamp suffix, recreate the DB with `EnsureCreatedAsync()`, set a `_needsFullResync = true` flag, and notify the user.
3. After each complete sync pass, run `PRAGMA wal_checkpoint(TRUNCATE);`.

**Done when:** `state.db-wal` exists during sync. Corrupt DB is detected on startup, preserved as `state.db.corrupt.<timestamp>`, and a fresh DB is created.

---

#### Task 1.7: Operation Retry Queue with Backoff

**Files:** `LocalStateDb` + `SyncEngine` in Client Core.

1. Add `NextRetryAt DateTime?` and `LastError string?` columns to `PendingOperationDbRow` (EF Core migration needed for client SQLite DB).
2. In `ExecutePendingOperationAsync()`, on failure set `RetryCount++`, compute `NextRetryAt` with the schedule: 1 min → 5 min → 15 min → 1 h → 6 h (repeating). After `RetryCount >= 10`, move to a `FailedOperationDbRow` table (same schema + `FailedAt DateTime`).
3. In `GetPendingOperationsAsync()`, add filter: `WHERE NextRetryAt IS NULL OR NextRetryAt <= @now`.
4. On success: reset `RetryCount = 0`, `NextRetryAt = null`, `LastError = null`.

**Done when:** Failed operations back off exponentially. After 10 failures they move to the failed operations table and stop retrying.

---

**Request back after all three tasks:**
- commit hash from `Windows11-TestDNC`
- build output (0 errors expected)
- brief confirmation that Task 1.5 retry loop is in place, Task 1.6 WAL file exists, Task 1.7 new columns added

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
