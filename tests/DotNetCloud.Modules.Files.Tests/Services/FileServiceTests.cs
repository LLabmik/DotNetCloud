using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class FileServiceTests
{
    private static FilesDbContext CreateContext(string? name = null)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    private static FileService CreateService(FilesDbContext db) =>
        new(db, Mock.Of<IEventBus>(), NullLoggerFactory.Instance.CreateLogger<FileService>());

    private static CallerContext UserCaller(Guid userId) => new(userId, Array.Empty<string>(), CallerType.User);

    [TestMethod]
    public async Task CreateFolderAsync_AtRoot_CreatesSuccessfully()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var service = CreateService(db);

        var result = await service.CreateFolderAsync(
            new CreateFolderDto { Name = "MyFolder" },
            UserCaller(userId));

        Assert.AreEqual("MyFolder", result.Name);
        Assert.AreEqual("Folder", result.NodeType);
        Assert.IsNull(result.ParentId);
        Assert.AreEqual(userId, result.OwnerId);
    }

    [TestMethod]
    public async Task CreateFolderAsync_UnderParent_SetsPathAndDepth()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var parent = new FileNode
        {
            Name = "Parent",
            NodeType = FileNodeType.Folder,
            OwnerId = userId,
            MaterializedPath = "/parent-id",
            Depth = 0
        };
        parent.MaterializedPath = $"/{parent.Id}";
        db.FileNodes.Add(parent);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.CreateFolderAsync(
            new CreateFolderDto { Name = "Child", ParentId = parent.Id },
            UserCaller(userId));

        Assert.AreEqual(parent.Id, result.ParentId);
        Assert.AreEqual("Child", result.Name);
    }

    [TestMethod]
    public async Task CreateFolderAsync_DuplicateName_ThrowsValidationException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileNodes.Add(new FileNode { Name = "Existing", NodeType = FileNodeType.Folder, OwnerId = userId });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => service.CreateFolderAsync(
                new CreateFolderDto { Name = "Existing" },
                UserCaller(userId)));
    }

    [TestMethod]
    public async Task GetNodeAsync_ExistingNode_ReturnsDto()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "file.txt", NodeType = FileNodeType.File, OwnerId = userId };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetNodeAsync(node.Id, UserCaller(userId));

        Assert.IsNotNull(result);
        Assert.AreEqual("file.txt", result.Name);
    }

    [TestMethod]
    public async Task GetNodeAsync_NonExistent_ReturnsNull()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        var result = await service.GetNodeAsync(Guid.NewGuid(), UserCaller(Guid.NewGuid()));
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ListChildrenAsync_ReturnsChildrenSorted()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var parent = new FileNode { Name = "Root", NodeType = FileNodeType.Folder, OwnerId = userId };
        db.FileNodes.Add(parent);
        db.FileNodes.Add(new FileNode { Name = "b.txt", NodeType = FileNodeType.File, OwnerId = userId, ParentId = parent.Id });
        db.FileNodes.Add(new FileNode { Name = "a.txt", NodeType = FileNodeType.File, OwnerId = userId, ParentId = parent.Id });
        db.FileNodes.Add(new FileNode { Name = "SubFolder", NodeType = FileNodeType.Folder, OwnerId = userId, ParentId = parent.Id });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var children = await service.ListChildrenAsync(parent.Id, UserCaller(userId));

        Assert.AreEqual(3, children.Count);
        // Files (enum 0) come first sorted by name, then folders (enum 1)
        Assert.AreEqual("a.txt", children[0].Name);
        Assert.AreEqual("b.txt", children[1].Name);
        Assert.AreEqual("SubFolder", children[2].Name);
    }

    [TestMethod]
    public async Task RenameAsync_ValidInput_UpdatesName()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "old.txt", NodeType = FileNodeType.File, OwnerId = userId };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.RenameAsync(node.Id, new RenameNodeDto { Name = "new.txt" }, UserCaller(userId));

        Assert.AreEqual("new.txt", result.Name);
    }

    [TestMethod]
    public async Task RenameAsync_NonOwner_ThrowsForbiddenException()
    {
        using var db = CreateContext();
        var ownerId = Guid.NewGuid();
        var node = new FileNode { Name = "file.txt", NodeType = FileNodeType.File, OwnerId = ownerId };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<ForbiddenException>(
            () => service.RenameAsync(node.Id, new RenameNodeDto { Name = "hacked.txt" }, UserCaller(Guid.NewGuid())));
    }

    [TestMethod]
    public async Task MoveAsync_ValidMove_UpdatesParent()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var source = new FileNode
        {
            Name = "file.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            MaterializedPath = "/file"
        };
        source.MaterializedPath = $"/{source.Id}";
        var target = new FileNode
        {
            Name = "Target",
            NodeType = FileNodeType.Folder,
            OwnerId = userId,
            Depth = 0
        };
        target.MaterializedPath = $"/{target.Id}";
        db.FileNodes.AddRange(source, target);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.MoveAsync(source.Id, new MoveNodeDto { TargetParentId = target.Id }, UserCaller(userId));

        Assert.AreEqual(target.Id, result.ParentId);
    }

    [TestMethod]
    public async Task MoveAsync_IntoSelf_ThrowsValidationException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var folder = new FileNode { Name = "Folder", NodeType = FileNodeType.Folder, OwnerId = userId };
        folder.MaterializedPath = $"/{folder.Id}";
        db.FileNodes.Add(folder);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => service.MoveAsync(folder.Id, new MoveNodeDto { TargetParentId = folder.Id }, UserCaller(userId)));
    }

    [TestMethod]
    public async Task DeleteAsync_SoftDeletesNode()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "file.txt", NodeType = FileNodeType.File, OwnerId = userId };
        node.MaterializedPath = $"/{node.Id}";
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.DeleteAsync(node.Id, UserCaller(userId));

        // Query filters should hide it
        Assert.AreEqual(0, await db.FileNodes.CountAsync());

        // But it exists with IgnoreQueryFilters
        var deleted = await db.FileNodes.IgnoreQueryFilters().FirstAsync(n => n.Id == node.Id);
        Assert.IsTrue(deleted.IsDeleted);
        Assert.IsNotNull(deleted.DeletedAt);
    }

    [TestMethod]
    public async Task ToggleFavoriteAsync_TogglesState()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "file.txt", NodeType = FileNodeType.File, OwnerId = userId };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result1 = await service.ToggleFavoriteAsync(node.Id, UserCaller(userId));
        Assert.IsTrue(result1.IsFavorite);

        var result2 = await service.ToggleFavoriteAsync(node.Id, UserCaller(userId));
        Assert.IsFalse(result2.IsFavorite);
    }

    [TestMethod]
    public async Task SearchAsync_ByName_ReturnsMatches()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileNodes.Add(new FileNode { Name = "report.pdf", NodeType = FileNodeType.File, OwnerId = userId });
        db.FileNodes.Add(new FileNode { Name = "report-final.pdf", NodeType = FileNodeType.File, OwnerId = userId });
        db.FileNodes.Add(new FileNode { Name = "notes.txt", NodeType = FileNodeType.File, OwnerId = userId });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.SearchAsync("report", 1, 10, UserCaller(userId));

        Assert.AreEqual(2, result.TotalCount);
        Assert.AreEqual(2, result.Items.Count);
    }

    [TestMethod]
    public async Task CopyAsync_File_CreatesCopy()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var source = new FileNode
        {
            Name = "file.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            Size = 100,
            MimeType = "text/plain",
            ContentHash = "abc123"
        };
        source.MaterializedPath = $"/{source.Id}";
        var target = new FileNode { Name = "Target", NodeType = FileNodeType.Folder, OwnerId = userId, Depth = 0 };
        target.MaterializedPath = $"/{target.Id}";
        db.FileNodes.AddRange(source, target);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.CopyAsync(source.Id, target.Id, UserCaller(userId));

        Assert.AreNotEqual(source.Id, result.Id);
        Assert.AreEqual("file.txt", result.Name);
        Assert.AreEqual(target.Id, result.ParentId);
        Assert.AreEqual(100, result.Size);
    }
}
