# Client/Server Mediation — Archived Context

Archived: 2026-03-08. Full git history preserved in commits up to `8e02b52`.

This file contains historical reference from the client/server mediation sessions.
Only consult this if you encounter a regression or need to understand a past fix.

## Archived: Linux Runtime Verification — Blocked by Upload/Download API Errors (2026-03-14)

Archived from Active Handoff on 2026-03-14 when replaced by upload chunk failure diagnostic task.

- Runtime verification on `mint-dnc-client` partially completed: OAuth login, cursor/tree fetch, upload initiate all working.
- Blockers found: `upload/initiate` intermittent 429 under burst (168 calls, 9 with 429), and `GET /files/{id}/chunks` returning 404 for some nodes.
- P0 sync hardening fixes (atomic SyncSequence, unique name constraint, atomic chunk refcount) deployed to `mint22` to address root causes.
- New issue discovered post-P0: client uploads initiate but chunks never arrive at server. Zero `PUT .../chunks/{hash}` requests in server logs. Upload path has never been exercised from desktop clients (all prior uploads via web UI).

## Archived: Linux Sync Bring-Up Execution Trail (2026-03-14)

Archived from Active Handoff on 2026-03-14 to enforce single active task policy.

### Summary of Archived Execution Updates

- Initial Linux bring-up on `mint-dnc-client` validated non-root service/tray startup, IPC connectivity, and OAuth discovery to `https://mint22:15443/.well-known/openid-configuration` (HTTP 200), but did not complete interactive OAuth login through a full pass.
- Reconciliation hardening landed in `SyncEngine` to remove stale `LocalFileRecord` entries when local files are missing and re-queue missing downloads. Regression test added: `SyncAsync_ReconcileWithStaleFileRecord_RemovesRecordAndQueuesDownload`.
- Linux runtime hardening follow-up landed:
    - remote path resolution fallback using `ParentId` + path map,
    - case-insensitive node-type helpers,
    - bounded/jittered 429 Retry-After handling,
    - selective-sync folder browser directory-type handling.
- Paced E2E run confirmed upload-path success (initiate 201, chunk/complete 409-as-success) and cursor/tree calls, but exposed rapid post-pass re-entry leading to `/api/v1/files/sync/tree` 429 pressure.
- Per-user singleton enforcement landed for both SyncService and SyncTray using user-local lock files to prevent duplicate same-user processes.
- Sync re-entry coalescing hardening landed in `SyncEngine`:
    - overlapping `SyncAsync` requests now collapse into one trailing rerun,
    - regression test added: `SyncAsync_BurstWhileRunning_CoalescesIntoSingleTrailingPass`.

### Archived Validation Snapshot

- `DotNetCloud.Client.Core.Tests`: 160 passed, 0 failed.
- `DotNetCloud.Client.SyncService.Tests`: 27 passed, 0 failed.
- `DotNetCloud.Client.SyncTray.Tests`: 72 passed, 0 failed.

### Archived Remaining Work Item

- Complete interactive Linux OAuth + one clean upload/download full pass on `mint-dnc-client` with timestamped logs proving no immediate rapid re-entry churn.

## Archived: Sync 404 Runtime Verification Closeout (2026-03-13)

Client agent completed final runtime verification on `Windows11-TestDNC` with SyncTray `0.23.2.0` after two client hardening follow-ups (404 terminal classification + reconciliation requeue suppression).

**Final observed outcomes:**
- `err.txt` exists locally at `C:\Users\benk\Documents\synctray\Test\err.txt` with size `0` bytes.
- `create_admin.cs` remains missing locally (expected because server blob is missing and endpoint returns 404).
- Latest pass log (UTC):
    - `Sync pass starting ...` at `2026-03-13T22:55:59.7838693Z`
    - `Sync pass complete ... DurationMs=491, RemoteChanges=0, LocalQueued=0, LocalApplied=0` at `2026-03-13T22:56:00.2752546Z`

**Conclusion:** retry churn and reconciliation requeue churn are both resolved for this incident. No active cross-machine blocker remains.

## Archived: Sync E2E Retry — Client Verification (2026-03-13)

Client agent verified the server-side sync fixes (cursor path + rate limit raise) end-to-end on `Windows11-TestDNC`:

**Test procedure:** Wiped `state.db` in MSIX service context (`ff717557-133a-49b7-b91f-0ab8cecaceee`) at `C:\WINDOWS\system32\config\systemprofile\AppData\Local\DotNetCloud\Sync\`, preserved `.tok` auth file, restarted `DotNetCloudSync` service.

**Results (4 sync passes observed: 16:12, 16:17, 16:17:47, 16:18:09 UTC):**
- `GET /api/v1/files/sync/changes?limit=500` → **200 OK** (6ms). Client received cursor-based Object format (proven by subsequent pass using `cursor=MDE5Y2MxYWMtZGE0Mi03MzdjLWIwYWItZDBmMmVjY2E4MDE5OjA%3D`). ✅
- `GET /api/v1/files/sync/tree` → **200 OK** (8–25ms). ✅
- `POST /connect/token` (refresh) → **200 OK** (209ms). ✅
- **Zero 429 errors** across all 4 passes. ✅
- **Zero 404 errors.** ✅
- **Zero Error-level log entries.** ✅
- Files on disk: 8 files synced (from prior download), local state.db cursor persisted correctly.
- FileSystemWatcher reactive sync working — file creation/rename triggers immediate sync passes.

**Finding — Upload gap:** The sync engine currently only handles server→client (download). New local files are NOT detected or queued as `PendingUpload`. `ApplyLocalChangesAsync` only processes already-queued pending operations; there is no local filesystem scan that compares local files against state.db to detect new/modified files. Client→server upload of new files is not yet implemented.

## Archived: Sync Changes Shape + Rate Limit Fix (2026-03-13)

Server agent fixed two sync blockers:
1. **`SyncController` cursor path**: `Core.Server/Controllers/SyncController.cs` was using the legacy `since` path (returning `IReadOnlyList<SyncChangeDto>` flat array) instead of the cursor path (`PagedSyncChangesDto`). Updated controller to match `Files.Host` implementation — now supports `cursor`, `since` (legacy), `limit`, and `folderId` params. No cursor/since → cursor path returns `{changes, nextCursor, hasMore}`.
2. **Chunk download rate limit**: `appsettings.json` `ModuleLimits.chunks` raised from 3000 → 10000 permits/60s to prevent 429s during initial sync bursts.

Deployed to mint22 (2026-03-13). Binary timestamp: `2026-03-13 08:15:33`. Health: Healthy. Endpoint verified: `GET /api/v1/files/sync/changes?limit=5` → 401 (auth required, correct).

## Archived: Chat Message Sender Names Fix (2026-03-12)

Server agent deployed display name resolution for chat messages via `_displayNameCache` + `ResolveDisplayNamesAsync()`. All message paths (initial load, load-more, send, edit, search) resolve sender names. Deployed to mint22, verified Healthy.

## Archived: Chat UI Fixes — Invite, Online Status, MemberListPanel (2026-03-12)

Server agent deployed channel invite UI, fixed member online status (current user shows "Online"), fixed MemberListPanel CSS class mismatch, overhauled MemberListPanel dark theme styling. All verified on mint22 — health checks Healthy, CSS bundle 200 OK.

## Archived: Channel Invite System — Migration + Deploy (2026-03-12)

Server agent generated PostgreSQL EF migration for ChannelInvite table, fixed corrupted SQL Server model snapshot (was PostgreSQL in Designer but SQL Server in snapshot), applied migration to production DB, redeployed to mint22. All health checks Healthy. 283 chat tests pass.

## Archived: Chat Permission Hardening + Members Display Names (2026-03-12)

Deployed to mint22. Role-based UI gating, membership checks, announcement author-only edits, display names in members panel. All verified healthy.

## Archived: Chat UI CSS — Visual Verification (2026-03-12)

Server agent created 8 new `.razor.css` files and overhauled 6 existing ones (14 total chat component stylesheets). Deployed to mint22, health verified Healthy, 263 Chat tests passing.

**Client verification result:** FAILED — CSS not loading. Root cause: the `<link>` tag for `DotNetCloud.Modules.Chat.styles.css` was never added to `App.razor`. All 14 stylesheets compiled correctly but the browser never requested them. Fix applied by client agent (added missing link tag). See Active Handoff for details.

---

## Resolved Issues (Issues #1–#22, 2026-03-07 to 2026-03-08)

| # | Issue | Root Cause | Fix | Side |
|---|-------|-----------|-----|------|
| 1 | `invalid_client` on authorize | `dotnetcloud-desktop` not registered | Added OIDC client seeder with upsert | Server |
| 2 | `invalid_scope` on authorize | `files:read`/`files:write` not registered | Added scope registration + client permissions | Server |
| 3 | `404` on `GET /connect/authorize` | Only `POST` mapped | Changed to `GET`+`POST` mapping | Server |
| 4 | Login redirect to wrong path | `/login` instead of `/auth/login` | Corrected redirect path | Server |
| 5 | Placeholder JSON on authenticated authorize | Not calling OpenIddict `SignIn` | Reworked passthrough to issue `SignIn` | Server |
| 6 | TLS errors on token exchange | Self-signed cert not trusted by client | Client-side bypass for local/LAN hosts | Client |
| 7 | Token JSON field mapping | Snake_case `access_token` etc. not mapped | Client DTO mapping + typed HttpClient | Client |
| 8 | `UserId = Guid.Empty` | Access tokens encrypted (JWE); no OIDC claims; no userinfo endpoint | `DisableAccessTokenEncryption()`, DB claim lookup, userinfo registration | Server |
| 9 | Sync endpoints `404` | `SyncController` in `Files.Host` (not loaded) | Added `SyncController` to `Core.Server` | Server |
| 10 | TLS errors on sync API calls | `DotNetCloudSync` named HttpClient had no cert bypass | Added cert bypass to named client registration | Client |
| 11 | Sync calls required `userId` query param | Server controller bound `userId` from query string | Derived `CallerContext` from bearer claims; removed `userId` param | Server |
| 12 | Sync response deserialization mismatch | Server returned envelope-wrapped sync payloads | Changed sync responses from `Ok(Envelope(...))` to `Ok(...)` | Server |
| 13 | Token refresh was a stub | `RefreshAccessTokenAsync` did nothing | Implemented actual refresh: API call → save tokens → update accessor | Client |
| 14 | Missing `client_id` in refresh request | OpenIddict requires `client_id` for public clients | Added `clientId` parameter to `RefreshTokenAsync` | Client |
| 15 | `DateTime` serialization bug — tokens appear unexpired | `DateTimeKind` lost after JSON roundtrip | Changed `ExpiresAt` from `DateTime` to `DateTimeOffset` across client chain | Client |
| 16 | Refresh token `invalid_grant` | Ephemeral RSA keys regenerated on every restart | Created `OidcKeyManager` for persistent PEM key files; fixed config key names | Server |
| 17 | Sync API returns 403 with valid bearer token | No `[Authorize]` attribute; default auth scheme was cookies | Added OpenIddict bearer `[Authorize]` to `FilesControllerBase` | Server |
| 18 | Files API returns 403 "Caller user ID does not match" | 20 `FilesController` endpoints used `[FromQuery] Guid userId` | Changed all to `GetAuthenticatedCaller()`; removed `userId` param | Server |
| 19 | Files API responses double-envelope wrapped | `Ok(Envelope(data))` + `ResponseEnvelopeMiddleware` | Removed `Envelope()` calls; middleware handles wrapping | Server |
| 20 | Sync changes endpoint returns 500 | `since` parsed as `DateTime Kind=Unspecified`; Npgsql rejects for `timestamptz` | `DateTime.SpecifyKind(since, DateTimeKind.Utc)`; added general exception handler | Server |
| 21 | Chunk manifest deserialization failure | Server returns `string[]`; client expected object with `Chunks`+`TotalSize` | Client deserializes `List<string>` and maps to `ChunkManifestResponse` | Client |
| 22 | Sync flattens directory structure | `ResolveLocalPathAsync` used filename only, ignoring `ParentId` | Client fetches folder tree, builds `nodeId→path` map, creates dirs before files | Client |

## Verified State at Milestone Completion

### Server (mint22, commit `69dd5eb`)
- Build: 0 errors, 0 warnings
- Tests: 304 server + 85 auth + 513 files = 902 passed
- Health: `https://localhost:15443/health/live` → Healthy

### Client (Windows11-TestDNC, commit `6a9ccb0`)
- Build: 0 errors, 0 warnings
- Tests: 53 Core + 24 SyncService + 24 SyncTray = 101 passed
- Sync: 7 files into correct subdirectories with full directory hierarchy

### End-to-End Flow Verified
OAuth login → token exchange → sync changes → tree → reconcile → chunk manifest → chunk download → file assembly. 7 files synced into `clients/`, `Finance/`, `Pictures/`, `Test/`, and root.

---

## Resolved Issues (Issues #23–#42, 2026-03-08 to 2026-03-09)

Archived: 2026-03-09. Full git history preserved in commits `c69aeac` through `c70bd47`.

| # | Issue | Commit(s) | Side |
|---|-------|-----------|------|
| 23 | Batch 1 Task 1.1 — Sync Service Logging | `c69aeac` | Client |
| 24 | Batch 1 Task 1.1b — Audit Logging | `c585dae` | Server |
| 25 | Batch 1 Task 1.2 — Request Correlation IDs | `97afdd8` | Client |
| 26 | Batch 1 Task 1.3 — Rate Limiting | `4570c16` | Server |
| 27 | Batch 1 Task 1.4 — Chunk Integrity (SHA-256) | Windows 2026-03-08 | Client |
| 28 | Batch 1 Tasks 1.5/1.6/1.7 — Retry / WAL / Upload Queue | `1aa6b18` | Client |
| 29 | Batch 1 Tasks 1.8/1.9 — Temp-file atomicity / Malware scanning stub | `82ca53b` | Client |
| 30 | Batch 2 Task 2.1 — CDC chunking (FastCDC) | `3a7e0ae` / `bc9e08a` | Both |
| 31 | Batch 2 Task 2.2 — Streaming upload/download pipeline | `7cbc12e` | Both |
| 32 | Batch 2 Task 2.3 — Brotli compression for chunk transfers | `032f6a2` | Both |
| 33 | Batch 3 Task 3.1 — `.syncignore` pattern matching | `a9c6812` | Client |
| 34 | Batch 3 Task 3.2 — Persistent upload sessions (crash recovery) | `4243328` | Client |
| 35 | Batch 3 Task 3.3 — Locked file handling (VSS on Windows) | `b971551` | Client |
| 36 | Batch 3 Task 3.4 — Per-file transfer progress in Tray UI | `7f93226` | Client |
| 37 | Batch 3 Task 3.5 — Conflict resolution UI (DiffPlex 5-strategy) | `8508afc` | Client |
| 38 | Batch 2 Tasks 2.4+2.5 — Server-issued sync cursor + paginated changes | `c81495d` / `1a9c4c6` | Both |
| 39 | Batch 2 Task 2.6 — ETag / chunk-download file-system cache | `c81495d` / `1a9c4c6` | Both |
| 40 | Batch 3 Task 3.6 — Idempotent upload operations (hash pre-check) | `3504932` | Client |
| 41 | Batch 4 Task 4.1 — Case-sensitivity conflict detection (NAME_CONFLICT) | `3504932` | Client |
| 42 | Batch 4 Task 4.2 — POSIX permission metadata sync | `c70bd47` | Both |
| 43 | Batch 4 Task 4.3 — Symbolic link policy | Server `d3a6422`, Client `1cd594a` | Both |
| 44 | Batch 4 Task 4.4 — inotify/inode health monitoring | Server `d3a6422`, Client `1cd594a` | Both |
| 45 | Batch 4 Task 4.5 — Path length/filename compatibility validation | Server `d3a6422`, Client `1cd594a` | Both |

### Verified State at Batch 1–4.5 Completion

**Server (mint22, commit `d3a6422`):** Build 0 errors, all tests pass.  
**Client (Windows11-TestDNC, commit `1cd594a`):** Build 0 errors, 123/123 tests pass.

---

## Sprint A Archive (Phase 1.19.2, archived 2026-03-10)

The following Sprint A updates were moved from
`CLIENT_SERVER_MEDIATION_HANDOFF.md` to keep the active handoff document small.
Active entries should remain in the handoff doc; older completed updates belong here.

### Sprint A Update #1 - Server Kickoff (`phase-1.19.2`)

**Date:** 2026-03-10  
**Owner:** Server (`mint22`)  
**Status:** in-progress ☐

### Send to Server Agent
Execute Sprint A for `phase-1.19.2` in `tests/DotNetCloud.Integration.Tests/`.

Required coverage:
1. REST CRUD/tree/search/favorites end-to-end tests.
2. Chunked upload E2E tests (initiate, upload, complete, dedup behavior, quota rejection path).
3. Version/share/trash end-to-end tests.
4. WOPI and sync endpoint smoke tests (auth enforcement + payload shape).
5. Provider matrix execution notes: PostgreSQL required; SQL Server if available.

### Request Back
- commit hash: `<SERVER_COMMIT_HASH>`
- exact tests added/updated (file paths + test names):
	- `<tests/DotNetCloud.Integration.Tests/...>`
	- `<TestClass.TestName>`
- raw endpoint/URL used for any failing test:
	- `<METHOD /api/v1/...>`
- raw error/query params:
	- `<error payload / query string>`
- raw log lines around failure (timestamped):
	- `<2026-03-10T..Z ...>`
- intentionally deferred coverage (if any):
	- `<deferred item + reason>`

### Server Progress Checklist
- ✓ Test-gap inventory posted
- ✓ New REST integration tests added
- ✓ New chunked upload E2E tests added
- ✓ New version/share/trash E2E tests added
- ✓ WOPI + sync smoke tests added
- ☐ PostgreSQL run completed
- ✓ SQL Server run attempted/documented
- ✓ Evidence returned (commit/tests/logs)

### Build/Test Commands (run from repo root on `mint22`)
- `dotnet build tests/DotNetCloud.Integration.Tests/DotNetCloud.Integration.Tests.csproj`
- `dotnet test tests/DotNetCloud.Integration.Tests/DotNetCloud.Integration.Tests.csproj`
- `dotnet test tests/DotNetCloud.Integration.Tests/DotNetCloud.Integration.Tests.csproj --filter "FullyQualifiedName~Files"`

### Sprint A Update #2 - Initial Inventory + First Expansion (Server)

**Date:** 2026-03-10  
**Owner:** Server (`mint22`)  
**Status:** in-progress ☐

**Coverage inventory before changes (integration project):**
- Existing Files coverage concentrated in isolation tests (`FilesRestIsolationIntegrationTests`, `FilesGrpcIsolationIntegrationTests`).
- Non-files coverage present for auth/health and DB matrix scaffolding.
- Gaps confirmed: broader CRUD workflow assertions, sync/WOPI smoke in REST class, and deeper end-to-end chunk/version/share/trash matrix scenarios.

**Completed in this update:**
- Added first Sprint A REST workflow expansion in `tests/DotNetCloud.Integration.Tests/Api/FilesRestIsolationIntegrationTests.cs`:
	- `FileListSearchFavoritesAndRecent_WorkForOwner`
	- `SyncEndpoints_TreeChangesAndReconcile_ReturnSuccess`
	- `WopiDiscoveryEndpoints_ReturnExpectedShape`
- Hardened payload handling in integration assertions for raw + envelope responses:
	- `tests/DotNetCloud.Integration.Tests/Infrastructure/ApiAssert.cs`
- Added `DataOrRoot` handling in Files REST integration tests for mixed response shapes.

**Test evidence (local run on mint22):**
- Command:
	- `dotnet test tests/DotNetCloud.Integration.Tests/DotNetCloud.Integration.Tests.csproj --filter "FullyQualifiedName~FilesRestIsolationIntegrationTests"`
- Result:
	- total: 7, failed: 0, succeeded: 7, skipped: 0

**Still pending in Sprint A:**
- Chunked upload E2E depth (resume/dedup/quota permutations beyond isolation path)
- Version/share/trash end-to-end depth expansion
- PostgreSQL and SQL Server matrix execution notes for new tests

### Sprint A Update #3 - Deeper Files REST E2E Coverage (Server)

**Date:** 2026-03-10  
**Owner:** Server (`mint22`)  
**Status:** in-progress ☐

**Completed in this update:**
- Added deeper Files REST integration tests in `tests/DotNetCloud.Integration.Tests/Api/FilesRestIsolationIntegrationTests.cs`:
	- `UploadInitiation_ReportsExistingChunks_ForDedup`
	- `ShareLifecycle_CreateUpdateRevoke_WorksForOwner`
	- `VersionEndpoints_ListGetAndLabel_WorkForUploadedFile`
	- `TrashLifecycle_ListSizeAndPurge_WorksForOwner`
- Expanded response-shape unwrapping helper in the same file to handle nested `data` envelopes.

**Test evidence (local run on mint22):**
- Command:
	- `dotnet test tests/DotNetCloud.Integration.Tests/DotNetCloud.Integration.Tests.csproj --filter "FullyQualifiedName~FilesRestIsolationIntegrationTests"`
- Result:
	- total: 11, failed: 0, succeeded: 11, skipped: 0

- Command:
	- `dotnet test tests/DotNetCloud.Integration.Tests/DotNetCloud.Integration.Tests.csproj --filter "FullyQualifiedName~Files"`
- Result:
	- total: 14, failed: 0, succeeded: 14, skipped: 0

**Remaining Sprint A gaps:**
- Provider matrix execution notes for the expanded suite (PostgreSQL required, SQL Server if available).

### Sprint A Update #4 - Provider Matrix Execution Notes (Server)

**Date:** 2026-03-10  
**Owner:** Server (`mint22`)  
**Status:** in-progress ☐

**Command executed:**
- `dotnet test tests/DotNetCloud.Integration.Tests/DotNetCloud.Integration.Tests.csproj --filter "FullyQualifiedName~MultiDatabaseMatrixTests"`
	- Result: total 21, succeeded 21, failed 0 (in-memory naming-strategy matrix)

- `dotnet test tests/DotNetCloud.Integration.Tests/DotNetCloud.Integration.Tests.csproj --filter "FullyQualifiedName~DockerDatabaseIntegrationTests"`
	- Result: total 12, succeeded 0, skipped 12, failed 0

**Interpretation:**
- SQL Server path was attempted and documented via `DockerDatabaseIntegrationTests`, but no runnable SQL Server source was detected in this environment (tests skipped).
- PostgreSQL real-container execution was also unavailable in this environment (tests skipped), so PostgreSQL-required real-provider confirmation remains blocked pending container/runtime availability.

**Next action to close Sprint A matrix requirement:**
- Re-run `DockerDatabaseIntegrationTests` on a host with Docker available (or with reachable local SQL Server for SQL lane) and attach raw pass/fail logs.

## Sprint A Archive Continuation (Phase 1.19.2 - updates #5-#9, archived 2026-03-10)

### Sprint A Update #5 - Local Verification After Main Pull (Server)

**Date:** 2026-03-10  
**Owner:** Server (`Windows workspace`)  
**Status at time of update:** in-progress ☐

**Command executed:**
- `dotnet test tests/DotNetCloud.Integration.Tests/DotNetCloud.Integration.Tests.csproj --filter "FullyQualifiedName~FilesRestIsolationIntegrationTests"` (11/11)
- `dotnet test tests/DotNetCloud.Integration.Tests/DotNetCloud.Integration.Tests.csproj --filter "FullyQualifiedName~Files"` (14/14)
- `dotnet test tests/DotNetCloud.Integration.Tests/DotNetCloud.Integration.Tests.csproj --filter "FullyQualifiedName~MultiDatabaseMatrixTests"` (21/21)
- `dotnet test tests/DotNetCloud.Integration.Tests/DotNetCloud.Integration.Tests.csproj --filter "FullyQualifiedName~DockerDatabaseIntegrationTests"` (12 skipped)

### Sprint A Update #6 - Remaining Endpoint-Depth Coverage Added (Server)

**Date:** 2026-03-10  
**Owner:** Server (`Windows workspace`)  
**Status at time of update:** in-progress ☐

**Tests added/updated (`FilesRestIsolationIntegrationTests.cs`):**
- `WopiFileEndpoints_CheckGetPut_WorkWithGeneratedToken`
- `VersionRestore_RestoresPreviousContent`
- `TrashRestore_WorkflowRestoresNodeVisibility`
- `PublicShare_WithPassword_RequiresPasswordAndResolvesWithCorrectPassword`
- `BulkOperations_MoveCopyDeleteAndPermanentDelete_ReturnExpectedCounts`

**Results:**
- Files rest filter: 16/16 passed
- Files filter: 19/19 passed

**Iteration note:**
- WOPI token path handled disabled provider guard (`DB_INVALID_OPERATION`) correctly.

### Sprint A Update #7 - Provider Matrix Retry (Server, Linux host)

**Date:** 2026-03-10  
**Owner:** Server (`Linux workspace`)  
**Status at time of update:** in-progress ☐

**Command executed:**
- `docker --version && docker ps --format '{{.Names}}' && dotnet test tests/DotNetCloud.Integration.Tests/DotNetCloud.Integration.Tests.csproj --filter "FullyQualifiedName~DockerDatabaseIntegrationTests"`

**Result:**
- Docker runtime missing on host (`docker` command not found), provider matrix still blocked in this attempt.

### Sprint A Update #8 - Provider Matrix Completed (Server, Linux host)

**Date:** 2026-03-10  
**Owner:** Server (`Linux workspace`)  
**Status at time of update:** completed ✅

**Key points:**
- Docker confirmed available (`Docker version 28.2.2`).
- `DatabaseContainerFixture` hardened with thread-safe Docker detection and `/usr/bin/docker` fallback.
- `DockerDatabaseIntegrationTests`: 12/12 passed.
- PostgreSQL lane: 6/6 passed.
- SQL Server lane: 6/6 passed.

### Sprint A Update #9 - Client Compatibility Validation Sign-Off (Server, Windows workspace)

**Date:** 2026-03-10  
**Owner:** Server (`Windows workspace`)  
**Status at time of update:** completed ✅

**Command executed:**
- `dotnet test tests\DotNetCloud.Client.Core.Tests\DotNetCloud.Client.Core.Tests.csproj --filter "FullyQualifiedName~DotNetCloudApiClientTests"` (20/20)
- `dotnet test tests\DotNetCloud.Client.Core.Tests\DotNetCloud.Client.Core.Tests.csproj --filter "FullyQualifiedName~SyncEngineTests"` (28/28)

**Assessment:**
- No response-envelope contract regressions.
- No auth-flow regressions for Files/sync endpoint assumptions.

## Resolved Issues Archive (Batch 5, archived 2026-03-10)

### Issue #46: Batch 5 Task 5.1 - Bandwidth Throttling

**Side:** Client-only  
**Status:** completed ✅

**Archived implementation summary:**
- Added `UploadLimitKbps` / `DownloadLimitKbps` on `SyncContext`.
- Implemented `ThrottledStream` token-bucket throttling.
- Implemented `ThrottledHttpHandler` for upload/download stream throttling.
- Wired context-specific throttling pipeline in `SyncContextManager` when limits are non-zero.
- Persisted limits via `sync-settings.json` bandwidth section.
- Added unit tests for unlimited pass-through and throttling behavior.

### Issue #47: Batch 5 Task 5.2 - Selective Sync Folder Browser

**Side:** Client-only  
**Status:** completed ✅

**Archived implementation summary:**
- Added `FolderBrowserItemViewModel` with three-state checkbox propagation.
- Added `FolderBrowserViewModel` for tree load + selective sync rule persistence.
- Added Avalonia folder browser view/dialog integration.
- Integrated folder selection into add-account flow and settings account actions.
- Added tests for tree build, exclusion persistence, parent/child propagation, and indeterminate state.

## Sprint B Archive (Phase 1.15 - updates #1-#2, archived 2026-03-10)

### Sprint B Update #1 - IPC Identity Boundary + Sync Trigger Debounce

**Date:** 2026-03-10  
**Owner:** Server (`Windows workspace`)  
**Status at time of update:** in-progress ☐

**Archived implementation summary:**
- Added transport-level caller identity model (`IpcCallerIdentity`) at IPC boundary.
- Enforced context ownership and caller-filtered `list-contexts`/push events.
- Added deterministic identity-denial semantics (`Caller identity unavailable.`, `Context not found or inaccessible.`).
- Added `sync-now` cooldown no-op response semantics (`started=false`, `reason="rate-limited"`).
- SyncService tests: 27/27 passed.

### Sprint B Update #2 - Disk-Full Detection + SyncError Surfacing

**Date:** 2026-03-10  
**Owner:** Server (`Linux workspace`)  
**Status at time of update:** in-progress ☐

**Archived implementation summary:**
- Added explicit disk-full detection in `SyncEngine` (Win32 disk-full HRESULT and ENOSPC-style Linux/macOS message patterns).
- On disk-full, sync transitions to `SyncState.Error`, pauses additional sync attempts, and surfaces deterministic user-facing `LastError`.
- Added regression coverage in `SyncEngineTests` for disk-full pause behavior.
- Targeted test execution: 1/1 passed.


## Handoff Compaction Backfill (2026-03-11)

Source snapshot: `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md` from commit `e105454` (pre-compaction).

Reason: restore completed historical handoff blocks that were removed during active-file compaction.

# Client/Server Mediation Handoff

Last updated: 2026-03-10 (Phase 2.9 plan pivot: Windows toast + quick reply prioritized)

Purpose: Shared handoff between client-side and server-side agents, mediated by user.

> Archived context (45 resolved issues — initial sync milestone through Batch 4.5) moved to
> [CLIENT_SERVER_MEDIATION_ARCHIVE.md](CLIENT_SERVER_MEDIATION_ARCHIVE.md).
> Full git history in commits up to `1cd594a`.

## Process Rules

- All technical findings and debugging conclusions go in this document, pushed to `main`.
- Mediator role is relay-only — commit notifications and cross-agent request forwarding.
- Keep this handoff lean: when resolved/completed history causes the file to grow beyond active use,
    move completed blocks to `CLIENT_SERVER_MEDIATION_ARCHIVE.md` and leave a short reference pointer.
- Archive cadence for Sprint work: keep only current sprint kickoff + latest 1-2 update entries in this file;
    move older completed updates to archive.
- Start-of-handoff archive check (automatic): at the beginning of every new handoff/update cycle, verify this file only contains active sprint kickoff + latest 1-2 updates; immediately move older completed blocks to `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Moderator relay standard (default): keep relay prompts to one simple line unless extra detail is explicitly requested.
- Preferred relay text for new work handoff: `New commit on main with handoff updates. Pull and resume from the current checklist.`
- Moderator relay mode: mediator sends only short notifications between machines (example: "new handoff update available; pull and continue").
- Git push responsibility (default): assistant pushes commits to remote; moderator relays notifications.
- All complex instructions, technical details, acceptance criteria, and troubleshooting context MUST be written in this handoff document, not relayed verbally.

## Moderator Short-Ping Templates

- `New handoff update is in docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md. Pull latest main and continue.`
- `Please read the latest Sprint section in the handoff doc and post results back there.`
- `New commit on main with handoff updates. Pull and resume from the current checklist.`

## Current Status

**Issues #1–#45 fully resolved.** See [CLIENT_SERVER_MEDIATION_ARCHIVE.md](CLIENT_SERVER_MEDIATION_ARCHIVE.md) for details.

**Batch 4 — ALL ISSUES RESOLVED:**
- Issue #43 (Task 4.3): Symbolic link policy — server ✅ `d3a6422`, client ✅ `1cd594a`
- Issue #44 (Task 4.4): inotify/inode health monitoring — server ✅ `d3a6422`, client ✅ `1cd594a`
- Issue #45 (Task 4.5): Path length/filename validation — server ✅ `d3a6422`, client ✅ `1cd594a`

**Batch 5 — ALL ISSUES RESOLVED:**
- Issue #46 (Task 5.1): Bandwidth throttling — client ✅ complete
- Issue #47 (Task 5.2): Selective sync folder browser — client ✅ complete

**All sync improvement batches (1–5) are now complete.** The sync improvement plan is closed.
See [SYNC_IMPROVEMENT_PLAN.md](SYNC_IMPROVEMENT_PLAN.md) for full history.

**Next work (server):** Sprint A/B/C closeout work in this handoff is complete.
Continue from the next prioritized step in `docs/MASTER_PROJECT_PLAN.md`.

**Acceptance update (2026-03-10):** User accepted Sprint A/B/C completion. Temporary execution plan
`docs/development/REMAINING_PHASE0_PHASE1_3SPRINT_PLAN.md` has been removed per closeout note.
Next prioritized implementation target remains `phase-2.3` (Chat Business Logic & Services).

**Phase 2.3 acceptance update (2026-03-10):** User accepted Phase 2.3 completion.
Temporary file `docs/development/PHASE_2_3_EXECUTION_PLAN.md` has been removed per closeout rule.
Next prioritized implementation target is `phase-2.4` (Chat REST API Endpoints).

## Environment

| | Machine | Detail |
|---|---------|--------|
| Server | `mint22` | `https://mint22:15443/` |
| Client | `Windows11-TestDNC` | Sync dir: `C:\Users\benk\Documents\synctray` |

## Key Architecture Decisions (Carry Forward)

- **Auth:** OpenIddict bearer on all files/sync endpoints via `FilesControllerBase` `[Authorize]`. Persistent RSA keys in `{DOTNETCLOUD_DATA_DIR}/oidc-keys/`. `DisableAccessTokenEncryption()`.
- **API contract:** All endpoints use `GetAuthenticatedCaller()` (no `userId` query param). All return raw payloads — `ResponseEnvelopeMiddleware` wraps automatically. Client unwraps envelope via `ReadEnvelopeDataAsync<T>()`.
- **Sync flow:** changes → tree → reconcile → chunk manifest → chunk download → file assembly. `since` param converted to UTC kind. Client builds `nodeId→path` map from folder tree.
- **Token handling:** Client uses `DateTimeOffset` for expiry. `RefreshTokenAsync` sends `client_id`. `OAuthConstants.ClientId = "dotnetcloud-desktop"`.

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

## Active Handoff

### Client Handoff — Android Contract Alignment (Phase 2.10)

**Date:** 2026-03-10  
**Owner:** Client (`Windows workspace`)  
**Status:** ready for server follow-up 🔄

**Summary of client-side alignment completed:**
- Android chat REST client updated to `api/v1/chat` routes.
- Request parsing updated for server envelope shapes (`success`, `data`, paged payloads).
- `userId` query now supplied on chat/push endpoints by extracting GUID `sub` from access token.
- Push registration/unregister routes switched to `api/v1/notifications/devices/*` with server DTO field names.
- Android project build verified after patch (`dotnet build ... -f net10.0-android` succeeded).

**Files changed (client):**
- `src/Clients/DotNetCloud.Client.Android/Chat/HttpChatRestClient.cs`
- `src/Clients/DotNetCloud.Client.Android/Services/AccessTokenUserIdExtractor.cs` (new)
- `src/Clients/DotNetCloud.Client.Android/Services/FcmPushService.cs`
- `src/Clients/DotNetCloud.Client.Android/Services/UnifiedPushService.cs`

**Server-side follow-up needed (blockers for full end-to-end):**
1. **OIDC client registration for mobile:** Android uses `client_id=dotnetcloud-mobile` + redirect `net.dotnetcloud.client://oauth2redirect`; server seeder currently registers only desktop client (`dotnetcloud-desktop` + localhost redirect).
2. **SignalR contract confirmation:** Android currently expects chat-focused hub patterns; server real-time default hub path is `/hubs/core` and event/method naming needs explicit compatibility confirmation for unread/new-message paths.
3. **Caller identity contract decision:** Current chat host requires `userId` query on most endpoints. If server intends bearer-derived caller identity (no query), publish that change before Android auth hardening is finalized.

### Send to Server Agent
New client contract-alignment patch is on `main`. Please pull and continue with server follow-up for mobile OIDC client seeding, SignalR event contract confirmation, and caller identity strategy (query vs bearer-derived).

### Request Back
- commit hash
- confirmed OIDC client IDs + redirect URIs for Android
- confirmed SignalR hub path + event names for unread/new-message
- final caller identity contract for chat REST endpoints (requires `userId` query or not)

### Client Handoff - Phase 2.9/2.10 Closeout (Windows workspace)

**Date:** 2026-03-10  
**Owner:** Client (`Windows workspace`)  
**Status:** completed and pushed ✅

**Commit hash:** `ed2a000`

**Summary of completed client scope:**
- Step 6 (Phase 2.9): regression checklist pass completed and documented.
- Step 7 (Phase 2.10 in client plan): release hardening pass completed for chat UI surfaces.
- Tracking docs synchronized: `docs/development/PHASE_2_5_2_10_CLIENT_PLAN.md`, `docs/IMPLEMENTATION_CHECKLIST.md`, `docs/MASTER_PROJECT_PLAN.md`.

**Code areas updated (client-side):**
- Chat UI hardening in:
    - `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChannelList.razor`
    - `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChannelList.razor.cs`
    - `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChannelList.razor.css`
    - `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/AnnouncementList.razor`
    - `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/AnnouncementList.razor.cs`
    - `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/AnnouncementList.razor.css` (new)
    - `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/MessageList.razor`
    - `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/MessageList.razor.cs`
    - `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/MessageList.razor.css`
    - `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/DirectMessageView.razor`
    - `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/DirectMessageView.razor.cs`
- SyncTray settings UX polish in:
    - `src/Clients/DotNetCloud.Client.SyncTray/Views/SettingsWindow.axaml`

**Validation evidence:**
- `dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj` -> 252 passed, 0 failed
- `dotnet test tests/DotNetCloud.Client.SyncTray.Tests/DotNetCloud.Client.SyncTray.Tests.csproj` -> 54 passed, 0 failed
- Regression full-suite run (Step 6): `dotnet test` -> 2013 total, 0 failed, 2000 passed, 13 skipped

**Server-facing impact assessment:**
- No API contract-breaking changes introduced in this client update.
- Changes are UI/UX hardening and client behavior only (`IsLoading`/`ErrorMessage` rendering paths, accessibility metadata, empty/error/loading states).
- No immediate server hotfix required from this handoff.

### Send to Server Agent
New commit on main (`ed2a000`) with completed client Phase 2.9 regression pass and Step 7 release hardening. Pull latest main, review handoff section in `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md`, then continue from next prioritized server-owned step in `docs/MASTER_PROJECT_PLAN.md`.

### Client Handoff — Phase 2.8 Remaining Items (Windows workspace)

**Date:** 2026-03-10
**Owner:** Client (`Windows workspace`)
**Status:** completed and pushed ✅

**Baseline commit:** `95a4e2b`

**Scope of work:**

Two checklist items remain open in phase-2.8. Both are UI-side only (Blazor chat module + tests).
Do not modify server projects.

---

#### Item 1 — `ChatNotificationBadge`: distinguish mentions from regular unread (real-time update)

**Current state:**
- `HasMentions` in `ChatNotificationBadge.razor.cs` is `TotalUnread > 0` — it does not separately track mention count.
- `ISignalRChatService.UnreadCountUpdated` has signature `Action<Guid, int>` (channelId, unreadCount only) — no mention count is forwarded to the badge.
- The `.razor` already renders `has-mentions` CSS class based on `HasMentions`, but the class is applied whenever there are any unread messages, not only when there are mentions.

**Required changes:**

1. **`src/Modules/Chat/DotNetCloud.Modules.Chat/Services/SignalRChatService.cs`** —
   Add a second event to `ISignalRChatService`:
   ```csharp
   event Action<Guid, int>? MentionCountUpdated;
   ```
   Follow the same pattern as `UnreadCountUpdated`. Add it to `NullSignalRChatService` with the same `#pragma disable CS0067` stub treatment.

2. **`src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChatNotificationBadge.razor.cs`** —
   - Add a `_mentionsByChannel` dictionary mirroring `_unreadByChannel`.
   - Add `TotalMentions` computed property (`_mentionsByChannel.Values.Sum()`).
   - Change `HasMentions` to `TotalMentions > 0` (not `TotalUnread > 0`).
   - Subscribe to `SignalR.MentionCountUpdated` in `OnInitialized`.
   - Unsubscribe in `Dispose`.
   - Add `internal void ApplyMentionCountUpdate(Guid channelId, int count)` following the same replace-in-dictionary pattern as `ApplyUnreadCountUpdate`.

3. **`src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChatNotificationBadge.razor`** — no change required (razor already uses `HasMentions`).

4. **`tests/DotNetCloud.Modules.Chat.Tests/ChatNotificationBadgeTests.cs`** —
   Add tests for the new `MentionCount` tracking path:
   - `WhenMentionCountUpdatedThenHasMentionsIsTrue`
   - `WhenMentionCountResetToZeroThenHasMentionsIsFalse`
   - `WhenOnlyUnreadWithNoMentionsThenHasMentionsIsFalse`
   - `WhenMultipleChannelMentionCountsThenTotalMentionsSumsAll`

**Server API note:** The server's `UnreadCountUpdated` broadcast already carries `mentionCount` in the payload (`ChatRealtimeService` line 126, payload shape: `{ channelId, count }`). When the hub connection concrete implementation fires `MentionCountUpdated`, it should read the `mentionCount` field from the same `UnreadCountUpdated` hub message. The event signature change (`ISignalRChatService`) must remain compatible with the existing server payload — no server changes are needed.

---

#### Item 2 — `AnnouncementEditor`: preview before publishing — add unit test coverage

**Current state:**
- `AnnouncementEditor.razor` already renders a complete Edit/Preview tab pair with a Markdown-rendered preview pane (`TogglePreview`, `IsPreviewMode`, `RenderMarkdown`).
- `AnnouncementEditor.razor.cs` already has `TogglePreview()`, `IsPreviewMode`, `IsSaveDisabled`, `RenderMarkdown()`, and full `OnParametersSet` population.
- **No test file exists** for `AnnouncementEditor`. The checklist item is `☐ Preview before publishing` and is the only open sub-item.

**Required changes:**

1. **Create `tests/DotNetCloud.Modules.Chat.Tests/AnnouncementEditorTests.cs`** —
   Test the preview toggle and Markdown render path using the same `TestableFoo : AnnouncementEditor` subclass pattern as `ChatNotificationBadgeTests`. Expose `TestIsPreviewMode`, `TestIsSaveDisabled`, `TestTitle`, `TestContent`.

   Required tests:
   - `WhenInitializedThenIsPreviewModeIsFalse`
   - `WhenTogglePreviewCalledThenIsPreviewModeIsTrue`
   - `WhenTogglePreviewCalledTwiceThenIsPreviewModeIsFalse`
   - `WhenTitleAndContentEmptyThenIsSaveDisabledIsTrue`
   - `WhenTitleAndContentSetThenIsSaveDisabledIsFalse`
   - `WhenEditingAnnouncementSetThenFieldsArePopulated` (set `EditingAnnouncement` + `IsEditing = true`, call `OnParametersSet`, assert title/content/priority propagate)
   - `WhenNotEditingThenFieldsAreReset` (set `IsEditing = false`, call `OnParametersSet`, assert fields reset to defaults)

---

**Acceptance criteria (both items):**
- `dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj` — all pass, 0 failed.
- `dotnet build` — succeeded.
- `HasMentions` on badge is `false` when there are unread messages but zero mentions.
- `HasMentions` on badge is `true` only when mention count > 0.

**Request back (strict format):**
```
Phase: 2.8 remaining items
Commit: <hash>
Files:
  - <list of files changed>
Tests added:
  - <test class>.<test name> for each new test
Verification:
  dotnet test ... -> total N, succeeded N, failed 0
  dotnet build -> succeeded
Blockers (if any): <none or description>
```

### Client Result — Phase 2.8 Remaining Items (Windows workspace)

**Date:** 2026-03-10
**Commit:** `0e3bbd8`

```
Phase: 2.8 remaining items
Commit: <see commit hash after push>
Files:
  - src/Modules/Chat/DotNetCloud.Modules.Chat/Services/SignalRChatService.cs
  - src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChatNotificationBadge.razor.cs
  - tests/DotNetCloud.Modules.Chat.Tests/ChatNotificationBadgeTests.cs
  - tests/DotNetCloud.Modules.Chat.Tests/AnnouncementEditorTests.cs (new)
  - docs/IMPLEMENTATION_CHECKLIST.md
  - docs/MASTER_PROJECT_PLAN.md
  - docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md
Tests added:
  - ChatNotificationBadgeTests.WhenMentionCountIsNonZeroThenHasMentionsIsTrue
  - ChatNotificationBadgeTests.WhenMentionCountUpdatedThenHasMentionsIsTrue
  - ChatNotificationBadgeTests.WhenMentionCountResetToZeroThenHasMentionsIsFalse
  - ChatNotificationBadgeTests.WhenOnlyUnreadWithNoMentionsThenHasMentionsIsFalse
  - ChatNotificationBadgeTests.WhenMultipleChannelMentionCountsThenTotalMentionsSumsAll
  - AnnouncementEditorTests.WhenInitializedThenIsPreviewModeIsFalse
  - AnnouncementEditorTests.WhenTogglePreviewCalledThenIsPreviewModeIsTrue
  - AnnouncementEditorTests.WhenTogglePreviewCalledTwiceThenIsPreviewModeIsFalse
  - AnnouncementEditorTests.WhenTitleAndContentEmptyThenIsSaveDisabledIsTrue
  - AnnouncementEditorTests.WhenTitleAndContentSetThenIsSaveDisabledIsFalse
  - AnnouncementEditorTests.WhenEditingAnnouncementSetThenFieldsArePopulated
  - AnnouncementEditorTests.WhenNotEditingThenFieldsAreReset
Verification:
  dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj -> total 263, succeeded 263, failed 0
  dotnet build -> succeeded (0 errors)
Blockers (if any): none
```

### Request Back
- commit hash
- step/phase resumed from `docs/MASTER_PROJECT_PLAN.md`
- tests executed (commands + pass/fail counts)
- any API/contract mismatch discovered against updated client UI flows

### Client Handoff — Phase 2.9 Remaining Work (Windows workspace)

**Date:** 2026-03-10
**Owner:** Client (`Windows workspace`)
**Status:** ready for handoff ✅

**Baseline commit:** `67d4559`

**Planning decision:**
- Do not treat Windows notification grouping as a balloon-tip approximation task.
- The user wants the desktop chat path to support **quick reply**, so the remaining Phase 2.9 work should use a real Windows toast-notification foundation rather than extending `Shell_NotifyIcon` balloon tips further.

**Current Windows limitation to replace:**
- `src/Clients/DotNetCloud.Client.SyncTray/Notifications/WindowsNotificationService.cs` currently uses `Shell_NotifyIcon` balloon notifications via a hidden message-only window.
- That path is sufficient for basic popup + click-to-open, but it is the wrong primitive for real grouping and future quick reply.

---

#### Required implementation order

1. **Windows toast-notification migration**
     - Replace or supersede `WindowsNotificationService` with a toast-based implementation.
     - Add the Windows registration/bootstrap pieces required for desktop toast delivery and activation.
     - Preserve current click-to-open chat activation behavior.

2. **Notification grouping semantics**
     - Extend `INotificationService` to carry grouping metadata (channel/conversation key, replacement/group identifier as needed).
     - Implement per-channel grouping/replacement on Windows using toast metadata.
     - Keep Linux grouping/replacement aligned where possible using `notify-send` replacement/group behavior.

3. **Quick reply foundation**
     - Add a desktop chat send path suitable for SyncTray (prefer a reusable client-core abstraction, not Android-only reuse from `src/UI/DotNetCloud.UI.Android/Services/ChatApiClient.cs`).
     - Wire notification activation or action flow to open a quick-reply experience for a specific channel/conversation.
     - Support send-message execution through REST/API client plumbing.

4. **Typing indicator while composing**
     - While the quick-reply UI is open, emit typing updates if practical through the available chat transport.
     - If typing cannot be emitted from the notification surface directly, document the precise blocker and fallback design.

5. **Tray mention-vs-message visual badge state**
     - `TrayViewModel` already computes `ChatUnreadCount` and `ChatHasMentions`.
     - Update `TrayIconManager` icon rendering so mentions are visually distinct from generic unread messages.

---

#### Expected file targets

- `src/Clients/DotNetCloud.Client.SyncTray/Notifications/INotificationService.cs`
- `src/Clients/DotNetCloud.Client.SyncTray/Notifications/WindowsNotificationService.cs`
- `src/Clients/DotNetCloud.Client.SyncTray/Notifications/LinuxNotificationService.cs`
- `src/Clients/DotNetCloud.Client.SyncTray/Notifications/NotificationServiceFactory.cs`
- `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/TrayViewModel.cs`
- `src/Clients/DotNetCloud.Client.SyncTray/TrayIconManager.cs`
- `src/Clients/DotNetCloud.Client.SyncTray/App.axaml.cs`
- `src/Clients/DotNetCloud.Client.Core/` (new shared desktop chat send abstraction if needed)
- `tests/DotNetCloud.Client.SyncTray.Tests/`

#### Constraints

- Do not modify server projects unless a concrete client blocker is proven.
- If a new Windows package or activation model is needed for toasts, keep the change scoped to SyncTray and document packaging/runtime prerequisites.
- Prefer a reusable client-core send abstraction for quick reply rather than adding a one-off SyncTray-only HTTP path unless that is clearly lower risk.

#### Technical Design Plan (finish this design before broad implementation)

**Recommended architecture decision:**
- Keep Linux notification delivery on the current `notify-send` path and extend it only for grouping/replacement metadata.
- Replace the Windows-only `Shell_NotifyIcon` balloon path with a **toast-backed Windows notification service**.
- Keep all toast-specific code isolated behind `INotificationService` so SyncTray view-models remain platform-agnostic.
- Do **not** build quick reply on top of the Android-specific chat client; promote a reusable chat send abstraction into `DotNetCloud.Client.Core`.

**Recommended Windows toast approach:**
- Use a toolkit-backed desktop toast implementation for unpackaged Win32/.NET desktop apps, wrapped entirely inside `WindowsNotificationService`.
- The package choice should support:
    - building toast content with text + actions + optional text input,
    - toast activation callback handling for unpackaged desktop apps,
    - tag/group metadata for per-channel grouping/replacement,
    - Action Center persistence semantics.
- Keep the exact package/version decision in the client return under `Design decisions:` because package naming may vary by current NuGet availability; do not spread toast library types across the broader app.

**Notification abstraction redesign:**
- Replace the current `ShowNotification(title, body, type, actionUrl)`-only shape with a request object, for example:
    - `Title`
    - `Body`
    - `NotificationType`
    - `ActionUrl`
    - `ConversationKey` / `ChannelId`
    - `GroupKey`
    - `ReplaceKey` / `Tag`
    - `SupportsQuickReply`
    - `QuickReplyPlaceholder`
- Add a notification activation callback object/event rather than only `Action<string>` so activation can distinguish:
    - open-chat
    - quick-reply-submit
    - dismiss / default activation
- Preserve compatibility for existing call sites by updating `TrayViewModel` only once the new request model exists.

**Client-core chat send foundation:**
- Preferred path: add a reusable `IChatApiClient` (or similarly named interface) under `src/Clients/DotNetCloud.Client.Core/`.
- Minimum methods needed for quick reply:
    - `SendMessageAsync(Guid channelId, SendMessageDto dto, ...)`
    - `MarkAsReadAsync(Guid channelId, Guid messageId, ...)`
    - `NotifyTypingAsync(Guid channelId, ...)` if typing will be emitted from quick reply
- Reuse existing client-core primitives instead of inventing a second auth stack:
    - `IDotNetCloudApiClient`
    - `ITokenStore`
    - existing OAuth/token storage from `ClientCoreServiceExtensions`
- If extending `IDotNetCloudApiClient` for chat would make that interface too broad, create a dedicated chat API client in Client.Core that reuses the same `HttpClient` + token-loading pattern.

**Quick reply UX plan:**
- Target Windows first because that is where toast reply is the priority.
- Preferred UX order:
    1. Toast notification contains a reply affordance tied to the channel/conversation.
    2. If inline text input submission is reliable in the chosen toast activation model, use it.
    3. If inline input is not reliable in the current unpackaged Avalonia deployment, fallback within Phase 2.9 to opening a minimal `QuickReplyWindow` pre-addressed to the channel from the toast action.
- The fallback is acceptable only if the blocker is clearly documented with package/runtime evidence; do not silently downgrade to a plain open-chat action.

**Typing-indicator design:**
- Emit typing only when the reply UI remains open long enough to justify it.
- If inline toast reply does not provide a practical typing cadence, emit typing only from the fallback `QuickReplyWindow`.
- Keep typing best-effort; send-message reliability is the higher priority acceptance criterion.

**Tray icon badge design:**
- Do not entangle tray icon rendering with notification service implementation details.
- Add a pure mapping layer or helper in SyncTray that derives visual badge state from:
    - `OverallState`
    - `ChatUnreadCount`
    - `ChatHasMentions`
- Suggested visual policy:
    - base icon color continues to reflect sync state,
    - unread adds a subtle chat indicator,
    - mentions add a stronger/high-priority indicator distinct from normal unread.
- Unit-test the mapping logic without requiring Avalonia tray integration.

**Implementation order (recommended sequence):**
1. Redesign `INotificationService` to use a notification request/activation model.
2. Implement Windows toast service behind that abstraction, preserving click-to-open behavior first.
3. Add grouping/tag semantics and verify same-channel replacement/grouping behavior.
4. Add Client.Core chat send abstraction + token wiring for SyncTray.
5. Implement quick reply end-to-end (toast action -> send path -> success/failure handling).
6. Add typing support if the chosen quick-reply UX supports it cleanly.
7. Finish tray mention-vs-message visual badge state.
8. Update tests and docs/checklists/plan items together.

**Minimum acceptance criteria for phase closeout:**
- Windows notifications are toast-based, not balloon-tip based.
- Repeated notifications from the same channel are grouped or replaced deterministically.
- Quick reply can send a message to the targeted channel without opening the full browser chat UI.
- Quick reply failure surfaces a user-visible error and does not silently drop the message.
- Tray icon visually distinguishes mentions from generic unread messages.
- `dotnet test tests/DotNetCloud.Client.SyncTray.Tests/DotNetCloud.Client.SyncTray.Tests.csproj` passes.
- `dotnet build` passes.

**Risk notes to document during implementation:**
- Exact Windows toast activation/package prerequisites for unpackaged Avalonia desktop deployment.
- Whether inline reply input is reliable enough to keep, or whether the fallback `QuickReplyWindow` is required.
- Any Linux grouping limitations versus the Windows implementation.

#### Request back (strict format)

```
Phase: 2.9
Commit: <hash>
Files:
    - <list of files changed>
Design decisions:
    - <toast library / activation / grouping approach>
Implemented items:
    - <completed 2.9 tasks>
Verification:
    dotnet test tests/DotNetCloud.Client.SyncTray.Tests/DotNetCloud.Client.SyncTray.Tests.csproj -> total N, succeeded N, failed 0
    dotnet build -> succeeded
Blockers (if any):
    - <exact blocker, package/runtime issue, or API gap>
```

### Sprint Track (Phase 2.3 Closeout)

Reference tracker: Phase 2.3 accepted and closed out; continue from `docs/MASTER_PROJECT_PLAN.md` (`phase-2.4`).

- ✓ Sprint A kickoff sent
- ✓ Sprint A complete (`phase-1.19.2`)
- ✓ Sprint B kickoff sent (`phase-1.15` deferred hardening)
- ✓ Sprint B complete (`phase-1.15` deferred hardening)
- ✓ Sprint C complete (`phase-1.12` deferred UX/media)

### Phase 2.3 Update #1 - Service Hardening + Verification (Server, Linux workspace)

**Date:** 2026-03-10  
**Owner:** Server (`Linux workspace`)  
**Status:** completed ✅

**Commit hash:** `260199c`

**Files added/updated:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/ChannelMemberService.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/ReactionService.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/PinService.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/TypingIndicatorService.cs`
- `tests/DotNetCloud.Modules.Chat.Tests/ChannelMemberServiceTests.cs` (new)
- `tests/DotNetCloud.Modules.Chat.Tests/ReactionServiceTests.cs`
- `tests/DotNetCloud.Modules.Chat.Tests/PinServiceTests.cs`
- `tests/DotNetCloud.Modules.Chat.Tests/TypingIndicatorServiceTests.cs`
- `docs/development/PHASE_2_3_EXECUTION_PLAN.md` (temporary tracker, removed after acceptance)
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`

**Tests added/updated:**
- `tests/DotNetCloud.Modules.Chat.Tests/ChannelMemberServiceTests.cs`
    - `WhenOwnerAddsMemberThenMembershipIsCreated`
    - `WhenNonAdminAddsMemberThenUnauthorizedAccessExceptionIsThrown`
    - `WhenOutsiderListsMembersThenUnauthorizedAccessExceptionIsThrown`
    - `WhenOwnerDemotesLastOwnerThenInvalidOperationExceptionIsThrown`
    - `WhenCallerMarksReadWithInvalidMessageThenInvalidOperationExceptionIsThrown`
    - `WhenGetUnreadCountsThenMentionsIncludeAllAndChannelTypes`
    - `WhenRemovingLastOwnerThenInvalidOperationExceptionIsThrown`
- `tests/DotNetCloud.Modules.Chat.Tests/ReactionServiceTests.cs`
    - `WhenAddReactionWithWhitespaceEmojiThenEmojiIsTrimmed`
    - `WhenAddReactionAsNonMemberThenThrowsUnauthorizedAccessException`
    - `WhenRemoveReactionAsNonMemberThenThrowsUnauthorizedAccessException`
    - `WhenAddReactionThenReactionAddedEventContainsExpectedPayload`
    - `WhenRemoveReactionThenReactionRemovedEventContainsExpectedPayload`
- `tests/DotNetCloud.Modules.Chat.Tests/PinServiceTests.cs`
    - `WhenPinMessageAsNonMemberThenThrowsUnauthorizedAccessException`
    - `WhenPinMessageFromDifferentChannelThenThrowsInvalidOperationException`
    - `WhenGetPinnedMessagesThenLatestPinIsReturnedFirst`
- `tests/DotNetCloud.Modules.Chat.Tests/TypingIndicatorServiceTests.cs`
    - `WhenNotifyTypingWithEmptyChannelThenThrowsArgumentException`
    - `WhenTypingEntryExpiresThenUserIsRemoved`

**Verification commands and results:**
- `dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj`
    - Final result: total 197, succeeded 197, failed 0, skipped 0
- `dotnet build`
    - Final result: succeeded (full solution)

**Raw failing assertion/error text seen during iteration (fixed):**
- `WhenMultipleUsersReactThenCountIsCorrect`: `System.UnauthorizedAccessException: User <guid> is not a member of channel <guid>.`
    - Fix applied: test now adds the second caller as a channel member before reacting.

**Raw log snippets around authorization/event issues:**
- No runtime log-line capture in test harness (services are constructed with `NullLogger<T>` in unit tests).
- Added server-side warning/info logging statements in services for denied reaction/pin/member-management actions and reaction add/remove events.

**Intentionally deferred items:**
- Client-side compatibility validation pass (DTO/view-model/API-consumer assumptions) is deferred to the client workspace handoff.
- No Phase 2.4/2.5 work started in this update.

### Phase 2.3 Update #2 - Client Validation Block (Windows workspace)

**Date:** 2026-03-10  
**Owner:** Client (`Windows workspace`)  
**Status:** completed ✅

**Commit hash:** `9bcbcbf`

**Client paths reviewed:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/DTOs/ChatDtos.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/ChatApiClient.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ViewModels.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChannelList.razor.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/MessageList.razor.cs`
- `src/UI/DotNetCloud.UI.Android/Services/ChatApiClient.cs`
- `src/UI/DotNetCloud.UI.Android/Services/SignalRChatService.cs`

**Server paths validated against client assumptions:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/ChannelMemberService.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/ReactionService.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/PinService.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/TypingIndicatorService.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Controllers/ChatController.cs`

**Payload shape examples checked:**
- `GET api/v1/chat/unread?userId=<guid>` -> `success: true`, `data: UnreadCountDto[]` with `channelId`, `unreadCount`, `mentionCount`
- `GET api/v1/chat/channels/<channelId>/pins?userId=<guid>` -> `success: true`, `data: MessageDto[]` (ordered by latest pin first)
- `GET api/v1/chat/channels/<channelId>/typing` -> `success: true`, `data: TypingIndicatorDto[]` (5-second in-memory expiry)
- `POST api/v1/chat/messages/<messageId>/reactions?userId=<guid>` -> `success: true`, `data.added: true` on success

**Validation result (client contract):**
- DTO shape/nullability for `UnreadCountDto`, `MessageDto`, `MessageReactionDto`, and `TypingIndicatorDto` remains compatible with current client/UI consumers.
- Behavior assumptions validated:
    - Unread and mention counts include `@all` and `@channel` mentions after last-read boundary.
    - Pinned message retrieval preserves latest-pin-first ordering.
    - Typing indicators expire after 5 seconds and are channel-isolated.
- No mandatory client code changes required for Phase 2.3 acceptance.

**Mismatches found / follow-up actions:**
- Follow-up (server): align Chat REST controller exception mapping for hardened authorization paths (`reactions`, `pins`, `typing`) to deterministic API responses instead of unhandled 500s when service-level `UnauthorizedAccessException` / `InvalidOperationException` bubbles.
- Follow-up (client, non-blocking): once Phase 2.4 endpoints are finalized, add client integration tests for unread/pin/typing endpoint envelopes and denial-path handling.

**Intentionally deferred items:**
- No client runtime implementation changes in this update (validation-only pass).
- No Phase 2.4/2.5 implementation work started.

### Phase 2.4 Update #1 - REST Exception Mapping Hardening (Server, Linux workspace)

**Date:** 2026-03-10  
**Owner:** Server (`Linux workspace`)  
**Status:** completed ✅ (incremental phase-2.4 scope)

**Commit hash:** `7ccc3d1`

**Files added/updated:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Controllers/ChatController.cs`
- `tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj`
- `tests/DotNetCloud.Modules.Chat.Tests/ChatControllerTests.cs` (new)
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`
- `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md`

**Implemented in this update:**
1. Added deterministic REST exception mapping for member endpoints (`AddMember`, `RemoveMember`, `GetMembers`, `UpdateMemberRole`, `UpdateNotificationPreference`, `MarkAsRead`) to return expected 403/404 instead of unhandled 500 paths.
2. Added deterministic mapping for reaction endpoints (`AddReaction`, `RemoveReaction`) including 400 for validation (`ArgumentException`) and 403/404 for auth/not-found conditions.
3. Added deterministic mapping for pin endpoints (`PinMessage`, `UnpinMessage`, `GetPinnedMessages`) to return 403/404 on service denials/not-found.
4. Added deterministic mapping for typing endpoints (`NotifyTyping`, `GetTypingUsers`) to return 400 on validation failures.
5. Added controller-level unit tests to validate status-code mapping behavior with mocked services.

**Tests added/updated:**
- `tests/DotNetCloud.Modules.Chat.Tests/ChatControllerTests.cs`
    - `AddReactionAsync_WhenUnauthorized_ThenReturnsForbidResult`
    - `PinMessageAsync_WhenUnauthorized_ThenReturnsForbidResult`
    - `RemoveMemberAsync_WhenUnauthorized_ThenReturnsForbidResult`
    - `NotifyTypingAsync_WhenInvalidArgument_ThenReturnsBadRequest`
    - `GetPinnedMessagesAsync_WhenInvalidOperation_ThenReturnsNotFound`

**Verification commands and results:**
- `dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj`
    - Result: total 202, succeeded 202, failed 0, skipped 0
- `dotnet build`
    - Result: succeeded (full solution)

**Raw failing assertion/error text:**
- None in this update.

**Raw log snippets around authorization/event issues:**
- No runtime log-line capture in controller unit tests (mocked services + status code assertions).

**Intentionally deferred items:**
- Full phase-2.4 completion criteria (controller decomposition decision and endpoint-level integration/API verification) remain open.
- No phase-2.5 implementation started in this update.

### Phase 2.4 Update #2 - Endpoint Completion + API Verification (Server, Linux workspace)

**Date:** 2026-03-10  
**Owner:** Server (`Linux workspace`)  
**Status:** completed ✅

**Commit hash:** `5a6563c`

**Files added/updated:**
- `tests/DotNetCloud.Modules.Chat.Tests/ChatControllerTests.cs`
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`
- `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md`

**Implemented in this update:**
1. Added API verification coverage for success-envelope and denial-path status behavior in `ChatControllerTests`.
2. Verified endpoint completion criteria for phase-2.4 using consolidated `ChatController` scope (functional equivalent to split-controller task list).
3. Updated phase tracking artifacts to mark phase-2.4 completed and set next target to phase-2.5.

**Tests added/updated:**
- `tests/DotNetCloud.Modules.Chat.Tests/ChatControllerTests.cs`
    - `AddReactionAsync_WhenSuccessful_ThenReturnsEnvelopeWithAddedFlag`
    - `RemoveReactionAsync_WhenMessageMissing_ThenReturnsNotFound`
    - `MarkAsReadAsync_WhenUnauthorized_ThenReturnsForbidResult`
    - `GetUnreadCountsAsync_WhenSuccessful_ThenReturnsEnvelope`

**Verification commands and results:**
- `dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj`
    - Result: total 206, succeeded 206, failed 0, skipped 0
- `dotnet build`
    - Result: succeeded (full solution)

**Raw failing assertion/error text:**
- None in this update.

**Raw log snippets around authorization/event issues:**
- No runtime log-line capture in controller unit tests (mocked services + status code and envelope assertions).

**Intentionally deferred items:**
- No phase-2.5 implementation started in this update.

### Phase 2.5 Update #1 - SignalR Group Lifecycle + Reconnect Hardening (Server, mint22)

**Date:** 2026-03-10  
**Owner:** Server (`mint22`)  
**Status:** completed ✅ (incremental phase-2.5 scope)

**Commit hash:** `f9e5453`

**Files added/updated:**
- `src/Core/DotNetCloud.Core.Server/RealTime/UserConnectionTracker.cs`
- `src/Core/DotNetCloud.Core.Server/RealTime/RealtimeBroadcasterService.cs`
- `src/Core/DotNetCloud.Core.Server/RealTime/CoreHub.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/ChannelService.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/ChannelMemberService.cs`
- `tests/DotNetCloud.Core.Server.Tests/RealTime/CoreHubTests.cs` (new)
- `tests/DotNetCloud.Core.Server.Tests/RealTime/UserConnectionTrackerTests.cs`
- `tests/DotNetCloud.Core.Server.Tests/RealTime/RealtimeBroadcasterServiceTests.cs`
- `tests/DotNetCloud.Modules.Chat.Tests/ChannelServiceTests.cs`
- `tests/DotNetCloud.Modules.Chat.Tests/ChannelMemberServiceTests.cs`
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`
- `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md`

**Implemented in this update:**
1. Added persistent per-user SignalR group membership tracking to `UserConnectionTracker` (`AddGroupMembership`, `RemoveGroupMembership`, `GetGroups`) so channel group intent survives disconnects.
2. Updated `RealtimeBroadcasterService` to persist/clear tracked memberships on `AddToGroupAsync` and `RemoveFromGroupAsync`.
3. Updated `CoreHub.OnConnectedAsync` to re-join all tracked groups for the connecting user/connection.
4. Wired chat data-layer lifecycle to realtime groups:
     - `ChannelService`: add all initial members to channel group on create/DM create; remove all members from group on delete.
     - `ChannelMemberService`: add/remove member group membership on join/leave.
5. Added focused coverage across core realtime + chat service tests.

**Tests added/updated:**
- `tests/DotNetCloud.Core.Server.Tests/RealTime/CoreHubTests.cs`
    - `WhenUserHasTrackedGroupsThenOnConnectedAddsConnectionToEachGroup`
- `tests/DotNetCloud.Core.Server.Tests/RealTime/UserConnectionTrackerTests.cs`
    - `WhenGroupMembershipAddedThenGetGroupsReturnsGroup`
    - `WhenGroupMembershipRemovedThenGetGroupsReturnsEmpty`
    - `WhenUserGoesOfflineThenGroupMembershipIsRetained`
    - `WhenGroupNameIsNullThenAddGroupMembershipThrows`
    - `WhenGroupNameIsNullThenRemoveGroupMembershipThrows`
- `tests/DotNetCloud.Core.Server.Tests/RealTime/RealtimeBroadcasterServiceTests.cs`
    - `WhenAddToGroupWithNoConnectionsThenDoesNothing` (extended to assert membership tracking)
    - `WhenRemoveFromGroupThenTrackedMembershipIsRemoved`
- `tests/DotNetCloud.Modules.Chat.Tests/ChannelServiceTests.cs`
    - `WhenDeleteChannelThenRealtimeGroupMembershipIsRemovedForAllMembers`
    - `WhenCreateChannelWithMembersThenMembersAreAdded` (extended to assert realtime group add calls)
- `tests/DotNetCloud.Modules.Chat.Tests/ChannelMemberServiceTests.cs`
    - `WhenAdminRemovesMemberThenRealtimeGroupMembershipIsRemoved`
    - `WhenOwnerAddsMemberThenMembershipIsCreated` (extended to assert realtime group add call)

**Verification commands and results:**
- `dotnet test tests/DotNetCloud.Core.Server.Tests/DotNetCloud.Core.Server.Tests.csproj`
    - Result: total 322, succeeded 320, failed 0, skipped 2
- `dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj`
    - Result: total 208, succeeded 208, failed 0, skipped 0
- `dotnet build`
    - Result: succeeded (full solution)

**Raw failing assertion/error text seen during iteration (fixed):**
- `CoreHubTests.cs`: `error CS0546: 'TestHubCallerContext.Items.set': cannot override because 'HubCallerContext.Items' does not have an overridable set accessor`
- `CoreHubTests.cs`: `error CS0534: 'TestHubCallerContext' does not implement inherited abstract member 'HubCallerContext.Features.get'`
    - Fix applied: test stub now exposes read-only `Items` backing store and implements `Features` with `FeatureCollection`.

**Raw log snippets around authorization/event issues:**
- No runtime service logs captured in unit tests (tests use `NullLogger<T>` or mocks); verification performed via behavior assertions.

**Intentionally deferred items:**
- Chat-specific client-to-server hub methods (`SendMessage`, `EditMessage`, `DeleteMessage`, `StartTyping`, `StopTyping`, `MarkRead`, `AddReaction`, `RemoveReaction`) remain pending.
- Presence custom status message and `PresenceChangedEvent` cross-module event integration remain pending.

### Phase 2.5 Update #2 - CoreHub Chat Method Registration (Server, mint22)

**Date:** 2026-03-10  
**Owner:** Server (`mint22`)  
**Status:** completed ✅ (incremental phase-2.5 scope)

**Commit hash:** `a10f382`

**Files added/updated:**
- `src/Core/DotNetCloud.Core.Server/RealTime/CoreHub.cs`
- `tests/DotNetCloud.Core.Server.Tests/RealTime/CoreHubTests.cs`
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`
- `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md`

**Implemented in this update:**
1. Registered chat hub methods in `CoreHub`: `SendMessageAsync`, `EditMessageAsync`, `DeleteMessageAsync`, `StartTypingAsync`, `StopTypingAsync`, `MarkReadAsync`, `AddReactionAsync`, and `RemoveReactionAsync`.
2. Wired hub methods to existing chat services (`IMessageService`, `IChannelMemberService`, `IReactionService`, `ITypingIndicatorService`) using authenticated `CallerContext` from SignalR connection identity.
3. Wired server-to-client realtime broadcasts through `IChatRealtimeService` for new/edited/deleted messages, typing indicators, unread count updates, and reaction updates.
4. Added deterministic SignalR error translation for expected service exceptions (`ArgumentException`, `InvalidOperationException`, `UnauthorizedAccessException`) to `HubException` messages.

**Tests added/updated:**
- `tests/DotNetCloud.Core.Server.Tests/RealTime/CoreHubTests.cs`
    - `WhenSendMessageCalledThenBroadcastsNewMessage`
    - `WhenMarkReadCalledThenBroadcastsUnreadCountForCaller`
    - `WhenAddReactionCalledThenBroadcastsUpdatedReactions`
    - `WhenStartTypingCalledThenPublishesTypingAndBroadcasts`
    - `WhenUserHasTrackedGroupsThenOnConnectedAddsConnectionToEachGroup`

**Verification commands and results:**
- `dotnet test tests/DotNetCloud.Core.Server.Tests/DotNetCloud.Core.Server.Tests.csproj`
    - Result: total 326, succeeded 324, failed 0, skipped 2
- `dotnet build`
    - Result: succeeded (full solution)

**Raw failing assertion/error text seen during iteration (fixed):**
- None in this update.

**Raw log snippets around authorization/event issues:**
- No runtime service logs captured in hub unit tests (mocked collaborators + behavior assertions).

**Intentionally deferred items:**
- Presence custom status message and `PresenceChangedEvent` cross-module event integration remain pending.

### Phase 2.5 Update #3 - Presence Extension + PresenceChangedEvent (Server, mint22)

**Date:** 2026-03-10  
**Owner:** Server (`mint22`)  
**Status:** completed ✅ (finishes remaining phase-2.5 scope)

**Commit hash:** `56fbe8c`

**Files added/updated:**
- `src/Core/DotNetCloud.Core.Server/RealTime/PresenceService.cs`
- `src/Core/DotNetCloud.Core.Server/RealTime/CoreHub.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Events/PresenceChangedEvent.cs` (new)
- `src/Modules/Chat/DotNetCloud.Modules.Chat/ChatModuleManifest.cs`
- `tests/DotNetCloud.Core.Server.Tests/RealTime/CoreHubTests.cs`
- `tests/DotNetCloud.Core.Server.Tests/RealTime/PresenceServiceTests.cs`
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`
- `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md`

**Implemented in this update:**
1. Added custom presence status-message support in `PresenceService` with tracked presence state and validated status transitions (`Online`, `Away`, `DoNotDisturb`, `Offline`).
2. Added `CoreHub.SetPresenceAsync(status, statusMessage)` to update caller presence, broadcast `PresenceChanged` via `IChatRealtimeService`, and publish `PresenceChangedEvent` through `IEventBus` for cross-module awareness.
3. Added new chat-domain event `PresenceChangedEvent` and declared it in `ChatModuleManifest.PublishedEvents`.
4. Completed remaining Phase 2.5 checklist/plan presence deliverables and advanced phase status to completed.

**Tests added/updated:**
- `tests/DotNetCloud.Core.Server.Tests/RealTime/CoreHubTests.cs`
    - `WhenSetPresenceCalledThenBroadcastsPresenceAndPublishesEvent`
- `tests/DotNetCloud.Core.Server.Tests/RealTime/PresenceServiceTests.cs`
    - `WhenSetPresenceThenCustomStatusMessageIsPersisted`
    - `WhenSetPresenceWithInvalidStatusThenThrows`

**Verification commands and results:**
- `dotnet test tests/DotNetCloud.Core.Server.Tests/DotNetCloud.Core.Server.Tests.csproj`
    - Result: total 329, succeeded 327, failed 0, skipped 2
- `dotnet build`
    - Result: succeeded (full solution)

**Raw failing assertion/error text seen during iteration (fixed):**
- None in this update.

**Raw log snippets around authorization/event issues:**
- No runtime service logs captured in unit tests (mocked collaborators + behavior assertions).

**Intentionally deferred items:**
- None for phase-2.5. Next target is phase-2.6.

### Phase 2.6 Update #1 - Announcements Endpoints + Realtime Broadcasts (Server, mint22)

**Date:** 2026-03-10  
**Owner:** Server (`mint22`)  
**Status:** completed ✅

**Commit hash:** `b987643`

**Files added/updated:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Controllers/ChatController.cs`
- `tests/DotNetCloud.Modules.Chat.Tests/ChatControllerTests.cs`
- `tests/DotNetCloud.Modules.Chat.Tests/ChatModuleManifestTests.cs`
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`
- `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md`

**Implemented in this update:**
1. Added missing announcement REST API surface in `ChatController` under `/api/v1/announcements`:
     - `POST /api/v1/announcements`
     - `GET /api/v1/announcements`
     - `GET /api/v1/announcements/{id}`
     - `PUT /api/v1/announcements/{id}`
     - `DELETE /api/v1/announcements/{id}`
     - `POST /api/v1/announcements/{id}/acknowledge`
     - `GET /api/v1/announcements/{id}/acknowledgements`
2. Added realtime broadcast behavior for announcements using `IRealtimeBroadcaster`:
     - `AnnouncementCreated` for all new announcements
     - `UrgentAnnouncement` for urgent-priority announcements
     - `AnnouncementBadgeUpdated` for live announcement-count badge updates
3. Updated tests for announcement controller behavior and broadcast assertions; updated manifest test expectations after added published events.
4. Updated phase tracking to mark phase-2.6 completed and set next target to phase-2.7.

**Tests added/updated:**
- `tests/DotNetCloud.Modules.Chat.Tests/ChatControllerTests.cs`
    - `CreateAnnouncementAsync_WhenSuccessful_ThenBroadcastsAndReturnsCreated`
    - `CreateAnnouncementAsync_WhenUrgent_ThenBroadcastsUrgentAnnouncement`
    - `GetAnnouncementAsync_WhenMissing_ThenReturnsNotFound`
    - `AcknowledgeAnnouncementAsync_WhenCalled_ThenReturnsSuccessEnvelope`
- `tests/DotNetCloud.Modules.Chat.Tests/ChatModuleManifestTests.cs`
    - `WhenCreatedThenPublishedEventsContainsExpectedEvents` (updated expected count and list)

**Verification commands and results:**
- `dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj`
    - Result: total 212, succeeded 212, failed 0, skipped 0
- `dotnet build`
    - Result: succeeded (full solution)

**Raw failing assertion/error text seen during iteration (fixed):**
- `ChatModuleManifestTests.WhenCreatedThenPublishedEventsContainsExpectedEvents`: `Assert.AreEqual failed. Expected:<5>. Actual:<6>.`
    - Fix applied: include `PresenceChangedEvent` and update expected count.

**Raw log snippets around authorization/event issues:**
- No runtime log-line capture in controller unit tests (mocked collaborators + result/broadcast assertions).

**Intentionally deferred items:**
- None for phase-2.6. Next target is phase-2.7.

### Phase 2.7 Update #1 - Push Endpoint Wiring (Server, mint22)

**Date:** 2026-03-10  
**Owner:** Server (`mint22`)  
**Status:** completed ✅ (incremental phase-2.7 scope)

**Commit hash:** `092dfd9`

**Files added/updated:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Controllers/ChatController.cs`
- `tests/DotNetCloud.Modules.Chat.Tests/ChatControllerTests.cs`
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`
- `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md`

**Implemented in this update:**
1. Added push registration endpoints in host API:
     - `POST /api/v1/notifications/devices/register`
     - `DELETE /api/v1/notifications/devices/{deviceToken}`
2. Added caller preferences endpoints:
     - `GET /api/v1/notifications/preferences`
     - `PUT /api/v1/notifications/preferences`
3. Wired device registration/unregistration to `IPushNotificationService` with provider validation.
4. Added controller tests for push endpoint behavior and error-path validation.

**Tests added/updated:**
- `tests/DotNetCloud.Modules.Chat.Tests/ChatControllerTests.cs`
    - `RegisterPushDeviceAsync_WhenValid_ThenRegistersDevice`
    - `RegisterPushDeviceAsync_WhenProviderInvalid_ThenReturnsBadRequest`
    - `UnregisterPushDeviceAsync_WhenCalled_ThenUnregistersDevice`
    - `UpdateNotificationPreferencesAsync_WhenCalled_ThenReturnsOk`

**Verification commands and results:**
- `dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj`
    - Result: total 216, succeeded 216, failed 0, skipped 0

**Raw failing assertion/error text seen during iteration (fixed):**
- None in this update.

**Raw log snippets around authorization/event issues:**
- No runtime log capture for controller unit tests (mocked services + response assertions).

**Intentionally deferred items:**
- FCM credential/config hardening and invalid-token cleanup.
- UnifiedPush retry/error handling.
- NotificationRouter dedup/preference enforcement and queueing.

### Phase 2.7 Update #2 - Router Preference + Online Dedup (Server, mint22)

**Date:** 2026-03-10  
**Owner:** Server (`mint22`)  
**Status:** completed ✅ (incremental phase-2.7 scope)

**Commit hash:** `042a12b`

**Files added/updated:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/INotificationPreferenceStore.cs` (new)
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/InMemoryNotificationPreferenceStore.cs` (new)
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/IPushProviderEndpoint.cs` (new)
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/NotificationRouter.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/FcmPushProvider.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/UnifiedPushProvider.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/ChatServiceRegistration.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Controllers/ChatController.cs`
- `tests/DotNetCloud.Modules.Chat.Tests/ChatControllerTests.cs`
- `tests/DotNetCloud.Modules.Chat.Tests/NotificationRouterTests.cs` (new)
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`
- `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md`

**Implemented in this update:**
1. Added shared notification preference storage abstraction (`INotificationPreferenceStore`) with in-memory implementation for server-side routing and API consistency.
2. Wired `ChatController` notification preference GET/PUT endpoints to the shared store (removed per-controller static preference map).
3. Hardened `NotificationRouter` to enforce caller preferences before delivery:
     - suppress when push is globally disabled,
     - suppress when DND is enabled,
     - suppress chat-message/chat-mention notifications for muted channels.
4. Implemented online dedup in `NotificationRouter` using `IPresenceTracker` (skip push when user is currently online).
5. Added provider abstraction (`IPushProviderEndpoint`) for deterministic provider selection and improved router testability.
6. Added focused unit coverage for routing policy behavior and preference endpoint storage flow.

**Tests added/updated:**
- `tests/DotNetCloud.Modules.Chat.Tests/NotificationRouterTests.cs`
    - `SendAsync_WhenPushDisabled_ThenNotificationIsSuppressed`
    - `SendAsync_WhenUserIsOnline_ThenNotificationIsSuppressed`
    - `SendAsync_WhenChannelMuted_ThenNotificationIsSuppressed`
    - `SendAsync_WhenEligible_ThenRoutesToRegisteredProviders`
- `tests/DotNetCloud.Modules.Chat.Tests/ChatControllerTests.cs`
    - `UpdateNotificationPreferencesAsync_WhenCalled_ThenReturnsOkAndStoresPreferences`
    - `GetNotificationPreferencesAsync_WhenCalled_ThenReturnsStoreValues`

**Verification commands and results:**
- `dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj`
    - Result: total 221, succeeded 221, failed 0, skipped 0
- `dotnet build`
    - Result: succeeded (full solution)

**Raw failing assertion/error text seen during iteration (fixed):**
- `GetNotificationPreferencesAsync_WhenCalled_ThenReturnsStoreValues`: `System.Collections.Generic.KeyNotFoundException: The given key was not present in the dictionary.`
    - Fix: aligned assertions with serialized property casing.
- `NotificationRouterTests` (all four tests): `Cannot set up IPushProviderEndpoint.get_Provider because it is not accessible to the proxy generator used by Moq`.
    - Fix: replaced Moq provider proxies with concrete internal test-double provider implementation.

**Raw log snippets around authorization/event issues:**
- No runtime log capture for these unit tests (mocked collaborators + behavior assertions).

**Intentionally deferred items:**
- FCM credentials/config model and invalid-token cleanup.
- UnifiedPush retry/error handling.
- Notification queue/reliability background processing.

### Phase 2.7 Update #3 - Provider Hardening (Server, mint22)

**Date:** 2026-03-10  
**Owner:** Server (`mint22`)  
**Status:** completed ✅ (incremental phase-2.7 scope)

**Commit hash:** `0703bf4`

**Files added/updated:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/IFcmTransport.cs` (new)
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/FcmLoggingTransport.cs` (new)
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/IUnifiedPushTransport.cs` (new)
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/UnifiedPushLoggingTransport.cs` (new)
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/FcmPushProvider.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/UnifiedPushProvider.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/ChatServiceRegistration.cs`
- `tests/DotNetCloud.Modules.Chat.Tests/FcmPushProviderTests.cs` (new)
- `tests/DotNetCloud.Modules.Chat.Tests/UnifiedPushProviderTests.cs` (new)
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`
- `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md`

**Implemented in this update:**
1. Added FCM transport abstraction (`IFcmTransport`) and default logging transport (`FcmLoggingTransport`).
2. Added UnifiedPush transport abstraction (`IUnifiedPushTransport`) and default logging transport (`UnifiedPushLoggingTransport`).
3. Hardened `FcmPushProvider` with invalid-token cleanup: invalid tokens are removed from in-memory registrations after failed send results marked as invalid.
4. Hardened `UnifiedPushProvider` with bounded retry handling for transient failures (max 3 attempts) and immediate stop for non-transient failures.
5. Wired new transports in `ChatServiceRegistration` so providers remain DI-constructed and testable.

**Tests added/updated:**
- `tests/DotNetCloud.Modules.Chat.Tests/FcmPushProviderTests.cs`
    - `SendAsync_WhenTransportMarksInvalidToken_ThenTokenIsCleanedUp`
- `tests/DotNetCloud.Modules.Chat.Tests/UnifiedPushProviderTests.cs`
    - `SendAsync_WhenTransientFailuresThenSuccess_ThenRetriesUntilDelivered`
    - `SendAsync_WhenNonTransientFailure_ThenDoesNotRetry`

**Verification commands and results:**
- `dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj`
    - Result: total 224, succeeded 224, failed 0, skipped 0
- `dotnet build`
    - Result: succeeded (full solution)

**Raw failing assertion/error text seen during iteration (fixed):**
- None in this update.

**Raw log snippets around authorization/event issues:**
- No runtime log capture for these unit tests (test doubles + behavior assertions).

**Intentionally deferred items:**
- FCM configuration model / credential management UI.
- UnifiedPush configuration model.
- Notification queue/reliability background processing.

### Phase 2.7 Update #4 - Queue/Reliability Background Processing (Server, mint22)

**Date:** 2026-03-10  
**Owner:** Server (`mint22`)  
**Status:** completed ✅ (final phase-2.7 scope)

**Commit hash:** `42aa009`

**Files added/updated:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/INotificationDeliveryQueue.cs` (new)
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/IQueuedNotificationDispatcher.cs` (new)
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/NotificationDeliveryBackgroundService.cs` (new)
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/NotificationRouter.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/ChatServiceRegistration.cs`
- `tests/DotNetCloud.Modules.Chat.Tests/NotificationRouterTests.cs`
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`
- `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md`

**Implemented in this update:**
1. Added a queue abstraction for deferred retries (`INotificationDeliveryQueue`) with in-memory channel-backed implementation.
2. Added queued-dispatch abstraction (`IQueuedNotificationDispatcher`) to separate direct sends from background retries.
3. Added `NotificationDeliveryBackgroundService` to process queued notifications with bounded retry/backoff behavior.
4. Updated `NotificationRouter` to enqueue failed delivery attempts and support queue-driven retry dispatch.
5. Wired queue + dispatcher + background worker in DI (`ChatServiceRegistration`).

**Tests added/updated:**
- `tests/DotNetCloud.Modules.Chat.Tests/NotificationRouterTests.cs`
    - `SendAsync_WhenAllProvidersFail_ThenNotificationIsQueued`
    - `DispatchQueuedAsync_WhenProviderRecovers_ThenReturnsTrue`
    - Existing tests updated for queue dependency and suppression expectations.

**Verification commands and results:**
- `dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj`
    - Result: total 226, succeeded 226, failed 0, skipped 0
- `dotnet build`
    - Result: succeeded (full solution)

**Raw failing assertion/error text seen during iteration (fixed):**
- None.

**Raw log snippets around authorization/event issues:**
- No runtime log capture for unit tests (test doubles + behavior assertions).

**Intentionally deferred items:**
- FCM configuration model / credential management UI.
- UnifiedPush configuration model.

### Phase 2.8 Update #1 - Channel List Presence Indicators (Server, mint22)

**Date:** 2026-03-10  
**Owner:** Server (`mint22`)  
**Status:** completed ✅ (incremental phase-2.8 scope)

**Commit hash:** `0f7ca2b`

**Files added/updated:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ViewModels.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChannelList.razor`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChannelList.razor.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChannelList.razor.css` (new)
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`
- `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md`

**Implemented in this update:**
1. Added `PresenceStatus` support to `ChannelViewModel` for DM/group presence rendering.
2. Updated `ChannelList.razor` to render presence dots for direct/group channels.
3. Added presence-class mapping in `ChannelList.razor.cs` with `Online`/`Away`/`Offline` UI states.
4. Added scoped styling in `ChannelList.razor.css` for presence indicator appearance.

**Tests added/updated:**
- No new unit tests (UI-only markup/styling enhancement).

**Verification commands and results:**
- `dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj`
    - Result: total 226, succeeded 226, failed 0, skipped 0
- `dotnet build`
    - Result: succeeded (full solution)

**Raw failing assertion/error text seen during iteration (fixed):**
- None.

**Raw log snippets around authorization/event issues:**
- N/A (UI-only change).

**Intentionally deferred items:**
- Channel drag-to-reorder.
- Additional phase-2.8 chat UI components and behaviors pending in checklist.

### Phase 2.8 Update #2 - Channel Header Action Controls (Server, mint22)

**Date:** 2026-03-10  
**Owner:** Server (`mint22`)  
**Status:** completed ✅ (incremental phase-2.8 scope)

**Commit hash:** `1ce0326`

**Files added/updated:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ViewModels.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChannelHeader.razor`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChannelHeader.razor.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChannelHeader.razor.css` (new)
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`
- `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md`

**Implemented in this update:**
1. Added channel action controls in `ChannelHeader.razor`: edit, archive, leave, and pin/unpin.
2. Added callback surface in `ChannelHeader.razor.cs` for action events (`OnEditChannel`, `OnArchiveChannel`, `OnLeaveChannel`, `OnPinChanged`).
3. Added `IsPinned` state to `ChannelViewModel` for UI-level pin toggling.
4. Added scoped styling in `ChannelHeader.razor.css` for action button layout.

**Tests added/updated:**
- No new unit tests (UI-only action-surface enhancement).

**Verification commands and results:**
- `dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj`
    - Result: total 226, succeeded 226, failed 0, skipped 0
- `dotnet build`
    - Result: succeeded (full solution)

**Raw failing assertion/error text seen during iteration (fixed):**
- None.

**Raw log snippets around authorization/event issues:**
- N/A (UI-only change).

**Intentionally deferred items:**
- Channel drag-to-reorder.
- Remaining phase-2.8 UI behavior and component items in checklist.

### Phase 2.8 Update #3 - MessageList Rendering Enhancements (Server, mint22)

**Date:** 2026-03-10  
**Owner:** Server (`mint22`)  
**Status:** completed ✅ (incremental phase-2.8 scope)

**Commit hash:** `e0dc999`

**Files added/updated:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/MessageList.razor`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/MessageList.razor.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/MessageList.razor.css` (new)
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`
- `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md`

**Implemented in this update:**
1. Added basic markdown rendering support for message content (safe encoding + links/bold/italic/inline code).
2. Added inline preview handling for attachments (image previews + document preview placeholders).
3. Added a "new messages" divider line using `NewMessagesStartMessageId`.
4. Added scoped MessageList styles for divider and inline preview presentation.

**Tests added/updated:**
- No new unit tests (UI-only rendering enhancement).

**Verification commands and results:**
- `dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj`
    - Result: total 226, succeeded 226, failed 0, skipped 0
- `dotnet build`
    - Result: succeeded (full solution)

**Raw failing assertion/error text seen during iteration (fixed):**
- None.

**Raw log snippets around authorization/event issues:**
- N/A (UI-only change).

**Intentionally deferred items:**
- Advanced markdown features (tables/code blocks) and full rich-text composer UX.
- Remaining phase-2.8 UI items from checklist.

### Phase 2.8 Update #4 - Member Actions + Channel Settings Management (Server, mint22)

**Date:** 2026-03-10  
**Owner:** Server (`mint22`)  
**Status:** completed ✅ (incremental phase-2.8 scope)

**Commit hash:** `2c86a4a`

**Files added/updated:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ViewModels.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/MemberListPanel.razor`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/MemberListPanel.razor.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/MemberListPanel.razor.css` (new)
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChannelSettingsDialog.razor`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChannelSettingsDialog.razor.cs`
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`
- `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md`

**Implemented in this update:**
1. Added member action controls in `MemberListPanel` for promote/demote/remove (callback-driven).
2. Added profile popup in `MemberListPanel` for click-to-view member details.
3. Expanded `ChannelSettingsDialog` with member management controls (add/remove/change role callbacks).
4. Added channel metadata section in settings dialog (created date + creator display).
5. Added supporting member metadata fields (`Username`, `Bio`) and scoped styles for member panel UI.

**Tests added/updated:**
- No new unit tests (UI-only enhancement and callback surface expansion).

**Verification commands and results:**
- `dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj`
    - Result: total 226, succeeded 226, failed 0, skipped 0
- `dotnet build`
    - Result: succeeded (full solution)

**Raw failing assertion/error text seen during iteration (fixed):**
- None.

**Raw log snippets around authorization/event issues:**
- N/A (UI-only change).

**Intentionally deferred items:**
- Remaining phase-2.8 items (DM user search/group DM, badge realtime update, announcement filter/preview, drag reorder, rich mention/paste image).

### Phase 2.7 Update #5 - Push Configuration Models (Server, mint22)

**Date:** 2026-03-10  
**Owner:** Server (`mint22`)  
**Status:** completed ✅ (server-only hardening scope)

**Commit hash:** `1d5730b`

**Files added/updated:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/PushProviderOptions.cs` (new)
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/FcmPushProvider.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/UnifiedPushProvider.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/ChatServiceRegistration.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Program.cs`
- `src/Core/DotNetCloud.Core.Server/Program.cs`
- `tests/DotNetCloud.Modules.Chat.Tests/FcmPushProviderTests.cs`
- `tests/DotNetCloud.Modules.Chat.Tests/UnifiedPushProviderTests.cs`
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`
- `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md`

**Implemented in this update:**
1. Added provider configuration models: `FcmPushOptions` and `UnifiedPushOptions`.
2. Bound options from configuration (`Chat:Push:Fcm`, `Chat:Push:UnifiedPush`) in chat service registration.
3. Updated server bootstrap call sites to pass configuration into `AddChatServices(builder.Configuration)`.
4. Updated `FcmPushProvider` to respect provider enable/disable option state.
5. Updated `UnifiedPushProvider` to use configurable enable state, max attempts, and retry delay.

**Tests added/updated:**
- `tests/DotNetCloud.Modules.Chat.Tests/FcmPushProviderTests.cs`
    - `SendAsync_WhenProviderDisabled_ThenTransportIsNotCalled`
- `tests/DotNetCloud.Modules.Chat.Tests/UnifiedPushProviderTests.cs`
    - `SendAsync_WhenMaxAttemptsConfiguredToTwo_ThenOnlyTwoAttemptsAreMade`
    - Existing tests updated for options injection.

**Verification commands and results:**
- `dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj`
    - Result: total 228, succeeded 228, failed 0, skipped 0
- `dotnet build`
    - Result: succeeded (full solution)

**Raw failing assertion/error text seen during iteration (fixed):**
- None.

**Raw log snippets around authorization/event issues:**
- N/A (provider options + behavior update).

**Intentionally deferred items:**
- FCM admin credential management UI.
- UnifiedPush admin configuration UI.

### Client Machine Handoff - Phase 2.8 UI Continuation

**Target machine:** `Windows11-TestDNC` (client workspace)  
**Owner:** Client agent  
**Status:** ready for handoff ✅

**Objective:** bring client/UI implementation to parity with completed server phases without modifying server contracts.

**Server-complete to client-required alignment matrix:**

| Phase | Server Capability Complete | Client/UI Work Required | Acceptance Check | Primary File Targets |
|---|---|---|---|---|
| 2.5 | SignalR realtime hubs, message/read/presence transport paths active | Ensure realtime events are reflected in channel list, unread badges, read-state markers, and presence indicators consistently across navigation and reconnect | Send message/read/presence update and verify visible UI changes in under 1s after reconnect and normal flow | `src/UI/*Chat*`, `src/Clients/*Chat*`, view models/store wiring files |
| 2.6 | Announcement API and server behavior stable | Finalize announcement UX: list filters, preview rendering, create/edit validation messages, and error states | Create/edit/filter/preview flows complete with correct empty/loading/error states | announcement components/pages and related view models |
| 2.7 | Push routing + preference enforcement + online dedup + provider hardening + queue/retry + options models | Implement client push registration/settings UX and align notification preference UI with server-backed behavior; no provider contract edits | Update preference, trigger message while online/offline, verify dedup and expected notification behavior | chat settings/preferences UI, push registration client service, notification state/view models |
| 2.8 | Core server support for chat/presence/actions already in place for current UI scope | Complete remaining UI polish: channel reorder, composer rich input, mention autocomplete, paste-image upload, header action consistency | Execute manual scenario checklist for each feature and verify no regressions in existing chat flows | channel list/header/composer/message list components and tests |
| 2.9 | Server-side prerequisites available to begin end-user validation | Add/finish client test and validation pass for end-user scenarios; capture blockers with reproducible steps | Regression checklist pass recorded with evidence and no unresolved P0/P1 UI blockers | client test projects and UI interaction test files |
| 2.10 | Server baseline stable for release hardening | Final client UX hardening pass, accessibility/empty-state cleanup, release readiness notes | Final parity checklist complete and documented for mediator relay | final UI polish files and release notes docs |

**Execution order on client machine:**
1. Phase 2.5 realtime UX verification and fixes.
2. Phase 2.6 announcement UX refinements.
3. Phase 2.7 push registration/preferences UX alignment.
4. Phase 2.8 remaining chat UI polish.
5. Phase 2.9 regression pass and evidence capture.
6. Phase 2.10 release hardening checklist.

**Server constraints for client implementation:**
- Server push infrastructure phase is complete (Phase 2.7, including queue/retry/options models).
- Do not change server push provider contracts unless a client blocker is confirmed.

---

## Migration Fix + Integration Test Fixes Archive (2026-03-12)

**Server agent (mint22), commit `3a2c0ac`:**
1. Applied `20260309093919_AddSymlinkSupport` migration — added `LinkTarget` column to `FileNodes` on mint22 PostgreSQL.
2. Rebuilt and redeployed server (Release publish → `dotnetcloud.service` restart).
3. Verified Files UI at `https://mint22:15443/apps/files` loads without `42703` errors.
4. Fixed 11 integration test failures from commit `49bdaa6`:
   - SignalR auth: added `TestAuthHandler` with `ForwardDefaultSelector` for scheme-specific `[Authorize]`.
   - SignalR method signatures: corrected arg count and enum casing.
   - File sync routes: fixed paths and removed stale `?userId=` params.
   - EF dual-provider conflict: changed `AddDbContext` to singleton options registration.
5. Test suite: 2,106 passed / 0 failed / 2 skipped (env-gated).

---

## Phase 2.10 Final Items Archive (2026-03-12)

**Client agent (Windows11-TestDNC):**
1. Notification badges on app icon: Created `AppBadgeManager` static utility with `WithBadgeCount()` extension method. Wired into both `FcmMessagingService.ShowChatNotification()` (Google Play) and `UnifiedPushReceiver.ShowNotification()` (F-Droid). Uses `SetNumber()` on `Notification.Builder` for launcher numeric badge support.
2. Direct APK download option: Expanded `docs/clients/android/DISTRIBUTION.md` with GitHub Releases download section, sideloading instructions, checksum verification, and enterprise MDM distribution guidance.
3. App store listing description: Added full Google Play listing (title, short description, full description with feature bullets) and F-Droid metadata reference to DISTRIBUTION.md.
4. All Phase 2.10 items now complete (8/8). Phase 2 fully closed.
5. Test suite: 2,095 passed / 0 failed / 13 skipped (env-gated).
- Treat server APIs as stable for this sprint; any suspected server gap must include endpoint, payload, and raw error evidence before requesting server changes.

**Send to client agent (copy/paste block):**
Continue client-only implementation to align with completed server phases (2.5-2.10) using the alignment matrix above.

Rules:
1. Do not modify server projects on this machine.
2. Complete work in execution order unless blocked.
3. For any blocker, provide exact endpoint, request payload, response/error, and timestamped log lines.

Required evidence in return:
1. Commit hash(es).
2. Exact client/UI file paths changed.
3. Completed checklist items mapped to phase number.
4. Short behavior verification notes per item (or screenshots if available).
5. Any server/API blockers with raw reproduction details.

**Request back from client machine (strict format):**
- `Phase:`
- `Commit:`

---

### PosixMode Migration Blocker FIXED — Server Ready for Client Testing (Archived 2026-03-12)

**Date:** 2026-03-12
**Owner:** Server agent (`mint22`)
**Status:** COMPLETE ✅

**What was completed:**
1. Discovered all 6 Files module migrations were pending against the production `dotnetcloud` database.
2. Recorded `InitialFilesSchema` as applied (tables already existed from prior manual creation).
3. Applied 4 pending migrations using `--connection` override.
4. Rebuilt, republished, and restarted `dotnetcloud.service`.

**Verification:** All 7 migrations recorded, health endpoint 200, Files API returns 401 (auth required, no column errors), test suite 2,106 passed / 0 failed / 2 skipped.
- `Files:`
- `Completed items:`
- `Verification notes:`
- `Blockers (if any):`

### Sprint A Kickoff - Phase 1.19.2 (Files API Integration Depth)

**Sprint goal:** Complete `phase-1.19.2` by expanding Files API integration tests beyond isolation paths.

**Owner split:**
- Server: primary implementation and test expansion in `tests/DotNetCloud.Integration.Tests/`
- Client: contract compatibility validation against response envelope/auth expectations

**Kickoff checklist:**
- ✓ Scope confirmed: CRUD/tree/search/favorites, chunked upload E2E, version/share/trash flows, WOPI+sync smoke
- ✓ Mediator workflow confirmed: relay via this handoff doc
- ✓ Server kickoff message sent
- ✓ Client validation message sent

### Send to Server Agent
Execute Sprint A for `phase-1.19.2` in `tests/DotNetCloud.Integration.Tests/`.

Required coverage:
1. REST CRUD/tree/search/favorites end-to-end tests.
2. Chunked upload E2E tests (initiate, upload, complete, dedup behavior, quota rejection path).
3. Version/share/trash end-to-end tests.

---

## Archived: Chat UI Blazor Binding Fix — COMPLETED (2026-03-12)

**Date:** 2026-03-12
**Owner:** Server agent (`mint22`)

**Actions taken:**
1. `git pull` — pulled commit `6f1cf55` with Blazor `@`-prefix fixes.
2. Published: `dotnet publish src/Core/DotNetCloud.Core.Server -c Release -o artifacts/publish/server-baremetal` — build succeeded (59s).
3. Restarted: `systemctl restart dotnetcloud.service` — active (running), PID 114823.
4. Health: `curl -sk https://mint22:15443/health` → **Healthy**.
5. Chat page: `https://mint22:15443/apps/chat` returns 302 (auth redirect, expected). No raw variable names in response body.

---

## Archived: Chat DbContext Concurrency Bug — FIXED (2026-03-12)

**Date:** 2026-03-12
**Owner:** Server agent (`mint22`)

**Root cause:** Two concurrent `ListChannelsAsync()` calls on the same scoped `ChatDbContext` — `ChatPageLayout.OnInitializedAsync()` and `ChannelList.OnInitializedAsync()` both fired channel loading independently.

**Fix:** Removed duplicate channel loading from `ChannelList` (parent `ChatPageLayout` is sole owner of channel state). Also optimized `ChannelService.ListChannelsAsync()` to use grouped query instead of N+1.

**Verified by client testing:** Channels load without error after service restart (`833f153`).
4. WOPI and sync endpoint smoke tests (auth enforcement + payload shape).
5. Document provider matrix execution: PostgreSQL required; SQL Server if environment is available.

Update this handoff doc with test inventory, remaining gaps, and completion status.

### Request Back
- commit hash
- exact tests added/updated (file paths + test names)
- raw endpoint/URL used for any failing test
- raw error/query params
- raw log lines around failures (timestamped)
- list of any intentionally deferred coverage

### Send to Client Agent
Validate Sprint A output for client compatibility risk.

Checks required:
1. No response-envelope contract regressions for `DotNetCloudApiClient` paths.
2. No auth-flow regressions for Files/sync/WOPI endpoint consumption assumptions.
3. Note any required client-side follow-up tests or fixes.

### Request Back
- commit hash (if any client-side changes)
- affected client paths reviewed
- raw endpoint/URL + payload shape examples checked
- any mismatch found between integration behavior and client assumptions

### Sprint A Historical Updates (Archived)

Completed Sprint A updates `#1` through `#9` are archived in
`docs/development/CLIENT_SERVER_MEDIATION_ARCHIVE.md` under
`Sprint A Archive (Phase 1.19.2)` and
`Sprint A Archive Continuation (Phase 1.19.2 - updates #5-#9)`.

### Sprint B Kickoff - Phase 1.15 Deferred Hardening (SyncService Identity Boundaries)

**Sprint goal:** Close deferred hardening items in `phase-1.15` with priority on IPC caller identity enforcement and per-context privilege boundaries.

**Owner split:**
- Client: primary implementation in `DotNetCloud.Client.SyncService` and platform plumbing.
- Server: identity/contract review and sign-off on failure semantics.

**Kickoff checklist:**
- ✓ Scope confirmed: Linux privilege dropping, Windows impersonation, IPC identity verification, trigger debounce, disk-full surfacing.
- ✓ Expected identity semantics posted in handoff (this update).
- ✓ Expected failure semantics posted in handoff (this update).
- ✓ Client implementation kickoff message sent.

### Sprint B - Expected Caller Identity (IPC/SyncService)

These expectations define the security contract for `IpcServer`/`IpcClientHandler` once Sprint B is implemented:

1. Connection identity must come from transport-level OS credentials, not from JSON payload fields.
2. On Linux/macOS Unix socket connections, caller identity must be resolved from peer credentials (UID/GID) and mapped to a normalized OS user identity.
3. On Windows named-pipe connections, caller identity must be resolved from the authenticated pipe client token and mapped to a normalized SID/account identity.
4. Every `SyncContextRegistration` is owner-scoped by `OsUserName`; context-scoped commands must execute only when caller identity matches the context owner identity.
5. `list-contexts` must be caller-filtered (return only contexts owned by the connected caller).
6. Push events for `subscribe` must be filtered to caller-owned contexts only.
7. If identity cannot be established reliably, no mutating command may execute.

### Sprint B - Expected Failure Semantics (IPC/SyncService)

Use deterministic denial behavior for identity-boundary violations:

1. Identity unavailable/unverifiable: reject command with `success=false` and error text `Caller identity unavailable.`
2. Context ownership mismatch: reject command with `success=false` and error text `Context not found or inaccessible.`
3. Unknown context for caller: same response as mismatch (`Context not found or inaccessible.`) to avoid cross-user context enumeration.
4. Invalid or missing required fields (`contextId`, `data`, malformed JSON): reject with `success=false` and existing bad-request style error text.
5. Privilege transition failure (Linux `setresuid`/`setresgid`, Windows impersonation): reject command with `success=false`, emit sync error event, and log raw OS/platform error details server-side.
6. Debounce/rate-limit rejections for `sync-now`: return `success=true` with an explicit no-op payload (`started=false`, `reason="rate-limited"`) rather than a hard failure.
7. Identity-boundary failures must be logged with timestamp, command, normalized caller identity, target contextId, and denial reason.

### Send to Client Agent
Execute Sprint B for `phase-1.15` deferred hardening in `src/Clients/DotNetCloud.Client.SyncService/` using the identity and failure semantics above as required contract.

Required work focus:
1. Implement caller-identity extraction and context ownership enforcement at IPC boundary.
2. Implement Linux privilege dropping path per context.
3. Implement Windows impersonation path per context.
4. Add sync trigger debounce/rate limiting behavior with observable no-op response semantics.
5. Add disk-full detection and tray-facing notification path.

### Request Back
- commit hash
- exact files and tests added/updated (paths + test names)
- raw IPC command/response examples for denial paths
- raw log lines around identity mismatch and privilege-transition failures (timestamped)
- platform matrix evidence (Linux + Windows behaviors)

### Sprint B Historical Updates (Archived)

Completed Sprint B updates `#1` and `#2` are archived in
`docs/development/CLIENT_SERVER_MEDIATION_ARCHIVE.md` under
`Sprint B Archive (Phase 1.15 - updates #1-#2, archived 2026-03-10)`.

### Sprint B Update #3 - Windows Impersonation Execution Boundary (Server, Windows workspace)

**Date:** 2026-03-10  
**Owner:** Server (`Windows workspace`)  
**Status:** completed ✅

**Files added/updated:**
- `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcCallerIdentity.cs`
- `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcServer.cs`
- `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcClientHandler.cs`

**Implemented in this update:**
1. `IpcServer` now captures Windows named-pipe caller identity plus a duplicated caller access token at connection time.
2. `IpcCallerIdentity` now carries the duplicated Windows access token alongside normalized caller identity values.
3. `IpcClientHandler` now executes context-scoped operations under `WindowsIdentity.RunImpersonated` when a caller token is available.
4. Handler completion now disposes duplicated caller token handles to avoid leaking Windows token resources.
5. Failure semantics for impersonation transition errors now return deterministic IPC command errors: `Privilege transition failed.` with server-side error logs.

**Tests added/updated:**
- No new test files required.
- Existing SyncService suite run as regression validation.

**Command executed:**
- `dotnet test tests\DotNetCloud.Client.SyncService.Tests\DotNetCloud.Client.SyncService.Tests.csproj`
    - Result: total 27, succeeded 27, failed 0, skipped 0

**Remaining for Sprint B:**
- Linux per-context privilege drop (`setresuid`/`setresgid`) implementation.

### Sprint B Update #4 - Linux Privilege Drop Execution Boundary (Server, Linux workspace)

**Date:** 2026-03-10  
**Owner:** Server (`Linux workspace`)  
**Status:** completed ✅

**Files added/updated:**
- `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcCallerIdentity.cs`
- `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcServer.cs`
- `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcClientHandler.cs`
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`

**Implemented in this update:**
1. `IpcServer` now resolves Linux Unix-socket caller peer credentials (`SO_PEERCRED`) and maps UID/GID + account identity into `IpcCallerIdentity`.
2. `IpcCallerIdentity` now carries Unix UID/GID fields used for Linux privilege transitions.
3. `IpcClientHandler` now executes context-scoped operations under guarded Linux privilege transition using `setresgid`/`setresuid`, then restores original IDs after operation completion.
4. Linux privilege-transition failures now return deterministic IPC command error `Privilege transition failed.` and log raw errno with caller/context metadata.
5. Linux transition path is serialized with a transition lock to avoid overlapping process-credential mutation during context-scoped operations.

**Tests/validation executed:**
- `dotnet test tests/DotNetCloud.Client.SyncService.Tests/DotNetCloud.Client.SyncService.Tests.csproj`
    - Result: total 27, succeeded 27, failed 0, skipped 0
- `dotnet build`
    - Result: succeeded (full solution)

**Remaining for Sprint B:**
- None. Sprint B hardening scope is complete.

### Sprint C Update #1 - Folder Drag-and-Drop Recursive Upload (Server, Linux workspace)

**Date:** 2026-03-10  
**Owner:** Server (`Linux workspace`)  
**Status:** completed ✅

**Files added/updated:**
- `src/UI/DotNetCloud.UI.Web/wwwroot/js/files-drop-bridge.js`
- `src/UI/DotNetCloud.UI.Web/wwwroot/js/file-upload.js`
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileUploadComponent.razor.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/ViewModels.cs`
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`
- `docs/development/REMAINING_PHASE0_PHASE1_3SPRINT_PLAN.md`

**Implemented in this update:**
1. Browser drop bridge now traverses dropped directories recursively via `DataTransferItem.webkitGetAsEntry()` and collects file entries with relative paths.
2. Upload pipeline now preserves relative folder structure by resolving/creating nested folders through Files API (`GET /api/v1/files`, `POST /api/v1/files/folders`) before file upload.
3. Upload metadata now carries `RelativePath` from JS to Blazor queue model for dropped folder entries.
4. Existing single-file and multi-file drop/select upload flow remains intact.

**Tests/validation executed:**
- `dotnet test tests/DotNetCloud.Integration.Tests/DotNetCloud.Integration.Tests.csproj --filter "FullyQualifiedName~FilesThumbnailIntegrationTests"`
    - Result: total 2, succeeded 2, failed 0, skipped 0
- `dotnet build src/Modules/Files/DotNetCloud.Modules.Files/DotNetCloud.Modules.Files.csproj`
    - Result: succeeded
- `dotnet build src/UI/DotNetCloud.UI.Web/DotNetCloud.UI.Web.csproj`
    - Result: succeeded
- `dotnet build`
    - Result: failed due to pre-existing upstream test constructor mismatch in `tests/DotNetCloud.Modules.Files.Tests/Host/FilesControllerChunkDownloadTests.cs` (missing new `fileSystemOptions` ctor argument)

**Remaining for Sprint C:**
- Video thumbnail generation integration (FFmpeg)
- PDF thumbnail generation integration (PDF renderer)
- Touch gestures for preview (JS touch interop)

### Sprint C Update #2 - Video Thumbnail Generation (Server, Windows workspace)

**Date:** 2026-03-10  
**Owner:** Server (`Windows workspace`)  
**Status:** completed ✅

**Files added/updated:**
- `src/Modules/Files/DotNetCloud.Modules.Files/Services/IVideoFrameExtractor.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files/Services/FfmpegVideoFrameExtractor.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files/Services/ThumbnailService.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files/Services/IThumbnailService.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files.Data/FilesServiceRegistration.cs`
- `tests/DotNetCloud.Modules.Files.Tests/Services/ThumbnailServiceTests.cs`
- `tests/DotNetCloud.Modules.Files.Tests/Host/FilesControllerChunkDownloadTests.cs`
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`

**Implemented in this update:**
1. Added a video frame extraction abstraction (`IVideoFrameExtractor`) and FFmpeg implementation (`FfmpegVideoFrameExtractor`) with configurable executable path (`Files:Thumbnails:FfmpegPath`, default `ffmpeg`).
2. Extended `ThumbnailService` to process common video MIME types by extracting first-frame JPEGs and generating cached 128/256/512 thumbnails.
3. Kept image thumbnail generation flow unchanged while adding video path and temporary extraction file cleanup safeguards.
4. Wired extractor through DI (`FilesServiceRegistration`) so runtime upload completion can generate video thumbnails through the existing service pipeline.
5. Added focused unit tests for successful and failed video extraction paths; fixed upstream test constructor mismatch after `FilesController` signature expansion.

**Tests/validation executed:**
- `dotnet test tests\DotNetCloud.Modules.Files.Tests\DotNetCloud.Modules.Files.Tests.csproj --filter "FullyQualifiedName~ThumbnailServiceTests"`
    - Result: total 2, succeeded 2, failed 0, skipped 0
- `dotnet test tests\DotNetCloud.Integration.Tests\DotNetCloud.Integration.Tests.csproj --filter "FullyQualifiedName~FilesThumbnailIntegrationTests"`
    - Result: total 2, succeeded 2, failed 0, skipped 0

**Remaining for Sprint C:**
- PDF thumbnail generation integration (PDF renderer)
- Touch gestures for preview (JS touch interop)

### Sprint C Update #3 - PDF Thumbnail + Touch Gesture Completion (Server, Windows workspace)

**Date:** 2026-03-10  
**Owner:** Server (`Windows workspace`)  
**Status:** completed ✅

**Files added/updated:**
- `src/Modules/Files/DotNetCloud.Modules.Files/Services/IPdfPageRenderer.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files/Services/PdftoppmPdfPageRenderer.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files/Services/ThumbnailService.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files/Services/IThumbnailService.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files.Data/FilesServiceRegistration.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/FilePreview.razor`
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/FilePreview.razor.cs`
- `src/UI/DotNetCloud.UI.Web/wwwroot/js/file-preview-gestures.js`
- `src/UI/DotNetCloud.UI.Web/Components/App.razor`
- `tests/DotNetCloud.Modules.Files.Tests/Services/ThumbnailServiceTests.cs`
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`
- `docs/development/REMAINING_PHASE0_PHASE1_3SPRINT_PLAN.md`

**Implemented in this update:**
1. Added PDF first-page thumbnail rendering pipeline via `IPdfPageRenderer` + `PdftoppmPdfPageRenderer` (configurable command path: `Files:Thumbnails:PdfToPpmPath`, default `pdftoppm`).
2. Extended `ThumbnailService` to generate cached thumbnails for `application/pdf` using the same 128/256/512 cache strategy as image/video.
3. Added touch gesture support to `FilePreview`: swipe left/right to navigate and pinch zoom for image previews.
4. Added browser touch bridge script (`file-preview-gestures.js`) and wired it through `App.razor` and `FilePreview` JS interop lifecycle.
5. Expanded thumbnail unit tests to cover PDF success/failure paths in addition to existing video coverage.

**Tests/validation executed:**
- `dotnet test tests\DotNetCloud.Modules.Files.Tests\DotNetCloud.Modules.Files.Tests.csproj --filter "FullyQualifiedName~ThumbnailServiceTests"`
    - Result: total 4, succeeded 4, failed 0, skipped 0
- `dotnet test tests\DotNetCloud.Integration.Tests\DotNetCloud.Integration.Tests.csproj --filter "FullyQualifiedName~FilesThumbnailIntegrationTests"`
    - Result: total 2, succeeded 2, failed 0, skipped 0

**Remaining for Sprint C:**
- None. Sprint C deferred UX/media scope is complete.

---

**Sync Remediation — Issues #48–#61**

Verification of the sync implementation (2026-03-09) found 4 missing and 10 partial items.
Full plan: [SYNC_REMEDIATION_PLAN.md](SYNC_REMEDIATION_PLAN.md)

### Remediation Batch A — Quick Wins (next up)

| Issue | Task | Owner | Complexity | Description |
|-------|------|-------|------------|-------------|
| #49 | 2.6 | BOTH | LOW | Client ETag/If-None-Match for chunk downloads — ✅ `158ebdc` |
| #50 | 2.3 | CLIENT | LOW | Compression skip for pre-compressed MIME types — ✅ `158ebdc` |
| #52 | 1.2 | SERVER | LOW | RequestId in Serilog LogContext — ✅ `0a0ab19` |
| #54 | 1.9 | SERVER | LOW | Content-Disposition on versioned downloads — ✅ `0a0ab19` |
| #59 | 1.5 | CLIENT | LOW | TaskCanceledException retry in chunk transfers — ✅ `158ebdc` |
| #61 | 3.2 | CLIENT | LOW | Session resume window 18h → 48h — ✅ `158ebdc` |

**Server issues (#52, #54):** ✅ COMPLETE — commit `0a0ab19`  
**Client issues (#49, #50, #59, #61):** ✅ COMPLETE — commit `158ebdc`

### Status: ✅ Batch A fully resolved.

---

### Remediation Batch B — Medium Items (next up)

| Issue | Task | Owner | Complexity | Description | Status |
|-------|------|-------|------------|-------------|--------|
| #51 | 4.1 | CLIENT | MEDIUM | Case-sensitivity handling in SyncEngine | ✅ |
| #55 | 3.5b | CLIENT | MEDIUM | Conflict resolution settings in sync-settings.json | ✅ |
| #57 | 4.3/4.4 | CLIENT | LOW | FSW.Error event + symlink config | ✅ |
| #58 | 5.2 | CLIENT | MEDIUM | Selective sync cleanup + lazy load | ✅ |

**All client-side. No server work in this batch.**

#### Issue #51 (Task 4.1) — Case-Sensitivity Handling

- In `SyncEngine`, use `StringComparer.OrdinalIgnoreCase` for path comparisons on Windows/macOS (check `RuntimeInformation.IsOSPlatform` or equivalent).
- Before applying a remote file locally, check if a file with different casing already exists at the target path.
- If a case conflict exists on a case-insensitive filesystem, rename the incoming file to `filename (case conflict).ext`.
- Log a warning with both path variants.
- Add unit tests for case-conflict detection and renaming logic.
- Reference: `SYNC_IMPLEMENTATION_GUIDE.md` Task 4.1.

#### Issue #55 (Task 3.5b) — Conflict Resolution Settings

- Add a `conflictResolution` section to `sync-settings.json` with defaults: `{ "autoResolveEnabled": true, "newerWinsThresholdMinutes": 5, "enabledStrategies": ["identical", "fast-forward", "clean-merge", "newer-wins", "append-only"] }`.
- Wire `ConflictResolver` to read these settings from config instead of hardcoded values (e.g., the 5-minute newer-wins threshold).
- Add Settings UI controls: checkboxes for each strategy, a threshold input for `newerWinsThresholdMinutes`.
- Add unit tests verifying config-driven behavior.

#### Issue #57 (Tasks 4.3, 4.4) — FSW.Error Event + Symlink Config

- Subscribe to `FileSystemWatcher.Error` event — log the error, set `_pollingFallback = true`, and notify the user via the tray/notification system.
- Add a `symlinks` section to `sync-settings.json`: `{ "mode": "ignore" }` (with `"ignore"` and `"sync-as-link"` as valid values).
- Add a Settings UI dropdown for symlink mode.

#### Issue #58 (Task 5.2) — Selective Sync Cleanup + Lazy Load

- In `FolderBrowserViewModel`, implement lazy-load children on expand (there's currently a TODO comment for this).
- When a folder is unchecked in selective sync, delete local files for that folder (with a confirmation dialog before deletion).
- Add unit tests for lazy-load and cleanup behavior.

#### After completing all four

Update `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md` — mark all four issues in the Batch B table with ✅ and commit hashes. Update `docs/development/SYNC_REMEDIATION_PLAN.md` to mark #51, #55, #57, and #58 as ✓.

### Status: ✅ Batch B fully resolved.

---

## Resolved Issues Archive (Batch 5)

Completed Batch 5 implementation details were archived to
`docs/development/CLIENT_SERVER_MEDIATION_ARCHIVE.md` under
`Resolved Issues Archive (Batch 5, archived 2026-03-10)`.

---

## Handoff Compaction Archive (2026-03-11)

Archived from `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md` when enforcing single-active-task policy.

### Archived completed task summaries

1. Server follow-up result: Android contract alignment (Phase 2.10)
- Confirmed OIDC mobile client seeding (`dotnetcloud-mobile`) and redirects.
- Confirmed SignalR contract target: `/hubs/core`.
- Confirmed payload contract: `UnreadCountUpdated { channelId, count }`, `NewMessage { channelId, message }`.
- Confirmed chat REST still uses `userId` query contract.

2. Server validation complete: Android SignalR contract accepted (Phase 2.10)
- Server build/test sanity passed for chat module.
- Client alignment accepted for hub path + object payload handlers.
- Marked integration-ready for E2E run.

3. Phase 2.10 Live E2E — Token-Missing Deadlock and Resolution (2026-03-11)
- Client reported DOTNETCLOUD_E2E_BEARER_TOKEN=MISSING across 3+ handoff cycles.
- Server agent minted mobile OAuth token directly via auth-code + PKCE flow on mint22.
- Discovered and fixed two server-side bugs:
  a. CoreHub only accepted Identity cookie auth — bearer tokens rejected with 401.
     Fix: `[Authorize(AuthenticationSchemes = "Identity.Application,OpenIddict.Validation.AspNetCore")]`
  b. GetUserId() only checked ClaimTypes.NameIdentifier — OpenIddict bearer uses `sub` claim.
     Fix: Added `?? Context.User?.FindFirst("sub")?.Value` fallback.
- Test rewritten to be self-contained: connect → join group → SendMessageAsync → MarkReadAsync → assert events.
- SSL bypass added for self-signed cert (test-only).
- Live test PASSED: 1 test, 1 passed, 0 failed.
- Phase 2.10 Android contract alignment marked COMPLETE.

4. Phase 2.10 Client Receipt + Flaky Test Fix (2026-03-11, Windows11-TestDNC)
- Pulled `bdded31` on client — server CoreHub fix + self-contained E2E test landed cleanly.
- Build: `DotNetCloud.Core.Server` compiled 0 errors after CoreHub auth-scheme change.
- Live Android probe still skips correctly (DOTNETCLOUD_E2E_BEARER_TOKEN=MISSING; correct, token not set here).
- Full suite readiness gate initially surfaced one intermittent failure:
  - `HandleAsync_SyncNowCommand_DebounceReturnsRateLimitedOnSecondRequest` failed in full parallel run but passed in isolation.
  - Root cause: `SyncNowAsync` is invoked via fire-and-forget `Task.Run`; `Verify` ran before background task got CPU time under parallel suite load.
  - Fix: replaced `Returns(Task.CompletedTask)` with `TaskCompletionSource` + `.Callback` + `await Task.WhenAny(tcs.Task, Delay(5s))` before `Verify`.
- Full suite after fix: 3/3 consecutive passes, 0 failures, ~2040 passed, 13 skipped (all env-gated).
- Phase 2.10 fully closed on both sides.

## 5. Phase 2.13 Documentation + Migration Fix + Integration Test Fixes (2026-03-11 → 2026-03-12)

1. Phase 2.13 Documentation Complete (2026-03-11, mint22)
- Chat module docs: `docs/modules/chat/` — README, API, ARCHITECTURE, REALTIME, PUSH.
- Android app docs: `docs/clients/android/` — README, SETUP, DISTRIBUTION.
- Per-project developer READMEs for Chat Core, Chat.Data, Chat.Host.
- XML docs on all chat module public types and Android platform types.
- Test suite: 2,086 passed / 0 failed / 0 skipped.

2. Integration Testing Sprint (2026-03-11, mint22)
- Added SignalR hub, file sync flow, and chat-files cross-module integration tests.
- New test factory: `DotNetCloudWebApplicationFactory` for Core.Server in-process testing.
- 132 integration tests added across 3 new test classes.

3. Urgent Migration Fix (2026-03-12, mint22)
- `20260309093919_AddSymlinkSupport` migration not applied on mint22 PostgreSQL.
- Files UI crashed with `42703: column f.LinkTarget does not exist`.
- Fix: `ALTER TABLE "FileNodes" ADD "LinkTarget" text NULL;` + EF migration history insert.
- Rebuilt and redeployed server; Files UI confirmed working.

4. Integration Test Fixes (2026-03-12, mint22)
- 11 integration test failures from `49bdaa6` commit fixed:
  - SignalR hub tests: 401 Unauthorized — added `TestAuthHandler` scheme with `ForwardDefaultSelector` on `Identity.Application` cookie to forward to test scheme when `x-test-user-id` header present; added header to `HubConnectionBuilder` opts.
  - SignalR `SendMessageAsync`: wrong arg count (2 instead of 3) — added `null` for `replyToId`.
  - SignalR `SetPresenceAsync`: wrong status casing (`"online"` → `"Online"`).
  - File sync tests: wrong route paths (`/api/v1/files/changes` → `/api/v1/files/sync/changes`), removed stale `?userId=` params, replaced non-existent `/api/v1/files/sync-state` with `/api/v1/files/sync/tree`.
  - ChatDbContext dual-provider conflict (Npgsql + InMemory): changed `AddDbContext` to singleton `DbContextOptions` registration to avoid registering a second EF provider.
- Final suite: 2,106 passed / 0 failed / 2 skipped (env-gated).

---

## PosixMode Migration Blocker Fix (2026-03-12)

**Date:** 2026-03-12
**Owner:** Server agent (`mint22`)
**Status:** COMPLETE ✅

**Problem:** Web UI at `https://mint22:15443/` failed with `42703: column f.PosixMode does not exist`. All 6 Files module migrations were pending against the production `dotnetcloud` database. The design-time factory targeted `dotnetcloud_files_dev` (non-existent), so migrations had never been applied to the actual database.

**Root cause:** Design-time factory hardcodes `Database=dotnetcloud_files_dev`, but production uses `Database=dotnetcloud` via `DefaultConnection`. Previous migration work only applied `InitialCreate` (core) and `AddSymlinkSupport` (manually), leaving 4 Files migrations unapplied.

**Fix applied:**
1. Inserted `20260304172504_InitialFilesSchema` into `__EFMigrationsHistory` (tables already existed from prior manual creation).
2. Applied 4 pending migrations via `dotnet ef database update --connection "Host=localhost;Database=dotnetcloud;..."`:
   - `20260308113429_AddFileVersionScanStatus` — added `ScanStatus` to `FileVersions`
   - `20260308164648_AddCdcChunkMetadata` — added `ChunkSizesManifest` to `UploadSessions`, `ChunkSize`/`Offset` to `FileVersionChunks`
   - `20260309063020_AddSyncCursorSupport` — added `SyncSequence` to `FileNodes`, created `UserSyncCounters` table
   - `20260309083622_AddPosixPermissions` — added `PosixMode`/`PosixOwnerHint` to `FileNodes`, `FileVersions`, `UploadSessions`
3. Rebuilt and republished server (`dotnet publish -c Release`).
4. Restarted `dotnetcloud.service`.

**Verification:**
- All 7 migrations recorded in `__EFMigrationsHistory`.
- `PosixMode`, `PosixOwnerHint`, `SyncSequence` confirmed on `FileNodes`.
- `PosixMode`, `ScanStatus` confirmed on `FileVersions`.
- `PosixMode`, `PosixOwnerHint`, `ChunkSizesManifest` confirmed on `UploadSessions`.
- `UserSyncCounters` table created.
- Health endpoint: 200 Healthy.
- Files API: 401 (auth required, no column errors).
- Server logs: no DB errors.
- Test suite: 2,106 passed / 0 failed / 2 skipped (env-gated).

---

## Chat UI Fix — ChatPageLayout Rebuild and Redeploy (2026-03-12)

**Handoff from:** Client agent
**Executed by:** Server agent (`mint22`)
**Commit:** `f24677d`

**Problem:** Chat module's web UI was broken — clicking a channel in the sidebar did nothing. `ModuleUiRegistrationHostedService` registered `ChannelList` (the sidebar component) as the root component for `/apps/chat`. Since nobody handled its `OnChannelSelected` callback, clicks were swallowed.

**Fix applied (client-side):**
1. Created `ChatPageLayout.razor/.cs/.css` — an orchestrator component composing `ChannelList` + `ChannelHeader` + `MessageList` + `MessageComposer` into a split-pane layout.
2. Updated `ModuleUiRegistrationHostedService` to register `ChatPageLayout` instead of `ChannelList`.
3. Clicking a channel now loads messages via `IMessageService.GetMessagesAsync()` and renders the full chat conversation view.

**Server-side deploy (server agent):**
1. `git pull` — fast-forward to `f24677d`.
2. `dotnet publish src/Core/DotNetCloud.Core.Server -c Release -o artifacts/publish/server-baremetal` — build succeeded.
3. `sudo systemctl restart dotnetcloud.service` — service restarted, status: active.

**Verification:**
- Health endpoint: `curl -sk https://mint22:15443/health` → 200 Healthy (all checks: self, startup, collabora_online, linux-resources).
- `/apps/chat` route: returns 302→login (auth-gated, correct). After login redirect: 200 with Blazor server-side rendered HTML.
- Published binary verification: `ChatPageLayout` confirmed in `DotNetCloud.Modules.Chat.dll` with all async methods (`OnInitializedAsync`, `LoadChannelsAsync`, `HandleChannelSelected`, `LoadMessagesAsync`).
- `ModuleUiRegistrationHostedService` in `DotNetCloud.Core.Server.dll`: references only `ChatPageLayout`, not `ChannelList`.
- No database changes required.

---

## Chat UI Blazor Binding Fix — Redeploy Needed (2026-03-12)

**Handoff from:** Client agent (Windows11-TestDNC)
**Executed by:** Server agent (`mint22`) — PENDING

**Problem:** Chat page at `https://mint22:15443/apps/chat` shows raw variable names (`_channelErrorMessage`, `_messageErrorMessage`) as literal text instead of actual error messages. Channel list shows "Unable to load channels right now." followed by the literal text `_channelErrorMessage`.

**Root cause:** `ChatPageLayout.razor` component attribute bindings were missing the `@` prefix. In Blazor, `ErrorMessage="_channelErrorMessage"` passes the literal string `"_channelErrorMessage"` — it needs `ErrorMessage="@_channelErrorMessage"` to pass the C# field value. All attribute bindings in the component were affected.

**Fix applied (client-side):**
1. `ChatPageLayout.razor` — Added `@` prefix to all component parameter bindings (Channels, IsLoading, ErrorMessage, HasMoreMessages, TypingUsers, Channel, ReplyToMessage, MentionSuggestions, and all EventCallback bindings).
2. `DirectMessageView.razor` — Same fix applied (Messages, IsLoading, ErrorMessage, HasMoreMessages, TypingUsers, MentionSuggestions, ReplyToMessage, and all EventCallback bindings).
3. Build: succeeded (0 errors).
4. Tests: 263 Chat tests passed / 0 failed.
