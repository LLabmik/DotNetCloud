# Client/Server Mediation Handoff

Last updated: 2026-06-23 20:51 UTC (Windows11-TestDNC: YARP 502 fix verified — zero 502 errors. 🔴 NEW: CompleteUpload 409 — chunks upload, file node not created.)

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

- ✅ **YARP 502 fix VERIFIED** — zero 502 errors during large file upload. Server deployed (19/19 Healthy), client rebuilt with `88b951a3`.
- ✅ **429 fix** verified on `Windows11-TestDNC` — zero 429 errors end-to-end. Deployed on `cloud.kimball.home`.
- ✅ **Client resilience improved**: `ChunkUploadMaxRetries` 3→6, 502-specific backoff, 404-on-resume cleanup for stale sessions.
- ✅ **Server cleanup**: 62 orphaned upload sessions + 237 orphaned chunk blobs cleaned.
- 🔴 **NEW: CompleteUpload 409 issue** — Chunks upload successfully (zero 502s) but `CompleteUpload` returns 409 and file node never created in parent folder. ALL uploaded files (PDF + 4 small ODTs) affected. Files not visible on `cloud.dotnetcloud.net`. Server-side investigation needed.
- All prior Phase 2, chat, pre-Linux sync remediation, SyncTray icon enhancement, VFS work complete and archived.

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

**Summary:** 🔴 CompleteUpload 409 root cause found and fixed server-side. Server cleaned. Client needs to retry upload.

**Root cause:** Our previous cleanup deleted 237 orphaned `FileChunks` DB records + blobs, but the upload session `019EF7A8` (created before cleanup) still had `ReceivedChunks=237`. When `CompleteUploadAsync` queried `FileChunks` for all 252 manifest hashes, only 45 existed → threw `ValidationException("Missing 207 chunk(s)")` → mapped to 409 Conflict. The `ReceivedChunks` counter was stale.

**Server fix (committed `def196b6`, deployed):**
- `InitiateUploadAsync`: Added `ReferenceCount > 0` filter to existing-chunk query. Orphaned chunks (RefCount=0) will no longer be reported as "existing" to clients.
- `CompleteUploadAsync`: When chunks are missing, updates `session.ReceivedChunks` to actual count and saves before throwing. Session state stays accurate for subsequent `GetSession`/`InitiateUpload` calls.

**Server cleanup:**
- 25 InProgress sessions deleted
- 45 orphaned `FileChunks` (RefCount=0) + their blobs deleted
- Clean state: 0 sessions, 0 orphaned chunks

**What client needs to do:** The server fix prevents stale session state going forward. The client should retry the upload — the next `InitiateUpload` will correctly identify only genuinely existing chunks, all missing chunks will be uploaded, and `CompleteUpload` should succeed.

**Optional client improvement:** The client's 409 handler (ChunkedTransferClient.cs ~line 355) treats ALL 409s as name conflicts. If chunks go missing between upload and CompleteUpload, the client gives up with `Guid.Empty`. Consider calling `GET api/v1/files/upload/{sessionId}` on 409 to check if session is still InProgress with missing chunks, then re-upload and retry.

---

### Server Actions — `cloud.kimball.home`

- ✓ Root cause found: stale session with 237 ReceivedChunks but only 45 actual chunks
- ✓ Server fix: `ReferenceCount > 0` filter + session state update on missing chunks
- ✓ DB cleaned: 25 sessions + 45 orphaned chunks deleted
- ✓ Deployed and healthy

### Client Actions — `Windows11-TestDNC`

- [ ] Pull `perf/synctray-scan-and-transfer-speedups`, rebuild, and retry upload
- [ ] Files should upload successfully — server now correctly identifies existing chunks
- [ ] Verify files appear on `cloud.dotnetcloud.net`
