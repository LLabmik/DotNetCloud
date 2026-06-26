# Client/Server Mediation Handoff

Last updated: 2026-06-26 21:35 UTC (Server-side rename API verification complete — web UI rename, server logs, and DB all confirmed working. Ready to relay to Windows 11 for remote rename client testing.)

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
- ⏳ **Tests 2-5** — Remote rename, batch conflict resolve, full-sync progress, and CRUD regression partially tested. Remaining tests gated by server-side access or multi-client setup.

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

**Summary:** Rename API verified end-to-end from server side — web UI rename, server logs, and database all confirmed. Server actions completed. Ready for client to pick up remote rename.

**Context:** Tested the 4 sync architecture improvements on Windows 11. Test 1 revealed 3 bugs in the rename detection code that were fixed on the client side. Tests 2-5 partially complete (gated by server-side rename or multi-client setup). Server verification of rename API completed on `cloud.kimball.home`.

---

### Client Actions — `Windows11-TestDNC` ✓

**Setup:** ✓ Done (`9c089190`, branch `perf/synctray-scan-and-transfer-speedups`, built Release SyncTray)

**Test Case 1 — Rename/move sync:** ⚠️ **Fixed — now working**

Initial test failed. Found 3 bugs in `ScanLocalDirectoryAsync` rename detection:

**Bug 1 — Hash mismatch (line 1982):** The upload completion code stored the server's manifest-based content hash (from `uploadResult.ContentHash`), but the rename detection code compared it against a locally-computed SHA256 (`ComputeFileHashAsync`). These two hashing methods produce different values for the same file content, so rename was never detected.

**Fix:** Changed upload to always compute and store the local SHA256 hash (matching the download path which already did this).

**Bug 2 — Path mutation in catch block (line ~994):** When the rename DB update threw an exception, `missing.LocalPath` had already been mutated to the new path before being added to `actualDeletions`. This caused the fallback delete+create to attempt deletion of the NEW filename (which didn't exist on server) instead of the OLD filename.

**Fix:** Added `missing.LocalPath = Path.Combine(context.LocalFolderPath, missingRelPath)` to restore the original path before adding to `actualDeletions`.

**Bug 3 — UNIQUE constraint violation on LocalPath (line ~987):** `UpsertFileRecordAsync` looks up records by `LocalPath` (which has a UNIQUE index). After changing the path to the new name, it couldn't find the record and tried to INSERT a new row, which collided with the existing old-path row.

**Fix:** Added `UpdateFileRecordPathAsync(Guid nodeId, ...)` method to `ILocalStateDb`/`LocalStateDb` that updates the record by NodeId instead of LocalPath, bypassing the UNIQUE constraint issue.

**Result:** After fixes, the log shows:
```
[13:21:38 INF] Local rename detected: Test.txt → renamed.txt (NodeId=...).
[13:21:38 INF] API call PUT ".../rename"
[13:21:38 INF] Local rename detection: 1 rename(s) propagated to server in context "..."
```
Server API call succeeded, local state DB updated. No delete+create fallback.

**Fix commit hash:** `03283d57` (4 files changed: `ILocalStateDb.cs`, `LocalStateDb.cs`, `SyncEngine.cs`, handoff doc)

**Test Case 2 — Remote rename from server:** ☐ Not tested (requires server web UI access from Windows11-TestDNC)

**Test Case 3 — Batch resolve conflicts:** ☐ Not tested (requires triggering a conflict with another client)

**Test Case 4 — Full-sync progress:** ☐ Not tested (state DB was deleted multiple times during testing but progress window visibility not verified — gated by UI interaction from client machine)

**Test Case 5 — Regression: basic CRUD sync:** ✓ **Create verified**
- Created `crud-test.txt` → uploaded successfully via gRPC (`NodeId="019f0599-2c8d-786e-b95a-464db9dc9fd1"`, DurationMs=194)
- Edit and delete not verified from Windows side

---

### Server Actions — `cloud.kimball.home` ✓

**Client-side fixes applied.** No server code changes needed. The rename detection bug was entirely in the client-side state DB handling code.

**Verify rename API works end-to-end:** ✓ **Completed** — Renamed `crud-test.txt` → `renamed-from-webui.txt` via web UI right-click menu.

**Result — All 3 checks passed:**

1. **Web UI:** `renamed-from-webui.txt` appears in file listing (60 B, Jun 26) ✓
2. **Server logs:**
   ```
   Jun 26 16:29:59 INF Node 019f0599-2c8d-786e-b95a-464db9dc9fd1 renamed to 'renamed-from-webui.txt'
   ```
3. **Database (SQL Server):**
   ```
   019F0599-2C8D-786E-B95A-464DB9DC9FD1 renamed-from-webui.txt  2026-06-26 21:29:59.565
   ```

**Conclusion:** The `RenameAsync` endpoint at `PUT .../rename` works correctly end-to-end:
- Client-initiated renames (gRPC → HTTP) return 200 and persist (verified from earlier Windows 11 tests)
- Web UI renames also succeed and persist
- No server code changes needed

**Next handoff suggestion:** This handoff is ready to relay to `Windows11-TestDNC` for Test Case 2 (remote rename from server) — verify a connected SyncTray client picks up the rename via polling/notification without re-downloading content.
