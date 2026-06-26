# Client/Server Mediation Handoff

Last updated: 2026-06-26 14:45 UTC (Test Case 5 completed on Windows11-TestDNC — CRUD edit/delete/create all verified. Ready to relay to cloud.kimball.home for server-side verification.)

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

- ✅ **Sync architecture flow review implemented** — Explicit rename/move sync, batch conflict resolve, full-sync progress reporting, sync flow reference doc. All built and tested on server (0 errors, 1104 tests passing). See `docs/development/SYNC_FLOW_REFERENCE.md` for full sync flow documentation.
- ✅ **gRPC streaming upload fully functional** — (archived)
- ✅ **Client scanner bugs fixed** — (archived)
- ✅ **Windows 11 rename/move sync tested** — Found and fixed 3 bugs in rename detection (hash mismatch, path mutation in catch, UNIQUE constraint violation). Rename now propagates to server correctly.
- ✅ **Test Case 2 (Remote rename from server)** — Verified on Windows11-TestDNC. SyncTray picks up remote renames via polling. One bug found and fixed (`UpsertFileRecordAsync` → `UpdateFileRecordPathAsync` in `TryHandleRemoteRenameAsync`).
- ✅ **Test Case 5 (CRUD edit/delete/create)** — Verified on Windows11-TestDNC. Edit (384ms gRPC upload), delete (DELETE API, 317ms), create (108ms gRPC upload) all propagated successfully.
- ⏳ **Tests 3-4** — Batch conflict resolve (needs multi-client), full-sync progress (needs UI observation).

## Environment

| Role           | Machine              | Detail                                                                             |
| -------------- | -------------------- | ---------------------------------------------------------------------------------- |
| Server         | `cloud.kimball.home` | `https://cloud.dotnetcloud.net/` (production)                                      |
| Server         | `mint22`             | `https://mint22:5443/` (dev)                                                       |
| Client         | `Windows11-TestDNC`  | Sync dir: `C:\Users\benk\synctray`                                       |
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

**Summary:** Test Case 5 (CRUD) completed on Windows11-TestDNC. Test Case 4 (full-sync progress) remains gated by UI observation. Test Case 3 (batch conflict resolve) deferred until multi-client setup is available.

**Context:** All previous tests completed:
- Test Case 1 (local rename): ✓ 3 bugs fixed, working
- Test Case 2 (remote rename): ✓ 1 bug fixed, working
- Test Case 3 (batch conflict resolve): ☐ Deferred — needs second client
- Test Case 4 (full-sync progress): ☐ Gated — needs UI observation
- Test Case 5 (CRUD edit/delete/create): ✓ **All verified**

---

### Client Actions — `Windows11-TestDNC` ✓

**Test Case 5 — Regression: CRUD edit and delete:** ✓ **All verified**

**5a — Edit:** ✓ **Propagated successfully**
- Modified `renamed-from-webui.txt` (added text, file grew from 60 B to 100 B)
- FileSystemWatcher triggered → sync pass initiated
- gRPC upload: 384ms, NodeId preserved
```
[14:43:52 INF] FileSystemWatcher trigger: ChangeType="Changed", Path=...renamed-from-webui.txt
[14:43:53 INF] Local scan queued 1 new/modified file(s) for upload
[14:43:54 INF] File upload starting: FileName=renamed-from-webui.txt, FileSize=100
[14:43:54 INF] gRPC UploadFileStream: baseUrl=https://cloud.dotnetcloud.net/, tokenPresent=true
[14:43:54 INF] File upload complete (gRPC): FileName=renamed-from-webui.txt, NodeId="019f0599-...", FileSize=100, DurationMs=384
[14:43:56 INF] Sync pass complete: DurationMs=3277, RemoteChanges=0, LocalQueued=1, LocalApplied=1
```

**5b — Delete:** ✓ **Propagated successfully**
- Deleted `renamed-from-webui.txt` locally
- Sync detected deletion → queued server delete → DELETE API called
```
[14:44:26 INF] Local file deleted, queuing server deletion: renamed-from-webui.txt (NodeId="019f0599-...")
[14:44:26 INF] Deleting server node "019f0599-..." for locally deleted file/folder: renamed-from-webui.txt
[14:44:26 INF] API call DELETE "https://cloud.dotnetcloud.net/api/v1/files/019f0599-..."
[14:44:26 INF] Applied 1 local change(s)
[14:44:26 INF] Sync pass complete: DurationMs=317, RemoteChanges=0, LocalQueued=1, LocalApplied=1
```
Subsequent pass confirmed server acked deletion (RemoteChanges=1, tree size changed from 8205 to 7900).

**5c — Create (re-verify):** ✓ **Uploaded successfully**
- Created `crud-test-2.txt` (72 B)
- FileSystemWatcher triggered → gRPC upload 108ms → new NodeId assigned
```
[14:44:57 INF] FileSystemWatcher trigger: ChangeType="Created", Path=...crud-test-2.txt
[14:44:59 INF] File upload complete (gRPC): FileName=crud-test-2.txt, NodeId="019f05e4-...", FileSize=72, DurationMs=108
[14:44:59 INF] Sync pass complete: DurationMs=1311, RemoteChanges=0, LocalQueued=1, LocalApplied=1
```

**Test Case 4 — Full-sync progress:** ☐ Gated — needs UI observation on Windows11-TestDNC

---

### Next Steps

**Current handoff:** Relay to `cloud.kimball.home` for server-side verification.

**Server Actions — `cloud.kimball.home`:**

1. Verify edit propagated: Check if `renamed-from-webui.txt` shows 100 B content (was 60 B, edited to include "Edited by Windows11-TestDNC for TC5a.") via web UI or DB.
2. Verify delete propagated: Confirm `renamed-from-webui.txt` is no longer in the file listing (deleted from Windows11-TestDNC via DELETE API).
3. Verify create propagated: Check that `crud-test-2.txt` (72 B) appears in the file listing (uploaded via gRPC, NodeId=`019f05e4-a86b-74be-9acd-7bbd82dac181`).
4. If any server-side issues found, fix on server and relay back to Windows11-TestDNC for re-test.

**Remaining deferred items:**
- **Test Case 3** (batch conflict resolve) — needs multi-client setup
- **Test Case 4** (full-sync progress UI) — needs UI observation on client machine
