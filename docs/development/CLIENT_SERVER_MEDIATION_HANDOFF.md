# Client/Server Mediation Handoff

Last updated: 2026-03-15 (client re-verification complete; server deployment follow-up for unique-violation hardening)

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
- Linux bring-up engineering fixes are complete.
- P0 sync hardening (atomic SyncSequence, unique name constraint, atomic chunk refcount) deployed.
- **Server gzip fix deployed (af66b41):** `UseRequestDecompression()` middleware added. Server now auto-decompresses `Content-Encoding: gzip` request bodies before controllers read them. The chunk PUT hash mismatch that caused false 409 is resolved.
- **Upload complete 500 fixed:** `SyncCursorHelper.AssignNextSequenceAsync` was calling `.SingleAsync()` on non-composable raw SQL. EF Core 10 rejects LINQ composition on `SqlQueryRaw` with `RETURNING`. Fixed by materializing with `.ToListAsync()` then `.Single()`.
- Client re-verification complete on `mint-dnc-client`: fresh upload now succeeds with initiate `201` -> chunk `200` -> complete `200`.
- Active task: deploy and runtime-verify unique-violation hardening commit on `mint22`.

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

### Server Deployment Follow-Up: Unique-Violation Hardening on `mint22`

**Date:** 2026-03-15
**Owner:** Server agent on `mint22`
**Status:** ACTIVE — code complete, pending deploy/runtime verification

#### Completed verification context

- Client re-verification is complete and archived in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Successful sequence for fresh file `/home/benk/synctray/upload-e2e-test-1773534949.txt`:
  - `2026-03-15T00:36:22.1527216Z` `POST /api/v1/files/upload/initiate` -> `201`
  - `2026-03-15T00:36:22.1918408Z` `PUT /api/v1/files/upload/39ca2304-9012-4a88-83c1-b8154832d43a/chunks/9f1d9a31b19ff8659781e0ee0fb28424ab05687e12aca7aa6dc5966a40e35da9` -> `200`
  - `2026-03-15T00:36:22.2229962Z` `POST /api/v1/files/upload/39ca2304-9012-4a88-83c1-b8154832d43a/complete` -> `200`
- `FileNodeResponse` for successful complete:
  - `id=280339db-3ece-4a00-8129-2a688ede1a79`
  - `name=upload-e2e-test-1773534949.txt`
  - `contentHash=7172fa139d61bcf795a2b5dc0d3d78756f86839f0d2776a6ec83765eaba06b25`

#### Scope (Server Agent on `mint22`)

1. Pull `main` and deploy latest server binaries.
2. Runtime-verify binary freshness (PID/start time, deployed DLL timestamp, `/health/live`).
3. Validate conflict behavior for duplicate-key races (no unhandled 500s):
   - upload complete duplicate/name race
   - chunk dedup race on concurrent PUT
4. Run targeted verification tests:
   - `dotnet test tests/DotNetCloud.Modules.Files.Tests/DotNetCloud.Modules.Files.Tests.csproj`
   - document any environment-gated suites explicitly if skipped.
5. If clean, request one optional client sanity retry on `mint-dnc-client`.

#### Required Evidence Back in Next Handoff Update

- Commit hash deployed on `mint22`
- Runtime verification command outputs (PID/start time, binary timestamp, health endpoint)
- Raw endpoint evidence from at least one conflict/race scenario showing expected non-500 mapping
- Test evidence (what ran, pass/fail, and any gated skips)
- Any remaining blockers

#### Exit Criteria

- Latest server build is running on `mint22` with verified fresh binaries
- No unhandled `500` responses for duplicate-key conflict paths in upload complete/chunk dedup races
- Handoff contains deploy + verification evidence and is ready for optional client sanity retry

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
