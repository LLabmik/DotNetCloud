# Client/Server Mediation Handoff

Last updated: 2026-03-15 (Linux parity re-verification failed: uploaded node still echoed back on `mint-dnc-client`; server correlation task active)

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
- **Next active cycle:** server/client correlation on `mint22` to identify why the Linux-uploaded node is still treated as remote.

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

### P1 Echo Suppression Fix — Linux Failure Correlation (Server + Client)

**Date:** 2026-03-15
**Owner:** `mint22` (server-side investigation first)
**Status:** READY FOR SERVER INVESTIGATION

#### Linux Failure Evidence (Completed)

Linux (`mint-dnc-client`) re-verification used:

- verification file: `/home/benk/synctray/echo-reverify-linux-20260315-090808.txt`
- log path: `/home/benk/.local/share/DotNetCloud/logs/sync-service20260315.log`

Observed runtime sequence:

- `2026-03-15T09:08:09.0136307Z` upload complete: `NodeId=97471092-72de-4654-9217-f653d1a2059f`
- `2026-03-15T09:09:09.1872615Z` follow-up pass: `RemoteChanges=1, LocalApplied=1` (expected `LocalApplied=0`)
- `2026-03-15T09:09:09.1531502Z` and `2026-03-15T09:09:09.2020480Z` download path entered for same node (`File download starting`)
- `2026-03-15T09:09:09.3059273Z` next pass clean: `RemoteChanges=0, LocalApplied=0`

Windows still passes the same scenario, so current regression is Linux-specific (or Linux-runtime-context specific) under the same server deployment.

#### Action Required — `mint22` ONLY

1. Pull latest `main` and read this section.
2. Correlate node `97471092-72de-4654-9217-f653d1a2059f` on server:
   - `FileNode.OriginatingDeviceId`
   - upload session `DeviceId`
   - sync tree/change payload device metadata returned for this node to Linux client.
3. Confirm whether server is returning device identity for this node that should have been suppressed for the Linux uploader.
4. Document findings and root cause in this handoff file.
5. If server bug is confirmed, implement fix, run tests, redeploy, and post runtime verification gate evidence.
6. If server looks correct, explicitly call out required Linux-side diagnostic target (for example context/device-id mismatch across local contexts) with exact next steps.

#### Verification Results

| Machine | Status | Echo suppression working | Notes |
|---|---|---|---|
| `Windows11-TestDNC` | COMPLETE | ✓ | Verified on 2026-03-15. Upload completed; follow-up pass `RemoteChanges=1, LocalApplied=0`; no download entry for verification node; next scheduled pass clean. |
| `mint-dnc-client` | COMPLETE (FAILED RESULT) | ☐ | Re-verification completed on 2026-03-15; uploaded node `97471092-72de-4654-9217-f653d1a2059f` was downloaded on follow-up pass (`RemoteChanges=1, LocalApplied=1`). |

**Instructions for `mint22` agent:** Correlate this exact node/session/device identity path on server, update findings in-place, then commit and push.

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
