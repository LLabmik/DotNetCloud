using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class StorageMetricsServiceTests
{
    private static FilesDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [TestMethod]
    public async Task GetDeduplicationMetricsAsync_EmptyDatabase_ReturnsZeros()
    {
        using var db = CreateContext();
        var service = new StorageMetricsService(db);

        var metrics = await service.GetDeduplicationMetricsAsync();

        Assert.AreEqual(0, metrics.PhysicalStorageBytes);
        Assert.AreEqual(0, metrics.LogicalStorageBytes);
        Assert.AreEqual(0, metrics.DeduplicationSavingsBytes);
        Assert.AreEqual(0, metrics.TotalUniqueChunks);
        Assert.AreEqual(0, metrics.TotalVersions);
        Assert.AreEqual(0, metrics.TotalFiles);
    }

    [TestMethod]
    public async Task GetDeduplicationMetricsAsync_NoDedup_SavingsIsZero()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        // One file, one version, one chunk — no duplication
        var node = new FileNode { Name = "test.txt", NodeType = FileNodeType.File, OwnerId = userId };
        db.FileNodes.Add(node);

        var chunk = new FileChunk { ChunkHash = "hash1", StoragePath = "chunks/ha/sh/hash1", Size = 1000, ReferenceCount = 1 };
        db.FileChunks.Add(chunk);

        var version = new FileVersion
        {
            FileNodeId = node.Id,
            VersionNumber = 1,
            Size = 1000,
            ContentHash = "hash1",
            StoragePath = "files/ha/sh/hash1",
            CreatedByUserId = userId
        };
        db.FileVersions.Add(version);

        await db.SaveChangesAsync();

        var service = new StorageMetricsService(db);
        var metrics = await service.GetDeduplicationMetricsAsync();

        Assert.AreEqual(1000, metrics.PhysicalStorageBytes);
        Assert.AreEqual(1000, metrics.LogicalStorageBytes);
        Assert.AreEqual(0, metrics.DeduplicationSavingsBytes);
        Assert.AreEqual(1, metrics.TotalUniqueChunks);
        Assert.AreEqual(1, metrics.TotalVersions);
        Assert.AreEqual(1, metrics.TotalFiles);
    }

    [TestMethod]
    public async Task GetDeduplicationMetricsAsync_WithDedup_ReportsSavings()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        // Two files sharing the same chunk
        var node1 = new FileNode { Name = "file1.txt", NodeType = FileNodeType.File, OwnerId = userId };
        var node2 = new FileNode { Name = "file2.txt", NodeType = FileNodeType.File, OwnerId = userId };
        db.FileNodes.AddRange(node1, node2);

        // One physical chunk, referenced twice
        var chunk = new FileChunk { ChunkHash = "shared_hash", StoragePath = "chunks/sh/ar/shared_hash", Size = 500, ReferenceCount = 2 };
        db.FileChunks.Add(chunk);

        var version1 = new FileVersion
        {
            FileNodeId = node1.Id,
            VersionNumber = 1,
            Size = 500,
            ContentHash = "shared_hash",
            StoragePath = "files/sh/ar/shared_hash",
            CreatedByUserId = userId
        };
        var version2 = new FileVersion
        {
            FileNodeId = node2.Id,
            VersionNumber = 1,
            Size = 500,
            ContentHash = "shared_hash",
            StoragePath = "files/sh/ar/shared_hash",
            CreatedByUserId = userId
        };
        db.FileVersions.AddRange(version1, version2);

        await db.SaveChangesAsync();

        var service = new StorageMetricsService(db);
        var metrics = await service.GetDeduplicationMetricsAsync();

        // Physical: 500 bytes (one chunk stored once)
        // Logical: 1000 bytes (two versions each claim 500 bytes)
        // Savings: 500 bytes
        Assert.AreEqual(500, metrics.PhysicalStorageBytes);
        Assert.AreEqual(1000, metrics.LogicalStorageBytes);
        Assert.AreEqual(500, metrics.DeduplicationSavingsBytes);
        Assert.AreEqual(1, metrics.TotalUniqueChunks);
        Assert.AreEqual(2, metrics.TotalVersions);
        Assert.AreEqual(2, metrics.TotalFiles);
    }

    [TestMethod]
    public async Task GetDeduplicationMetricsAsync_OrphanedChunks_ExcludedFromPhysical()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        // A chunk that hasn't been referenced yet (ReferenceCount = 0)
        db.FileChunks.Add(new FileChunk
        {
            ChunkHash = "orphan_hash",
            StoragePath = "chunks/or/ph/orphan_hash",
            Size = 200,
            ReferenceCount = 0
        });

        await db.SaveChangesAsync();

        var service = new StorageMetricsService(db);
        var metrics = await service.GetDeduplicationMetricsAsync();

        // Orphaned chunk excluded from physical count
        Assert.AreEqual(0, metrics.PhysicalStorageBytes);
        Assert.AreEqual(0, metrics.TotalUniqueChunks);
    }
}
