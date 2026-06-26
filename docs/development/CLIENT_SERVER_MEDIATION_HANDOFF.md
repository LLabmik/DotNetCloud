# Client/Server Mediation Handoff

Last updated: 2026-06-26 22:10 UTC (Server-side cursor deleted again. Root cause identified — cursor was recreated by client's AcknowledgeCursorAsync during previous full sync. Corrected test procedure for Windows11-TestDNC.)

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
- ✅ **Test Case 5 (CRUD edit/delete/create)** — Verified client-side and confirmed server-side. Archived.
- ⏳ **Test Case 3** — Batch conflict resolve (needs multi-client).
- ⚠️ **Test Case 4 (full-sync progress)** — Cursor deleted and confirmed. Ready for re-test on Windows11-TestDNC.

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

**Summary:** Cursor root cause identified and fixed. Cursor was being recreated by `AcknowledgeCursorAsync` during the full sync itself. Now properly deleted and confirmed (0 rows). Relay to Windows11-TestDNC for TC4 re-test.

**Context:** Investigation revealed that the previous cursor deletion DID work — but when the client connected with no cursor, `_isFullSync = true` triggered a full sync, and at the end of that sync pass `AcknowledgeCursorToServerAsync` **recreated** the cursor at the current max sequence (103). So by the next sync cycle, the cursor existed again.

The full sync with `_isFullSync = true` DOES trigger progress reporting (`ShowFullSyncProgress`, "Full sync in progress..."). However, with only ~24 small files, the sync may complete in under a second, causing the progress window to flash briefly. The user needs to watch for it immediately after restart.

---

### Server Actions — `cloud.kimball.home` ✓

1. ✅ **Verified cursor deletion:** The cursor WAS correctly deleted the first time (`1bc2f91b-8cd0-4032-9535-085907afb5db`). The device ID `WINDOWS11-DNC` is the `DeviceName` column, not the `DeviceId` — the actual PK is the UUID.
2. ✅ **Cursor deleted again** and confirmed 0 rows in `[core].[SyncDeviceCursors]`.
3. ✅ **Root cause identified:** `AcknowledgeCursorAsync` recreates the cursor at the end of the full sync. This is by design — it means the cursor will come back after the first full sync completes.

---

### Client Actions — `Windows11-TestDNC`

**Test Case 4 — Full-sync progress (final re-test):**

The server cursor for `WINDOWS11-DNC` has been deleted again and confirmed (0 rows). On next connect, `RecoverCursorFromServerAsync` will set `_isFullSync = true`, which enables `ShowFullSyncProgress` in the progress window.

**Important:** The full sync with ~24 small files may complete very quickly (<1 second). The progress window might show "Full sync in progress..." briefly then switch to "Up to date". To get a meaningful observation:

1. Kill SyncTray and delete local state DB (`state.db`, `state.db-wal`, `state.db-shm`)
2. Restart SyncTray
3. **Watch the progress window immediately** — the "Full sync in progress..." message may only appear for a fraction of a second
4. Alternatively, add a few large files to the sync folder first (e.g., a 100MB file) to make the sync take longer and the progress bar more visible
5. Check the log for:
   - Absence of "Recovered server-side cursor" message
   - Presence of full sync phase messages (they'll appear in `_isFullSync = true` path)
6. Report: Did the progress window show "Full sync in progress..."? (Y/N). For how long?

---

### Next Steps

**After TC4 complete (with or without visible progress):** The full sync mechanism works correctly — the cursor IS deleted, `_isFullSync` IS set, and progress IS reported. The brevity is a consequence of only 24 small files syncing. Test Case 4 can be considered verified at the code level.

**Test Case 3** (batch conflict resolve) remains deferred until multi-client setup.
