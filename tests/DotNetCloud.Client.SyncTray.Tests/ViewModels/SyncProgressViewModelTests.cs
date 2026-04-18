using DotNetCloud.Client.Core;
using DotNetCloud.Client.Core.Sync;
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
        var contextId = Guid.NewGuid();

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
        var contextId = Guid.NewGuid();

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
        var contextId = Guid.NewGuid();

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
        var contextId = Guid.NewGuid();

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
        var contextId = Guid.NewGuid();

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
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

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
        var contextId = Guid.NewGuid();

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

    // ── Dispose ───────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Dispose_UnsubscribesFromEvents()
    {
        var (vm, trayVm, syncMock) = BuildVm();
        var contextId = Guid.NewGuid();

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
        var trayVm = new TrayViewModel(syncMock.Object, chatMock.Object, notifMock.Object, NullLogger<TrayViewModel>.Instance);

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
                UserId = Guid.NewGuid(),
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
