using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.Auth;
using DotNetCloud.Client.Core.Conflict;
using DotNetCloud.Client.Core.LocalState;
using DotNetCloud.Client.Core.Platform;
using DotNetCloud.Client.Core.SelectiveSync;
using DotNetCloud.Client.Core.Sync;
using DotNetCloud.Client.Core.Transfer;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace DotNetCloud.Client.Core.Tests.Sync;

[TestClass]
public class SyncEngineTests
{
    private string _tempDir = null!;
    private SyncContext _context = null!;
    private Mock<IDotNetCloudApiClient> _apiMock = null!;
    private Mock<ILocalStateDb> _stateDbMock = null!;
    private Mock<ILockedFileReader> _lockedFileReaderMock = null!;
    private SyncEngine _engine = null!;

    [TestInitialize]
    public void Initialize()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);

        _context = new SyncContext
        {
            Id = Guid.NewGuid(),
            ServerBaseUrl = "https://cloud.example.com",
            UserId = Guid.NewGuid(),
            LocalFolderPath = _tempDir,
            StateDatabasePath = Path.Combine(_tempDir, "state.db"),
            AccountKey = "test-account",
        };

        _apiMock = new Mock<IDotNetCloudApiClient>();
        _apiMock.SetupProperty(a => a.AccessToken);

        _stateDbMock = new Mock<ILocalStateDb>();
        _stateDbMock.Setup(db => db.InitializeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _stateDbMock.Setup(db => db.GetCheckpointAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);
        _stateDbMock.Setup(db => db.GetPendingOperationCountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PendingOperationCount());
        _stateDbMock.Setup(db => db.GetPendingOperationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _stateDbMock.Setup(db => db.UpdateCheckpointAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _apiMock.Setup(a => a.GetChangesSinceAsync(It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _apiMock.Setup(a => a.GetFolderTreeAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncTreeNodeResponse { NodeId = Guid.Empty, Name = "/", NodeType = "Folder" });

        var tokenStoreMock = new Mock<ITokenStore>();
        tokenStoreMock.Setup(ts => ts.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TokenInfo { AccessToken = "test-token", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) });

        _lockedFileReaderMock = new Mock<ILockedFileReader>();
        _lockedFileReaderMock
            .Setup(r => r.TryReadLockedFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null);

        _engine = new SyncEngine(
            _apiMock.Object,
            tokenStoreMock.Object,
            new Mock<IChunkedTransferClient>().Object,
            new Mock<IConflictResolver>().Object,
            _stateDbMock.Object,
            new SelectiveSyncConfig(),
            new DotNetCloud.Client.Core.SyncIgnore.SyncIgnoreParser(),
            _lockedFileReaderMock.Object,
            NullLogger<SyncEngine>.Instance);

        // Speed up Tier 2 retries so locked file tests don't take 6+ seconds.
        _engine.Tier2RetryDelay = TimeSpan.Zero;
    }

    [TestCleanup]
    public void Cleanup()
    {
        _engine.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── StartAsync ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task StartAsync_InitializesStateDb()
    {
        await _engine.StartAsync(_context);
        await _engine.StopAsync();

        _stateDbMock.Verify(db => db.InitializeAsync(_context.StateDatabasePath, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetStatusAsync ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetStatusAsync_InitialState_ReturnsIdle()
    {
        var status = await _engine.GetStatusAsync(_context);

        Assert.AreEqual(SyncState.Idle, status.State);
        Assert.AreEqual(0, status.PendingUploads);
        Assert.AreEqual(0, status.PendingDownloads);
    }

    // ── PauseAsync / ResumeAsync ────────────────────────────────────────────

    [TestMethod]
    public async Task PauseAsync_SetsPausedState()
    {
        await _engine.StartAsync(_context);
        await _engine.PauseAsync(_context);

        var status = await _engine.GetStatusAsync(_context);
        Assert.AreEqual(SyncState.Paused, status.State);
        await _engine.StopAsync();
    }

    [TestMethod]
    public async Task ResumeAsync_AfterPause_SetsIdleState()
    {
        await _engine.StartAsync(_context);
        await _engine.PauseAsync(_context);
        await _engine.ResumeAsync(_context);

        // Brief delay for async sync to settle
        await Task.Delay(50);

        var status = await _engine.GetStatusAsync(_context);
        Assert.IsTrue(status.State is SyncState.Idle or SyncState.Syncing);
        await _engine.StopAsync();
    }

    // ── SyncAsync ───────────────────────────────────────────────────────────

    [TestMethod]
    public async Task SyncAsync_NoChanges_UpdatesCheckpoint()
    {
        await _engine.StartAsync(_context);
        await _engine.SyncAsync(_context);
        await _engine.StopAsync();

        _stateDbMock.Verify(db => db.UpdateCheckpointAsync(
            _context.StateDatabasePath,
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task SyncAsync_WhenPaused_DoesNotSync()
    {
        await _engine.StartAsync(_context);
        await _engine.PauseAsync(_context);
        await _engine.SyncAsync(_context);
        await _engine.StopAsync();

        _stateDbMock.Verify(db => db.UpdateCheckpointAsync(
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── StatusChanged event ─────────────────────────────────────────────────

    [TestMethod]
    public async Task StatusChanged_FiredOnStateTransition()
    {
        SyncStatusChangedEventArgs? eventArgs = null;
        _engine.StatusChanged += (_, args) => eventArgs = args;

        await _engine.StartAsync(_context);

        Assert.IsNotNull(eventArgs);
        Assert.AreEqual(_context.Id, eventArgs.Context.Id);
        await _engine.StopAsync();
    }

    // ── Locked File Handling (Task 3.3) ────────────────────────────────────

    [TestMethod]
    public async Task SyncAsync_FileOpenedWithReadWriteShare_UploadsSuccessfully()
    {
        // Arrange: create a temp file and hold it open with FileShare.ReadWrite | FileShare.Delete
        // (simulates an app like Word that allows concurrent readers while the file is open).
        var filePath = Path.Combine(_tempDir, "shared-file.txt");
        File.WriteAllText(filePath, "hello world");

        var transferMock = new Mock<IChunkedTransferClient>();
        var expectedNodeId = Guid.NewGuid();
        transferMock
            .Setup(t => t.UploadAsync(
                It.IsAny<Guid?>(), filePath, It.IsAny<Stream>(), It.IsAny<IProgress<TransferProgress>?>(),
                It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(expectedNodeId);

        _stateDbMock.Setup(db => db.RemoveOperationAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _stateDbMock.Setup(db => db.UpsertFileRecordAsync(
                It.IsAny<string>(), It.IsAny<LocalFileRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var pendingOp = new PendingUpload { Id = 1, LocalPath = filePath, RetryCount = 0 };
        _stateDbMock
            .Setup(db => db.GetPendingOperationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pendingOp]);

        // Hold the file open with ReadWrite share (like Word or Excel would).
        using var holderStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite,
            FileShare.ReadWrite | FileShare.Delete);

        var engine = new SyncEngine(
            _apiMock.Object,
            new Mock<ITokenStore>().Object,
            transferMock.Object,
            new Mock<IConflictResolver>().Object,
            _stateDbMock.Object,
            new SelectiveSyncConfig(),
            new DotNetCloud.Client.Core.SyncIgnore.SyncIgnoreParser(),
            _lockedFileReaderMock.Object,
            NullLogger<SyncEngine>.Instance);
        engine.Tier2RetryDelay = TimeSpan.Zero;

        // Set up token mock used by RefreshAccessTokenAsync
        // (engine uses _api.AccessToken assignment, not token store, in this test)

        // Act
        await engine.StartAsync(_context);
        await engine.SyncAsync(_context);
        await engine.StopAsync();

        // Assert: upload was called (Tier 1 FileShare.ReadWrite succeeded)
        transferMock.Verify(
            t => t.UploadAsync(It.IsAny<Guid?>(), filePath, It.IsAny<Stream>(),
                It.IsAny<IProgress<TransferProgress>?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SyncAsync_LockedFileVssSucceeds_UploadsFromVssStream()
    {
        // Arrange: lock the file with FileShare.None so Tiers 1 and 2 fail,
        // then mock ILockedFileReader to return a stream (simulating VSS success).
        var filePath = Path.Combine(_tempDir, "vss-file.txt");
        var fileContent = "vss test content"u8.ToArray();
        File.WriteAllBytes(filePath, fileContent);

        var transferMock = new Mock<IChunkedTransferClient>();
        var expectedNodeId = Guid.NewGuid();
        transferMock
            .Setup(t => t.UploadAsync(
                It.IsAny<Guid?>(), filePath, It.IsAny<Stream>(), It.IsAny<IProgress<TransferProgress>?>(),
                It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(expectedNodeId);

        _stateDbMock.Setup(db => db.RemoveOperationAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _stateDbMock.Setup(db => db.UpsertFileRecordAsync(
                It.IsAny<string>(), It.IsAny<LocalFileRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var pendingOp = new PendingUpload { Id = 1, LocalPath = filePath, RetryCount = 0 };
        _stateDbMock
            .Setup(db => db.GetPendingOperationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pendingOp]);

        // VSS mock provides a stream with the file's bytes.
        _lockedFileReaderMock
            .Setup(r => r.TryReadLockedFileAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(fileContent));

        var engine = new SyncEngine(
            _apiMock.Object,
            new Mock<ITokenStore>().Object,
            transferMock.Object,
            new Mock<IConflictResolver>().Object,
            _stateDbMock.Object,
            new SelectiveSyncConfig(),
            new DotNetCloud.Client.Core.SyncIgnore.SyncIgnoreParser(),
            _lockedFileReaderMock.Object,
            NullLogger<SyncEngine>.Instance);
        engine.Tier2RetryDelay = TimeSpan.Zero;

        // Lock the file so Tiers 1 and 2 fail.
        using var lockStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        // Act
        await engine.StartAsync(_context);
        await engine.SyncAsync(_context);
        await engine.StopAsync();

        // Assert: upload called with the VSS stream (Tier 3 succeeded).
        transferMock.Verify(
            t => t.UploadAsync(It.IsAny<Guid?>(), filePath, It.IsAny<Stream>(),
                It.IsAny<IProgress<TransferProgress>?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SyncAsync_LockedFileAllTiersFail_DefersWithoutIncrementingRetryCount()
    {
        // Arrange: lock the file and have VSS also fail → expect Tier 4 (deferred).
        var filePath = Path.Combine(_tempDir, "deferred-file.txt");
        File.WriteAllText(filePath, "deferred content");

        _stateDbMock
            .Setup(db => db.UpdateOperationRetryAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _stateDbMock
            .Setup(db => db.GetFileRecordAsync(It.IsAny<string>(), filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LocalFileRecord?)null);

        const int originalRetryCount = 2;
        var pendingOp = new PendingUpload { Id = 42, LocalPath = filePath, RetryCount = originalRetryCount };
        _stateDbMock
            .Setup(db => db.GetPendingOperationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pendingOp]);

        // VSS also returns null → all tiers fail.
        _lockedFileReaderMock
            .Setup(r => r.TryReadLockedFileAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null);

        var engine = new SyncEngine(
            _apiMock.Object,
            new Mock<ITokenStore>().Object,
            new Mock<IChunkedTransferClient>().Object,
            new Mock<IConflictResolver>().Object,
            _stateDbMock.Object,
            new SelectiveSyncConfig(),
            new DotNetCloud.Client.Core.SyncIgnore.SyncIgnoreParser(),
            _lockedFileReaderMock.Object,
            NullLogger<SyncEngine>.Instance);
        engine.Tier2RetryDelay = TimeSpan.Zero;

        // Lock file with FileShare.None so Tiers 1 and 2 both fail.
        using var lockStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        // Act
        await engine.StartAsync(_context);
        await engine.SyncAsync(_context);
        await engine.StopAsync();

        // Assert: UpdateOperationRetryAsync called with the SAME RetryCount (not incremented).
        _stateDbMock.Verify(db => db.UpdateOperationRetryAsync(
            It.IsAny<string>(),
            42,
            originalRetryCount,    // RetryCount unchanged
            It.IsAny<DateTime?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Assert: NOT moved to permanent failure queue.
        _stateDbMock.Verify(db => db.MoveToFailedAsync(
            It.IsAny<string>(), It.IsAny<PendingOperationRecord>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task SyncAsync_AfterSyncPass_ReleasesLockedFileReaderSnapshot()
    {
        // Arrange: after each sync pass, SyncEngine must call ReleaseSnapshot
        // so that VSS shadow copies (held during the pass) are freed.
        await _engine.StartAsync(_context);

        // Act: run a sync pass with no pending operations.
        await _engine.SyncAsync(_context);

        await _engine.StopAsync();

        // Assert: ReleaseSnapshot was called at least once (from SyncAsync finally block).
        _lockedFileReaderMock.Verify(r => r.ReleaseSnapshot(), Times.AtLeastOnce);
    }
}
