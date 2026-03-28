# Client/Server Mediation Handoff

Last updated: 20260328 (WS-4 Phase C — Linux sync client testing handoff)

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
- **Active cycle (20260328):** WS-4 live verification 58/66 passed. Windows Phase C complete (8 pass, 2 deferred, 1 skip). Linux Phase C handoff active for mint-dnc-client.

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

**Target machine:** `mint-dnc-client`
**Status:** READY FOR EXECUTION
**Context:** WS-4 Live Verification — Phase C Sync Client Tests (Linux)

### Objective

Execute Phase C sync client end-to-end tests on Linux using SyncTray. Record results per test case. This includes **TC-1.48** (Linux launch) which was excluded from the Windows run. **Do NOT run TC-1.47** (that is the Windows launch test, already completed).

**Server:** `https://mint22:5443/`
**Test account:** `testdude@llabmik.net` / `TestMilk01!`
**OAuth client_id:** `dotnetcloud-desktop`

### Prerequisites

1. Pull latest `main`:
   ```bash
   cd ~/Repos/dotnetcloud
   git pull
   ```
2. Build SyncTray:
   ```bash
   dotnet build src/Clients/DotNetCloud.Client.SyncTray/DotNetCloud.Client.SyncTray.csproj
   ```
   Or use the published tarball if available:
   - `artifacts/installers/dotnetcloud-desktop-client-linux-x64-0.1.0-alpha.tar.gz`
3. Ensure `mint22:5443` is reachable from mint-dnc-client.
4. Sync directory: `~/synctray` (or user-selected during account setup).

### Architecture Note

SyncService has been merged into SyncTray — **single Avalonia process** owns the full sync lifecycle. No separate service, no IPC. On startup SyncTray resolves `ISyncContextManager` (via `AddSyncContextManager`) and loads contexts in-process. Single-instance enforced via file lock.

### Test Execution (11 tests)

Execute each test and record result as **PASS** or **FAIL (reason)** in the results table at the bottom.

---

#### TC-1.46 — Rapid-save debounce behavior
1. SyncTray running with synced account + local folder.
2. Save same file rapidly 10 times (e.g., open in text editor, save repeatedly with small edits).
3. Check SyncTray logs: `~/.local/share/DotNetCloud/logs/sync-tray.log`
4. **Pass:** At most 2 sync cycles triggered (FSW debouncer coalesces events).

#### TC-1.48 — Launch SyncTray on Linux
1. Run SyncTray from terminal: `dotnet run --project src/Clients/DotNetCloud.Client.SyncTray/DotNetCloud.Client.SyncTray.csproj` or run the published binary.
2. Verify tray icon appears in system tray (may need a system tray extension on some DEs).
3. **Pass:** SyncTray launches under user session, tray icon visible.

#### TC-1.49 — Add account via OAuth2
1. In SyncTray, click Add Account (tray menu or settings window).
2. Browser opens to `https://mint22:5443/` login page.
3. Log in with `testdude@llabmik.net` / `TestMilk01!`.
4. Complete OAuth2 PKCE consent flow.
5. **Pass:** Account appears connected in SyncTray UI, sync context created.

#### TC-1.50 — Server-to-local file sync
1. In browser (`https://mint22:5443/`), upload or create a new file (e.g., `test-server-to-local-linux.txt`).
2. Wait for SyncTray sync cycle to complete (watch tray icon or logs).
3. Check local sync folder for the new file.
4. **Pass:** File appears in local sync folder with correct content.

#### TC-1.51 — Local-to-server file sync
1. Create a file in the local sync folder (e.g., `test-local-to-server-linux.txt`).
2. Wait for SyncTray sync cycle.
3. Check web UI for the new file.
4. **Pass:** File appears in server web UI with correct content.

#### TC-1.52 — Conflict copy on concurrent edits
1. Ensure a file exists both locally and on server (e.g., `conflict-test.txt`).
2. Edit the file on server (web UI) AND locally before sync settles.
3. Allow sync cycle to run.
4. **Pass:** Conflict copy created (e.g., `conflict-test (conflict).txt`), both versions preserved.
5. **Note:** Windows deferred this test — try to trigger a genuine race condition if possible.

#### TC-1.53 — Offline queue and reconnect
1. Disable network on mint-dnc-client (`nmcli networking off` or disconnect cable).
2. Make local file changes (create/edit files in sync folder).
3. Re-enable network (`nmcli networking on`).
4. **Pass:** Queued changes sync automatically after reconnect without manual intervention.
5. **Note:** Windows deferred this (VM limitation). Linux should be able to test this directly.

#### TC-1.54 — Upload 100MB+ file through sync
1. Generate or place a file ≥100 MB in the synced folder (e.g., `dd if=/dev/urandom of=~/synctray/largefile.bin bs=1M count=105`).
2. Wait for upload to complete (monitor logs or tray status).
3. Verify file appears in web UI.
4. **Pass:** Large file uploads successfully (chunked transfer in logs).

#### TC-1.55 — SyncTray status indicators
1. Observe idle state (tray icon).
2. Trigger a sync (add a file) — watch for syncing indicator.
3. Disconnect network — watch for offline/error indicator.
4. **Pass:** Tray icon/status reflects idle, syncing, and offline states correctly.

#### TC-1.56 — Selective sync exclusion
1. In SyncTray settings or create `.syncignore` file in sync root, exclude a folder (e.g., `ExcludedFolder/`).
2. On server, add a file under that folder.
3. Wait for sync cycle.
4. **Pass:** Excluded folder content is NOT synced locally.

#### TC-1.57 — Multi-account independent sync
1. Add a second account in SyncTray (if available — different user or different server).
2. Make changes in each account's scope.
3. **Pass:** Both accounts sync independently with no cross-over.
4. **If only one account available:** Mark as SKIP (environment-gated, requires second account).

### Results Table (Linux)

Fill in after each test:

| Test ID | Test Name | Result | Notes |
|---------|-----------|--------|-------|
| TC-1.46 | FSW debounce | | |
| TC-1.48 | Launch SyncTray Linux | | |
| TC-1.49 | OAuth2 account add | | |
| TC-1.50 | Server → local sync | | |
| TC-1.51 | Local → server sync | | |
| TC-1.52 | Conflict copy | | |
| TC-1.53 | Offline queue + reconnect | | |
| TC-1.54 | 100MB+ file upload | | |
| TC-1.55 | Status indicators | | |
| TC-1.56 | Selective sync exclusion | | |
| TC-1.57 | Multi-account sync | | |

### Windows Results Reference (Completed)

For comparison — Windows Phase C results (archived in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`):
- 8 PASS: TC-1.46, 1.47, 1.49, 1.50, 1.51, 1.54, 1.55, 1.56
- 2 DEFERRED: TC-1.52 (conflict), TC-1.53 (offline — VM limitation)
- 1 SKIP: TC-1.57 (multi-account — environment-gated)

### After Completion

1. Fill in the results table above.
2. Commit and push:
   ```bash
   git add docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md
   git commit -m "WS-4 Phase C: Linux sync client test results"
   git push origin main
   ```
3. Relay back to moderator with commit hash so `mint22` can pull results and consolidate.
