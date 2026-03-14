# Client/Server Mediation Handoff

Last updated: 2026-03-14 (mint-dnc-client onboarding handoff created for Linux sync client implementation/testing)

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
- **Server download bug fixed** (2026-03-13): `BuildStreamFromVersionAsync` in `DownloadService` now (1) serves empty stream for 0-byte files without touching storage, (2) throws `NotFoundException` (→ HTTP 404) instead of `InvalidOperationException` (→ HTTP 400) when a chunk blob is missing. 2 new tests added. Deployed to mint22 commit `f60541c` — health Healthy.
- **Flaky CDC test fixed** (2026-03-13): `ChunkAndHashCdcAsync_SmallData_ReturnsSingleChunk` used 1KB data with minSize=512 — Phase 2 boundary detection could split it into 2 chunks. Fixed by using 256 bytes (strictly < minSize). Commit `6b89a60`.
- **Client 404 handling hardened** (2026-03-13): `SyncEngine.ApplyLocalChangesAsync` now treats `PendingDownload` HTTP 404 as terminal and moves operation to failed queue without retry loop. Added `SyncAsync_PendingDownloadNotFound_MovesToFailedWithoutRetry` test.
- **Handoff verification refresh** (2026-03-13): After pull to latest main, client regression suites re-run on Linux workspace and passing: `SyncEngineTests` (33/33) and `ChunkedTransferClientTests` (23/23). Runtime E2E validation remains required on `Windows11-TestDNC`.
- **Windows11 runtime probe completed** (2026-03-13, SyncTray 0.23.0): `err.txt` synced correctly as 0-byte local file, but `create_admin.cs` still entered retry churn after direct-download 404 due `HttpRequestException` path lacking `StatusCode`.
- **Client retry-loop hotfix implemented** (2026-03-13): `SyncEngine` 404 handling now treats NotFound as terminal when either `StatusCode == 404` **or** exception message indicates 404/Not Found. Added regression test `SyncAsync_PendingDownloadNotFoundWithoutStatusCode_MovesToFailedWithoutRetry`.
- **Windows11 runtime re-check completed** (2026-03-13, SyncTray 0.23.1): per-operation exponential retries are gone (`Download operation ... Moving to failed queue without retry`), but `create_admin.cs` is still re-queued each pass by tree reconciliation.
- **Client tree-requeue hotfix implemented** (2026-03-13): reconciliation now skips re-queue for files with recent terminal 404 download failures; added LocalStateDb tests. Patched package built: `dotnetcloud-sync-tray-win-x64-0.23.2-alpha.msix`.
- **Windows11 final runtime verification completed** (2026-03-13, SyncTray 0.23.2): `err.txt` exists locally with 0 bytes, `create_admin.cs` remains missing (expected), and latest sync pass completed with `RemoteChanges=0, LocalQueued=0, LocalApplied=0` (no requeue churn observed).

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

### Linux Sync Client Bring-Up on `mint-dnc-client`

**Date:** 2026-03-14
**Owner:** Client agent first, then server agent if API/contract issues are found
**Status:** IN PROGRESS

Goal: onboard a third machine (`mint-dnc-client`, Linux Mint 22) as the primary Linux runtime for finishing sync client implementation and validating end-to-end behavior against `mint22`.

#### Scope (Client Agent)
- Pull latest `main` on `mint-dnc-client`.
- Build and run focused sync client tests in Linux environment:
	- `tests/DotNetCloud.Client.SyncService.Tests`
	- `tests/DotNetCloud.Client.Core.Tests` (if touched by sync path)
- Run desktop/sync runtime against `https://mint22:15443/` using Linux local sync directory.
- Validate these flows with raw logs and timestamps:
	- OAuth login/token mint and refresh path
	- Cursor-based remote changes download path
	- Local scan upload path (new + modified files)
	- Conflict and retry behavior (ensure no infinite requeue loops)
	- 0-byte file handling and missing-chunk 404 terminal behavior
- Record exact Linux runtime/package details used for the run (build config, binary path, version string).

#### Scope (Server Agent, only if needed)
- If client reports API/contract/runtime failures, pull latest `main` and reproduce against `mint22`.
- Fix server-side defects with tests first, redeploy, and provide endpoint + log evidence.
- Confirm running binaries are current (no stale publish output) and include verification command/output.

#### Required Evidence Back in Next Handoff Update
- Commit hash.
- Exact commands run on `mint-dnc-client`.
- Raw failing/passing log excerpts with timestamps.
- Endpoint URLs and HTTP status codes involved.
- Expected vs actual for each validated flow.

#### Exit Criteria
- Linux Mint client completes at least one clean full sync pass without retry churn.
- Upload and download paths both verified on Linux runtime.
- Any discovered server blockers either fixed and verified or documented as explicit next blocker.

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
