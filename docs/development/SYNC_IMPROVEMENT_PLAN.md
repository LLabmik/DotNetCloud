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

## Operational Notes (Read Before Implementing)

These are hard-won lessons from previous implementation sessions. Read them before starting any task.

1. **Repo paths are NOT the same on every machine.** Server (`mint22`) may have the repo at `~/dotnetcloud`. Client (`Windows11-TestDNC`) has it at `C:\Repos\dotnetcloud`. All file references in this document use paths relative to the repo root (e.g., `src/Clients/...`). Never hardcode absolute paths.

2. **Serilog rolling file date suffix.** When using `RollingInterval.Day`, Serilog appends `YYYYMMDD` to the log filename. A file configured as `sync-service.log` will actually write to `sync-service20260308.log`. When verifying logging output, always look for date-suffixed files, not the base name.

3. **Triggering a sync pass for testing.** The sync engine runs on a 5-minute timer (`FullScanInterval: 00:05:00`). To trigger an immediate sync pass without waiting, send a `sync-now` command to the IPC named pipe:
   - **Windows:** `echo '{"command":"sync-now"}' | & { $pipe = New-Object System.IO.Pipes.NamedPipeClientStream('.','dotnetcloud-sync','InOut'); $pipe.Connect(5000); $w = New-Object System.IO.StreamWriter($pipe); $w.WriteLine('{"command":"sync-now"}'); $w.Flush(); $r = New-Object System.IO.StreamReader($pipe); Write-Output $r.ReadLine(); $pipe.Close() }`
   - **Linux:** `echo '{"command":"sync-now"}' | socat - UNIX-CONNECT:/run/dotnetcloud/sync.sock`
   - Expected response: `{"success":true,"data":{"started":true}}`

4. **Check what already exists before creating anything.** Before implementing any task, read the files listed in the "Files to modify" table. Previous implementations have made the mistake of creating new middleware/config/services that duplicate infrastructure that already existed (e.g., rate limiting middleware was already fully implemented when Task 1.3 was assigned).

5. **Build and test from repo root.** Always build and test using repo-relative project paths:
   ```bash
   dotnet build src/Clients/DotNetCloud.Client.Core/DotNetCloud.Client.Core.csproj
   dotnet test tests/DotNetCloud.Client.Core.Tests/
   ```

6. **SyncService background process.** When running the sync service for testing:
   ```bash
   dotnet run --project src/Clients/DotNetCloud.Client.SyncService/DotNetCloud.Client.SyncService.csproj
   ```
   Stop with Ctrl+C. Check logs at `%APPDATA%\DotNetCloud\logs\` (Windows) or `~/.local/share/DotNetCloud/logs/` (Linux). Remember the date-suffix naming (Note 2 above).

7. **HttpClient registration.** Two HttpClient registrations exist:
   - **Typed client** for `DotNetCloudApiClient` — registered in `src/Clients/DotNetCloud.Client.Core/ClientCoreServiceExtensions.cs`
   - **Named client** `"DotNetCloudSync"` — registered in `src/Clients/DotNetCloud.Client.SyncService/SyncServiceExtensions.cs`
   - Any new `DelegatingHandler` (like `CorrelationIdHandler`) must be added to BOTH registrations.

## Rules

1. **Server changes** → implemented directly on `mint22`
2. **Client changes** → implemented on `Windows11-TestDNC`, described in handoff doc
3. **Both-side changes** → server first (provides the contract), then client
4. **Linux readiness** → all client code must account for Linux differences; platform-specific code behind `RuntimeInformation` / `OperatingSystem` checks
5. **macOS awareness** → design interfaces and abstractions so a future macOS contributor can plug in without restructuring (no Windows-only assumptions baked into core logic)
6. **All file types sync** — this is a backup system; never block file extensions. Security is enforced via server-side execution prevention and scanning, not upload rejection.
7. **All file paths in this document are relative to the repo root.** The repo lives at different absolute paths on the server (`~/dotnetcloud`) and client (`C:\Repos\dotnetcloud`). Never use absolute paths in task descriptions. Always write paths like `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkedTransferClient.cs`.
8. **Check what already exists before creating new code.** Before implementing any task, read the files listed in the "Files to modify" section to understand current state. Do NOT create new middleware, configuration classes, or services that duplicate existing infrastructure.
9. **Build/test commands** — always run from the repo root:
   - Build: `dotnet build <project-relative-path>.csproj`
   - Test: `dotnet test <test-project-relative-path>/`
   - Full build: `dotnet build`
   - Full test: `dotnet test`
10. **Serilog rolling files** — Serilog with `RollingInterval.Day` appends a date suffix to log filenames. A file configured as `sync-service.log` will actually write to `sync-service20260308.log` (YYYYMMDD). Always check for date-suffixed files when verifying logging.
11. **IPC "sync-now" command** — To trigger an immediate sync pass for testing (instead of waiting for the 5-minute timer), pipe a JSON command to the IPC named pipe:
    - Windows: `echo '{"command":"sync-now"}' | & { $pipe = New-Object System.IO.Pipes.NamedPipeClientStream('.','dotnetcloud-sync','InOut'); $pipe.Connect(5000); $w = New-Object System.IO.StreamWriter($pipe); $w.WriteLine('{"command":"sync-now"}'); $w.Flush(); $r = New-Object System.IO.StreamReader($pipe); Write-Output $r.ReadLine(); $pipe.Close() }`
    - Linux: `echo '{"command":"sync-now"}' | socat - UNIX-CONNECT:/run/dotnetcloud/sync.sock`

---

## Component File Reference (Repo-Relative Paths)

Use these paths whenever a task references a component by name. All paths are relative to the repository root.

### Client — Core Library (`src/Clients/DotNetCloud.Client.Core/`)

| Component | File Path |
|-----------|-----------|
| **ChunkedTransferClient** | `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkedTransferClient.cs` |
| **IChunkedTransferClient** | `src/Clients/DotNetCloud.Client.Core/Transfer/IChunkedTransferClient.cs` |
| **TransferProgress** | `src/Clients/DotNetCloud.Client.Core/Transfer/TransferProgress.cs` |
| **SyncEngine** | `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs` |
| **ISyncEngine** | `src/Clients/DotNetCloud.Client.Core/Sync/ISyncEngine.cs` |
| **SyncState enum** | `src/Clients/DotNetCloud.Client.Core/Sync/SyncState.cs` |
| **SyncContext** | `src/Clients/DotNetCloud.Client.Core/Sync/SyncContext.cs` |
| **SyncStatus** | `src/Clients/DotNetCloud.Client.Core/Sync/SyncStatus.cs` |
| **DotNetCloudApiClient** | `src/Clients/DotNetCloud.Client.Core/Api/DotNetCloudApiClient.cs` |
| **IDotNetCloudApiClient** | `src/Clients/DotNetCloud.Client.Core/Api/IDotNetCloudApiClient.cs` |
| **CorrelationIdHandler** | `src/Clients/DotNetCloud.Client.Core/Api/CorrelationIdHandler.cs` |
| **LocalStateDbContext** | `src/Clients/DotNetCloud.Client.Core/LocalState/LocalStateDbContext.cs` |
| **PendingOperationDbRow** | `src/Clients/DotNetCloud.Client.Core/LocalState/LocalStateDbContext.cs` (line ~49) |
| **PendingOperationRecord** | `src/Clients/DotNetCloud.Client.Core/LocalState/Entities/PendingOperationRecord.cs` |
| **LocalFileRecord** | `src/Clients/DotNetCloud.Client.Core/LocalState/Entities/LocalFileRecord.cs` |
| **ConflictResolver** | `src/Clients/DotNetCloud.Client.Core/Conflict/ConflictResolver.cs` |
| **IConflictResolver** | `src/Clients/DotNetCloud.Client.Core/Conflict/IConflictResolver.cs` |
| **ConflictInfo** | `src/Clients/DotNetCloud.Client.Core/Conflict/ConflictInfo.cs` |
| **SelectiveSyncConfig** | `src/Clients/DotNetCloud.Client.Core/SelectiveSync/SelectiveSyncConfig.cs` |
| **ClientCoreServiceExtensions** | `src/Clients/DotNetCloud.Client.Core/ClientCoreServiceExtensions.cs` |
| **Client.Core .csproj** | `src/Clients/DotNetCloud.Client.Core/DotNetCloud.Client.Core.csproj` |

### Client — Sync Service (`src/Clients/DotNetCloud.Client.SyncService/`)

| Component | File Path |
|-----------|-----------|
| **SyncContextManager** | `src/Clients/DotNetCloud.Client.SyncService/ContextManager/SyncContextManager.cs` |
| **ISyncContextManager** | `src/Clients/DotNetCloud.Client.SyncService/ContextManager/ISyncContextManager.cs` |
| **SyncContextRegistration** | `src/Clients/DotNetCloud.Client.SyncService/ContextManager/SyncContextRegistration.cs` |
| **SyncServiceExtensions** | `src/Clients/DotNetCloud.Client.SyncService/SyncServiceExtensions.cs` |
| **SyncWorker** | `src/Clients/DotNetCloud.Client.SyncService/SyncWorker.cs` |
| **Program.cs** | `src/Clients/DotNetCloud.Client.SyncService/Program.cs` |
| **sync-settings.json** | `src/Clients/DotNetCloud.Client.SyncService/sync-settings.json` |
| **IpcServer** | `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcServer.cs` |
| **IpcProtocol** | `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcProtocol.cs` |
| **IpcClientHandler** | `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcClientHandler.cs` |
| **SyncService .csproj** | `src/Clients/DotNetCloud.Client.SyncService/DotNetCloud.Client.SyncService.csproj` |

### Client — SyncTray (`src/Clients/DotNetCloud.Client.SyncTray/`)

| Component | File Path |
|-----------|-----------|
| **SyncTray .csproj** | `src/Clients/DotNetCloud.Client.SyncTray/DotNetCloud.Client.SyncTray.csproj` |

### Server — Core Server (`src/Core/DotNetCloud.Core.Server/`)

| Component | File Path |
|-----------|-----------|
| **RateLimitingConfiguration** | `src/Core/DotNetCloud.Core.Server/Configuration/RateLimitingConfiguration.cs` |
| **Server appsettings.json** | `src/Core/DotNetCloud.Core.Server/appsettings.json` |
| **Server Program.cs** | `src/Core/DotNetCloud.Core.Server/Program.cs` |

### Server — Files Module (`src/Modules/Files/`)

| Component | File Path |
|-----------|-----------|
| **ContentHasher** | `src/Modules/Files/DotNetCloud.Modules.Files/Services/ContentHasher.cs` |
| **SyncController** | `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/SyncController.cs` |
| **FilesController** | `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/FilesController.cs` |

### Tests

| Component | File Path |
|-----------|-----------|
| **Client.Core Tests** | `tests/DotNetCloud.Client.Core.Tests/` |
| **SyncService Tests** | `tests/DotNetCloud.Client.SyncService.Tests/` |
| **Server Tests** | `tests/DotNetCloud.Core.Server.Tests/` |
| **Files Module Tests** | `tests/DotNetCloud.Modules.Files.Tests/` |

---

## Batch 1 — Foundation: Reliability + Security Baseline

**Goal:** Make what we have today robust and debuggable before adding new capabilities.

### 1.1 — Sync Service Logging (Client + Server) ✅ COMPLETE

**Status:** ✅ Both sides complete. Client validated at commit `c69aeac`. Server audit logging at commit `c585dae`.

**Deliverables:**
- ✓ Server: Serilog structured audit logging in sync/file service classes
- ✓ Server: Dedicated `audit-sync.log` rolling file sink
- ✓ Client: Serilog integration in SyncService with rolling file sink
- ✓ Client: Structured log events for all sync lifecycle operations
- ✓ Client: `sync-settings.json` logging configuration section
- ✓ Client: Log rotation with configurable retention (default 30 days)
- ✓ Client: Platform-appropriate log directory and file permissions

**Files modified:**
- Client: `src/Clients/DotNetCloud.Client.SyncService/Program.cs` — Serilog setup with `LoadLoggingSettings()` and `BuildLogPath()`
- Client: `src/Clients/DotNetCloud.Client.SyncService/sync-settings.json` — logging config section
- Client: `src/Clients/DotNetCloud.Client.SyncService/DotNetCloud.Client.SyncService.csproj` — Serilog NuGet packages

**Operational note:** Serilog `RollingInterval.Day` appends YYYYMMDD to log filenames. A file configured as `sync-service.log` will actually write to `sync-service20260308.log`. Always look for date-suffixed files when verifying logging output.

---

### 1.2 — Request Correlation IDs ✅ COMPLETE

**Status:** ✅ Both sides complete. Server middleware at commit `16dd7df`. Client handler at commit `97afdd8`.

**Deliverables:**
- ✓ Server: `RequestCorrelationMiddleware` reads/generates `X-Request-ID`, pushes to Serilog context, returns in response
- ✓ Client: `CorrelationIdHandler` (`DelegatingHandler`) generates and sends `X-Request-ID` on every request
- ✓ Client: Request ID logged with every API operation

**Files modified:**
- Client: `src/Clients/DotNetCloud.Client.Core/Api/CorrelationIdHandler.cs` — created (DelegatingHandler)
- Client: `src/Clients/DotNetCloud.Client.Core/ClientCoreServiceExtensions.cs` — registered handler on typed HttpClient
- Client: `src/Clients/DotNetCloud.Client.SyncService/SyncServiceExtensions.cs` — registered handler on named `"DotNetCloudSync"` HttpClient

---

### 1.3 — Server-Side Rate Limiting on Sync Endpoints ✅ COMPLETE

**Status:** ✅ Both sides complete. Server at commit `4570c16`. Client required no changes.

**⚠️ IMPORTANT: Rate limiting infrastructure ALREADY EXISTS. Do NOT create new middleware or configuration classes.**

**What already exists (DO NOT recreate):**
- `src/Core/DotNetCloud.Core.Server/Configuration/RateLimitingConfiguration.cs` — has `AddDotNetCloudRateLimiting()` extension method with sliding-window policies (`"global"`, `"authenticated"`, per-module `"module-{name}"`), and a 429 rejection handler that returns `Retry-After` header.
- `src/Core/DotNetCloud.Core.Server/appsettings.json` — already has a `"RateLimiting"` section with `Enabled`, `GlobalPermitLimit`, `AuthenticatedPermitLimit`, `ModuleLimits`, etc.
- `src/Core/DotNetCloud.Core.Server/Program.cs` — already calls `app.UseDotNetCloudRateLimiting()` in the pipeline.

**Files to modify:**

| File (repo-relative) | What to do |
|---|---|
| `src/Core/DotNetCloud.Core.Server/appsettings.json` | Add sync-specific entries under `RateLimiting.ModuleLimits` |
| `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/SyncController.cs` | Add `[EnableRateLimiting]` attributes to sync methods |
| `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/FilesController.cs` | Add `[EnableRateLimiting]` attributes to upload/download methods |

**Step-by-step implementation:**

**Step 1:** Add sync-specific module limits to `src/Core/DotNetCloud.Core.Server/appsettings.json` under the existing `RateLimiting.ModuleLimits` key:

```json
"ModuleLimits": {
  "sync-changes":    { "PermitLimit": 60,  "WindowSeconds": 60 },
  "sync-tree":       { "PermitLimit": 10,  "WindowSeconds": 60 },
  "sync-reconcile":  { "PermitLimit": 30,  "WindowSeconds": 60 },
  "upload-initiate": { "PermitLimit": 30,  "WindowSeconds": 60 },
  "upload-chunks":   { "PermitLimit": 300, "WindowSeconds": 60 },
  "download":        { "PermitLimit": 120, "WindowSeconds": 60 },
  "chunks":          { "PermitLimit": 300, "WindowSeconds": 60 }
}
```

**Step 2:** Add `[EnableRateLimiting("module-{name}")]` attributes to controller methods. Add `using Microsoft.AspNetCore.RateLimiting;` to both controllers if not already present.

In `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/SyncController.cs`:
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

In `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/FilesController.cs`:
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
dotnet build src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj
dotnet build src/Modules/Files/DotNetCloud.Modules.Files.Host/DotNetCloud.Modules.Files.Host.csproj
dotnet test tests/DotNetCloud.Core.Server.Tests/
dotnet test tests/DotNetCloud.Modules.Files.Tests/
```

**Client-side verification (no code changes needed):**
- Client already handles 429 + `Retry-After` in `src/Clients/DotNetCloud.Client.Core/Api/DotNetCloudApiClient.cs` inside `SendWithRetryAsync()`. Verify this by reading the method — it should check for `HttpStatusCode.TooManyRequests` and read the `Retry-After` header.
- Optionally add a log line when rate-limited, but this is low priority.

**Deliverables:**
- ✓ Server: Sync-specific module limits added to `appsettings.json` under `RateLimiting.ModuleLimits`
- ✓ Server: `[EnableRateLimiting]` attributes on sync controller methods in `SyncController.cs`
- ✓ Server: `[EnableRateLimiting]` attributes on file controller methods in `FilesController.cs`
- ✓ Client: 429 + `Retry-After` handling already present in `DotNetCloudApiClient.SendWithRetryAsync()` — no changes needed

**Side:** Server (+ client verification only)
**Complexity:** Low

---

### 1.4 — Chunk Integrity Verification on Download ✅ COMPLETE

**Status:** ✅ Client complete. Commit on `Windows11-TestDNC` (2026-03-08). 55 tests pass.

**Files to modify:**

| File (repo-relative) | What to do |
|---|---|
| `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkedTransferClient.cs` | Add SHA-256 verification after each chunk download in `DownloadChunksAsync()` / `DownloadAsync()` |

**What already exists:**
- `ChunkedTransferClient.DownloadAsync()` downloads a chunk manifest (list of hashes) then downloads each chunk by hash via `DotNetCloudApiClient.DownloadChunkByHashAsync()`.
- The hash of each chunk IS known from the manifest — it's the `chunkHash` string used to request the download.
- `System.Security.Cryptography.SHA256` is available in the runtime (no NuGet needed).
- There is a `ContentHasher` on the server at `src/Modules/Files/DotNetCloud.Modules.Files/Services/ContentHasher.cs` with `ComputeHash(ReadOnlySpan<byte>)` — but this is server-side only. The client needs its own inline SHA-256 computation (it's a one-liner: `Convert.ToHexStringLower(SHA256.HashData(bytes))`).

**Step-by-step implementation:**

**Step 1:** In `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkedTransferClient.cs`, find the method that downloads chunks (likely `DownloadChunksAsync` or inside `DownloadAsync`). After receiving each chunk's byte array from the API:

```csharp
// After downloading chunk bytes:
var actualHash = Convert.ToHexStringLower(SHA256.HashData(chunkBytes));
if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
{
    _logger.LogWarning("Chunk hash mismatch for {ExpectedHash}: got {ActualHash}. Retrying ({Attempt}/3)...",
        expectedHash, actualHash, attempt);
    // Retry up to 3 times
    continue;
}
```

**Step 2:** Wrap the download + verify in a retry loop (max 3 attempts). If all 3 fail, throw an exception with the request ID (from `CorrelationIdHandler`) included in the log:

```csharp
_logger.LogError("Chunk {ExpectedHash} failed integrity verification after 3 attempts. Aborting file download.", expectedHash);
throw new ChunkIntegrityException($"Chunk {expectedHash} corrupted after 3 download attempts.");
```

**Step 3:** Build and test:
```bash
dotnet build src/Clients/DotNetCloud.Client.Core/DotNetCloud.Client.Core.csproj
dotnet test tests/DotNetCloud.Client.Core.Tests/
```

**Deliverables:**
- ✓ Client: Created `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkIntegrityException.cs`
- ✓ Client: Post-download SHA-256 verification for every chunk in `ChunkedTransferClient.DownloadChunksAsync()`
- ✓ Client: Retry on hash mismatch (up to 3 attempts) with `LogWarning`
- ✓ Client: `LogError` + `ChunkIntegrityException` thrown after all 3 attempts fail
- ✓ Tests: 3 new/updated transfer tests pass

**Side:** Client only
**Complexity:** Low

---

### 1.5 — Per-Chunk Retry with Exponential Backoff ✅ COMPLETE

**Status:** ✅ Client complete. Commit `1aa6b18` (2026-03-08). 64 tests pass.

**Approved Proposal:** 2.2

**Problem:** A single chunk failure aborts the entire file transfer.

**Files to modify:**

| File (repo-relative) | What to do |
|---|---|
| `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkedTransferClient.cs` | Add per-chunk retry loop with exponential backoff in upload and download methods |
| `src/Clients/DotNetCloud.Client.Core/Transfer/TransferProgress.cs` | Add `ChunksSkipped` already exists — verify it's used |

**What already exists:**
- `ChunkedTransferClient` at `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkedTransferClient.cs` has `UploadAsync()` and `DownloadAsync()` methods that process chunks sequentially.
- `DotNetCloudApiClient` at `src/Clients/DotNetCloud.Client.Core/Api/DotNetCloudApiClient.cs` already has retry logic in `SendWithRetryAsync()` (max 3 retries, 500ms base delay). The per-chunk retry in this task is SEPARATE from the API-level retry — this is a higher-level retry that wraps the entire chunk operation (hash + download/upload + verify).
- `TransferProgress` at `src/Clients/DotNetCloud.Client.Core/Transfer/TransferProgress.cs` already has `ChunksSkipped` (int).

**Step-by-step implementation:**

**Step 1:** In `ChunkedTransferClient.cs`, wrap each chunk upload/download call in a retry loop:

```csharp
private async Task<byte[]> DownloadChunkWithRetryAsync(
    string chunkHash, int chunkIndex, int totalChunks, CancellationToken ct)
{
    const int maxRetries = 3;
    var baseDelay = TimeSpan.FromSeconds(1);

    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            var bytes = await _apiClient.DownloadChunkByHashAsync(chunkHash, ct);
            // Integrity check (from Task 1.4)
            var actualHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            if (!string.Equals(actualHash, chunkHash, StringComparison.OrdinalIgnoreCase))
                throw new ChunkIntegrityException($"Hash mismatch: expected {chunkHash}, got {actualHash}");
            return bytes;
        }
        catch (Exception ex) when (attempt < maxRetries && ShouldRetryChunk(ex))
        {
            var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500));
            var delay = baseDelay * Math.Pow(2, attempt - 1) + jitter;
            _logger.LogWarning(ex, "Chunk {ChunkIndex}/{TotalChunks} ({Hash}) failed on attempt {Attempt}/{MaxRetries}. Retrying in {Delay}ms...",
                chunkIndex, totalChunks, chunkHash, attempt, maxRetries, delay.TotalMilliseconds);
            await Task.Delay(delay, ct);
        }
    }
    throw new ChunkTransferException($"Chunk {chunkHash} failed after {maxRetries} attempts");
}

private static bool ShouldRetryChunk(Exception ex) => ex switch
{
    HttpRequestException => true,           // Network errors
    TaskCanceledException => false,         // User cancellation — don't retry
    ChunkIntegrityException => true,        // Hash mismatch — retry download
    _ when ex.Message.Contains("5") => true, // 5xx server errors
    _ => false
};
```

**Step 2:** Do NOT retry on 4xx responses (client error) or 429 (handled separately by `SendWithRetryAsync` in `DotNetCloudApiClient`).

**Step 3:** For upload failures after max retries: leave the upload session open (it can be resumed on the next sync pass because `InitiateUpload` dedup will skip already-uploaded chunks). Log the partial state.

**Step 4:** Build and test:
```bash
dotnet build src/Clients/DotNetCloud.Client.Core/DotNetCloud.Client.Core.csproj
dotnet test tests/DotNetCloud.Client.Core.Tests/
```

**Deliverables:**
- ✓ Client: Per-chunk retry loop with exponential backoff + jitter in `ChunkedTransferClient`
- ✓ Client: `ShouldRetryChunk()` logic (retry network/5xx/hash-mismatch, NOT 4xx/429/cancellation)
- ✓ Client: Detailed logging per chunk (hash, attempt number, delay, error)

**Side:** Client only
**Complexity:** Low-Medium

---

### 1.6 — SQLite WAL Mode + Corruption Recovery ✅ COMPLETE

**Status:** ✅ Client complete. Commit `1aa6b18` (2026-03-08). 64 tests pass.

**Approved Proposal:** 2.4

**Problem:** Client SQLite uses slow DELETE journal mode with no corruption detection.

**Files to modify:**

| File (repo-relative) | What to do |
|---|---|
| `src/Clients/DotNetCloud.Client.Core/LocalState/LocalStateDbContext.cs` | Add `Journal Mode=Wal` to connection string; add `InitializeAsync()` integrity check |
| `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs` | Run WAL checkpoint after each complete sync pass |

**What already exists:**
- `LocalStateDbContext` at `src/Clients/DotNetCloud.Client.Core/LocalState/LocalStateDbContext.cs` uses `UseSqlite($"Data Source={dbPath}")` for the connection string. The `dbPath` comes from the sync context data directory.
- The DbContext has `DbSet<LocalFileRecord>`, `DbSet<PendingOperationDbRow>`, and `DbSet<SyncCheckpointRow>`.
- `SyncEngine` at `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs` has `SyncAsync()` which is the main sync pass method. WAL checkpoint should run at the end of a successful `SyncAsync()`.

**Step-by-step implementation:**

**Step 1:** In `LocalStateDbContext.cs`, change the SQLite connection string to enable WAL mode:
```csharp
// Change from:
optionsBuilder.UseSqlite($"Data Source={dbPath}");
// To:
optionsBuilder.UseSqlite($"Data Source={dbPath};Journal Mode=Wal");
```

**Step 2:** Add an `InitializeAsync()` method to `LocalStateDbContext` (or a wrapper service) that runs on startup:
```csharp
public async Task<bool> InitializeAsync(CancellationToken ct = default)
{
    var connection = Database.GetDbConnection();
    await connection.OpenAsync(ct);

    await using var cmd = connection.CreateCommand();
    cmd.CommandText = "PRAGMA integrity_check";
    var result = await cmd.ExecuteScalarAsync(ct);

    if (result?.ToString() != "ok")
    {
        _logger.LogError("SQLite database corruption detected: {Details}", result);
        await connection.CloseAsync();

        // Preserve corrupt DB for post-mortem
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var dbPath = connection.DataSource;
        File.Move(dbPath, $"{dbPath}.corrupt.{timestamp}");
        if (File.Exists($"{dbPath}-wal"))
            File.Move($"{dbPath}-wal", $"{dbPath}-wal.corrupt.{timestamp}");
        if (File.Exists($"{dbPath}-shm"))
            File.Move($"{dbPath}-shm", $"{dbPath}-shm.corrupt.{timestamp}");

        // Recreate
        await Database.EnsureCreatedAsync(ct);
        return false; // Signals that a full re-sync is needed
    }
    return true;
}
```

**Step 3:** In `SyncEngine.cs`, after a successful sync pass in `SyncAsync()`, run WAL checkpoint:
```csharp
// After sync completes successfully:
await using var cmd = _dbContext.Database.GetDbConnection().CreateCommand();
cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
await cmd.ExecuteNonQueryAsync(ct);
```

**Step 4:** Build and test:
```bash
dotnet build src/Clients/DotNetCloud.Client.Core/DotNetCloud.Client.Core.csproj
dotnet test tests/DotNetCloud.Client.Core.Tests/
```

**Deliverables:**
- ✓ Client: WAL journal mode via `PRAGMA journal_mode=WAL` in `RunSchemaEvolutionAsync`
- ✓ Client: Startup integrity check with automatic recovery in `InitializeAsync()`
- ✓ Client: Corrupt DB preservation (renamed with timestamp, not deleted), for post-mortem
- ✓ Client: Post-sync WAL checkpoint via `CheckpointWalAsync()` in `SyncEngine`
- ✓ Client: `WasRecentlyReset()` flag for tray notification

**Side:** Client only
**Complexity:** Low

---

### 1.7 — Operation Retry Queue with Backoff ✅ COMPLETE

**Status:** ✅ Client complete. Commit `1aa6b18` (2026-03-08). 64 tests pass.

**Approved Proposal:** 2.6

**Problem:** `RetryCount` field exists but is never used. Failed operations retry every 5 minutes indefinitely without backoff.

**Files to modify:**

| File (repo-relative) | What to do |
|---|---|
| `src/Clients/DotNetCloud.Client.Core/LocalState/LocalStateDbContext.cs` | Add `NextRetryAt` and `LastError` columns to `PendingOperationDbRow`; add `FailedOperationDbRow` entity + DbSet |
| `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs` | Update `ExecutePendingOperationAsync()` to implement backoff schedule; update query in pending operations retrieval to filter by `NextRetryAt` |

**What already exists:**
- `PendingOperationDbRow` (defined at ~line 49 in `src/Clients/DotNetCloud.Client.Core/LocalState/LocalStateDbContext.cs`) has:
  - `Id` (int), `OperationType` (string), `LocalPath` (string?), `NodeId` (Guid?), `QueuedAt` (DateTime), `RetryCount` (int)
  - The `RetryCount` field EXISTS but it is never incremented or checked anywhere.
- `SyncEngine.ExecutePendingOperationAsync()` at `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs` processes pending operations but does not implement any backoff logic.
- The sync pass runs every 5 minutes via `RunPeriodicScanAsync()`, so without backoff, every failed operation retries every 5 minutes forever.

**Step-by-step implementation:**

**Step 1:** Add new columns to `PendingOperationDbRow` in `LocalStateDbContext.cs`:
```csharp
public class PendingOperationDbRow
{
    // ... existing properties ...
    public DateTime? NextRetryAt { get; set; }
    public string? LastError { get; set; }
}
```

**Step 2:** Add `FailedOperationDbRow` entity for permanently failed operations:
```csharp
public class FailedOperationDbRow
{
    public int Id { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public string? LocalPath { get; set; }
    public Guid? NodeId { get; set; }
    public DateTime QueuedAt { get; set; }
    public DateTime FailedAt { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
}
```
Add `DbSet<FailedOperationDbRow> FailedOperations { get; set; }` to `LocalStateDbContext`.

**Step 3:** In `SyncEngine.ExecutePendingOperationAsync()`, implement backoff on failure:
```csharp
// On failure:
operation.RetryCount++;
operation.LastError = ex.Message;
operation.NextRetryAt = operation.RetryCount switch
{
    1 => DateTime.UtcNow.AddMinutes(1),
    2 => DateTime.UtcNow.AddMinutes(5),
    3 => DateTime.UtcNow.AddMinutes(15),
    4 => DateTime.UtcNow.AddHours(1),
    >= 5 and < 10 => DateTime.UtcNow.AddHours(6),
    _ => null // Will be moved to FailedOperations
};

if (operation.RetryCount >= 10)
{
    // Move to FailedOperations table
    _dbContext.FailedOperations.Add(new FailedOperationDbRow { /* copy fields */ });
    _dbContext.PendingOperations.Remove(operation);
    _logger.LogError("Operation {OperationType} for {LocalPath} permanently failed after {RetryCount} attempts: {Error}",
        operation.OperationType, operation.LocalPath, operation.RetryCount, operation.LastError);
}
await _dbContext.SaveChangesAsync(ct);
```

**Step 4:** In the method that retrieves pending operations (look for LINQ query on `PendingOperations`), add a filter:
```csharp
.Where(op => op.NextRetryAt == null || op.NextRetryAt <= DateTime.UtcNow)
```

**Step 5:** On success, clear retry state:
```csharp
operation.RetryCount = 0;
operation.NextRetryAt = null;
operation.LastError = null;
```

**Step 6:** Build and test:
```bash
dotnet build src/Clients/DotNetCloud.Client.Core/DotNetCloud.Client.Core.csproj
dotnet test tests/DotNetCloud.Client.Core.Tests/
```

**Note:** Since this changes the SQLite schema, existing `state.db` files will need migration. EF Core `EnsureCreatedAsync()` handles this for fresh databases. For existing databases, either add an EF migration or use `ALTER TABLE` SQL in the initialization code.

**Deliverables:**
- ✓ Client: `NextRetryAt` and `LastError` columns on `PendingOperationDbRow` in `LocalStateDbContext.cs`
- ✓ Client: `FailedOperationDbRow` entity + DbSet in `LocalStateDbContext.cs`
- ✓ Client: Exponential backoff schedule in `SyncEngine.ApplyLocalChangesAsync()`
- ✓ Client: Filter pending operations by `NextRetryAt` eligibility in query
- ✓ Client: Logging of retry attempts and final failures

**Side:** Client only
**Complexity:** Low

---

### 1.8 — Secure Temp File Handling ✅ COMPLETE

**Status:** ✅ Server complete. Commit `82ca53b` (2026-03-08).

**Files to modify:**

| File (repo-relative) | What to do |
|---|---|
| Server: `src/Modules/Files/DotNetCloud.Modules.Files/Services/DownloadService.cs` (or equivalent) | Change temp directory from `Path.GetTempPath()` to `{DOTNETCLOUD_DATA_DIR}/tmp/` |
| Server: A new `TempFileCleanupService.cs` (suggest `src/Modules/Files/DotNetCloud.Modules.Files/Services/TempFileCleanupService.cs`) | `IHostedService` that deletes stale temp files on startup |

**What already exists:**
- The server uses `Path.GetTempPath()` for file reconstruction during downloads. Find the exact location by searching for `GetTempPath` in the `src/Modules/Files/` directory.
- `FileOptions.DeleteOnClose` is already used as the primary cleanup mechanism.
- `DOTNETCLOUD_DATA_DIR` environment variable is the root for all server data storage.

**Step-by-step implementation:**

**Step 1:** Create a helper method to get the app-specific temp directory:
```csharp
private static string GetAppTempDirectory()
{
    var dataDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_DATA_DIR")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DotNetCloud");
    var tempDir = Path.Combine(dataDir, "tmp");
    if (!Directory.Exists(tempDir))
    {
        Directory.CreateDirectory(tempDir);
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            File.SetUnixFileMode(tempDir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); // 700
    }
    return tempDir;
}
```

**Step 2:** Replace all `Path.GetTempPath()` calls in DownloadService with `GetAppTempDirectory()`.

**Step 3:** Create `TempFileCleanupService` as an `IHostedService`:
```csharp
public class TempFileCleanupService : IHostedService
{
    public Task StartAsync(CancellationToken ct)
    {
        var tempDir = GetAppTempDirectory();
        foreach (var file in Directory.EnumerateFiles(tempDir))
        {
            if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddHours(-1))
            {
                try { File.Delete(file); }
                catch (IOException) { /* in use, skip */ }
            }
        }
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

**Step 4:** Register in DI (`builder.Services.AddHostedService<TempFileCleanupService>()`).

**Step 5:** Build and test:
```bash
dotnet build src/Modules/Files/DotNetCloud.Modules.Files/DotNetCloud.Modules.Files.csproj
dotnet test tests/DotNetCloud.Modules.Files.Tests/
```

**Deliverables:**
- ✓ Server: Dedicated temp directory under `DOTNETCLOUD_DATA_DIR/tmp/`
- ✓ Server: Restrictive permissions (`chmod 700`) on temp directory creation (Linux)
- ✓ Server: `DownloadService` uses app-specific temp dir instead of `Path.GetTempPath()`
- ✓ Server: `TempFileCleanupService` (`IHostedService`) deletes files older than 1 hour on startup

**Side:** Server only
**Complexity:** Low

---

### 1.9 — Server-Side File Scanning (Replaces Extension Blocklist) ✅ COMPLETE

**Status:** ✅ Server complete. Commit `82ca53b` (2026-03-08).

**Approved Proposal:** 3.5 (modified per user feedback — no extension blocking)

**Problem (revised):** This is a backup system — ALL file types must sync, including `.exe`, `.sh`, `.bat`, etc. But the server must never execute uploaded content, and ideally should detect obviously malicious uploads.

**Files to modify:**

| File (repo-relative) | What to do |
|---|---|
| Server: Find the storage engine file (search for `WriteChunkAsync` in `src/Modules/Files/`) | Enforce no-execute permissions on written chunk files |
| Server: Find the download endpoint (search for `DownloadAsync` or `DownloadChunkAsync` in `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/`) | Add `X-Content-Type-Options: nosniff` and `Content-Disposition: attachment` headers |
| Server: Create new `src/Modules/Files/DotNetCloud.Modules.Files/Services/IFileScanner.cs` | Scanner interface for future ClamAV integration |
| Server: Create new `src/Modules/Files/DotNetCloud.Modules.Files/Services/NoOpFileScanner.cs` | Default pass-through implementation |
| Server: Find the `FileVersion` model (search in `src/Core/DotNetCloud.Core/` or `src/Modules/Files/`) | Add nullable `ScanStatus` enum field |
| Server: `src/Core/DotNetCloud.Core.Server/appsettings.json` | Add `FileUpload:MaxFileSizeBytes` config |
| Server: Find `InitiateUploadAsync` in the upload service | Add max file size check before accepting chunks |

**What already exists:**
- Chunk storage paths are content-addressed hashes (not user-supplied filenames) — verify this in the `LocalFileStorageEngine` or equivalent storage engine class. If confirmed, document it.
- Security headers middleware in `src/Core/DotNetCloud.Core.ServiceDefaults/` may already add `X-Content-Type-Options: nosniff` globally — check before adding per-endpoint.

**Step-by-step implementation:**

**Step 1: Execution prevention (mandatory)**
- In the storage engine (`WriteChunkAsync`), after writing a chunk file on Linux:
```csharp
if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
    File.SetUnixFileMode(chunkPath, UnixFileMode.UserRead | UnixFileMode.UserWrite); // 600, no execute
```

**Step 2: Response headers**
- On download endpoints, add response headers:
```csharp
Response.Headers["X-Content-Type-Options"] = "nosniff";
Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";
```
- Check `src/Core/DotNetCloud.Core.ServiceDefaults/` first — if `nosniff` is already set globally, skip the per-endpoint header.

**Step 3: Scanner interface**
```csharp
// IFileScanner.cs
public interface IFileScanner
{
    Task<ScanResult> ScanAsync(Stream content, string fileName, CancellationToken ct);
}

public record ScanResult(bool IsClean, string? ThreatName = null, string ScannerName = "NoOp");

// NoOpFileScanner.cs
public class NoOpFileScanner : IFileScanner
{
    public Task<ScanResult> ScanAsync(Stream content, string fileName, CancellationToken ct)
        => Task.FromResult(new ScanResult(IsClean: true));
}
```
Register `NoOpFileScanner` as the default `IFileScanner` in DI.

**Step 4: ScanStatus enum on FileVersion** (nullable, for future use):
```csharp
public enum ScanStatus { NotScanned, Clean, Threat, Error }
// Add to FileVersion model: public ScanStatus? ScanStatus { get; set; }
```

**Step 5: Max file size**
- In `appsettings.json`: `"FileUpload": { "MaxFileSizeBytes": 16106127360 }` (15 GB default)
- In `InitiateUploadAsync()`: check total file size before accepting, return `413 Payload Too Large` if exceeded.

**Step 6:** Build and test:
```bash
dotnet build src/Modules/Files/DotNetCloud.Modules.Files/DotNetCloud.Modules.Files.csproj
dotnet build src/Modules/Files/DotNetCloud.Modules.Files.Host/DotNetCloud.Modules.Files.Host.csproj
dotnet test tests/DotNetCloud.Modules.Files.Tests/
```

**Deliverables:**
- ✓ Server: Chunk storage file permissions enforced (no execute bits) in storage engine
- ✓ Server: Content-addressed storage paths confirmed (chunks stored by SHA-256 hash, not user filename)
- ✓ Server: `X-Content-Type-Options: nosniff` on download endpoints
- ✓ Server: `IFileScanner` interface + `NoOpFileScanner` default implementation
- ✓ Server: `ScanStatus` field on `FileVersion` model (nullable)
- ✓ Server: Configurable max file size in `appsettings.json` with pre-upload rejection

**Side:** Server only
**Complexity:** Medium

---

## Batch 2 — Efficiency: Bandwidth Savings

**Goal:** Reduce bandwidth consumption significantly for common workflows (editing existing files, re-syncing after partial failure).

### 2.1 — Content-Defined Chunking (CDC)

**Approved Proposal:** 1.1

**Status:** Server ✅ COMPLETE — commit `3a7e0ae` (2026-03-08). Client 🔲 PENDING.

**Problem:** Fixed 4MB chunks mean a 1-byte edit can force re-upload of all subsequent chunks.

**Files to modify:**

| File (repo-relative) | What to do |
|---|---|
| `src/Modules/Files/DotNetCloud.Modules.Files/Services/ContentHasher.cs` | ✓ Add `ChunkAndHashCdcAsync()` method implementing FastCDC algorithm |
| Server: `FileVersionChunk` model | ✓ `Offset` (long) and `ChunkSize` (int) columns added |
| Server: `InitiateUploadDto` / gRPC request | ✓ `ChunkSizes` array field added |
| `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkedTransferClient.cs` | Replace `SplitIntoChunksAsync()` with FastCDC-based splitting |
| `src/Clients/DotNetCloud.Client.Core/Api/DotNetCloudApiClient.cs` | Update `InitiateUploadAsync()` to send chunk sizes |

**What already exists:**
- `ContentHasher.ChunkAndHashAsync()` at `src/Modules/Files/DotNetCloud.Modules.Files/Services/ContentHasher.cs` — current fixed-size (4MB) chunking. The CDC version should be a NEW method alongside the existing one, not a replacement (for backward compat).
- `ChunkedTransferClient.SplitIntoChunksAsync()` at `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkedTransferClient.cs` — current client-side fixed chunking.

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
- ✓ Server: FastCDC implementation in `ContentHasher` (`ChunkAndHashCdcAsync`, `CdcChunkInfo`)
- ✓ Server: `Offset` + `ChunkSize` fields on `FileVersionChunk`
- ✓ Server: Updated `InitiateUploadDto` with `ChunkSizes`, gRPC proto with `chunk_sizes`
- ✓ Server: `ChunkSizesManifest` on `ChunkedUploadSession`; offsets computed in `CompleteUploadAsync`
- ✓ Server: Backward-compatible (legacy clients without chunk sizes work unchanged)
- ☐ Client: FastCDC-based `SplitIntoChunksAsync()` replacement
- ☐ Client: Chunk sizes sent in upload initiation (`InitiateUploadDto.ChunkSizes`)
- ☐ Client: `X-Sync-Capabilities: cdc` header for feature negotiation

**Side:** Both
**Complexity:** Medium

---

### 2.2 — Streaming Chunk Pipeline

**Approved Proposal:** 1.2

**Problem:** All chunks buffered in memory simultaneously — OOM risk on large files.

**Files to modify:**

| File (repo-relative) | What to do |
|---|---|
| `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkedTransferClient.cs` | Refactor `UploadAsync()` and download to use `Channel<ChunkData>` bounded producer-consumer pattern |

**What already exists:**
- `ChunkedTransferClient.UploadAsync()` at `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkedTransferClient.cs` — currently buffers all chunks.
- `TransferProgress` at `src/Clients/DotNetCloud.Client.Core/Transfer/TransferProgress.cs` — has `ChunksTransferred`, `TotalChunks`, `BytesTransferred`, `TotalBytes`.

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

**Files to modify:**

| File (repo-relative) | What to do |
|---|---|
| Server: `src/Core/DotNetCloud.Core.Server/Program.cs` (or ServiceDefaults) | Add `AddResponseCompression()` with Brotli + Gzip |
| `src/Clients/DotNetCloud.Client.Core/ClientCoreServiceExtensions.cs` | Ensure `AutomaticDecompression = DecompressionMethods.All` on HttpClient handler |
| `src/Clients/DotNetCloud.Client.SyncService/SyncServiceExtensions.cs` | Same for named `"DotNetCloudSync"` HttpClient |
| `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkedTransferClient.cs` | Gzip-wrap upload chunk streams + `Content-Encoding: gzip` header |

**What already exists:**
- `ClientCoreServiceExtensions` registers the typed `DotNetCloudApiClient` HttpClient — check if `HttpClientHandler { AutomaticDecompression = ... }` is already configured. If not, add it.
- `SyncServiceExtensions` registers the named `"DotNetCloudSync"` HttpClient — same check.
- Security headers middleware at `src/Core/DotNetCloud.Core.ServiceDefaults/` may already handle some response headers.

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

**Files to modify:**

| File (repo-relative) | What to do |
|---|---|
| Server: New model `UserSyncCounter` (add in Core.Data or Files module models) | Per-user monotonic sequence counter |
| Server: `FileNode` model (find via search for `FileNode` in `src/`) | Add `SyncSequence long?` column |
| Server: `SyncController.cs` at `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/SyncController.cs` | Accept `cursor` query param, return `nextCursor` |
| Server: SyncService (find via search for `GetChangesSinceAsync` in `src/Modules/Files/`) | Add cursor-based overload |
| `src/Clients/DotNetCloud.Client.Core/Api/DotNetCloudApiClient.cs` | Add cursor overload for `GetChangesSinceAsync()` |
| `src/Clients/DotNetCloud.Client.Core/LocalState/LocalStateDbContext.cs` | Replace `SyncCheckpointRow.LastSyncedAt` with `SyncCursor` string |

**What already exists:**
- `DotNetCloudApiClient.GetChangesSinceAsync()` at `src/Clients/DotNetCloud.Client.Core/Api/DotNetCloudApiClient.cs` — currently uses a `DateTime since` parameter.
- `SyncCheckpointRow` in `LocalStateDbContext` — currently stores `LastSyncedAt` (DateTime).

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

**Files to modify:**

| File (repo-relative) | What to do |
|---|---|
| Server: `SyncController.cs` at `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/SyncController.cs` | Accept `limit` query param, return `hasMore` + `nextCursor` |
| `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs` | Add pagination loop in `ApplyRemoteChangesAsync()` |
| `src/Clients/DotNetCloud.Client.Core/Api/DotNetCloudApiClient.cs` | Update `GetChangesSinceAsync()` to accept `limit` param and return `hasMore` |

**What already exists:**
- `SyncEngine.ApplyRemoteChangesAsync()` at `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs` — currently processes all remote changes in one call.

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

**Files to modify:**

| File (repo-relative) | What to do |
|---|---|
| Server: chunk download endpoint in `FilesController.cs` at `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/FilesController.cs` | Add `ETag` response header, handle `If-None-Match` → `304` |
| `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkedTransferClient.cs` | Send `If-None-Match` header before downloading, handle `304 Not Modified` |

**What already exists:**
- `DotNetCloudApiClient.DownloadChunkByHashAsync()` at `src/Clients/DotNetCloud.Client.Core/Api/DotNetCloudApiClient.cs` — downloads a chunk by its content hash. The hash IS the ETag (content-addressed storage).

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

**Files to modify:**

| File (repo-relative) | What to do |
|---|---|
| Create: `src/Clients/DotNetCloud.Client.Core/SyncIgnore/SyncIgnoreParser.cs` | New class: `.gitignore`-compatible pattern parser |
| `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs` | Add `SyncIgnoreParser.IsIgnored()` checks before upload queue / download apply / periodic scan |
| `src/Clients/DotNetCloud.Client.Core/DotNetCloud.Client.Core.csproj` | Add `Microsoft.Extensions.FileSystemGlobbing` NuGet |
| `src/Clients/DotNetCloud.Client.Core/ClientCoreServiceExtensions.cs` | Register `SyncIgnoreParser` in DI |
| `src/Clients/DotNetCloud.Client.SyncTray/` (views/viewmodels) | "Ignored Files" settings panel |

**What already exists:**
- `SelectiveSyncConfig` at `src/Clients/DotNetCloud.Client.Core/SelectiveSync/SelectiveSyncConfig.cs` — folder-level selective sync. The `.syncignore` system is complementary (not a replacement). Selective sync controls which folders to sync; `.syncignore` controls which files/patterns to skip within synced folders.
- `SyncEngine` at `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs` — has `ApplyRemoteChangesAsync()`, `ApplyLocalChangesAsync()`, and `RunPeriodicScanAsync()` where ignore checks need to be added.

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

**Files to modify:**

| File (repo-relative) | What to do |
|---|---|
| `src/Clients/DotNetCloud.Client.Core/LocalState/LocalStateDbContext.cs` | Add `ActiveUploadSessionRecord` entity + DbSet |
| `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkedTransferClient.cs` | Persist session after `InitiateUploadAsync()`, update after each chunk, delete after `CompleteUploadAsync()` |
| `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs` | On startup, query `ActiveUploadSessionRecord` for incomplete sessions and resume |

**What already exists:**
- `ChunkedTransferClient.UploadAsync()` at `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkedTransferClient.cs` — calls `InitiateUploadAsync()` → uploads chunks → `CompleteUploadAsync()`. Session persistence needs to wrap this flow.
- `LocalStateDbContext` at `src/Clients/DotNetCloud.Client.Core/LocalState/LocalStateDbContext.cs` — where the new entity goes.
- `SyncEngine.StartAsync()` at `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs` — startup hook for resuming incomplete sessions.

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

**Files to modify:**

| File (repo-relative) | What to do |
|---|---|
| `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs` | Replace `File.OpenRead()` calls with `FileShare.ReadWrite \| FileShare.Delete`; add retry loop for sharing violations |
| Create: `src/Clients/DotNetCloud.Client.Core/Platform/ILockedFileReader.cs` | Interface for Tier 3 (VSS) locked file reading |
| Create: `src/Clients/DotNetCloud.Client.Core/Platform/VssLockedFileReader.cs` | Windows VSS implementation (AlphaVSS) |
| Create: `src/Clients/DotNetCloud.Client.Core/Platform/NoOpLockedFileReader.cs` | Linux/macOS fallback (returns null) |
| `src/Clients/DotNetCloud.Client.Core/Sync/SyncState.cs` | Add `Deferred` value to `SyncStateTag` or `LocalFileRecord.SyncStateTag` |
| `src/Clients/DotNetCloud.Client.Core/ClientCoreServiceExtensions.cs` | Register `ILockedFileReader` with platform detection |
| `src/Clients/DotNetCloud.Client.Core/DotNetCloud.Client.Core.csproj` | Add `AlphaVSS` NuGet package (Windows-only conditional) |

**What already exists:**
- `SyncEngine` file read operations — search for `File.OpenRead`, `File.ReadAllBytes`, or `FileStream` usage in `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs`.
- `SyncState.cs` at `src/Clients/DotNetCloud.Client.Core/Sync/SyncState.cs` has enum values: `Idle`, `Syncing`, `Paused`, `Error`, `Offline`.
- `LocalFileRecord.SyncStateTag` (string property) uses values "Synced", "Pending", "Conflict" — add "Deferred" as a new value.

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

**Files to modify:**

| File (repo-relative) | What to do |
|---|---|
| `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkedTransferClient.cs` | Already has `IProgress<TransferProgress>` — make sure it's wired through to IPC events |
| `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcProtocol.cs` | Add `transfer-progress` and `transfer-complete` event types |
| `src/Clients/DotNetCloud.Client.SyncService/ContextManager/SyncContextManager.cs` | Wire `TransferProgress` callback to IPC event publishing with throttling (max 2/sec/file) |
| `src/Clients/DotNetCloud.Client.SyncTray/` (views/viewmodels) | `ActiveTransfersViewModel` with progress bars |

**What already exists:**
- `TransferProgress` at `src/Clients/DotNetCloud.Client.Core/Transfer/TransferProgress.cs` — has `BytesTransferred`, `TotalBytes`, `ChunksTransferred`, `TotalChunks`, `ChunksSkipped`, `PercentComplete`.
- `IpcProtocol` at `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcProtocol.cs` — existing IPC message protocol.
- `SyncContextManager` at `src/Clients/DotNetCloud.Client.SyncService/ContextManager/SyncContextManager.cs` — already raises `SyncProgress`, `SyncComplete`, `SyncError`, `ConflictDetected` events.

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

**Files to modify:**

| File (repo-relative) | What to do |
|---|---|
| `src/Clients/DotNetCloud.Client.Core/LocalState/LocalStateDbContext.cs` | Add `ConflictRecord` entity + DbSet |
| `src/Clients/DotNetCloud.Client.Core/Conflict/ConflictResolver.cs` | Save `ConflictRecord` to DB; implement auto-resolution pipeline (5 strategies) |
| `src/Clients/DotNetCloud.Client.Core/Conflict/ConflictInfo.cs` | May need extension with `BaseContentHash`, `AutoResolved` etc. |
| `src/Clients/DotNetCloud.Client.Core/DotNetCloud.Client.Core.csproj` | Add `DiffPlex` NuGet for text diff/merge; `Microsoft.XmlDiffPatch` for XML merge |
| `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcProtocol.cs` | Add `list-conflicts` and `resolve-conflict` IPC commands |
| `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcClientHandler.cs` | Handle new commands |
| `src/Clients/DotNetCloud.Client.SyncTray/` (views/viewmodels) | "Conflicts" panel, tray icon warning state, badge, merge editor window |

**What already exists:**
- `ConflictResolver` at `src/Clients/DotNetCloud.Client.Core/Conflict/ConflictResolver.cs` — currently creates conflict copies and fires `ConflictDetected` event. Uses `BuildConflictCopyPath()` to generate `{baseName} (conflict - {user} - {date}){ext}` filename.
- `ConflictInfo` at `src/Clients/DotNetCloud.Client.Core/Conflict/ConflictInfo.cs` — describes a conflict with local path, node ID, remote timestamp, and remote content hash.
- `IpcClientHandler` at `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcClientHandler.cs` — handles existing IPC commands like `sync-now`, `list-contexts`, etc.
- `SyncContextManager` already raises `ConflictDetected` event at `src/Clients/DotNetCloud.Client.SyncService/ContextManager/SyncContextManager.cs`.

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

**Files to modify:**

| File (repo-relative) | What to do |
|---|---|
| `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs` | Add pre-upload content hash comparison in `ApplyLocalChangesAsync()` or `ExecutePendingOperationAsync()` |
| `src/Clients/DotNetCloud.Client.Core/Api/DotNetCloudApiClient.cs` | Use `GetNodeAsync()` to fetch current server content hash for comparison |

**What already exists:**
- `SyncEngine.ApplyLocalChangesAsync()` at `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs` — processes pending uploads. This is where the hash comparison should go.
- `DotNetCloudApiClient.GetNodeAsync()` at `src/Clients/DotNetCloud.Client.Core/Api/DotNetCloudApiClient.cs` — fetches node metadata including content hash.
- `SyncEngine.ComputeFileHashAsync()` at `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs` — computes SHA-256 of a local file.
- `DotNetCloudApiClient.InitiateUploadAsync()` already returns which chunks exist on the server (dedup). If ALL chunks in manifest already exist, `CompleteUploadAsync()` is essentially free (no chunk transfers). This means the system is ALREADY partially idempotent at the chunk level — this task formalizes it at the file level.

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

**Files to modify:**

| File (repo-relative) | What to do |
|---|---|
| Server: File service (search for `CreateFolderAsync` and `RenameAsync` in `src/Modules/Files/`) | Add case-insensitive uniqueness check before file/folder creation |
| Server: Upload service (search for `CompleteUploadAsync` in `src/Modules/Files/`) | Same check on new file creation |
| Server: `src/Core/DotNetCloud.Core.Server/appsettings.json` | Add `FileSystem:EnforceCaseInsensitiveUniqueness` config |
| `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs` | Check for case-insensitive name collisions before applying remote changes |

**What already exists:**
- `SyncEngine.ApplyRemoteChangesAsync()` at `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs` — applies server changes to local filesystem. Case conflict detection should go here.

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

**Files to modify:**

| File (repo-relative) | What to do |
|---|---|
| Server: `FileNode` model (search in `src/`) | Add `PosixMode int?` and `PosixOwnerHint string?` columns |
| Server: `FileVersion` model (search in `src/`) | Add `PosixMode int?` column |
| Server: DTOs (`FileNodeDto`, `SyncChangeDto`, `SyncTreeNodeDto`) | Add `PosixMode` + `PosixOwnerHint` fields |
| Server: gRPC `FileNodeInfo` message | Add fields |
| `src/Clients/DotNetCloud.Client.Core/Api/DotNetCloudApiClient.cs` | Include `PosixMode`/`PosixOwnerHint` in upload/download DTOs |
| `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs` | Linux: read/apply file permissions; Windows: pass null, ignore on download |

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

**Files to modify:**

| File (repo-relative) | What to do |
|---|---|
| `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs` | Add symlink detection in FileSystemWatcher handlers and periodic scan |
| `src/Clients/DotNetCloud.Client.SyncService/sync-settings.json` | Add `"symlinks": { "mode": "ignore" }` config |
| Server: `FileNode` model | Add `NodeType.SymbolicLink` enum value, `LinkTarget string?` column |
| `src/Clients/DotNetCloud.Client.SyncTray/` (settings views) | Symlink mode dropdown |

**What already exists:**
- `SyncEngine` file watcher event handlers — search for `FileSystemWatcher` setup in `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs`.
- .NET 7+ `FileSystemInfo.LinkTarget` property can detect symlinks.

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

**Files to modify:**

| File (repo-relative) | What to do |
|---|---|
| `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs` | On startup (Linux only): read inotify limits, estimate needed, offer auto-fix, fallback to fast periodic scan |
| `src/Core/DotNetCloud.Core.Server/Program.cs` (or health check service) | Server startup inotify/inode checks, health endpoint degraded status |
| `tools/install.sh` | Set recommended inotify limit during installation |

**What already exists:**
- `SyncEngine.StartAsync()` at `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs` — startup hook where inotify checks should go (guarded by `OperatingSystem.IsLinux()`).
- `SyncEngine.RunPeriodicScanAsync()` — the fallback that runs every 5 minutes. When inotify fails, switch to 30-second interval.
- Server health checks may exist at `src/Core/DotNetCloud.Core.ServiceDefaults/` — search for "health" to find existing health check registration.

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

**Files to modify:**

| File (repo-relative) | What to do |
|---|---|
| `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs` | Add path length checks before writing files; use `\\?\` prefix fallback on Windows; add `SyncStateTag.PathTooLong` |
| `src/Clients/DotNetCloud.Client.Core/Sync/SyncState.cs` | Add `PathTooLong` to state tag values |
| Server: File service (upload/rename handlers) | Validate filename characters (reject Windows-illegal chars) and reserved names |
| Server: `src/Core/DotNetCloud.Core.Server/appsettings.json` | Add `FileSystem:MaxPathWarningThreshold` config |
| Client: App manifest (build config in `.csproj`) | Add `<longPathAware>true</longPathAware>` |

**What already exists:**
- `SyncEngine.ApplyRemoteChangesAsync()` — where downloads/writes happen. Path length check should go before file write operations.
- `LocalFileRecord.SyncStateTag` (string) — uses "Synced", "Pending", "Conflict". Add "PathTooLong" as a new value.

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

**Files to modify:**

| File (repo-relative) | What to do |
|---|---|
| Create: `src/Clients/DotNetCloud.Client.Core/Transfer/ThrottledStream.cs` | Token bucket rate-limiting stream wrapper |
| Create: `src/Clients/DotNetCloud.Client.Core/Api/ThrottledHttpHandler.cs` | `DelegatingHandler` wrapping request/response streams in `ThrottledStream` |
| `src/Clients/DotNetCloud.Client.SyncService/SyncServiceExtensions.cs` | Wire `ThrottledHttpHandler` into `HttpClientFactory` |
| `src/Clients/DotNetCloud.Client.SyncService/ContextManager/SyncContextManager.cs` | Read throttle limits from `SyncContext` config and apply to engine |

**What already exists:**
- `SyncContext` at `src/Clients/DotNetCloud.Client.Core/Sync/SyncContext.cs` — already has `UploadLimitKbps` and `DownloadLimitKbps` fields (verify by reading the file).
- `SyncServiceExtensions` at `src/Clients/DotNetCloud.Client.SyncService/SyncServiceExtensions.cs` — where HttpClient is configured. The `ThrottledHttpHandler` should be registered alongside `CorrelationIdHandler`.
- Settings UI already has input fields for bandwidth limits — they just don't do anything yet.

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

**Files to modify:**

| File (repo-relative) | What to do |
|---|---|
| Create: `src/Clients/DotNetCloud.Client.SyncTray/Views/FolderBrowserView.axaml` + `.axaml.cs` | Avalonia TreeView with checkboxes |
| Create: `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/FolderBrowserViewModel.cs` | Fetches tree from server API, manages check states |
| `src/Clients/DotNetCloud.Client.Core/SelectiveSync/SelectiveSyncConfig.cs` | Update from browser selections |
| `src/Clients/DotNetCloud.Client.Core/Api/DotNetCloudApiClient.cs` | Already has `GetFolderTreeAsync()` — verify it returns enough data for the browser |

**What already exists:**
- `SelectiveSyncConfig` at `src/Clients/DotNetCloud.Client.Core/SelectiveSync/SelectiveSyncConfig.cs` — in-memory selective sync config with JSON persistence.
- `DotNetCloudApiClient.GetFolderTreeAsync()` at `src/Clients/DotNetCloud.Client.Core/Api/DotNetCloudApiClient.cs` — fetches the folder tree from the server.

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
