# Client/Server Mediation Handoff

Last updated: 20260622 (Client re-test: 401 STILL persists after triple server fix — token refresh works, API rejects fresh tokens)

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

**Role separation (MANDATORY):**

- **Client code** (`src/Clients/`, `src/UI/`) is handled ONLY by client machines (`mint-OptiPlex-7010`, `Windows11-TestDNC`, `mint-dnc-client`, `monolith`).
- **Server code** (`src/Core/`, `src/Modules/`) is handled ONLY by server machines (`cloud.kimball.home`, `mint22`).
- Each agent ONLY executes actions in the block matching their machine name (from the Environment table).
- If no action block matches your machine, the handoff is not for you — relay it to the moderator.
- Never cross role boundaries: a client agent never deploys server code, a server agent never builds client apps.

**Active Handoff format (MANDATORY):**

Every Active Handoff MUST use per-machine action blocks. Actions are grouped by the machine that executes them, using the exact machine names from the Environment table.

```markdown
## Active Handoff

**Summary:** [one-line description of what's happening]

[Context/background — what changed, why, relevant commits]

---

### Server Actions — `cloud.kimball.home`

- [ ] Action 1 with exact commands
- [ ] Action 2

### Client Actions — `mint-OptiPlex-7010`

- [ ] Action 1 with exact commands
- [ ] Action 2
```

**Critical rules:**
- Each agent ONLY executes actions in the block matching their machine name (from the Environment table).
- If no action block matches your machine, the handoff is not for you — relay it to the moderator.
- Always include exact commands (ready to copy-paste).
- Mark blocks with `✓` when complete; update status inline.
- One handoff may have 1 or 2 action blocks depending on workflow stage.

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
- Moderator handoff prompt rule (MANDATORY): every ready-to-relay message must explicitly state the target machine name (for example: `cloud.kimball.home`, `mint-dnc-client`, `Windows11-TestDNC`).
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
- VFS Phase 1 (server-side prerequisites) complete on `cloud.kimball.home`.
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

**Status:** ❌ STILL 401 — three server fixes deployed (`4d00ddc7`) but client still gets `invalid_token`

**Client test results (`mint-OptiPlex-7010`, 2026-06-22 19:10 UTC):**

| Step | Result |
|------|--------|
| Build SyncTray | 0 errors ✅ |
| Token state on load | Not expired, can refresh ✅ |
| `GET device-cursor` | **401** `error="invalid_token"` ❌ |
| SSE stream connect | **401** (3 attempts, fell back to polling) ❌ |
| Token refresh (`POST /connect/token`) | 200 OK, new expiry 01:10:12 ✅ |
| Retry SSE after refresh | **401** — fresh token rejected ❌ |

**Error:** `WWW-Authenticate: Bearer error="invalid_token", error_description="The token is invalid or expired."`

**Pattern:** Token refresh works perfectly. The refreshed token is immediately rejected by Files API and SSE. This is NOT a client-side issue — the client is sending validly-refreshed bearer tokens.

**The three server fixes so far (not sufficient):**
1. `806d0716` — removed duplicate `UseHttpsRedirection()` (307s gone)
2. `13838258` — added `module-id` gRPC metadata header
3. `4d00ddc7` — `CallerContextInterceptor` defaults to System caller

**What remains unclear:** Is the deployed binary actually running commit `4d00ddc7`? Or is there a fourth validation issue in the introspection/auth pipeline that rejects valid tokens?

---

### Server Actions — `cloud.kimball.home`

- [ ] **Verify the running binary IS commit `4d00ddc7`:**
  ```bash
  git checkout fix/files-module-bearer-auth && git pull
  git log --oneline -3
  # Confirm HEAD is 4d00ddc7
  
  # Check deploy timestamp vs service restart
  systemctl show dotnetcloud --property=ActiveEnterTimestamp
  stat /opt/dotnetcloud/publish/DotNetCloud.Core.Server.dll | grep Modify
  # ActiveEnterTimestamp MUST be AFTER Modify
  ```
- [ ] **Check server-side introspection logs in real time** while a client connects:
  ```bash
  journalctl -u dotnetcloud -f | grep -i "introspect\|invalid_token\|inactive\|claims"
  # Watch this while client sends requests — look for introspection result (active=true/false)
  ```
- [ ] **Manually introspect a real token** to see what the server thinks:
  ```bash
  # Get a token (use real credentials — the client's refresh token flow proves these work)
  curl -sk -X POST https://localhost:5443/connect/token \
    -d "grant_type=refresh_token&refresh_token=<real-refresh-token>&client_id=dotnetcloud-desktop"
  # Introspect the resulting access_token via gRPC or the introspection endpoint
  ```
- [ ] **Check if token scope includes the Files API:**
  - Does the token have `scope=api` or the required scope for Files endpoints?
  - Check OpenIddict client configuration for `dotnetcloud-desktop` — are the right scopes/permissions granted?
- [ ] **If binary is stale:** redeploy immediately:
  ```bash
  dotnet build -c Release
  dotnet publish -c Release
  sudo ./scripts/deploy.sh
  systemctl restart dotnetcloud
  ```
- [ ] **Update this handoff** with findings and push.
