# Client/Server Mediation Handoff

Last updated: 2026-03-15 (Linux re-test run on fresh client binaries: echo suppression split by duplicate local contexts; client context cleanup now required)

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
- **Next active cycle:** client-side duplicate-context cleanup on `mint-dnc-client`, then a single-context Linux re-test.

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

### P1 Echo Suppression — Linux Re-Test Completed, Duplicate Context Blocker Identified

**Date:** 2026-03-15
**Owner:** `mint-dnc-client` (context cleanup + re-test)
**Status:** READY FOR CLIENT CONTEXT CLEANUP

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

#### Linux Re-Test Results (Completed on `mint-dnc-client`)

Runtime process verification:

- stale client process (started 02:12) was still running old binary; restarted from current source at 05:18.
- process after restart:
  - `dotnet run --project src/Clients/DotNetCloud.Client.SyncService/DotNetCloud.Client.SyncService.csproj`
  - child binary: `src/Clients/DotNetCloud.Client.SyncService/bin/Debug/net10.0/dotnetcloud-sync-service`

Device identity persistence after restart:

- `~/.local/share/DotNetCloud/Sync/device-id` now exists
- value: `21c95ee6-2094-447e-bc8a-e70177c3025f`

Verification upload file:

- `/home/benk/synctray/echo-diag-linux-restart-20260315-052729.txt`
- server node id (from client log): `a0359028-e93e-4aae-b080-ebb3485117ce`

Observed follow-up behavior:

- context `cb22726a-cdef-4cc8-a29c-755b22f1c899`: follow-up pass shows `RemoteChanges=1, LocalApplied=0` (suppressed)
- context `e7ba5002-dc72-4c97-a511-17f194ca79c5`: same node still downloaded (`File download starting`), pass shows `RemoteChanges=1, LocalApplied=1`

Key evidence:

- contexts registry contains **two active contexts** with same `ServerBaseUrl`, `UserId`, and `LocalFolderPath` (`/home/benk/synctray`):
  - `e7ba5002-dc72-4c97-a511-17f194ca79c5`
  - `cb22726a-cdef-4cc8-a29c-755b22f1c899`
- this dual-engine configuration causes one engine to suppress and the other to re-download the same uploaded node.

Conclusion:

- server-side device identity path is no longer the only blocker.
- primary Linux parity blocker is now **duplicate local sync contexts against the same folder/account**.

#### Action Required — `mint-dnc-client` ONLY

1. Remove one duplicate context so only a single context remains bound to `/home/benk/synctray` + `https://mint22:15443` + user `019cc1ac-da42-737c-b0ab-d0f2ecca8019`.
2. Restart sync service.
3. Re-run the same upload/follow-up verification with one context only.
4. Update this handoff with:
   - remaining context id
   - follow-up pass summary (`RemoteChanges`/`LocalApplied`)
   - whether `File download starting` appears for the uploaded node.

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
