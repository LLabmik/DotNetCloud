using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class SyncServiceTests
{
    private static FilesDbContext CreateContext(string? name = null)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    private static SyncService CreateService(FilesDbContext db) =>
        new(db, NullLoggerFactory.Instance.CreateLogger<SyncService>());

    private static CallerContext UserCaller(Guid userId) => new(userId, Array.Empty<string>(), CallerType.User);

    [TestMethod]
    public async Task GetChangesSinceAsync_ReturnsActiveChanges()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.FileNodes.Add(new FileNode
        {
            Name = "recent.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            UpdatedAt = now
        });
        db.FileNodes.Add(new FileNode
        {
            Name = "old.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            UpdatedAt = now.AddDays(-10)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var changes = await service.GetChangesSinceAsync(now.AddMinutes(-1), null, UserCaller(userId));

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual("recent.txt", changes[0].Name);
        Assert.IsFalse(changes[0].IsDeleted);
    }

    [TestMethod]
    public async Task GetChangesSinceAsync_IncludesDeletedNodes()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var deleted = new FileNode
        {
            Name = "deleted.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            IsDeleted = true,
            DeletedAt = now,
            DeletedByUserId = userId,
            OriginalParentId = Guid.NewGuid(),
            UpdatedAt = now.AddMinutes(-5)
        };
        deleted.MaterializedPath = $"/{deleted.Id}";
        db.FileNodes.Add(deleted);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var changes = await service.GetChangesSinceAsync(now.AddMinutes(-1), null, UserCaller(userId));

        Assert.AreEqual(1, changes.Count);
        Assert.IsTrue(changes[0].IsDeleted);
        Assert.AreEqual("deleted.txt", changes[0].Name);
    }

    [TestMethod]
    public async Task GetChangesSinceAsync_FiltersByFolder()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var folder = new FileNode { Name = "Folder", NodeType = FileNodeType.Folder, OwnerId = userId, UpdatedAt = now };
        folder.MaterializedPath = $"/{folder.Id}";
        db.FileNodes.Add(folder);

        db.FileNodes.Add(new FileNode
        {
            Name = "inside.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            ParentId = folder.Id,
            UpdatedAt = now
        });
        db.FileNodes.Add(new FileNode
        {
            Name = "outside.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var changes = await service.GetChangesSinceAsync(now.AddMinutes(-1), folder.Id, UserCaller(userId));

        // Should include the folder itself and its child, but not the outside file
        Assert.IsTrue(changes.All(c => c.Name != "outside.txt"));
        Assert.IsTrue(changes.Any(c => c.Name == "inside.txt"));
    }

    [TestMethod]
    public async Task GetChangesSinceAsync_OtherUsersNodes_NotIncluded()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.FileNodes.Add(new FileNode { Name = "mine.txt", NodeType = FileNodeType.File, OwnerId = userId, UpdatedAt = now });
        db.FileNodes.Add(new FileNode { Name = "theirs.txt", NodeType = FileNodeType.File, OwnerId = otherId, UpdatedAt = now });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var changes = await service.GetChangesSinceAsync(now.AddMinutes(-1), null, UserCaller(userId));

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual("mine.txt", changes[0].Name);
    }

    [TestMethod]
    public async Task GetFolderTreeAsync_RootLevel_ReturnsAllRootNodes()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileNodes.Add(new FileNode { Name = "file1.txt", NodeType = FileNodeType.File, OwnerId = userId });
        db.FileNodes.Add(new FileNode { Name = "Folder1", NodeType = FileNodeType.Folder, OwnerId = userId });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var tree = await service.GetFolderTreeAsync(null, UserCaller(userId));

        Assert.AreEqual("/", tree.Name);
        Assert.AreEqual(2, tree.Children.Count);
    }

    [TestMethod]
    public async Task GetFolderTreeAsync_SpecificFolder_BuildsRecursiveTree()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var parent = new FileNode { Name = "Root", NodeType = FileNodeType.Folder, OwnerId = userId };
        parent.MaterializedPath = $"/{parent.Id}";
        db.FileNodes.Add(parent);

        var child = new FileNode { Name = "child.txt", NodeType = FileNodeType.File, OwnerId = userId, ParentId = parent.Id };
        db.FileNodes.Add(child);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var tree = await service.GetFolderTreeAsync(parent.Id, UserCaller(userId));

        Assert.AreEqual("Root", tree.Name);
        Assert.AreEqual(1, tree.Children.Count);
        Assert.AreEqual("child.txt", tree.Children[0].Name);
    }

    [TestMethod]
    public async Task GetFolderTreeAsync_NonExistentFolder_ThrowsNotFoundException()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => service.GetFolderTreeAsync(Guid.NewGuid(), UserCaller(Guid.NewGuid())));
    }

    [TestMethod]
    public async Task ReconcileAsync_NewOnServer_ProducesDownloadAction()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var serverNode = new FileNode
        {
            Name = "server-only.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            ContentHash = "abc"
        };
        db.FileNodes.Add(serverNode);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ReconcileAsync(new SyncReconcileRequestDto
        {
            ClientNodes = [] // Client has nothing
        }, UserCaller(userId));

        Assert.AreEqual(1, result.Actions.Count);
        Assert.AreEqual("Download", result.Actions[0].Action);
        Assert.AreEqual(serverNode.Id, result.Actions[0].NodeId);
    }

    [TestMethod]
    public async Task ReconcileAsync_NewOnClient_ProducesUploadAction()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        // Server is empty
        await db.SaveChangesAsync();

        var clientNodeId = Guid.NewGuid();
        var service = CreateService(db);
        var result = await service.ReconcileAsync(new SyncReconcileRequestDto
        {
            ClientNodes = [new SyncClientNodeDto { NodeId = clientNodeId, ContentHash = "xyz", UpdatedAt = DateTime.UtcNow }]
        }, UserCaller(userId));

        Assert.AreEqual(1, result.Actions.Count);
        Assert.AreEqual("Upload", result.Actions[0].Action);
        Assert.AreEqual(clientNodeId, result.Actions[0].NodeId);
    }

    [TestMethod]
    public async Task ReconcileAsync_DeletedOnServer_ProducesDeleteAction()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var deleted = new FileNode
        {
            Name = "deleted.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow,
            DeletedByUserId = userId,
            OriginalParentId = Guid.NewGuid()
        };
        deleted.MaterializedPath = $"/{deleted.Id}";
        db.FileNodes.Add(deleted);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ReconcileAsync(new SyncReconcileRequestDto
        {
            ClientNodes = [new SyncClientNodeDto { NodeId = deleted.Id, ContentHash = "old", UpdatedAt = DateTime.UtcNow.AddDays(-1) }]
        }, UserCaller(userId));

        Assert.IsTrue(result.Actions.Any(a => a.Action == "Delete" && a.NodeId == deleted.Id));
    }

    [TestMethod]
    public async Task ReconcileAsync_ServerNewer_ProducesDownloadAction()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var node = new FileNode
        {
            Name = "file.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            ContentHash = "server_hash",
            UpdatedAt = now
        };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ReconcileAsync(new SyncReconcileRequestDto
        {
            ClientNodes = [new SyncClientNodeDto { NodeId = node.Id, ContentHash = "client_hash", UpdatedAt = now.AddMinutes(-5) }]
        }, UserCaller(userId));

        Assert.AreEqual(1, result.Actions.Count);
        Assert.AreEqual("Download", result.Actions[0].Action);
        Assert.AreEqual("Server is newer", result.Actions[0].Reason);
    }

    [TestMethod]
    public async Task ReconcileAsync_ClientNewer_ProducesUploadAction()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var node = new FileNode
        {
            Name = "file.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            ContentHash = "server_hash",
            UpdatedAt = now.AddMinutes(-5)
        };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ReconcileAsync(new SyncReconcileRequestDto
        {
            ClientNodes = [new SyncClientNodeDto { NodeId = node.Id, ContentHash = "client_hash", UpdatedAt = now }]
        }, UserCaller(userId));

        Assert.AreEqual(1, result.Actions.Count);
        Assert.AreEqual("Upload", result.Actions[0].Action);
        Assert.AreEqual("Client is newer", result.Actions[0].Reason);
    }

    [TestMethod]
    public async Task ReconcileAsync_SameHashAndTimestamp_NoAction()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var node = new FileNode
        {
            Name = "file.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            ContentHash = "same_hash",
            UpdatedAt = now
        };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ReconcileAsync(new SyncReconcileRequestDto
        {
            ClientNodes = [new SyncClientNodeDto { NodeId = node.Id, ContentHash = "same_hash", UpdatedAt = now }]
        }, UserCaller(userId));

        Assert.AreEqual(0, result.Actions.Count);
    }

    [TestMethod]
    public async Task ReconcileAsync_SameTimestampDifferentHash_ProducesConflict()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var node = new FileNode
        {
            Name = "file.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            ContentHash = "server_hash",
            UpdatedAt = now
        };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ReconcileAsync(new SyncReconcileRequestDto
        {
            ClientNodes = [new SyncClientNodeDto { NodeId = node.Id, ContentHash = "different_hash", UpdatedAt = now }]
        }, UserCaller(userId));

        Assert.AreEqual(1, result.Actions.Count);
        Assert.AreEqual("Conflict", result.Actions[0].Action);
    }
}
