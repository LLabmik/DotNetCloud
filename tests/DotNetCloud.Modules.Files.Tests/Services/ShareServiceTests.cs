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
using FileShare = DotNetCloud.Modules.Files.Models.FileShare;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class ShareServiceTests
{
    private static FilesDbContext CreateContext(string? name = null)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    private static ShareService CreateService(FilesDbContext db) =>
        new(db, Mock.Of<IEventBus>(), NullLoggerFactory.Instance.CreateLogger<ShareService>(), new PermissionService(db));

    private static CallerContext UserCaller(Guid userId) => new(userId, Array.Empty<string>(), CallerType.User);

    private static FileNode CreateFileNode(Guid ownerId) => new()
    {
        Name = "shared.txt",
        NodeType = FileNodeType.File,
        OwnerId = ownerId
    };

    [TestMethod]
    public async Task CreateShareAsync_UserShare_CreatesSuccessfully()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var node = CreateFileNode(userId);
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.CreateShareAsync(node.Id, new CreateShareDto
        {
            ShareType = "User",
            SharedWithUserId = targetUserId,
            Permission = "ReadWrite"
        }, UserCaller(userId));

        Assert.AreEqual("User", result.ShareType);
        Assert.AreEqual(targetUserId, result.SharedWithUserId);
        Assert.AreEqual("ReadWrite", result.Permission);
    }

    [TestMethod]
    public async Task CreateShareAsync_GroupShare_ReturnsGroupTarget()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var targetGroupId = Guid.NewGuid();
        var node = CreateFileNode(userId);
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.CreateShareAsync(node.Id, new CreateShareDto
        {
            ShareType = "Group",
            SharedWithGroupId = targetGroupId,
            Permission = "Read"
        }, UserCaller(userId));

        Assert.AreEqual("Group", result.ShareType);
        Assert.AreEqual(targetGroupId, result.SharedWithGroupId);
    }

    [TestMethod]
    public async Task CreateShareAsync_PublicLink_GeneratesToken()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateFileNode(userId);
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.CreateShareAsync(node.Id, new CreateShareDto
        {
            ShareType = "PublicLink",
            Permission = "Read"
        }, UserCaller(userId));

        Assert.AreEqual("PublicLink", result.ShareType);
        Assert.IsNotNull(result.LinkToken);
        Assert.IsTrue(result.LinkToken.Length > 10);
    }

    [TestMethod]
    public async Task CreateShareAsync_PublicLinkWithPassword_HashesPassword()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateFileNode(userId);
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.CreateShareAsync(node.Id, new CreateShareDto
        {
            ShareType = "PublicLink",
            LinkPassword = "secret123"
        }, UserCaller(userId));

        var share = await db.FileShares.FirstAsync();
        Assert.IsNotNull(share.LinkPasswordHash);
        Assert.AreNotEqual("secret123", share.LinkPasswordHash);
    }

    [TestMethod]
    public async Task CreateShareAsync_NonOwner_ThrowsForbiddenException()
    {
        using var db = CreateContext();
        var node = CreateFileNode(Guid.NewGuid());
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<ForbiddenException>(
            () => service.CreateShareAsync(node.Id, new CreateShareDto { ShareType = "User" }, UserCaller(Guid.NewGuid())));
    }

    [TestMethod]
    public async Task UpdateShareAsync_ValidUpdate_UpdatesFields()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateFileNode(userId);
        db.FileNodes.Add(node);
        var share = new FileShare
        {
            FileNodeId = node.Id,
            ShareType = ShareType.User,
            Permission = SharePermission.Read,
            CreatedByUserId = userId
        };
        db.FileShares.Add(share);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.UpdateShareAsync(share.Id, new UpdateShareDto
        {
            Permission = "ReadWrite",
            Note = "Updated note"
        }, UserCaller(userId));

        Assert.AreEqual("ReadWrite", result.Permission);
        Assert.AreEqual("Updated note", result.Note);
    }

    [TestMethod]
    public async Task DeleteShareAsync_Owner_DeletesSuccessfully()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var share = new FileShare
        {
            FileNodeId = Guid.NewGuid(),
            ShareType = ShareType.User,
            CreatedByUserId = userId
        };
        db.FileShares.Add(share);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.DeleteShareAsync(share.Id, UserCaller(userId));

        Assert.AreEqual(0, await db.FileShares.CountAsync());
    }

    [TestMethod]
    public async Task GetSharesAsync_ReturnsAllSharesOnNode()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateFileNode(userId);
        db.FileNodes.Add(node);
        db.FileShares.Add(new FileShare { FileNodeId = node.Id, ShareType = ShareType.User, CreatedByUserId = userId });
        db.FileShares.Add(new FileShare { FileNodeId = node.Id, ShareType = ShareType.PublicLink, LinkToken = "tok", CreatedByUserId = userId });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var shares = await service.GetSharesAsync(node.Id, UserCaller(userId));

        Assert.AreEqual(2, shares.Count);
    }

    [TestMethod]
    public async Task GetSharedWithMeAsync_ReturnsUserShares()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var node = CreateFileNode(userId);
        db.FileNodes.Add(node);
        db.FileShares.Add(new FileShare { FileNodeId = node.Id, ShareType = ShareType.User, SharedWithUserId = targetUserId, CreatedByUserId = userId });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var shares = await service.GetSharedWithMeAsync(UserCaller(targetUserId));

        Assert.AreEqual(1, shares.Count);
    }

    [TestMethod]
    public async Task GetSharedWithMeAsync_ExcludesNonUserShareTypes()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var node = CreateFileNode(userId);
        db.FileNodes.Add(node);
        db.FileShares.Add(new FileShare
        {
            FileNodeId = node.Id,
            ShareType = ShareType.User,
            SharedWithUserId = targetUserId,
            CreatedByUserId = userId,
        });
        db.FileShares.Add(new FileShare
        {
            FileNodeId = node.Id,
            ShareType = ShareType.Team,
            SharedWithUserId = targetUserId,
            SharedWithTeamId = Guid.NewGuid(),
            CreatedByUserId = userId,
        });
        db.FileShares.Add(new FileShare
        {
            FileNodeId = node.Id,
            ShareType = ShareType.Group,
            SharedWithUserId = targetUserId,
            SharedWithGroupId = Guid.NewGuid(),
            CreatedByUserId = userId,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var shares = await service.GetSharedWithMeAsync(UserCaller(targetUserId));

        Assert.AreEqual(1, shares.Count);
        Assert.IsTrue(shares.All(share => share.ShareType == ShareType.User.ToString()));
    }

    [TestMethod]
    public async Task ResolvePublicLinkAsync_ValidToken_ReturnsShare()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateFileNode(userId);
        db.FileNodes.Add(node);
        db.FileShares.Add(new FileShare
        {
            FileNodeId = node.Id,
            ShareType = ShareType.PublicLink,
            LinkToken = "test_token_123",
            Permission = SharePermission.Read,
            CreatedByUserId = userId
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ResolvePublicLinkAsync("test_token_123", null);

        Assert.IsNotNull(result);
        Assert.AreEqual(node.Id, result.FileNodeId);
    }

    [TestMethod]
    public async Task ResolvePublicLinkAsync_ExpiredLink_ReturnsNull()
    {
        using var db = CreateContext();
        db.FileShares.Add(new FileShare
        {
            FileNodeId = Guid.NewGuid(),
            ShareType = ShareType.PublicLink,
            LinkToken = "expired_token",
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            CreatedByUserId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ResolvePublicLinkAsync("expired_token", null);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task IncrementDownloadCountAsync_IncrementsCount()
    {
        using var db = CreateContext();
        var node = CreateFileNode(Guid.NewGuid());
        db.FileNodes.Add(node);
        var share = new FileShare
        {
            FileNodeId = node.Id,
            ShareType = ShareType.PublicLink,
            LinkToken = "dl_token",
            DownloadCount = 5,
            CreatedByUserId = Guid.NewGuid()
        };
        db.FileShares.Add(share);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.IncrementDownloadCountAsync(share.Id);

        var updated = await db.FileShares.FindAsync(share.Id);
        Assert.AreEqual(6, updated!.DownloadCount);
    }

    [TestMethod]
    public async Task IncrementDownloadCountAsync_FirstAccess_PublishesPublicLinkAccessedEvent()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateFileNode(userId);
        db.FileNodes.Add(node);
        var share = new FileShare
        {
            FileNodeId = node.Id,
            ShareType = ShareType.PublicLink,
            LinkToken = "first_access_token",
            DownloadCount = 0,
            CreatedByUserId = userId
        };
        db.FileShares.Add(share);
        await db.SaveChangesAsync();

        var eventBusMock = new Mock<IEventBus>();
        var service = new ShareService(db, eventBusMock.Object,
            NullLoggerFactory.Instance.CreateLogger<ShareService>(), new PermissionService(db));

        await service.IncrementDownloadCountAsync(share.Id);

        eventBusMock.Verify(e => e.PublishAsync(
            It.Is<Files.Events.PublicLinkAccessedEvent>(evt =>
                evt.ShareId == share.Id &&
                evt.FileNodeId == node.Id &&
                evt.CreatedByUserId == userId),
            It.IsAny<CallerContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task IncrementDownloadCountAsync_SubsequentAccess_DoesNotPublishEvent()
    {
        using var db = CreateContext();
        var node = CreateFileNode(Guid.NewGuid());
        db.FileNodes.Add(node);
        var share = new FileShare
        {
            FileNodeId = node.Id,
            ShareType = ShareType.PublicLink,
            LinkToken = "second_access_token",
            DownloadCount = 1,
            CreatedByUserId = Guid.NewGuid()
        };
        db.FileShares.Add(share);
        await db.SaveChangesAsync();

        var eventBusMock = new Mock<IEventBus>();
        var service = new ShareService(db, eventBusMock.Object,
            NullLoggerFactory.Instance.CreateLogger<ShareService>(), new PermissionService(db));

        await service.IncrementDownloadCountAsync(share.Id);

        eventBusMock.Verify(e => e.PublishAsync(
            It.IsAny<Files.Events.PublicLinkAccessedEvent>(),
            It.IsAny<CallerContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
