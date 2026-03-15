# Client/Server Mediation Handoff

Last updated: 2026-03-15 (Server correlation complete: OriginatingDeviceId=NULL for Linux uploads, diagnostic logging deployed, awaiting `mint-dnc-client` re-test)

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
- Server-side P1 echo suppression/device-identity fix and `SyncDeviceIdentity` DB migration are now applied on `mint22`.
- **Windows (`Windows11-TestDNC`) re-verification PASSED** on 2026-03-15: uploaded file completed, immediate follow-up pass showed `RemoteChanges=1, LocalApplied=0`, no download path was entered for the uploaded node, and the next scheduled pass was clean.
- **Linux (`mint-dnc-client`) parity re-verification FAILED** on 2026-03-15: uploaded verification node was downloaded on follow-up pass (`RemoteChanges=1, LocalApplied=1`), so parity with Windows behavior is not achieved.
- **Next active cycle:** Linux client re-test on `mint-dnc-client` with diagnostic logging deployed on `mint22` to trace the exact `DeviceIdentityFilter` failure path.

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

### P1 Echo Suppression — Server Correlation Complete, Linux Client Re-Test Required

**Date:** 2026-03-15
**Owner:** `mint-dnc-client` (Linux client re-test)
**Status:** READY FOR LINUX CLIENT RE-VERIFICATION

#### Server Investigation Findings (Completed on `mint22`)

**Root cause confirmed: `OriginatingDeviceId` is NULL for all Linux-uploaded nodes.**

Database evidence for node `97471092-72de-4654-9217-f653d1a2059f`:

| Field | Value |
|---|---|
| `FileNode.OriginatingDeviceId` | **NULL** |
| `UploadSession.DeviceId` (completed session `71940839-ca6f-4498-b2d4-ff35df7d8e10`) | **NULL** |
| `SyncDevices` table — Linux device entry | **DOES NOT EXIST** |

Comparison — Windows node (`echo-reverify-20260315-014651.txt`):

| Field | Value |
|---|---|
| `FileNode.OriginatingDeviceId` | `3035f6e1-4337-4377-8661-f7288d677b34` ✓ |
| `SyncDevices` entry | Present, `FirstSeenAt: 03:44 CDT` ✓ |

**Server timeline on 2026-03-15:**
- PID 1529 (01:34–02:41): Old binary without device identity code.
- PID 31217 (03:14–03:39): New binary, but `SyncDeviceIdentity` migration NOT yet applied. All `SyncDeviceResolver` calls failed with `relation "SyncDevices" does not exist` (252 occurrences).
- PID 35979 (03:41–04:52): Migration applied, new binary. Windows device successfully auto-registered at 03:44. Windows upload at 03:46 correctly captured `DeviceId`. **Linux upload at 04:08 did NOT capture `DeviceId`** — but no error or warning logged for the Linux device.

**Diagnosis: DeviceIdentityFilter has silent skip paths (no logging at Warning level for non-exceptional failures).** The filter silently skips device registration when:
- Auth not established
- `X-Device-Id` header missing/unparseable
- User claim (`NameIdentifier`/`sub`) not parseable
- DI services (`ISyncDeviceResolver`/`IDeviceContext`) unavailable

Under PID 35979, the file log (min level: Warning) shows NO device-related warnings for the Linux upload — meaning the filter either completed successfully (impossible since device wasn't registered) or silently skipped ONE of its preconditions without logging.

#### Server-Side Fixes Applied

1. **Comprehensive Warning-level diagnostic logging added to `DeviceIdentityFilter`:**
   - Logs on every code path: success, resolver-null, DI-missing, userId-parse-failure, deviceId-parse-failure
   - All at Warning level so they appear in the file log (FileMinimumLevel: Warning)

2. **Warning-level logging added to `ChunkedUploadService`:**
   - Logs when `DeviceId` is NULL at upload initiation
   - Logs when `OriginatingDeviceId` is NULL at upload completion
   - Captures both `session.DeviceId` and `context.DeviceId` values for correlation

3. **Server redeployed** with diagnostic logging active (PID 48362+ after restart).

#### Action Required — `mint-dnc-client` ONLY

1. Pull latest `main`.
2. Upload a NEW test file to the sync folder (e.g., `echo-diag-linux-$(date +%Y%m%d-%H%M%S).txt`).
3. Wait for the upload to complete and one follow-up sync pass to occur.
4. **Report back in this handoff file:**
   - Whether echo suppression worked (`LocalApplied=0` for the uploaded node on follow-up pass)
   - The sync-service log lines showing the upload and follow-up pass
   - The client's `device-id` file content: `cat ~/.local/share/DotNetCloud/device-id` (or wherever `SyncContextManager.GetSystemDataRoot()` points)
5. **On mint22** (server-side verification — the moderator or mint22 agent should check):
   - `grep "DeviceIdentityFilter\|NULL DeviceId\|NULL OriginatingDeviceId" /home/benk/Repos/dotnetcloud/artifacts/publish/server-baremetal/logs/dotnetcloud-$(date +%Y%m%d).log | tail -20`
   - This will show whether the filter succeeded, which precondition failed, or if the upload service logged null values.

#### Expected Diagnostic Log Output

On success (device properly resolved):
```
DeviceIdentityFilter: resolved device {GUID} for user {GUID} on /api/v1/files/upload/initiate
```

On failure — identifies exactly which precondition failed:
```
DeviceIdentityFilter: failed to parse userId from claims (NameIdentifier=..., sub=...) ...
DeviceIdentityFilter: missing DI services — resolver=..., deviceContext=...
DeviceIdentityFilter: X-Device-Id header present but not a valid GUID: '...'
Upload session {GUID} has NULL DeviceId at initiation ...
CompleteUpload: new FileNode '...' has NULL OriginatingDeviceId ...
```

#### Verification Flow

1. Linux client uploads file → server logs show which filter path was taken
2. If filter succeeded → check `SyncDevices` table for new Linux entry and `FileNode.OriginatingDeviceId` is set
3. If filter failed → log message identifies exact failing precondition
4. Fix the identified issue → re-test

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
