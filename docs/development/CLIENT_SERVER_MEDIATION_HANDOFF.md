# Client/Server Mediation Handoff

Last updated: 2026-03-09 (All sync improvement batches complete)

Purpose: Shared handoff between client-side and server-side agents, mediated by user.

> Archived context (45 resolved issues — initial sync milestone through Batch 4.5) moved to
> [CLIENT_SERVER_MEDIATION_ARCHIVE.md](CLIENT_SERVER_MEDIATION_ARCHIVE.md).
> Full git history in commits up to `1cd594a`.

## Process Rules

- All technical findings and debugging conclusions go in this document, pushed to `main`.
- Mediator role is relay-only — commit notifications and cross-agent request forwarding.

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

**Sync Remediation — Issues #48–#61**

Verification of the sync implementation (2026-03-09) found 4 missing and 10 partial items.
Full plan: [SYNC_REMEDIATION_PLAN.md](SYNC_REMEDIATION_PLAN.md)

### Remediation Batch A — Quick Wins (next up)

| Issue | Task | Owner | Complexity | Description |
|-------|------|-------|------------|-------------|
| #49 | 2.6 | BOTH | LOW | Client ETag/If-None-Match for chunk downloads |
| #50 | 2.3 | CLIENT | LOW | Compression skip for pre-compressed MIME types |
| #52 | 1.2 | SERVER | LOW | RequestId in Serilog LogContext — ✅ `0a0ab19` |
| #54 | 1.9 | SERVER | LOW | Content-Disposition on versioned downloads — ✅ `0a0ab19` |
| #59 | 1.5 | CLIENT | LOW | TaskCanceledException retry in chunk transfers |
| #61 | 3.2 | CLIENT | LOW | Session resume window 18h → 48h |

**Server issues (#52, #54):** ✅ COMPLETE — commit `0a0ab19`  
**Client issues (#49, #50, #59, #61):** Ready for client agent on Windows11-TestDNC.

### Status: Server issues resolved. Client issues pending.

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
