# Client/Server Mediation Handoff

Last updated: 2026-03-15 (mint-dnc-client verification complete; upload `complete` returns 500 on mint22)

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
- Active task: server-side investigation for `POST /api/v1/files/upload/{sessionId}/complete` returning 500 on `mint22`.

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

### Server Investigation: Upload Complete 500 on `mint22`

**Date:** 2026-03-15
**Owner:** Server agent on `mint22`
**Status:** ACTIVE — client verification complete, server-side blocker remains

#### What client verification proved (`mint-dnc-client`)

- Environment confirmed on `main` at `1f0d700` (includes gzip decompression fix `af66b41`).
- Fresh file created: `/home/benk/synctray/upload-verify-test-1773533399.txt`.
- Upload pipeline now reaches chunk upload (no false 409 conflict pattern from pre-fix behavior):
  - `POST https://mint22:15443/api/v1/files/upload/initiate` -> `201`
  - Response preview includes `missingChunks: ["737b133ef2d09bb83f53a8a768068ae32dc30bd3bcd69e2a6f7ab34180bc3cc2"]`
  - `PUT https://mint22:15443/api/v1/files/upload/{sessionId}/chunks/{hash}` includes successful `200` responses
- New failure observed: `POST https://mint22:15443/api/v1/files/upload/{sessionId}/complete` consistently returns `500`.
- Observed session IDs:
  - `27897c31-2d37-4649-ba52-2e5fe55bd75d`
  - `56a55d92-b0e6-4967-9966-66fd6eec0844`
- Example request IDs for failing complete calls:
  - `f923097bef0f4e2b92553bb02b871a00`
  - `d8279a0ad4654e14bf7f8f132a23b412`
  - `a638e6f6e7b1486d9cbc3102d79388b5`
  - `1b0d5f849e604c8eb2f3f0a0f1542459`
  - `627395a815874053b075fcb8f6365709`
  - `2bba60391a5e4f87aa3166c472557458`
- Client evidence source:
  - `/home/benk/.local/share/DotNetCloud/logs/sync-service20260314.log` (timestamps around `2026-03-15T00:09:59Z` to `00:11:12Z`)

#### Scope (Server Agent on `mint22`)

1. Pull `main` and confirm current server process uses current binaries.

2. Correlate server logs by request IDs and timestamps above to capture stack traces for:
   - `POST /api/v1/files/upload/{sessionId}/complete` -> 500
   - intermittent `PUT /chunks/{hash}` -> 500 for one concurrent session before retry success

3. Inspect upload completion flow and invariants for concurrent sessions targeting same file/path:
   - session/chunk existence checks
   - idempotency/name-conflict handling at complete stage
   - transaction boundaries and exception mapping

4. Validate DB state for the two session IDs and associated chunk rows/blobs.

5. Implement server fix so complete is deterministic and returns:
   - `200` + `FileNode` on successful complete
   - expected conflict status only for true duplicate/name conflict semantics

6. Redeploy on `mint22`, verify runtime binary freshness, and run relevant tests.

7. Request re-verification on `mint-dnc-client` only after server logs show corrected behavior.

#### Required Evidence Back in Next Handoff Update

- Server stack trace/root cause for the `complete` 500
- Commit hash with exact files changed
- Runtime verification command/output proving active server binary is current
- Test evidence (what was run, pass/fail)
- One raw successful sequence after fix (initiate `201` -> chunk PUT `200` -> complete `200`) with timestamps

#### Exit Criteria

- `POST /api/v1/files/upload/{sessionId}/complete` no longer returns 500 for the verification scenario
- End-to-end upload from `mint-dnc-client` reaches complete `200` with valid `FileNode`
- No false 409 chunk behavior reintroduced

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
