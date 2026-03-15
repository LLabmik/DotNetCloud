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

    // ── WAL Mode (Task 1.6) ─────────────────────────────────────────────────

    [TestMethod]
    public async Task InitializeAsync_EnablesWalMode()
    {
        // After InitializeAsync (called in test setup), verify the journal_mode is WAL
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode;";
        var result = (string?)await cmd.ExecuteScalarAsync();
        Assert.AreEqual("wal", result?.ToLowerInvariant());
    }

    [TestMethod]
    public async Task InitializeAsync_CorruptDb_ArchivesAndCreatesNewDb()
    {
        var corruptDbPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".db");
        try
        {
            // Write garbage bytes to simulate a corrupt SQLite file
            await File.WriteAllBytesAsync(corruptDbPath, "this is not a valid sqlite database file!"u8.ToArray());

            var db = new LocalStateDb(Microsoft.Extensions.Logging.Abstractions.NullLogger<LocalStateDb>.Instance);
            await db.InitializeAsync(corruptDbPath);

            // Verify reset flag is set
            Assert.IsTrue(db.WasRecentlyReset(corruptDbPath), "WasRecentlyReset should be true after corruption recovery.");

            // Verify a corrupt archive file was created
            var dir = Path.GetDirectoryName(corruptDbPath)!;
            var fileName = Path.GetFileName(corruptDbPath);
            var corruptFiles = Directory.GetFiles(dir, $"{fileName}.corrupt.*");
            Assert.IsTrue(corruptFiles.Length > 0, "Expected a .corrupt.<timestamp> archive file.");

            // Verify the fresh DB is functional
            await db.QueueOperationAsync(corruptDbPath, new PendingUpload { LocalPath = "/recovered.txt" });
            var ops = await db.GetPendingOperationsAsync(corruptDbPath);
            Assert.AreEqual(1, ops.Count);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var dir = Path.GetDirectoryName(corruptDbPath)!;
            var fileName = Path.GetFileName(corruptDbPath);
            foreach (var f in Directory.GetFiles(dir, $"{fileName}*"))
                try { File.Delete(f); } catch { }
        }
    }

    [TestMethod]
    public async Task CheckpointWalAsync_DoesNotThrow()
    {
        // Write some data so there is something to checkpoint
        await _db.QueueOperationAsync(_dbPath, new PendingUpload { LocalPath = "/wal-test.txt" });

        // WAL checkpoint should complete without error
        await _db.CheckpointWalAsync(_dbPath);
    }

    // ── Retry Queue (Task 1.7) ──────────────────────────────────────────────

    [TestMethod]
    public async Task GetPendingOperationsAsync_ExcludesFutureRetry()
    {
        // Queue two operations: one due now, one scheduled in the future
        await _db.QueueOperationAsync(_dbPath, new PendingUpload { LocalPath = "/immediate.txt" });
        await _db.QueueOperationAsync(_dbPath, new PendingUpload
        {
            LocalPath = "/deferred.txt",
            NextRetryAt = DateTime.UtcNow.AddHours(1),
        });

        var ops = await _db.GetPendingOperationsAsync(_dbPath);

        // Only the immediate operation should be returned
        Assert.AreEqual(1, ops.Count);
        Assert.IsInstanceOfType<PendingUpload>(ops[0]);
        Assert.AreEqual("/immediate.txt", ((PendingUpload)ops[0]).LocalPath);
    }

    [TestMethod]
    public async Task UpdateOperationRetryAsync_UpdatesRetryFields()
    {
        await _db.QueueOperationAsync(_dbPath, new PendingUpload { LocalPath = "/retry-test.txt" });
        var ops = await _db.GetPendingOperationsAsync(_dbPath);
        var op = ops[0];

        var nextRetry = DateTime.UtcNow.AddMinutes(5);
        await _db.UpdateOperationRetryAsync(_dbPath, op.Id, 1, nextRetry, "Network error");

        // Operation is now deferred — should not appear in GetPendingOperationsAsync
        var pending = await _db.GetPendingOperationsAsync(_dbPath);
        Assert.AreEqual(0, pending.Count);

        // Simulate time passing: update NextRetryAt to the past and re-query
        await _db.UpdateOperationRetryAsync(_dbPath, op.Id, 1, DateTime.UtcNow.AddSeconds(-1), "Network error");
        var pendingNow = await _db.GetPendingOperationsAsync(_dbPath);
        Assert.AreEqual(1, pendingNow.Count);
        Assert.AreEqual(1, pendingNow[0].RetryCount);
        Assert.AreEqual("Network error", pendingNow[0].LastError);
    }

    [TestMethod]
    public async Task MoveToFailedAsync_RemovesFromPendingAndAddsToFailed()
    {
        await _db.QueueOperationAsync(_dbPath, new PendingUpload { LocalPath = "/to-fail.txt" });
        var ops = await _db.GetPendingOperationsAsync(_dbPath);
        Assert.AreEqual(1, ops.Count);

        await _db.MoveToFailedAsync(_dbPath, ops[0], "Permanent failure after 10 retries");

        // Should no longer be in pending
        var pending = await _db.GetPendingOperationsAsync(_dbPath);
        Assert.AreEqual(0, pending.Count);
    }

    [TestMethod]
    public async Task HasRecentTerminalDownloadFailureAsync_When404FailedDownloadExists_ReturnsTrue()
    {
        var nodeId = Guid.NewGuid();
        const string localPath = "/docs/missing.txt";

        await _db.QueueOperationAsync(_dbPath, new PendingDownload { LocalPath = localPath, NodeId = nodeId });
        var op = (await _db.GetPendingOperationsAsync(_dbPath)).Single();
        await _db.MoveToFailedAsync(_dbPath, op, "Response status code does not indicate success: 404 (Not Found).", CancellationToken.None);

        var hasFailure = await _db.HasRecentTerminalDownloadFailureAsync(_dbPath, nodeId, localPath);

        Assert.IsTrue(hasFailure);
    }

    [TestMethod]
    public async Task HasRecentTerminalDownloadFailureAsync_WhenOnlyNon404FailureExists_ReturnsFalse()
    {
        var nodeId = Guid.NewGuid();
        const string localPath = "/docs/transient.txt";

        await _db.QueueOperationAsync(_dbPath, new PendingDownload { LocalPath = localPath, NodeId = nodeId });
        var op = (await _db.GetPendingOperationsAsync(_dbPath)).Single();
        await _db.MoveToFailedAsync(_dbPath, op, "Response status code does not indicate success: 500 (Internal Server Error).", CancellationToken.None);

        var hasFailure = await _db.HasRecentTerminalDownloadFailureAsync(_dbPath, nodeId, localPath);

        Assert.IsFalse(hasFailure);
    }

    // ── Sync Cursor (Tasks 2.4 + 2.5) ──────────────────────────────────────

    [TestMethod]
    public async Task GetSyncCursorAsync_InitialState_ReturnsNull()
    {
        var cursor = await _db.GetSyncCursorAsync(_dbPath);
        Assert.IsNull(cursor, "Expected null cursor for a fresh database.");
    }

    [TestMethod]
    public async Task UpdateAndGetSyncCursorAsync_RoundTrips()
    {
        const string expectedCursor = "dXNlcjoxMjM="; // base64url typical cursor
        await _db.UpdateSyncCursorAsync(_dbPath, expectedCursor);
        var loaded = await _db.GetSyncCursorAsync(_dbPath);
        Assert.AreEqual(expectedCursor, loaded);
    }

    [TestMethod]
    public async Task UpdateSyncCursorAsync_UpdatesExisting()
    {
        await _db.UpdateSyncCursorAsync(_dbPath, "cursor-v1");
        await _db.UpdateSyncCursorAsync(_dbPath, "cursor-v2");
        var loaded = await _db.GetSyncCursorAsync(_dbPath);
        Assert.AreEqual("cursor-v2", loaded, "Expected the latest cursor to be persisted.");
    }

    [TestMethod]
    public async Task UpdateSyncCursorAsync_IndependentOfLastSyncedAt()
    {
        // Cursor and LastSyncedAt should be updated independently.
        var now = new DateTime(2026, 3, 9, 0, 0, 0, DateTimeKind.Utc);
        await _db.UpdateCheckpointAsync(_dbPath, now);
        await _db.UpdateSyncCursorAsync(_dbPath, "cursor-after-checkpoint");

        var checkpoint = await _db.GetCheckpointAsync(_dbPath);
        var cursor = await _db.GetSyncCursorAsync(_dbPath);

        Assert.AreEqual(now, checkpoint, "LastSyncedAt should be unchanged.");
        Assert.AreEqual("cursor-after-checkpoint", cursor, "Cursor should be set independently.");
    }

    // ── Upload Dedup ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task QueueOperationAsync_DuplicateUpload_SkipsSecondQueue()
    {
        await _db.QueueOperationAsync(_dbPath, new PendingUpload { LocalPath = "/docs/same-file.txt" });
        await _db.QueueOperationAsync(_dbPath, new PendingUpload { LocalPath = "/docs/same-file.txt" });

        var ops = await _db.GetPendingOperationsAsync(_dbPath);
        Assert.AreEqual(1, ops.Count, "Duplicate upload for same path should be deduplicated.");
    }

    [TestMethod]
    public async Task QueueOperationAsync_DifferentUploadPaths_BothQueued()
    {
        await _db.QueueOperationAsync(_dbPath, new PendingUpload { LocalPath = "/docs/file-a.txt" });
        await _db.QueueOperationAsync(_dbPath, new PendingUpload { LocalPath = "/docs/file-b.txt" });

        var ops = await _db.GetPendingOperationsAsync(_dbPath);
        Assert.AreEqual(2, ops.Count, "Uploads for different paths should both be queued.");
    }

    [TestMethod]
    public async Task QueueOperationAsync_DuplicateDownload_SkipsSecondQueue()
    {
        var nodeId = Guid.NewGuid();
        await _db.QueueOperationAsync(_dbPath, new PendingDownload { NodeId = nodeId, LocalPath = "/docs/dl.txt" });
        await _db.QueueOperationAsync(_dbPath, new PendingDownload { NodeId = nodeId, LocalPath = "/docs/dl.txt" });

        var ops = await _db.GetPendingOperationsAsync(_dbPath);
        Assert.AreEqual(1, ops.Count, "Duplicate download for same NodeId should be deduplicated.");
    }

    [TestMethod]
    public async Task QueueOperationAsync_UploadAfterRemoval_AllowsRequeue()
    {
        await _db.QueueOperationAsync(_dbPath, new PendingUpload { LocalPath = "/docs/requeue.txt" });
        var ops = await _db.GetPendingOperationsAsync(_dbPath);
        await _db.RemoveOperationAsync(_dbPath, ops[0].Id);

        // After removal, same path should be queueable again.
        await _db.QueueOperationAsync(_dbPath, new PendingUpload { LocalPath = "/docs/requeue.txt" });
        var opsAfter = await _db.GetPendingOperationsAsync(_dbPath);
        Assert.AreEqual(1, opsAfter.Count, "Upload should be re-queueable after previous one is removed.");
    }
}
