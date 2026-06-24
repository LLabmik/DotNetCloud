# Client/Server Mediation Handoff

Last updated: 2026-06-23 20:35 UTC (Windows11-TestDNC: YARP 502 root cause found — stale HTTP/2 connections. Fixed on both client and server. Needs server deploy.)

Purpose: shared handoff between client-side and server-side agents, mediated by user.

Archived context:

- Historical completed updates are in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Additional history remains available in git.
- VFS Phase 3 (Windows Cloud Filter API) completed on Windows11-TestDNC (2026-05-12).
- VFS Phase 2 (core abstraction layer) completed on Windows11-TestDNC (previously).

## Process Rules

**Agent autonomy (CRITICAL):**

- Both client and server agents work autonomously — they do NOT ask the moderator for context or permission.
- Agents pull the branch specified in the relay message, read the **Active Handoff** section, and execute the work described there independently.
- All actionable items, blockers, and technical details go directly in this document.
- **Current active branch:** `perf/synctray-scan-and-transfer-speedups`
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
### Active Handoff

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

- 429 fix verified on `Windows11-TestDNC` — zero 429 errors end-to-end. ✅
- 429 fix deployed and verified on `cloud.kimball.home`.
- CompleteUpload 409 root cause found: missing chunks (237/252 received). Server cleaned.
- UploadSessionDto now includes `TotalChunks`, `ReceivedChunks`, `Status` for easy progress tracking.
- YARP 502 logging improved to capture ForwarderError exceptions for future diagnosis.
- All orphaned upload sessions (62) and chunk blobs (237) cleaned from server.
- **YARP 502 root cause found and fixed**: `SocketsHttpHandler` in `MapModuleApiProxies` had no `PooledConnectionLifetime` — HTTP/2 connections to module hosts went stale on the backend while YARP still held them as valid. When YARP reused a stale connection, it failed → 502 Bad Gateway. Fixed by adding `PooledConnectionLifetime=2min`, `PooledConnectionIdleTimeout=30s`, `ConnectTimeout=10s`.
- **Client-side 502 resilience improved**: `ChunkUploadMaxRetries` 3→6, new 502-specific catch block with longer backoff (`4^(n-1)` instead of `3^(n-1)`), 502 excluded from generic 5xx handler.
- **Client-side 404-on-resume fix**: When a resumed upload session returns 404 (server cleaned it), the client now deletes the local session record so the next sync pass starts a fresh `InitiateUpload` instead of retrying a dead session forever.
- All prior Phase 2, chat, pre-Linux sync remediation, SyncTray icon enhancement work is complete and archived.
- VFS Phase 1-6 complete.

## Environment

| Role           | Machine              | Detail                                                                             |
| -------------- | -------------------- | ---------------------------------------------------------------------------------- |
| Server         | `cloud.kimball.home` | `https://cloud.dotnetcloud.net/` (production)                                      |
| Server         | `mint22`             | `https://mint22:5443/` (dev)                                                       |
| Client         | `Windows11-TestDNC`  | Sync dir: `C:\Users\benk\Documents\synctray`                                       |
| Client         | `mint-dnc-client`    | Linux Mint 22 validation host for desktop sync client implementation + E2E testing |
| Client         | `mint-OptiPlex-7010` | production client connected to `cloud.dotnetcloud.net`              |
| Android Client | `monolith`           | Android MAUI app development + emulator testing (Windows 11)                       |

## Key Carry-Forward Contracts

- Auth: Files module host uses a policy scheme (`DotNetCloud.Module`) that auto-selects between `OpenIddict.Validation.AspNetCore` (JWT Bearer) and `Identity.Application` (cookie) based on the `Authorization` header. Controllers use plain `[Authorize]`. All module hosts must follow this pattern.
- API envelope: middleware wraps responses; clients should unwrap via envelope helpers.
- Sync flow: changes -> tree -> reconcile -> chunk manifest -> chunk download -> file assembly.
- Desktop OAuth constant: `OAuthConstants.ClientId = "dotnetcloud-desktop"`.
- **SignalR channel group naming:** `chat-channel-{channelId}` (used by `ChatRealtimeService.ChannelGroup()` and Android `SignalRChatClient`).
- **Controller discovery:** Core.Server references Files.Host and Chat.Host via `ProjectReference`. ASP.NET Core auto-discovers controllers from referenced assemblies. Do NOT create duplicate controllers in Core.Server for routes already served by module Host assemblies.

## Active Handoff

**Summary:** 🔴 Large file upload blocked by YARP 502 Bad Gateway. Root cause found (stale HTTP/2 connections in proxy) and fixed on both client and server. Server needs to deploy YARP fix, then client can retry.

**Root cause of 502:** The `SocketsHttpHandler` in `MapModuleApiProxies` (`src/Core/DotNetCloud.Core.Server/Program.cs`) had no `PooledConnectionLifetime` set. HTTP/2 connections to module hosts (e.g. Files module) could go stale on the backend side while YARP's `HttpMessageInvoker` still held them as valid in the connection pool. When YARP tried to reuse a stale connection, it failed immediately → 502 Bad Gateway. The errors were intermittent because connections became stale at different times. Once stale, ALL subsequent requests through that connection failed.

**Server-side fix (committed):**
- Added `PooledConnectionLifetime = TimeSpan.FromMinutes(2)` — forces HTTP/2 connection refresh before they go stale on the backend
- Added `PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30)` — closes idle connections faster
- Added `ConnectTimeout = TimeSpan.FromSeconds(10)` — prevents hanging on dead backends

**Client-side fixes (committed):**
- `ChunkUploadMaxRetries` 3→6, `ChunkDownloadMaxAttempts` 3→6
- New 502-specific catch block with longer backoff: `4^(n-1)` (1s, 4s, 16s, 64s, 256s) + up to 2s jitter — gives YARP time to establish a fresh connection
- 502 excluded from generic 5xx retry handler so it gets independent backoff
- 404-on-resume cleanup: when a resumed session returns 404 (server cleaned it up), the client deletes the local session record so the next sync pass starts a fresh `InitiateUpload` instead of retrying the dead session forever

**What's needed to complete verification:**

---

### Server Actions — `cloud.kimball.home`

- [ ] Deploy Program.cs changes (YARP connection lifetime fix)
- [ ] Restart Core.Server: `sudo systemctl restart dotnetcloud`
- [ ] Verify health: `curl -s -o /dev/null -w "%{http_code}" https://cloud.dotnetcloud.net/health`
- [ ] Confirm all 14 modules healthy

### Client Actions — `Windows11-TestDNC`

- [ ] After server deploys, rebuild and run SyncTray
- [ ] Upload large file — should complete without 502 errors (or with brief 502s that client retries through)
- [ ] Verify file appears on `cloud.dotnetcloud.net`
- [ ] Verify subsequent sync passes show `0 queued`
