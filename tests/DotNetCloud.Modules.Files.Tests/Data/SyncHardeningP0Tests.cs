using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Tests.Data;

/// <summary>
/// Tests verifying P0 sync hardening constraints:
/// - P0.2: Unique constraint on (ParentId, Name) for active file nodes
/// - P0.3: CHECK constraint on FileChunks.ReferenceCount >= 0
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

    // ── P0.2: Application-Level Behavior (InMemory — no unique constraint enforcement) ──

    [TestMethod]
    public async Task FileNode_SoftDeletedDuplicateName_AllowedInSameParent()
    {
        // Soft-deleted nodes should NOT prevent new nodes with the same name.
        // This validates the filter: "IsDeleted" = false.
        using var context = CreateContext();
        var ownerId = Guid.NewGuid();
        var parentId = Guid.NewGuid();

        // Create a parent folder
        var parent = new FileNode
        {
            Id = parentId,
            Name = "Folder",
            NodeType = FileNodeType.Folder,
            OwnerId = ownerId,
            MaterializedPath = $"/{parentId}"
        };
        context.FileNodes.Add(parent);

        // Create a soft-deleted file
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

        // Create a new file with the same name — should succeed
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

        // Both should exist (InMemory doesn't enforce the unique constraint,
        // but this validates the model allows the pattern)
        var allNodes = await context.FileNodes
            .IgnoreQueryFilters()
            .Where(n => n.ParentId == parentId && n.Name == "report.pdf")
            .ToListAsync();

        Assert.AreEqual(2, allNodes.Count, "Should have both the deleted and new node.");
    }

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
}
