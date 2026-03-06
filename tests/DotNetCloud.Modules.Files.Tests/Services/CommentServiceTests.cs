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
public class CommentServiceTests
{
    private static FilesDbContext CreateContext(string? name = null)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    private static CommentService CreateService(FilesDbContext db) =>
        new(db, NullLoggerFactory.Instance.CreateLogger<CommentService>(), new PermissionService(db));

    private static CallerContext UserCaller(Guid userId) => new(userId, Array.Empty<string>(), CallerType.User);

    private static FileNode CreateFileNode(Guid ownerId) => new()
    {
        Name = "test.txt",
        NodeType = FileNodeType.File,
        OwnerId = ownerId
    };

    [TestMethod]
    public async Task AddCommentAsync_ValidInput_CreatesComment()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateFileNode(userId);
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.AddCommentAsync(node.Id, "Hello world", null, UserCaller(userId));

        Assert.AreEqual("Hello world", result.Content);
        Assert.AreEqual(userId, result.CreatedByUserId);
        Assert.IsNull(result.ParentCommentId);
    }

    [TestMethod]
    public async Task AddCommentAsync_WithParent_CreatesReply()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateFileNode(userId);
        db.FileNodes.Add(node);
        var parent = new FileComment { FileNodeId = node.Id, Content = "Parent", CreatedByUserId = userId };
        db.FileComments.Add(parent);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.AddCommentAsync(node.Id, "Reply", parent.Id, UserCaller(userId));

        Assert.AreEqual(parent.Id, result.ParentCommentId);
    }

    [TestMethod]
    public async Task AddCommentAsync_NonExistentNode_ThrowsNotFoundException()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => service.AddCommentAsync(Guid.NewGuid(), "text", null, UserCaller(Guid.NewGuid())));
    }

    [TestMethod]
    public async Task EditCommentAsync_Author_UpdatesContent()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateFileNode(userId);
        db.FileNodes.Add(node);
        var comment = new FileComment { FileNodeId = node.Id, Content = "Original", CreatedByUserId = userId };
        db.FileComments.Add(comment);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.EditCommentAsync(comment.Id, "Updated", UserCaller(userId));

        Assert.AreEqual("Updated", result.Content);
        Assert.IsNotNull(result.UpdatedAt);
    }

    [TestMethod]
    public async Task EditCommentAsync_NonAuthor_ThrowsForbiddenException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var node = CreateFileNode(userId);
        db.FileNodes.Add(node);
        var comment = new FileComment { FileNodeId = node.Id, Content = "Original", CreatedByUserId = userId };
        db.FileComments.Add(comment);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<ForbiddenException>(
            () => service.EditCommentAsync(comment.Id, "Hacked", UserCaller(otherId)));
    }

    [TestMethod]
    public async Task DeleteCommentAsync_Author_SoftDeletes()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateFileNode(userId);
        db.FileNodes.Add(node);
        var comment = new FileComment { FileNodeId = node.Id, Content = "ToDelete", CreatedByUserId = userId };
        db.FileComments.Add(comment);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.DeleteCommentAsync(comment.Id, UserCaller(userId));

        // Comment is soft-deleted, so default query filters hide it
        var remaining = await db.FileComments.CountAsync();
        Assert.AreEqual(0, remaining);

        // But it still exists with IgnoreQueryFilters
        var deleted = await db.FileComments.IgnoreQueryFilters().FirstAsync(c => c.Id == comment.Id);
        Assert.IsTrue(deleted.IsDeleted);
    }

    [TestMethod]
    public async Task GetCommentsAsync_ReturnsTopLevelWithReplyCounts()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateFileNode(userId);
        db.FileNodes.Add(node);

        var parent = new FileComment { FileNodeId = node.Id, Content = "Parent", CreatedByUserId = userId };
        var reply1 = new FileComment { FileNodeId = node.Id, Content = "Reply1", CreatedByUserId = userId, ParentCommentId = parent.Id };
        var reply2 = new FileComment { FileNodeId = node.Id, Content = "Reply2", CreatedByUserId = userId, ParentCommentId = parent.Id };

        db.FileComments.AddRange(parent, reply1, reply2);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var comments = await service.GetCommentsAsync(node.Id, UserCaller(userId));

        Assert.AreEqual(1, comments.Count);
        Assert.AreEqual("Parent", comments[0].Content);
        Assert.AreEqual(2, comments[0].ReplyCount);
    }

    [TestMethod]
    public async Task GetCommentAsync_ExistingComment_ReturnsDto()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateFileNode(userId);
        db.FileNodes.Add(node);
        var comment = new FileComment { FileNodeId = node.Id, Content = "Hello", CreatedByUserId = userId };
        db.FileComments.Add(comment);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetCommentAsync(comment.Id, UserCaller(userId));

        Assert.IsNotNull(result);
        Assert.AreEqual("Hello", result.Content);
    }

    [TestMethod]
    public async Task GetCommentAsync_NonExistent_ReturnsNull()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        var result = await service.GetCommentAsync(Guid.NewGuid(), UserCaller(Guid.NewGuid()));

        Assert.IsNull(result);
    }
}
