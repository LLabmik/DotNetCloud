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

**Summary:** 🔴 YARP 502 fix verified (0 errors) but ALL file uploads fail at `CompleteUpload` — returns 409 and file node never created. Files not visible on server. Server-side investigation needed.

**Context:** On 2026-06-23 at ~20:49 UTC, Windows11-TestDNC ran SyncTray Release with YARP fix deployed on `cloud.kimball.home`. Results:

**✅ YARP fix verified — zero 502 errors:**
- Server: 19/19 modules Healthy, YARP `PooledConnectionLifetime` fix deployed
- Client rebuilt with `88b951a3` (retry improvements + 502 resilience)
- During upload of 1.17 GB PDF (252 chunks at 4 MB each), the client resumed session `019ef7a8-1b90-7fce-aaec-62f8974f6736` and uploaded **51 new chunks** (~200 MB) via HTTP PUT to the Files module through YARP — **zero 502 errors**.

**🔴 CompleteUpload 409 — ALL 5 files affected:**
- 4 small ODT files (Test.odt, Test2.odt, Test3.odt, Checkbook Register - 2026.ods): All chunks dedup'd (HTTP 409 expected), CompleteUpload returned 409, file node not found in parent folder → stored `Guid.Empty`.
- 1 large PDF (bb-The.Art.Of.Dejah.Thoris.And.The.Worlds.Of.Mars.Vol.2.HC.pdf): 51 new chunks uploaded successfully (HTTP 200, no 409s on individual chunks), then `CompleteUpload` returned 409. Lookup by filename in parent folder failed → stored `Guid.Empty`.
- Web UI (`cloud.dotnetcloud.net/apps/files`) shows Documents folder with only old files. None of the newly uploaded files visible.

**Client behavior on CompleteUpload 409 (ChunkedTransferClient.cs ~line 352):**
```csharp
catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
{
    // Deletes local upload session tracking
    // Lists children of parent folder by name (case-insensitive)
    // If found → stores existing NodeId
    // If not found → stores Guid.Empty (sync engine reconciles later)
}
```

The client correctly handles the 409 but the file node genuinely doesn't exist on the server. The chunk blobs are stored (content-addressable storage), but CompleteUpload never created the file node in the parent folder.

**Hypothesis:** The `CompleteUpload` endpoint on the server returns 409 (Conflict) instead of 201 (Created) even when the session wasn't previously completed. The 409 might indicate the session was already marked complete (from a previous attempt that was interrupted before the client recorded the NodeId), or the endpoint has a logic error when handling sessions that were resumed after a server restart/cleanup.

**Server-side investigation needed:**

---

### Server Actions — `cloud.kimball.home`

- [ ] **Investigate CompleteUpload 409 behavior**: Check `CompleteUpload` endpoint in Files module. Why does it return 409 when all chunks are present? Is it returning 409 because the session was already marked complete? Or is there a content-hash collision? Look at server-side logs from ~20:50 UTC for `CompleteUpload` calls on session `019ef7a8-1b90-7fce-aaec-62f8974f6736`.
- [ ] **Check if file nodes exist**: Query the files database for any file nodes created around 20:49-20:51 UTC in the Documents folder (parentId `e0504d16-83fd-4be7-8b3c-40fab83f63cd`). Are there orphaned file nodes without parent linkage? Or was the node genuinely never created?
- [ ] **Test CompleteUpload directly**: Send a manual `POST` to `api/v1/files/upload/{sessionId}/complete` for a known-good session and check the response.
- [ ] **Fix if needed**: If the 409 response is incorrect, update the CompleteUpload endpoint to return 201 with the created file node when chunks are successfully assembled. If the 409 is correct (file already exists), ensure the response body includes the existing node's ID so the client can record it without an additional lookup.

### Client Actions — `Windows11-TestDNC`

- ☐ Completed verification tasks. Awaiting server-side fix. No further client action until CompleteUpload is fixed on the server.
