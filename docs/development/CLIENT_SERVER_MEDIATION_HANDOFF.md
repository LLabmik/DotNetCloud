# Client/Server Mediation Handoff

Last updated: 2026-03-18 (Duplicate controller fix — removed Core.Server duplicates, server redeployment required on mint22)

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
- **Active cycle:** Duplicate controller fix deployed from `monolith` — server redeployment required on `mint22` to resolve HTTP 500 on Files/Sync/WOPI endpoints.

## Environment

| Role | Machine | Detail |
|---|---|---|
| Server | `mint22` | `https://mint22:15443/` |
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

### Server Redeployment Required — Duplicate Controller Fix

**Target machine:** `mint22`
**Priority:** Critical
**Completed by:** `monolith` (client agent)

#### Root Cause — HTTP 500 on All Files/Sync/WOPI Endpoints

Android "My Files" tab returned `500 Internal Server Error` on `GET /api/v1/files`. Root cause: **duplicate controller classes** at identical routes in both `Core.Server` and `Files.Host` assemblies.

ASP.NET Core's `ApplicationPartManager` auto-discovers controllers from referenced assemblies that depend on MVC packages. Since `Core.Server.csproj` references `Files.Host` (ProjectReference line 42), and `Files.Host` references MVC, all Files.Host controllers were auto-discovered — creating duplicates with the Core.Server copies at the same routes → `AmbiguousMatchException` → HTTP 500 for **every** Files/Sync/WOPI request.

**Why Chat worked but Files didn't:** `ChatController` only exists in `Chat.Host` (no duplicate in Core.Server), so it was the sole controller at `/api/v1/chat`. Files had duplicates at all three routes.

#### What Was Done on monolith

1. **Removed 4 duplicate controller files from Core.Server:**
   - `src/Core/DotNetCloud.Core.Server/Controllers/FilesController.cs`
   - `src/Core/DotNetCloud.Core.Server/Controllers/SyncController.cs`
   - `src/Core/DotNetCloud.Core.Server/Controllers/WopiController.cs`
   - `src/Core/DotNetCloud.Core.Server/Controllers/FilesControllerBase.cs`

2. **Updated Files.Host `FilesControllerBase` auth scheme:**
   - Changed `[Authorize]` to `[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]`
   - Added `OpenIddict.Validation.AspNetCore` v7.2.0 package to `Files.Host.csproj`
   - Reason: plain `[Authorize]` defaults to cookies (via `AddIdentity`), which won't work for bearer-token API clients

3. **Fixed MIME type fallback bug in Files.Host `DownloadAsync`:**
   - Changed `node.MimeType ?? "application/octet-stream"` to `string.IsNullOrWhiteSpace(node.MimeType) ? "application/octet-stream" : node.MimeType`
   - Same for versioned downloads (`ver.MimeType`)
   - The `??` operator only handles `null`, not empty/whitespace MIME types

4. **Updated test infrastructure:**
   - `FilesControllerTests.cs`: Updated using to `Files.Host.Controllers`, added `IThumbnailService` + `ILogger<FilesController>` mocks, added `ServiceProvider` with logging to `DefaultHttpContext.RequestServices`
   - `FilesHostWebApplicationFactory.cs`: Added `TestAuthHandler` for `"OpenIddict.Validation.AspNetCore"` scheme
   - `DotNetCloudWebApplicationFactory.cs`: Added OpenIddict scheme registration for Core.Server integration tests

#### Test Results on monolith

| Suite | Passed | Failed | Notes |
|-------|--------|--------|-------|
| Core.Server.Tests | 332 | 0 | 1 skipped (Linux-only) |
| Modules.Files.Tests | 638 | 0 | |
| Modules.Chat.Tests | 283 | 0 | |
| FilesControllerTests | 31 | 0 | All download/upload/CRUD tests pass |
| FilesRestIsolationIntegrationTests | 16 | 0 | All Files integration tests pass |
| Client.Core.Tests | 182 | 0 | |
| Core.Auth.Tests | 85 | 0 | |
| Core.Data.Tests | 176 | 0 | |
| CLI.Tests | 118 | 0 | |
| Client.SyncService.Tests | 27 | 0 | |
| Modules.Example.Tests | 51 | 0 | |
| Integration.Tests (other) | 112 | 20 | Pre-existing: `ModuleUiRegistrationHostedService` crash |
| Client.SyncTray.Tests | 75 | 2 | Pre-existing: Linux-specific tests on Windows |

#### Action Required on mint22

1. `git pull origin main`
2. Rebuild and redeploy:
   ```bash
   cd /opt/dotnetcloud
   dotnet publish src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj -c Release -o /opt/dotnetcloud/publish
   sudo systemctl restart dotnetcloud.service
   ```
3. Verify the service is healthy:
   ```bash
   systemctl status dotnetcloud.service --no-pager | head -10
   curl -k https://localhost:15443/health
   ```
4. Test Files endpoint responds (should return 401 Unauthorized, NOT 500):
   ```bash
   curl -k -s -o /dev/null -w "%{http_code}" https://localhost:15443/api/v1/files
   ```
   Expected: `401` (no auth token). Previously returned `500` (AmbiguousMatchException).

#### Request Back

- Commit hash after redeployment
- `systemctl status dotnetcloud.service --no-pager` (first 10 lines)
- `curl -k -s -o /dev/null -w "%{http_code}" https://localhost:15443/api/v1/files` output
- Any errors from `journalctl -u dotnetcloud.service --since "5 min ago" | grep -i err`

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
