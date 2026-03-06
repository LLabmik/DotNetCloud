using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
        new(db, NullLoggerFactory.Instance.CreateLogger<TagService>(), new PermissionService(db));

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

    [TestMethod]
    public async Task RemoveTagByNameAsync_ExistingTag_RemovesIt()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateFileNode(userId);
        db.FileNodes.Add(node);
        db.FileTags.Add(new FileTag { FileNodeId = node.Id, Name = "Work", CreatedByUserId = userId });
        db.FileTags.Add(new FileTag { FileNodeId = node.Id, Name = "Keep", CreatedByUserId = userId });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.RemoveTagByNameAsync(node.Id, "Work", UserCaller(userId));

        Assert.AreEqual(1, await db.FileTags.CountAsync());
        Assert.AreEqual("Keep", (await db.FileTags.FirstAsync()).Name);
    }

    [TestMethod]
    public async Task RemoveTagByNameAsync_NonExistentTag_ThrowsNotFoundException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateFileNode(userId);
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => service.RemoveTagByNameAsync(node.Id, "NoSuchTag", UserCaller(userId)));
    }

    [TestMethod]
    public async Task GetAllUserTagsAsync_ReturnsDistinctTagNames()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node1 = CreateFileNode(userId);
        var node2 = CreateFileNode(userId);
        node2.Name = "other.txt";
        db.FileNodes.AddRange(node1, node2);
        db.FileTags.Add(new FileTag { FileNodeId = node1.Id, Name = "Work", CreatedByUserId = userId });
        db.FileTags.Add(new FileTag { FileNodeId = node2.Id, Name = "Work", CreatedByUserId = userId }); // Duplicate name
        db.FileTags.Add(new FileTag { FileNodeId = node1.Id, Name = "Personal", CreatedByUserId = userId });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var tags = await service.GetAllUserTagsAsync(UserCaller(userId));

        Assert.AreEqual(2, tags.Count);
        Assert.AreEqual("Personal", tags[0]); // Alphabetical order
        Assert.AreEqual("Work", tags[1]);
    }

    [TestMethod]
    public async Task GetAllUserTagsAsync_NoTags_ReturnsEmpty()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        var service = CreateService(db);
        var tags = await service.GetAllUserTagsAsync(UserCaller(userId));

        Assert.AreEqual(0, tags.Count);
    }

    [TestMethod]
    public async Task GetAllUserTagsAsync_OtherUsersTags_NotIncluded()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var myNode = CreateFileNode(userId);
        var theirNode = CreateFileNode(otherId);
        theirNode.Name = "other.txt";
        db.FileNodes.AddRange(myNode, theirNode);
        db.FileTags.Add(new FileTag { FileNodeId = myNode.Id, Name = "Mine", CreatedByUserId = userId });
        db.FileTags.Add(new FileTag { FileNodeId = theirNode.Id, Name = "Theirs", CreatedByUserId = otherId });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var tags = await service.GetAllUserTagsAsync(UserCaller(userId));

        Assert.AreEqual(1, tags.Count);
        Assert.AreEqual("Mine", tags[0]);
    }

    [TestMethod]
    public async Task GetUserTagSummariesAsync_ReturnsSummariesWithCounts()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node1 = CreateFileNode(userId);
        var node2 = CreateFileNode(userId);
        node2.Name = "other.txt";
        db.FileNodes.AddRange(node1, node2);
        db.FileTags.Add(new FileTag { FileNodeId = node1.Id, Name = "Work", Color = "#ff0000", CreatedByUserId = userId });
        db.FileTags.Add(new FileTag { FileNodeId = node2.Id, Name = "Work", Color = "#ff0000", CreatedByUserId = userId });
        db.FileTags.Add(new FileTag { FileNodeId = node1.Id, Name = "Personal", Color = "#00ff00", CreatedByUserId = userId });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var summaries = await service.GetUserTagSummariesAsync(UserCaller(userId));

        Assert.AreEqual(2, summaries.Count);
        var personal = summaries.First(s => s.Name == "Personal");
        var work = summaries.First(s => s.Name == "Work");
        Assert.AreEqual(1, personal.FileCount);
        Assert.AreEqual(2, work.FileCount);
        Assert.AreEqual("#00ff00", personal.Color);
    }

    [TestMethod]
    public async Task GetUserTagSummariesAsync_NoTags_ReturnsEmpty()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        var service = CreateService(db);
        var summaries = await service.GetUserTagSummariesAsync(UserCaller(userId));

        Assert.AreEqual(0, summaries.Count);
    }

    [TestMethod]
    public async Task BulkAddTagAsync_ValidNodes_AddsTagToAll()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node1 = CreateFileNode(userId);
        var node2 = CreateFileNode(userId);
        node2.Name = "other.txt";
        db.FileNodes.AddRange(node1, node2);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.BulkAddTagAsync([node1.Id, node2.Id], "Important", "#ff0000", UserCaller(userId));

        Assert.AreEqual(2, result.TotalCount);
        Assert.AreEqual(2, result.SuccessCount);
        Assert.AreEqual(0, result.FailureCount);
        Assert.AreEqual(2, await db.FileTags.CountAsync());
    }

    [TestMethod]
    public async Task BulkAddTagAsync_SomeNodesMissing_PartialSuccess()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateFileNode(userId);
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var missingId = Guid.NewGuid();
        var service = CreateService(db);
        var result = await service.BulkAddTagAsync([node.Id, missingId], "Work", null, UserCaller(userId));

        Assert.AreEqual(2, result.TotalCount);
        Assert.AreEqual(1, result.SuccessCount);
        Assert.AreEqual(1, result.FailureCount);
    }

    [TestMethod]
    public async Task BulkRemoveTagByNameAsync_ValidNodes_RemovesTagFromAll()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node1 = CreateFileNode(userId);
        var node2 = CreateFileNode(userId);
        node2.Name = "other.txt";
        db.FileNodes.AddRange(node1, node2);
        db.FileTags.Add(new FileTag { FileNodeId = node1.Id, Name = "Work", CreatedByUserId = userId });
        db.FileTags.Add(new FileTag { FileNodeId = node2.Id, Name = "Work", CreatedByUserId = userId });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.BulkRemoveTagByNameAsync([node1.Id, node2.Id], "Work", UserCaller(userId));

        Assert.AreEqual(2, result.TotalCount);
        Assert.AreEqual(2, result.SuccessCount);
        Assert.AreEqual(0, await db.FileTags.CountAsync());
    }

    [TestMethod]
    public async Task BulkRemoveTagByNameAsync_TagMissingOnSomeNodes_PartialSuccess()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node1 = CreateFileNode(userId);
        var node2 = CreateFileNode(userId);
        node2.Name = "other.txt";
        db.FileNodes.AddRange(node1, node2);
        db.FileTags.Add(new FileTag { FileNodeId = node1.Id, Name = "Work", CreatedByUserId = userId });
        // node2 does NOT have the "Work" tag
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.BulkRemoveTagByNameAsync([node1.Id, node2.Id], "Work", UserCaller(userId));

        Assert.AreEqual(2, result.TotalCount);
        Assert.AreEqual(1, result.SuccessCount);
        Assert.AreEqual(1, result.FailureCount);
        Assert.AreEqual(0, await db.FileTags.CountAsync());
    }
}
