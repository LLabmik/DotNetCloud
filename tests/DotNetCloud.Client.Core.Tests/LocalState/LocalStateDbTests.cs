using DotNetCloud.Client.Core.LocalState;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Client.Core.Tests.LocalState;

[TestClass]
public class LocalStateDbTests
{
    private string _dbPath = null!;
    private LocalStateDb _db = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".db");
        _db = new LocalStateDb(NullLogger<LocalStateDb>.Instance);
        await _db.InitializeAsync(_dbPath);
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Release all pooled SQLite connections before deleting the temp file
        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        try
        {
            if (File.Exists(_dbPath))
                File.Delete(_dbPath);
        }
        catch (IOException)
        {
            // Best-effort cleanup; temp file will be cleaned by OS eventually
        }
    }

    // ── File Records ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task UpsertAndGetFileRecord_RoundTrips()
    {
        var nodeId = Guid.NewGuid();
        var record = new LocalFileRecord
        {
            LocalPath = "/home/user/docs/report.docx",
            NodeId = nodeId,
            ContentHash = "abc123",
            LastSyncedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            LocalModifiedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        await _db.UpsertFileRecordAsync(_dbPath, record);
        var loaded = await _db.GetFileRecordAsync(_dbPath, "/home/user/docs/report.docx");

        Assert.IsNotNull(loaded);
        Assert.AreEqual(nodeId, loaded.NodeId);
        Assert.AreEqual("abc123", loaded.ContentHash);
    }

    [TestMethod]
    public async Task GetFileRecordAsync_NotFound_ReturnsNull()
    {
        var result = await _db.GetFileRecordAsync(_dbPath, "/no/such/file.txt");
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetFileRecordByNodeIdAsync_FindsRecord()
    {
        var nodeId = Guid.NewGuid();
        await _db.UpsertFileRecordAsync(_dbPath, new LocalFileRecord
        {
            LocalPath = "/docs/file.txt",
            NodeId = nodeId,
            LastSyncedAt = DateTime.UtcNow,
            LocalModifiedAt = DateTime.UtcNow,
        });

        var found = await _db.GetFileRecordByNodeIdAsync(_dbPath, nodeId);

        Assert.IsNotNull(found);
        Assert.AreEqual("/docs/file.txt", found.LocalPath);
    }

    [TestMethod]
    public async Task UpsertFileRecordAsync_UpdatesExistingRecord()
    {
        var nodeId = Guid.NewGuid();
        var path = "/docs/update.txt";
        await _db.UpsertFileRecordAsync(_dbPath, new LocalFileRecord
        {
            LocalPath = path,
            NodeId = nodeId,
            ContentHash = "hash-v1",
            LastSyncedAt = DateTime.UtcNow,
            LocalModifiedAt = DateTime.UtcNow,
        });

        await _db.UpsertFileRecordAsync(_dbPath, new LocalFileRecord
        {
            LocalPath = path,
            NodeId = nodeId,
            ContentHash = "hash-v2",
            LastSyncedAt = DateTime.UtcNow,
            LocalModifiedAt = DateTime.UtcNow,
        });

        var loaded = await _db.GetFileRecordAsync(_dbPath, path);
        Assert.AreEqual("hash-v2", loaded?.ContentHash);
    }

    [TestMethod]
    public async Task RemoveFileRecordAsync_RemovesRecord()
    {
        var path = "/docs/to-delete.txt";
        await _db.UpsertFileRecordAsync(_dbPath, new LocalFileRecord
        {
            LocalPath = path,
            NodeId = Guid.NewGuid(),
            LastSyncedAt = DateTime.UtcNow,
            LocalModifiedAt = DateTime.UtcNow,
        });

        await _db.RemoveFileRecordAsync(_dbPath, path);

        var result = await _db.GetFileRecordAsync(_dbPath, path);
        Assert.IsNull(result);
    }

    // ── Pending Operations ──────────────────────────────────────────────────

    [TestMethod]
    public async Task QueueAndGetPendingOperations_RoundTrips()
    {
        await _db.QueueOperationAsync(_dbPath, new PendingUpload { LocalPath = "/docs/upload.txt" });
        await _db.QueueOperationAsync(_dbPath, new PendingDownload { NodeId = Guid.NewGuid(), LocalPath = "/docs/download.txt" });

        var ops = await _db.GetPendingOperationsAsync(_dbPath);

        Assert.AreEqual(2, ops.Count);
        Assert.IsTrue(ops.Any(o => o is PendingUpload));
        Assert.IsTrue(ops.Any(o => o is PendingDownload));
    }

    [TestMethod]
    public async Task RemoveOperationAsync_RemovesOperation()
    {
        await _db.QueueOperationAsync(_dbPath, new PendingUpload { LocalPath = "/docs/file.txt" });
        var ops = await _db.GetPendingOperationsAsync(_dbPath);
        Assert.AreEqual(1, ops.Count);

        await _db.RemoveOperationAsync(_dbPath, ops[0].Id);

        var remaining = await _db.GetPendingOperationsAsync(_dbPath);
        Assert.AreEqual(0, remaining.Count);
    }

    [TestMethod]
    public async Task GetPendingOperationCountAsync_CountsCorrectly()
    {
        await _db.QueueOperationAsync(_dbPath, new PendingUpload { LocalPath = "/a.txt" });
        await _db.QueueOperationAsync(_dbPath, new PendingUpload { LocalPath = "/b.txt" });
        await _db.QueueOperationAsync(_dbPath, new PendingDownload { NodeId = Guid.NewGuid(), LocalPath = "/c.txt" });

        var counts = await _db.GetPendingOperationCountAsync(_dbPath);

        Assert.AreEqual(2, counts.Uploads);
        Assert.AreEqual(1, counts.Downloads);
    }

    // ── Checkpoint ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetCheckpointAsync_InitialState_ReturnsNull()
    {
        var checkpoint = await _db.GetCheckpointAsync(_dbPath);
        Assert.IsNull(checkpoint);
    }

    [TestMethod]
    public async Task UpdateAndGetCheckpointAsync_RoundTrips()
    {
        var time = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        await _db.UpdateCheckpointAsync(_dbPath, time);
        var loaded = await _db.GetCheckpointAsync(_dbPath);

        Assert.AreEqual(time, loaded);
    }

    [TestMethod]
    public async Task UpdateCheckpointAsync_UpdatesExisting()
    {
        var first = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var second = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        await _db.UpdateCheckpointAsync(_dbPath, first);
        await _db.UpdateCheckpointAsync(_dbPath, second);

        var loaded = await _db.GetCheckpointAsync(_dbPath);
        Assert.AreEqual(second, loaded);
    }
}
