# Client/Server Mediation Handoff

Last updated: 2026-03-15 (Linux verification complete; handoff advanced to Windows — chain: mint-dnc-client → Windows11-TestDNC → mint22)

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
- **NEW (commit `4c575cc`):** Client-side upload dedup + echo suppression fixes committed to main.
- **Linux verification complete on `mint-dnc-client` (tests + rebuild + runtime checks).**
- **Active task: `Windows11-TestDNC` must rebuild SyncService/SyncTray with new Client.Core, deploy, and runtime-verify.** See Active Handoff below for full instructions.

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

### Step 2 of 3: Rebuild & Verify Windows Sync Client on `Windows11-TestDNC`

**Date:** 2026-03-15
**Owner:** `Windows11-TestDNC` agent
**Status:** ACTIVE
**Commit:** `4c575cc` — fix: client-side upload dedup + echo suppression

#### Prior Step Result (Step 1 Complete on `mint-dnc-client`)

- Linux verification is complete and archived in `docs/development/CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Client.Core tests passed (`164/164`).
- Linux SyncService + SyncTray were rebuilt and deployed from release payloads.
- Runtime evidence (single-context run) shows one `upload/initiate` per file event and no local conflict-copy creation for test files.

#### What Changed (already on `main`)

- Upload dedup in `LocalStateDb.QueueOperationAsync` to suppress duplicate pending operations for same file/node.
- Echo suppression in `SyncEngine.HandleRemoteUpdateAsync` to skip conflict resolver when local and remote hashes match.

**Files changed in fix commit (`4c575cc`):**
- `src/Clients/DotNetCloud.Client.Core/LocalState/LocalStateDb.cs` — dedup in `QueueOperationAsync`
- `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs` — echo suppression in `HandleRemoteUpdateAsync`
- `tests/DotNetCloud.Client.Core.Tests/LocalState/LocalStateDbTests.cs` — 4 new tests

#### Task for `Windows11-TestDNC`

1. `git pull origin main` and confirm commit includes `4c575cc`.
2. Run all Client.Core tests: `dotnet test tests/DotNetCloud.Client.Core.Tests/` — all must pass (including 4 new dedup tests).
3. Rebuild SyncService for Windows (`win-x64`):
  ```powershell
  dotnet publish src/Clients/DotNetCloud.Client.SyncService/DotNetCloud.Client.SyncService.csproj `
    -c Release -r win-x64 --self-contained `
    -o artifacts/desktop-client-staging/0.1.0-alpha/win-x64/payload/SyncService/
  ```
4. Rebuild SyncTray for Windows (`win-x64`):
  ```powershell
  dotnet publish src/Clients/DotNetCloud.Client.SyncTray/DotNetCloud.Client.SyncTray.csproj `
    -c Release -r win-x64 --self-contained `
    -o artifacts/desktop-client-staging/0.1.0-alpha/win-x64/payload/SyncTray/
  ```
5. Stop running desktop client processes and service.
  ```powershell
  Stop-Process -Name dotnetcloud-sync-tray -Force -ErrorAction SilentlyContinue
  Stop-Service DotNetCloudSync -ErrorAction SilentlyContinue
  ```
6. Deploy rebuilt binaries to install location (default: `$env:ProgramFiles\DotNetCloud\DesktopClient\SyncService` and `...\SyncTray`).
7. Restart service and tray.
  ```powershell
  Start-Service DotNetCloudSync
  & "$env:ProgramFiles\DotNetCloud\DesktopClient\SyncTray\dotnetcloud-sync-tray.exe"
  ```
8. **Runtime verification:**
  - Create a unique test file (for example `seq-test-windows-<timestamp>.txt`) in `C:\Users\benk\Documents\synctray`.
  - Optionally append one additional line after first upload to validate exactly one initiate per file event.
   - Watch logs for: exactly **one** `upload/initiate` request per file (no duplicates).
   - After upload completes, trigger another sync pass — verify **no** conflict copy is created (echo suppression working).
  - Capture timestamped log evidence for both behaviors from sync service logs (`%ProgramData%\DotNetCloud\logs\sync-service*.log` and/or service profile log path in use).

#### Exit Criteria

- All Client.Core tests pass on Windows.
- SyncService and SyncTray rebuilt and deployed for `win-x64`.
- Runtime log evidence shows: one `upload/initiate` per file event, no spurious conflict copies after echo.
- Commit/push handoff doc updates.

#### Chain Handoff

When done, update this document:
- Archive this step 2 block.
- Write **Step 3 of 3** Active Handoff targeting `mint22` confirming both Linux + Windows client verification results.
- Include concise pass/fail summary and any residual blockers.
- Push and provide relay message for moderator (must include target machine `mint22`).

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
