# Client/Server Mediation Handoff

Last updated: 2026-03-15 (Windows verification complete; handoff advanced to mint22 for chain closeout)

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
- **Windows verification on `Windows11-TestDNC` is complete:** package `0.23.3.0` installed, runtime binary hash gate passed, and sync runtime evidence confirms expected initiate/conflict behavior.

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

### Step 3 of 3: Final Chain Closeout on `mint22`

**Date:** 2026-03-15
**Owner:** `mint22` agent
**Status:** ACTIVE
**Commits:** `4c575cc` (client dedup + echo suppression), `33d4672` (Windows handoff status updates)

#### Verification Summary (Linux + Windows)

- Linux (`mint-dnc-client`) verification: complete and archived, including runtime evidence showing one `upload/initiate` per file event and no local conflict-copy creation for verification files.
- Windows (`Windows11-TestDNC`) verification: complete.
  - Client.Core tests: `164 passed, 0 failed`.
  - Installed package/version: `DotNetCloud.SyncTray_0.23.3.0_x64__xrs2wr7p8d2rc`.
  - Runtime service path now points to `0.23.3.0` package.
  - Runtime hash gate: `SYNC_SERVICE_EXE_MATCH: True`, `CLIENT_CORE_DLL_MATCH: True`.
  - Runtime evidence file: `C:\ProgramData\DotNetCloud\Sync\logs\sync-service20260314.log`.
  - Verification file: `seq-test-windows-20260314-234612.txt`.
    - Create event produced one upload initiation sequence (`upload/initiate` line cluster around `11355-11359`).
    - Append event produced one upload initiation sequence (`upload/initiate` line cluster around `11442-11446`).
    - Conflict evidence for verification file: `CONFLICT_LINES_FOR_FILE: 0`.

#### Active Task for `mint22`

1. Pull latest `main` and review the archived Step 1 + Step 2 evidence in `docs/development/CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
2. Run a short server-side sanity window focused on upload endpoints:
   - confirm no new 5xx/error bursts for `POST /api/v1/files/upload/initiate` and `POST /api/v1/files/upload/{sessionId}/complete` after Windows `0.23.3.0` runtime verification.
3. If server-side sanity is green, archive this Step 3 block and replace Active Handoff with standby monitoring.

#### Exit Criteria

- Cross-machine verification chain is complete (Linux + Windows pass evidence archived).
- `mint22` confirms no immediate server-side regressions under the verified client runtime.
- Active Handoff is transitioned to either standby monitoring or next concrete task.

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
