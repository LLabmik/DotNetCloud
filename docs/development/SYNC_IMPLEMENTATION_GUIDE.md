# Sync Improvement Implementation Guide

**Purpose:** Step-by-step instructions for implementing the Sync Improvement Plan. Every decision is already made — follow the instructions exactly. No design decisions required.

**Reference:** Full design rationale is in [SYNC_IMPROVEMENT_PLAN.md](SYNC_IMPROVEMENT_PLAN.md)

**Date:** 2026-03-08

---

## How To Use This Document

Each task below is a self-contained unit of work. Do them **in order within each batch**. Each task has:
- **What** — the exact thing to build
- **Where** — exact file paths and project names
- **How** — step-by-step instructions with code patterns
- **Test** — how to verify it works
- **Done when** — acceptance criteria

**Terminology:**
- "Server" = the ASP.NET Core server project at `src/Modules/Files/DotNetCloud.Modules.Files/`
- "Server Core" = `src/Core/DotNetCloud.Core/` and `src/Core/DotNetCloud.Core.Data/`
- "Client Core" = `src/Clients/SyncService/DotNetCloud.Client.Core/`
- "Client SyncService" = `src/Clients/SyncService/DotNetCloud.Client.SyncService/`
- "Client SyncTray" = `src/Clients/SyncTray/DotNetCloud.Client.SyncTray/`

**Project reference:** Always check the actual solution structure with `dotnet sln DotNetCloud.sln list` if unsure about project paths.

---

## Build Environments + Handoff Workflow

**This is a multi-machine project.** Server code runs on Linux. Client code runs on Windows. They share one git repo but are built and tested on their target OS.

### Machines

| Role | Machine | OS | What gets built here |
|------|---------|-----|---------------------|
| **Server** | `mint22` | Linux Mint 22 | Server projects (`DotNetCloud.Core`, `DotNetCloud.Core.Data`, `DotNetCloud.Modules.Files`, etc.) |
| **Client (primary)** | `Windows11-TestDNC` | Windows 11 | Client projects (`DotNetCloud.Client.Core`, `DotNetCloud.Client.SyncService`, `DotNetCloud.Client.SyncTray`) |
| **Client (future)** | `mint-dnc-client` | Linux Mint | Linux client build/test (Batch 4+) |

### Rule: Build on the Target OS

- **Server code** → build and test on `mint22` (Linux)
- **Client code** → build and test on `Windows11-TestDNC` (Windows)
- **Shared libraries** (e.g., interfaces in `DotNetCloud.Core`) → build on whichever machine is making the change, but verify on both
- **Never** build the Windows client on Linux just because the code is there. Build on Windows so platform-specific features (VSS, Cloud Files API, registry access, app manifest, named pipes) actually compile and run.
- **Never** build the server on Windows. The server runs on Linux with Unix sockets, POSIX file permissions, and systemd. Build where it runs.

### Handoff Process (Step by Step)

Many tasks touch **both** server and client. **Handoffs happen at the individual task level, NOT the batch level.** When you reach a "Both" task:

1. The server agent does ONLY that task's server side
2. Writes a handoff entry for ONLY that task
3. The client agent does ONLY that task's client side
4. Then move to the next task in order

**Do NOT batch all server work together.** Tasks within a batch are done in order, and "Both" tasks cause a server→client round-trip before moving to the next task.

Here's the exact workflow for a single "Both" task:

```
┌─────────────────────────────────────────────────────────────┐
│ 1. SERVER AGENT (on mint22, Linux)                          │
│    - Implement the server-side part of the task              │
│    - Build: dotnet build                                    │
│    - Test:  dotnet test                                     │
│    - Commit + push to main                                  │
│    - Record: commit hash, what changed, any new API         │
│      contracts (endpoints, DTOs, headers, status codes)     │
│                                                             │
│ 2. HANDOFF (via CLIENT_SERVER_MEDIATION_HANDOFF.md)         │
│    - Server agent writes what changed into the handoff doc  │
│    - Include: commit hash, exact endpoint signatures,       │
│      new request/response shapes, new headers, new DB       │
│      columns, any breaking changes                          │
│    - Push the handoff doc to main                           │
│                                                             │
│ 3. CLIENT AGENT (on Windows11-TestDNC, Windows)             │
│    - Pull latest from main (gets server changes + handoff)  │
│    - Read the handoff doc to understand what changed         │
│    - Implement the client-side part of the task             │
│    - Build: dotnet build                                    │
│    - Test:  dotnet test                                     │
│    - Commit + push to main                                  │
│    - Record: commit hash back into handoff doc              │
│                                                             │
│ 4. VERIFICATION                                             │
│    - Run end-to-end sync between mint22 and Windows11       │
│    - Upload a file from client → verify on server           │
│    - Create a file on server → verify syncs to client       │
│    - Check logs on both sides for correlation IDs matching  │
│                                                             │
│ 5. LINUX CLIENT VERIFICATION (Batch 4+)                     │
│    - Pull on mint-dnc-client                                │
│    - Build + test client projects                           │
│    - Run sync against mint22 server                         │
│    - Verify platform-specific behavior (permissions,        │
│      symlinks, inotify, case sensitivity)                   │
└─────────────────────────────────────────────────────────────┘
```

### Per-Task Side Labels

Every task in this guide has a **Side** label:

| Label | What it means | Where to build |
|-------|---------------|----------------|
| **Server only** | Only server code changes | Build + test on `mint22` (Linux) only |
| **Client only** | Only client code changes | Build + test on `Windows11-TestDNC` (Windows) only |
| **Both** | Server AND client change | Server first on `mint22`, handoff, then client on `Windows11-TestDNC` |

### Task Classification Quick Reference

**Server only** (build on Linux):
- 1.1b (audit logging), 1.3 (rate limiting), 1.8 (temp files), 1.9 (file scanning)

**Client only** (build on Windows):
- 1.1 (sync logging), 1.4 (chunk integrity), 1.5 (chunk retry), 1.6 (SQLite WAL), 1.7 (retry queue)
- 2.2 (streaming pipeline)
- 3.1 (.syncignore), 3.2 (persistent uploads), 3.3 (locked files), 3.4 (progress UI), 3.5a-e (conflicts), 3.6 (idempotent)
- 5.1 (throttling), 5.2 (folder browser)

**Both** (server first on Linux, then client on Windows):
- 1.2 (correlation IDs)
- 2.1 (CDC), 2.3 (compression), 2.4 (sync cursor), 2.5 (pagination), 2.6 (ETags)
- 4.1 (case sensitivity), 4.2 (permissions), 4.3 (symlinks), 4.4 (inotify), 4.5 (path limits)

### Handoff Document Format

When completing the server side of a "Both" task, write an entry in `CLIENT_SERVER_MEDIATION_HANDOFF.md`:

```markdown
### Issue #{number}: {Task name}

**Server commit:** `{hash}`
**Status:** Server complete, awaiting client implementation

**What changed (server):**
- Added endpoint: `GET /api/v1/sync/changes?cursor={base64}`
- New response fields: `nextCursor` (string), `hasMore` (bool)
- New DB column: `FileNode.SyncSequence` (long, nullable)
- Backward compat: `since` param still works if no cursor provided

**Client needs to:**
- Send `cursor` query param instead of `since` timestamp
- Store cursor string in LocalStateDb
- Loop on `hasMore == true` to consume all pages

**Testing contract:**
- Call with no cursor → returns all items + first cursor
- Call with cursor → returns only newer items + next cursor
- Call with `since` (no cursor) → old behavior still works
```

### Android Client (Future)

When the Android client starts, it will follow the same pattern:
- Build on the Android development machine (or CI with Android SDK)
- Same handoff doc workflow — server changes come first, client adapts
- Same git repo, same API contracts

### Key Principle

**The server defines the contract. The client adapts to it.** Server-side changes always land first for "Both" tasks. The client pulls, reads the handoff, and implements its side against the now-live server API.

### Example: Batch 2 Task Order (Interleaved)

Batch 2 has 6 tasks. Here's the exact execution order showing how agents alternate:

```
Task 2.1 (Both):   → SERVER agent: implement CDC server-side → HANDOFF → CLIENT agent: implement CDC client-side
Task 2.2 (Client):  → CLIENT agent: implement streaming pipeline
Task 2.3 (Both):   → SERVER agent: implement compression → HANDOFF → CLIENT agent: add compression support
Task 2.4 (Both):   → SERVER agent: add sync cursor → HANDOFF → CLIENT agent: use sync cursor
Task 2.5 (Both):   → SERVER agent: add pagination → HANDOFF → CLIENT agent: consume pages
Task 2.6 (Both):   → SERVER agent: add ETag headers → HANDOFF → CLIENT agent: send If-None-Match
```

Notice: Task 2.2 (Client only) runs entirely on Windows — no server work, no handoff. But Task 2.3 requires the server agent to go first, handoff, then the client agent finishes before moving to 2.4.

**The pattern for each task is always: check Side label → do the work on the right machine(s) → move to next task.**

---

## Batch 1 — Foundation (Do First)

### Task 1.1: Add Serilog Logging to Sync Service (Client)

**What:** Add structured JSON logging to the client sync service.

**Where:** `src/Clients/SyncService/DotNetCloud.Client.SyncService/`

**How:**
1. Add NuGet packages to the SyncService project:
   - `Serilog.AspNetCore`
   - `Serilog.Sinks.File`
   - `Serilog.Formatting.Compact`
2. In `Program.cs` (or wherever the host is built), configure Serilog:
   ```csharp
   Log.Logger = new LoggerConfiguration()
       .MinimumLevel.Information()
       .WriteTo.File(
           new CompactJsonFormatter(),
           Path.Combine(GetSystemDataRoot(), "logs", "sync-service.log"),
           rollingInterval: RollingInterval.Day,
           retainedFileCountLimit: 30,
           fileSizeLimitBytes: 50 * 1024 * 1024)
       .CreateLogger();
   ```
   - `GetSystemDataRoot()` already exists — it returns `%APPDATA%\DotNetCloud` on Windows, `~/.local/share/DotNetCloud` on Linux
3. Add log statements to these classes/methods (use `ILogger<T>` injection):
   - `SyncEngine`: log sync pass start, complete (with duration + file count), and errors
   - `ChunkedTransferClient`: log each file upload/download start, complete (with size, duration), and errors
   - `ConflictResolver`: log conflict detection (file path, reason)
   - `OAuth2Service`: log token refresh success/failure (never log the actual token)
   - `IpcServer`: log IPC commands received
   - `SyncWorker`: log FileSystemWatcher events that trigger sync
4. Add a `"logging"` section to `sync-settings.json`:
   ```json
   {
     "logging": {
       "retentionDays": 30,
       "maxFileSizeMB": 50,
       "minimumLevel": "Information"
     }
   }
   ```
5. On Linux: set log file permissions to `600` (owner-only) using `File.SetUnixFileMode()` after creating the log directory.

**Test:** Run the sync service. Check that `logs/sync-service.log` appears with JSON entries. Trigger a sync, verify events are logged.

**Done when:** Structured JSON log file is written with sync lifecycle events. Log rotation works (daily files, 30-day retention).

**Side:** Client only → **Build on `Windows11-TestDNC` (Windows)**

---

### Task 1.1b: Add Sync Audit Logging (Server)

**What:** Add structured Serilog audit logging to server sync/file operations.

**Where:** `src/Modules/Files/DotNetCloud.Modules.Files/`

**How:**
1. In the file module's service classes (`ChunkedUploadService`, `DownloadService`, `FileService`, `SyncService`), add `ILogger<T>` if not already present.
2. Log these events with structured fields:
   - `file.uploaded` — after `CompleteUploadAsync()` succeeds: `{NodeId, FileName, FileSize, UserId, ClientIp, RequestId}`
   - `file.downloaded` — in download endpoint: `{NodeId, FileName, FileSize, UserId, ClientIp, RequestId}`
   - `file.deleted` — in delete handler: `{NodeId, FileName, UserId, ClientIp, RequestId}`
   - `file.moved` / `file.renamed` — in move/rename handlers: `{NodeId, OldPath, NewPath, UserId}`
   - `sync.reconcile.completed` — after reconcile: `{UserId, ChangeCount, Duration}`
3. Add a dedicated audit log file sink. In the server's Serilog config, add a second `WriteTo` targeting `{DOTNETCLOUD_DATA_DIR}/logs/audit-sync.log` with the same rolling-file settings (daily, 30-day retention).

**Test:** Upload, download, and delete a file via the API. Check `audit-sync.log` for structured entries.

**Done when:** All sync/file operations write structured audit log entries to a dedicated file.

**Side:** Server only → **Build on `mint22` (Linux)**

---

### Task 1.2: Request Correlation IDs

**What:** Add `X-Request-ID` header to every request/response so client and server logs can be matched.

**Where:** Server: `src/Core/DotNetCloud.Core.ServiceDefaults/` or server's `Program.cs`. Client: `DotNetCloudApiClient`.

**How — Server:**
1. Create `RequestCorrelationMiddleware`:
   ```csharp
   public class RequestCorrelationMiddleware
   {
       private readonly RequestDelegate _next;

       public RequestCorrelationMiddleware(RequestDelegate next) => _next = next;

       public async Task InvokeAsync(HttpContext context)
       {
           var requestId = context.Request.Headers["X-Request-ID"].FirstOrDefault()
                           ?? Guid.NewGuid().ToString("N");

           context.Response.Headers["X-Request-ID"] = requestId;

           using (LogContext.PushProperty("RequestId", requestId))
           {
               await _next(context);
           }
       }
   }
   ```
2. Register in the pipeline: `app.UseMiddleware<RequestCorrelationMiddleware>();` — place early in the pipeline (before auth, before controllers).

**How — Client:**
1. In `DotNetCloudApiClient` (or a `DelegatingHandler`):
   ```csharp
   var requestId = Guid.NewGuid().ToString("N");
   request.Headers.Add("X-Request-ID", requestId);
   _logger.LogInformation("API call {Method} {Url} RequestId={RequestId}", method, url, requestId);
   ```
2. On error, include the request ID in the log: `_logger.LogError("API call failed. RequestId={RequestId}, Status={StatusCode}", requestId, response.StatusCode);`

**Test:** Make an API call from the client. Check that the server log entry and client log entry share the same `RequestId` value.

**Done when:** Every API call has a `RequestId` in both client and server logs.

**Side:** Both → **Server on `mint22` first, then client on `Windows11-TestDNC`. Use handoff doc.**

---

### Task 1.3: Server Rate Limiting

**What:** Add per-user rate limiting to sync endpoints.

**Where:** Server `Program.cs` + sync/file controllers.

**How:**
1. In `Program.cs`, add rate limiting:
   ```csharp
   builder.Services.AddRateLimiter(options =>
   {
       options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
       options.AddSlidingWindowLimiter("sync-standard", limiterOptions =>
       {
           limiterOptions.PermitLimit = 60;
           limiterOptions.Window = TimeSpan.FromMinutes(1);
           limiterOptions.SegmentsPerWindow = 6;
       });
       options.AddSlidingWindowLimiter("sync-heavy", limiterOptions =>
       {
           limiterOptions.PermitLimit = 300;
           limiterOptions.Window = TimeSpan.FromMinutes(1);
           limiterOptions.SegmentsPerWindow = 6;
       });
       options.AddSlidingWindowLimiter("sync-tree", limiterOptions =>
       {
           limiterOptions.PermitLimit = 10;
           limiterOptions.Window = TimeSpan.FromMinutes(1);
           limiterOptions.SegmentsPerWindow = 2;
       });
   });
   ```
2. In the pipeline: `app.UseRateLimiter();`
3. Add `[EnableRateLimiting("sync-standard")]` to `SyncController` methods.
4. Add `[EnableRateLimiting("sync-heavy")]` to chunk upload/download endpoints.
5. Add `[EnableRateLimiting("sync-tree")]` to the tree endpoint.
6. Set `Retry-After` header in the rejection response via `options.OnRejected`.

**Rate limits to apply:**

| Endpoint Pattern | Policy | Limit |
|-----------------|--------|-------|
| `GET sync/changes` | `sync-standard` | 60/min |
| `GET sync/tree` | `sync-tree` | 10/min |
| `POST sync/reconcile` | `sync-standard` | 30/min |
| `POST files/upload/initiate` | `sync-standard` | 30/min |
| Chunk upload/download | `sync-heavy` | 300/min |
| File download | `sync-heavy` | 120/min |

**Test:** Send 61 requests to `sync/changes` within 1 minute. The 61st should return `429`. Check that `Retry-After` header is present.

**Done when:** Rate limiting returns 429 on excess requests with `Retry-After` header.

**Side:** Server only → **Build on `mint22` (Linux)**

---

### Task 1.4: Chunk Integrity Verification on Download

**What:** Verify SHA-256 hash of each downloaded chunk matches expected hash.

**Where:** `src/Clients/SyncService/DotNetCloud.Client.Core/` — `ChunkedTransferClient`

**How:**
1. In `DownloadAsync()`, after receiving chunk bytes:
   ```csharp
   var actualHash = ContentHasher.ComputeHash(chunkBytes);
   if (actualHash != expectedHash)
   {
       _logger.LogWarning("Chunk hash mismatch. Expected={Expected}, Actual={Actual}. Retrying.", expectedHash, actualHash);
       // retry up to 3 times
   }
   ```
2. Retry loop: if hash doesn't match after 3 attempts, throw an exception with the request ID.
3. Use the existing `ContentHasher.ComputeHash()` method (it already does SHA-256).

**Test:** Download a file. Verify no hash mismatch warnings. (To test the error path, temporarily modify the expected hash.)

**Done when:** Every downloaded chunk's SHA-256 is verified. Mismatches retry 3 times, then fail with a clear error.

**Side:** Client only → **Build on `Windows11-TestDNC` (Windows)**

---

### Task 1.5: Per-Chunk Retry with Exponential Backoff

**What:** Each individual chunk upload/download retries on failure instead of aborting the whole file.

**Where:** `ChunkedTransferClient` in Client Core.

**How:**
1. Wrap each chunk transfer in a retry loop:
   ```csharp
   int maxRetries = 3;
   for (int attempt = 1; attempt <= maxRetries; attempt++)
   {
       try
       {
           await UploadChunkAsync(chunk, cancellationToken);
           break; // success
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
2. Only retry on: network errors (`HttpRequestException`), 5xx status codes, timeouts.
3. Do NOT retry on: 4xx status codes (client error), 429 (handled separately).
4. Create a simple `ChunkTransferResult` record: `record ChunkTransferResult(string Hash, bool Success, int Attempts, string? Error);`
5. Log the final result for each chunk.

**Test:** Upload a large file (multiple chunks). Verify it succeeds. Temporarily break network mid-transfer and verify retry behavior.

**Done when:** Individual chunks retry with backoff. A single chunk failure doesn't abort the entire file transfer.

**Side:** Client only → **Build on `Windows11-TestDNC` (Windows)**

---

### Task 1.6: SQLite WAL Mode + Corruption Recovery

**What:** Enable WAL journal mode and add startup integrity checks to the client's SQLite database.

**Where:** `LocalStateDb` / `LocalStateDbContext` in Client Core.

**How:**
1. Add `Journal Mode=Wal` to the SQLite connection string.
2. On `InitializeAsync()` (or equivalent startup method), run:
   ```csharp
   using var cmd = connection.CreateCommand();
   cmd.CommandText = "PRAGMA integrity_check;";
   var result = (string?)await cmd.ExecuteScalarAsync();
   if (result != "ok")
   {
       _logger.LogError("SQLite database corrupted: {Result}", result);
       var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
       File.Move(dbPath, $"{dbPath}.corrupt.{timestamp}");
       if (File.Exists($"{dbPath}-wal")) File.Move($"{dbPath}-wal", $"{dbPath}-wal.corrupt.{timestamp}");
       if (File.Exists($"{dbPath}-shm")) File.Move($"{dbPath}-shm", $"{dbPath}-shm.corrupt.{timestamp}");
       // Recreate fresh DB
       await context.Database.EnsureCreatedAsync();
       // Set flag for full re-sync
       _needsFullResync = true;
       // Notify user
       await _notificationService.ShowAsync("Sync database was corrupted and has been reset. A full re-sync will occur.");
   }
   ```
3. After each complete sync pass, run: `PRAGMA wal_checkpoint(TRUNCATE);`

**Test:** Run the sync service. Verify `state.db-wal` exists (WAL mode active). Corrupt the DB manually, restart, verify recovery happens and a fresh DB is created.

**Done when:** WAL mode enabled. Corrupt DB is detected, preserved (renamed), and a fresh DB created automatically.

**Side:** Client only → **Build on `Windows11-TestDNC` (Windows)**

---

### Task 1.7: Operation Retry Queue with Backoff

**What:** Failed sync operations retry with exponential backoff instead of retrying every 5 minutes forever.

**Where:** `LocalStateDb` + `SyncEngine` in Client Core.

**How:**
1. Add two columns to `PendingOperationDbRow` (EF migration):
   - `NextRetryAt` — `DateTime?` (nullable — null means "ready now")
   - `LastError` — `string?` (nullable)
2. In `ExecutePendingOperationAsync()`, on failure:
   ```csharp
   operation.RetryCount++;
   operation.LastError = ex.Message;
   operation.NextRetryAt = operation.RetryCount switch
   {
       1 => DateTime.UtcNow.AddMinutes(1),
       2 => DateTime.UtcNow.AddMinutes(5),
       3 => DateTime.UtcNow.AddMinutes(15),
       4 => DateTime.UtcNow.AddHours(1),
       <= 9 => DateTime.UtcNow.AddHours(6),
       _ => null // move to failed
   };
   if (operation.RetryCount >= 10)
   {
       // Move to FailedOperationDbRow table, remove from pending
   }
   ```
3. Create `FailedOperationDbRow` table (same schema as pending, plus `FailedAt DateTime`).
4. In `GetPendingOperationsAsync()`, add filter: `WHERE NextRetryAt IS NULL OR NextRetryAt <= @now`
5. On success: set `RetryCount = 0`, `NextRetryAt = null`, `LastError = null`.

**Test:** Force an operation to fail (e.g., disconnect network). Verify it retries at increasing intervals. After 10 failures, verify it moves to the failed table.

**Done when:** Failed operations back off exponentially. After 10 failures, they stop retrying and are moved to a failed operations table.

**Side:** Client only → **Build on `Windows11-TestDNC` (Windows)**

---

### Task 1.8: Secure Temp File Handling (Server)

**What:** Use a dedicated temp directory with restrictive permissions instead of the system temp dir.

**Where:** Server — `DownloadService` and startup code.

**How:**
1. In server startup, create `{DOTNETCLOUD_DATA_DIR}/tmp/`:
   ```csharp
   var tmpDir = Path.Combine(dataDir, "tmp");
   Directory.CreateDirectory(tmpDir);
   if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
   {
       File.SetUnixFileMode(tmpDir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
   }
   ```
2. Modify `DownloadService.cs`: replace `Path.GetTempPath()` with the new temp dir path. Inject it via DI or configuration.
3. Add a `IHostedService` (or add to an existing startup service) that on startup deletes files in `{DATA_DIR}/tmp/` older than 1 hour:
   ```csharp
   foreach (var file in Directory.GetFiles(tmpDir))
   {
       if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddHours(-1))
           File.Delete(file);
   }
   ```

**Test:** Upload and download a file. Verify temp files are created in `{DATA_DIR}/tmp/`, not `/tmp/`. Restart server, verify old temp files are cleaned up.

**Done when:** Server uses its own temp directory with `700` permissions. Stale files cleaned on startup.

**Side:** Server only → **Build on `mint22` (Linux)**

---

### Task 1.9: Server-Side File Scanning Interface + Execution Prevention

**What:** Prevent uploaded files from being executable on disk. Add a scanner interface for future ClamAV integration.

**Where:** Server — `LocalFileStorageEngine`, file download endpoints, and new `IFileScanner` interface.

**How:**
1. **Execution prevention** — In `LocalFileStorageEngine.WriteChunkAsync()` (or wherever chunks are written to disk):
   ```csharp
   if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
   {
       File.SetUnixFileMode(chunkPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
   }
   ```
2. Verify that chunk storage paths use content-addressed hashes (they already do — document this in a code comment).
3. On file download endpoints, add headers:
   ```csharp
   response.Headers["X-Content-Type-Options"] = "nosniff";
   response.Headers["Content-Disposition"] = "attachment; filename=\"" + safeFileName + "\"";
   ```
4. Create `IFileScanner` interface:
   ```csharp
   public interface IFileScanner
   {
       Task<ScanResult> ScanAsync(Stream content, string fileName, CancellationToken ct);
   }

   public record ScanResult(bool IsClean, string? ThreatName = null, string? ScannerName = null);
   ```
5. Create `NoOpFileScanner : IFileScanner` that always returns `new ScanResult(true)`.
6. Register in DI: `services.AddSingleton<IFileScanner, NoOpFileScanner>();`
7. Add nullable `ScanStatus` to `FileVersion` model (enum: `NotScanned = 0`, `Clean = 1`, `Threat = 2`, `Error = 3`). Don't enforce it yet — just add the column (EF migration).
8. Max file size: add config `"FileUpload:MaxFileSizeBytes": 16106127360` (15GB). In `InitiateUploadAsync()`, reject if total size exceeds this.

**Test:** Upload a file, verify chunk files on disk have no execute permission (`ls -la`). Download a file, verify response headers include `nosniff` and `attachment`.

**Done when:** Chunk files have no execute bits. Download responses have security headers. `IFileScanner` interface exists with `NoOpFileScanner`. Max file size is enforced at 15GB.

**Side:** Server only → **Build on `mint22` (Linux)**

---

## Batch 2 — Efficiency (Do Second)

### Task 2.1: Content-Defined Chunking (CDC)

**What:** Replace fixed 4MB chunks with content-defined chunking so small edits only re-upload affected chunks.

**Where:** Server: `ContentHasher`. Client: `ChunkedTransferClient`.

**How — Server:**
1. Add NuGet package `FastCdc.Net` (or implement the FastCDC algorithm — it's a ~200 line rolling hash algorithm).
2. Add method to `ContentHasher`:
   ```csharp
   public async Task<List<ChunkInfo>> ChunkCdcAsync(Stream stream, int avgSize = 4 * 1024 * 1024, int minSize = 512 * 1024, int maxSize = 16 * 1024 * 1024)
   ```
   Returns `List<ChunkInfo>` where `ChunkInfo` has `{ string Hash, long Offset, int Size }`.
3. Add `Offset` (long) and `ChunkSize` (int) columns to `FileVersionChunk` (EF migration).
4. Update `InitiateUploadDto`: add `int[]? ChunkSizes` property alongside existing `string[] ChunkHashes`.
5. In `ChunkedUploadService.InitiateUploadAsync()`: if `ChunkSizes` is provided, store them. If not, assume fixed 4MB (backward compat).
6. In `ChunkedUploadService.CompleteUploadAsync()`: use `Offset` + `ChunkSize` from manifest for reassembly.

**How — Client:**
1. In `ChunkedTransferClient`, replace the fixed-size `SplitIntoChunksAsync()` with FastCDC-based splitting (same algorithm/library as server).
2. Send `ChunkSizes` array in `InitiateUploadAsync()`.
3. On download: read chunk manifest with sizes and reassemble using offset+size.
4. Add header `X-Sync-Capabilities: cdc` to requests (for future feature negotiation).

**Test:** Edit 1 byte in the middle of a large file. Re-upload. Verify only 1-2 chunks are uploaded (not the entire file).

**Done when:** Chunking is content-defined. Small edits = small uploads. Legacy fixed-size clients still work.

**Side:** Both → **Server on `mint22` first, then client on `Windows11-TestDNC`. Use handoff doc.**

---

### Task 2.2: Streaming Chunk Pipeline

**What:** Process chunks through a bounded channel instead of buffering all in memory.

**Where:** `ChunkedTransferClient` in Client Core.

**How:**
1. For uploads, create a producer-consumer pipeline:
   ```csharp
   var channel = Channel.CreateBounded<ChunkData>(new BoundedChannelOptions(8)
   {
       FullMode = BoundedChannelFullMode.Wait
   });

   // Producer: read file → split (CDC) → hash → write to channel
   var producer = Task.Run(async () =>
   {
       await foreach (var chunk in ChunkFileAsync(fileStream))
           await channel.Writer.WriteAsync(chunk, ct);
       channel.Writer.Complete();
   });

   // Consumers: 4 parallel uploaders
   var consumers = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
   {
       await foreach (var chunk in channel.Reader.ReadAllAsync(ct))
           await UploadChunkAsync(chunk, ct);
   }));

   await Task.WhenAll(new[] { producer }.Concat(consumers));
   ```
2. Peak memory is ~32MB (8 slots × 4MB avg) regardless of file size.
3. For downloads: download chunks to temp files on disk, then concatenate into the target file. Don't hold all chunks in memory.
4. Clean up temp chunk files after assembly.

**Test:** Upload a 500MB file. Monitor memory usage — it should stay under ~100MB, not spike to 500MB.

**Done when:** Uploads use bounded channels. Downloads stream to disk. Memory usage is bounded regardless of file size.

**Side:** Client only → **Build on `Windows11-TestDNC` (Windows)**

---

### Task 2.3: Compression for Chunk Transfers

**What:** Enable Brotli/Gzip compression for chunk transfers.

**Where:** Server: `Program.cs`. Client: `HttpClient` setup.

**How — Server:**
1. In `Program.cs`:
   ```csharp
   builder.Services.AddResponseCompression(options =>
   {
       options.EnableForHttps = true;
       options.Providers.Add<BrotliCompressionProvider>();
       options.Providers.Add<GzipCompressionProvider>();
       options.MimeTypes = ResponseCompressionDefaults.MimeTypes
           .Concat(new[] { "application/octet-stream" });
   });
   ```
2. In the pipeline: `app.UseResponseCompression();` — place before `UseRouting()`.
3. Skip compression for files with already-compressed MIME types (JPEG, PNG, ZIP, etc.)

**How — Client:**
1. Ensure `HttpClientHandler.AutomaticDecompression = DecompressionMethods.All` is set.
2. For chunk uploads, wrap the chunk stream in `GZipStream` and set `Content-Encoding: gzip`.
3. Check if the file's MIME type is already compressed before compressing upload chunks. Skip compression for: `.jpg`, `.jpeg`, `.png`, `.gif`, `.zip`, `.gz`, `.bz2`, `.xz`, `.7z`, `.rar`, `.mp4`, `.mp3`, `.mkv`, `.avi`, `.webm`.

**Test:** Upload a large text file. Verify network traffic is smaller than the raw file size (check with network tools or log the content length).

**Done when:** Responses are compressed. Uploads of compressible content are gzip-compressed. Already-compressed formats are sent raw.

**Side:** Both → **Server on `mint22` first, then client on `Windows11-TestDNC`. Use handoff doc.**

---

### Task 2.4: Server-Issued Sync Cursor

**What:** Replace timestamp-based sync delta with a monotonic sequence number ("cursor") that eliminates clock skew issues.

**Where:** Server: `SyncService`, `SyncController`, new DB models. Client: `DotNetCloudApiClient`, `LocalStateDb`.

**How — Server:**
1. Create `UserSyncCounter` entity:
   ```csharp
   public class UserSyncCounter
   {
       public string UserId { get; set; } = null!;
       public long CurrentSequence { get; set; }
   }
   ```
2. Add `SyncSequence` column (`long?`) to `FileNode` (EF migration).
3. On every file mutation (create, update, delete, move, rename):
   ```csharp
   var counter = await _db.UserSyncCounters.FindAsync(userId);
   if (counter == null) { counter = new UserSyncCounter { UserId = userId }; _db.Add(counter); }
   counter.CurrentSequence++;
   fileNode.SyncSequence = counter.CurrentSequence;
   ```
4. In `SyncController`, accept `cursor` query param:
   - Decode cursor: `{userId}:{sequenceNumber}` (base64 encoded)
   - Query: `WHERE SyncSequence > sequenceNumber`
   - Return `nextCursor` = base64 of `{userId}:{maxSequenceInResults}`
   - If no cursor param (or `since` param is provided instead): fall back to existing timestamp-based query

**How — Client:**
1. Change `DotNetCloudApiClient.GetChangesSinceAsync()` to accept an optional `cursor` string parameter.
2. Send `cursor` query param instead of `since` timestamp (when cursor is available).
3. In `LocalStateDb`, store cursor string instead of (or alongside) `LastSyncedAt`:
   - Add `SyncCursor` column (string, nullable) to whatever table stores the last sync checkpoint.
4. First sync: no cursor → server returns everything + first cursor. Store cursor.
5. Subsequent syncs: send cursor → get delta + next cursor. Update stored cursor.

**Test:** Sync once, note cursor value. Create a file on server. Sync again with cursor — only the new file should come back. Verify no issues with clock skew.

**Done when:** Sync uses monotonic sequence cursors. Timestamp-based sync still works as fallback.

**Side:** Both → **Server on `mint22` first, then client on `Windows11-TestDNC`. Use handoff doc.**

---

### Task 2.5: Paginated Change Responses

**What:** Large sync deltas are returned in pages instead of one giant response.

**Where:** Server: `SyncController`. Client: `SyncEngine`.

**How — Server:**
1. Add `limit` query param to `GET /api/v1/sync/changes` (default: 500, max: 5000).
2. Return response envelope:
   ```json
   {
     "changes": [...],
     "nextCursor": "...",
     "hasMore": true
   }
   ```
3. If more changes exist beyond the page, set `hasMore = true` and `nextCursor` to the last item's cursor.

**How — Client:**
1. In `SyncEngine.ApplyRemoteChangesAsync()`, loop:
   ```csharp
   string? cursor = GetStoredCursor();
   bool hasMore;
   do
   {
       var response = await _apiClient.GetChangesSinceAsync(cursor, limit: 500);
       await ProcessChanges(response.Changes);
       cursor = response.NextCursor;
       SaveCursor(cursor); // crash resilience — save after each page
       hasMore = response.HasMore;
   } while (hasMore);
   ```

**Test:** Create 1500 files on the server. Sync the client from scratch. Verify it makes 3 paginated calls (500 each).

**Done when:** Changes come in pages of 500. Client loops until all pages consumed. Cursor saved after each page.

**Side:** Both → **Server on `mint22` first, then client on `Windows11-TestDNC`. Use handoff doc.**

---

### Task 2.6: ETag / If-None-Match for Chunk Downloads

**What:** Avoid re-downloading chunks the client already has.

**Where:** Server: chunk download endpoint. Client: `ChunkedTransferClient`.

**How — Server:**
1. On `GET /api/v1/files/chunks/{hash}`:
   ```csharp
   response.Headers.ETag = new EntityTagHeaderValue($"\"{hash}\"");
   if (request.Headers.IfNoneMatch.Any(e => e.Tag == $"\"{hash}\""))
       return StatusCode(304);
   ```

**How — Client:**
1. Before downloading a chunk, check if it exists locally (in cache or local chunk store):
   ```csharp
   request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue($"\"{chunkHash}\""));
   var response = await _httpClient.SendAsync(request);
   if (response.StatusCode == HttpStatusCode.NotModified)
   {
       // Use local copy, skip download
       return localChunkData;
   }
   ```

**Test:** Download a file. Trigger a re-sync of the same file. Verify chunks return 304 (check logs for "chunk already cached" or similar).

**Done when:** Server returns 304 for chunks the client already has. No unnecessary re-downloads.

**Side:** Both → **Server on `mint22` first, then client on `Windows11-TestDNC`. Use handoff doc.**

---

## Batch 3 — User Experience (Do Third)

### Task 3.1: .syncignore with UI Support

**What:** Allow users to ignore files/patterns from sync. Ship with built-in defaults for OS junk and temp files.

**Where:** Client Core: new `SyncIgnoreParser` class. Client SyncTray: new settings panel.

**How — Core logic:**
1. Add NuGet package: `Microsoft.Extensions.FileSystemGlobbing`
2. Create `SyncIgnoreParser` class:
   - Constructor: takes list of built-in patterns + path to user `.syncignore` file
   - Built-in patterns (always active, hardcoded):
     ```
     .DS_Store, Thumbs.db, desktop.ini, *.swp, *~, *.tmp, *.temp, ~$*,
     .git/, .svn/, .hg/, node_modules/, .npm/, .yarn/, .pnp.*, packages/, .nuget/,
     .directory
     ```
   - Parse `.syncignore` from sync root (same format as `.gitignore`: `*`, `?`, `**`, `!` for negation, `#` for comments, `/` suffix for directories)
   - Method: `bool IsIgnored(string relativePath)` — returns true if the path matches any rule
   - User rules override built-in (e.g., `!*.tmp` would un-ignore `.tmp` files)
3. In `SyncEngine`, call `IsIgnored(relativePath)` before:
   - Queuing an upload from a FileSystemWatcher event
   - Applying a remote change (downloading)
   - Including a file in periodic scan comparisons

**How — UI:**
1. New panel in Settings: "Ignored Files"
2. Show two sections:
   - "System defaults" — gray/italic, not editable, show the built-in list
   - "Your rules" — editable list from `.syncignore`, with Add/Remove buttons
3. "Test a path" input field: user types a path, shows "✓ Ignored by rule: `*.tmp`" or "✗ Not ignored"
4. Save button writes to `.syncignore` in sync root (this file IS synced to other clients)

**Test:** Add `*.log` to `.syncignore`. Create a `.log` file in the sync folder. Verify it is NOT uploaded to the server.

**Done when:** `.syncignore` works with gitignore-style patterns. Built-in defaults skip OS junk. UI lets users manage rules.

**Side:** Client only → **Build on `Windows11-TestDNC` (Windows)**

---

### Task 3.2: Persistent Upload Sessions

**What:** If the client crashes during upload, resume where it left off instead of starting over.

**Where:** Client Core: `LocalStateDb` + `ChunkedTransferClient` + `SyncEngine`.

**How:**
1. Create `ActiveUploadSessionRecord` entity in `LocalStateDb`:
   ```csharp
   public class ActiveUploadSessionRecord
   {
       public int Id { get; set; }
       public string ServerSessionId { get; set; } = null!;
       public Guid NodeId { get; set; }
       public string FilePath { get; set; } = null!;
       public long FileSize { get; set; }
       public int TotalChunks { get; set; }
       public int UploadedChunks { get; set; }
       public DateTime StartedAt { get; set; }
       public DateTime LastActivityAt { get; set; }
   }
   ```
2. In `ChunkedTransferClient.UploadAsync()`:
   - After `InitiateUploadAsync()`: save session to `ActiveUploadSessionRecord`
   - After each chunk uploads: update `UploadedChunks` and `LastActivityAt`
   - After `CompleteUploadAsync()`: delete the record
   - On failure: leave the record in place
3. On `SyncEngine.StartAsync()`:
   - Query for incomplete `ActiveUploadSessionRecord` entries
   - For each: call server to check if session is still valid; if yes, resume from `UploadedChunks`; if expired, re-initiate
4. Clean up records older than 48 hours (server session TTL is 24h, give buffer).

**Test:** Start uploading a large file. Kill the sync service mid-upload. Restart it. Verify upload resumes from where it left off.

**Done when:** Upload progress survives crashes. On restart, incomplete uploads are resumed.

**Side:** Client only → **Build on `Windows11-TestDNC` (Windows)**

---

### Task 3.3: Locked File Handling

**What:** Handle files locked by other processes (e.g., Word documents) using a 4-tier approach.

**Where:** Client Core.

**How — Implement 4 tiers in order:**

**Tier 1: Shared-read open (try first, zero cost):**
```csharp
using var stream = new FileStream(filePath,
    FileMode.Open, FileAccess.Read,
    FileShare.ReadWrite | FileShare.Delete);
```
Replace ALL existing `File.OpenRead()` calls in the sync code with this.

**Tier 2: Retry with backoff (transient locks):**
If Tier 1 throws `IOException`:
```csharp
const int sharingViolationHResult = unchecked((int)0x80070020);
if (ex.HResult == sharingViolationHResult)
{
    // Retry up to 3 times, 2 seconds apart
}
```

**Tier 3: Volume Shadow Copy (Windows-only stubborn locks):**
1. Add NuGet package: `AlphaVSS` (Windows-only, MIT license)
2. Create `ILockedFileReader` interface:
   ```csharp
   public interface ILockedFileReader
   {
       Task<Stream?> TryReadLockedFileAsync(string path, CancellationToken ct);
   }
   ```
3. Create `VssLockedFileReader` (Windows): creates a VSS snapshot, reads the file from the shadow copy path.
4. Create `NoOpLockedFileReader` (Linux/macOS): always returns `null`.
5. Register via DI: `if (OperatingSystem.IsWindows()) services.AddSingleton<ILockedFileReader, VssLockedFileReader>(); else services.AddSingleton<ILockedFileReader, NoOpLockedFileReader>();`
6. Optimization: create ONE shadow copy per sync pass, use it for ALL locked files in that pass, dispose in `finally`.

**Tier 4: Defer (last resort):**
1. Add `Deferred` value to `SyncStateTag` enum.
2. If all tiers fail: mark file as deferred, log warning, show tray notification: "Skipped syncing {filename} — file is in use."
3. On next sync pass: retry deferred files from Tier 1.

**Linux note:** On Linux, Tier 1 almost always works (advisory locking). Add a consistency check: compare file size before and after read — if changed, defer.

**Test:** Open a Word document. Trigger sync. Verify the file syncs (via Tier 1 or Tier 3). Close Word. Verify deferred files retry.

**Done when:** Locked files handled gracefully with 4-tier fallback. Users see a notification for skipped files.

**Side:** Client only → **Build on `Windows11-TestDNC` (Windows)**

---

### Task 3.4: Per-File Transfer Progress in Tray UI

**What:** Show which files are syncing and their progress in the tray UI.

**Where:** Client SyncService (IPC events) + Client SyncTray (UI).

**How — SyncService:**
1. Wire `ChunkedTransferClient`'s `IProgress<TransferProgress>` to IPC event publishing.
2. Send `transfer-progress` IPC events with: `fileName`, `direction` ("upload"/"download"), `bytesTransferred`, `totalBytes`, `speedBps`.
3. Send `transfer-complete` IPC event when a file finishes.
4. Throttle progress events: max 2 per second per file (track last send time, skip if < 500ms).

**How — SyncTray:**
1. Create `ActiveTransfersViewModel`:
   - Observable collection of current transfers
   - Each item: file name, direction (↑/↓), progress percentage, speed, ETA
2. Show in the tray popup (expandable section) or a "Transfers" tab in Settings.
3. When a transfer completes: show it briefly (5 seconds), then remove from list.

**Test:** Upload a large file. Verify the tray UI shows the file name, progress bar, and speed.

**Done when:** Active transfers show in tray UI with progress bars. Completed transfers auto-dismiss after 5 seconds.

**Side:** Client only → **Build on `Windows11-TestDNC` (Windows)**

---

### Task 3.5: Conflict Resolution UI + Auto-Resolution Engine

This is a large task. Break it into sub-tasks:

#### Task 3.5a: ConflictRecord Entity

**What:** Create the database entity for tracking conflicts.

**Where:** `LocalStateDb` in Client Core.

**How:**
1. Create entity:
   ```csharp
   public class ConflictRecord
   {
       public int Id { get; set; }
       public string OriginalPath { get; set; } = null!;
       public string ConflictCopyPath { get; set; } = null!;
       public Guid NodeId { get; set; }
       public DateTime LocalModifiedAt { get; set; }
       public DateTime RemoteModifiedAt { get; set; }
       public DateTime DetectedAt { get; set; }
       public DateTime? ResolvedAt { get; set; }
       public string? Resolution { get; set; } // "kept-local", "kept-server", "kept-both", "merged", "auto-identical", "auto-fast-forward", "auto-merged", "auto-newer-wins", "auto-append", "auto-append-combined"
       public string? BaseContentHash { get; set; }
       public bool AutoResolved { get; set; }
   }
   ```
2. Add `DbSet<ConflictRecord>` to `LocalStateDbContext`.
3. In `ConflictResolver`, when creating a conflict copy: save a `ConflictRecord` and raise `ConflictDetected` event.
4. Add IPC commands: `list-conflicts` (returns all unresolved), `resolve-conflict` (takes ID + resolution).

**Done when:** Conflict records saved to DB. IPC commands work.

**Side:** Client only → **Build on `Windows11-TestDNC` (Windows)**

#### Task 3.5b: Auto-Resolution Pipeline

**What:** Automatically resolve conflicts when safe, running 5 strategies in order.

**Where:** Client Core — new `AutoConflictResolver` class.

**How:**
Create `AutoConflictResolver` with method `Task<bool> TryAutoResolveAsync(ConflictRecord conflict)`:

```
Strategy 1 (Identical): Compute SHA-256 of both files. If identical → resolved ("auto-identical").
Strategy 2 (Fast-forward): Compare local + server hashes against BaseContentHash.
  - If only one side changed → keep the changed one ("auto-fast-forward").
Strategy 3 (Clean text merge): For text files only, using DiffPlex library:
  - Three-way diff: base→local changes, base→server changes
  - If changes don't overlap → merge both into result ("auto-merged")
  - Requires base version from server version history
Strategy 4 (Newer wins): If same user on two devices, timestamps differ by > 5 minutes:
  - Keep newer version ("auto-newer-wins")
  - Only if setting "autoNewerWins" is true (default: true)
  - Does NOT apply to multi-user conflicts
Strategy 5 (Append-only): If one file is a prefix of the other:
  - Single-user: keep longer version ("auto-append")
  - Multi-user: only if shared prefix is ≥ 90% of shorter → combine appendages ("auto-append-combined")
```

Run strategies 1→2→3→4→5. First success stops the pipeline.

Add `DiffPlex` NuGet package for Strategy 3.

**Settings in `sync-settings.json`:**
```json
{
  "conflictResolution": {
    "autoNewerWins": true,
    "notificationSound": true,
    "reminderIntervalMinutes": 240
  }
}
```

**Done when:** Auto-resolver runs before user sees conflicts. Resolved conflicts logged with strategy name.

**Side:** Client only → **Build on `Windows11-TestDNC` (Windows)**

#### Task 3.5c: Conflict Notifications (Psychologically Loud)

**What:** Make conflicts impossible to ignore.

**Where:** Client SyncTray.

**How:**
1. **Tray icon change:** When unresolved conflicts > 0, replace the normal cloud icon with a warning variant (cloud + orange/yellow exclamation ⚠). Use `TrayIconManager` to swap the icon asset.
2. **Badge count:** Overlay the conflict count on the tray icon (red circle with number).
3. **Persistent toast:** On first conflict detection, show a non-auto-dismissing notification: "Sync conflict detected: `{filename}` — two versions exist. Click to resolve."
4. **Tooltip change:** "DotNetCloud — All files synced" → "DotNetCloud — ⚠ {n} unresolved conflicts"
5. **Tray menu:** Add "Conflicts ({n})" at the TOP of the context menu with warning icon (only shown when conflicts > 0).
6. **Recurring reminder:** If conflicts are unresolved > 24 hours, re-show toast once per day.
7. **First-conflict education:** On the very first conflict ever, show a longer notification explaining what a conflict is and how to resolve it.

**Done when:** Unresolved conflicts change the tray icon, show persistent notifications, and re-remind after 24 hours.

**Side:** Client only → **Build on `Windows11-TestDNC` (Windows)**

#### Task 3.5d: Conflicts Panel UI

**What:** A panel where users can see and resolve conflicts.

**Where:** Client SyncTray.

**How:**
1. New "Conflicts" panel accessible from tray menu or settings.
2. List unresolved conflicts with:
   - File name and path
   - "Local version" timestamp + size
   - "Server version" timestamp + size
   - Buttons: **Keep local**, **Keep server**, **Keep both**, **Merge** (text files only), **Open folder**
3. "History" tab: resolved conflicts from last 30 days.
4. "Keep local" → delete conflict copy, keep current file, mark `Resolution = "kept-local"`.
5. "Keep server" → replace current file with server version, mark `Resolution = "kept-server"`.
6. "Keep both" → leave both files, mark `Resolution = "kept-both"`.
7. "Open folder" → `Process.Start()` with platform-detected command (`explorer`, `xdg-open`, `open`).

**Done when:** Users can view and resolve conflicts from the tray UI.

**Side:** Client only → **Build on `Windows11-TestDNC` (Windows)**

#### Task 3.5e: Three-Pane Merge Editor

**What:** A visual merge editor for text file conflicts.

**Where:** Client SyncTray — new window.

**How:**
1. Add NuGet package: `DiffPlex` (MIT, pure .NET).
2. New window with 4 panes:
   - Left: "Local version" (read-only)
   - Center: "Base version" / common ancestor (read-only, if available)
   - Right: "Server version" (read-only)
   - Bottom: "Merged result" (editable, pre-populated with auto-merge attempt)
3. Diff engine: use `DiffPlex` for line-level diff. Color: green=added, red=removed, yellow=conflict.
4. Auto-merge non-conflicting hunks. Mark conflicts with `<<<<<<<` / `=======` / `>>>>>>>` markers.
5. Interactions:
   - Click a hunk → apply that version to merge result
   - "Accept all local" / "Accept all server" buttons
   - "Reset merge" button
   - "Save & resolve" button → write merged result, mark `Resolution = "merged"`
   - "Cancel" → return to conflict list
6. Text file types that open in the merge editor: `.txt`, `.md`, `.json`, `.yaml`, `.yml`, `.csv`, `.tsv`, `.html`, `.css`, `.js`, `.ts`, `.cs`, `.py`, `.java`, `.c`, `.cpp`, `.h`, `.sh`, `.ps1`, `.sql`, `.ini`, `.cfg`, `.conf`, `.toml`, `.env`, `.log`, `.gitignore`, `.dockerignore`
7. Binary files (images, Office docs, PDFs, archives, media): show only Keep/Both buttons, no merge editor.
8. Window size: ~80% of screen, resizable, separate window (not embedded in settings).

**For XML files** (`.xml`, `.csproj`, `.fsproj`, `.props`, `.targets`, `.xaml`, `.svg`, `.xslt`):
1. Add NuGet package: `Microsoft.XmlDiffPatch`
2. Use tree-based diffing instead of line-based: parse into DOM, diff nodes/attributes.
3. Show conflicting nodes in a tree view alongside text view.
4. Post-merge validation: `XDocument.Parse()` — block save if invalid XML.
5. Show an in-editor help panel ("How XML merging works") on first XML merge.

**Done when:** Three-pane merge editor works for text files. XML files use tree-based diffing. Binary files only show Keep/Both buttons.

**Side:** Client only → **Build on `Windows11-TestDNC` (Windows)**

---

### Task 3.6: Idempotent Operations

**What:** Avoid creating duplicate versions when a crash happens after upload but before local DB update.

**Where:** `SyncEngine` in Client Core.

**How:**
1. In `SyncEngine.ApplyLocalChangesAsync()`, before executing an upload for an existing file:
   ```csharp
   if (pendingUpload.NodeId is Guid nodeId)
   {
       var serverNode = await _apiClient.GetNodeAsync(nodeId);
       var localHash = await ContentHasher.ComputeHashAsync(filePath);
       if (serverNode.ContentHash == localHash)
       {
           _logger.LogInformation("Skipped upload — server already has this version. File={File}", filePath);
           // Update LocalStateDb, skip upload
           continue;
       }
   }
   ```

**Test:** Upload a file. Simulate a crash (kill process after upload completes but before DB update). Restart. Verify no duplicate version is created.

**Done when:** Pre-upload hash check prevents duplicate versions.

**Side:** Client only → **Build on `Windows11-TestDNC` (Windows)**

---

## Batch 4 — Cross-Platform Hardening (Do Fourth)

### Task 4.1: Case-Sensitivity Conflict Detection

**What:** Prevent case-insensitive name collisions (e.g., `Report.docx` and `report.docx`).

**Where:** Server: `FileService`, `ChunkedUploadService`. Client: `SyncEngine`.

**How — Server:**
1. In `FileService.CreateFolderAsync()`, `RenameAsync()`, and `ChunkedUploadService.CompleteUploadAsync()`:
   ```csharp
   var existing = await _db.FileNodes
       .Where(n => n.ParentId == parentId && n.Id != currentId)
       .Where(n => EF.Functions.ILike(n.Name, newName)) // case-insensitive
       .FirstOrDefaultAsync();
   if (existing != null)
       return Conflict($"A file with a case-insensitively matching name '{existing.Name}' already exists.");
   ```
2. Make configurable: `"FileSystem:EnforceCaseInsensitiveUniqueness": true` in `appsettings.json` (default: true).

**How — Client:**
1. Before applying remote changes, check if the incoming filename matches an existing file case-insensitively but not case-sensitively.
2. If conflict on Windows/macOS: rename incoming file with `(case conflict)` suffix.
3. Use `StringComparer.OrdinalIgnoreCase` on Windows/macOS, `StringComparer.Ordinal` on Linux.

**Done when:** Server rejects case-insensitive duplicates. Client handles collisions gracefully.

**Side:** Both → **Server on `mint22` first, then client on `Windows11-TestDNC`. Use handoff doc.**

---

### Task 4.2: File Permission Metadata Sync

**What:** Preserve POSIX file permissions (especially the execute bit) when syncing between Linux clients.

**Where:** Server: `FileNode` model + DTOs. Client: upload/download code.

**How — Server:**
1. Add to `FileNode` (EF migration):
   - `PosixMode` — `int?` (nullable) — stores `UnixFileMode` bitmask
   - `PosixOwnerHint` — `string?` (nullable) — stores `"user:group"` as hint
2. Add `PosixMode` to `FileVersion` as well (per-version permission tracking).
3. Include both in all DTOs: `FileNodeDto`, `SyncChangeDto`, `SyncTreeNodeDto`, gRPC `FileNodeInfo`.
4. **Preservation rule:** when a Windows client uploads a new version of a file that had `PosixMode` set, copy the previous version's `PosixMode` to the new version.

**How — Client (Linux):**
1. On upload: `var mode = File.GetUnixFileMode(filePath);` → send as `PosixMode`.
2. On download: `File.SetUnixFileMode(filePath, (UnixFileMode)posixMode);`
   - If `PosixMode` is null (from Windows): use default `0o644` for files, `0o755` for directories.
3. setuid/setgid bits: store in DB but do NOT apply on download by default (log an info message).
4. Detect permission-only changes in periodic scan: compare current mode vs stored mode → queue metadata-only sync.

**How — Client (Windows):**
1. On upload: send `PosixMode = null`, `PosixOwnerHint = null`.
2. On download: ignore both fields entirely.

**Done when:** Execute bits survive Linux→server→Linux round-trips. Windows doesn't crash or break on permission fields.

**Side:** Both → **Server on `mint22` first, then client on `Windows11-TestDNC`. Use handoff doc.**

---

### Task 4.3: Symbolic Link Policy

**What:** Ignore symlinks by default. Optionally sync them as lightweight metadata.

**Where:** `SyncEngine` in Client Core. Server: `FileNode` model.

**How:**
1. **Default (ignore):** In `SyncEngine`, before processing any file:
   ```csharp
   var info = new FileInfo(path);
   if (info.LinkTarget != null) // .NET 7+ — detects symlinks
   {
       _logger.LogInformation("Skipped symbolic link: {Path} → {Target}", path, info.LinkTarget);
       continue;
   }
   ```
   Also check `FileAttributes.ReparsePoint` on Windows.
2. **Opt-in (sync-as-link):** configurable in `sync-settings.json`:
   ```json
   { "symlinks": { "mode": "ignore" } }
   ```
   Options: `"ignore"` (default), `"sync-as-link"`.
3. When `"sync-as-link"`:
   - Server: add `NodeType.SymbolicLink` enum value, add `LinkTarget` column (nullable string) on `FileNode` (EF migration).
   - Client upload: send link target as metadata only (no content/chunks).
   - Client download: `File.CreateSymbolicLink(path, target)` on Linux. On Windows, only if running with admin/developer mode privileges.
   - Only sync **relative** symlinks that stay within the sync root. Reject absolute links and links that escape the root.
4. Settings UI: dropdown in Settings → Sync: "Symbolic links" → "Ignore (default)" / "Sync as links".

**Done when:** Symlinks ignored by default. Optional sync-as-link mode works.

**Side:** Both → **Server on `mint22` first (NodeType + LinkTarget migration), then client on `Windows11-TestDNC`. Use handoff doc.**

---

### Task 4.4: inotify Watch Limits + inode Monitoring (Linux)

**What:** Detect and auto-fix Linux inotify watch limits. Monitor inode usage.

**Where:** Client: `SyncEngine`. Server: startup + health checks.

**How — Client (Linux only):**
1. On `SyncEngine.StartAsync()`, if `OperatingSystem.IsLinux()`:
   ```csharp
   var maxWatches = int.Parse(File.ReadAllText("/proc/sys/fs/inotify/max_user_watches").Trim());
   var subdirCount = Directory.GetDirectories(syncRoot, "*", SearchOption.AllDirectories).Length;
   var target = Math.Max(524288, (int)(subdirCount * 1.5));
   
   if (maxWatches < target)
   {
       // Show notification: "File watching needs more resources. [Fix automatically] [Dismiss]"
       // "Fix automatically" → write sysctl.d config, run sysctl --system (needs sudo/polkit)
   }
   ```
2. If `FileSystemWatcher` raises `Error` event: fall back to fast periodic scan (30 seconds).
3. inode check:
   ```csharp
   // Parse output of: df -i {syncRoot} (or use statvfs P/Invoke)
   // If free inodes < 5%: show warning notification
   // If free inodes < 1%: show critical notification
   ```

**How — Server (Linux only):**
1. On startup: read inotify limits, log them, warn if low.
2. inode check on startup + every 30 minutes:
   - healthy: free inodes ≥ 10%
   - degraded: free inodes 2–10%
   - unhealthy: free inodes < 2%
3. Include inode status in `/health/ready` endpoint.

**Done when:** Client detects low inotify limits and offers auto-fix. Falls back to polling if limits hit. Server monitors inodes in health checks.

**Side:** Both (Linux-specific) → **Server on `mint22`, then client on `mint-dnc-client` (Linux client). Windows client: no-op (skip inotify/inode code via `OperatingSystem.IsLinux()`).**

---

### Task 4.5: Path Length + Filename Limit Handling

**What:** Handle Windows 260-char path limit and cross-platform filename restrictions.

**Where:** Client (Windows): startup + download code. Client (Linux): download code. Server: file creation/rename validation.

**How — Client (Windows):**
1. Add `<longPathAware>true</longPathAware>` to the app manifest.
2. On first run, check registry `HKLM\SYSTEM\CurrentControlSet\Control\FileSystem\LongPathsEnabled`:
   - If `0`: show notification "Windows limits paths to 260 chars. [Enable long paths] [Dismiss]"
   - "Enable long paths" → UAC elevation → set registry `LongPathsEnabled = 1`
3. Before writing files with paths > 259 chars:
   - Try `\\?\` prefix.
   - If that fails: mark file as `SyncStateTag.PathTooLong`, log warning, notify user.

**How — Client (Linux):**
1. Before writing files, check filename byte length:
   ```csharp
   if (Encoding.UTF8.GetByteCount(filename) > 255)
   {
       // Truncate preserving extension and UTF-8 validity
       // Append ~{4-char-hash}.{ext} to avoid collisions
   }
   ```

**How — Server:**
1. On file creation/rename, validate:
   - Filename > 255 chars → reject with 400
   - Path > 250 chars → accept but add response header `X-Path-Warning: path-length-exceeds-windows-limit`
   - Filename contains `\ / : * ? " < > |` or control chars → reject with 400: "Filename contains characters not supported on all platforms"
   - Filename matches reserved names (`CON`, `PRN`, `AUX`, `NUL`, `COM1`-`COM9`, `LPT1`-`LPT9`, case-insensitive) → reject with 400
2. Configurable threshold: `"FileSystem:MaxPathWarningThreshold": 250` in `appsettings.json`.

**Done when:** Windows long paths enabled or gracefully handled. Linux byte-length filenames handled. Server rejects invalid characters and reserved names.

**Side:** Both → **Server on `mint22` first (validation rules), then client on `Windows11-TestDNC` (long path handling). Use handoff doc.**

---

## Batch 5 — Polish (Do Last)

### Task 5.1: Bandwidth Throttling

**What:** Implement the upload/download speed limit settings that already exist in the UI.

**Where:** Client Core.

**How:**
1. Create `ThrottledStream` class:
   ```csharp
   public class ThrottledStream : Stream
   {
       private readonly Stream _inner;
       private readonly long _bytesPerSecond;
       private long _bytesTransferred;
       private DateTime _windowStart = DateTime.UtcNow;

       // On Read/Write: track bytes, if exceeding rate, Task.Delay() to throttle
   }
   ```
   Use a token bucket algorithm: refill `_bytesPerSecond` tokens each second, consume on read/write, sleep when depleted.
2. Create `ThrottledHttpHandler : DelegatingHandler`:
   ```csharp
   protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
   {
       // Wrap request content stream in ThrottledStream (upload throttle)
       var response = await base.SendAsync(request, ct);
       // Wrap response content stream in ThrottledStream (download throttle)
       return response;
   }
   ```
3. Read limits from `SyncContext` config (`UploadLimitKbps`, `DownloadLimitKbps`). Value of 0 = unlimited.
4. Wire into `HttpClientFactory` setup in `SyncContextManager.CreateEngine()`.
5. Settings UI already has the input fields — wire them to save/load from `SyncContext` config.

**Test:** Set download limit to 100 KB/s. Download a 10MB file. Verify it takes ~100 seconds.

**Done when:** Upload and download speed limits work. 0 = unlimited.

**Side:** Client only → **Build on `Windows11-TestDNC` (Windows)**

---

### Task 5.2: Selective Sync Folder Browser

**What:** Visual treeview for choosing which server folders to sync locally.

**Where:** Client SyncTray.

**How:**
1. Create `FolderBrowserViewModel`:
   - Fetch folder tree from `GET /api/v1/sync/tree`
   - Present as tree with checkboxes (checked = sync, unchecked = exclude)
   - Support partial/indeterminate check state for mixed children
   - Lazy-load children on expand (for performance)
2. Create `FolderBrowserView` using Avalonia `TreeView` with `CheckBox` template.
3. Accessible from:
   - Add-account flow (after authentication, before first sync)
   - Settings → account → "Choose folders to sync"
4. Save selections as `SelectiveSyncConfig` rules.
5. When selections change: delete locally excluded files, download newly included files.

**Test:** Uncheck a folder. Verify its files are deleted locally. Re-check it. Verify files are re-downloaded.

**Done when:** Users can browse and check/uncheck folders to include/exclude from sync.

**Side:** Client only → **Build on `Windows11-TestDNC` (Windows)**

---

## Future Phase — Virtual Filesystem (Not Now)

> **Do NOT implement this yet.** This is documented for awareness only. Implement Batches 1–5 first.

**Concept:** Files show in the filesystem but are only downloaded from the server when opened.
- **Windows:** Use Cloud Files API (`cfapi`) — same as OneDrive. Files show as placeholders with cloud icons.
- **Linux:** Use FUSE — custom filesystem that fetches content on `read()`. This would make DotNetCloud unique among cloud sync clients (Nextcloud doesn't support this on Linux).
- **macOS:** Use File Provider framework. Left for future contributor.

**Key architectural note:** Nothing in Batches 1–5 should prevent this feature. Specifically:
- `LocalStateDb` metadata cache → reused for virtual FS placeholder metadata
- Content-addressed chunks → natural cache for on-demand hydration
- Cursor-based delta sync → efficient metadata updates for placeholder refresh
- `ChunkedTransferClient` → reused for on-demand downloads

---

## Implementation Order Summary

```
Batch 1 (Foundation):
  1.1  → Sync service logging (client)
  1.1b → Sync audit logging (server)
  1.2  → Request correlation IDs
  1.3  → Server rate limiting
  1.4  → Chunk integrity verification
  1.5  → Per-chunk retry with backoff
  1.6  → SQLite WAL mode + corruption recovery
  1.7  → Operation retry queue with backoff
  1.8  → Secure temp file handling
  1.9  → File scanning interface + execution prevention

Batch 2 (Efficiency):
  2.1  → Content-defined chunking (CDC)
  2.2  → Streaming chunk pipeline
  2.3  → Compression for chunk transfers
  2.4  → Server-issued sync cursor
  2.5  → Paginated change responses
  2.6  → ETag / If-None-Match for chunks

Batch 3 (User Experience):
  3.1  → .syncignore with UI
  3.2  → Persistent upload sessions
  3.3  → Locked file handling
  3.4  → Per-file transfer progress
  3.5a → ConflictRecord entity
  3.5b → Auto-resolution pipeline
  3.5c → Conflict notifications
  3.5d → Conflicts panel UI
  3.5e → Three-pane merge editor
  3.6  → Idempotent operations

Batch 4 (Cross-Platform):
  4.1  → Case-sensitivity conflict detection
  4.2  → File permission metadata sync
  4.3  → Symbolic link policy
  4.4  → inotify + inode monitoring
  4.5  → Path length + filename limits

Batch 5 (Polish):
  5.1  → Bandwidth throttling
  5.2  → Selective sync folder browser
```

## General Rules

1. **Build and test after every task:** `dotnet build && dotnet test`
2. **Platform checks:** Always use `OperatingSystem.IsWindows()` / `OperatingSystem.IsLinux()` / `OperatingSystem.IsMacOS()` for platform-specific code.
3. **Path handling:** Always use `Path.Combine()`, never hardcode `\` or `/`.
4. **All file types sync:** This is a backup system. Never block file extensions or MIME types.
5. **Never fail on OS limits:** If a system limit is hit (inotify, inodes, path length), degrade gracefully — don't crash.
6. **Server does both-side tasks first:** When a task says "Both," implement the server part first (it defines the contract), write the handoff, then the client part. **Complete each task fully (both sides) before starting the next task.** Do not batch all server work together — handoffs are per-task, not per-batch.
7. **Update docs after each task:** Mark deliverables `✓` in `SYNC_IMPROVEMENT_PLAN.md`.
8. **Max file size:** 15GB (16,106,127,360 bytes).
9. **Log rotation:** 30-day retention, daily rolling files.
10. **Auto-resolution settings default:** `autoNewerWins: true`, `notificationSound: true`, `reminderIntervalMinutes: 240`.
