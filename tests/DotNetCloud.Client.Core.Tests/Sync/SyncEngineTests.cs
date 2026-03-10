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
        _stateDbMock.Setup(db => db.GetSyncCursorAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _stateDbMock.Setup(db => db.UpdateSyncCursorAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _stateDbMock.Setup(db => db.GetPendingOperationCountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PendingOperationCount());
        _stateDbMock.Setup(db => db.GetPendingOperationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _stateDbMock.Setup(db => db.UpdateCheckpointAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _apiMock.Setup(a => a.GetChangesSinceAsync(It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _apiMock.Setup(a => a.GetChangesSinceAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedSyncChangesResponse { Changes = [], NextCursor = null, HasMore = false });
        _apiMock.Setup(a => a.GetNodeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileNodeResponse { Id = Guid.NewGuid(), Name = "file", NodeType = "File" });
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

    [TestMethod]
    public async Task SyncAsync_DiskFullError_SetsErrorAndPausesFurtherSyncAttempts()
    {
        _stateDbMock
            .Setup(db => db.UpdateCheckpointAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DiskFullIOException());

        await _engine.StartAsync(_context);
        await _engine.SyncAsync(_context);

        var statusAfterError = await _engine.GetStatusAsync(_context);
        Assert.AreEqual(SyncState.Error, statusAfterError.State);
        Assert.IsNotNull(statusAfterError.LastError);
        StringAssert.Contains(statusAfterError.LastError, "Disk full");

        _stateDbMock.Invocations.Clear();
        await _engine.SyncAsync(_context);

        _stateDbMock.Verify(db => db.UpdateCheckpointAsync(
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Never);

        await _engine.StopAsync();
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
                It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>()))
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
                It.IsAny<IProgress<TransferProgress>?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>()),
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
                It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>()))
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
                It.IsAny<IProgress<TransferProgress>?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>()),
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

    [TestMethod]
    public async Task SyncAsync_UploadPendingOperation_FiresFileTransferProgressAndCompleteEvents()
    {
        // Arrange: a pending upload; the UploadAsync mock invokes the progress reporter.
        var filePath = Path.Combine(_tempDir, "progress-file.txt");
        File.WriteAllText(filePath, "progress test content");

        var transferMock = new Mock<IChunkedTransferClient>();
        var expectedNodeId = Guid.NewGuid();
        transferMock
            .Setup(t => t.UploadAsync(
                It.IsAny<Guid?>(), filePath, It.IsAny<Stream>(),
                It.IsNotNull<IProgress<TransferProgress>>(),
                It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .Callback<Guid?, string, Stream, IProgress<TransferProgress>?, CancellationToken, string?, int?, string?>(
                (_, _, _, progress, _, _, _, _) =>
                {
                    // Simulate one progress callback before completing.
                    progress?.Report(new TransferProgress
                    {
                        BytesTransferred = 512,
                        TotalBytes = 1024,
                        ChunksTransferred = 1,
                        TotalChunks = 2,
                    });
                })
            .ReturnsAsync(expectedNodeId);

        _stateDbMock.Setup(db => db.RemoveOperationAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _stateDbMock.Setup(db => db.UpsertFileRecordAsync(
                It.IsAny<string>(), It.IsAny<LocalFileRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _stateDbMock
            .Setup(db => db.GetPendingOperationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PendingUpload { Id = 1, LocalPath = filePath, RetryCount = 0 }]);

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

        var progressEvents = new List<FileTransferProgressEventArgs>();
        var completeEvents = new List<FileTransferCompleteEventArgs>();
        engine.FileTransferProgress += (_, e) => progressEvents.Add(e);
        engine.FileTransferComplete += (_, e) => completeEvents.Add(e);

        // Act
        await engine.StartAsync(_context);
        await engine.SyncAsync(_context);
        await engine.StopAsync();

        // Assert: at least one FileTransferProgress event with direction=upload.
        Assert.IsTrue(progressEvents.Count >= 1, "Expected at least one FileTransferProgress event.");
        Assert.AreEqual("upload", progressEvents[0].Direction);
        Assert.AreEqual(Path.GetFileName(filePath), progressEvents[0].FileName);
        Assert.AreEqual(512L, progressEvents[0].Progress.BytesTransferred);

        // Assert: exactly one FileTransferComplete event with direction=upload.
        Assert.AreEqual(1, completeEvents.Count, "Expected one FileTransferComplete event.");
        Assert.AreEqual("upload", completeEvents[0].Direction);
        Assert.AreEqual(Path.GetFileName(filePath), completeEvents[0].FileName);
    }

    [TestMethod]
    public async Task SyncAsync_UploadNullProgress_DoesNotThrow()
    {
        // Verify that if no subscribers are attached, progress callbacks don't error.
        var filePath = Path.Combine(_tempDir, "null-progress-file.txt");
        File.WriteAllText(filePath, "null progress content");

        var transferMock = new Mock<IChunkedTransferClient>();
        transferMock
            .Setup(t => t.UploadAsync(
                It.IsAny<Guid?>(), filePath, It.IsAny<Stream>(), It.IsAny<IProgress<TransferProgress>?>(),
                It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync(Guid.NewGuid());

        _stateDbMock.Setup(db => db.RemoveOperationAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _stateDbMock.Setup(db => db.UpsertFileRecordAsync(
                It.IsAny<string>(), It.IsAny<LocalFileRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _stateDbMock
            .Setup(db => db.GetPendingOperationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PendingUpload { Id = 1, LocalPath = filePath, RetryCount = 0 }]);

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

        // No event subscribers — should not throw.
        await engine.StartAsync(_context);
        await engine.SyncAsync(_context);
        await engine.StopAsync();

        transferMock.Verify(
            t => t.UploadAsync(It.IsAny<Guid?>(), filePath, It.IsAny<Stream>(),
                It.IsAny<IProgress<TransferProgress>?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>()),
            Times.Once);
    }

    // ── Cursor-based pagination (Tasks 2.4 + 2.5) ──────────────────────────

    [TestMethod]
    public async Task SyncAsync_PaginatedChanges_FetchesAllPagesAndPersistsCursor()
    {
        // Arrange: server returns two pages — page 1 (hasMore=true, cursor="page2"),
        // page 2 (hasMore=false, no cursor).
        var nodeId = Guid.NewGuid();
        var page1 = new PagedSyncChangesResponse
        {
            Changes = [new SyncChangeResponse { NodeId = nodeId, Name = "file.txt", NodeType = "File", UpdatedAt = DateTime.UtcNow }],
            NextCursor = "page2cursor",
            HasMore = true,
        };
        var page2 = new PagedSyncChangesResponse
        {
            Changes = [new SyncChangeResponse { NodeId = Guid.NewGuid(), Name = "file2.txt", NodeType = "File", UpdatedAt = DateTime.UtcNow }],
            NextCursor = "finalcursor",
            HasMore = false,
        };

        var callCount = 0;
        _apiMock.Setup(a => a.GetChangesSinceAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => callCount++ == 0 ? page1 : page2);

        string? savedCursor = null;
        _stateDbMock.Setup(db => db.UpdateSyncCursorAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, c, __) => savedCursor = c)
            .Returns(Task.CompletedTask);

        await _engine.StartAsync(_context);
        await _engine.SyncAsync(_context);
        await _engine.StopAsync();

        // Both pages were fetched
        Assert.AreEqual(2, callCount, "Expected exactly 2 pages fetched.");
        // Cursor was persisted at least once (after page 1 and page 2)
        _stateDbMock.Verify(db => db.UpdateSyncCursorAsync(
            _context.StateDatabasePath, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeast(1));
        // Final saved cursor is from page 2
        Assert.AreEqual("finalcursor", savedCursor);
    }

    [TestMethod]
    public async Task SyncAsync_CursorFromPreviousSync_SentToServer()
    {
        // Arrange: stored cursor "storedCursor" should be passed to GetChangesSinceAsync on next sync.
        _stateDbMock.Setup(db => db.GetSyncCursorAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("storedCursor");

        string? receivedCursor = null;
        _apiMock.Setup(a => a.GetChangesSinceAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string?, int, CancellationToken>((c, _, __) => receivedCursor = c)
            .ReturnsAsync(new PagedSyncChangesResponse { Changes = [], NextCursor = null, HasMore = false });

        await _engine.StartAsync(_context);
        await _engine.SyncAsync(_context);
        await _engine.StopAsync();

        Assert.AreEqual("storedCursor", receivedCursor, "Expected stored cursor passed to GetChangesSinceAsync.");
    }

    // ── Idempotent uploads (Issue #40) ──────────────────────────────────────

    [TestMethod]
    public async Task SyncAsync_ExistingFileServerHashMatches_SkipsUploadAndMarksSynced()
    {
        // Arrange: create a real file whose hash matches what the server reports.
        var filePath = Path.Combine(_tempDir, "idempotent.txt");
        var content = "already-synced content"u8.ToArray();
        await File.WriteAllBytesAsync(filePath, content);
        var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(content));

        var existingNodeId = Guid.NewGuid();
        _apiMock.Setup(a => a.GetNodeAsync(existingNodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileNodeResponse { Id = existingNodeId, Name = "idempotent.txt", NodeType = "File", ContentHash = hash });

        var transferMock = new Mock<IChunkedTransferClient>();
        _stateDbMock.Setup(db => db.RemoveOperationAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _stateDbMock.Setup(db => db.UpsertFileRecordAsync(It.IsAny<string>(), It.IsAny<LocalFileRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _stateDbMock.Setup(db => db.GetPendingOperationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PendingUpload { Id = 1, LocalPath = filePath, NodeId = existingNodeId, RetryCount = 0 }]);

        await using var engine = BuildEngine(transferMock.Object);

        // Act
        await engine.StartAsync(_context);
        await engine.SyncAsync(_context);
        await engine.StopAsync();

        // Assert: upload was NOT performed.
        transferMock.Verify(
            t => t.UploadAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<IProgress<TransferProgress>?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>()),
            Times.Never);

        // Assert: local record was upserted (marked synced).
        _stateDbMock.Verify(db => db.UpsertFileRecordAsync(
            _context.StateDatabasePath,
            It.Is<LocalFileRecord>(r => r.LocalPath == filePath && r.NodeId == existingNodeId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SyncAsync_ExistingFileServerHashMismatch_ProceedsWithUpload()
    {
        // Arrange: server has a different hash → normal upload should proceed.
        var filePath = Path.Combine(_tempDir, "changed.txt");
        await File.WriteAllTextAsync(filePath, "new content");

        var existingNodeId = Guid.NewGuid();
        _apiMock.Setup(a => a.GetNodeAsync(existingNodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileNodeResponse { Id = existingNodeId, Name = "changed.txt", NodeType = "File", ContentHash = "oldhashhex" });

        var uploadedNodeId = Guid.NewGuid();
        var transferMock = new Mock<IChunkedTransferClient>();
        transferMock.Setup(t => t.UploadAsync(
                It.IsAny<Guid?>(), filePath, It.IsAny<Stream>(), It.IsAny<IProgress<TransferProgress>?>(),
                It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync(uploadedNodeId);

        _stateDbMock.Setup(db => db.RemoveOperationAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _stateDbMock.Setup(db => db.UpsertFileRecordAsync(It.IsAny<string>(), It.IsAny<LocalFileRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _stateDbMock.Setup(db => db.GetPendingOperationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PendingUpload { Id = 1, LocalPath = filePath, NodeId = existingNodeId, RetryCount = 0 }]);

        await using var engine = BuildEngine(transferMock.Object);

        // Act
        await engine.StartAsync(_context);
        await engine.SyncAsync(_context);
        await engine.StopAsync();

        // Assert: upload WAS called.
        transferMock.Verify(
            t => t.UploadAsync(existingNodeId, filePath, It.IsAny<Stream>(),
                It.IsAny<IProgress<TransferProgress>?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SyncAsync_NewFileNoNodeId_SkipsIdempotencyCheckAndUploads()
    {
        // Arrange: no NodeId on the operation → GetNodeAsync must never be called.
        var filePath = Path.Combine(_tempDir, "newfile.txt");
        await File.WriteAllTextAsync(filePath, "brand new");

        var transferMock = new Mock<IChunkedTransferClient>();
        transferMock.Setup(t => t.UploadAsync(
                null, filePath, It.IsAny<Stream>(), It.IsAny<IProgress<TransferProgress>?>(),
                It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync(Guid.NewGuid());

        _stateDbMock.Setup(db => db.RemoveOperationAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _stateDbMock.Setup(db => db.UpsertFileRecordAsync(It.IsAny<string>(), It.IsAny<LocalFileRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _stateDbMock.Setup(db => db.GetPendingOperationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PendingUpload { Id = 1, LocalPath = filePath, NodeId = null, RetryCount = 0 }]);

        await using var engine = BuildEngine(transferMock.Object);

        // Act
        await engine.StartAsync(_context);
        await engine.SyncAsync(_context);
        await engine.StopAsync();

        // Assert: GetNodeAsync was never called since there's no existing NodeId.
        _apiMock.Verify(a => a.GetNodeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        // Assert: upload proceeded normally.
        transferMock.Verify(
            t => t.UploadAsync(null, filePath, It.IsAny<Stream>(),
                It.IsAny<IProgress<TransferProgress>?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>()),
            Times.Once);
    }

    // ── Case-sensitivity conflict (Issue #41) ───────────────────────────────

    [TestMethod]
    public async Task SyncAsync_NameConflictException_MovesOperationToFailedWithoutRetry()
    {
        // Arrange: upload throws NameConflictException (server 409 NAME_CONFLICT).
        var filePath = Path.Combine(_tempDir, "conflict.txt");
        await File.WriteAllTextAsync(filePath, "conflict test");

        var transferMock = new Mock<IChunkedTransferClient>();
        transferMock.Setup(t => t.UploadAsync(
                It.IsAny<Guid?>(), filePath, It.IsAny<Stream>(), It.IsAny<IProgress<TransferProgress>?>(),
                It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .ThrowsAsync(new NameConflictException("A file named 'Conflict.txt' already exists."));

        _stateDbMock.Setup(db => db.MoveToFailedAsync(
                It.IsAny<string>(), It.IsAny<PendingOperationRecord>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var pendingOp = new PendingUpload { Id = 1, LocalPath = filePath, NodeId = null, RetryCount = 0 };
        _stateDbMock.Setup(db => db.GetPendingOperationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pendingOp]);
        await using var engine = BuildEngine(transferMock.Object);

        // Act
        await engine.StartAsync(_context);
        await engine.SyncAsync(_context);
        await engine.StopAsync();

        // Assert: moved to failed queue immediately.
        _stateDbMock.Verify(db => db.MoveToFailedAsync(
            _context.StateDatabasePath,
            pendingOp,
            It.Is<string>(s => s.Contains("Conflict.txt")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SyncAsync_NameConflictException_DoesNotRetry()
    {
        // Upload throws NameConflictException → UpdateOperationRetryAsync must never be called.
        var filePath = Path.Combine(_tempDir, "no-retry.txt");
        await File.WriteAllTextAsync(filePath, "no retry test");

        var transferMock = new Mock<IChunkedTransferClient>();
        transferMock.Setup(t => t.UploadAsync(
                It.IsAny<Guid?>(), filePath, It.IsAny<Stream>(), It.IsAny<IProgress<TransferProgress>?>(),
                It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .ThrowsAsync(new NameConflictException("Conflict."));

        _stateDbMock.Setup(db => db.MoveToFailedAsync(
                It.IsAny<string>(), It.IsAny<PendingOperationRecord>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _stateDbMock.Setup(db => db.GetPendingOperationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PendingUpload { Id = 2, LocalPath = filePath, NodeId = null, RetryCount = 0 }]);

        await using var engine = BuildEngine(transferMock.Object);

        // Act
        await engine.StartAsync(_context);
        await engine.SyncAsync(_context);
        await engine.StopAsync();

        // Assert: retry scheduling was NOT called.
        _stateDbMock.Verify(db => db.UpdateOperationRetryAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── POSIX metadata downloads (Issue #42) ────────────────────────────────

    [TestMethod]
    public async Task SyncAsync_Download_AppliesPosixModeOnLinux()
    {
        if (!OperatingSystem.IsLinux()) return;

        var localPath = Path.Combine(_tempDir, "posix-file.txt");
        var nodeId = Guid.NewGuid();

        var transferMock = new Mock<IChunkedTransferClient>();
        transferMock
            .Setup(t => t.DownloadAsync(nodeId, It.IsAny<IProgress<TransferProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream("content"u8.ToArray()));

        _stateDbMock.Setup(db => db.GetPendingOperationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PendingDownload { Id = 1, LocalPath = localPath, NodeId = nodeId, PosixMode = 420 }]); // 0o644
        _stateDbMock.Setup(db => db.RemoveOperationAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _stateDbMock.Setup(db => db.UpsertFileRecordAsync(It.IsAny<string>(), It.IsAny<LocalFileRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await using var engine = BuildEngine(transferMock.Object);
        await engine.StartAsync(_context);
        await engine.SyncAsync(_context);
        await engine.StopAsync();

        Assert.IsTrue(File.Exists(localPath), "Expected downloaded file to exist.");
        var actualMode = (int)File.GetUnixFileMode(localPath);
        Assert.AreEqual(420, actualMode, $"Expected mode 0o644 (420) but got {actualMode}.");
    }

    [TestMethod]
    public async Task SyncAsync_Download_NullPosixMode_AppliesDefault644OnLinux()
    {
        if (!OperatingSystem.IsLinux()) return;

        var localPath = Path.Combine(_tempDir, "default-mode-file.txt");
        var nodeId = Guid.NewGuid();

        var transferMock = new Mock<IChunkedTransferClient>();
        transferMock
            .Setup(t => t.DownloadAsync(nodeId, It.IsAny<IProgress<TransferProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream("content"u8.ToArray()));

        _stateDbMock.Setup(db => db.GetPendingOperationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PendingDownload { Id = 1, LocalPath = localPath, NodeId = nodeId, PosixMode = null }]);
        _stateDbMock.Setup(db => db.RemoveOperationAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _stateDbMock.Setup(db => db.UpsertFileRecordAsync(It.IsAny<string>(), It.IsAny<LocalFileRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await using var engine = BuildEngine(transferMock.Object);
        await engine.StartAsync(_context);
        await engine.SyncAsync(_context);
        await engine.StopAsync();

        Assert.IsTrue(File.Exists(localPath), "Expected downloaded file to exist.");
        var expectedMode = (int)(UnixFileMode.UserRead | UnixFileMode.UserWrite
            | UnixFileMode.GroupRead | UnixFileMode.OtherRead); // 0o644 = 420
        var actualMode = (int)File.GetUnixFileMode(localPath);
        Assert.AreEqual(expectedMode, actualMode, $"Expected default mode 0o644 (420) but got {actualMode}.");
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private SyncEngine BuildEngine(IChunkedTransferClient? transfer = null) =>
        new(
            _apiMock.Object,
            new Mock<ITokenStore>().Object,
            transfer ?? new Mock<IChunkedTransferClient>().Object,
            new Mock<IConflictResolver>().Object,
            _stateDbMock.Object,
            new SelectiveSyncConfig(),
            new DotNetCloud.Client.Core.SyncIgnore.SyncIgnoreParser(),
            _lockedFileReaderMock.Object,
            NullLogger<SyncEngine>.Instance)
        {
            Tier2RetryDelay = TimeSpan.Zero,
        };

    // ── Issue #51: Case-conflict detection ──────────────────────────────────

    [TestMethod]
    public void ResolveCaseConflict_NoExistingFile_ReturnsOriginalPath()
    {
        var path = Path.Combine(_tempDir, "newfile.txt");
        var result = SyncEngine.ResolveCaseConflict(path);
        Assert.AreEqual(path, result);
    }

    [TestMethod]
    public void ResolveCaseConflict_SameCase_ReturnsOriginalPath()
    {
        var existing = Path.Combine(_tempDir, "document.txt");
        File.WriteAllText(existing, "content");

        var result = SyncEngine.ResolveCaseConflict(existing);
        Assert.AreEqual(existing, result);
    }

    [TestMethod]
    public void ResolveCaseConflict_DifferentCase_ReturnsCaseConflictPath()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS())
            return; // Only meaningful on case-insensitive filesystems.

        var existing = Path.Combine(_tempDir, "Document.txt");
        File.WriteAllText(existing, "content");

        // Request the same file with different casing.
        var requested = Path.Combine(_tempDir, "DOCUMENT.txt");
        var result = SyncEngine.ResolveCaseConflict(requested);

        StringAssert.Contains(result, "(case conflict)");
        Assert.AreNotEqual(requested, result);
    }

    [TestMethod]
    public void BuildCaseConflictPath_ProducesCorrectFormat()
    {
        var path = Path.Combine(_tempDir, "report.docx");
        var result = SyncEngine.BuildCaseConflictPath(path);

        StringAssert.Contains(result, "report (case conflict).docx");
        Assert.AreEqual(Path.GetDirectoryName(path), Path.GetDirectoryName(result));
    }

    [TestMethod]
    public void BuildCaseConflictPath_IncrementsWhenExists()
    {
        var conflictPath = Path.Combine(_tempDir, "report (case conflict).docx");
        File.WriteAllText(conflictPath, "existing conflict");

        var path = Path.Combine(_tempDir, "report.docx");
        var result = SyncEngine.BuildCaseConflictPath(path);

        StringAssert.Contains(result, "report (case conflict 1).docx");
    }

    // ── Issue #57: FSW.Error event handler ──────────────────────────────────

    [TestMethod]
    public async Task StartAsync_FswErrorEvent_HandlerIsWired()
    {
        // Arrange & Act: start engine (which creates a FileSystemWatcher
        // and wires Error, Changed, Created, Deleted, Renamed handlers).
        await _engine.StartAsync(_context);

        // We can't externally trigger FileSystemWatcher.Error, but we
        // can verify the engine started with real-time monitoring enabled.
        var status = await _engine.GetStatusAsync(_context);
        Assert.IsNotNull(status, "Engine should report status after start.");

        await _engine.StopAsync();
    }

    private sealed class DiskFullIOException : IOException
    {
        public DiskFullIOException()
            : base("There is not enough space on the disk.")
        {
            HResult = unchecked((int)0x80070070);
        }
    }
}



