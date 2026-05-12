using DotNetCloud.Client.Core.LocalState;
using DotNetCloud.Client.Core.Sync;
using DotNetCloud.Client.Core.Transfer;
using DotNetCloud.Client.Core.VirtualFiles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace DotNetCloud.Client.Core.Tests.VirtualFiles;

[TestClass]
public sealed class VirtualFileSyncEngineTests
{
    private Mock<ISyncEngine> _innerMock = null!;
    private Mock<IVirtualFileProvider> _vfsMock = null!;
    private VirtualFileSettings _settings = null!;
    private VirtualFileSyncEngine _engine = null!;
    private SyncContext _context = null!;

    [TestInitialize]
    public void Initialize()
    {
        _innerMock = new Mock<ISyncEngine>();
        _vfsMock = new Mock<IVirtualFileProvider>();
        _settings = new VirtualFileSettings();

        _context = new SyncContext
        {
            Id = Guid.NewGuid(),
            ServerBaseUrl = "https://cloud.example.com",
            UserId = Guid.NewGuid(),
            LocalFolderPath = Path.Combine(Path.GetTempPath(), "vfs-test"),
            StateDatabasePath = Path.Combine(Path.GetTempPath(), "vfs-test", "state.db"),
            AccountKey = "test-account",
        };

        _engine = new VirtualFileSyncEngine(
            _innerMock.Object,
            _vfsMock.Object,
            _settings,
            NullLogger<VirtualFileSyncEngine>.Instance);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _engine.DisposeAsync();
    }

    // ── StartAsync ─────────────────────────────────────────────────────

    [TestMethod]
    public async Task StartAsync_FilesOnDemand_InitializesProvider()
    {
        _settings.StorageMode = VirtualFileStorageMode.FilesOnDemand;

        await _engine.StartAsync(_context);

        _vfsMock.Verify(v => v.InitializeAsync(_context, It.IsAny<CancellationToken>()), Times.Once);
        _innerMock.Verify(e => e.StartAsync(_context, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task StartAsync_DownloadAll_DoesNotInitializeProvider()
    {
        _settings.StorageMode = VirtualFileStorageMode.DownloadAll;

        await _engine.StartAsync(_context);

        _vfsMock.Verify(v => v.InitializeAsync(It.IsAny<SyncContext>(), It.IsAny<CancellationToken>()), Times.Never);
        _innerMock.Verify(e => e.StartAsync(_context, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── SyncAsync ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task SyncAsync_FilesOnDemand_DelegatesToInner()
    {
        _settings.StorageMode = VirtualFileStorageMode.FilesOnDemand;

        await _engine.SyncAsync(_context);

        _innerMock.Verify(e => e.SyncAsync(_context, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SyncAsync_DownloadAll_DelegatesToInner()
    {
        _settings.StorageMode = VirtualFileStorageMode.DownloadAll;

        await _engine.SyncAsync(_context);

        _innerMock.Verify(e => e.SyncAsync(_context, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── StopAsync ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task StopAsync_ShutsDownProviderAndStopsInner()
    {
        await _engine.StopAsync();

        _vfsMock.Verify(v => v.ShutdownAsync(It.IsAny<CancellationToken>()), Times.Once);
        _innerMock.Verify(e => e.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── SwitchModeAsync ────────────────────────────────────────────────

    [TestMethod]
    public async Task SwitchModeAsync_SameMode_DoesNothing()
    {
        _settings.StorageMode = VirtualFileStorageMode.DownloadAll;

        await _engine.SwitchModeAsync(_context, VirtualFileStorageMode.DownloadAll);

        Assert.AreEqual(VirtualFileStorageMode.DownloadAll, _settings.StorageMode);
    }

    [TestMethod]
    public async Task SwitchModeAsync_ToFilesOnDemand_UpdatesSetting()
    {
        _settings.StorageMode = VirtualFileStorageMode.DownloadAll;

        await _engine.SwitchModeAsync(_context, VirtualFileStorageMode.FilesOnDemand);

        Assert.AreEqual(VirtualFileStorageMode.FilesOnDemand, _settings.StorageMode);
    }

    [TestMethod]
    public async Task SwitchModeAsync_ToDownloadAll_UpdatesSetting()
    {
        _settings.StorageMode = VirtualFileStorageMode.FilesOnDemand;

        await _engine.SwitchModeAsync(_context, VirtualFileStorageMode.DownloadAll);

        Assert.AreEqual(VirtualFileStorageMode.DownloadAll, _settings.StorageMode);
    }

    // ── Event forwarding ───────────────────────────────────────────────

    [TestMethod]
    public void StatusChanged_ForwardsFromInner()
    {
        var raised = false;
        _engine.StatusChanged += (_, _) => raised = true;

        _innerMock.Raise(e => e.StatusChanged += null,
            _innerMock.Object,
            new SyncStatusChangedEventArgs
            {
                Status = new SyncStatus { State = SyncState.Syncing },
                Context = _context,
            });

        Assert.IsTrue(raised);
    }

    [TestMethod]
    public void FileTransferProgress_ForwardsFromInner()
    {
        var raised = false;
        _engine.FileTransferProgress += (_, _) => raised = true;

        _innerMock.Raise(e => e.FileTransferProgress += null,
            _innerMock.Object,
            new FileTransferProgressEventArgs
            {
                FileName = "test.txt",
                Direction = "download",
                Progress = new TransferProgress { BytesTransferred = 0, TotalBytes = 100 },
            });

        Assert.IsTrue(raised);
    }

    [TestMethod]
    public void FileTransferComplete_ForwardsFromInner()
    {
        var raised = false;
        _engine.FileTransferComplete += (_, _) => raised = true;

        _innerMock.Raise(e => e.FileTransferComplete += null,
            _innerMock.Object,
            new FileTransferCompleteEventArgs
            {
                FileName = "test.txt",
                Direction = "download",
                TotalBytes = 100,
                TotalChunks = 1,
            });

        Assert.IsTrue(raised);
    }

    // ── Pause / Resume / GetStatus ─────────────────────────────────────

    [TestMethod]
    public async Task PauseAsync_DelegatesToInner()
    {
        await _engine.PauseAsync(_context);
        _innerMock.Verify(e => e.PauseAsync(_context, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ResumeAsync_DelegatesToInner()
    {
        await _engine.ResumeAsync(_context);
        _innerMock.Verify(e => e.ResumeAsync(_context, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetStatusAsync_DelegatesToInner()
    {
        var expected = new SyncStatus { State = SyncState.Idle };
        _innerMock.Setup(e => e.GetStatusAsync(_context, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var actual = await _engine.GetStatusAsync(_context);

        Assert.AreEqual(expected.State, actual.State);
    }

    // ── InnerEngine / VirtualFileProvider access ───────────────────────

    [TestMethod]
    public void InnerEngine_ReturnsWrappedEngine()
    {
        Assert.AreSame(_innerMock.Object, _engine.InnerEngine);
    }

    [TestMethod]
    public void VirtualFileProvider_ReturnsWrappedProvider()
    {
        Assert.AreSame(_vfsMock.Object, _engine.VirtualFileProvider);
    }
}
