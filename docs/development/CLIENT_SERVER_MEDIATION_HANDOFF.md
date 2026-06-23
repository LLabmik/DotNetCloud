# Client/Server Mediation Handoff

Last updated: 20260622 (Client re-test #4: 401 still persists after encryption key fix — four fixes not sufficient)

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

**Status:** ❌ STILL 401 — four server fixes deployed (`0df90c38`) including JWE encryption keys, client still gets `invalid_token`

**Client test results (`mint-OptiPlex-7010`, 2026-06-22 20:12 UTC):**

| Step | Result |
|------|--------|
| Build SyncTray | 0 errors ✅ |
| Token (expired, auto-refresh) | 200 OK, new expiry 02:12:02 ✅ |
| `GET device-cursor` with fresh token | **401** `error="invalid_token"` ❌ |
| SSE stream with fresh token | **401** (3 attempts, fell back to polling) ❌ |
| Second token refresh after SSE 401 | 200 OK ✅ |
| Retry SSE after second refresh | **401** ❌ |

**The four server fixes so far (none sufficient):**
1. `806d0716` — removed duplicate `UseHttpsRedirection()`
2. `13838258` — added `module-id` gRPC metadata header
3. `4d00ddc7` — `CallerContextInterceptor` defaults to System caller
4. `0df90c38` — encryption key loading for JWE token decryption

**Critical observation:** The server claims encryption keys are loaded and deployed. But the token is STILL rejected. One of these must be true:
- The deployed binary is NOT commit `0df90c38` (stale deploy)
- The encryption key loaded doesn't match the one used to encrypt tokens
- Something else in the validation chain still fails after decryption

---

### Server Actions — `cloud.kimball.home`

- [ ] **Verify the deployed binary IS commit `0df90c38`:**
  ```bash
  systemctl show dotnetcloud --property=ActiveEnterTimestamp
  stat /opt/dotnetcloud/publish/DotNetCloud.Core.Server.dll | grep Modify
  # ActiveEnterTimestamp MUST be AFTER Modify
  ```
- [ ] **Verify encryption key is loaded** in the Files module host:
  ```bash
  journalctl -u dotnetcloud --since "1 hour ago" | grep -i "encrypt\|decrypt\|jwe\|encryption.key" | tail -20
  ```
- [ ] **Manually introspect a real client token** to see what the introspection service returns:
  - Does introspection return `active=true` or `active=false`?
  - If `active=false`, what's the error? Decryption failure? Signature failure? Expired?
- [ ] **Test the Files API directly from the server** with a manually-acquired token:
  ```bash
  # Get a token
  TOKEN=$(curl -sk -X POST https://localhost:5443/connect/token \
    -d "grant_type=password&username=<user>&password=<pass>&client_id=dotnetcloud-desktop&scope=api" \
    | jq -r '.access_token')
  # Call Files API
  curl -sk -H "Authorization: Bearer $TOKEN" https://localhost:5443/api/v1/files/sync/device-cursor?deviceId=test
  # If this works locally but client still gets 401, it's a deployment/network issue
  ```
- [ ] **If binary is stale or deploy incomplete:** rebuild and redeploy.
- [ ] **Update this handoff** with findings and push.
