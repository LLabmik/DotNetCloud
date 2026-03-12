# Client/Server Mediation Handoff

Last updated: 2026-03-11 (compacted active-only handoff)

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
- This handoff was compacted on 2026-03-11 to remove completed historical sections from active view.

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

### URGENT — Apply Pending Database Migration on mint22

**Date:** 2026-03-11
**Owner:** Server agent (`mint22`)
**Status:** ACTION REQUIRED ⚠️

**Problem:**
The Files module web UI (`https://mint22:15443/apps/files`) crashes with:
```
42703: column f.LinkTarget does not exist POSITION: 146
```

**Root cause:**
Migration `20260309093919_AddSymlinkSupport` added a `LinkTarget` column (nullable `text`) to the `FileNodes` table. The code (EF queries in `SyncService`, `FileService`, `FilesGrpcService`) references this column, but the migration has not been applied to the PostgreSQL database on mint22.

**Migration file:** `src/Modules/Files/DotNetCloud.Modules.Files.Data/Migrations/20260309093919_AddSymlinkSupport.cs`

**Fix — Option A (EF CLI):**
```bash
cd /path/to/dotnetcloud
git pull origin main
dotnet ef database update --project src/Modules/Files/DotNetCloud.Modules.Files.Data --context FilesDbContext
```

**Fix — Option B (raw SQL against PostgreSQL):**
```sql
ALTER TABLE "FileNodes" ADD "LinkTarget" text NULL;
```
Then insert the migration into the EF history table so future `database update` calls don't re-run it:
```sql
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260309093919_AddSymlinkSupport', '10.0.0');
```

**Verification:**
1. After applying, restart the DotNetCloud server process on mint22.
2. Open `https://mint22:15443/apps/files` — it should load the file browser without error.
3. Confirm: `SELECT column_name FROM information_schema.columns WHERE table_name = 'FileNodes' AND column_name = 'LinkTarget';` returns one row.

**After completion:**
- Mark this handoff as COMPLETE and archive it.
- Proceed to remaining in-progress phases: Phase 2.8 (Blazor UI components), Phase 2.10 (Android app features — client-side, Windows machine).
- Or begin Phase 3 planning if Phase 2 gaps are deferred.

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
