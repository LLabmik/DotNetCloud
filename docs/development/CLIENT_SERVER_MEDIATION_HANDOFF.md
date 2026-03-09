# Client/Server Mediation Handoff

Last updated: 2026-03-09

Purpose: Shared handoff between client-side and server-side agents, mediated by user.

> Archived context (42 resolved issues — initial sync milestone through Batch 4.2) moved to
> [CLIENT_SERVER_MEDIATION_ARCHIVE.md](CLIENT_SERVER_MEDIATION_ARCHIVE.md).
> Full git history in commits up to `c70bd47`.

## Process Rules

- All technical findings and debugging conclusions go in this document, pushed to `main`.
- Mediator role is relay-only — commit notifications and cross-agent request forwarding.

## Current Status

**Issues #1–#42 fully resolved.** See [CLIENT_SERVER_MEDIATION_ARCHIVE.md](CLIENT_SERVER_MEDIATION_ARCHIVE.md) for details.

**Batch 4 remaining — server-side complete, client-side pending:**
- Issue #43 (Task 4.3): Symbolic link policy — server ✅ complete, client ☐ pending
- Issue #44 (Task 4.4): inotify/inode health monitoring — server ✅ complete, client ☐ pending
- Issue #45 (Task 4.5): Path length/filename validation — server ✅ complete, client ☐ pending

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

### Issue #43: Batch 4 Task 4.3 — Symbolic Link Policy

**Server-side status:** ✅ COMPLETE — commit `d3a6422` (2026-03-09) includes:
- `FileNodeType.SymbolicLink = 2` added to the enum
- `LinkTarget string?` property on `FileNode` model
- `LinkTarget` propagated through `FileNodeDto`, `SyncChangeDto`, `SyncTreeNodeDto`
- `files_service.proto` `FileNodeMessage` has `string link_target = 16`
- `FilesGrpcService.ToMessage()` and `FileService.ToDto()` include `LinkTarget`
- All `SyncChangeDto`/`SyncTreeNodeDto` constructions in `SyncService` set `LinkTarget`
- EF migration `AddSymlinkSupport` applied on server start

**Client-side status:** ☐ PENDING

---

#### What to implement (client):

**Step 1 — Add `LinkTarget` to client API response models**

**File:** `src/Clients/DotNetCloud.Client.Core/Api/ApiModels.cs`

Add to `FileNodeResponse`, `SyncChangeResponse`, `SyncTreeNodeResponse`:
```csharp
/// <summary>Relative target path for symbolic link nodes. Null for files and folders.</summary>
public string? LinkTarget { get; init; }
```

**Step 2 — Add `LinkTarget` to `PendingDownload` and `LocalFileRecord`**

If `PendingDownload` or a download context carries node metadata, add `LinkTarget string?` so the sync engine can use it during materialisation.

Add `LinkTarget string?` to `LocalFileRecord` and update `LocalStateDb` schema migration:
```sql
ALTER TABLE FileRecords ADD COLUMN LinkTarget TEXT NULL;
```

**Step 3 — Handle `SymbolicLink` NodeType in `SyncEngine`**

When processing a download where `change.NodeType == "SymbolicLink"`:
- Do **not** attempt content download (symlinks have no bytes)
- Call `File.CreateSymbolicLink(localPath, change.LinkTarget)` (cross-platform in .NET 6+)
- If the target doesn't exist, create the symlink anyway (dangling symlinks are valid)
- Store in `LocalFileRecord` with `LinkTarget = change.LinkTarget`

When processing an upload where the local path is a symlink:
- Detect with `File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)` (Windows) or check `fileInfo.LinkTarget != null` (.NET 6+)
- Read `fileInfo.LinkTarget` (relative path)
- Send a create/update with `nodeType = "SymbolicLink"`, `linkTarget = <target>`, `totalSize = 0`, no chunks
- **Security:** Do NOT follow the symlink — never upload the pointed-to content as the symlink's data

**Step 4 — Skip chunk upload/download for symlinks in `ChunkedTransferClient`**

Guard at the start of `UploadAsync`: if `localPath` is a symlink, return the existing node ID (or create it via a metadata-only endpoint if one exists). For now, the simplest approach is to call `InitiateUploadAsync` with zero chunks and immediately call `CompleteUploadAsync` (no actual chunk transfer needed for zero-size content).

**Build and test:**
```powershell
dotnet build src\Clients\DotNetCloud.Client.Core\DotNetCloud.Client.Core.csproj
dotnet test tests\DotNetCloud.Client.Core.Tests\
```

**New tests to add:**
1. `SyncAsync_Download_SymlinkNode_CreatesLocalSymlink` — mock API returns `SyncChangeResponse` with `NodeType = "SymbolicLink"` and `LinkTarget = "Documents/readme.md"`; verify `File.ResolveLinkTarget(localPath, false)` equals the expected relative path.
2. `SyncAsync_Upload_LocalSymlink_SendsSymlinkMetadata` — create a local symlink file; verify `InitiateUploadAsync` is called with `nodeType = "SymbolicLink"` and `linkTarget` matching the local link target.

**⚠️ PROCESS NOTE:**
1. Pull latest (`git pull`) before starting — server commit `d3a6422` is on `main`.
2. `File.CreateSymbolicLink` requires elevated permissions on Windows by default (Developer Mode or admin). On Linux no special permissions needed. Gate Windows symlink creation to avoid failures on machines without Dev Mode.
3. After committing, update this document and mark ✅ COMPLETE.
4. Use **targeted edits only**.

---

### Issue #44: Batch 4 Task 4.4 — inotify/inode Health Monitoring

**Server-side status:** ✅ COMPLETE — commit `d3a6422` (2026-03-09) includes:
- `LinuxResourceHealthCheck` (`IHealthCheck`):
  - Reads `/proc/sys/fs/inotify/max_user_watches` — warns if below 65536
  - Uses `statvfs()` P/Invoke to check inode availability on the data directory mount
  - Returns `Healthy`/`Degraded`/`Unhealthy` with descriptive messages; no-op on non-Linux
- `LinuxResourceMonitorService` (`BackgroundService`):
  - Logs inotify limit and inode status at startup and every 30 minutes
  - No-op on non-Linux platforms
- Both registered in `Program.cs` (health check endpoint tag `"ready"`)
- Registered in `appsettings.json` under `FileSystem.MaxPathWarningThreshold` / `EnforceWindowsFilenameCompatibility`

**Client-side status:** ☐ PENDING

---

#### What to implement (client):

**Step 1 — Check inotify limit on Linux startup**

**File:** `src/Clients/DotNetCloud.Client.SyncService/LinuxStartup.cs` (new file) or inline in the sync service startup.

```csharp
if (OperatingSystem.IsLinux())
{
    const int MinWatches = 65536;
    try
    {
        var raw = File.ReadAllText("/proc/sys/fs/inotify/max_user_watches").Trim();
        if (int.TryParse(raw, out int limit) && limit < MinWatches)
        {
            logger.LogWarning(
                "inotify.watch_limit_low {Limit} recommendation={Recommended} " +
                "Fix: echo 'fs.inotify.max_user_watches={Recommended}' | sudo tee /etc/sysctl.d/50-dotnetcloud.conf && sudo sysctl --system",
                limit, MinWatches);
        }
    }
    catch (Exception ex)
    {
        logger.LogDebug(ex, "Could not read inotify watch limit.");
    }
}
```

**Step 2 — Fall back to periodic scan when inotify limit is low**

In `SyncEngine` (or wherever `FileSystemWatcher` is created), check if the inotify limit could exhaust: if the sync root has many files AND the limit is low, emit a warning and optionally switch to polling mode:
```csharp
// If we cannot create a FileSystemWatcher (catches IOException from exhausted watches),
// fall back to a 30-second polling interval:
try { _watcher = CreateWatcher(syncRoot); }
catch (IOException ex) when (OperatingSystem.IsLinux())
{
    _logger.LogWarning(ex, "Could not create FileSystemWatcher — inotify limit likely exhausted. Falling back to polling.");
    _pollingFallback = true;
}
```

**Step 3 — Inode check on Linux startup (optional)**

Statfs P/Invoke (same as server-side). If inode availability is below 10%, log a warning. This is low priority — can be deferred.

**Build and test:**
```powershell
dotnet build src\Clients\DotNetCloud.Client.SyncService\DotNetCloud.Client.SyncService.csproj
dotnet test tests\DotNetCloud.Client.SyncService.Tests\
```

**New tests to add:**
1. `SyncService_Startup_LogsInotifyWarning_WhenLimitLow` — mock file read from `/proc/sys/fs/inotify/max_user_watches` with value 100; verify warning logged. Gate on Linux.
2. `SyncEngine_WatcherCreationFails_FallsBackToPolling` — throw `IOException` from `FileSystemWatcher` constructor; verify polling mode activated.

**⚠️ PROCESS NOTE:**
1. Pull latest before starting. Server health check is transparent to the client.
2. The client-side work here is modest — mainly startup logging and the watcher fallback.
3. After committing, update this document and mark ✅ COMPLETE.

---

### Issue #45: Batch 4 Task 4.5 — Path Length and Filename Compatibility Validation

**Server-side status:** ✅ COMPLETE — commit `d3a6422` (2026-03-09) includes:
- `FileSystemOptions` now has:
  - `MaxPathWarningThreshold = 250` — filename length above which `X-Path-Warning` is returned
  - `EnforceWindowsFilenameCompatibility = true` — rejects Windows-illegal chars and reserved names
- `FileService.ValidateFilenameCompatibility(name, options)` (internal static):
  - Rejects control characters and `\/:*?"<>|` (Windows illegal chars)
  - Rejects reserved device names: `CON`, `PRN`, `AUX`, `NUL`, `COM1`–`COM9`, `LPT1`–`LPT9` (case-insensitive, extension-stripped)
  - Throws `ValidationException` with field `"Name"` and descriptive message
- Called in `FileService.CreateFolderAsync()` and `FileService.RenameAsync()`
- Called in `ChunkedUploadService.InitiateUploadAsync()` for uploaded filenames
- Both `DotNetCloud.Core.Server.Controllers.FilesController` and `DotNetCloud.Modules.Files.Host.Controllers.FilesController`:
  - `InitiateUploadAsync`: if `dto.FileName.Length > _fileSystemOptions.MaxPathWarningThreshold`, sets `X-Path-Warning: path-length-exceeds-windows-limit` response header

**Client-side status:** ☐ PENDING

---

#### What to implement (client):

**Step 1 — Read and act on `X-Path-Warning` response header**

**File:** `src/Clients/DotNetCloud.Client.Core/Api/DotNetCloudApiClient.cs`

After `InitiateUploadAsync` receives a response, check for the header:
```csharp
if (response.Headers.TryGetValues("X-Path-Warning", out var vals)
    && vals.Contains("path-length-exceeds-windows-limit"))
{
    _logger.LogWarning("upload.path_length_warning FileName={FileName} Length={Length}",
        fileName, fileName.Length);
}
```

Optionally surface this to the sync engine as a warning (not an error — the upload should still proceed).

**Step 2 — Windows long-path fallback with `\\?\` prefix**

**File:** `src/Clients/DotNetCloud.Client.SyncService/SyncEngine.cs` (or the file I/O helpers)

When a `PathTooLongException` is thrown while reading/writing a file on Windows, retry with the `\\?\` extended-path prefix:
```csharp
private static string ToWindowsLongPath(string path)
{
    if (!OperatingSystem.IsWindows() || path.StartsWith(@"\\?\"))
        return path;
    return @"\\?\" + Path.GetFullPath(path);
}
```

Use `ToWindowsLongPath(localPath)` in `FileStream` opens and `File.WriteAllBytesAsync` calls.

**Step 3 — Track `SyncStateTag.PathTooLong` for files that exceed OS limits**

If a download or upload fails with `PathTooLongException` after the long-path retry, mark the local file record with a `PathTooLong` state instead of `Failed`:
```csharp
catch (PathTooLongException)
{
    await _stateDb.SetSyncStateAsync(stateDatabasePath, localPath, SyncStateTag.PathTooLong, cancellationToken);
    _logger.LogWarning("sync.path_too_long {LocalPath}", localPath);
}
```

`SyncStateTag.PathTooLong` should cause the sync engine to skip the file on subsequent runs (rather than retry infinitely).

**Step 4 — UTF-8 byte-length check on Linux (optional)**

Linux filesystems enforce a 255-byte limit per filename component (not character count). A filename with many multi-byte characters (e.g. CJK) can fail. Add a pre-upload check:
```csharp
var byteLen = System.Text.Encoding.UTF8.GetByteCount(fileName);
if (byteLen > 255)
{
    _logger.LogWarning("upload.filename_too_long_utf8 {FileName} bytes={Bytes}", fileName, byteLen);
    // Either skip or truncate — for now, log and skip with an error state
}
```

**Step 5 — Enable long-path support via Windows registry (optional auto-setup)**

On Windows, log a hint if `PathTooLongException` is hit AND the registry key `HKLM\SYSTEM\CurrentControlSet\Control\FileSystem\LongPathsEnabled` is not set to `1`:
```csharp
if (OperatingSystem.IsWindows())
    _logger.LogWarning("Windows long-path support is not enabled. Set HKLM\\SYSTEM\\CurrentControlSet\\Control\\FileSystem\\LongPathsEnabled=1 (requires admin, then reboot).");
```

**Build and test:**
```powershell
dotnet build src\Clients\DotNetCloud.Client.Core\DotNetCloud.Client.Core.csproj
dotnet build src\Clients\DotNetCloud.Client.SyncService\DotNetCloud.Client.SyncService.csproj
dotnet test tests\DotNetCloud.Client.Core.Tests\
dotnet test tests\DotNetCloud.Client.SyncService.Tests\
```

**New tests to add:**
1. `InitiateUploadAsync_PathWarningHeader_LogsWarning` — mock `HttpMessageHandler` that returns `X-Path-Warning: path-length-exceeds-windows-limit`; verify warning is logged.
2. `SyncEngine_PathTooLong_MarksFilePathTooLong` — mock download that throws `PathTooLongException`; verify `SyncStateTag.PathTooLong` stored in local DB.
3. `ToWindowsLongPath_NormalPath_PrependsPrefix` (Windows only) — simple unit test for the helper.

**⚠️ PROCESS NOTE:**
1. Pull latest before starting. Server rejects illegal chars/reserved names via `ValidationException` → 422 Unprocessable Entity. The client will receive this as an error response — handle gracefully (log + mark file as `NameConflict` or `InvalidName` state, do not retry).
2. The `X-Path-Warning` header is informational — do not block the upload on it.
3. After committing, update this document and mark ✅ COMPLETE.
4. Use **targeted edits only**.
