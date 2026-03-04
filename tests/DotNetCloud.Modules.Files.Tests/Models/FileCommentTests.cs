using DotNetCloud.Modules.Files.Models;

namespace DotNetCloud.Modules.Files.Tests.Models;

/// <summary>
/// Tests for <see cref="FileComment"/> entity covering defaults, threading, and soft-delete.
/// </summary>
[TestClass]
public class FileCommentTests
{
    [TestMethod]
    public void WhenCreatedThenIdIsGenerated()
    {
        var comment = new FileComment { Content = "Hello" };

        Assert.AreNotEqual(Guid.Empty, comment.Id);
    }

    [TestMethod]
    public void WhenCreatedThenIsDeletedIsFalse()
    {
        var comment = new FileComment { Content = "Hello" };

        Assert.IsFalse(comment.IsDeleted);
    }

    [TestMethod]
    public void WhenCreatedThenParentCommentIdIsNull()
    {
        var comment = new FileComment { Content = "Top-level" };

        Assert.IsNull(comment.ParentCommentId);
    }

    [TestMethod]
    public void WhenCreatedThenUpdatedAtIsNull()
    {
        var comment = new FileComment { Content = "Fresh comment" };

        Assert.IsNull(comment.UpdatedAt);
    }

    [TestMethod]
    public void WhenCreatedThenRepliesCollectionIsEmpty()
    {
        var comment = new FileComment { Content = "Parent" };

        Assert.AreEqual(0, comment.Replies.Count);
    }

    [TestMethod]
    public void WhenCreatedThenCreatedAtIsRecentUtc()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var comment = new FileComment { Content = "Test" };
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.IsTrue(comment.CreatedAt >= before && comment.CreatedAt <= after);
    }

    [TestMethod]
    public void WhenPropertiesSetThenStoresValues()
    {
        var nodeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var parentId = Guid.NewGuid();

        var comment = new FileComment
        {
            FileNodeId = nodeId,
            ParentCommentId = parentId,
            Content = "This is a **reply**",
            CreatedByUserId = userId
        };

        Assert.AreEqual(nodeId, comment.FileNodeId);
        Assert.AreEqual(parentId, comment.ParentCommentId);
        Assert.AreEqual("This is a **reply**", comment.Content);
        Assert.AreEqual(userId, comment.CreatedByUserId);
    }

    [TestMethod]
    public void WhenSoftDeletedThenIsDeletedIsTrue()
    {
        var comment = new FileComment { Content = "To be deleted" };

        comment.IsDeleted = true;
        comment.UpdatedAt = DateTime.UtcNow;

        Assert.IsTrue(comment.IsDeleted);
        Assert.IsNotNull(comment.UpdatedAt);
    }

    [TestMethod]
    public void WhenCreatedThenNavigationPropertiesAreNull()
    {
        var comment = new FileComment { Content = "Test" };

        Assert.IsNull(comment.FileNode);
        Assert.IsNull(comment.ParentComment);
    }
}
