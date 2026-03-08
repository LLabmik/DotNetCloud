using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.Auth;
using DotNetCloud.Client.Core.Conflict;
using DotNetCloud.Client.Core.LocalState;
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

        _engine = new SyncEngine(
            _apiMock.Object,
            tokenStoreMock.Object,
            new Mock<IChunkedTransferClient>().Object,
            new Mock<IConflictResolver>().Object,
            _stateDbMock.Object,
            new SelectiveSyncConfig(),
            NullLogger<SyncEngine>.Instance);
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
}
