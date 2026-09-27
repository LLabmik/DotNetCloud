using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Files.Tests.Services;

/// <summary>
/// Tests the file version retention policy: max versions per file, max age of a version, and the
/// "versioning disabled" case (keep the current version only). Labeled versions and the newest
/// version are never removed.
/// </summary>
[TestClass]
public class VersionRetentionServiceTests
{
    private static FilesDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options);

    private static VersionRetentionService CreateService(FilesDbContext db, VersionRetentionOptions opts) =>
        new(db, TestFileVersioningSettings.From(opts), NullLogger<VersionRetentionService>.Instance);

    private static (FileNode Node, FileChunk Chunk) SeedNodeWithVersions(
        FilesDbContext db, Guid userId, int versionCount, int chunkRefCount = 1)
    {
        var node = new FileNode
        {
            Name = "file.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            CurrentVersion = versionCount,
            ContentHash = $"hash_v{versionCount}",
            StoragePath = $"files/v{versionCount}"
        };
        db.FileNodes.Add(node);

        var chunk = new FileChunk
        {
            ChunkHash = "shared_chunk",
            StoragePath = "chunks/sh/ar/shared_chunk",
            Size = 100,
            ReferenceCount = chunkRefCount
        };
        db.FileChunks.Add(chunk);

        for (var i = 1; i <= versionCount; i++)
        {
            var version = new FileVersion
            {
                FileNodeId = node.Id,
                VersionNumber = i,
                Size = 100,
                ContentHash = $"hash_v{i}",
                StoragePath = $"files/v{i}",
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow.AddDays(-(versionCount - i)) // oldest = most days ago
            };
            db.FileVersions.Add(version);
            db.FileVersionChunks.Add(new FileVersionChunk
            {
                FileVersionId = version.Id,
                FileChunkId = chunk.Id,
                SequenceIndex = 0
            });
        }

        return (node, chunk);
    }

    [TestMethod]
    public async Task RunAsync_BothPoliciesDisabled_NoVersionsDeleted()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        SeedNodeWithVersions(db, userId, versionCount: 10, chunkRefCount: 10);
        await db.SaveChangesAsync();

        var result = await CreateService(db, new VersionRetentionOptions { MaxVersionCount = 0, RetentionDays = 0 })
            .RunAsync();

        Assert.AreEqual(10, await db.FileVersions.CountAsync());
        Assert.AreEqual(0, result.VersionsDeleted);
    }

    [TestMethod]
    public async Task RunAsync_MaxVersionCount_DeletesOldestUnlabeled()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        SeedNodeWithVersions(db, userId, versionCount: 10, chunkRefCount: 10);
        await db.SaveChangesAsync();

        var result = await CreateService(db, new VersionRetentionOptions { MaxVersionCount = 5, RetentionDays = 0 })
            .RunAsync();

        Assert.AreEqual(5, await db.FileVersions.CountAsync());
        Assert.AreEqual(5, result.VersionsDeleted);
        Assert.AreEqual(1, result.FilesScanned);

        // Newest versions (6–10) should remain
        var remaining = await db.FileVersions.Select(v => v.VersionNumber).OrderBy(n => n).ToListAsync();
        CollectionAssert.AreEqual(new[] { 6, 7, 8, 9, 10 }, remaining);
    }

    [TestMethod]
    public async Task RunAsync_MaxVersionCount_NeverDeletesLabeledVersions()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var (node, _) = SeedNodeWithVersions(db, userId, versionCount: 5, chunkRefCount: 5);
        await db.SaveChangesAsync();

        // Label the oldest version so it should be protected
        var oldestVersion = await db.FileVersions
            .Where(v => v.FileNodeId == node.Id)
            .OrderBy(v => v.VersionNumber)
            .FirstAsync();
        oldestVersion.Label = "Release v1";
        await db.SaveChangesAsync();

        // MaxVersionCount = 3, excess = 5-3 = 2; oldest 2 unlabeled (v2, v3) are deleted
        // v1 is labeled and protected from deletion
        await CreateService(db, new VersionRetentionOptions { MaxVersionCount = 3, RetentionDays = 0 }).RunAsync();

        // v1 (labeled), v4, v5 remain = 3 versions
        Assert.AreEqual(3, await db.FileVersions.CountAsync());

        // v1 (labeled) must still exist
        Assert.IsNotNull(await db.FileVersions.FirstOrDefaultAsync(v => v.VersionNumber == 1 && v.FileNodeId == node.Id));
    }

    [TestMethod]
    public async Task RunAsync_RetentionDays_DeletesExpiredUnlabeled()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var node = new FileNode
        {
            Name = "file.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            CurrentVersion = 3,
            ContentHash = "hash_v3",
            StoragePath = "files/v3"
        };
        db.FileNodes.Add(node);

        var chunk = new FileChunk { ChunkHash = "ch", StoragePath = "chunks/ch", Size = 100, ReferenceCount = 3 };
        db.FileChunks.Add(chunk);

        // v1 and v2: 40 days old (expired), v3: today (recent)
        for (var i = 1; i <= 3; i++)
        {
            var v = new FileVersion
            {
                FileNodeId = node.Id,
                VersionNumber = i,
                Size = 100,
                ContentHash = $"hash_v{i}",
                StoragePath = $"files/v{i}",
                CreatedByUserId = userId,
                CreatedAt = i < 3 ? DateTime.UtcNow.AddDays(-40) : DateTime.UtcNow
            };
            db.FileVersions.Add(v);
            db.FileVersionChunks.Add(new FileVersionChunk { FileVersionId = v.Id, FileChunkId = chunk.Id, SequenceIndex = 0 });
        }

        await db.SaveChangesAsync();

        await CreateService(db, new VersionRetentionOptions { MaxVersionCount = 0, RetentionDays = 30 }).RunAsync();

        // v1 and v2 expired; v3 remains
        Assert.AreEqual(1, await db.FileVersions.CountAsync());
        Assert.IsNotNull(await db.FileVersions.FirstOrDefaultAsync(v => v.VersionNumber == 3));
    }

    [TestMethod]
    public async Task RunAsync_OnlyOneVersion_NeverDeleted()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        SeedNodeWithVersions(db, userId, versionCount: 1, chunkRefCount: 1);
        await db.SaveChangesAsync();

        await CreateService(db, new VersionRetentionOptions { MaxVersionCount = 1, RetentionDays = 1 }).RunAsync();

        Assert.AreEqual(1, await db.FileVersions.CountAsync());
    }

    [TestMethod]
    public async Task RunAsync_AllVersionsExpiredAndUnlabeled_KeepsNewestOne()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var (node, _) = SeedNodeWithVersions(db, userId, versionCount: 3, chunkRefCount: 3);
        await db.SaveChangesAsync();

        // Make all versions old
        foreach (var v in db.FileVersions.Where(v => v.FileNodeId == node.Id))
            v.CreatedAt = DateTime.UtcNow.AddDays(-60);
        await db.SaveChangesAsync();

        await CreateService(db, new VersionRetentionOptions { MaxVersionCount = 0, RetentionDays = 30 }).RunAsync();

        // Newest version (v3) must survive
        Assert.AreEqual(1, await db.FileVersions.CountAsync());
        var survivors = await db.FileVersions.Select(v => v.VersionNumber).ToListAsync();
        Assert.AreEqual(3, survivors[0]);
    }

    [TestMethod]
    public async Task RunAsync_VersionDeletion_DecrementsChunkRefcount()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        SeedNodeWithVersions(db, userId, versionCount: 5, chunkRefCount: 5);
        await db.SaveChangesAsync();

        // MaxVersionCount = 3: delete oldest 2 versions (v1 and v2)
        await CreateService(db, new VersionRetentionOptions { MaxVersionCount = 3, RetentionDays = 0 }).RunAsync();

        // 2 versions deleted → 2 refcount decrements
        var chunk = await db.FileChunks.FirstAsync();
        Assert.AreEqual(3, chunk.ReferenceCount);
    }

    [TestMethod]
    public async Task RunAsync_MultipleFiles_AppliesPolicyToEach()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();

        // File A: 8 versions
        SeedNodeWithVersions(db, userId, versionCount: 8, chunkRefCount: 8);
        // File B: 3 versions — seeded separately
        var nodeB = new FileNode { Name = "b.txt", NodeType = FileNodeType.File, OwnerId = userId, CurrentVersion = 3, ContentHash = "hb3", StoragePath = "fb3" };
        db.FileNodes.Add(nodeB);
        var chunkB = new FileChunk { ChunkHash = "cb", StoragePath = "cb", Size = 50, ReferenceCount = 3 };
        db.FileChunks.Add(chunkB);
        for (var i = 1; i <= 3; i++)
        {
            var v = new FileVersion { FileNodeId = nodeB.Id, VersionNumber = i, Size = 50, ContentHash = $"hb{i}", StoragePath = $"fb{i}", CreatedByUserId = userId };
            db.FileVersions.Add(v);
            db.FileVersionChunks.Add(new FileVersionChunk { FileVersionId = v.Id, FileChunkId = chunkB.Id, SequenceIndex = 0 });
        }
        await db.SaveChangesAsync();

        // MaxVersionCount = 5: File A (8→5), File B (3→3, already within limit)
        var result = await CreateService(db, new VersionRetentionOptions { MaxVersionCount = 5, RetentionDays = 0 }).RunAsync();

        Assert.AreEqual(5 + 3, await db.FileVersions.CountAsync());
        Assert.AreEqual(2, result.FilesScanned);
        Assert.AreEqual(3, result.VersionsDeleted);
    }

    [TestMethod]
    public async Task RunAsync_VersioningDisabled_KeepsOnlyTheCurrentVersion()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var (node, _) = SeedNodeWithVersions(db, userId, versionCount: 6, chunkRefCount: 6);
        await db.SaveChangesAsync();

        // Versioning off, and even with the count limit set to "unlimited" (0) the policy must still
        // reduce the file to its current version — otherwise disabled versioning would keep growing.
        var result = await CreateService(db, new VersionRetentionOptions
        {
            Enabled = false,
            MaxVersionCount = 0,
            RetentionDays = 0
        }).RunAsync();

        Assert.AreEqual(1, await db.FileVersions.CountAsync());
        Assert.AreEqual(5, result.VersionsDeleted);
        Assert.IsFalse(result.VersioningEnabled);

        // The version that remains must be the current one (the content the file serves).
        var survivor = await db.FileVersions.SingleAsync();
        Assert.AreEqual(6, survivor.VersionNumber);
        Assert.AreEqual(node.Id, survivor.FileNodeId);
    }

    [TestMethod]
    public async Task RunAsync_VersioningDisabled_KeepsLabeledVersions()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var (node, _) = SeedNodeWithVersions(db, userId, versionCount: 4, chunkRefCount: 4);
        await db.SaveChangesAsync();

        var labeled = await db.FileVersions
            .Where(v => v.FileNodeId == node.Id)
            .OrderBy(v => v.VersionNumber)
            .FirstAsync();
        labeled.Label = "Keep me";
        await db.SaveChangesAsync();

        await CreateService(db, new VersionRetentionOptions { Enabled = false, MaxVersionCount = 50 }).RunAsync();

        // The labeled version and the current version survive; the two in between are released.
        Assert.AreEqual(2, await db.FileVersions.CountAsync());
        Assert.IsNotNull(await db.FileVersions.FirstOrDefaultAsync(v => v.Label == "Keep me"));
        Assert.IsNotNull(await db.FileVersions.FirstOrDefaultAsync(v => v.VersionNumber == 4));
    }

    [TestMethod]
    public async Task RunAsync_VersioningEnabled_KeepsHistoryWithinTheCountLimit()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        SeedNodeWithVersions(db, userId, versionCount: 6, chunkRefCount: 6);
        await db.SaveChangesAsync();

        await CreateService(db, new VersionRetentionOptions { Enabled = true, MaxVersionCount = 50 }).RunAsync();

        Assert.AreEqual(6, await db.FileVersions.CountAsync());
    }
}
