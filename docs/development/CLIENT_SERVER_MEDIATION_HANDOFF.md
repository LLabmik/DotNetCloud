# Client/Server Mediation Handoff

Last updated: 2026-03-15 (Upload hardening story fully closed — all machines verified)

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
- **Linux (`mint-dnc-client`) duplicate-context cleanup + parity re-verification PASSED** on 2026-03-15:
  - context registry reduced to one context (`cb22726a-cdef-4cc8-a29c-755b22f1c899`)
  - service restart logged `Loading 1 persisted sync context(s)`
  - verification upload `m2_single_ctx_20260315_061322.txt` completed (`NodeId=289d45f4-2c97-498c-920e-8eb5f61c6768`)
  - follow-up pass showed `RemoteChanges=1, LocalApplied=0`
  - no `File download starting` line appeared for the uploaded node
- Test gate on `mint-dnc-client`:
  - `dotnet test` (solution-wide) is environment-gated on this host due missing `maui-android` workload (`NETSDK1147`).
  - Executable non-gated suite passed: `dotnet test tests/DotNetCloud.Modules.Files.Tests/DotNetCloud.Modules.Files.Tests.csproj` => `609 passed, 0 failed`.
- **Next active cycle:** No pending work. Upload hardening story fully closed across all machines.

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

No pending work. Upload hardening story fully closed on 2026-03-15.

All three machines verified clean:
- **Windows (`Windows11-TestDNC`):** 2,177 tests passed, upload verified, no self-echo.
- **Linux (`mint-dnc-client`):** 609 file module tests passed, upload verified, no self-echo.
- **Server (`mint22`):** Zero errors/exceptions in logs, all client uploads confirmed server-side.

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
