using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class TrashServiceTests
{
    private static FilesDbContext CreateContext(string? name = null)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    private static TrashService CreateService(FilesDbContext db) =>
        new(db, Mock.Of<IFileStorageEngine>(), Mock.Of<IEventBus>(), NullLoggerFactory.Instance.CreateLogger<TrashService>());

    private static CallerContext UserCaller(Guid userId) => new(userId, Array.Empty<string>(), CallerType.User);

    private static FileNode CreateDeletedNode(Guid ownerId, Guid? originalParentId = null)
    {
        var node = new FileNode
        {
            Name = "deleted.txt",
            NodeType = FileNodeType.File,
            OwnerId = ownerId,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow,
            DeletedByUserId = ownerId,
            OriginalParentId = originalParentId ?? Guid.NewGuid()
        };
        node.MaterializedPath = $"/{node.Id}";
        return node;
    }

    [TestMethod]
    public async Task ListTrashAsync_ReturnsDeletedItems()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileNodes.Add(CreateDeletedNode(userId));
        db.FileNodes.Add(CreateDeletedNode(userId));
        // Active node should not appear
        db.FileNodes.Add(new FileNode { Name = "active.txt", OwnerId = userId });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var trash = await service.ListTrashAsync(UserCaller(userId));

        Assert.AreEqual(2, trash.Count);
    }

    [TestMethod]
    public async Task RestoreAsync_ExistingParent_RestoresToOriginalParent()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var parent = new FileNode { Name = "Parent", NodeType = FileNodeType.Folder, OwnerId = userId, Depth = 0 };
        parent.MaterializedPath = $"/{parent.Id}";
        db.FileNodes.Add(parent);

        var deleted = CreateDeletedNode(userId, parent.Id);
        db.FileNodes.Add(deleted);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.RestoreAsync(deleted.Id, UserCaller(userId));

        Assert.IsFalse(result.IsFavorite); // just checking it's a valid DTO
        Assert.AreEqual(parent.Id, result.ParentId);

        var node = await db.FileNodes.FindAsync(deleted.Id);
        Assert.IsFalse(node!.IsDeleted);
        Assert.IsNull(node.DeletedAt);
    }

    [TestMethod]
    public async Task RestoreAsync_MissingParent_RestoresToRoot()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var deleted = CreateDeletedNode(userId, Guid.NewGuid()); // Parent doesn't exist
        db.FileNodes.Add(deleted);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.RestoreAsync(deleted.Id, UserCaller(userId));

        Assert.IsNull(result.ParentId);
    }

    [TestMethod]
    public async Task RestoreAsync_NonExistent_ThrowsNotFoundException()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => service.RestoreAsync(Guid.NewGuid(), UserCaller(Guid.NewGuid())));
    }

    [TestMethod]
    public async Task RestoreAsync_NonOwner_ThrowsForbiddenException()
    {
        using var db = CreateContext();
        var deleted = CreateDeletedNode(Guid.NewGuid());
        db.FileNodes.Add(deleted);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<ForbiddenException>(
            () => service.RestoreAsync(deleted.Id, UserCaller(Guid.NewGuid())));
    }

    [TestMethod]
    public async Task PermanentDeleteAsync_RemovesNodeAndRelatedData()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateDeletedNode(userId);
        db.FileNodes.Add(node);

        // Add related data
        db.FileTags.Add(new FileTag { FileNodeId = node.Id, Name = "Tag", CreatedByUserId = userId });
        db.FileComments.Add(new FileComment { FileNodeId = node.Id, Content = "Comment", CreatedByUserId = userId });

        var chunk = new FileChunk { ChunkHash = "hash1", StoragePath = "chunks/ha/sh/hash1", Size = 100, ReferenceCount = 1 };
        db.FileChunks.Add(chunk);

        var version = new FileVersion
        {
            FileNodeId = node.Id,
            VersionNumber = 1,
            Size = 100,
            ContentHash = "hash1",
            StoragePath = "files/v1",
            CreatedByUserId = userId
        };
        db.FileVersions.Add(version);
        db.FileVersionChunks.Add(new FileVersionChunk
        {
            FileVersionId = version.Id,
            FileChunkId = chunk.Id,
            SequenceIndex = 0
        });

        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.PermanentDeleteAsync(node.Id, UserCaller(userId));

        // Node should be gone
        Assert.AreEqual(0, await db.FileNodes.IgnoreQueryFilters().CountAsync());
        Assert.AreEqual(0, await db.FileTags.CountAsync());
        Assert.AreEqual(0, await db.FileComments.IgnoreQueryFilters().CountAsync());
        Assert.AreEqual(0, await db.FileVersions.CountAsync());
        Assert.AreEqual(0, await db.FileVersionChunks.CountAsync());

        // Chunk refcount should be decremented
        var updatedChunk = await db.FileChunks.FindAsync(chunk.Id);
        Assert.AreEqual(0, updatedChunk!.ReferenceCount);
    }

    [TestMethod]
    public async Task EmptyTrashAsync_DeletesAllTrashItems()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileNodes.Add(CreateDeletedNode(userId));
        db.FileNodes.Add(CreateDeletedNode(userId));
        // Active node should be preserved
        db.FileNodes.Add(new FileNode { Name = "keep.txt", OwnerId = userId });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.EmptyTrashAsync(UserCaller(userId));

        var remaining = await db.FileNodes.IgnoreQueryFilters().CountAsync();
        Assert.AreEqual(1, remaining);

        var activeNode = await db.FileNodes.FirstAsync();
        Assert.AreEqual("keep.txt", activeNode.Name);
    }

    [TestMethod]
    public async Task GetTrashSizeAsync_ReturnsSumOfDeletedSizes()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var d1 = CreateDeletedNode(userId);
        d1.Size = 1000;
        var d2 = CreateDeletedNode(userId);
        d2.Size = 2500;
        d2.Name = "other.txt";
        db.FileNodes.AddRange(d1, d2);
        // Active node should not be counted
        db.FileNodes.Add(new FileNode { Name = "active.txt", OwnerId = userId, Size = 9999 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var size = await service.GetTrashSizeAsync(UserCaller(userId));

        Assert.AreEqual(3500, size);
    }

    [TestMethod]
    public async Task GetTrashSizeAsync_NoTrash_ReturnsZero()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileNodes.Add(new FileNode { Name = "active.txt", OwnerId = userId, Size = 500 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var size = await service.GetTrashSizeAsync(UserCaller(userId));

        Assert.AreEqual(0, size);
    }

    [TestMethod]
    public async Task GetTrashSizeAsync_OtherUsersTrash_NotCounted()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var mine = CreateDeletedNode(userId);
        mine.Size = 100;
        var theirs = CreateDeletedNode(otherId);
        theirs.Size = 9999;
        db.FileNodes.AddRange(mine, theirs);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var size = await service.GetTrashSizeAsync(UserCaller(userId));

        Assert.AreEqual(100, size);
    }

    [TestMethod]
    public async Task RestoreAsync_NameConflict_RenamesNode()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        // Active node with the same name at root
        db.FileNodes.Add(new FileNode { Name = "deleted.txt", NodeType = FileNodeType.File, OwnerId = userId });

        // Deleted node with same name (OriginalParentId = null => restores to root)
        var deleted = new FileNode
        {
            Name = "deleted.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow,
            DeletedByUserId = userId,
            OriginalParentId = Guid.NewGuid() // non-existent parent -> goes to root
        };
        deleted.MaterializedPath = $"/{deleted.Id}";
        db.FileNodes.Add(deleted);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.RestoreAsync(deleted.Id, UserCaller(userId));

        // Should be renamed to avoid conflict
        Assert.AreNotEqual("deleted.txt", result.Name);
        Assert.IsTrue(result.Name.StartsWith("deleted"));
    }

    [TestMethod]
    public async Task RestoreAsync_NoNameConflict_KeepsOriginalName()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var deleted = CreateDeletedNode(userId, Guid.NewGuid());
        deleted.Name = "unique-file.txt";
        db.FileNodes.Add(deleted);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.RestoreAsync(deleted.Id, UserCaller(userId));

        Assert.AreEqual("unique-file.txt", result.Name);
    }

    [TestMethod]
    public async Task PermanentDeleteAsync_UpdatesUserQuota()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateDeletedNode(userId);
        node.Size = 2048;
        node.NodeType = FileNodeType.File;
        db.FileNodes.Add(node);

        db.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 1_000_000, UsedBytes = 5000 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.PermanentDeleteAsync(node.Id, UserCaller(userId));

        var quota = await db.FileQuotas.FirstAsync(q => q.UserId == userId);
        Assert.AreEqual(2952L, quota.UsedBytes); // 5000 - 2048 = 2952
    }

    [TestMethod]
    public async Task PermanentDeleteAsync_QuotaNotDecremented_BelowZero()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateDeletedNode(userId);
        node.Size = 99999;
        node.NodeType = FileNodeType.File;
        db.FileNodes.Add(node);

        db.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 1_000_000, UsedBytes = 100 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.PermanentDeleteAsync(node.Id, UserCaller(userId));

        var quota = await db.FileQuotas.FirstAsync(q => q.UserId == userId);
        Assert.AreEqual(0L, quota.UsedBytes); // clamped to 0
    }

    [TestMethod]
    public async Task PermanentDeleteAsync_NoQuotaRecord_Succeeds()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateDeletedNode(userId);
        node.Size = 1024;
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        // Should not throw even if no quota record exists
        await service.PermanentDeleteAsync(node.Id, UserCaller(userId));

        Assert.AreEqual(0, await db.FileNodes.IgnoreQueryFilters().CountAsync());
    }

    [TestMethod]
    public async Task EmptyTrashAsync_UpdatesUserQuota()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        var d1 = CreateDeletedNode(userId);
        d1.Size = 1000;
        d1.NodeType = FileNodeType.File;
        var d2 = CreateDeletedNode(userId);
        d2.Name = "other.txt";
        d2.Size = 2000;
        d2.NodeType = FileNodeType.File;
        db.FileNodes.AddRange(d1, d2);

        db.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 1_000_000, UsedBytes = 10_000 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.EmptyTrashAsync(UserCaller(userId));

        var quota = await db.FileQuotas.FirstAsync(q => q.UserId == userId);
        Assert.AreEqual(7000L, quota.UsedBytes); // 10000 - 3000 = 7000
    }

    [TestMethod]
    public async Task RestoreAllAsync_RestoresAllTopLevelItems()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var d1 = CreateDeletedNode(userId, Guid.NewGuid());
        d1.Name = "file1.txt";
        var d2 = CreateDeletedNode(userId, Guid.NewGuid());
        d2.Name = "file2.txt";
        db.FileNodes.AddRange(d1, d2);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.RestoreAllAsync(UserCaller(userId));

        var restored = await db.FileNodes.Where(n => n.OwnerId == userId).ToListAsync();
        Assert.AreEqual(2, restored.Count);
        Assert.IsTrue(restored.All(n => !n.IsDeleted));
    }
}
