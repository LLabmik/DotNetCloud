# Client/Server Mediation Handoff

Last updated: 20260621 (Files module host Bearer auth fix — deploy required)

Purpose: shared handoff between client-side and server-side agents, mediated by user.

Archived context:

- Historical completed updates are in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Additional history remains available in git.
- VFS Phase 3 (Windows Cloud Filter API) completed on Windows11-TestDNC (2026-05-12).
- VFS Phase 2 (core abstraction layer) completed on Windows11-TestDNC (previously).

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

- All prior Phase 2, chat, pre-Linux sync remediation, SyncTray icon enhancement work is complete and archived.
- VFS Phase 1 (server-side prerequisites) complete on `mint22`.
- VFS Phase 2 (core abstraction layer) complete on `Windows11-TestDNC`.
- VFS Phase 3 (Windows Cloud Filter API) complete on `Windows11-TestDNC`.
- VFS Phase 4 (Linux FUSE) complete on `mint-dnc-client`:
  - `FuseSyncFilesystem : IVirtualFileProvider` with mount/unmount lifecycle
  - `DotNetCloudFuseOperations : IFuseOperations` with all FUSE callbacks
  - `LruCacheManager` wired into FUSE read path
  - DI registration: `FuseSyncFilesystem` on Linux
  - Build: 0 errors (CI solution filter). Tests: 253/254 Client.Core pass, 106/106 SyncTray pass.
- VFS Phase 5 (SyncTray UI Integration) complete on `Windows11-TestDNC` (archived).
- VFS Phase 6 (Testing & Validation) complete on `Windows11-TestDNC`:
  - 50+ unit tests across all VFS components
  - `LruCacheManager` class created + DI registered
  - Windows/Linux/E2E test scenarios documented
  - Build: 0 errors. Tests: Core 435, Client.Core 253/254, SyncTray 106.

## Environment

| Role           | Machine              | Detail                                                                             |
| -------------- | -------------------- | ---------------------------------------------------------------------------------- |
| Server         | `cloud.kimball.home` | `https://cloud.dotnetcloud.net/` (production)                                      |
| Server         | `mint22`             | `https://mint22:5443/` (dev)                                                       |
| Client         | `Windows11-TestDNC`  | Sync dir: `C:\Users\benk\Documents\synctray`                                       |
| Client         | `mint-dnc-client`    | Linux Mint 22 validation host for desktop sync client implementation + E2E testing |
| Client         | `mint-OptiPlex-7010` | This machine — production client connected to `cloud.dotnetcloud.net`              |
| Android Client | `monolith`           | Android MAUI app development + emulator testing (Windows 11)                       |

## Key Carry-Forward Contracts

- Auth: Files module host now uses a policy scheme (`DotNetCloud.Module`) that auto-selects between `Bearer` (JWT) and `Identity.Application` (cookie) based on the `Authorization` header. Controllers use plain `[Authorize]` — no explicit scheme. All module hosts must follow this pattern.
- API envelope: middleware wraps responses; clients should unwrap via envelope helpers.
- Sync flow: changes -> tree -> reconcile -> chunk manifest -> chunk download -> file assembly.
- Desktop OAuth constant: `OAuthConstants.ClientId = "dotnetcloud-desktop"`.
- **SignalR channel group naming:** `chat-channel-{channelId}` (used by `ChatRealtimeService.ChannelGroup()` and Android `SignalRChatClient`).
- **Controller discovery:** Core.Server references Files.Host and Chat.Host via `ProjectReference`. ASP.NET Core auto-discovers controllers from referenced assemblies. Do NOT create duplicate controllers in Core.Server for routes already served by module Host assemblies.

## Active Handoff

**Status:** 🔴 SERVER-SIDE DEPLOY REQUIRED — deploy `main` to `cloud.kimball.home`

**Problem:** Desktop sync client (`SyncStreamListener`) sends `Authorization: Bearer <jwt>` to SSE endpoint `api/v1/files/sync/stream` and all other Files module REST APIs. The Files module host only had `Identity.Application` cookie auth registered — no Bearer token handler. Result: 401 on every desktop client API call to the Files module (SSE, device-cursor, changes, etc.). Client falls back to polling but push-based sync is broken.

**Fix applied on branch `fix/files-module-bearer-auth` (commit pending):**

**Files changed (14 files):**

| File                                                                                     | Change                                                                                                         |
| ---------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------- |
| `Directory.Packages.props`                                                               | Added `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.3                                                   |
| `src/Modules/Files/DotNetCloud.Modules.Files.Host/DotNetCloud.Modules.Files.Host.csproj` | Added `PackageReference` for JwtBearer                                                                         |
| `src/Modules/Files/DotNetCloud.Modules.Files.Host/Program.cs`                            | Added JWT Bearer auth + policy scheme (auto-selects Bearer vs Cookie based on `Authorization` header)          |
| 13 controller files in `.../Controllers/*.cs`                                            | Changed `[Authorize(AuthenticationSchemes = "Identity.Application")]` → `[Authorize]` (uses new policy scheme) |

**Deploy steps (on `cloud.kimball.home`):**

1. `git pull` on `main`
2. `dotnet build src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj -c Release`
3. `sudo ./scripts/deploy.sh` (rebuilds and deploys all modules including the Files module host)
4. Restart the service: `sudo systemctl restart dotnetcloud`
5. Verify the Files module is running: `curl -sk https://cloud.dotnetcloud.net/api/v1/files/` (should return 200, not 401)
6. Verify SSE stream accepts Bearer tokens: `curl -sk -H "Authorization: Bearer <token>" https://cloud.dotnetcloud.net/api/v1/files/sync/stream` (should return `text/event-stream`, not 401)

**Verification on client side (`mint-OptiPlex-7010`):**

After deploy completes, restart the SyncTray client on this machine:

```bash
cd /home/benk/Repos/DotNetCloud
dotnet run --project src/Clients/DotNetCloud.Client.SyncTray/DotNetCloud.Client.SyncTray.csproj
```

Check logs for: `"SSE stream connected."` (was previously getting 401 and falling back to polling).

**Client version:** 0.3.9-alpha (built locally, connects to `https://cloud.dotnetcloud.net`)
**Server update available:** Client auto-detected 0.3.12 — deploy will also push the latest server build.
