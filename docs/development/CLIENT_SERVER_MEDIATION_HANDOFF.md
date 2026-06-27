# Client/Server Mediation Handoff

Last updated: 2026-06-26 22:30 UTC (Windows sync architecture fully verified. Relay to mint-OptiPlex-7010 for Linux client validation.)

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
- ✅ **Test Case 3 (Batch conflict resolve)** — All 4 scenarios verified on Windows11-TestDNC. 3 bugs found and fixed (hash mismatch → text fallback in Strategy 1, placeholder base content in Strategy 3, missing ConflictCopyCreated download handler). See Active Handoff for full results.

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

**Summary:** Windows 11 sync architecture fully verified (all 5 test cases). Relay to `mint-OptiPlex-7010` for Linux client validation. The fixes are already pushed and merged — pull latest, rebuild SyncTray, and run the CRUD regression.

**Context:** 7 bugs found and fixed across the sync architecture testing on Windows11-TestDNC. All fixes are in `perf/synctray-scan-and-transfer-speedups`. Linux client needs to validate the same fixes work on Linux before the branch is merged to main.

---

### Client Actions — `mint-OptiPlex-7010`

**Setup:**
1. On `cloud.kimball.home`: cursor for device `mint-OptiPlex-7010` will be deleted to force full re-sync
2. On `mint-OptiPlex-7010`: `git pull`, rebuild SyncTray, wipe local state DB, restart

**Verify these operations work correctly on Linux:**

**1. Basic CRUD sync (regression):**
- Create a new file (e.g., `linux-test.txt`) with some text
- Verify it uploads and appears at `https://cloud.dotnetcloud.net/apps/files`
- Edit the file locally, save, verify edit propagates
- Delete the file locally, verify deletion propagates

**2. Rename/move sync:**
- Rename a synced file locally, wait for sync, verify name updates on server
- Rename a file via the web UI, wait for client poll, verify local file renames

**3. Conflict resolution:**
- Create `conflict-test.txt` with content `Base content` (via web UI on server)
- Wait for sync to client
- Kill SyncTray, edit local file to `Local Linux version`
- Edit same file via web UI to `Server version`
- Restart SyncTray
- **Expected:** Conflict copy created, server version wins on disk

**4. Batch resolve:**
- With conflict visible in SyncTray Conflicts tab, click "Resolve All"
- **Expected:** Conflict marked resolved, server version stays

---

### Server Actions — `cloud.kimball.home`

1. ✅ **Delete cursor** for `mint-OptiPlex-7010` device to force full re-sync:
   - Find device ID: `/opt/mssql-tools18/bin/sqlcmd -S hyperdrive.kimball.home -d DotNetCloud -U dotnetcloud -C -I -Q "SELECT Id, DeviceName FROM [core].[SyncDevices] WHERE DeviceName LIKE '%mint%optiplex%' OR DeviceName LIKE '%OPTIPLEX%';"`
   - Delete cursor: `/opt/mssql-tools18/bin/sqlcmd -S hyperdrive.kimball.home -d DotNetCloud -U dotnetcloud -C -I -Q "DELETE FROM [core].[SyncDeviceCursors] WHERE DeviceId = '<uuid>';"`
2. Create `linux-test.txt`, `conflict-test.txt` with initial content via web UI when client is ready
