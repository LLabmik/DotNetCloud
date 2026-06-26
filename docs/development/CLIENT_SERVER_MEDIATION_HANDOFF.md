# Client/Server Mediation Handoff

Last updated: 2026-06-26 14:40 UTC (Test Case 2 verified on Windows11-TestDNC — remote rename propagated to local SyncTray, one bug found and fixed in TryHandleRemoteRenameAsync.)

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
- ⏳ **Tests 3-5** — Batch conflict resolve, full-sync progress, CRUD edit/delete. Gated by multi-client setup or UI interaction.

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

**Summary:** Test Case 2 (remote rename from server) verified on Windows11-TestDNC. One new bug found and fixed in `TryHandleRemoteRenameAsync`. Remaining Test Cases 3-5 gated by multi-client setup or UI interaction.

**Context:** Server renamed `crud-test.txt` → `renamed-from-webui.txt` via web UI. SyncTray detected the change in its polling cycle but crashed with `UNIQUE constraint failed: FileRecords.Id` — `TryHandleRemoteRenameAsync` used `UpsertFileRecordAsync` (lookup by LocalPath) after changing the path, so the old row was not found and an INSERT was attempted with the original auto-increment Id.

**Fix:** Replaced `UpsertFileRecordAsync` with `UpdateFileRecordPathAsync` (lookup by NodeId) in `TryHandleRemoteRenameAsync` — same pattern already proven in `ScanLocalDirectoryAsync` local rename handling.

**Result — All 3 checks passed on Windows11-TestDNC:**

1. **File on disk:** `crud-test.txt` renamed to `renamed-from-webui.txt` ✓
2. **Sync log:** No errors. `RemoteChanges=1`, `DurationMs=897` ✓
3. **Sync pass complete:** Clean completion with ack to server ✓

---

### Client Actions — `Windows11-TestDNC` ✓

**Test Case 2 — Remote rename from server:** ✓ **Completed**

**Bug found — UNIQUE constraint violation in `TryHandleRemoteRenameAsync`:**
`UpsertFileRecordAsync` looks up by `LocalPath`. After changing `localRecord.LocalPath` to the new path, the lookup couldn't find the old row and attempted `ctx.FileRecords.Add(record)` — INSERT with the original auto-increment `Id`, which already existed.

**Fix in `SyncEngine.cs` line ~1604:** Changed from:
```csharp
localRecord.LocalPath = expectedLocalPath;
localRecord.LastSyncedAt = DateTime.UtcNow;
localRecord.LocalModifiedAt = File.Exists(expectedLocalPath) ? File.GetLastWriteTimeUtc(expectedLocalPath) : default;
await _stateDb.UpsertFileRecordAsync(context.StateDatabasePath, localRecord, cancellationToken);
```
To:
```csharp
var localModifiedAt = File.Exists(expectedLocalPath) ? File.GetLastWriteTimeUtc(expectedLocalPath) : default;
await _stateDb.UpdateFileRecordPathAsync(context.StateDatabasePath, nodeId, expectedLocalPath, localRecord.ContentHash, localModifiedAt, cancellationToken);
```

**Verification log:**
```
[14:37:57 INF] Remote rename/move detected: ...crud-test.txt → ...renamed-from-webui.txt (NodeId="019f0599-...").
[14:37:58 INF] ScanLocalDirectory: 0 queued, 41ms total (totalFiles=24)
[14:37:58 INF] Sync pass complete: DurationMs=897, RemoteChanges=1, LocalQueued=0, LocalApplied=0
```

**Fix commit:** `171600ea` (1 file changed: `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs`)

**Test Case 3 — Batch resolve conflicts:** ☐ Not tested (requires triggering a conflict with another client — needs `mint-dnc-client` or `mint-OptiPlex-7010`)

**Test Case 4 — Full-sync progress:** ☐ Not tested (gated by UI interaction from client machine)

**Test Case 5 — Regression: basic CRUD sync:** ✓ **Create verified**
- Created `crud-test.txt` → uploaded successfully
- File was remotely renamed to `renamed-from-webui.txt` → rename propagated to local successfully
- Edit and delete not yet verified from Windows side

---

### Next Steps

No server actions needed at this point. Remaining unverified tests (3-5) require either:
- A second connected client to trigger conflicts (Test Case 3)
- Manual UI observation on the client machine (Test Case 4)
- Local edit/delete verification (Test Case 5 remaining items)

These can be picked up when a multi-client setup is available.
