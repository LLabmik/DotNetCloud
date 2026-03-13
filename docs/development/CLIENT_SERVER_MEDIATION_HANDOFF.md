# Client/Server Mediation Handoff

Last updated: 2026-03-13 (Client sync fixes complete — upload/download contract fixed, subdirectory reconciliation added, server-side download bug identified)

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
- **Client sync fixes complete** (2026-03-13): Upload contract fixed (POST→PUT, chunkHash, existingChunks, CompleteUpload deserialization, 409 handling). Duplicate upload prevention via server tree comparison. Subdirectory download via tree reconciliation. Chunk 404→direct download fallback. 10/12 server files sync correctly. 2 files fail: server returns 400 on direct download for web-UI-uploaded files (`create_admin.cs`, `err.txt`).

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

### Server-Side Download Bug — Server Agent

**Date:** 2026-03-13
**Owner:** Server agent
**Status:** ACTION REQUIRED — Fix direct file download for web-UI-uploaded files

#### Context

Client agent completed all upload and download fixes. Commit `eedd619` on main. E2E testing on Windows11-TestDNC (MSIX v0.22.0-alpha) shows:

- **Upload path**: fully working. Upload contract mismatches fixed (POST→PUT, chunkIndex→chunkHash, existingChunks mapping, CompleteUpload deserialization). Duplicate prevention via server tree comparison working. 409 on chunk upload and CompleteUpload handled gracefully.
- **Download path**: subdirectory file sync fixed via new `ReconcileServerTreeAsync` — walks full server tree after change feed, queues downloads for files missing locally. 5 of 7 subdirectory files downloaded successfully.
- **2 files still failing**: `Test/create_admin.cs` and `Test/err.txt` — these were uploaded via the web UI (not the sync client).

#### The Bug

Files uploaded through the **web UI** have chunk manifest entries in the database but **no actual chunk blobs** in the content-addressable store. When the sync client tries to download:

1. `GET /api/v1/files/{nodeId}/chunks` → **200 OK** with chunk hash list (e.g. `fd250474...` for create_admin.cs, `e3b0c44298fc...` for err.txt)
2. `GET /api/v1/files/chunks/{chunkHash}` → **404 Not Found** (chunk blob doesn't exist in storage)
3. Client falls back to `GET /api/v1/files/{nodeId}/download` → **400 Bad Request**

The 404 on chunk download is expected (web UI doesn't use chunked storage). The client already handles this with a fallback to direct download. But the **direct download endpoint returns 400** instead of serving the file.

#### Specific Node IDs

- `Test/create_admin.cs`: NodeId=`f4ca7c97-e794-4ec5-bfc2-339ddb44c0eb`, chunk hash `fd250474eef08187c32380149ee3f203bc514c39fd5fcf07ab83513d40190c6a`
- `Test/err.txt`: NodeId=`bc099775-abf5-466a-a51f-e12da11d2f40`, chunk hash `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` (SHA-256 of empty content — 0-byte file)

#### Server endpoint to fix

`GET /api/v1/files/{nodeId}/download` in `FilesController.cs` (line ~245 in `DotNetCloud.Modules.Files.Host`).

The `_downloadService.DownloadCurrentAsync(nodeId, caller)` call is either:
- Throwing an exception that gets caught as 400
- Returning null/invalid stream
- Failing because the file content was stored inline (web UI upload) rather than in the chunk store, and the download service only knows how to reassemble from chunks

#### What the server agent should do

1. Pull main (`eedd619`).
2. Reproduce: `curl -k -H "Authorization: Bearer <token>" https://mint22:15443/api/v1/files/f4ca7c97-e794-4ec5-bfc2-339ddb44c0eb/download` — expect 400.
3. Debug why `DownloadCurrentAsync` fails for web-UI-uploaded files.
4. Fix — ensure direct download works for ALL files regardless of upload method (web UI vs sync client).
5. Verify both NodeIds above return 200 with correct file content.

#### Client-side changes (commit eedd619)

Files changed:
- `ApiModels.cs` — `[JsonPropertyName("existingChunks")]` on `PresentChunks`
- `DotNetCloudApiClient.cs` — PUT chunks, direct FileNodeResponse deserialization on CompleteUpload
- `SyncEngine.cs` — `ReconcileServerTreeAsync`, `BuildServerFileMap`, server tree passed to `ScanLocalDirectoryAsync` for dedup
- `ChunkedTransferClient.cs` — 409 catch on chunk upload, 409 catch on CompleteUpload, 404→direct download fallback

All 152 client tests pass.

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
