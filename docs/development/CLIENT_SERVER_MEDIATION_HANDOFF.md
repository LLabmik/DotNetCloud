# Client/Server Mediation Handoff

Last updated: 2026-03-15 (Upload hardening story fully closed — all machines verified)

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
- **Active cycle:** Chained handoff — Linux client → Windows client → mint22 confirmation.

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

**Chain: Step 1 of 3 — Target: `mint-dnc-client` (Linux client)**

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

### Instructions for `mint-dnc-client`

1. **Pull latest:**
   ```bash
   cd ~/Repos/dotnetcloud && git pull
   ```

2. **Build and run tests:**
   ```bash
   dotnet build src/Clients/DotNetCloud.Client.Core/DotNetCloud.Client.Core.csproj
   dotnet test tests/DotNetCloud.Client.Core.Tests/ 2>&1 | tail -20
   ```
   All new deletion tests must pass.

3. **Rebuild and redeploy the sync client** (use whatever the current deployment method is on this host — rebuild SyncTray or restart the service).

4. **Runtime verification — file deletion:**
   - Create a test file in the sync directory (e.g., `echo "delete-test" > ~/synctray/delete_test_linux_$(date +%s).txt`)
   - Wait for sync to upload it to the server
   - Verify it appears on the server (check server logs or web UI)
   - Delete the file from the sync directory
   - Wait for the next sync pass
   - Verify the file is **deleted on the server** (not re-downloaded to client)
   - Document the test file name and results

5. **Runtime verification — directory deletion (optional but recommended):**
   - Create a test directory with a file: `mkdir ~/synctray/deltest_dir && echo "inner" > ~/synctray/deltest_dir/inner.txt`
   - Wait for sync to upload
   - Delete the directory: `rm -rf ~/synctray/deltest_dir`
   - Wait for sync pass
   - Verify directory and contents deleted on server

6. **When done — write handoff for Windows:**
   - Update this document's **Active Handoff** section:
     - Record Linux test results (pass/fail, file names, any issues)
     - Change target to `Windows11-TestDNC` (Step 2 of 3)
     - Keep the same "What Changed" and similar instructions adapted for Windows
     - Windows instructions: pull, build, run tests, rebuild MSIX or restart service, same deletion verification
     - After Windows completes, it should write Step 3 targeting `mint22` with confirmation results
   - Commit and push
   - Provide relay message for moderator targeting `Windows11-TestDNC`

### Chain Summary

| Step | Target | Action | Status |
|------|--------|--------|--------|
| 1 | `mint-dnc-client` | Pull, build, test, verify deletion sync | **ACTIVE** |
| 2 | `Windows11-TestDNC` | Pull, build, test, verify deletion sync | Pending |
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
