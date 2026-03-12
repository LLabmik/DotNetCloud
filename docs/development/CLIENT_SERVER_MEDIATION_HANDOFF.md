# Client/Server Mediation Handoff

Last updated: 2026-03-12 (Phase 2.10 fully closed — all client items complete)

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
- Other agent pulls latest, reads the handoff, and takes action without asking questions.

**Document maintenance:**
- Pre-commit archive rule (MANDATORY): before committing this file, move all completed/older handoff tasks to `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Keep only the single current task in **Active Handoff** (one active block only).
- If a task is completed, archive it first, then replace **Active Handoff** with the next task.

## Moderator Communication (Minimal)

**Moderator relays ONLY ONE OF THESE messages — nothing more:**

- `New handoff update. Pull main and resume from 'Active Handoff' section.`
- `<Commit hash> — New handoff update. Pull and check docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md Active Handoff.`

**No moderator task:** Moderator provides zero context, zero explanation. The handoff document has everything the receiving agent needs.

## Current Status

- Issues #1-#45 and previous sprint/batch closeout work: complete.
- Phase 2.10 Android contract alignment: complete (archived).
- Phase 2.12 Chat Testing Infrastructure: complete (integration tests added).
- Phase 2.13 Documentation: complete.
- Urgent migration fix (AddSymlinkSupport/LinkTarget column): complete (2026-03-12).
- Integration test fixes (11 failures → 0): complete (2026-03-12).
- Phase 2.10 final items (badges, APK download docs, app store listing): complete (2026-03-12).
- **All Phase 2 work is now complete.**
- Full test suite: 2,095 passed / 0 failed / 13 skipped (env-gated).

## Environment

| Role | Machine | Detail |
|---|---|---|
| Server | `mint22` | `https://mint22:15443/` |
| Client | `Windows11-TestDNC` | Sync dir: `C:\Users\benk\Documents\synctray` |

## Key Carry-Forward Contracts

- Auth: OpenIddict bearer on files/sync endpoints via `FilesControllerBase` `[Authorize]`.
- API envelope: middleware wraps responses; clients should unwrap via envelope helpers.
- Sync flow: changes -> tree -> reconcile -> chunk manifest -> chunk download -> file assembly.
- Desktop OAuth constant: `OAuthConstants.ClientId = "dotnetcloud-desktop"`.

## Active Handoff

### BLOCKER: Missing PosixMode Migration on Server DB

**Date:** 2026-03-12
**Owner:** Server agent (`mint22`)
**Status:** ACTION REQUIRED

**Problem:**
Client testing of the Web UI at `https://mint22:15443/` immediately fails on page load with:

```
Something went wrong
42703: column f.PosixMode does not exist POSITION: 271
```

The PostgreSQL database on `mint22` is missing the `PosixMode` and `PosixOwnerHint` columns on the `FileNodes`, `FileVersions`, and `UploadSessions` tables. The EF Core migration exists in code (`20260309083622_AddPosixPermissions`) but was never applied to the running database.

This was reported as fixed previously but the error is still occurring — possibly the service was restarted but the migration was never applied, or the service is running stale binaries.

**Required fix (server agent):**

1. Pull latest `main`:
   ```bash
   cd /path/to/dotnetcloud
   git pull
   ```

2. Apply the pending Files module migration:
   ```bash
   dotnet ef database update \
     --project src/Modules/Files/DotNetCloud.Modules.Files.Data \
     --context FilesDbContext
   ```

3. Verify the columns exist:
   ```sql
   SELECT column_name FROM information_schema.columns 
   WHERE table_name = 'FileNodes' AND column_name IN ('PosixMode', 'PosixOwnerHint');
   ```
   Expected: both columns returned.

4. Restart the server and verify the Web UI loads without error.

5. Also check for any other pending migrations while you're at it:
   ```bash
   dotnet ef migrations list \
     --project src/Modules/Files/DotNetCloud.Modules.Files.Data \
     --context FilesDbContext
   ```
   Verify all migrations show as "applied."

**Migration details:**
- Migration name: `20260309083622_AddPosixPermissions`
- File: `src/Modules/Files/DotNetCloud.Modules.Files.Data/Migrations/20260309083622_AddPosixPermissions.cs`
- Adds: `PosixMode` (int?, nullable) to `FileNodes`, `FileVersions`, `UploadSessions`
- Adds: `PosixOwnerHint` (varchar(200), nullable) to `FileNodes`, `UploadSessions`

There may also be a second migration to check: `20260309093919_AddSymlinkSupport` — verify that one is applied too.

**Impact:**
- ALL client testing is blocked (Web UI, Sync App, Android) — every file-related endpoint hits this missing column.
- No client-side changes needed.

**Request back:**
- Confirmation that migrations were applied
- Output of the `information_schema` column check
- Confirmation Web UI loads without error
- Commit hash if any changes were made

## Relay Template

```markdown
### Send to [Server|Client] Agent
<message text>

### Request Back
- commit hash
- raw endpoint/URL used
- raw error/query params
- raw log lines around the event (with timestamp)
```
