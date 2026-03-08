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

Open issue: Sync Improvement Batch 1 Task 1.1 (client logging) is code-complete but pending Windows11-TestDNC restore/build/runtime validation.

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
**Client-side status:** Awaiting implementation validation on `Windows11-TestDNC`.

**What already changed in repo (code is ready for Windows validation):**
- Updated `src/Clients/DotNetCloud.Client.SyncService/DotNetCloud.Client.SyncService.csproj`
	- Added packages: `Serilog.AspNetCore`, `Serilog.Sinks.File`, `Serilog.Formatting.Compact`
	- Added `sync-settings.json` as content with `CopyToOutputDirectory=PreserveNewest`
- Updated `src/Clients/DotNetCloud.Client.SyncService/Program.cs`
	- Added Serilog rolling JSON file configuration
	- Added logging settings loader from `sync-settings.json`
	- Added Linux `600` file mode handling path (safe no-op on Windows)
- Updated logging in:
	- `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs`
	- `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkedTransferClient.cs`
	- `src/Clients/DotNetCloud.Client.Core/Conflict/ConflictResolver.cs`
	- `src/Clients/DotNetCloud.Client.Core/Auth/OAuth2Service.cs`
	- `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcClientHandler.cs`
	- `src/Clients/DotNetCloud.Client.SyncService/SyncWorker.cs`
- Added file:
	- `src/Clients/DotNetCloud.Client.SyncService/sync-settings.json`

**Client agent must run on Windows11-TestDNC (PowerShell, from repo root):**
```powershell
git pull
dotnet restore "src\Clients\DotNetCloud.Client.SyncService\DotNetCloud.Client.SyncService.csproj"
dotnet build "src\Clients\DotNetCloud.Client.SyncService\DotNetCloud.Client.SyncService.csproj"

# Run service once to create logs
dotnet run --project "src\Clients\DotNetCloud.Client.SyncService\DotNetCloud.Client.SyncService.csproj"
```

**Validation required on Windows:**
- Verify log file exists at `%APPDATA%\DotNetCloud\logs\sync-service.log`
- Verify JSON log entries appear for:
	- Sync pass start/complete/error
	- Upload/download start/complete/error
	- Conflict detection
	- OAuth token refresh success/failure
	- IPC commands received
	- FileSystemWatcher-triggered sync events

**Request back from Windows client agent:**
- commit hash
- raw restore/build errors (if any)
- first 10 lines of `%APPDATA%\DotNetCloud\logs\sync-service.log`
- confirmation of Task 1.1 acceptance criteria status (pass/fail)
