using DotNetCloud.Modules.Files.Models;

namespace DotNetCloud.Modules.Files.Tests.Models;

/// <summary>
/// Tests for <see cref="FileTag"/> entity covering defaults and properties.
/// </summary>
[TestClass]
public class FileTagTests
{
    [TestMethod]
    public void WhenCreatedThenIdIsGenerated()
    {
        var tag = new FileTag { Name = "Important" };

        Assert.AreNotEqual(Guid.Empty, tag.Id);
    }

    [TestMethod]
    public void WhenCreatedThenColorIsNull()
    {
        var tag = new FileTag { Name = "Work" };

        Assert.IsNull(tag.Color);
    }

    [TestMethod]
    public void WhenCreatedThenCreatedAtIsRecentUtc()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var tag = new FileTag { Name = "Personal" };
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.IsTrue(tag.CreatedAt >= before && tag.CreatedAt <= after);
    }

    [TestMethod]
    public void WhenPropertiesSetThenStoresValues()
    {
        var nodeId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var tag = new FileTag
        {
            FileNodeId = nodeId,
            Name = "Important",
            Color = "#E53E3E",
            CreatedByUserId = userId
        };

        Assert.AreEqual(nodeId, tag.FileNodeId);
        Assert.AreEqual("Important", tag.Name);
        Assert.AreEqual("#E53E3E", tag.Color);
        Assert.AreEqual(userId, tag.CreatedByUserId);
    }

    [TestMethod]
    public void WhenCreatedThenNavigationPropertyIsNull()
    {
        var tag = new FileTag { Name = "Test" };

        Assert.IsNull(tag.FileNode);
    }
}
