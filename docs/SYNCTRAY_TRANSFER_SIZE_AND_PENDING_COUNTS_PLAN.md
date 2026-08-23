# SyncTray: Transfer Size Display & Pending Count Updates

> Implementation plan for the desktop SyncTray client (`DotNetCloud.Client.SyncTray` + shared `DotNetCloud.Client.Core`).

## Purpose

1. During an active transfer, the Sync Progress window must show the **real size** of the file being transferred. When the size cannot be determined, it must show **"unknown"** — never "0 KB".
2. The **Incoming (↓) / Outgoing (↑) remaining counts** must decrease as each file finishes transferring.

## Root Causes

| Symptom                         | Cause                                                                                                                                                                                                                                                                                                                                                                                 |
| ------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Download size always "0 KB"     | `DotNetCloudApiClient.GetChunkManifestAsync` builds a `ChunkManifestResponse` with only chunk hashes. `TotalSize` is never set (defaults `0`). That `0` flows: `ChunkedTransferClient.DownloadChunksAsync` → `SyncContextManager` → `TrayViewModel` → `ActiveTransferViewModel.BytesLabel`.                                                                                           |
| "0 KB" instead of "unknown"     | `ActiveTransferViewModel.BytesLabel` formats `TotalBytes` with `FormatBytes`, so `0` renders as `"0 KB"`.                                                                                                                                                                                                                                                                             |
| Counts never change during sync | `AccountViewModel.PendingUploads/PendingDownloads` are only set from `SyncStatus` in `TrayViewModel.OnSyncProgress` / `RefreshAccountsAsync`. At pass start the engine emits `StatusChanged` with **0** counts (`SetState(SyncState.Syncing)`), which wipes them. `OnTransferComplete` only updates the end-of-cycle toast aggregation (`_cycleTransfers`), never the account counts. |

---

## Change 1 — Populate manifest `TotalSize` from node metadata

**File:** `src/Clients/DotNetCloud.Client.Core/Api/DotNetCloudApiClient.cs`
**Method:** `GetChunkManifestAsync` (~line 430)

The server's `/chunks` endpoint returns only a list of chunk hashes, not the total size. Fetch the node metadata (which already has `Size`) and set `TotalSize`. Must stay non-throwing (best-effort).

**BEFORE:**

```csharp
    public async Task<ChunkManifestResponse> GetChunkManifestAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        // Server returns IReadOnlyList<string> (chunk hashes only), not an object.
        var hashes = await GetAsync<List<string>>($"api/v1/files/{nodeId}/chunks", cancellationToken) ?? [];
        return new ChunkManifestResponse
        {
            Chunks = hashes.Select((h, i) => new ChunkManifestEntry { Index = i, Hash = h }).ToList(),
        };
    }
```

**AFTER:**

```csharp
    public async Task<ChunkManifestResponse> GetChunkManifestAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        // Server returns IReadOnlyList<string> (chunk hashes only), not an object.
        var hashes = await GetAsync<List<string>>($"api/v1/files/{nodeId}/chunks", cancellationToken) ?? [];

        // The chunks endpoint does not include the total file size. Fetch the node
        // metadata to populate TotalSize for accurate transfer progress. Best-effort:
        // if the node cannot be fetched, leave TotalSize at 0 (UI shows "unknown").
        long totalSize = 0;
        try
        {
            var node = await GetNodeAsync(nodeId, cancellationToken);
            totalSize = node.Size;
        }
        catch
        {
            totalSize = 0;
        }

        return new ChunkManifestResponse
        {
            TotalSize = totalSize,
            Chunks = hashes.Select((h, i) => new ChunkManifestEntry { Index = i, Hash = h }).ToList(),
        };
    }
```

**Notes:**

- `GetNodeAsync` is defined in the same class and already calls `GetAsync<FileNodeResponse>($"api/v1/files/{nodeId}")`.
- `FileNodeResponse.Size` is `long` and already exists (see `src/Clients/DotNetCloud.Client.Core/Api/ApiModels.cs`).
- The catch must swallow **all** exceptions (`InvalidOperationException` from a null node, `HttpRequestException` from 404/5xx) so a size-lookup failure never breaks a download.

---

## Change 2 — Render "unknown" when total size is unknown

**File:** `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/ActiveTransferViewModel.cs`
**Property:** `BytesLabel`

**BEFORE:**

```csharp
    /// <summary>Human-readable transferred/total string (e.g. "12.3 MB / 100 MB").</summary>
    public string BytesLabel =>
        $"{FormatBytes(BytesTransferred)} / {FormatBytes(TotalBytes)}";
```

**AFTER:**

```csharp
    /// <summary>
    /// Human-readable transferred/total string (e.g. "12.3 MB / 100 MB").
    /// When the total size is unknown, shows "12.3 MB / unknown".
    /// </summary>
    public string BytesLabel =>
        TotalBytes > 0
            ? $"{FormatBytes(BytesTransferred)} / {FormatBytes(TotalBytes)}"
            : $"{FormatBytes(BytesTransferred)} / unknown";
```

**Notes:**

- Do **not** change `FormatBytes`. It is only used for the transferred bytes and known sizes.
- `BytesLabel` is already re-evaluated in `Update()` and `MarkComplete()` via `OnPropertyChanged(nameof(BytesLabel))`, so no new notifications are needed.
- `MarkComplete(long totalBytes)` sets `TotalBytes` to the actual completed size (from the transfer-complete event), so **finished** items always show the real size.
- The `Eta` property already returns `"—"` when the total is unknown — leave it as-is.

---

## Change 3 — Engine reports pending upload/download counts

**File:** `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs`
**Methods:** `ReportFullSyncProgress` (~line 2999) and `ReportPhaseProgress` (~line 3024)

Add two optional parameters and set them on the emitted `SyncStatus`.

**BEFORE (`ReportFullSyncProgress`):**

```csharp
    private void ReportFullSyncProgress(SyncContext context, string phaseLabel, int completedItems, int totalItems)
    {
        _fullSyncCompletedItems = completedItems;
        _fullSyncTotalItems = totalItems;

        StatusChanged?.Invoke(this, new SyncStatusChangedEventArgs
        {
            Context = context,
            Status = new SyncStatus
            {
                State = SyncState.Syncing,
                IsFullSync = _isFullSync,
                FullSyncPhaseLabel = phaseLabel,
                FullSyncCompletedItems = completedItems,
                FullSyncTotalItems = totalItems,
            },
        });
    }
```

**AFTER (`ReportFullSyncProgress`):**

```csharp
    private void ReportFullSyncProgress(
        SyncContext context,
        string phaseLabel,
        int completedItems,
        int totalItems,
        int pendingUploads = 0,
        int pendingDownloads = 0)
    {
        _fullSyncCompletedItems = completedItems;
        _fullSyncTotalItems = totalItems;

        StatusChanged?.Invoke(this, new SyncStatusChangedEventArgs
        {
            Context = context,
            Status = new SyncStatus
            {
                State = SyncState.Syncing,
                IsFullSync = _isFullSync,
                FullSyncPhaseLabel = phaseLabel,
                FullSyncCompletedItems = completedItems,
                FullSyncTotalItems = totalItems,
                PendingUploads = pendingUploads,
                PendingDownloads = pendingDownloads,
            },
        });
    }
```

**BEFORE (`ReportPhaseProgress`):**

```csharp
    private void ReportPhaseProgress(SyncContext context, string phaseLabel, int completedItems, int totalItems)
    {
        StatusChanged?.Invoke(this, new SyncStatusChangedEventArgs
        {
            Context = context,
            Status = new SyncStatus
            {
                State = SyncState.Syncing,
                IsFullSync = false,
                FullSyncPhaseLabel = phaseLabel,
                FullSyncCompletedItems = completedItems,
                FullSyncTotalItems = totalItems,
            },
        });
    }
```

**AFTER (`ReportPhaseProgress`):**

```csharp
    private void ReportPhaseProgress(
        SyncContext context,
        string phaseLabel,
        int completedItems,
        int totalItems,
        int pendingUploads = 0,
        int pendingDownloads = 0)
    {
        StatusChanged?.Invoke(this, new SyncStatusChangedEventArgs
        {
            Context = context,
            Status = new SyncStatus
            {
                State = SyncState.Syncing,
                IsFullSync = false,
                FullSyncPhaseLabel = phaseLabel,
                FullSyncCompletedItems = completedItems,
                FullSyncTotalItems = totalItems,
                PendingUploads = pendingUploads,
                PendingDownloads = pendingDownloads,
            },
        });
    }
```

---

## Change 4 — Pass counts at the "Syncing N files…" phase report

**File:** `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs`
**Method:** `SyncAsync` (~lines 291–305)

The engine already computes `pendingCount` (an object with `Uploads` and `Downloads`). Pass those to the phase report.

**BEFORE:**

```csharp
            var pendingCount = await _stateDb.GetPendingOperationCountAsync(context.StateDatabasePath, cancellationToken);
            var totalPending = pendingCount.Downloads + pendingCount.Uploads;
            if (totalPending > 0)
            {
                if (_isFullSync)
                {
                    _fullSyncTotalItems = totalPending;
                    _fullSyncCompletedItems = 0;
                    ReportFullSyncProgress(context, $"Syncing {totalPending} files…", 0, totalPending);
                }
                else
                {
                    ReportPhaseProgress(context, $"Syncing {totalPending} files…", 0, totalPending);
                }
            }
```

**AFTER:**

```csharp
            var pendingCount = await _stateDb.GetPendingOperationCountAsync(context.StateDatabasePath, cancellationToken);
            var totalPending = pendingCount.Downloads + pendingCount.Uploads;
            if (totalPending > 0)
            {
                if (_isFullSync)
                {
                    _fullSyncTotalItems = totalPending;
                    _fullSyncCompletedItems = 0;
                    ReportFullSyncProgress(
                        context,
                        $"Syncing {totalPending} files…",
                        0,
                        totalPending,
                        pendingCount.Uploads,
                        pendingCount.Downloads);
                }
                else
                {
                    ReportPhaseProgress(
                        context,
                        $"Syncing {totalPending} files…",
                        0,
                        totalPending,
                        pendingCount.Uploads,
                        pendingCount.Downloads);
                }
            }
```

**Notes:**

- Leave the other call sites (`"Fetching server file list…"`, `"Scanning local changes…"`, `"Full sync complete"`) unchanged — they use the new default `0` values.
- `SyncStatus` already has `PendingUploads` / `PendingDownloads` properties (see `src/Clients/DotNetCloud.Client.Core/Sync/SyncStatus.cs`). No new types needed.

---

## Change 5 — Don't wipe pending counts on the "Syncing" state event

**File:** `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/TrayViewModel.cs`
**Method:** `OnSyncProgress` (~lines 521–548)

`SetState(SyncState.Syncing)` and the phase reports without counts all carry `0` counts. Only apply counts when the engine reports a non-zero value, so the live decrementing counts (Change 6) are not erased.

**BEFORE:**

```csharp
        vm.State = stateStr;
        vm.PendingUploads = e.Status.PendingUploads;
        vm.PendingDownloads = e.Status.PendingDownloads;
    }

    // Forward the full status snapshot to any subscribers (e.g. SyncProgressViewModel).
    SyncStatusUpdated?.Invoke(e.Status);

    UpdateAggregateState();
```

**AFTER:**

```csharp
        vm.State = stateStr;

        // Only apply pending counts when the engine reports a non-zero value.
        // State/phase-transition events (e.g. the initial "Syncing" state) carry
        // 0 counts and must not wipe the live counts that OnTransferComplete
        // decrements as each file completes.
        if (e.Status.PendingUploads > 0 || e.Status.PendingDownloads > 0)
        {
            vm.PendingUploads = e.Status.PendingUploads;
            vm.PendingDownloads = e.Status.PendingDownloads;
        }
    }

    // Forward the full status snapshot to any subscribers (e.g. SyncProgressViewModel).
    SyncStatusUpdated?.Invoke(e.Status);

    UpdateAggregateState();
```

---

## Change 6 — Decrement remaining counts on transfer completion

**File:** `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/TrayViewModel.cs`
**Method:** `OnTransferComplete` (~lines 811–852)

After the existing per-cycle aggregation, decrement the matching account count and raise a new `PendingCountsUpdated` event so the Sync Progress window refreshes.

**BEFORE (relevant portion):**

```csharp
        vm.MarkComplete(e.TotalBytes);

        // Count towards the current cycle aggregation.
        _cycleTransfers.TryGetValue(e.ContextId, out var counts);
        if (e.Direction == "upload")
            _cycleTransfers[e.ContextId] = (counts.Uploads + 1, counts.Downloads);
        else
            _cycleTransfers[e.ContextId] = (counts.Uploads, counts.Downloads + 1);
    }

    // Auto-dismiss after 5 seconds.
    _ = Task.Run(async () =>
```

**AFTER:**

```csharp
        vm.MarkComplete(e.TotalBytes);

        // Count towards the current cycle aggregation.
        _cycleTransfers.TryGetValue(e.ContextId, out var counts);
        if (e.Direction == "upload")
            _cycleTransfers[e.ContextId] = (counts.Uploads + 1, counts.Downloads);
        else
            _cycleTransfers[e.ContextId] = (counts.Uploads, counts.Downloads + 1);
    }

    // Decrement the remaining pending count as each file finishes transferring.
    if (_accounts.TryGetValue(e.ContextId, out var account))
    {
        if (e.Direction == "upload")
            account.PendingUploads = Math.Max(0, account.PendingUploads - 1);
        else
            account.PendingDownloads = Math.Max(0, account.PendingDownloads - 1);

        PendingCountsUpdated?.Invoke();
        UpdateAggregateState();
    }

    // Auto-dismiss after 5 seconds.
    _ = Task.Run(async () =>
```

**Notes:**

- `account.PendingUploads` / `account.PendingDownloads` are `int` properties with `SetProperty` (see `AccountViewModel.cs`), so assigning them raises their own `PropertyChanged`.
- Clamp at `0` with `Math.Max` (already used elsewhere in this file, no new `using` needed).

---

## Change 7 — Add the `PendingCountsUpdated` event and subscribe

### 7a. Declare the event

**File:** `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/TrayViewModel.cs`

Add this declaration immediately after the existing `SyncStatusUpdated` event (~line 527):

```csharp
    /// <summary>
    /// Raised when pending upload/download counts change (e.g. after a file
    /// finishes transferring). Lets the Sync Progress window refresh its counts.
    /// </summary>
    public event Action? PendingCountsUpdated;
```

### 7b. Subscribe / unsubscribe

**File:** `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/SyncProgressViewModel.cs`

**Constructor** (~line 38, inside the block that already subscribes):

```csharp
        _trayVm.PropertyChanged += OnTrayVmPropertyChanged;
        _trayVm.ActiveTransfers.CollectionChanged += OnActiveTransfersChanged;
        _trayVm.SyncStatusUpdated += OnSyncStatusUpdated;
        _trayVm.SyncErrorRaised += OnSyncError;
```

Add one line:

```csharp
        _trayVm.PendingCountsUpdated += UpdateDerivedProperties;
```

**Dispose()** (end of file):

```csharp
        _trayVm.PropertyChanged -= OnTrayVmPropertyChanged;
        _trayVm.ActiveTransfers.CollectionChanged -= OnActiveTransfersChanged;
        _trayVm.SyncStatusUpdated -= OnSyncStatusUpdated;
        _trayVm.SyncErrorRaised -= OnSyncError;
```

Add one line:

```csharp
        _trayVm.PendingCountsUpdated -= UpdateDerivedProperties;
```

**Notes:**

- `UpdateDerivedProperties()` is `private void UpdateDerivedProperties()` — it matches the `Action` delegate signature exactly.
- `UpdateDerivedProperties()` already raises `TotalPendingUploads`, `TotalPendingDownloads`, and `HasPendingItems` — which is exactly what the Sync Progress window binds to (`Views/SyncProgressWindow.axaml`).

---

## Tests

### Test 1 — Manifest total size populated (`Client.Core`)

**File:** `tests/DotNetCloud.Client.Core.Tests/Api/DotNetCloudApiClientTests.cs`

Add a test that verifies `GetChunkManifestAsync` returns `TotalSize` from node metadata:

```csharp
    [TestMethod]
    public async Task GetChunkManifestAsync_PopulatesTotalSizeFromNode()
    {
        var nodeId = Guid.CreateVersion7();
        var client = CreateMockHttpClient(req =>
        {
            if (req.RequestUri!.PathAndQuery.Contains("/chunks"))
                return JsonOk(new List<string> { "aaaa", "bbbb" });
            return JsonOk(new FileNodeResponse
            {
                Id = nodeId,
                Name = "f.bin",
                NodeType = "File",
                Size = 2048,
            });
        });
        var apiClient = new DotNetCloudApiClient(client, NullLogger<DotNetCloudApiClient>.Instance);

        var result = await apiClient.GetChunkManifestAsync(nodeId);

        Assert.AreEqual(2, result.Chunks.Count);
        Assert.AreEqual(2048, result.TotalSize);
    }

    [TestMethod]
    public async Task GetChunkManifestAsync_NodeFetchFails_TotalSizeFallsBackToZero()
    {
        var nodeId = Guid.CreateVersion7();
        var client = CreateMockHttpClient(req =>
        {
            if (req.RequestUri!.PathAndQuery.Contains("/chunks"))
                return JsonOk(new List<string> { "aaaa" });
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var apiClient = new DotNetCloudApiClient(client, NullLogger<DotNetCloudApiClient>.Instance);

        var result = await apiClient.GetChunkManifestAsync(nodeId);

        Assert.AreEqual(1, result.Chunks.Count);
        Assert.AreEqual(0, result.TotalSize);
    }
```

**Notes:**

- The `CreateMockHttpClient` helper and `JsonOk<T>` already exist in this test file. `System.Net` (`HttpStatusCode`) and `DotNetCloud.Client.Core.Api` are already imported.
- `GetAsync<T>` retries only 5xx/429, not 404, so the fallback test is fast and deterministic.

### Test 2 — `BytesLabel` shows "unknown" (`SyncTray`)

Add a test in the SyncTray test project (either a new `ActiveTransferViewModelTests.cs` or an existing view-model test file). The view-model has no hard dependencies, so it can be constructed directly:

```csharp
    [TestMethod]
    public void BytesLabel_WhenTotalBytesUnknown_ShowsUnknown()
    {
        var vm = new ActiveTransferViewModel(Guid.CreateVersion7(), "big.bin", "download");
        vm.Update(bytesTransferred: 0, totalBytes: 0, chunksCompleted: 0, chunksTotal: 0, percentComplete: 0);

        Assert.AreEqual("0 KB / unknown", vm.BytesLabel);
    }

    [TestMethod]
    public void BytesLabel_WhenTotalBytesKnown_ShowsRealSize()
    {
        var vm = new ActiveTransferViewModel(Guid.CreateVersion7(), "big.bin", "download");
        vm.Update(bytesTransferred: 1024, totalBytes: 2048, chunksCompleted: 1, chunksTotal: 2, percentComplete: 50);

        Assert.AreEqual("1 KB / 2 KB", vm.BytesLabel);
    }
```

> Note: the exact "0 KB" string depends on the current `FormatBytes` implementation (which returns `"0 KB"` for `0`). If the wording is changed, adjust the expected string accordingly. The important assertion is that the text contains `"unknown"` when `TotalBytes == 0`.

### Test 3 — `TrayViewModel` decrements counts (`SyncTray`)

**File:** `tests/DotNetCloud.Client.SyncTray.Tests/ViewModels/TrayViewModelTests.cs`

Add:

```csharp
    [TestMethod]
    public async Task OnTransferComplete_DecrementsPendingCounts()
    {
        var (vm, syncMock, _, _) = BuildVm();
        var contextId = Guid.CreateVersion7();

        await SeedAccountAsync(vm, syncMock, contextId, "Syncing");

        // Seed the initial pending counts.
        syncMock.Raise(
            i => i.SyncProgress += null,
            syncMock.Object,
            new SyncProgressEventArgs
            {
                ContextId = contextId,
                Status = new SyncStatus { State = SyncState.Syncing, PendingUploads = 2, PendingDownloads = 1 },
            });

        syncMock.Raise(i => i.TransferComplete += null, syncMock.Object,
            new ContextTransferCompleteEventArgs { ContextId = contextId, FileName = "a.txt", Direction = "upload", TotalBytes = 100 });

        var account = vm.Accounts.First(a => a.ContextId == contextId);
        Assert.AreEqual(1, account.PendingUploads);
        Assert.AreEqual(1, account.PendingDownloads);
    }

    [TestMethod]
    public async Task OnSyncProgress_SyncingWithZeroCounts_DoesNotWipePendingCounts()
    {
        var (vm, syncMock, _, _) = BuildVm();
        var contextId = Guid.CreateVersion7();

        await SeedAccountAsync(vm, syncMock, contextId, "Idle");

        // Seed counts.
        syncMock.Raise(
            i => i.SyncProgress += null,
            syncMock.Object,
            new SyncProgressEventArgs
            {
                ContextId = contextId,
                Status = new SyncStatus { State = SyncState.Syncing, PendingUploads = 3, PendingDownloads = 2 },
            });

        // A later phase event with zero counts must not wipe them.
        syncMock.Raise(
            i => i.SyncProgress += null,
            syncMock.Object,
            new SyncProgressEventArgs
            {
                ContextId = contextId,
                Status = new SyncStatus { State = SyncState.Syncing, PendingUploads = 0, PendingDownloads = 0 },
            });

        var account = vm.Accounts.First(a => a.ContextId == contextId);
        Assert.AreEqual(3, account.PendingUploads);
        Assert.AreEqual(2, account.PendingDownloads);
    }
```

**Notes:**

- Use the existing `BuildVm()` / `SeedAccountAsync(...)` helpers already in `TrayViewModelTests`.
- The new tests must live in a class that can access the internal `OnTransferComplete` via the `TransferComplete` event (the existing tests already do this via `syncMock.Raise(i => i.TransferComplete += null, ...)`).

### Test 4 — `SyncProgressViewModel` refreshes counts (`SyncTray`)

**File:** `tests/DotNetCloud.Client.SyncTray.Tests/ViewModels/SyncProgressViewModelTests.cs`

Add a test that raising `TransferComplete` on the underlying `TrayViewModel` refreshes `TotalPendingUploads`/`TotalPendingDownloads`:

```csharp
    [TestMethod]
    public async Task PendingCounts_RefreshAfterTransferComplete()
    {
        var (vm, trayVm, syncMock) = BuildVm();
        var contextId = Guid.CreateVersion7();

        await SeedAccountAsync(trayVm, syncMock, contextId, "Syncing");

        syncMock.Raise(
            i => i.SyncProgress += null,
            syncMock.Object,
            new SyncProgressEventArgs
            {
                ContextId = contextId,
                Status = new SyncStatus { State = SyncState.Syncing, PendingUploads = 2, PendingDownloads = 1 },
            });

        Assert.AreEqual(2, vm.TotalPendingUploads);
        Assert.AreEqual(1, vm.TotalPendingDownloads);

        syncMock.Raise(i => i.TransferComplete += null, syncMock.Object,
            new ContextTransferCompleteEventArgs { ContextId = contextId, FileName = "a.txt", Direction = "upload", TotalBytes = 100 });

        Assert.AreEqual(1, vm.TotalPendingUploads);
        Assert.AreEqual(1, vm.TotalPendingDownloads);
    }
```

---

## Verification

Run from the repository root (`/home/benk/Repos/DotNetCloud`):

```bash
# Build (code style is enforced as errors via Directory.Build.props)
dotnet build

# Targeted tests
dotnet test tests/DotNetCloud.Client.Core.Tests/
dotnet test tests/DotNetCloud.Client.SyncTray.Tests/
```

Manual verification (if a running server + sync folder are available):

1. Trigger a download of a large file in SyncTray.
2. Confirm the transfer card's byte label shows a real total (e.g. `"12.0 MB / 100.0 MB"`), and — for any file whose size can't be resolved — shows `"… / unknown"`.
3. Confirm the `↑ pending` / `↓ pending` counts decrease by one each time a file finishes transferring, and reach `0` when the pass completes.

---

## Edge Cases / Notes

- **Completed items** still show the real size: `MarkComplete(totalBytes)` receives the actual transferred byte count from `FileTransferCompleteEventArgs`, so `BytesLabel` is correct after completion.
- **Unknown size path:** the direct-download fallback (empty manifest or chunk 404) and the gRPC streaming upload path report little or no in-flight progress. The `"unknown"` label handles the display gracefully; no progress events are fabricated.
- **Symlink downloads** (materialized without content) do not raise `TransferComplete`, so they do not decrement the remaining count. This is an accepted, rare edge case; if full accuracy is required later, refresh counts via `GetStatusAsync` after completion instead of client-side decrement.
- **Existing tests remain green:** `SyncEngine` only sets `PendingUploads/PendingDownloads` when `totalPending > 0`; the mocked tests return an empty `PendingOperationCount`, so the new fields stay `0`.
