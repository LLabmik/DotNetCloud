# Client/Server Mediation Handoff

Last updated: 2026-06-26 22:25 UTC (TC3 plan ready — server web UI as Client B. 4 conflict scenarios: identical, fast-forward, text merge, conflict copy + batch resolve.)

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
- ✅ **Test Case 4 (full-sync progress)** — Full sync mechanism confirmed working. Archived.
- ⏳ **Test Case 3** — Batch conflict resolve. Plan ready — see Active Handoff.

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

**Summary:** Test Case 3 — Batch Conflict Resolve. Server web UI acts as Client B to trigger conflicts. Approach: edit file locally on Windows11-TestDNC while SyncTray is dead, then edit same file via web UI to create server-side change, then restart SyncTray → conflict detected on next sync.

**Context:** TC4 archived. All other tests complete. This is the last remaining sync architecture test.

---

### Server Actions — `cloud.kimball.home`

**Setup — Create test files via web UI:**

Create each file below via the web UI at `https://cloud.dotnetcloud.net/apps/files` using the "New File" button:

1. **`tc3-identical.txt`** with content `Hello world`
2. **`tc3-fastforward.txt`** with content `Base content`
3. **`tc3-merge.txt`** with content:
   ```
   Line 1: alpha
   Line 2: beta
   Line 3: gamma
   ```
4. **`tc3-conflict.txt`** with content `Original content`

Wait for both files to sync to Windows11-TestDNC before proceeding.

---

### Client Actions — `Windows11-TestDNC`

**Test A1 — Identical Content (Strategy 1):**

1. Verify `tc3-identical.txt` has synced locally with content `Hello world`
2. **Kill SyncTray** (so local edits don't upload)
3. Edit `tc3-identical.txt` → change content to `Modified same on both`
4. **Server (`cloud.kimball.home`):** Edit `tc3-identical.txt` via web UI → same content: `Modified same on both`
5. **Restart SyncTray**
6. **Expected:** Auto-resolved (hashes match). No conflict. Report result.

**Test A2 — Fast-Forward (Strategy 2):**

1. Verify `tc3-fastforward.txt` has synced with content `Base content`
2. **Kill SyncTray**
3. Edit `tc3-fastforward.txt` → no change needed (leave as `Base content`)
4. **Server (`cloud.kimball.home`):** Edit `tc3-fastforward.txt` via web UI → `Server changed this`
5. **Restart SyncTray**
6. **Expected:** Auto-resolved (local hash = base hash → fast-forward). Server version wins locally. Report result.

**Test A3 — Non-Overlapping Text Merge (Strategy 3):**

1. Verify `tc3-merge.txt` has synced with all 3 lines
2. **Kill SyncTray**
3. Edit `tc3-merge.txt` → change line 1 to `Line 1: alpha-modified`
4. **Server (`cloud.kimball.home`):** Edit `tc3-merge.txt` via web UI → change line 3 to `Line 3: gamma-modified`
5. **Restart SyncTray**
6. **Expected:** DiffPlex three-way merge succeeds. Both edits survive. No conflict copy. Report result.

**Test B1 — Conflict Copy (Strategy 5):**

1. Verify `tc3-conflict.txt` has synced with content `Original content`
2. **Kill SyncTray**
3. Edit `tc3-conflict.txt` → change content to `Client A's version`
4. **Server (`cloud.kimball.home`):** Edit `tc3-conflict.txt` via web UI → change content to `Server's version`
5. **Restart SyncTray**
6. **Expected:** Auto-resolution fails (same content area, different text). Conflict copy created named `tc3-conflict (conflict - ...).txt`. Conflict record saved in local state DB.
7. **Check SyncTray Conflicts tab** — should show 1 unresolved conflict.

**Test C1 — Batch Resolve:**

1. With conflicts visible in SyncTray Conflicts tab, click **"Resolve All"** button
2. **Expected:** All conflict records marked resolved with `"keep-server"`. Server version wins.
3. Verify `tc3-conflict.txt` now contains `Server's version`
4. Conflict copy `tc3-conflict (conflict - ...).txt` remains as a separate file
5. Report: How many conflicts were batch-resolved? Did server version propagate correctly?

---

### Next Steps

**After TC3 complete:** All 5 sync architecture tests verified. Relay to `cloud.kimball.home` for final summary and archive.

| Test | Description | Status |
|------|-------------|--------|
| TC1 | Local rename/move sync | ✅ 3 bugs fixed |
| TC2 | Remote rename from server | ✅ 1 bug fixed |
| TC3 | Batch conflict resolve | ☐ |
| TC4 | Full-sync progress | ✅ Archived |
| TC5 | CRUD edit/delete/create | ✅ Archived |
