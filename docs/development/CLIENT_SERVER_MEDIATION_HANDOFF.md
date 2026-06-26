# Client/Server Mediation Handoff

Last updated: 2026-06-26 21:30 UTC (Sync architecture flow review — rename/move sync, batch conflict resolve, full-sync progress, sync flow reference doc. Ready for Windows 11 client testing.)

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
- ⏳ **Windows 11 client verification pending** — See Active Handoff for test cases.

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

**Summary:** Sync architecture flow review implemented on server (cloud). Ready for Windows 11 client verification. See `docs/development/SYNC_FLOW_REFERENCE.md` for the full sync flow reference.

**Context:** Four sync architecture improvements implemented and tested on server:

1. **Explicit rename/move sync** — `TryHandleRemoteRenameAsync` (receiver side: rename local file when server path differs) + `ScanLocalDirectoryAsync` rename detection (sender side: content-hash match → server RenameAsync instead of delete+create)
2. **Batch conflict resolve** — "Resolve All" button in conflicts tab
3. **Full-sync progress reporting** — Engine tracking + progress bar in SyncProgressWindow
4. **Sync flow reference doc** — `docs/development/SYNC_FLOW_REFERENCE.md`

All built and tested on server (0 errors, 1104 tests passing across 3 test projects). Need client-side validation.

---

### Client Actions — `Windows11-TestDNC`

**Setup:**
1. Pull latest from `perf/synctray-scan-and-transfer-speedups` (this branch)
2. Build SyncTray: `dotnet build src\Clients\DotNetCloud.Client.SyncTray\DotNetCloud.Client.SyncTray.csproj -c Release`
3. Stop running SyncTray if active
4. Launch the newly built SyncTray and let initial sync complete

**Test Case 1 — Rename/move sync:**
1. On Windows 11, rename a file in the sync folder (e.g. `test.txt` → `renamed.txt`)
2. Verify the file appears at the new name on the server (check via web UI or server file listing)
3. On another client (e.g. Linux), verify the file appears with the new name — NOT a delete+create
4. Repeat with moving a file to a subfolder

**Test Case 2 — Remote rename from server:**
1. Rename a file via server web UI
2. On Windows 11, verify the local file is renamed to match (not re-downloaded)

**Test Case 3 — Batch resolve conflicts:**
1. Trigger a conflict (edit same file on two clients while offline)
2. Open Settings → Conflicts tab
3. Verify "Resolve All" button appears when conflicts exist
4. Click "Resolve All" — verify all conflicts resolved with "keep-server"

**Test Case 4 — Full-sync progress:**
1. Delete (or rename) the local `state.db` file in the sync folder to force a full re-sync
   - Location: check the sync settings for the DB path, or
   - Stop SyncTray, delete `local_state.db` from the sync data directory, restart SyncTray
2. Verify the Sync Progress window shows:
   - "Full sync in progress" or similar phase label
   - A progress bar indicating items completed vs total
   - The phase label updating through "Fetching server file list…" → "Scanning local changes…" → "Syncing N files…"

**Test Case 5 — Regression: basic CRUD sync:**
1. Create a new file on Windows 11 → verify it appears on server and other clients
2. Edit an existing file → verify changes propagate
3. Delete a file → verify it's removed from server and other clients

**Reporting:**
- Document any failures with exact error messages / screenshots
- Note any UX issues (progress bar not updating, missing labels, etc.)

---

### Server Actions — `cloud.kimball.home`

No server-side changes needed. All changes are client-side. If server API issues are discovered during testing (e.g., `RenameAsync` returning unexpected errors), update this handoff with details.
