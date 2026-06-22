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

- Auth: Files module host uses a policy scheme (`DotNetCloud.Module`) that auto-selects between `OpenIddict.Validation.AspNetCore` (JWT Bearer) and `Identity.Application` (cookie) based on the `Authorization` header. Controllers use plain `[Authorize]`. All module hosts must follow this pattern.
- API envelope: middleware wraps responses; clients should unwrap via envelope helpers.
- Sync flow: changes -> tree -> reconcile -> chunk manifest -> chunk download -> file assembly.
- Desktop OAuth constant: `OAuthConstants.ClientId = "dotnetcloud-desktop"`.
- **SignalR channel group naming:** `chat-channel-{channelId}` (used by `ChatRealtimeService.ChannelGroup()` and Android `SignalRChatClient`).
- **Controller discovery:** Core.Server references Files.Host and Chat.Host via `ProjectReference`. ASP.NET Core auto-discovers controllers from referenced assemblies. Do NOT create duplicate controllers in Core.Server for routes already served by module Host assemblies.

## Active Handoff

**Status:** 🔴 RE-DEPLOY REQUIRED — Switched from JwtBearer to OpenIddict validation

**⚠️ JwtBearer approach failed — version conflict identified.**

The initial deploy (JwtBearer 10.0.3) had two issues:
1. `System.IdentityModel.Tokens.Jwt` v8.0.1 NuGet package ships assembly version **0.96.1.0**, but deps.json declares **8.0.1.0** — caused `FileNotFoundException` on module startup (500 error).
2. After manually copying assemblies, JwtBearer validated tokens but rejected them (`invalid_token` — signing key mismatch).

**Fix (commit `b45b1691` on `fix/files-module-bearer-auth`):**

Replaced `Microsoft.AspNetCore.Authentication.JwtBearer` with **`OpenIddict.Validation.AspNetCore`** (already installed v7.2.0). No new NuGet packages needed — avoids the version conflict entirely.

| Before (broken) | After (fixed) |
|----------------|---------------|
| `AddJwtBearer("Bearer", ...)` with `TokenValidationParameters` + local signing key | `AddOpenIddict().AddValidation()` with shared RSA signing keys from `oidc-keys/` |
| `"Bearer"` in policy scheme selector | `"OpenIddict.Validation.AspNetCore"` in policy scheme selector |
| JwtBearer package reference + deps.json mismatch | Zero new dependencies — uses OpenIddict's built-in JWT handler |

**Deploy steps (on `cloud.kimball.home`):**

```bash
git checkout main && git pull
git merge fix/files-module-bearer-auth
sudo ./scripts/deploy.sh --force
sudo systemctl restart dotnetcloud
```

**Verification after deploy:**

1. `curl -sk https://cloud.dotnetcloud.net/api/v1/files/` → should return 200 JSON, not 401
2. `curl -sk -H "Authorization: Bearer $(curl -sk -d 'grant_type=refresh_token&refresh_token=...&client_id=dotnetcloud-desktop' https://cloud.dotnetcloud.net/connect/token | python3 -c 'import json,sys;print(json.load(sys.stdin)[\"access_token\"])')" https://cloud.dotnetcloud.net/api/v1/files/` → should return 200 (token validated by OpenIddict)

**On `mint-OptiPlex-7010` (after deploy):**

```bash
dotnet run --project src/Clients/DotNetCloud.Client.SyncTray/DotNetCloud.Client.SyncTray.csproj
```

Expected log: `"SSE stream connected."` (was previously falling back to polling with 401).

**Client version:** 0.3.9-alpha
**Server build:** 0.3.12
