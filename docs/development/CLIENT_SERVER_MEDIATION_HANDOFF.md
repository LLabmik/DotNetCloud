# Client/Server Mediation Handoff

Last updated: 20260329 (WS-4 — TC-1.54 retest handoff for Windows after file stability code changes)

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
- Deletion propagation story: **CLOSED** (2026-03-16). All three machines verified.
  - Linux client (`mint-dnc-client`): verified 2026-03-16 ~03:00Z
  - Windows client (`Windows11-TestDNC`): verified 2026-03-16 ~08:16Z. Bug fixed: `RemoveFileRecordsUnderPathAsync` path separator on Windows.
  - Server (`mint22`): confirmed stable 2026-03-16. Zero ERR entries, both nodes soft-deleted, no 5xx.
- Duplicate controller fix: CLOSED (2026-03-18). Deployed and verified on `mint22`. Files endpoint returns 401, service healthy.
- Windows IIS + Service Validation: **COMPLETE** (2026-03-21). Three startup blockers resolved. IIS reverse proxy configured and verified (URL Rewrite + ARR). HTTP (port 80) and HTTPS (port 443) both proxy to Kestrel :5080. Self-signed localhost cert bound.
- File browser child count fix: **DEPLOYED** (2026-03-21). `mint22` redeployed; service stable.
- `mint22` connectivity diagnosis: **COMPLETE** (2026-03-22). Current deployment listens directly on HTTPS `:5443`; no listener exists on `:15443`.
- Security audit desktop client validation on `Windows11-TestDNC`: **COMPLETE** (2026-03-23).
- Security audit closeout + merge validation on `mint22`: **COMPLETE** (2026-03-23).
- Post-closeout Windows runtime smoke: **COMPLETE** (2026-03-23). 4/4 targeted tests passed; login launch path verified reachable.
- **Active cycle (20260328–20260329):** WS-4 live verification 58/66 passed. Windows Phase C complete (8 pass, 2 deferred, 1 skip). Linux Phase C complete (10 pass, 1 skip). Both deferred Windows tests (TC-1.52 conflict, TC-1.53 offline) passed on Linux.
- **Current (20260329):** TC-1.54 retest on Windows after `SyncEngine` file stability check code changes (commit `38c32f4`).

## Environment

| Role | Machine | Detail |
|---|---|---|
| Server | `mint22` | `https://mint22:5443/` |
| Client | `Windows11-TestDNC` | Sync dir: `C:\Users\benk\Documents\synctray` |
| Client | `mint-dnc-client` | Linux Mint 22 validation host for desktop sync client implementation + E2E testing |
| Android Client | `monolith` | Android MAUI app development + emulator testing (Windows 11) |

## Key Carry-Forward Contracts

- Auth: OpenIddict bearer on files/sync endpoints via `FilesControllerBase` `[Authorize(AuthenticationSchemes = "OpenIddict.Validation.AspNetCore")]`.
- API envelope: middleware wraps responses; clients should unwrap via envelope helpers.
- Sync flow: changes -> tree -> reconcile -> chunk manifest -> chunk download -> file assembly.
- Desktop OAuth constant: `OAuthConstants.ClientId = "dotnetcloud-desktop"`.
- **SignalR channel group naming:** `chat-channel-{channelId}` (used by `ChatRealtimeService.ChannelGroup()` and Android `SignalRChatClient`).
- **Controller discovery:** Core.Server references Files.Host and Chat.Host via `ProjectReference`. ASP.NET Core auto-discovers controllers from referenced assemblies. Do NOT create duplicate controllers in Core.Server for routes already served by module Host assemblies.

## Active Handoff

**Target machine:** `Windows11-TestDNC`
**Status:** READY
**Context:** WS-4 — Retest TC-1.54 (100MB+ file upload) after file stability code changes
**Summary:** Linux testing revealed that large files generated with `dd` could be caught mid-write by the FSW. Code changes were made to `SyncEngine` to add a pre-upload file stability check. Windows needs to retest TC-1.54 to confirm the new behavior works correctly on Windows.

### Objective

Retest **TC-1.54** (100MB+ file upload through sync) on Windows. The SyncEngine now includes a `WaitForFileStabilityAsync()` check that samples file size before and after a 1-second delay. If the file is still growing, it throws `FileStillGrowingException` and defers the upload without consuming retry budget.

**What changed (commit `38c32f4`):**
- New: `src/Clients/DotNetCloud.Client.Core/Platform/FileStillGrowingException.cs`
- Modified: `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs`
  - Added `FileStabilityDelay` property (1s default)
  - Added `WaitForFileStabilityAsync()` — checks file size before/after delay
  - Added catch handler for `FileStillGrowingException` — defers upload, retries in 5s, does NOT consume retry count
- New: `tests/DotNetCloud.Client.Core.Tests/Sync/SyncEngineTests.cs` — unit tests for stability check

**Server:** `https://mint22:5443/`
**Test account:** `testdude@llabmik.net` / `TestMilk01!`
**OAuth client_id:** `dotnetcloud-desktop`

### Prerequisites

1. Pull latest `main`:
   ```powershell
   cd C:\Users\benk\Repos\dotnetcloud
   git pull
   ```
2. Build SyncTray:
   ```powershell
   dotnet build src\Clients\DotNetCloud.Client.SyncTray\DotNetCloud.Client.SyncTray.csproj
   ```
3. Ensure `mint22:5443` is reachable from Windows11-TestDNC.
4. Sync directory: `C:\Users\benk\Documents\synctray` (or wherever previously configured).
5. SyncTray running with synced account.

### Test Execution

#### TC-1.54 — Upload 100MB+ file through sync (RETEST)

1. Generate a 105 MB file in the synced folder:
   ```powershell
   $bytes = New-Object byte[] (105 * 1024 * 1024)
   [System.Random]::new().NextBytes($bytes)
   [System.IO.File]::WriteAllBytes("C:\Users\benk\Documents\synctray\largefile-retest.bin", $bytes)
   ```
2. Wait for SyncTray to detect and upload the file (monitor logs in `%LOCALAPPDATA%\DotNetCloud\logs\sync-tray.log`).
3. Verify in logs:
   - File stability check should PASS (file is fully written before FSW fires, since `WriteAllBytes` is atomic).
   - No `FileStillGrowingException` expected for this scenario.
   - Chunked upload completes successfully.
4. Verify file appears in web UI at `https://mint22:5443/`.
5. **Pass criteria:** File uploads without error, appears on server with correct size (110100480 bytes).

**Bonus (optional):** To explicitly test the stability check deferral:
1. Start a slow copy of a large file into the sync folder (e.g., use `robocopy` with `/IPG:100` throttle, or copy from a USB 2.0 source).
2. Watch logs for `Deferring upload of ... file size changed during stability check` message.
3. After copy completes, watch for automatic retry and successful upload.

### Results Table

| Test ID | Test Name | Result | Notes |
|---------|-----------|--------|-------|
| TC-1.54 | 100MB+ file upload (retest) | | |

### After Completion

1. Fill in the results table above.
2. Commit and push:
   ```powershell
   git add docs\development\CLIENT_SERVER_MEDIATION_HANDOFF.md
   git commit -m "WS-4: TC-1.54 retest results (Windows, post-stability-check)"
   git push origin main
   ```
3. Relay back to moderator with commit hash.
