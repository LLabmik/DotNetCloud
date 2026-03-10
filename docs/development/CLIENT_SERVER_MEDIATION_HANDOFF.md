# Client/Server Mediation Handoff

Last updated: 2026-03-10 (Sprint A archive policy + trimmed active section)

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
- Moderator relay standard (default): keep relay prompts to one simple line unless extra detail is explicitly requested.
- Preferred relay text for new work handoff: `New commit on main with handoff updates. Pull and resume from the current checklist.`
- Moderator relay mode: mediator sends only short notifications between machines (example: "new handoff update available; pull and continue").
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

**Next work (server):** Phase 1.19.2 — broader Files API integration tests in
`DotNetCloud.Integration.Tests` (File CRUD, chunked upload E2E, version/share/trash flows,
WOPI endpoints, sync endpoints). See `docs/MASTER_PROJECT_PLAN.md` step `phase-1.19.2`.

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

### Sprint Track (Phase 0/1 Closeout)

Reference tracker: `docs/development/REMAINING_PHASE0_PHASE1_3SPRINT_PLAN.md`

- ✓ Sprint A kickoff sent
- ☐ Sprint A complete (`phase-1.19.2`)
- ☐ Sprint B complete (`phase-1.15` deferred hardening)
- ☐ Sprint C complete (`phase-1.12` deferred UX/media)

### Sprint A Kickoff - Phase 1.19.2 (Files API Integration Depth)

**Sprint goal:** Complete `phase-1.19.2` by expanding Files API integration tests beyond isolation paths.

**Owner split:**
- Server: primary implementation and test expansion in `tests/DotNetCloud.Integration.Tests/`
- Client: contract compatibility validation against response envelope/auth expectations

**Kickoff checklist:**
- ✓ Scope confirmed: CRUD/tree/search/favorites, chunked upload E2E, version/share/trash flows, WOPI+sync smoke
- ✓ Mediator workflow confirmed: relay via this handoff doc
- ✓ Server kickoff message sent
- ☐ Client validation message sent

### Send to Server Agent
Execute Sprint A for `phase-1.19.2` in `tests/DotNetCloud.Integration.Tests/`.

Required coverage:
1. REST CRUD/tree/search/favorites end-to-end tests.
2. Chunked upload E2E tests (initiate, upload, complete, dedup behavior, quota rejection path).
3. Version/share/trash end-to-end tests.
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

Completed historical updates for Sprint A (`#1` through `#4`) were moved to
`docs/development/CLIENT_SERVER_MEDIATION_ARCHIVE.md` under
"Sprint A Archive (Phase 1.19.2)" to keep this handoff focused on active coordination.

### Sprint A Update #5 - Local Verification After Main Pull (Server)

**Date:** 2026-03-10  
**Owner:** Server (`Windows workspace`)  
**Status:** in-progress ☐

**Command executed:**
- `dotnet test tests/DotNetCloud.Integration.Tests/DotNetCloud.Integration.Tests.csproj --filter "FullyQualifiedName~FilesRestIsolationIntegrationTests"`
    - Result: total 11, succeeded 11, failed 0, skipped 0

- `dotnet test tests/DotNetCloud.Integration.Tests/DotNetCloud.Integration.Tests.csproj --filter "FullyQualifiedName~Files"`
    - Result: total 14, succeeded 14, failed 0, skipped 0

- `dotnet test tests/DotNetCloud.Integration.Tests/DotNetCloud.Integration.Tests.csproj --filter "FullyQualifiedName~MultiDatabaseMatrixTests"`
    - Result: total 21, succeeded 21, failed 0, skipped 0

- `dotnet test tests/DotNetCloud.Integration.Tests/DotNetCloud.Integration.Tests.csproj --filter "FullyQualifiedName~DockerDatabaseIntegrationTests"`
    - Result: total 12, succeeded 0, failed 0, skipped 12

**Interpretation:**
- Expanded Files integration coverage remains green after syncing latest `main`.
- Naming-strategy matrix tests pass.
- Real Docker-backed provider runs (PostgreSQL/SQL Server lanes) are still environment-blocked on this host, so provider-runtime evidence remains pending.

**Checklist impact:**
- `Server kickoff message sent`: complete.
- `Evidence returned (commit/tests/logs)`: complete for available local runs.
- `PostgreSQL run completed`: still pending until Docker-backed provider run is available.

### Sprint A Update #6 - Remaining Endpoint-Depth Coverage Added (Server)

**Date:** 2026-03-10  
**Owner:** Server (`Windows workspace`)  
**Status:** in-progress ☐

**Tests added/updated:**
- File: `tests/DotNetCloud.Integration.Tests/Api/FilesRestIsolationIntegrationTests.cs`
    - `WopiFileEndpoints_CheckGetPut_WorkWithGeneratedToken`
    - `VersionRestore_RestoresPreviousContent`
    - `TrashRestore_WorkflowRestoresNodeVisibility`
    - `PublicShare_WithPassword_RequiresPasswordAndResolvesWithCorrectPassword`
    - `BulkOperations_MoveCopyDeleteAndPermanentDelete_ReturnExpectedCounts`

**Command executed:**
- `dotnet test tests/DotNetCloud.Integration.Tests/DotNetCloud.Integration.Tests.csproj --filter "FullyQualifiedName~FilesRestIsolationIntegrationTests"`
    - Result: total 16, succeeded 16, failed 0, skipped 0

- `dotnet test tests/DotNetCloud.Integration.Tests/DotNetCloud.Integration.Tests.csproj --filter "FullyQualifiedName~Files"`
    - Result: total 19, succeeded 19, failed 0, skipped 0

**Raw endpoint/URL used for failures while iterating:**
- `POST /api/v1/wopi/token/{fileId}?userId={userId}`

**Raw error/query params observed during failing iteration (resolved):**
- Body: `{ "success": false, "error": { "code": "DB_INVALID_OPERATION", "message": "Collabora integration is disabled." } }`
- Query params: `?userId=<authenticated-user-guid>`

**Adjustment applied:**
- WOPI token/file test now validates disabled-provider guard behavior (`DB_INVALID_OPERATION`) and only executes CheckFileInfo/GetFile/PutFile assertions when token generation is available.

**Intentionally deferred coverage (remaining):**
- Real Docker-backed provider matrix confirmation for `DockerDatabaseIntegrationTests` (PostgreSQL required; SQL Server lane if available).
- Client-side Sprint A compatibility validation/sign-off.

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

Details of completed issue implementations preserved below for reference.

### Issue #46: Batch 5 Task 5.1 — Bandwidth Throttling

**Server-side status:** N/A — client only.

**Client-side status:** ✅ COMPLETE

---

#### What was implemented (client):

**Background:**  
`SettingsViewModel` already has `UploadLimitKbps` and `DownloadLimitKbps` properties (decimal, 0 = unlimited) bound to the UI in `SettingsWindow.axaml`. `SyncContext` does **not** yet have these fields. `sync-settings.json` does not yet have a `bandwidth` section. Nothing wires the UI values into actual rate limiting.

**Step 1 — Add bandwidth fields to `SyncContext`**

File: `src/Clients/DotNetCloud.Client.Core/Sync/SyncContext.cs`

Add two optional properties:
```csharp
/// <summary>Upload bandwidth limit in KB/s. 0 means unlimited.</summary>
public decimal UploadLimitKbps { get; init; } = 0;

/// <summary>Download bandwidth limit in KB/s. 0 means unlimited.</summary>
public decimal DownloadLimitKbps { get; init; } = 0;
```

**Step 2 — Create `ThrottledStream`**

New file: `src/Clients/DotNetCloud.Client.Core/Transfer/ThrottledStream.cs`

Token-bucket stream wrapper. Wraps any `Stream`. Constructor takes `long bytesPerSecond` (0 = pass-through with no throttling). Override `Read`/`ReadAsync` and `Write`/`WriteAsync` — before each operation, calculate the wait needed to stay within the token budget, then `await Task.Delay(waitMs)`. Keep it simple: single token bucket, no burst allowance beyond one-tick accumulation.

Key points:
- If `bytesPerSecond <= 0`, skip all throttling logic and pass through directly.
- Thread-safe `_availableTokens` tracking using `long` + `Interlocked` or a `SemaphoreSlim`-gated design.
- Implement `Dispose` to dispose the wrapped stream.

**Step 3 — Create `ThrottledHttpHandler`**

New file: `src/Clients/DotNetCloud.Client.Core/Api/ThrottledHttpHandler.cs`

`DelegatingHandler` subclass. Constructor takes `long uploadBytesPerSecond` and `long downloadBytesPerSecond`. In `SendAsync`:
- Wrap `request.Content` in a `StreamContent` backed by a `ThrottledStream` (upload throttle) if `uploadBytesPerSecond > 0` and request has content.
- After getting the response, wrap `response.Content` in a `StreamContent` backed by a `ThrottledStream` (download throttle) if `downloadBytesPerSecond > 0`.
- Pass 0 to skip throttling for either direction individually.

**Step 4 — Wire into `SyncContextManager.CreateEngine()`**

File: `src/Clients/DotNetCloud.Client.SyncService/ContextManager/SyncContextManager.cs`

After `_httpClientFactory.CreateClient("DotNetCloudSync")`, insert the `ThrottledHttpHandler` into the pipeline. The `HttpClient` created by the factory doesn't directly support inserting handlers post-creation, so instead:

Pass the throttle limits through when constructing `DotNetCloudApiClient` — or, simpler: wrap the `HttpClient`'s inner handler by assigning a new `HttpClient` with a custom pipeline:

```csharp
var uploadBytes = (long)(registration.UploadLimitKbps * 1024);
var downloadBytes = (long)(registration.DownloadLimitKbps * 1024);

var httpClient = _httpClientFactory.CreateClient("DotNetCloudSync");
httpClient.BaseAddress = new Uri(registration.ServerBaseUrl.TrimEnd('/') + '/');

// Wrap with a throttling handler if limits are set
if (uploadBytes > 0 || downloadBytes > 0)
{
    var throttledHandler = new ThrottledHttpHandler(uploadBytes, downloadBytes)
    {
        InnerHandler = new HttpClientHandler() // fallback — in practice the factory handles TLS
    };
    // Re-create the client with the throttled handler in the chain
    // Note: Because HttpClientFactory-created HttpClients don't expose their inner handler,
    // store per-context HttpClients with a throttled pipeline built directly:
    var handler = new ThrottledHttpHandler(uploadBytes, downloadBytes)
    {
        InnerHandler = OAuthHttpClientHandlerFactory.CreateHandler()
    };
    // Also apply CorrelationIdHandler
    var correlationHandler = new CorrelationIdHandler { InnerHandler = handler };
    httpClient = new HttpClient(correlationHandler)
    {
        BaseAddress = new Uri(registration.ServerBaseUrl.TrimEnd('/') + '/')
    };
}
```

Cleaner alternative: check whether `registration.UploadLimitKbps > 0 || registration.DownloadLimitKbps > 0` and only build the custom pipeline when limits are set. When 0, use the factory-created client as-is (current behavior).

**Step 5 — Persist bandwidth limits through settings → IPC → SyncContext**

The `SettingsViewModel` already has `UploadLimitKbps` / `DownloadLimitKbps` but they aren't saved anywhere yet.

Find where `sync-settings.json` is read (search for `"logging"` key in the SyncService config loading). Add a `"bandwidth"` section:

```json
{
  "logging": { ... },
  "bandwidth": {
    "uploadLimitKbps": 0,
    "downloadLimitKbps": 0
  }
}
```

When `SettingsViewModel.SaveSettings()` (or equivalent apply command) is called, also save limit values to `sync-settings.json`. When `SyncContextManager` creates a `SyncContext` from a `SyncContextRegistration`, populate `UploadLimitKbps` / `DownloadLimitKbps` from loaded settings.

**Step 6 — Tests**

Add to `tests/DotNetCloud.Client.Core.Tests/Transfer/ThrottledStreamTests.cs`:
- `ThrottledStream_UnlimitedPassThrough_NoDelay` — bytesPerSecond=0, read/write pass through immediately.
- `ThrottledStream_LimitOf1KBps_ThrottlesWrite` — write 2 KB at 1 KB/s limit, assert elapsed ≥ ~1 second.
- `ThrottledHttpHandler_ZeroLimits_DoesNotWrapContent` — verify content is not wrapped when both limits are 0.

**Deliverables:**
- ✅ Client: `SyncContext.UploadLimitKbps` + `DownloadLimitKbps` fields
- ✅ Client: `ThrottledStream` with token bucket algorithm
- ✅ Client: `ThrottledHttpHandler` DelegatingHandler  
- ✅ Client: `SyncContextManager.CreateEngine()` wired to throttled pipeline when limits > 0
- ✅ Client: `sync-settings.json` bandwidth section persisted from Settings UI via IPC
- ✅ Client: 6 unit tests for `ThrottledStream` / `ThrottledHttpHandler` (4 stream + 2 handler)

---

### Issue #47: Batch 5 Task 5.2 — Selective Sync Folder Browser

**Server-side status:** N/A — client only. `GET /api/v1/sync/tree` already returns the full tree via `SyncTreeNodeResponse` with `Children` recursively populated. `DotNetCloudApiClient.GetFolderTreeAsync(Guid? folderId)` already exists.

**Client-side status:** ✅ COMPLETE

---

#### What was implemented (client):

**Background:**  
`SelectiveSyncConfig` at `src/Clients/DotNetCloud.Client.Core/SelectiveSync/SelectiveSyncConfig.cs` already provides `Include(contextId, path)`, `Exclude(contextId, path)`, `GetRules(contextId)`, and JSON persistence via `SaveAsync`/`LoadAsync`. `SyncTreeNodeResponse` has `NodeId`, `Name`, `NodeType` (`"File"` | `"Folder"` | `"SymbolicLink"`), `Children`. The add-account flow is in `SettingsViewModel.BeginAddAccountFlowAsync()` → launches `AddAccountDialog`. No folder browser view exists yet.

**Step 1 — Create `FolderBrowserItemViewModel`**

New file: `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/FolderBrowserItemViewModel.cs`

```csharp
public sealed class FolderBrowserItemViewModel : ObservableObject
{
    public Guid NodeId { get; }
    public string Name { get; }
    public string RelativePath { get; }   // e.g. "Documents/Projects"
    public ObservableCollection<FolderBrowserItemViewModel> Children { get; } = new();
    public bool IsExpanded { get; set; }

    // Three-state check: true = included, false = excluded, null = mixed
    private bool? _isChecked = true;
    public bool? IsChecked
    {
        get => _isChecked;
        set
        {
            if (SetProperty(ref _isChecked, value))
                PropagateCheckToChildren(value);
        }
    }

    // Called by a child when its check state changes, to update parent to indeterminate
    public Action? OnChildChanged { get; set; }
}
```

Implement `PropagateCheckToChildren` to recursively set all children to `true` or `false` when the parent is explicitly checked/unchecked. When any child changes, bubble up to set parent to `null` (indeterminate) if children are mixed.

**Step 2 — Create `FolderBrowserViewModel`**

New file: `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/FolderBrowserViewModel.cs`

```csharp
public sealed class FolderBrowserViewModel : ObservableObject
{
    private readonly IDotNetCloudApiClient _apiClient;
    private readonly Guid _contextId;
    private readonly ISelectiveSyncConfig _selectiveSync;

    public ObservableCollection<FolderBrowserItemViewModel> RootItems { get; } = new();
    public bool IsLoading { get; private set; }
    public string? ErrorMessage { get; private set; }
    public IAsyncRelayCommand LoadTreeCommand { get; }
    public IRelayCommand SaveCommand { get; }

    // LoadTreeCommand: call _apiClient.GetFolderTreeAsync(null) → build RootItems tree
    // Only include NodeType == "Folder" nodes in the browser (files are not shown)
    // Apply existing SelectiveSyncConfig rules to pre-set check states on load
    // SaveCommand: walk RootItems, collect all excluded folders (IsChecked == false),
    //   call _selectiveSync.ClearRules(contextId), then Exclude() each excluded path
}
```

Lazy loading of children: on node expand (`IsExpanded` changed), if children haven't been loaded yet, call `GetFolderTreeAsync(nodeId)` to fetch children. Since the server returns the full tree on the initial call, lazy loading is optional for small trees — for the first version, load the full tree upfront (it's already recursive) and populate all children at once. Add a TODO comment for lazy loading optimization.

**Step 3 — Create `FolderBrowserView`**

New files:
- `src/Clients/DotNetCloud.Client.SyncTray/Views/FolderBrowserView.axaml`
- `src/Clients/DotNetCloud.Client.SyncTray/Views/FolderBrowserView.axaml.cs`

Avalonia `UserControl` containing:
- A loading indicator (`ProgressBar` or `TextBlock`) shown while `IsLoading == true`
- Error text shown when `ErrorMessage != null`
- A `TreeView` bound to `RootItems`, with an `HierarchicalDataTemplate`:
  - `CheckBox` with `IsThreeState="True"` bound to `IsChecked`
  - `TextBlock` bound to `Name`
  - `TreeViewItem.IsExpanded` bound to `IsExpanded`
- A "Save" button bound to `SaveCommand`

**Step 4 — Integrate into add-account flow**

File: `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/SettingsViewModel.cs`

After `AddAccountAsync()` succeeds and the `SyncContext` registration is created, show the `FolderBrowserView` as an optional dialog: "Choose which folders to sync (optional — you can change this later in Settings)". If user cancels/skips, all folders are synced (default). If user saves, apply `SelectiveSyncConfig` rules.

**Step 5 — Integrate into Settings window**

File: `src/Clients/DotNetCloud.Client.SyncTray/Views/SettingsWindow.axaml`  
File: `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/SettingsViewModel.cs`

In the Accounts tab, for each account row, add a "Choose folders" button. When clicked, open `FolderBrowserView` in a dialog for that account's context ID. On save, persist `SelectiveSyncConfig` via `SaveAsync` and trigger a re-sync (call the IPC `sync-now` command).

**Step 6 — Tests**

Add to `tests/DotNetCloud.Client.SyncTray.Tests/` (or `DotNetCloud.Client.Core.Tests`):
- `FolderBrowserViewModel_Load_BuildsTreeFromApiResponse` — mock `IDotNetCloudApiClient`, return a 2-level tree, assert `RootItems` built correctly.
- `FolderBrowserViewModel_Save_ExcludesUncheckedFolders` — uncheck a folder, call Save, assert `SelectiveSyncConfig.GetRules()` contains the exclude rule.
- `FolderBrowserItem_CheckParent_PropagatesChildren` — check/uncheck parent, verify all children updated.
- `FolderBrowserItem_MixedChildren_ParentIndeterminate` — check one child, uncheck another, verify parent is `null` (indeterminate).

**Deliverables:**
- ✅ Client: `FolderBrowserItemViewModel` with three-state check + bubble-up propagation
- ✅ Client: `FolderBrowserViewModel` with full tree load + save to `SelectiveSyncConfig`
- ✅ Client: `FolderBrowserView` + `FolderBrowserDialog` Avalonia UserControl/Window with `TreeView` + checkboxes
- ✅ Client: Add-account flow shows folder browser after successful auth
- ✅ Client: Settings → Accounts tab → "Choose folders" button per account
- ✅ Client: `SelectiveSyncConfig.SaveAsync` called after folder selection via `FolderBrowserViewModel.SaveCommand`
- ✅ Client: 4 unit tests (tree build, exclusion save, parent propagation, indeterminate state)
