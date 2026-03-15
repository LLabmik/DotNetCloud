# Client/Server Mediation Handoff

Last updated: 2026-03-14 (client-side upload dedup + echo suppression — chained handoff: mint-dnc-client → Windows11-TestDNC → mint22)

Purpose: shared handoff between client-side and server-side agents, mediated by user.

Archived context:
- Historical completed updates are in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Additional history remains available in git.

## Process Rules

**Agent autonomy (CRITICAL):**
- Both client and server agents work autonomously — they do NOT ask the moderator for context or permission.
- Agents pull the latest `main`, read the **Active Handoff** section, and execute the work described there independently.
- All actionable items, blockers, and technical details go directly in this document (committed to `main`).
- No moderator involvement in technical decisions, code reviews, or work coordination.

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
- Moderator handoff prompt rule (MANDATORY): every ready-to-relay message must explicitly state the target machine name (for example: `mint22`, `mint-dnc-client`, `Windows11-TestDNC`).
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

- All prior Phase 2, chat, and pre-Linux sync remediation work is complete and archived.
- Linux bring-up engineering fixes are complete.
- P0 sync hardening (atomic SyncSequence, unique name constraint, atomic chunk refcount) deployed.
- **Server gzip fix deployed (af66b41):** `UseRequestDecompression()` middleware added. Server now auto-decompresses `Content-Encoding: gzip` request bodies before controllers read them. The chunk PUT hash mismatch that caused false 409 is resolved.
- **Upload complete 500 fixed:** `SyncCursorHelper.AssignNextSequenceAsync` was calling `.SingleAsync()` on non-composable raw SQL. EF Core 10 rejects LINQ composition on `SqlQueryRaw` with `RETURNING`. Fixed by materializing with `.ToListAsync()` then `.Single()`.
- Client re-verification complete on `mint-dnc-client`: fresh upload now succeeds with initiate `201` -> chunk `200` -> complete `200`.
- Optional sanity retry complete on `mint-dnc-client`: fresh file upload again validated (`201` -> `200` -> `200`) with duplicate-name `409` behavior and no `500`.
- Active task: standby monitoring only; no active cross-machine upload blocker.

## Environment

| Role | Machine | Detail |
|---|---|---|
| Server | `mint22` | `https://mint22:15443/` |
| Client | `Windows11-TestDNC` | Sync dir: `C:\Users\benk\Documents\synctray` |
| Client | `mint-dnc-client` | Linux Mint 22 validation host for desktop sync client implementation + E2E testing |

## Key Carry-Forward Contracts

- Auth: OpenIddict bearer on files/sync endpoints via `FilesControllerBase` `[Authorize]`.
- API envelope: middleware wraps responses; clients should unwrap via envelope helpers.
- Sync flow: changes -> tree -> reconcile -> chunk manifest -> chunk download -> file assembly.
- Desktop OAuth constant: `OAuthConstants.ClientId = "dotnetcloud-desktop"`.

## Active Handoff

### Step 1 of 3: Rebuild & Verify Linux Sync Client on `mint-dnc-client`

**Date:** 2026-03-14
**Owner:** `mint-dnc-client` agent
**Status:** ACTIVE
**Commit:** `4c575cc` — fix: client-side upload dedup + echo suppression

#### What Changed (Server Agent on `mint22`)

Two client-side bugs were found and fixed in `DotNetCloud.Client.Core`:

1. **Upload dedup missing** (`LocalStateDb.QueueOperationAsync`): No dedup check existed. Multiple sync triggers (FileSystemWatcher, periodic scan, SyncNow IPC) all independently queued the same file. Linux client fired 6 concurrent `upload/initiate` for one file, Windows fired 21+. **Fixed:** Before inserting a Pending operation, check if an identical one already exists (same `LocalPath` for uploads, same `NodeId` for downloads). Skip with debug log if duplicate.

2. **Echo-triggered conflict copies** (`SyncEngine.HandleRemoteUpdateAsync`): When client A uploads a file, the server's `sync/changes` feed echoes it back. Client A's next sync pass saw the echo, entered the conflict resolver, and created a spurious conflict copy (e.g., `seq-test-windows (conflict - WINDOWS11-DNC$ - 2026-03-14).txt`). **Fixed:** Before entering conflict resolver, compare local file content hash with remote change `ContentHash`. If they match, update the record as "Synced" and return early (echo suppression).

**Files modified:**
- `src/Clients/DotNetCloud.Client.Core/LocalState/LocalStateDb.cs` — dedup in `QueueOperationAsync`
- `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs` — echo suppression in `HandleRemoteUpdateAsync`
- `tests/DotNetCloud.Client.Core.Tests/LocalState/LocalStateDbTests.cs` — 4 new tests

**Server DB cleanup already done on `mint22`:**
- 121 stale InProgress upload sessions deleted
- Spurious conflict copy FileNode (`149f66ce`) soft-deleted

#### Task for `mint-dnc-client`

1. `git pull origin main` to get commit `4c575cc`.
2. Run all Client.Core tests: `dotnet test tests/DotNetCloud.Client.Core.Tests/` — all must pass (including 4 new dedup tests).
3. Rebuild the SyncService for Linux:
   ```bash
   dotnet publish src/Clients/DotNetCloud.Client.SyncService/DotNetCloud.Client.SyncService.csproj \
     -c Release -r linux-x64 --self-contained \
     -o artifacts/desktop-client-staging/0.1.0-alpha/linux-x64/payload/SyncService/
   ```
4. Rebuild the SyncTray for Linux:
   ```bash
   dotnet publish src/Clients/DotNetCloud.Client.SyncTray/DotNetCloud.Client.SyncTray.csproj \
     -c Release -r linux-x64 --self-contained \
     -o artifacts/desktop-client-staging/0.1.0-alpha/linux-x64/payload/SyncTray/
   ```
5. Stop running sync client processes if any.
6. Deploy rebuilt binaries to the sync client install location (wherever SyncService/SyncTray are installed on `mint-dnc-client`).
7. Start the sync client.
8. **Runtime verification:**
   - Delete and re-add a test file (e.g., `seq-test-linux.txt`) to the sync folder.
   - Watch logs for: exactly **one** `upload/initiate` request per file (no duplicates).
   - After upload completes, trigger another sync pass — verify **no** conflict copy is created (echo suppression working).
   - Capture timestamped log evidence of both behaviors.

#### Exit Criteria

- All Client.Core tests pass.
- SyncService and SyncTray rebuilt and deployed.
- Runtime log evidence shows: single upload per file (no dedup spam), no spurious conflict copies after echo.
- Commit any local changes (if needed) and push.

#### Chain Handoff

When done, update this document:
- Archive this step 1 block.
- Write a new **Active Handoff** targeting `Windows11-TestDNC` with the same task (steps 2-8 adapted for Windows: `win-x64` RID, PowerShell commands, Windows paths). The Windows agent should:
  - Pull main, run tests, rebuild SyncService + SyncTray for `win-x64`, redeploy, runtime verify.
  - When Windows is done, write a final Active Handoff back to `mint22` confirming both clients verified.
- Push and provide relay message for moderator.

## Relay Template

```markdown
### Send to [Server|Client] Agent on <target-machine>
<message text including target machine>

### Request Back
- commit hash
- raw endpoint/URL used
- raw error/query params
- raw log lines around the event (with timestamp)
```
