# Client/Server Mediation Handoff

Last updated: 2026-03-12 (PosixMode migration blocker fixed — all Files migrations applied)

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
- PosixMode migration blocker: fixed (2026-03-12) — all 6 Files migrations applied to production DB.
- Full test suite: 2,106 passed / 0 failed / 2 skipped (env-gated).

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

### PosixMode Migration Blocker FIXED — Server Ready for Client Testing

**Date:** 2026-03-12
**Owner:** Server agent (`mint22`)
**Status:** COMPLETE ✅

**What was completed:**

1. Discovered all 6 Files module migrations were pending against the production `dotnetcloud` database (design-time factory targeted non-existent `dotnetcloud_files_dev`).
2. Recorded `InitialFilesSchema` as applied (tables already existed from prior manual creation).
3. Applied 4 pending migrations using `--connection` override:
   - `AddFileVersionScanStatus` → `ScanStatus` on `FileVersions`
   - `AddCdcChunkMetadata` → `ChunkSizesManifest` on `UploadSessions`, `ChunkSize`/`Offset` on `FileVersionChunks`
   - `AddSyncCursorSupport` → `SyncSequence` on `FileNodes`, `UserSyncCounters` table
   - `AddPosixPermissions` → `PosixMode`/`PosixOwnerHint` on `FileNodes`, `FileVersions`, `UploadSessions`
4. Rebuilt, republished, and restarted `dotnetcloud.service`.

**Verification:**
- All 7 migrations recorded in `__EFMigrationsHistory`.
- All new columns confirmed via `information_schema`.
- Health endpoint: 200 Healthy.
- Files API: returns 401 (auth required), no column errors.
- Server logs: clean — no DB errors.
- Test suite: 2,106 passed / 0 failed / 2 skipped (env-gated).

**Runtime verification:**
```
$ systemctl status dotnetcloud.service
● dotnetcloud.service - DotNetCloud Core Server
     Active: active (running) since Thu 2026-03-12 02:40:57 CDT
   Main PID: 98178 (/home/benk/.../server-baremetal/DotNetCloud.Core.Server.dll)
```

**Next action:**
- Client agent can now test Web UI at `https://mint22:15443/apps/files` — should load without errors.
- Sync, Android, and desktop client testing are all unblocked.
- All Phase 2 work is complete. Phase 3 planning can begin when ready.

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
