using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class TagServiceTests
{
    private static FilesDbContext CreateContext(string? name = null)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    private static TagService CreateService(FilesDbContext db) =>
        new(db, NullLoggerFactory.Instance.CreateLogger<TagService>());

    private static CallerContext UserCaller(Guid userId) => new(userId, Array.Empty<string>(), CallerType.User);

    private static FileNode CreateFileNode(Guid ownerId) => new()
    {
        Name = "test.txt",
        NodeType = FileNodeType.File,
        OwnerId = ownerId
    };

    [TestMethod]
    public async Task AddTagAsync_ValidInput_CreatesTag()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateFileNode(userId);
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.AddTagAsync(node.Id, "Important", "#FF0000", UserCaller(userId));

        Assert.AreEqual("Important", result.Name);
        Assert.AreEqual("#FF0000", result.Color);
    }

    [TestMethod]
    public async Task AddTagAsync_DuplicateTag_ThrowsValidationException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateFileNode(userId);
        db.FileNodes.Add(node);
        db.FileTags.Add(new FileTag { FileNodeId = node.Id, Name = "Work", CreatedByUserId = userId });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => service.AddTagAsync(node.Id, "Work", null, UserCaller(userId)));
    }

    [TestMethod]
    public async Task AddTagAsync_NonOwner_ThrowsForbiddenException()
    {
        using var db = CreateContext();
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var node = CreateFileNode(ownerId);
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<ForbiddenException>(
            () => service.AddTagAsync(node.Id, "Tag", null, UserCaller(otherId)));
    }

    [TestMethod]
    public async Task RemoveTagAsync_ValidInput_RemovesTag()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateFileNode(userId);
        db.FileNodes.Add(node);
        var tag = new FileTag { FileNodeId = node.Id, Name = "ToRemove", CreatedByUserId = userId };
        db.FileTags.Add(tag);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.RemoveTagAsync(node.Id, tag.Id, UserCaller(userId));

        Assert.AreEqual(0, await db.FileTags.CountAsync());
    }

    [TestMethod]
    public async Task GetTagsAsync_ReturnsAllTagsOnNode()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateFileNode(userId);
        db.FileNodes.Add(node);
        db.FileTags.Add(new FileTag { FileNodeId = node.Id, Name = "A", CreatedByUserId = userId });
        db.FileTags.Add(new FileTag { FileNodeId = node.Id, Name = "B", CreatedByUserId = userId });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var tags = await service.GetTagsAsync(node.Id, UserCaller(userId));

        Assert.AreEqual(2, tags.Count);
    }

    [TestMethod]
    public async Task GetNodesByTagAsync_ReturnsMatchingNodes()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node1 = CreateFileNode(userId);
        var node2 = CreateFileNode(userId);
        node2.Name = "other.txt";
        db.FileNodes.AddRange(node1, node2);
        db.FileTags.Add(new FileTag { FileNodeId = node1.Id, Name = "Important", CreatedByUserId = userId });
        db.FileTags.Add(new FileTag { FileNodeId = node2.Id, Name = "Important", CreatedByUserId = userId });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var nodes = await service.GetNodesByTagAsync("Important", UserCaller(userId));

        Assert.AreEqual(2, nodes.Count);
    }
}
