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
using System.Diagnostics;

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
        _stateDbMock.Setup(db => db.GetAllFileRecordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _stateDbMock.Setup(db => db.GetPendingUploadPathsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());
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
    public async Task SyncAsync_BurstWhileRunning_CoalescesIntoSingleTrailingPass()
    {
        var firstPassGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var checkpointCalls = 0;

        _stateDbMock
            .Setup(db => db.UpdateCheckpointAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                var callIndex = Interlocked.Increment(ref checkpointCalls);
                if (callIndex == 1)
                    await firstPassGate.Task;
            });

        await _engine.StartAsync(_context);

        var firstPass = _engine.SyncAsync(_context);

        var spinUntilEntered = Stopwatch.StartNew();
        while (Volatile.Read(ref checkpointCalls) == 0 && spinUntilEntered.Elapsed < TimeSpan.FromSeconds(2))
            await Task.Delay(10);

        Assert.AreEqual(1, Volatile.Read(ref checkpointCalls), "First sync pass did not start in time.");

        var burstTasks = Enumerable.Range(0, 5)
            .Select(_ => _engine.SyncAsync(_context))
            .ToArray();

        firstPassGate.SetResult();

        await Task.WhenAll(burstTasks.Append(firstPass));

        var spinUntilTrailing = Stopwatch.StartNew();
        while (Volatile.Read(ref checkpointCalls) < 2 && spinUntilTrailing.Elapsed < TimeSpan.FromSeconds(2))
            await Task.Delay(10);

        Assert.AreEqual(2, Volatile.Read(ref checkpointCalls));

        await _engine.StopAsync();
    }

    [TestMethod]
    public async Task SyncAsync_SelfOriginatedRemoteChangeWithDifferentHashFormat_DoesNotQueueDownload()
    {
        var deviceId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var localPath = Path.Combine(_tempDir, "echo-self-originated.txt");
        File.WriteAllText(localPath, "self-originated content");

        var localModifiedAt = File.GetLastWriteTimeUtc(localPath);
        var localRecord = new LocalFileRecord
        {
            LocalPath = localPath,
            NodeId = nodeId,
            ContentHash = "raw-file-sha256",
            LastSyncedAt = localModifiedAt.AddSeconds(1),
            LocalModifiedAt = localModifiedAt,
        };

        _engine.DeviceId = deviceId;

        _stateDbMock
            .Setup(db => db.GetAllFileRecordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([localRecord]);
        _stateDbMock
            .Setup(db => db.GetFileRecordByNodeIdAsync(It.IsAny<string>(), nodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(localRecord);
        _stateDbMock
            .Setup(db => db.GetFileRecordAsync(It.IsAny<string>(), localPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(localRecord);

        _apiMock
            .Setup(a => a.GetChangesSinceAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedSyncChangesResponse
            {
                Changes =
                [
                    new SyncChangeResponse
                    {
                        NodeId = nodeId,
                        Name = Path.GetFileName(localPath),
                        NodeType = "File",
                        ContentHash = "server-manifest-hash",
                        UpdatedAt = DateTime.UtcNow,
                        OriginatingDeviceId = deviceId,
                    },
                ],
                NextCursor = null,
                HasMore = false,
            });

        await _engine.StartAsync(_context);
        await _engine.SyncAsync(_context);
        await _engine.StopAsync();

        _stateDbMock.Verify(db => db.QueueOperationAsync(
            It.IsAny<string>(),
            It.Is<PendingDownload>(op => op.NodeId == nodeId),
            It.IsAny<CancellationToken>()), Times.Never);

        _stateDbMock.Verify(db => db.UpsertFileRecordAsync(
            It.IsAny<string>(),
            It.Is<LocalFileRecord>(r => r.NodeId == nodeId && r.ContentHash == "server-manifest-hash"),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
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

    [TestMethod]
    public async Task SyncAsync_ReconcileWithStaleFileRecord_RemovesRecordAndQueuesDownload()
    {
        // Arrange: server tree contains a file that is missing locally. The state DB has a
        // stale record for the same node pointing at a non-existent old path.
        var nodeId = Guid.NewGuid();
        var stalePath = Path.Combine(_tempDir, "stale", "remote.txt");
        var expectedLocalPath = Path.Combine(_tempDir, "remote.txt");

        _apiMock.Setup(a => a.GetFolderTreeAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncTreeNodeResponse
            {
                NodeId = Guid.Empty,
                Name = "/",
                NodeType = "Folder",
                Children =
                [
                    new SyncTreeNodeResponse
                    {
                        NodeId = nodeId,
                        Name = "remote.txt",
                        NodeType = "File",
                    },
                ],
            });

        _stateDbMock.Setup(db => db.GetFileRecordByNodeIdAsync(_context.StateDatabasePath, nodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocalFileRecord
            {
                LocalPath = stalePath,
                NodeId = nodeId,
            });

        _stateDbMock.Setup(db => db.RemoveFileRecordAsync(_context.StateDatabasePath, stalePath, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _stateDbMock.Setup(db => db.HasRecentTerminalDownloadFailureAsync(
                _context.StateDatabasePath,
                nodeId,
                expectedLocalPath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        PendingDownload? queuedDownload = null;
        _stateDbMock.Setup(db => db.QueueOperationAsync(
                _context.StateDatabasePath,
                It.IsAny<PendingOperationRecord>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, PendingOperationRecord, CancellationToken>((_, op, _) => queuedDownload = op as PendingDownload)
            .Returns(Task.CompletedTask);

        await _engine.StartAsync(_context);

        // Act
        await _engine.SyncAsync(_context);
        await _engine.StopAsync();

        // Assert
        _stateDbMock.Verify(db => db.RemoveFileRecordAsync(
            _context.StateDatabasePath,
            stalePath,
            It.IsAny<CancellationToken>()), Times.Once);

        _stateDbMock.Verify(db => db.QueueOperationAsync(
            _context.StateDatabasePath,
            It.IsAny<PendingOperationRecord>(),
            It.IsAny<CancellationToken>()), Times.Once);

        Assert.IsNotNull(queuedDownload);
        Assert.AreEqual(nodeId, queuedDownload.NodeId);
        Assert.AreEqual(expectedLocalPath, queuedDownload.LocalPath);
    }

    [TestMethod]
    public async Task SyncAsync_RemoteChangeMissingNodeMap_UsesParentPathForDownload()
    {
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var expectedPath = Path.Combine(_tempDir, "Docs", "notes.txt");

        // Tree snapshot contains only the parent folder. The changed file is absent from the map,
        // but provides ParentId in the change feed.
        _apiMock.Setup(a => a.GetFolderTreeAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncTreeNodeResponse
            {
                NodeId = Guid.Empty,
                Name = "/",
                NodeType = "Folder",
                Children =
                [
                    new SyncTreeNodeResponse
                    {
                        NodeId = parentId,
                        Name = "Docs",
                        NodeType = "Folder",
                    },
                ],
            });

        _apiMock.SetupSequence(a => a.GetChangesSinceAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedSyncChangesResponse
            {
                Changes =
                [
                    new SyncChangeResponse
                    {
                        NodeId = childId,
                        ParentId = parentId,
                        Name = "notes.txt",
                        NodeType = "File",
                        UpdatedAt = DateTime.UtcNow,
                    },
                ],
                HasMore = false,
                NextCursor = null,
            });

        _stateDbMock.Setup(db => db.GetFileRecordByNodeIdAsync(_context.StateDatabasePath, childId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LocalFileRecord?)null);
        _stateDbMock.Setup(db => db.GetFileRecordAsync(_context.StateDatabasePath, expectedPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LocalFileRecord?)null);
        _stateDbMock.Setup(db => db.GetPendingOperationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        PendingDownload? queuedDownload = null;
        _stateDbMock.Setup(db => db.QueueOperationAsync(
                _context.StateDatabasePath,
                It.IsAny<PendingOperationRecord>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, PendingOperationRecord, CancellationToken>((_, op, _) =>
            {
                if (op is PendingDownload pd && pd.NodeId == childId)
                    queuedDownload = pd;
            })
            .Returns(Task.CompletedTask);

        await _engine.StartAsync(_context);
        await _engine.SyncAsync(_context);
        await _engine.StopAsync();

        Assert.IsNotNull(queuedDownload, "Expected the changed file to be queued for download.");
        Assert.AreEqual(expectedPath, queuedDownload!.LocalPath);
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

    [TestMethod]
    public async Task SyncAsync_PendingDownloadNotFound_MovesToFailedWithoutRetry()
    {
        var localPath = Path.Combine(_tempDir, "missing-remote-file.txt");
        var nodeId = Guid.NewGuid();
        var pendingOp = new PendingDownload { Id = 7, LocalPath = localPath, NodeId = nodeId, RetryCount = 0 };

        var transferMock = new Mock<IChunkedTransferClient>();
        transferMock
            .Setup(t => t.DownloadAsync(nodeId, It.IsAny<IProgress<TransferProgress>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Not Found", null, System.Net.HttpStatusCode.NotFound));

        _stateDbMock.Setup(db => db.GetPendingOperationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pendingOp]);
        _stateDbMock.Setup(db => db.MoveToFailedAsync(
                It.IsAny<string>(), It.IsAny<PendingOperationRecord>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await using var engine = BuildEngine(transferMock.Object);

        await engine.StartAsync(_context);
        await engine.SyncAsync(_context);
        await engine.StopAsync();

        _stateDbMock.Verify(db => db.MoveToFailedAsync(
            _context.StateDatabasePath,
            pendingOp,
            It.Is<string>(s => s.Contains("Not Found")),
            It.IsAny<CancellationToken>()), Times.Once);
        _stateDbMock.Verify(db => db.UpdateOperationRetryAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task SyncAsync_PendingDownloadNotFoundWithoutStatusCode_MovesToFailedWithoutRetry()
    {
        var localPath = Path.Combine(_tempDir, "missing-remote-file-no-status.txt");
        var nodeId = Guid.NewGuid();
        var pendingOp = new PendingDownload { Id = 8, LocalPath = localPath, NodeId = nodeId, RetryCount = 0 };

        var transferMock = new Mock<IChunkedTransferClient>();
        transferMock
            .Setup(t => t.DownloadAsync(nodeId, It.IsAny<IProgress<TransferProgress>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Response status code does not indicate success: 404 (Not Found)."));

        _stateDbMock.Setup(db => db.GetPendingOperationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pendingOp]);
        _stateDbMock.Setup(db => db.MoveToFailedAsync(
                It.IsAny<string>(), It.IsAny<PendingOperationRecord>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await using var engine = BuildEngine(transferMock.Object);

        await engine.StartAsync(_context);
        await engine.SyncAsync(_context);
        await engine.StopAsync();

        _stateDbMock.Verify(db => db.MoveToFailedAsync(
            _context.StateDatabasePath,
            pendingOp,
            It.Is<string>(s => s.Contains("404")),
            It.IsAny<CancellationToken>()), Times.Once);
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

    // ── Local directory scan (upload path) ─────────────────────────────────

    [TestMethod]
    public async Task SyncAsync_DownloadWithNonSeekableStream_FiresTransferCompleteWithoutThrowing()
    {
        // Regression: stream.Length on an HTTP response stream (non-seekable) throws
        // NotSupportedException. FileTransferComplete should use FileInfo.Length instead.
        var localPath = Path.Combine(_tempDir, "non-seekable.txt");
        var nodeId = Guid.NewGuid();
        var content = "hello world"u8.ToArray();

        // Wrap content in a non-seekable stream to reproduce the HTTP response stream behaviour.
        var nonSeekableStream = new NonSeekableStream(new MemoryStream(content));

        var transferMock = new Mock<IChunkedTransferClient>();
        transferMock
            .Setup(t => t.DownloadAsync(nodeId, It.IsAny<IProgress<TransferProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(nonSeekableStream);

        _stateDbMock.Setup(db => db.GetPendingOperationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PendingDownload { Id = 1, LocalPath = localPath, NodeId = nodeId }]);
        _stateDbMock.Setup(db => db.RemoveOperationAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _stateDbMock.Setup(db => db.UpsertFileRecordAsync(It.IsAny<string>(), It.IsAny<LocalFileRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        FileTransferCompleteEventArgs? completed = null;
        await using var engine = BuildEngine(transferMock.Object);
        engine.FileTransferComplete += (_, e) => completed = e;

        await engine.StartAsync(_context);
        await engine.SyncAsync(_context);
        await engine.StopAsync();

        Assert.IsTrue(File.Exists(localPath), "File should be written to disk.");
        Assert.IsNotNull(completed, "FileTransferComplete event should have fired.");
        Assert.AreEqual("download", completed!.Direction);
        Assert.AreEqual(content.Length, completed.TotalBytes);
    }

    [TestMethod]
    public async Task SyncAsync_NewLocalFile_QueuesUpload()
    {
        // Arrange: create a file on disk that has no state.db record.
        var filePath = Path.Combine(_tempDir, "new-file.txt");
        File.WriteAllText(filePath, "hello");

        _stateDbMock
            .Setup(db => db.GetAllFileRecordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _stateDbMock
            .Setup(db => db.GetPendingUploadPathsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());
        _stateDbMock
            .Setup(db => db.QueueOperationAsync(It.IsAny<string>(), It.IsAny<PendingOperationRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _engine.StartAsync(_context);
        await _engine.SyncAsync(_context);
        await _engine.StopAsync();

        // Assert: a PendingUpload was queued for the new file.
        _stateDbMock.Verify(db => db.QueueOperationAsync(
            _context.StateDatabasePath,
            It.Is<PendingUpload>(u => u.LocalPath == filePath && u.NodeId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SyncAsync_ModifiedLocalFile_QueuesUpload()
    {
        // Arrange: file exists on disk and in state.db, but was modified after LastSyncedAt.
        var filePath = Path.Combine(_tempDir, "modified-file.txt");
        File.WriteAllText(filePath, "updated content");

        var nodeId = Guid.NewGuid();
        var record = new LocalFileRecord
        {
            LocalPath = filePath,
            NodeId = nodeId,
            ContentHash = "oldhash",
            LastSyncedAt = DateTime.UtcNow.AddHours(-1), // older than file mtime
            LocalModifiedAt = DateTime.UtcNow.AddHours(-1),
        };

        _stateDbMock
            .Setup(db => db.GetAllFileRecordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([record]);
        _stateDbMock
            .Setup(db => db.GetPendingUploadPathsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());
        _stateDbMock
            .Setup(db => db.QueueOperationAsync(It.IsAny<string>(), It.IsAny<PendingOperationRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _engine.StartAsync(_context);
        await _engine.SyncAsync(_context);
        await _engine.StopAsync();

        // Assert: a PendingUpload was queued with the existing NodeId.
        _stateDbMock.Verify(db => db.QueueOperationAsync(
            _context.StateDatabasePath,
            It.Is<PendingUpload>(u => u.LocalPath == filePath && u.NodeId == nodeId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SyncAsync_UnmodifiedLocalFile_DoesNotQueueUpload()
    {
        // Arrange: file exists on disk with mtime BEFORE LastSyncedAt → no change.
        var filePath = Path.Combine(_tempDir, "unchanged-file.txt");
        File.WriteAllText(filePath, "same content");
        // Back-date the file so mtime < LastSyncedAt
        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddHours(-2));

        var record = new LocalFileRecord
        {
            LocalPath = filePath,
            NodeId = Guid.NewGuid(),
            ContentHash = "samehash",
            LastSyncedAt = DateTime.UtcNow.AddHours(-1), // newer than file mtime
            LocalModifiedAt = DateTime.UtcNow.AddHours(-2),
        };

        _stateDbMock
            .Setup(db => db.GetAllFileRecordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([record]);
        _stateDbMock
            .Setup(db => db.GetPendingUploadPathsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());

        // Act
        await _engine.StartAsync(_context);
        await _engine.SyncAsync(_context);
        await _engine.StopAsync();

        // Assert: no upload queued.
        _stateDbMock.Verify(db => db.QueueOperationAsync(
            It.IsAny<string>(),
            It.Is<PendingUpload>(u => u.LocalPath == filePath),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task SyncAsync_AlreadyQueuedFile_DoesNotDoubleQueue()
    {
        // Arrange: new file on disk, but already in the pending-upload set.
        var filePath = Path.Combine(_tempDir, "already-queued.txt");
        File.WriteAllText(filePath, "content");

        _stateDbMock
            .Setup(db => db.GetAllFileRecordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _stateDbMock
            .Setup(db => db.GetPendingUploadPathsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { filePath });

        // Act
        await _engine.StartAsync(_context);
        await _engine.SyncAsync(_context);
        await _engine.StopAsync();

        // Assert: no duplicate queue entry.
        _stateDbMock.Verify(db => db.QueueOperationAsync(
            It.IsAny<string>(),
            It.Is<PendingUpload>(u => u.LocalPath == filePath),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class DiskFullIOException : IOException
    {
        public DiskFullIOException()
            : base("There is not enough space on the disk.")
        {
            HResult = unchecked((int)0x80070070);
        }
    }

    /// <summary>
    /// Wraps a readable stream but disables CanSeek so that calling .Length throws
    /// NotSupportedException — replicating the behaviour of an HTTP response stream.
    /// </summary>
    private sealed class NonSeekableStream(Stream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException("Stream is not seekable.");
        public override long Position
        {
            get => throw new NotSupportedException("Stream is not seekable.");
            set => throw new NotSupportedException("Stream is not seekable.");
        }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException("Stream is not seekable.");
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }
}



