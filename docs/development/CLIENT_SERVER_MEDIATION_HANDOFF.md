# Client/Server Mediation Handoff

Last updated: 2026-03-23 (Desktop security fixes complete on mint-dnc-client; Windows validation handoff)

Purpose: shared handoff between client-side and server-side agents, mediated by user.

Archived context:
- Historical completed updates are in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Additional history remains available in git.

## Process Rules

**Agent autonomy (CRITICAL):**
- Both client and server agents work autonomously — they do NOT ask the moderator for context or permission.
- Agents pull the latest `main`, read the **Active Handoff** section, and execute the work described there independently.
- All actionable items, blockers, and technical details go directly in this document (committed to `main`).
- No moderator involvement in technical decisions, code reviews, or work coordination.

**Handoff management:**
- Put all technical findings, debugging conclusions, and next-step details in this document.
- Assistant (current agent) commits their findings/work and updates the **Active Handoff** section with actionable next steps for the other client.
- Assistant pushes commits to `main`.
- Unexpected untracked content rule (MANDATORY): remove unexpected untracked files/directories before commit; only keep intentional tracked changes for the handoff update.
- Handoff readiness gate (MANDATORY): all executable tests must pass before marking a handoff as ready.
- Environment-gated tests are allowed to be skipped, but must be explicitly identified as gated with the required environment/runtime prerequisites documented in the handoff.
- Runtime verification gate (MANDATORY): before declaring a server-side blocker fixed, verify the running service is on current binaries (not stale publish output) and document the verification command/output in handoff notes.
- OAuth contract check (MANDATORY when auth is involved): verify `client_id`, `redirect_uri`, and requested scopes exactly match server-registered OpenIddict client permissions before requesting cross-machine retries.
- Secret handling rule (MANDATORY): never commit raw bearer tokens/refresh tokens; share token acquisition steps and sanitized outputs only.
- Moderator relays a short "check for updates" message to the other machine.
- Moderator handoff prompt rule (MANDATORY): every ready-to-relay message must explicitly state the target machine name (for example: `mint22`, `mint-dnc-client`, `Windows11-TestDNC`).
- Other agent pulls latest, reads the handoff, and takes action without asking questions.

**Document maintenance:**
- Pre-commit archive rule (MANDATORY): before committing this file, move all completed/older handoff tasks to `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Keep only the single current task in **Active Handoff** (one active block only).
- If a task is completed, archive it first, then replace **Active Handoff** with the next task.

## Moderator Communication (Minimal)

**Moderator relays ONLY ONE OF THESE messages — nothing more:**

- `New handoff update for <target-machine>. Pull main and resume from 'Active Handoff' section.`
- `<Commit hash> — New handoff update for <target-machine>. Pull and check docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md Active Handoff.`

**No moderator task:** Moderator provides zero context, zero explanation. The handoff document has everything the receiving agent needs.

## Current Status

- All prior Phase 2, chat, and pre-Linux sync remediation work is complete and archived.
- P0 server-side sync hardening deployed and verified on `mint22`.
- Upload hardening story: CLOSED (2026-03-15). All machines verified.
- Deletion propagation story: **CLOSED** (2026-03-16). All three machines verified.
  - Linux client (`mint-dnc-client`): verified 2026-03-16 ~03:00Z
  - Windows client (`Windows11-TestDNC`): verified 2026-03-16 ~08:16Z. Bug fixed: `RemoveFileRecordsUnderPathAsync` path separator on Windows.
  - Server (`mint22`): confirmed stable 2026-03-16. Zero ERR entries, both nodes soft-deleted, no 5xx.
- Duplicate controller fix: CLOSED (2026-03-18). Deployed and verified on `mint22`. Files endpoint returns 401, service healthy.
- Windows IIS + Service Validation: **COMPLETE** (2026-03-21). Three startup blockers resolved. IIS reverse proxy configured and verified (URL Rewrite + ARR). HTTP (port 80) and HTTPS (port 443) both proxy to Kestrel :5080. Self-signed localhost cert bound.
- File browser child count fix: **DEPLOYED** (2026-03-21). `mint22` redeployed; service stable.
- `mint22` connectivity diagnosis: **COMPLETE** (2026-03-22). Current deployment listens directly on HTTPS `:5443`; no listener exists on `:15443`.
- **Active cycle:** Security audit — Windows runtime/client parity validation handoff to `Windows11-TestDNC` (2026-03-23).

## Environment

| Role | Machine | Detail |
|---|---|---|
| Server | `mint22` | `https://mint22:5443/` |
| Client | `Windows11-TestDNC` | Sync dir: `C:\Users\benk\Documents\synctray` |
| Client | `mint-dnc-client` | Linux Mint 22 validation host for desktop sync client implementation + E2E testing |
| Android Client | `monolith` | Android MAUI app development + emulator testing (Windows 11) |

## Key Carry-Forward Contracts

- Auth: OpenIddict bearer on files/sync endpoints via `FilesControllerBase` `[Authorize(AuthenticationSchemes = "OpenIddict.Validation.AspNetCore")]`.
- API envelope: middleware wraps responses; clients should unwrap via envelope helpers.
- Sync flow: changes -> tree -> reconcile -> chunk manifest -> chunk download -> file assembly.
- Desktop OAuth constant: `OAuthConstants.ClientId = "dotnetcloud-desktop"`.
- **SignalR channel group naming:** `chat-channel-{channelId}` (used by `ChatRealtimeService.ChannelGroup()` and Android `SignalRChatClient`).
- **Controller discovery:** Core.Server references Files.Host and Chat.Host via `ProjectReference`. ASP.NET Core auto-discovers controllers from referenced assemblies. Do NOT create duplicate controllers in Core.Server for routes already served by module Host assemblies.

## Active Handoff

**Target machine:** `Windows11-TestDNC`
**Status:** READY FOR PICKUP

### Security Audit — Desktop Client Fixes Validation Required (Windows)

Server-side security audit complete (commit `e5b5988`). Linux desktop client implementation and tests are complete; Windows validation is required for runtime parity.

#### Implemented changes (already on main)

- `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/SettingsViewModel.cs`
    - `_addAccountServerUrl` default changed to `string.Empty`.
- `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcServer.cs`
    - Unix socket permissions restricted to `0600` via `File.SetUnixFileMode`.
- `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs`
    - Symlink materialization blocked when resolved target escapes sync root.
    - `ResolveLocalPathAsync` now validates resolved paths remain within sync root.

#### Test coverage added

- `tests/DotNetCloud.Client.SyncTray.Tests/ViewModels/SettingsViewModelTests.cs`
    - `AddAccountServerUrl_DefaultsToEmptyString`
- `tests/DotNetCloud.Client.SyncService.Tests/IpcServerSecurityTests.cs`
    - `RestrictUnixSocketPermissions_SetsSocketModeTo600OnLinux`
- `tests/DotNetCloud.Client.Core.Tests/Sync/SyncEngineTests.cs`
    - `SyncAsync_PendingSymlinkDownload_TargetEscapesSyncFolder_BlocksMaterialization`
    - `SyncAsync_RemoteChangeWithTraversalName_SetsErrorStateAndSkipsQueueing`

#### Linux validation evidence (mint-dnc-client)

- `dotnet test tests/DotNetCloud.Client.Core.Tests/DotNetCloud.Client.Core.Tests.csproj --no-build` → 184 passed.
- `dotnet test tests/DotNetCloud.Client.SyncService.Tests/DotNetCloud.Client.SyncService.Tests.csproj --no-build` → 28 passed.
- `dotnet test tests/DotNetCloud.Client.SyncTray.Tests/DotNetCloud.Client.SyncTray.Tests.csproj --no-build` → 84 passed.
- `dotnet build` environment gate on Linux host: Android SDK missing (`XA5300`). Non-Android projects build successfully.

#### Acceptance Criteria
- ☐ Pull latest `main` on `Windows11-TestDNC`.
- ☐ Run targeted tests:
    - `dotnet test tests/DotNetCloud.Client.Core.Tests/DotNetCloud.Client.Core.Tests.csproj --no-build`
    - `dotnet test tests/DotNetCloud.Client.SyncService.Tests/DotNetCloud.Client.SyncService.Tests.csproj --no-build`
    - `dotnet test tests/DotNetCloud.Client.SyncTray.Tests/DotNetCloud.Client.SyncTray.Tests.csproj --no-build`
- ☐ Perform runtime smoke validation on Windows desktop client:
    - Add Account dialog default server URL is blank.
    - Existing account add flow still succeeds with valid URL.
    - No regression in sync cycle startup.
- ☐ Report back with commit hash (if changes needed), raw command outputs, and any runtime log evidence.

## Relay Template

```markdown
### Send to [Server|Client] Agent on <target-machine>
<message text including target machine>

### Request Back
- commit hash
- raw endpoint/URL used
- raw error/query params
- raw log lines around the event (with timestamp)
```
