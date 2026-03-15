# Client/Server Mediation Handoff

Last updated: 2026-03-15 (server DB migration `SyncDeviceIdentity` applied — `OriginatingDeviceId` column now exists; sync/tree 500s fixed)

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
- Client-side upload dedup + echo suppression (commit `4c575cc`) verified on all machines:
  - **Linux (`mint-dnc-client`):** runtime verified — single initiate per event, no conflict copies.
  - **Windows (`Windows11-TestDNC`):** runtime verified on MSIX `0.23.3.0` — single initiate per event, no conflict copies.
  - **Server (`mint22`):** zero 5xx errors since deployment; only normal token-refresh 401s observed.
- **Upload hardening story: CLOSED.** Full chain verification complete across all three machines.

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

### P1 Echo Suppression Fix — Client Re-Verification

**Date:** 2026-03-15
**Owner:** `Windows11-TestDNC` (Windows) — **this cycle is Windows-only**
**Status:** READY FOR CLIENT RE-VERIFICATION (server 500 fixed)

**Verification order:** Windows first → report back to `mint22` → then separate cycle for `mint-dnc-client`.

#### Bug Found and Fixed (Server — Echo Suppression)

Windows client (`0.23.4.0`) reported echo suppression failure: uploaded file was immediately re-downloaded on next sync pass.

**Root cause:** `ChunkedUploadService.CompleteUploadAsync` used `_deviceContext.DeviceId` (per-request device context) to set `FileNode.OriginatingDeviceId`. The device ID was captured correctly during `InitiateUploadAsync` into `session.DeviceId`, but `CompleteUploadAsync` didn't use the session value. If the device context was null/missing on the complete request, `OriginatingDeviceId` was set to null, and echo suppression had nothing to match against.

**Fix applied** in `ChunkedUploadService.cs`:
- File update path (line 209): `session.DeviceId ?? _deviceContext.DeviceId ?? fileNode.OriginatingDeviceId`
- New file path (line 265): `session.DeviceId ?? _deviceContext.DeviceId`

All tests pass: 607 (Files) + 138 (Core) + 176 (Data) = 921 total.

#### Bug Found and Fixed (Server — Missing DB Migration)

Windows client reported `500 INTERNAL_ERROR` on `GET /api/v1/files/sync/tree`. Server logs showed:
```
SqlState: 42703
MessageText: column f.OriginatingDeviceId does not exist
```

**Root cause:** The EF Core migration `20260315074239_SyncDeviceIdentity` (which adds `OriginatingDeviceId` to `FileNodes`, `DeviceId` to `UploadSessions`, and creates `SyncDevices` table) was committed but never applied to the production `dotnetcloud` database. The server's `EnsureModuleTablesCreatedAsync` only fires when the sentinel table (`FileNodes`) doesn't exist — it does not apply incremental migrations.

**Fix applied:**
```bash
dotnet ef database update --project src/Modules/Files/DotNetCloud.Modules.Files.Data \
  --context FilesDbContext \
  --connection "Host=localhost;Database=dotnetcloud;Username=postgres;Password=postgres"
```
Migration `20260315074239_SyncDeviceIdentity` applied successfully. Server restarted via `systemctl restart dotnetcloud.service`.

**Verification:**
- `curl -sk -w "%{http_code}" https://localhost:15443/health/live` → `Healthy`
- `curl -sk -w "%{http_code}" https://localhost:15443/api/v1/files/sync/tree` → `401` (expected — requires auth, no more 500)
- No `42703` errors in post-restart journalctl output

#### Action Required — Windows11-TestDNC ONLY

No client code changes needed — both fixes are server-side only.

1. `git pull` to get latest main
2. Upload a new test file (e.g., `echo-test-fix-20260315.txt`) to sync dir
3. Wait for next sync pass
4. Verify the sync pass does NOT re-download the file you just uploaded (check logs for echo suppression skip message)
5. Update verification table below, commit and push

#### Verification Results

| Machine | Status | Echo suppression working | Notes |
|---|---|---|---|
| `Windows11-TestDNC` | PENDING | ☐ | Previous FAIL was caused by missing DB migration on server (now fixed). Retry verification. |
| `mint-dnc-client` | DEFERRED | ☐ | Will be verified in a separate cycle after Windows completes. |

**Instructions for `Windows11-TestDNC` agent:** Upload a test file, verify echo suppression works (uploaded file is NOT re-downloaded), update YOUR row. Commit and push.

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
