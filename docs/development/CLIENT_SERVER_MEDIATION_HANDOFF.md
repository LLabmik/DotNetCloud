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
- Server-side P1 echo suppression/device-identity fix and `SyncDeviceIdentity` DB migration are now applied on `mint22`.
- **Windows (`Windows11-TestDNC`) re-verification PASSED** on 2026-03-15: uploaded file completed, immediate follow-up pass showed `RemoteChanges=1, LocalApplied=0`, no download path was entered for the uploaded node, and the next scheduled pass was clean.
- **Next active cycle:** Linux (`mint-dnc-client`) parity re-verification for the same server-side fix.

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

### P1 Echo Suppression Fix — Linux Re-Verification

**Date:** 2026-03-15
**Owner:** `mint-dnc-client` (Linux Mint 22) — **this cycle is Linux-only**
**Status:** READY FOR CLIENT RE-VERIFICATION (Windows passed; Linux parity check pending)

#### Server-Side Fixes Already Applied

Linux should verify against the same server-side fixes that Windows just passed against:

- `ChunkedUploadService.CompleteUploadAsync` now persists `OriginatingDeviceId` from `session.DeviceId` first:
  - file update path: `session.DeviceId ?? _deviceContext.DeviceId ?? fileNode.OriginatingDeviceId`
  - new file path: `session.DeviceId ?? _deviceContext.DeviceId`
- Production database migration `20260315074239_SyncDeviceIdentity` is applied on `mint22`.
- Server-side sanity verification already passed on `mint22`:
  - `GET /health/live` healthy
  - unauthenticated `GET /api/v1/files/sync/tree` now returns `401` instead of `500`

No Linux code changes are requested in this cycle. This is runtime parity verification only.

#### Windows Result (Completed)

Windows (`Windows11-TestDNC`) re-verification passed with file `echo-reverify-20260315-014651.txt`:

- upload completed successfully
- immediate follow-up pass showed `RemoteChanges=1, LocalApplied=0`
- no download path was entered for verification node `e2174c04-8fbd-43cc-a853-e45cc2d9dd53`
- next scheduled pass was clean (`RemoteChanges=0, LocalApplied=0`)

#### Action Required — `mint-dnc-client` ONLY

1. `git pull` to get latest `main`
2. Upload a fresh test file into the Linux sync directory
3. Wait for the next sync pass
4. Verify the uploaded file is not re-downloaded on the next pass
5. Update the verification table below with Linux evidence, commit, and push

Preferred evidence mirrors Windows:

- file path used for the test
- log path used for proof
- upload completion line with node ID
- immediate follow-up pass showing remote change observed without a local apply/download
- next scheduled clean pass, or equivalent proof that no echo download occurred

#### Verification Results

| Machine | Status | Echo suppression working | Notes |
|---|---|---|---|
| `Windows11-TestDNC` | COMPLETE | ✓ | Verified on 2026-03-15. Upload completed; follow-up pass showed `RemoteChanges=1, LocalApplied=0`; no download entry for verification node; next scheduled pass clean. |
| `mint-dnc-client` | PENDING | ☐ | Perform Linux parity re-verification against current `mint22` runtime. |

**Instructions for `mint-dnc-client` agent:** Upload a fresh test file, verify echo suppression works at runtime, update YOUR row, then commit and push.

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
