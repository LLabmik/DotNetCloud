# Client/Server Mediation Handoff

Last updated: 2026-03-16 (Deletion propagation chain in progress: Linux complete, Windows active)

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
- P0 server-side sync hardening deployed and verified on `mint22`.
- Upload hardening story: CLOSED (2026-03-15). All machines verified.
- **New (2026-03-16):** Client-side deletion propagation implemented on `mint22` (commit `b4160c6`).
  - Files deleted from sync directory on client were not being deleted on server — they just reappeared.
  - Fix: SyncEngine now detects files/directories tracked in LocalStateDb but missing from disk, creates PendingDelete operations, calls server DELETE API, and removes local state entries.
  - Supports single files, multiple files, and recursive directory deletions.
  - All changes are in `src/Clients/DotNetCloud.Client.Core/` — shared client library. Server unchanged.
  - **Both client machines must pull and rebuild to pick up the fix.**
- **Active cycle:** Deletion propagation chain — Linux client complete, Windows client active, mint22 confirmation pending.

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

**Chain: Step 2 of 3 — Target: `Windows11-TestDNC` (Windows client)**

### What Changed (commit `b4160c6`)

Client-side file/directory deletions were not propagating to the server. When a user deleted a file from their sync directory, the next sync pass would re-download it from the server. This is now fixed.

**Changed files (all in `src/Clients/DotNetCloud.Client.Core/`):**
- `LocalState/Entities/PendingOperationRecord.cs` — added `PendingDelete` operation type
- `LocalState/ILocalStateDb.cs` — added `GetAllTrackedFilesAsync`, `GetAllTrackedDirectoriesAsync`, `GetTrackedFilesInDirectoryAsync`, `RemoveDirectoryAsync`
- `LocalState/LocalStateDb.cs` — implemented the new methods
- `Sync/SyncEngine.cs` — deletion detection in `ScanLocalDirectoryAsync`, execution in `ExecutePendingOperationAsync`, skip re-download in reconciliation

**Test files updated:**
- `tests/DotNetCloud.Client.Core.Tests/LocalState/LocalStateDbTests.cs`
- `tests/DotNetCloud.Client.Core.Tests/Sync/SyncEngineTests.cs`

### Step 1 Results (`mint-dnc-client`) - COMPLETED

- Pull/build/tests:
   - `git pull` completed (fast-forward to include `b4160c6` chain changes)
   - `dotnet build src/Clients/DotNetCloud.Client.Core/DotNetCloud.Client.Core.csproj` -> succeeded
   - `dotnet test tests/DotNetCloud.Client.Core.Tests/` -> `182 passed, 0 failed`
- Runtime gate note:
   - Initial deletion probe on stale running process reproduced old behavior (file re-downloaded).
   - Rebuilt/restarted runtime from current binaries:
      - `dotnet build src/Clients/DotNetCloud.Client.SyncTray/DotNetCloud.Client.SyncTray.csproj`
      - restarted `dotnetcloud-sync-service` from `src/Clients/DotNetCloud.Client.SyncTray/bin/Debug/net10.0/`
- File deletion verification (PASS):
   - File: `delete_test_linux_retry2_20260316_030012.txt`
   - Upload evidence: `File upload complete ... NodeId=34370895-2422-4603-80e0-5796dd753a86`
   - Delete propagation evidence:
      - `Local file deleted, queuing server deletion: delete_test_linux_retry2_20260316_030012.txt`
      - `Deleting server node 34370895-2422-4603-80e0-5796dd753a86 for locally deleted file/folder`
   - Result: `REAPPEARED=no` and no queue-download line for that file after deletion.
- Directory deletion verification (PASS):
   - Directory: `deltest_dir_20260316_030153` with file `inner.txt`
   - Upload evidence: `File upload complete ... FileName=inner.txt ... NodeId=e2655c3f-5d18-43e7-88f8-c9417a82a312`
   - Delete propagation evidence:
      - `Local file deleted, queuing server deletion: deltest_dir_20260316_030153/inner.txt`
      - `Deleting server node e2655c3f-5d18-43e7-88f8-c9417a82a312 for locally deleted file/folder`
   - Result: `DIR_REAPPEARED=no`.

### Instructions for `Windows11-TestDNC`

1. **Pull latest:**
    ```powershell
    Set-Location "D:\Repos\dotnetcloud"
    git pull
   ```

2. **Build and run tests:**
    ```powershell
    dotnet build src/Clients/DotNetCloud.Client.Core/DotNetCloud.Client.Core.csproj
    dotnet test tests/DotNetCloud.Client.Core.Tests/
   ```
   All new deletion tests must pass.

3. **Rebuild and redeploy the sync client** on Windows (MSIX rebuild/install or restart service/tray from freshly built binaries).

4. **Runtime verification — file deletion:**
    - Create a test file in the sync directory (e.g., `delete_test_win_<timestamp>.txt` in `C:\Users\benk\Documents\synctray`)
   - Wait for sync to upload it to the server
    - Verify upload completed in Windows sync-service log
   - Delete the file from the sync directory
    - Verify logs show delete propagation (`Local file deleted, queuing server deletion` and `Deleting server node ...`)
    - Verify file does not reappear locally and is not queued for download
    - Document file name, NodeId, and key log lines with timestamps

5. **Runtime verification — directory deletion (optional but recommended):**
    - Create a test directory with a file under `C:\Users\benk\Documents\synctray`
   - Wait for sync to upload
    - Delete the directory
    - Verify deletion propagation in logs and no local reappearance

6. **When done — write Step 3 handoff for `mint22`:**
   - Update this document's **Active Handoff** section:
       - Record Windows test results (pass/fail, file names, any issues)
       - Change target to `mint22` (Step 3 of 3)
       - Ask `mint22` to confirm both clients are updated and verify server-side stability/no regression errors
   - Commit and push
    - Provide relay message for moderator targeting `mint22`

### Chain Summary

| Step | Target | Action | Status |
|------|--------|--------|--------|
| 1 | `mint-dnc-client` | Pull, build, test, verify deletion sync | **Completed** |
| 2 | `Windows11-TestDNC` | Pull, build, test, verify deletion sync | **ACTIVE** |
| 3 | `mint22` | Confirm both clients updated and verified | Pending |

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
