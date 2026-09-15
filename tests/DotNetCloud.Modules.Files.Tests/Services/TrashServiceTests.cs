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
            .UseInMemoryDatabase(name ?? Guid.CreateVersion7().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    private static TrashService CreateService(FilesDbContext db) =>
        new(db, Mock.Of<IFileStorageEngine>(), Mock.Of<IEventBus>(), Mock.Of<ISyncChangeNotifier>(), NullLoggerFactory.Instance.CreateLogger<TrashService>());

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
            OriginalParentId = originalParentId ?? Guid.CreateVersion7()
        };
        node.MaterializedPath = $"/{node.Id}";
        return node;
    }

    [TestMethod]
    public async Task ListTrashAsync_ReturnsDeletedItems()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
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
        var userId = Guid.CreateVersion7();
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
        var userId = Guid.CreateVersion7();
        var deleted = CreateDeletedNode(userId, Guid.CreateVersion7()); // Parent doesn't exist
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
            () => service.RestoreAsync(Guid.CreateVersion7(), UserCaller(Guid.CreateVersion7())));
    }

    [TestMethod]
    public async Task RestoreAsync_NonOwner_ThrowsForbiddenException()
    {
        using var db = CreateContext();
        var deleted = CreateDeletedNode(Guid.CreateVersion7());
        db.FileNodes.Add(deleted);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<ForbiddenException>(
            () => service.RestoreAsync(deleted.Id, UserCaller(Guid.CreateVersion7())));
    }

    [TestMethod]
    public async Task PermanentDeleteAsync_RemovesNodeAndRelatedData()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
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
        var userId = Guid.CreateVersion7();
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
        var userId = Guid.CreateVersion7();
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
        var userId = Guid.CreateVersion7();
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
        var userId = Guid.CreateVersion7();
        var otherId = Guid.CreateVersion7();
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
        var userId = Guid.CreateVersion7();

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
            OriginalParentId = Guid.CreateVersion7() // non-existent parent -> goes to root
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
        var userId = Guid.CreateVersion7();
        var deleted = CreateDeletedNode(userId, Guid.CreateVersion7());
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
        var userId = Guid.CreateVersion7();
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
        var userId = Guid.CreateVersion7();
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
        var userId = Guid.CreateVersion7();
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
        var userId = Guid.CreateVersion7();

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
        var userId = Guid.CreateVersion7();
        var d1 = CreateDeletedNode(userId, Guid.CreateVersion7());
        d1.Name = "file1.txt";
        var d2 = CreateDeletedNode(userId, Guid.CreateVersion7());
        d2.Name = "file2.txt";
        db.FileNodes.AddRange(d1, d2);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.RestoreAllAsync(UserCaller(userId));

        var restored = await db.FileNodes.Where(n => n.OwnerId == userId).ToListAsync();
        Assert.AreEqual(2, restored.Count);
        Assert.IsTrue(restored.All(n => !n.IsDeleted));
    }

    // ---------------------------------------------------------------------------------------------
    // Cascade delete -> restore round trip (regression coverage for trash restore losing the
    // original directory path).
    // ---------------------------------------------------------------------------------------------

    private static FileNode CreateFolder(Guid ownerId, string name, FileNode? parent)
    {
        var folder = new FileNode
        {
            Name = name,
            NodeType = FileNodeType.Folder,
            OwnerId = ownerId,
            ParentId = parent?.Id,
            Depth = parent is null ? 0 : parent.Depth + 1
        };
        folder.MaterializedPath = parent is null
            ? $"/{folder.Id}"
            : $"{parent.MaterializedPath}/{folder.Id}";
        return folder;
    }

    private static FileNode CreateFile(Guid ownerId, string name, FileNode parent)
    {
        var file = new FileNode
        {
            Name = name,
            NodeType = FileNodeType.File,
            OwnerId = ownerId,
            ParentId = parent.Id,
            Depth = parent.Depth + 1
        };
        file.MaterializedPath = $"{parent.MaterializedPath}/{file.Id}";
        return file;
    }

    /// <summary>
    /// Soft-deletes a node the way <c>FileService.DeleteAsync</c> does: the node records its parent in
    /// <see cref="FileNode.OriginalParentId"/> and detaches itself, while the whole subtree is marked
    /// deleted in one cascade and keeps its materialized paths.
    /// </summary>
    private static void SimulateCascadeDelete(FilesDbContext db, FileNode node)
    {
        node.IsDeleted = true;
        node.DeletedAt = DateTime.UtcNow;
        node.DeletedByUserId = node.OwnerId;
        node.OriginalParentId = node.ParentId;
        node.ParentId = null;

        var descendants = db.FileNodes.Local
            .Where(n => n.MaterializedPath.StartsWith(node.MaterializedPath + "/"))
            .ToList();

        foreach (var descendant in descendants)
        {
            descendant.IsDeleted = true;
            descendant.DeletedAt = DateTime.UtcNow;
            descendant.DeletedByUserId = descendant.OwnerId;
            descendant.OriginalParentId = descendant.ParentId;
        }
    }

    [TestMethod]
    public async Task ListTrashAsync_ReturnsNameBasedOriginalPath()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var photos = CreateFolder(userId, "Photos", null);
        var file = CreateFile(userId, "cat.jpg", photos);
        db.FileNodes.AddRange(photos, file);
        await db.SaveChangesAsync();

        SimulateCascadeDelete(db, file);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var trash = await service.ListTrashAsync(UserCaller(userId));

        var item = trash.Single(t => t.Id == file.Id);
        Assert.AreEqual("/Photos", item.OriginalPath);
    }

    [TestMethod]
    public async Task ListTrashAsync_ItemDeletedFromRoot_ReturnsRootPath()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var folder = CreateFolder(userId, "A", null);
        db.FileNodes.Add(folder);
        await db.SaveChangesAsync();

        SimulateCascadeDelete(db, folder);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var trash = await service.ListTrashAsync(UserCaller(userId));

        Assert.AreEqual("/", trash.Single(t => t.Id == folder.Id).OriginalPath);
    }

    [TestMethod]
    public async Task RestoreAsync_CascadeDeletedItem_RestoresAncestorChainAndOriginalPath()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var a = CreateFolder(userId, "A", null);
        var b = CreateFolder(userId, "B", a);
        var file = CreateFile(userId, "file.txt", b);
        db.FileNodes.AddRange(a, b, file);
        await db.SaveChangesAsync();

        SimulateCascadeDelete(db, a);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Restoring the deep child must rebuild A and B instead of dumping file.txt into the root.
        var result = await service.RestoreAsync(file.Id, UserCaller(userId));

        Assert.AreEqual(b.Id, result.ParentId);

        var restoredA = await db.FileNodes.IgnoreQueryFilters().SingleAsync(n => n.Id == a.Id);
        var restoredB = await db.FileNodes.IgnoreQueryFilters().SingleAsync(n => n.Id == b.Id);
        var restoredFile = await db.FileNodes.IgnoreQueryFilters().SingleAsync(n => n.Id == file.Id);

        Assert.IsFalse(restoredA.IsDeleted);
        Assert.IsFalse(restoredB.IsDeleted);
        Assert.IsFalse(restoredFile.IsDeleted);

        Assert.IsNull(restoredA.ParentId);
        Assert.AreEqual(a.Id, restoredB.ParentId);
        Assert.AreEqual(b.Id, restoredFile.ParentId);

        Assert.AreEqual($"/{a.Id}", restoredA.MaterializedPath);
        Assert.AreEqual($"/{a.Id}/{b.Id}", restoredB.MaterializedPath);
        Assert.AreEqual($"/{a.Id}/{b.Id}/{file.Id}", restoredFile.MaterializedPath);

        Assert.AreEqual(0, restoredA.Depth);
        Assert.AreEqual(1, restoredB.Depth);
        Assert.AreEqual(2, restoredFile.Depth);

        Assert.IsNull(restoredA.OriginalParentId);
        Assert.IsNull(restoredB.OriginalParentId);
        Assert.IsNull(restoredFile.OriginalParentId);

        // The trash is consumed by the restore — no orphaned descendants left behind.
        Assert.AreEqual(0, (await service.ListTrashAsync(UserCaller(userId))).Count);
    }

    [TestMethod]
    public async Task RestoreAsync_CascadeDeletedFolder_RestoresDescendantsAtOriginalPaths()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var a = CreateFolder(userId, "A", null);
        var b = CreateFolder(userId, "B", a);
        var file = CreateFile(userId, "file.txt", b);
        var other = CreateFile(userId, "other.txt", a);
        db.FileNodes.AddRange(a, b, file, other);
        await db.SaveChangesAsync();

        SimulateCascadeDelete(db, a);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.RestoreAsync(a.Id, UserCaller(userId));

        Assert.IsNull(result.ParentId); // restored to the root level

        var restored = await db.FileNodes.IgnoreQueryFilters().ToListAsync();
        var restoredB = restored.Single(n => n.Id == b.Id);
        var restoredFile = restored.Single(n => n.Id == file.Id);
        var restoredOther = restored.Single(n => n.Id == other.Id);

        Assert.AreEqual($"/{a.Id}/{b.Id}", restoredB.MaterializedPath);
        Assert.AreEqual($"/{a.Id}/{b.Id}/{file.Id}", restoredFile.MaterializedPath);
        Assert.AreEqual($"/{a.Id}/{other.Id}", restoredOther.MaterializedPath);

        Assert.AreEqual(1, restoredB.Depth);
        Assert.AreEqual(2, restoredFile.Depth);
        Assert.AreEqual(1, restoredOther.Depth);

        Assert.IsTrue(restored.All(n => !n.IsDeleted));
        Assert.IsTrue(restored.All(n => n.OriginalParentId is null));
        Assert.AreEqual(1, restored.Count(n => n.ParentId == null));
    }

    [TestMethod]
    public async Task RestoreAsync_CascadeDeletedItem_RestoresIntoLiveAncestor()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var root = CreateFolder(userId, "Root", null);
        var child = CreateFolder(userId, "Child", root);
        var file = CreateFile(userId, "file.txt", child);
        db.FileNodes.AddRange(root, child, file);
        await db.SaveChangesAsync();

        // Only "Child" is trashed (with its subtree); "Root" stays live.
        SimulateCascadeDelete(db, child);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.RestoreAsync(file.Id, UserCaller(userId));

        var restoredChild = await db.FileNodes.IgnoreQueryFilters().SingleAsync(n => n.Id == child.Id);
        var restoredFile = await db.FileNodes.IgnoreQueryFilters().SingleAsync(n => n.Id == file.Id);

        Assert.AreEqual(root.Id, restoredChild.ParentId);
        Assert.AreEqual($"/{root.Id}/{child.Id}", restoredChild.MaterializedPath);
        Assert.AreEqual(child.Id, restoredFile.ParentId);
        Assert.AreEqual($"/{root.Id}/{child.Id}/{file.Id}", restoredFile.MaterializedPath);
        Assert.AreEqual(1, restoredChild.Depth);
        Assert.AreEqual(2, restoredFile.Depth);
    }

    [TestMethod]
    public async Task RestoreAsync_CascadeDeletedItem_WhenNameTaken_RenamesRestoredRootOnly()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var a = CreateFolder(userId, "A", null);
        var file = CreateFile(userId, "file.txt", a);
        db.FileNodes.AddRange(a, file);
        await db.SaveChangesAsync();

        SimulateCascadeDelete(db, a);
        await db.SaveChangesAsync();

        // A live folder named "A" now occupies the root level.
        var liveA = CreateFolder(userId, "A", null);
        db.FileNodes.Add(liveA);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.RestoreAsync(file.Id, UserCaller(userId));

        var restoredA = await db.FileNodes.IgnoreQueryFilters().SingleAsync(n => n.Id == a.Id);
        var restoredFile = await db.FileNodes.IgnoreQueryFilters().SingleAsync(n => n.Id == file.Id);

        Assert.AreEqual("A (1)", restoredA.Name);
        Assert.IsNull(restoredA.ParentId);
        Assert.AreEqual($"/{a.Id}", restoredA.MaterializedPath);

        // The file inside the renamed folder keeps its own name and follows the new path.
        Assert.AreEqual("file.txt", restoredFile.Name);
        Assert.AreEqual(a.Id, restoredFile.ParentId);
        Assert.AreEqual($"/{a.Id}/{file.Id}", restoredFile.MaterializedPath);
    }

    [TestMethod]
    public async Task RestoreAllAsync_CascadeDeletedFolder_RebuildsStructureWithoutFlatteningIntoRoot()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var a = CreateFolder(userId, "A", null);
        var b = CreateFolder(userId, "B", a);
        var file = CreateFile(userId, "file.txt", b);
        var other = CreateFile(userId, "other.txt", a);
        var rootFile = CreateFile(userId, "root.txt", a);
        db.FileNodes.AddRange(a, b, file, other, rootFile);
        await db.SaveChangesAsync();

        SimulateCascadeDelete(db, a);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.RestoreAllAsync(UserCaller(userId));

        var restored = await db.FileNodes.IgnoreQueryFilters().ToListAsync();

        Assert.AreEqual(5, restored.Count);
        Assert.IsTrue(restored.All(n => !n.IsDeleted));

        // Exactly one node is back at the root level, and the whole tree hangs off it.
        Assert.AreEqual(1, restored.Count(n => n.ParentId == null));
        Assert.AreEqual(a.Id, restored.Single(n => n.ParentId == null).Id);

        var restoredFile = restored.Single(n => n.Id == file.Id);
        var restoredB = restored.Single(n => n.Id == b.Id);

        Assert.AreEqual($"/{a.Id}/{b.Id}", restoredB.MaterializedPath);
        Assert.AreEqual($"/{a.Id}/{b.Id}/{file.Id}", restoredFile.MaterializedPath);
        Assert.AreEqual($"/{a.Id}/{rootFile.Id}", restored.Single(n => n.Id == rootFile.Id).MaterializedPath);
        Assert.AreEqual(2, restoredFile.Depth);

        Assert.AreEqual(0, (await service.ListTrashAsync(UserCaller(userId))).Count);
    }

    [TestMethod]
    public async Task RestoreAllAsync_MultipleDeletedRootLevelFolders_RestoresEachStructure()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var one = CreateFolder(userId, "One", null);
        var oneChild = CreateFile(userId, "one.txt", one);
        var two = CreateFolder(userId, "Two", null);
        var twoChild = CreateFile(userId, "two.txt", two);
        db.FileNodes.AddRange(one, oneChild, two, twoChild);
        await db.SaveChangesAsync();

        SimulateCascadeDelete(db, one);
        SimulateCascadeDelete(db, two);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.RestoreAllAsync(UserCaller(userId));

        var restored = await db.FileNodes.IgnoreQueryFilters().ToListAsync();

        Assert.AreEqual(4, restored.Count);
        Assert.IsTrue(restored.All(n => !n.IsDeleted));
        Assert.AreEqual(2, restored.Count(n => n.ParentId == null));
        Assert.AreEqual($"/{one.Id}/{oneChild.Id}", restored.Single(n => n.Id == oneChild.Id).MaterializedPath);
        Assert.AreEqual($"/{two.Id}/{twoChild.Id}", restored.Single(n => n.Id == twoChild.Id).MaterializedPath);
    }
}
