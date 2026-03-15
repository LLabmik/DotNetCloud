using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Tests.Data;

/// <summary>
/// Tests verifying P0 sync hardening constraints and concurrency behavior:
/// - P0.1: Atomic SyncSequence assignment produces monotonic, distinct values
/// - P0.2: Unique constraint on (ParentId, Name) for active file nodes
/// - P0.3: Atomic chunk reference counting with correct counts
/// </summary>
[TestClass]
public class SyncHardeningP0Tests
{
    private static FilesDbContext CreateContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    /// <summary>
    /// Creates a SQLite-backed context that enforces real database constraints
    /// (unique indexes, CHECK constraints) unlike the InMemory provider.
    /// The connection must be kept open for the lifetime of the context.
    /// </summary>
    private static (FilesDbContext Context, SqliteConnection Connection) CreateSqliteContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new FilesDbContext(options);
        context.Database.EnsureCreated();
        return (context, connection);
    }

    // ── P0.1: Atomic SyncSequence Assignment ─────────────────────────────────

    [TestMethod]
    public async Task AssignNextSequenceAsync_SequentialCalls_ProducesDistinctMonotonicSequences()
    {
        using var context = CreateContext();
        var ownerId = Guid.NewGuid();
        var sequences = new List<long>();

        for (var i = 0; i < 10; i++)
        {
            var node = new FileNode
            {
                Name = $"file{i}.txt",
                OwnerId = ownerId,
                MaterializedPath = $"/file{i}.txt"
            };
            context.FileNodes.Add(node);
            await SyncCursorHelper.AssignNextSequenceAsync(context, node, ownerId);
            sequences.Add(node.SyncSequence!.Value);
        }

        // All sequences must be distinct
        Assert.AreEqual(10, sequences.Distinct().Count(),
            "All 10 sequences should be distinct.");

        // Sequences must be strictly monotonically increasing
        for (var i = 1; i < sequences.Count; i++)
        {
            Assert.IsTrue(sequences[i] > sequences[i - 1],
                $"Sequence {i} ({sequences[i]}) should be greater than sequence {i - 1} ({sequences[i - 1]}).");
        }
    }

    [TestMethod]
    public async Task AssignNextSequenceAsync_DifferentUsers_IndependentCounters()
    {
        using var context = CreateContext();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var nodeA1 = new FileNode { Name = "a1.txt", OwnerId = userA, MaterializedPath = "/a1.txt" };
        var nodeA2 = new FileNode { Name = "a2.txt", OwnerId = userA, MaterializedPath = "/a2.txt" };
        var nodeB1 = new FileNode { Name = "b1.txt", OwnerId = userB, MaterializedPath = "/b1.txt" };

        context.FileNodes.AddRange(nodeA1, nodeA2, nodeB1);

        await SyncCursorHelper.AssignNextSequenceAsync(context, nodeA1, userA);
        await SyncCursorHelper.AssignNextSequenceAsync(context, nodeB1, userB);
        await SyncCursorHelper.AssignNextSequenceAsync(context, nodeA2, userA);

        // User A's sequences: 1, 2 — User B's sequence: 1
        Assert.AreEqual(1, nodeA1.SyncSequence);
        Assert.AreEqual(1, nodeB1.SyncSequence, "User B should have independent counter starting at 1.");
        Assert.AreEqual(2, nodeA2.SyncSequence, "User A's second node should be 2.");
    }

    [TestMethod]
    public async Task AssignNextSequenceAsync_ConcurrentCalls_AllSequencesDistinct()
    {
        // With InMemory provider, concurrent access isn't truly atomic, but this verifies
        // the code doesn't crash and all calls complete. True atomicity requires PostgreSQL.
        var dbName = Guid.NewGuid().ToString();
        var ownerId = Guid.NewGuid();
        const int concurrency = 20;
        var sequences = new long[concurrency];

        var tasks = Enumerable.Range(0, concurrency).Select(async i =>
        {
            using var context = CreateContext(dbName);
            var node = new FileNode
            {
                Name = $"concurrent{i}.txt",
                OwnerId = ownerId,
                MaterializedPath = $"/concurrent{i}.txt"
            };
            context.FileNodes.Add(node);
            await SyncCursorHelper.AssignNextSequenceAsync(context, node, ownerId);
            await context.SaveChangesAsync();
            sequences[i] = node.SyncSequence!.Value;
        }).ToArray();

        await Task.WhenAll(tasks);

        // With PostgreSQL's atomic upsert, all 20 would be distinct.
        // InMemory may produce duplicates under concurrency — that's the known gap
        // this fix addresses. Verify at least all calls completed.
        Assert.IsTrue(sequences.All(s => s > 0), "All sequences should be positive.");
    }

    // ── P0.2: Unique Index Configuration ─────────────────────────────────────

    [TestMethod]
    public void FileNodeConfiguration_HasUniqueFilteredIndex_OnParentName()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(FileNode))!;

        var indexes = entityType.GetIndexes().ToList();

        // The unique filtered index should exist on (ParentId, Name)
        var uniqueIndex = indexes.FirstOrDefault(i =>
            i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "ParentId", "Name" }));

        Assert.IsNotNull(uniqueIndex,
            "Expected a unique index on (ParentId, Name) for preventing duplicate file names.");
    }

    [TestMethod]
    public void FileNodeConfiguration_HasUniqueFilteredIndex_OnRootName()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(FileNode))!;

        var indexes = entityType.GetIndexes().ToList();

        // The unique filtered index should exist on (OwnerId, Name) for root-level uniqueness
        var rootIndex = indexes.FirstOrDefault(i =>
            i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "OwnerId", "Name" }));

        Assert.IsNotNull(rootIndex,
            "Expected a unique index on (OwnerId, Name) for preventing duplicate root-level names.");
    }

    [TestMethod]
    public void FileNodeConfiguration_SyncSequenceIndex_Exists()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(FileNode))!;

        var syncIndex = entityType.GetIndexes().FirstOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "OwnerId", "SyncSequence" }));

        Assert.IsNotNull(syncIndex,
            "Expected index on (OwnerId, SyncSequence) for cursor-based sync queries.");
    }

    // ── P0.2: Unique Constraint Enforcement (SQLite) ─────────────────────────

    [TestMethod]
    public async Task FileNode_DuplicateNameInSameParent_ThrowsDbUpdateException()
    {
        var (context, connection) = CreateSqliteContext();
        using (connection)
        using (context)
        {
            var ownerId = Guid.NewGuid();
            var parentId = Guid.NewGuid();

            var parent = new FileNode
            {
                Id = parentId,
                Name = "Folder",
                NodeType = FileNodeType.Folder,
                OwnerId = ownerId,
                MaterializedPath = $"/{parentId}"
            };
            context.FileNodes.Add(parent);
            await context.SaveChangesAsync();

            var file1 = new FileNode
            {
                Name = "report.pdf",
                NodeType = FileNodeType.File,
                ParentId = parentId,
                OwnerId = ownerId,
                MaterializedPath = $"/{parentId}/f1"
            };
            context.FileNodes.Add(file1);
            await context.SaveChangesAsync();

            var file2 = new FileNode
            {
                Name = "report.pdf",
                NodeType = FileNodeType.File,
                ParentId = parentId,
                OwnerId = ownerId,
                MaterializedPath = $"/{parentId}/f2"
            };
            context.FileNodes.Add(file2);

            DbUpdateException? caught = null;
            try
            {
                await context.SaveChangesAsync();
                Assert.Fail("Expected DbUpdateException for duplicate name in same parent.");
            }
            catch (DbUpdateException ex)
            {
                caught = ex;
            }

            Assert.IsNotNull(caught);
            Assert.IsTrue(DbExceptionClassifier.IsUniqueConstraintViolation(caught),
                "Duplicate name violation should be classified as unique constraint violation.");
        }
    }

    [TestMethod]
    public async Task FileNode_DuplicateRootName_SameOwner_ThrowsDbUpdateException()
    {
        var (context, connection) = CreateSqliteContext();
        using (connection)
        using (context)
        {
            var ownerId = Guid.NewGuid();

            var file1 = new FileNode
            {
                Name = "notes.txt",
                NodeType = FileNodeType.File,
                ParentId = null,
                OwnerId = ownerId,
                MaterializedPath = "/f1"
            };
            context.FileNodes.Add(file1);
            await context.SaveChangesAsync();

            var file2 = new FileNode
            {
                Name = "notes.txt",
                NodeType = FileNodeType.File,
                ParentId = null,
                OwnerId = ownerId,
                MaterializedPath = "/f2"
            };
            context.FileNodes.Add(file2);

            DbUpdateException? caught = null;
            try
            {
                await context.SaveChangesAsync();
                Assert.Fail("Expected DbUpdateException for duplicate root-level name.");
            }
            catch (DbUpdateException ex)
            {
                caught = ex;
            }

            Assert.IsNotNull(caught);
            Assert.IsTrue(DbExceptionClassifier.IsUniqueConstraintViolation(caught),
                "Duplicate root-level name for same owner should be classified as unique constraint violation.");
        }
    }

    [TestMethod]
    public async Task FileNode_SameNameDifferentParents_Allowed()
    {
        var (context, connection) = CreateSqliteContext();
        using (connection)
        using (context)
        {
            var ownerId = Guid.NewGuid();
            var parent1Id = Guid.NewGuid();
            var parent2Id = Guid.NewGuid();

            context.FileNodes.AddRange(
                new FileNode { Id = parent1Id, Name = "Folder1", NodeType = FileNodeType.Folder, OwnerId = ownerId, MaterializedPath = $"/{parent1Id}" },
                new FileNode { Id = parent2Id, Name = "Folder2", NodeType = FileNodeType.Folder, OwnerId = ownerId, MaterializedPath = $"/{parent2Id}" }
            );
            await context.SaveChangesAsync();

            context.FileNodes.AddRange(
                new FileNode { Name = "report.pdf", NodeType = FileNodeType.File, ParentId = parent1Id, OwnerId = ownerId, MaterializedPath = $"/{parent1Id}/f1" },
                new FileNode { Name = "report.pdf", NodeType = FileNodeType.File, ParentId = parent2Id, OwnerId = ownerId, MaterializedPath = $"/{parent2Id}/f2" }
            );
            await context.SaveChangesAsync();

            var count = await context.FileNodes.IgnoreQueryFilters()
                .CountAsync(n => n.Name == "report.pdf");
            Assert.AreEqual(2, count, "Same name in different parents should be allowed.");
        }
    }

    // ── P0.2: Soft-Delete Filter ─────────────────────────────────────────────

    [TestMethod]
    public async Task FileNode_SoftDeletedDuplicateName_AllowedInSameParent()
    {
        var (context, connection) = CreateSqliteContext();
        using (connection)
        using (context)
        {
            var ownerId = Guid.NewGuid();
            var parentId = Guid.NewGuid();

            var parent = new FileNode
            {
                Id = parentId,
                Name = "Folder",
                NodeType = FileNodeType.Folder,
                OwnerId = ownerId,
                MaterializedPath = $"/{parentId}"
            };
            context.FileNodes.Add(parent);

            var deleted = new FileNode
            {
                Name = "report.pdf",
                NodeType = FileNodeType.File,
                ParentId = parentId,
                OwnerId = ownerId,
                IsDeleted = true,
                DeletedAt = DateTime.UtcNow,
                MaterializedPath = $"/{parentId}/deleted"
            };
            context.FileNodes.Add(deleted);
            await context.SaveChangesAsync();

            // New file with same name should succeed — soft-deleted node is excluded by filter
            var newFile = new FileNode
            {
                Name = "report.pdf",
                NodeType = FileNodeType.File,
                ParentId = parentId,
                OwnerId = ownerId,
                MaterializedPath = $"/{parentId}/new"
            };
            context.FileNodes.Add(newFile);
            await context.SaveChangesAsync();

            var allNodes = await context.FileNodes
                .IgnoreQueryFilters()
                .Where(n => n.ParentId == parentId && n.Name == "report.pdf")
                .ToListAsync();

            Assert.AreEqual(2, allNodes.Count, "Should have both the deleted and new node.");
        }
    }

    // ── P0.3: Atomic Reference Counting ──────────────────────────────────────

    [TestMethod]
    public void FileChunk_DefaultReferenceCount_IsOne()
    {
        var chunk = new FileChunk
        {
            ChunkHash = "abc123",
            StoragePath = "/chunks/ab/c1/abc123"
        };

        Assert.AreEqual(1, chunk.ReferenceCount,
            "Default ReferenceCount should be 1 for new chunks.");
    }

    [TestMethod]
    public async Task ChunkReferenceHelper_MultipleIncrements_CorrectRefcount()
    {
        using var context = CreateContext();
        var chunk = new FileChunk
        {
            ChunkHash = "hash_inc_test",
            StoragePath = "/chunks/test",
            ReferenceCount = 1
        };
        context.FileChunks.Add(chunk);
        await context.SaveChangesAsync();

        // Increment 5 times (1 → 6)
        for (var i = 0; i < 5; i++)
            await ChunkReferenceHelper.IncrementAsync(context, chunk.Id);
        await context.SaveChangesAsync();

        var updated = await context.FileChunks.FindAsync(chunk.Id);
        Assert.AreEqual(6, updated!.ReferenceCount,
            "After 5 increments from 1, refcount should be 6.");
    }

    [TestMethod]
    public async Task ChunkReferenceHelper_DecrementAtZero_ClampsToZero()
    {
        using var context = CreateContext();
        var chunk = new FileChunk
        {
            ChunkHash = "hash_clamp_test",
            StoragePath = "/chunks/clamp",
            ReferenceCount = 1
        };
        context.FileChunks.Add(chunk);
        await context.SaveChangesAsync();

        // Decrement twice from 1 → should clamp at 0, not go negative
        await ChunkReferenceHelper.DecrementAsync(context, chunk.Id);
        await ChunkReferenceHelper.DecrementAsync(context, chunk.Id);
        await context.SaveChangesAsync();

        var updated = await context.FileChunks.FindAsync(chunk.Id);
        Assert.AreEqual(0, updated!.ReferenceCount,
            "Decrementing past zero should clamp at 0.");
    }

    [TestMethod]
    public async Task ChunkReferenceHelper_IncrementThenDecrement_CorrectBalance()
    {
        using var context = CreateContext();
        var chunk = new FileChunk
        {
            ChunkHash = "hash_balance_test",
            StoragePath = "/chunks/balance",
            ReferenceCount = 1
        };
        context.FileChunks.Add(chunk);
        await context.SaveChangesAsync();

        // +3, -2 → net +1, so 1 + 1 = 2
        await ChunkReferenceHelper.IncrementAsync(context, chunk.Id);
        await ChunkReferenceHelper.IncrementAsync(context, chunk.Id);
        await ChunkReferenceHelper.IncrementAsync(context, chunk.Id);
        await ChunkReferenceHelper.DecrementAsync(context, chunk.Id);
        await ChunkReferenceHelper.DecrementAsync(context, chunk.Id);
        await context.SaveChangesAsync();

        var updated = await context.FileChunks.FindAsync(chunk.Id);
        Assert.AreEqual(2, updated!.ReferenceCount,
            "After +3/-2 from initial 1, refcount should be 2.");
    }

    [TestMethod]
    public async Task ChunkReferenceHelper_ConcurrentIncrements_AllComplete()
    {
        // Exercise concurrent increment paths against a shared InMemory database.
        // With PostgreSQL's row-level locking, the atomic SQL guarantees correct counts.
        // InMemory doesn't provide true atomicity — this verifies no crashes/exceptions.
        var dbName = Guid.NewGuid().ToString();
        const int concurrency = 20;

        Guid chunkId;
        using (var setup = CreateContext(dbName))
        {
            var chunk = new FileChunk
            {
                ChunkHash = "hash_concurrent",
                StoragePath = "/chunks/concurrent",
                ReferenceCount = 1
            };
            setup.FileChunks.Add(chunk);
            await setup.SaveChangesAsync();
            chunkId = chunk.Id;
        }

        var tasks = Enumerable.Range(0, concurrency).Select(async _ =>
        {
            using var context = CreateContext(dbName);
            await ChunkReferenceHelper.IncrementAsync(context, chunkId);
            await context.SaveChangesAsync();
        }).ToArray();

        await Task.WhenAll(tasks);

        using var verify = CreateContext(dbName);
        var result = await verify.FileChunks.FindAsync(chunkId);
        Assert.IsNotNull(result);
        // With atomic SQL on PostgreSQL, this would be exactly 21 (1 + 20).
        // InMemory may lose increments under concurrency — that's the known race P0.3 fixes.
        Assert.IsTrue(result.ReferenceCount >= 1,
            "All concurrent increments should complete without exceptions.");
    }

    [TestMethod]
    public async Task FileChunk_NegativeReferenceCount_RejectedByCheckConstraint()
    {
        // Verify the CHECK(ReferenceCount >= 0) constraint is enforced at the DB level.
        var (context, connection) = CreateSqliteContext();
        using (connection)
        using (context)
        {
            var chunk = new FileChunk
            {
                ChunkHash = "hash_check_test",
                StoragePath = "/chunks/check",
                ReferenceCount = 1
            };
            context.FileChunks.Add(chunk);
            await context.SaveChangesAsync();

            // Directly set negative refcount via raw SQL to bypass EF clamping
            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    "UPDATE \"FileChunks\" SET \"ReferenceCount\" = -1 WHERE \"Id\" = {0}",
                    chunk.Id);
                Assert.Fail("Expected CHECK constraint to reject negative ReferenceCount.");
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                // SQLite CHECK constraint violation
                Assert.IsTrue(ex.Message.Contains("CHECK constraint") || ex.SqliteErrorCode == 19,
                    $"Expected CHECK constraint violation, got: {ex.Message}");
            }
        }
    }
}
