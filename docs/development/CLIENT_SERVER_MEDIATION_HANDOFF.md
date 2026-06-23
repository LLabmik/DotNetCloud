# Client/Server Mediation Handoff

Last updated: 2026-06-23 01:55 UTC (Fresh OAuth login: token acquired successfully, still rejected 401 by Files API)

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

**Status:** ❌ STILL 401 — Files API rejects tokens as `invalid_token`. Client has retested 5 times across 5 server fixes. Server MUST run local introspection test before ANY further client retests.

**Decisive question (server must answer this):** Does `curl -k https://localhost:5443/api/v1/files/sync/device-cursor` with a fresh token return 200 or 401? If 200 locally but 401 from remote client, it's a proxy/routing issue. If 401 locally too, the deploy is broken or the fix is incomplete.

---

### Complete fix chain (all deployed to `cloud.kimball.home`)

| # | Commit | What was fixed | Why it mattered |
|---|--------|---------------|-----------------|
| 1 | `806d0716` | Remove duplicate `UseHttpsRedirection()` in `UseDotNetCloudMiddleware()` | gRPC introspection calls were getting HTTP 307 redirects instead of reaching the introspection endpoint |
| 2 | `13838258` | Add `module-id` gRPC metadata header to `TokenIntrospectionClient` | Files module introspection calls weren't attributed to the Files module, causing auth failure |
| 3 | `4d00ddc7` | `CallerContextInterceptor` defaults to `System` caller for module-to-core calls | Was throwing on null UserId when modules called core services via gRPC |
| 4 | `0df90c38` | Load encryption keys for JWE token introspection | Encryption keys existed on disk but weren't being loaded into the key ring at startup |
| 5 | `49880eb2` | Enable JWE token encryption (remove `DisableAccessTokenEncryption`) | Opaque reference tokens were incompatible with introspection — tokens must be JWE-encrypted JWTs |

---

### Client retest history (`mint-OptiPlex-7010`)

All 5 client retests returned **401 `invalid_token`** from the Files API, including the latest test (2026-06-23 01:55 UTC) which used a **fresh OAuth authorization-code grant** (not a refresh token). The OAuth flow itself succeeds — tokens are issued — but the Files API rejects them on every call.

| Retest | After fix # | Result |
|--------|------------|--------|
| 1 | 1 (307 redirect) | 401 |
| 2 | 2 (module-id header) | 401 |
| 3 | 3 (CallerContext) | 401 |
| 4 | 4 (encryption keys) | 401 |
| 5 | 5 (JWE enabled) | 401 |

**This disproves the theory that client requests aren't reaching the server.** The OAuth callback, token endpoint, and Files API are all reachable. The server issues tokens and then rejects them at introspection.

---

### ⚠️ NO CLIENT ACTIONS — this handoff is SERVER-ONLY

There is no client action block. The client has retested 5 times. Do NOT ask the client to retest until the server can confirm locally that:

```
curl -k https://localhost:5443/api/v1/files/sync/device-cursor -H "Authorization: Bearer <token>"
```
returns **200 OK**.

---

### Server Actions — `cloud.kimball.home`

- [ ] **🔴 STEP 1: Test introspection locally on the server (MANDATORY before anything else)**
  ```bash
  # Get a token locally
  TOKEN=$(curl -sk -X POST https://localhost:5443/connect/token \
    -d "grant_type=password&username=<user>&password=<pass>&client_id=dotnetcloud-desktop&scope=api" \
    | jq -r '.access_token')

  echo "Token: ${TOKEN:0:20}..."

  # Call Files API locally
  curl -sk -w "\nHTTP %{http_code}\n" -H "Authorization: Bearer $TOKEN" \
    "https://localhost:5443/api/v1/files/sync/device-cursor?deviceId=test"

  # Also test introspection endpoint directly
  curl -sk -w "\nHTTP %{http_code}\n" -X POST https://localhost:5443/connect/introspect \
    -d "token=$TOKEN" | jq .
  ```
  **Report the HTTP status codes and the introspection response (`active`: true or false).**

- [ ] **STEP 2: Check introspection logs**
  ```bash
  journalctl -u dotnetcloud --since "10 minutes ago" | grep -i "introspect\|decrypt\|JWE\|invalid_token\|active\|encrypt" | tail -50
  ```

- [ ] **STEP 3: Verify encryption keys are loaded**
  ```bash
  ls -la /opt/dotnetcloud/oidc-keys/
  # Should show both signing AND encryption keys
  # Check startup logs for key loading:
  journalctl -u dotnetcloud --since "1 hour ago" | grep -i "key\|encrypt\|signing" | tail -20
  ```

- [ ] **STEP 4: If local test ALSO fails with 401** — the deploy is stale. Do a clean deploy:
  ```bash
  sudo systemctl stop dotnetcloud
  rm -rf /opt/dotnetcloud/publish/*
  # Republish from source and redeploy
  sudo systemctl start dotnetcloud
  # Then re-run STEP 1
  ```

- [ ] **STEP 5: Update this handoff** with the local test results and push. Do NOT ask the client to retest until you can confirm local 200.
