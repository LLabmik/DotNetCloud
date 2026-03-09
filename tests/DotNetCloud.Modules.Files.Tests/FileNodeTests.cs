using DotNetCloud.Modules.Files.Models;

namespace DotNetCloud.Modules.Files.Tests;

/// <summary>
/// Tests for <see cref="FileNode"/> entity covering defaults, properties, and relationships.
/// </summary>
[TestClass]
public class FileNodeTests
{
    [TestMethod]
    public void WhenCreatedThenIdIsGenerated()
    {
        var node = new FileNode { Name = "test.txt" };

        Assert.AreNotEqual(Guid.Empty, node.Id);
    }

    [TestMethod]
    public void WhenCreatedThenDefaultNodeTypeIsFile()
    {
        var node = new FileNode { Name = "test.txt" };

        Assert.AreEqual(FileNodeType.File, node.NodeType);
    }

    [TestMethod]
    public void WhenCreatedWithSymlinkTypeThenLinkTargetIsStorable()
    {
        var node = new FileNode
        {
            Name = "link-to-docs",
            NodeType = FileNodeType.SymbolicLink,
            LinkTarget = "Documents/readme.md"
        };

        Assert.AreEqual(FileNodeType.SymbolicLink, node.NodeType);
        Assert.AreEqual("Documents/readme.md", node.LinkTarget);
    }

    [TestMethod]
    public void WhenCreatedAsFileThenLinkTargetIsNull()
    {
        var node = new FileNode { Name = "report.pdf" };

        Assert.IsNull(node.LinkTarget);
    }

    [TestMethod]
    public void WhenCreatedThenSizeIsZero()
    {
        var node = new FileNode { Name = "test.txt" };

        Assert.AreEqual(0, node.Size);
    }

    [TestMethod]
    public void WhenCreatedThenCurrentVersionIsOne()
    {
        var node = new FileNode { Name = "test.txt" };

        Assert.AreEqual(1, node.CurrentVersion);
    }

    [TestMethod]
    public void WhenCreatedThenIsDeletedIsFalse()
    {
        var node = new FileNode { Name = "test.txt" };

        Assert.IsFalse(node.IsDeleted);
    }

    [TestMethod]
    public void WhenCreatedThenIsFavoriteIsFalse()
    {
        var node = new FileNode { Name = "test.txt" };

        Assert.IsFalse(node.IsFavorite);
    }

    [TestMethod]
    public void WhenCreatedThenCreatedAtIsRecentUtc()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var node = new FileNode { Name = "test.txt" };
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.IsTrue(node.CreatedAt >= before && node.CreatedAt <= after);
    }

    [TestMethod]
    public void WhenCreatedThenUpdatedAtIsRecentUtc()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var node = new FileNode { Name = "test.txt" };
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.IsTrue(node.UpdatedAt >= before && node.UpdatedAt <= after);
    }

    [TestMethod]
    public void WhenCreatedThenMaterializedPathIsEmpty()
    {
        var node = new FileNode { Name = "test.txt" };

        Assert.AreEqual(string.Empty, node.MaterializedPath);
    }

    [TestMethod]
    public void WhenCreatedThenDepthIsZero()
    {
        var node = new FileNode { Name = "test.txt" };

        Assert.AreEqual(0, node.Depth);
    }

    [TestMethod]
    public void WhenCreatedThenCollectionsAreEmpty()
    {
        var node = new FileNode { Name = "test.txt" };

        Assert.AreEqual(0, node.Versions.Count);
        Assert.AreEqual(0, node.Shares.Count);
        Assert.AreEqual(0, node.Tags.Count);
        Assert.AreEqual(0, node.Comments.Count);
        Assert.AreEqual(0, node.Children.Count);
    }

    [TestMethod]
    public void WhenCreatedThenParentIdIsNull()
    {
        var node = new FileNode { Name = "test.txt" };

        Assert.IsNull(node.ParentId);
    }

    [TestMethod]
    public void WhenCreatedAsFolderThenNodeTypeIsFolder()
    {
        var folder = new FileNode { Name = "Documents", NodeType = FileNodeType.Folder };

        Assert.AreEqual(FileNodeType.Folder, folder.NodeType);
    }

    [TestMethod]
    public void WhenTwoNodesCreatedThenIdsAreDifferent()
    {
        var node1 = new FileNode { Name = "a.txt" };
        var node2 = new FileNode { Name = "b.txt" };

        Assert.AreNotEqual(node1.Id, node2.Id);
    }

    [TestMethod]
    public void WhenDeletedFieldsSetThenSoftDeleteTracked()
    {
        var userId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var node = new FileNode { Name = "test.txt", ParentId = parentId };

        node.IsDeleted = true;
        node.DeletedAt = DateTime.UtcNow;
        node.DeletedByUserId = userId;
        node.OriginalParentId = parentId;

        Assert.IsTrue(node.IsDeleted);
        Assert.IsNotNull(node.DeletedAt);
        Assert.AreEqual(userId, node.DeletedByUserId);
        Assert.AreEqual(parentId, node.OriginalParentId);
    }
}
