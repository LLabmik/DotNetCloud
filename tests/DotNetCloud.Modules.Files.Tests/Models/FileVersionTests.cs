using DotNetCloud.Modules.Files.Models;

namespace DotNetCloud.Modules.Files.Tests.Models;

/// <summary>
/// Tests for <see cref="FileVersion"/> entity covering defaults and properties.
/// </summary>
[TestClass]
public class FileVersionTests
{
    [TestMethod]
    public void WhenCreatedThenIdIsGenerated()
    {
        var version = new FileVersion { ContentHash = "abc", StoragePath = "/path" };

        Assert.AreNotEqual(Guid.Empty, version.Id);
    }

    [TestMethod]
    public void WhenTwoVersionsCreatedThenIdsAreDifferent()
    {
        var v1 = new FileVersion { ContentHash = "a", StoragePath = "/a" };
        var v2 = new FileVersion { ContentHash = "b", StoragePath = "/b" };

        Assert.AreNotEqual(v1.Id, v2.Id);
    }

    [TestMethod]
    public void WhenCreatedThenVersionNumberIsZero()
    {
        var version = new FileVersion { ContentHash = "abc", StoragePath = "/path" };

        Assert.AreEqual(0, version.VersionNumber);
    }

    [TestMethod]
    public void WhenCreatedThenSizeIsZero()
    {
        var version = new FileVersion { ContentHash = "abc", StoragePath = "/path" };

        Assert.AreEqual(0, version.Size);
    }

    [TestMethod]
    public void WhenCreatedThenCreatedAtIsRecentUtc()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var version = new FileVersion { ContentHash = "abc", StoragePath = "/path" };
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.IsTrue(version.CreatedAt >= before && version.CreatedAt <= after);
    }

    [TestMethod]
    public void WhenCreatedThenMimeTypeIsNull()
    {
        var version = new FileVersion { ContentHash = "abc", StoragePath = "/path" };

        Assert.IsNull(version.MimeType);
    }

    [TestMethod]
    public void WhenCreatedThenLabelIsNull()
    {
        var version = new FileVersion { ContentHash = "abc", StoragePath = "/path" };

        Assert.IsNull(version.Label);
    }

    [TestMethod]
    public void WhenPropertiesSetThenStoresValues()
    {
        var nodeId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var version = new FileVersion
        {
            FileNodeId = nodeId,
            VersionNumber = 3,
            Size = 1024,
            ContentHash = "abc123",
            StoragePath = "/files/ab/cd/abc123",
            MimeType = "application/pdf",
            CreatedByUserId = userId,
            Label = "Final draft"
        };

        Assert.AreEqual(nodeId, version.FileNodeId);
        Assert.AreEqual(3, version.VersionNumber);
        Assert.AreEqual(1024, version.Size);
        Assert.AreEqual("abc123", version.ContentHash);
        Assert.AreEqual("/files/ab/cd/abc123", version.StoragePath);
        Assert.AreEqual("application/pdf", version.MimeType);
        Assert.AreEqual(userId, version.CreatedByUserId);
        Assert.AreEqual("Final draft", version.Label);
    }

    [TestMethod]
    public void WhenCreatedThenFileNodeNavigationIsNull()
    {
        var version = new FileVersion { ContentHash = "abc", StoragePath = "/path" };

        Assert.IsNull(version.FileNode);
    }
}
