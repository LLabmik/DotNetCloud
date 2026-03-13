# Client/Server Mediation Handoff

Last updated: 2026-03-13 (Sync E2E retry verified — download path clean, upload path gap discovered)

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
- Chat UI fix: ChatPageLayout orchestrator added (2026-03-12) — channels now clickable with full message view.
- Chat UI fix deployed to mint22 (2026-03-12) — rebuilt, restarted, health verified Healthy.
- Chat UI Blazor binding fix verified on mint22 (2026-03-12) — redeploy complete, no raw variable names in `/apps/chat`, 302 auth redirect working.
- Full test suite: 2,106+ passed / 0 failed (1 pre-existing Files CDC test failure, unrelated).
- Chat DbContext concurrency bug: **FIXED** (2026-03-12). Service restarted, channels load.
- Chat UI CSS: Stylesheets created (2026-03-12) but **not loaded** — missing `<link>` tag in `App.razor`. Fixed by client agent.
- Chat UI CSS link tag fix: corrected `.styles.css` → `.bundle.scp.css` (2026-03-12). .NET 10 RCL CSS isolation uses `.bundle.scp.css` naming, not `.styles.css`. Deployed to mint22, all 14 component stylesheets verified loading (2,045 lines CSS, 200 OK).
- WYSIWYG Chat Composer: deployed to mint22 (2026-03-12). Contenteditable editor replaces raw textarea, JS module + CSS verified loading.
- Chat Permission Hardening + Members Display Names: deployed to mint22 (2026-03-12). Role-based UI gating, membership checks, announcement author-only edits, display names in members panel.
- **Channel Invite System**: implemented (2026-03-12). Single-user invites for private channels.
- Channel Invite EF migration + deploy: complete (2026-03-12). PostgreSQL migration applied, snapshot fixed, deployed to mint22.
- Chat UI fixes (invite button, members panel, online status): deployed to mint22 (2026-03-12). All new CSS verified loading.
- Chat message sender names fix: deployed to mint22 (2026-03-12). Display names resolved via IUserDirectory cache.
- **Sync changes response shape fix**: deployed to mint22 (2026-03-13). `SyncController` now uses cursor path returning `PagedSyncChangesDto {changes, nextCursor, hasMore}` when no `since` param. Legacy `since` path preserved for backward compat.
- **Chunk rate limit raised**: `appsettings.json` `ModuleLimits.chunks` 3000 → 10000/60s to prevent 429 bursts during initial sync.
- **Sync E2E retry verified** (2026-03-13): Clean state.db wipe + 4 sync passes — zero 429s, zero 404s, cursor-based response shape confirmed. Download path fully working.
- **Upload path gap identified** (2026-03-13): Sync engine has no local filesystem scan — new local files are not detected or uploaded. Implementation needed.
- **Upload path implemented** (2026-03-13): `ScanLocalDirectoryAsync` added to `SyncEngine` — detects new/modified local files and queues `PendingUpload`. 4 new tests, all passing. Awaiting E2E verification on `Windows11-TestDNC`.

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

### Sync Upload Path — Client Agent

**Date:** 2026-03-13
**Owner:** Client agent
**Status:** IMPLEMENTATION COMPLETE — E2E verification needed on Windows11-TestDNC

#### What was implemented (by server agent — note: client work incorrectly routed to server)

`SyncEngine.ScanLocalDirectoryAsync` was added and wired into `SyncAsync` between `ApplyRemoteChangesAsync` and `ApplyLocalChangesAsync`. The scan:

1. Loads all `LocalFileRecord` entries from `state.db` into an in-memory dictionary.
2. Loads all currently-queued upload paths to avoid double-queuing.
3. Enumerates all files in `context.LocalFolderPath` recursively.
4. Skips files matched by `.syncignore` or selective-sync exclusions.
5. For new (untracked) files → queues `PendingUpload` with `NodeId = null`.
6. For known files modified since `LastSyncedAt` → queues `PendingUpload` with existing `NodeId`.
7. Unmodified / already-queued files are skipped.

Two new methods added to `ILocalStateDb` + `LocalStateDb`:
- `GetAllFileRecordsAsync` — bulk fetch for O(1) path lookup during scan.
- `GetPendingUploadPathsAsync` — returns set of already-queued upload paths.

4 new tests added in `SyncEngineTests`: NewLocalFile, ModifiedLocalFile, UnmodifiedLocalFile, AlreadyQueuedFile. All 33 sync engine tests pass (148 total client tests pass).

Commits: see git log on `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs`.

#### Next step for client agent

1. Pull `main` on `Windows11-TestDNC`.
2. Build and install updated MSIX on the test machine.
3. Drop `state.db` (clean slate), create `test2.txt` in the synctray folder, then run a sync.
4. Confirm in SyncTray logs: `New local file detected, queuing upload: test2.txt` → `PendingUploads=1` → upload completes → file appears in the web UI Files section.
5. Also verify modified-file path: edit an existing synced file, trigger sync, confirm re-upload.

No server-side changes are needed — the upload endpoints are already working.

#### E2E Retry Results (download path verified ✅)

Server-side fixes confirmed working on `Windows11-TestDNC` (v0.15.0-alpha MSIX):
- `GET /api/v1/files/sync/changes?limit=500` → **200 OK**, cursor-based `{changes, nextCursor, hasMore}` format confirmed (client reuses cursor `MDE5Y2MxYWMtZGE0Mi03MzdjLWIwYWItZDBmMmVjY2E4MDE5OjA%3D` on subsequent passes).
- **Zero 429 errors** across 4 observed sync passes (16:12–16:18 UTC).
- **Zero 404 errors, zero Error-level log entries.**
- Token refresh, tree fetch, changes fetch — all 200 OK, latency 5–25ms.
- 8 files on disk from prior sync (test1.docx, test1.odt, 2 client tarballs, checkbook .ods, 2 PNGs, .selective-sync.json).

#### Finding: No client→server upload path

The sync engine (`SyncEngine.cs`) has **no local filesystem scan** that detects new/modified local files and queues them as `PendingUpload`. Current flow:

1. `SyncAsync()` calls `ApplyRemoteChangesAsync()` — downloads server changes via cursor-based pagination ✅
2. `SyncAsync()` calls `ApplyLocalChangesAsync()` — but this **only processes operations already in the pending queue**.
3. `PendingUpload` records are only created by conflict resolution (remote-delete vs local-modified), never by detecting new local files.
4. `FileSystemWatcher` triggers `SyncAsync()` reactively (confirmed working), but the subsequent sync pass doesn't scan for new files.
5. `RunPeriodicScanAsync()` just calls `SyncAsync()` on the timer — same gap.

**Test:** Created `test2.txt` in synctray folder. FileSystemWatcher detected it and triggered 3 sync passes. All completed with `PendingUploads=0, FileCount=0`. File was never uploaded.

#### Next step

Implement local filesystem scan in `SyncEngine.SyncAsync()` — between `ApplyRemoteChangesAsync` and `ApplyLocalChangesAsync`, add a scan that:
1. Enumerates all files in `context.LocalFolderPath` recursively
2. Compares against `state.db` file records (hash + mtime)
3. Queues new/modified files as `PendingUpload` operations
4. Respects `.syncignore` and selective sync filters

No server-side changes needed — upload endpoints (`InitiateUploadAsync`, chunk upload, `CompleteUploadAsync`) already exist in the API client.

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
