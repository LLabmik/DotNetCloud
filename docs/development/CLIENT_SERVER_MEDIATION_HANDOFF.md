# Client/Server Mediation Handoff

Last updated: 2026-03-14 (archived older execution trail; single active mint-dnc-client runtime verification task remains)

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
- Linux bring-up engineering fixes are complete (non-root path fallback, per-user singleton guards, reconciliation hardening, and sync re-entry coalescing).
- Remaining open item is runtime validation on `mint-dnc-client` for one clean Linux E2E sync cycle with no immediate churn.

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

### Linux Sync Client Final Runtime Verification on `mint-dnc-client`

**Date:** 2026-03-14
**Owner:** Client agent (server agent only if API regression is found)
**Status:** IN PROGRESS

Goal: complete one clean Linux Mint sync cycle against `mint22` after re-entry coalescing hardening, with full runtime evidence and no immediate retry churn.

#### Scope (Client Agent)
- Pull latest `main` on `mint-dnc-client`.
- Run targeted client suites:
	- `tests/DotNetCloud.Client.Core.Tests`
	- `tests/DotNetCloud.Client.SyncService.Tests`
- Run SyncService + SyncTray in Linux user mode and complete interactive OAuth login.
- Validate and capture logs for:
	- token mint/refresh path,
	- cursor-based changes + tree fetch,
	- local upload (new or modified file),
	- remote download materialization,
	- no immediate rapid re-entry churn after a completed pass,
	- no repeated `/api/v1/files/sync/tree` 429 escalation.

#### Scope (Server Agent, only if needed)
- If client finds server/API regressions, reproduce on `mint22`, fix with tests first, redeploy, and provide runtime verification evidence.

#### Required Evidence Back in Next Handoff Update
- Commit hash.
- Exact commands run on `mint-dnc-client`.
- Timestamped raw log lines proving one clean pass sequence.
- Endpoint URLs and HTTP status codes observed.
- Expected vs actual for upload and download flows.

#### Exit Criteria
- At least one clean full sync pass on Linux without immediate churn.
- Upload and download paths both verified on Linux runtime.
- Any blocker either fixed and redeployed or documented as the single next blocker.

#### Latest Engineering Update (Completed, archived details moved)
- Sync re-entry coalescing hardening landed in `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs` with regression test `SyncAsync_BurstWhileRunning_CoalescesIntoSingleTrailingPass`.
- Validation prior to this handoff state:
	- `DotNetCloud.Client.Core.Tests`: 160 passed, 0 failed.
	- `DotNetCloud.Client.SyncService.Tests`: 27 passed, 0 failed.

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
