# Client/Server Mediation Handoff

Last updated: 2026-03-15 (upload complete 500 fixed on mint22; awaiting client re-verification on mint-dnc-client)

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
- Active task: client re-verification of end-to-end upload on `mint-dnc-client`.

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

### Client Re-Verification: End-to-End Upload on `mint-dnc-client`

**Date:** 2026-03-15
**Owner:** Client agent on `mint-dnc-client`
**Status:** ACTIVE — server fix deployed, client re-test needed

#### Server fix summary

- **Root cause of complete 500:** `SyncCursorHelper.AssignNextSequenceAsync` (P0.1 atomic sequence) used `db.Database.SqlQueryRaw<long>(...).SingleAsync()`. EF Core 10 considers the `INSERT ... ON CONFLICT DO UPDATE ... RETURNING` SQL non-composable — `.SingleAsync()` tries to compose LINQ on top and throws `System.InvalidOperationException`.
- **Fix:** Replaced `.SingleAsync(ct)` with `.ToListAsync(ct)).Single()`. Materializes raw SQL result to memory first, then applies `.Single()` on the list — no LINQ composition on the query.
- **File changed:** `src/Modules/Files/DotNetCloud.Modules.Files.Data/SyncCursorHelper.cs` (line 43–56)
- **Server evidence:**
  - 2,150 tests pass (581 files module), 0 failures
  - Server redeployed and healthy: `https://mint22:15443/health/live` → `Healthy`
  - Binary: `DotNetCloud.Modules.Files.Data.dll` timestamp `Mar 14 19:27`, PID `95716` started `19:28`
  - Server log shows the exact `InvalidOperationException` matched all 6 reported request IDs from prior handoff

#### Scope (Client Agent on `mint-dnc-client`)

1. Pull `main` to get latest commit.
2. Create a fresh test file in `/home/benk/synctray/` (e.g., `upload-e2e-test-$(date +%s).txt`).
3. Let sync service pick it up, or trigger sync manually.
4. Capture the full upload sequence from client logs:
   - `POST /api/v1/files/upload/initiate` → expect `201`
   - `PUT /api/v1/files/upload/{sessionId}/chunks/{hash}` → expect `200`
   - `POST /api/v1/files/upload/{sessionId}/complete` → expect `200` (was 500 before fix)
5. Verify the file appears in the server file tree via `GET /api/v1/files/tree`.
6. Optionally verify from a second client (Windows) that the uploaded file syncs down.

#### Required Evidence Back in Next Handoff Update

- Commit hash client is running on
- Timestamped log lines showing initiate `201` → chunk PUT `200` → complete `200`
- File name and content hash from the upload response
- Confirmation file appears in tree listing
- Any remaining errors or new issues

#### Exit Criteria

- `POST /api/v1/files/upload/{sessionId}/complete` returns `200` with valid `FileNode`
- End-to-end upload confirmed: file created on client → uploaded → visible in server tree
- No 409 false conflicts, no 500 errors

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
