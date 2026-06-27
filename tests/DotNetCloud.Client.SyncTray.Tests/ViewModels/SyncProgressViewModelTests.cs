using DotNetCloud.Client.Core;
using DotNetCloud.Client.Core.LocalState;
using DotNetCloud.Client.Core.Sync;
using DotNetCloud.Client.Core.VirtualFiles;
using DotNetCloud.Client.SyncTray.Notifications;
using DotNetCloud.Client.SyncTray.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Client.SyncTray.Tests.ViewModels;

[TestClass]
public sealed class SyncProgressViewModelTests
{
    // ── Default state ─────────────────────────────────────────────────────

    [TestMethod]
    public void HasActiveTransfers_WhenNoTransfers_ReturnsFalse()
    {
        var (vm, _, _) = BuildVm();
        Assert.IsFalse(vm.HasActiveTransfers);
    }

    [TestMethod]
    public void SyncSummary_WhenNoTransfersAndNotSyncing_ReturnsUpToDate()
    {
        var (vm, _, _) = BuildVm();
        Assert.AreEqual("Up to date", vm.SyncSummary);
    }

    [TestMethod]
    public void HasPendingItems_WhenNoAccounts_ReturnsFalse()
    {
        var (vm, _, _) = BuildVm();
        Assert.IsFalse(vm.HasPendingItems);
    }

    // ── Active transfers ──────────────────────────────────────────────────

    [TestMethod]
    public async Task HasActiveTransfers_WhenTransferInProgress_ReturnsTrue()
    {
        var (vm, trayVm, syncMock) = BuildVm();
        var contextId = Guid.CreateVersion7();

        await SeedAccountAsync(trayVm, syncMock, contextId, "Syncing");

        syncMock.Raise(
            i => i.TransferProgress += null,
            syncMock.Object,
            new ContextTransferProgressEventArgs
            {
                ContextId = contextId,
                FileName = "test.txt",
                Direction = "upload",
                BytesTransferred = 512,
                TotalBytes = 1024,
                ChunksTransferred = 1,
                TotalChunks = 2,
                PercentComplete = 50,
            });

        Assert.IsTrue(vm.HasActiveTransfers);
    }

    [TestMethod]
    public async Task SyncSummary_SingleFileTransfer_ReturnsSingular()
    {
        var (vm, trayVm, syncMock) = BuildVm();
        var contextId = Guid.CreateVersion7();

        await SeedAccountAsync(trayVm, syncMock, contextId, "Syncing");

        syncMock.Raise(
            i => i.TransferProgress += null,
            syncMock.Object,
            new ContextTransferProgressEventArgs
            {
                ContextId = contextId,
                FileName = "photo.jpg",
                Direction = "download",
                BytesTransferred = 100,
                TotalBytes = 5000,
                ChunksTransferred = 1,
                TotalChunks = 10,
                PercentComplete = 2,
            });

        Assert.AreEqual("1 file syncing", vm.SyncSummary);
    }

    [TestMethod]
    public async Task SyncSummary_MultipleFileTransfers_ReturnsPlural()
    {
        var (vm, trayVm, syncMock) = BuildVm();
        var contextId = Guid.CreateVersion7();

        await SeedAccountAsync(trayVm, syncMock, contextId, "Syncing");

        syncMock.Raise(
            i => i.TransferProgress += null,
            syncMock.Object,
            new ContextTransferProgressEventArgs
            {
                ContextId = contextId,
                FileName = "a.txt",
                Direction = "upload",
                BytesTransferred = 100,
                TotalBytes = 1000,
                ChunksTransferred = 1,
                TotalChunks = 10,
                PercentComplete = 10,
            });

        syncMock.Raise(
            i => i.TransferProgress += null,
            syncMock.Object,
            new ContextTransferProgressEventArgs
            {
                ContextId = contextId,
                FileName = "b.txt",
                Direction = "download",
                BytesTransferred = 200,
                TotalBytes = 2000,
                ChunksTransferred = 2,
                TotalChunks = 10,
                PercentComplete = 10,
            });

        Assert.AreEqual("2 files syncing", vm.SyncSummary);
    }

    [TestMethod]
    public async Task SyncSummary_AfterTransferCompletes_ExcludesCompletedFromCount()
    {
        var (vm, trayVm, syncMock) = BuildVm();
        var contextId = Guid.CreateVersion7();

        await SeedAccountAsync(trayVm, syncMock, contextId, "Syncing");

        // Start two transfers.
        syncMock.Raise(
            i => i.TransferProgress += null,
            syncMock.Object,
            new ContextTransferProgressEventArgs
            {
                ContextId = contextId,
                FileName = "a.txt",
                Direction = "upload",
                BytesTransferred = 100,
                TotalBytes = 1000,
                ChunksTransferred = 1,
                TotalChunks = 10,
                PercentComplete = 10,
            });

        syncMock.Raise(
            i => i.TransferProgress += null,
            syncMock.Object,
            new ContextTransferProgressEventArgs
            {
                ContextId = contextId,
                FileName = "b.txt",
                Direction = "download",
                BytesTransferred = 200,
                TotalBytes = 2000,
                ChunksTransferred = 2,
                TotalChunks = 10,
                PercentComplete = 10,
            });

        // Complete one of them.
        syncMock.Raise(
            i => i.TransferComplete += null,
            syncMock.Object,
            new ContextTransferCompleteEventArgs
            {
                ContextId = contextId,
                FileName = "a.txt",
                Direction = "upload",
                TotalBytes = 1000,
            });

        Assert.AreEqual("1 file syncing", vm.SyncSummary);
    }

    // ── Pending counts ────────────────────────────────────────────────────

    [TestMethod]
    public async Task PendingCounts_ReflectAccountStatus()
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
                Status = new SyncStatus
                {
                    State = SyncState.Syncing,
                    PendingUploads = 5,
                    PendingDownloads = 3,
                },
            });

        Assert.AreEqual(5, vm.TotalPendingUploads);
        Assert.AreEqual(3, vm.TotalPendingDownloads);
        Assert.IsTrue(vm.HasPendingItems);
    }

    [TestMethod]
    public async Task PendingCounts_AggregateAcrossMultipleAccounts()
    {
        var (vm, trayVm, syncMock) = BuildVm();
        var id1 = Guid.CreateVersion7();
        var id2 = Guid.CreateVersion7();

        await SeedAccountAsync(trayVm, syncMock, id1, "Syncing");
        await SeedAccountAsync(trayVm, syncMock, id2, "Syncing");

        syncMock.Raise(
            i => i.SyncProgress += null,
            syncMock.Object,
            new SyncProgressEventArgs
            {
                ContextId = id1,
                Status = new SyncStatus { State = SyncState.Syncing, PendingUploads = 2, PendingDownloads = 1 },
            });

        syncMock.Raise(
            i => i.SyncProgress += null,
            syncMock.Object,
            new SyncProgressEventArgs
            {
                ContextId = id2,
                Status = new SyncStatus { State = SyncState.Syncing, PendingUploads = 3, PendingDownloads = 4 },
            });

        Assert.AreEqual(5, vm.TotalPendingUploads);
        Assert.AreEqual(5, vm.TotalPendingDownloads);
    }

    // ── Property change notifications ─────────────────────────────────────

    [TestMethod]
    public async Task PropertyChanged_RaisedWhenTransferAdded()
    {
        var (vm, trayVm, syncMock) = BuildVm();
        var contextId = Guid.CreateVersion7();

        await SeedAccountAsync(trayVm, syncMock, contextId, "Syncing");

        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null)
                changedProperties.Add(e.PropertyName);
        };

        syncMock.Raise(
            i => i.TransferProgress += null,
            syncMock.Object,
            new ContextTransferProgressEventArgs
            {
                ContextId = contextId,
                FileName = "file.txt",
                Direction = "upload",
                BytesTransferred = 0,
                TotalBytes = 1000,
                ChunksTransferred = 0,
                TotalChunks = 10,
                PercentComplete = 0,
            });

        CollectionAssert.Contains(changedProperties, nameof(SyncProgressViewModel.HasActiveTransfers));
        CollectionAssert.Contains(changedProperties, nameof(SyncProgressViewModel.SyncSummary));
    }

    // ── StatusMessage states ──────────────────────────────────────────────

    [TestMethod]
    public async Task StatusMessage_WhenIdle_ReturnsUpToDate()
    {
        var (vm, trayVm, syncMock) = BuildVm();
        var contextId = Guid.CreateVersion7();
        await SeedAccountAsync(trayVm, syncMock, contextId, "Idle");
        Assert.AreEqual("Everything is up to date.", vm.StatusMessage);
    }

    [TestMethod]
    public async Task StatusGlyph_WhenIdle_ReturnsCheckmark()
    {
        var (vm, trayVm, syncMock) = BuildVm();
        var contextId = Guid.CreateVersion7();
        await SeedAccountAsync(trayVm, syncMock, contextId, "Idle");
        Assert.AreEqual("✓", vm.StatusGlyph);
    }

    [TestMethod]
    public async Task StatusSubMessage_WhenIdleNoLastSynced_ReturnsEmpty()
    {
        var (vm, trayVm, syncMock) = BuildVm();
        var contextId = Guid.CreateVersion7();
        await SeedAccountAsync(trayVm, syncMock, contextId, "Idle");
        Assert.AreEqual("", vm.StatusSubMessage);
    }

    [TestMethod]
    public async Task StatusMessage_WhenSyncingNoPhaseLabel_ReturnsPreparing()
    {
        var (vm, trayVm, syncMock) = BuildVm();
        var contextId = Guid.CreateVersion7();

        // Set OverallState to Syncing via SeedAccount with "Syncing" state.
        await SeedAccountAsync(trayVm, syncMock, contextId, "Syncing");

        Assert.AreEqual("Preparing to sync…", vm.StatusMessage);
        Assert.AreEqual("⟳", vm.StatusGlyph);
        Assert.AreEqual("Scanning for changes…", vm.StatusSubMessage);
    }

    [TestMethod]
    public async Task StatusMessage_WhenSyncingWithPhaseLabel_ShowsPhase()
    {
        var (vm, trayVm, syncMock) = BuildVm();
        var contextId = Guid.CreateVersion7();

        await SeedAccountAsync(trayVm, syncMock, contextId, "Syncing");

        // Simulate a SyncStatus update with a phase label.
        vm.OnSyncStatusUpdated(new SyncStatus
        {
            State = SyncState.Syncing,
            FullSyncPhaseLabel = "Fetching server file list…",
        });

        Assert.AreEqual("Fetching server file list…", vm.StatusMessage);
    }

    [TestMethod]
    public async Task StatusMessage_WhenError_ReturnsErrorText()
    {
        var (vm, trayVm, syncMock) = BuildVm();
        var contextId = Guid.CreateVersion7();

        await SeedAccountAsync(trayVm, syncMock, contextId, "Syncing");

        // Trigger sync error through the tray VM's event -> the progress VM subscribes.
        syncMock.Raise(i => i.SyncError += null, syncMock.Object,
            new SyncErrorEventArgs { ContextId = contextId, ErrorMessage = "Connection failed" });

        Assert.AreEqual("Sync error", vm.StatusMessage);
        Assert.AreEqual("✗", vm.StatusGlyph);
    }

    [TestMethod]
    public async Task StatusMessage_WhenErrorAndErrorMessage_BannerVisible()
    {
        var (vm, trayVm, syncMock) = BuildVm();
        var contextId = Guid.CreateVersion7();

        await SeedAccountAsync(trayVm, syncMock, contextId, "Syncing");

        syncMock.Raise(i => i.SyncError += null, syncMock.Object,
            new SyncErrorEventArgs { ContextId = contextId, ErrorMessage = "Access denied" });

        Assert.IsTrue(vm.IsBannerVisible);
        Assert.IsTrue(vm.HasErrors);
        Assert.IsFalse(vm.HasConflicts);
    }

    [TestMethod]
    public async Task StatusMessage_WhenConflict_ReturnsConflictText()
    {
        var (vm, trayVm, syncMock) = BuildVm();
        var contextId = Guid.CreateVersion7();
        await SeedAccountAsync(trayVm, syncMock, contextId, "Idle");

        // Simulate a conflict being detected — tray VM increases ConflictCount.
        syncMock.Raise(i => i.ConflictDetected += null, syncMock.Object,
            new SyncConflictDetectedEventArgs { ContextId = contextId, OriginalPath = "/test/file.txt", ConflictCopyPath = "/test/file.conflict.txt" });

        Assert.AreEqual("Conflicts need attention", vm.StatusMessage);
        Assert.AreEqual("⚠", vm.StatusGlyph);
    }

    [TestMethod]
    public async Task StatusMessage_WhenPaused_ReturnsPausedText()
    {
        var (vm, trayVm, syncMock) = BuildVm();
        var contextId = Guid.CreateVersion7();

        await SeedAccountAsync(trayVm, syncMock, contextId, "Paused");

        Assert.AreEqual("Sync paused", vm.StatusMessage);
        Assert.AreEqual("⏸", vm.StatusGlyph);
    }

    [TestMethod]
    public async Task PauseResumeText_WhenPaused_ReturnsResume()
    {
        var (vm, trayVm, syncMock) = BuildVm();
        var contextId = Guid.CreateVersion7();

        await SeedAccountAsync(trayVm, syncMock, contextId, "Paused");

        Assert.AreEqual("Resume", vm.PauseResumeText);
    }

    // ── Footer tests ──────────────────────────────────────────────────────

    [TestMethod]
    public void FooterText_WhenSessionBytesPresent_IncludesTransferInfo()
    {
        var (vm, _, _) = BuildVm();

        vm.OnSyncStatusUpdated(new SyncStatus
        {
            State = SyncState.Syncing,
            BytesUploaded = 1_048_576,   // 1 MB
            BytesDownloaded = 2_097_152, // 2 MB
        });

        var text = vm.FooterText;
        StringAssert.Contains(text, "↑");
        StringAssert.Contains(text, "↓");
        StringAssert.Contains(text, "MB");
    }

    [TestMethod]
    public void FooterText_WhenNoData_ReturnsEmpty()
    {
        var (vm, _, _) = BuildVm();
        Assert.AreEqual("", vm.FooterText);
    }

    // ── OnSyncStatusUpdated ───────────────────────────────────────────────

    [TestMethod]
    public async Task SyncSummary_AfterSyncStatusUpdatedWithPhaseLabel_ReflectsPhase()
    {
        var (vm, trayVm, syncMock) = BuildVm();
        var contextId = Guid.CreateVersion7();
        await SeedAccountAsync(trayVm, syncMock, contextId, "Syncing");

        vm.OnSyncStatusUpdated(new SyncStatus
        {
            State = SyncState.Syncing,
            FullSyncPhaseLabel = "Scanning local changes…",
        });

        // SyncSummary should include the phase label since _fullSyncPhaseLabel was set.
        StringAssert.Contains(vm.SyncSummary, "Scanning");
    }

    // ── Dispose ───────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Dispose_UnsubscribesFromEvents()
    {
        var (vm, trayVm, syncMock) = BuildVm();
        var contextId = Guid.CreateVersion7();

        await SeedAccountAsync(trayVm, syncMock, contextId, "Syncing");

        vm.Dispose();

        // After dispose, adding a transfer should not raise PropertyChanged on the
        // SyncProgressViewModel (it unsubscribed from collection changes).
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null)
                changedProperties.Add(e.PropertyName);
        };

        syncMock.Raise(
            i => i.TransferProgress += null,
            syncMock.Object,
            new ContextTransferProgressEventArgs
            {
                ContextId = contextId,
                FileName = "after-dispose.txt",
                Direction = "download",
                BytesTransferred = 0,
                TotalBytes = 500,
                ChunksTransferred = 0,
                TotalChunks = 5,
                PercentComplete = 0,
            });

        // SyncProgressViewModel should NOT have fired its own PropertyChanged.
        Assert.AreEqual(0, changedProperties.Count,
            "Disposed SyncProgressViewModel should not raise PropertyChanged.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static (SyncProgressViewModel vm, TrayViewModel trayVm, Mock<ISyncContextManager> syncMock) BuildVm()
    {
        var syncMock = new Mock<ISyncContextManager>();
        syncMock.Setup(s => s.GetContextsAsync()).ReturnsAsync(new List<SyncContextRegistration>());

        var chatMock = new Mock<IChatSignalRClient>();
        chatMock.Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var notifMock = new Mock<INotificationService>();
        var vfsSettings = new VirtualFileSettings();
        var stateDbMock = new Mock<ILocalStateDb>();
        var trayVm = new TrayViewModel(
            syncMock.Object, chatMock.Object, notifMock.Object,
            vfsSettings, stateDbMock.Object,
            NullLogger<TrayViewModel>.Instance);

        var vm = new SyncProgressViewModel(trayVm);
        return (vm, trayVm, syncMock);
    }

    private static async Task SeedAccountAsync(
        TrayViewModel vm, Mock<ISyncContextManager> syncMock, Guid contextId, string state)
    {
        var existing = syncMock.Object.GetContextsAsync().GetAwaiter().GetResult();

        var contexts = new List<SyncContextRegistration>(existing)
        {
            new SyncContextRegistration
            {
                Id = contextId,
                DisplayName = $"TestAccount-{contextId}",
                ServerBaseUrl = "https://cloud.example.com",
                LocalFolderPath = "/sync",
                UserId = Guid.CreateVersion7(),
                AccountKey = $"test-{contextId}",
                OsUserName = "testuser",
                DataDirectory = "/tmp/data",
            },
        };

        syncMock
            .Setup(s => s.GetContextsAsync())
            .ReturnsAsync(contexts);

        var syncState = Enum.TryParse<SyncState>(state, ignoreCase: true, out var parsed)
            ? parsed
            : SyncState.Idle;

        syncMock
            .Setup(s => s.GetStatusAsync(contextId))
            .ReturnsAsync(new SyncStatus { State = syncState });

        await vm.RefreshAccountsAsync();
    }
}
