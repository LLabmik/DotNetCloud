using DotNetCloud.Modules.Files.Models;

using FileShare = DotNetCloud.Modules.Files.Models.FileShare;

namespace DotNetCloud.Modules.Files.Tests.Models;

/// <summary>
/// Tests for <see cref="FileShare"/> entity covering defaults and properties.
/// </summary>
[TestClass]
public class FileShareTests
{
    [TestMethod]
    public void WhenCreatedThenIdIsGenerated()
    {
        var share = new FileShare();

        Assert.AreNotEqual(Guid.Empty, share.Id);
    }

    [TestMethod]
    public void WhenCreatedThenDefaultPermissionIsRead()
    {
        var share = new FileShare();

        Assert.AreEqual(SharePermission.Read, share.Permission);
    }

    [TestMethod]
    public void WhenCreatedThenDefaultShareTypeIsUser()
    {
        var share = new FileShare();

        Assert.AreEqual(ShareType.User, share.ShareType);
    }

    [TestMethod]
    public void WhenCreatedThenDownloadCountIsZero()
    {
        var share = new FileShare();

        Assert.AreEqual(0, share.DownloadCount);
    }

    [TestMethod]
    public void WhenCreatedThenOptionalFieldsAreNull()
    {
        var share = new FileShare();

        Assert.IsNull(share.SharedWithUserId);
        Assert.IsNull(share.SharedWithTeamId);
        Assert.IsNull(share.SharedWithGroupId);
        Assert.IsNull(share.LinkToken);
        Assert.IsNull(share.LinkPasswordHash);
        Assert.IsNull(share.MaxDownloads);
        Assert.IsNull(share.ExpiresAt);
        Assert.IsNull(share.Note);
    }

    [TestMethod]
    public void WhenCreatedThenCreatedAtIsRecentUtc()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var share = new FileShare();
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.IsTrue(share.CreatedAt >= before && share.CreatedAt <= after);
    }

    [TestMethod]
    public void WhenPublicLinkConfiguredThenStoresValues()
    {
        var nodeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var expires = DateTime.UtcNow.AddDays(7);

        var share = new FileShare
        {
            FileNodeId = nodeId,
            ShareType = ShareType.PublicLink,
            Permission = SharePermission.Read,
            LinkToken = "abc123token",
            LinkPasswordHash = "hashed_pw",
            MaxDownloads = 100,
            ExpiresAt = expires,
            CreatedByUserId = userId,
            Note = "Public report"
        };

        Assert.AreEqual(nodeId, share.FileNodeId);
        Assert.AreEqual(ShareType.PublicLink, share.ShareType);
        Assert.AreEqual("abc123token", share.LinkToken);
        Assert.AreEqual("hashed_pw", share.LinkPasswordHash);
        Assert.AreEqual(100, share.MaxDownloads);
        Assert.AreEqual(expires, share.ExpiresAt);
        Assert.AreEqual(userId, share.CreatedByUserId);
        Assert.AreEqual("Public report", share.Note);
    }

    [TestMethod]
    public void WhenUserShareConfiguredThenStoresTargetUser()
    {
        var targetUser = Guid.NewGuid();

        var share = new FileShare
        {
            ShareType = ShareType.User,
            SharedWithUserId = targetUser,
            Permission = SharePermission.ReadWrite
        };

        Assert.AreEqual(ShareType.User, share.ShareType);
        Assert.AreEqual(targetUser, share.SharedWithUserId);
        Assert.AreEqual(SharePermission.ReadWrite, share.Permission);
    }

    [TestMethod]
    public void WhenCreatedThenNavigationPropertyIsNull()
    {
        var share = new FileShare();

        Assert.IsNull(share.FileNode);
    }
}
