using DotNetCloud.Core.Events;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services.Background;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using FileShare = DotNetCloud.Modules.Files.Models.FileShare;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class ShareExpiryNotificationServiceTests
{
    private static FilesDbContext CreateContext(string? name = null)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    [TestMethod]
    public async Task CheckExpiringSharesAsync_ExpiringWithinWindow_PublishesEvent()
    {
        var dbName = Guid.NewGuid().ToString();
        using var setupDb = CreateContext(dbName);

        var node = new FileNode
        {
            Name = "expiring.txt",
            NodeType = FileNodeType.File,
            OwnerId = Guid.NewGuid()
        };
        setupDb.FileNodes.Add(node);

        var share = new FileShare
        {
            FileNodeId = node.Id,
            ShareType = ShareType.PublicLink,
            LinkToken = "expiring_token",
            CreatedByUserId = node.OwnerId,
            ExpiresAt = DateTime.UtcNow.AddHours(12) // Expires in 12 hours (within 24h window)
        };
        setupDb.FileShares.Add(share);
        await setupDb.SaveChangesAsync();

        var eventBusMock = new Mock<IEventBus>();
        var services = new ServiceCollection();
        services.AddDbContext<FilesDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddSingleton<IEventBus>(eventBusMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var service = new ShareExpiryNotificationService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ShareExpiryNotificationService>.Instance,
            new BackgroundServiceTracker());

        await service.CheckExpiringSharesAsync(CancellationToken.None);

        eventBusMock.Verify(e => e.PublishAsync(
            It.Is<ShareExpiringEvent>(evt =>
                evt.ShareId == share.Id &&
                evt.FileNodeId == node.Id &&
                evt.CreatedByUserId == node.OwnerId),
            It.IsAny<Core.Authorization.CallerContext>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify ExpiryNotificationSentAt was set
        using var verifyDb = CreateContext(dbName);
        var updated = await verifyDb.FileShares.FindAsync(share.Id);
        Assert.IsNotNull(updated!.ExpiryNotificationSentAt);
    }

    [TestMethod]
    public async Task CheckExpiringSharesAsync_AlreadyNotified_DoesNotPublishAgain()
    {
        var dbName = Guid.NewGuid().ToString();
        using var setupDb = CreateContext(dbName);

        var node = new FileNode
        {
            Name = "already-notified.txt",
            NodeType = FileNodeType.File,
            OwnerId = Guid.NewGuid()
        };
        setupDb.FileNodes.Add(node);

        var share = new FileShare
        {
            FileNodeId = node.Id,
            ShareType = ShareType.PublicLink,
            LinkToken = "notified_token",
            CreatedByUserId = node.OwnerId,
            ExpiresAt = DateTime.UtcNow.AddHours(12),
            ExpiryNotificationSentAt = DateTime.UtcNow.AddHours(-1) // Already notified
        };
        setupDb.FileShares.Add(share);
        await setupDb.SaveChangesAsync();

        var eventBusMock = new Mock<IEventBus>();
        var services = new ServiceCollection();
        services.AddDbContext<FilesDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddSingleton<IEventBus>(eventBusMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var service = new ShareExpiryNotificationService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ShareExpiryNotificationService>.Instance,
            new BackgroundServiceTracker());

        await service.CheckExpiringSharesAsync(CancellationToken.None);

        eventBusMock.Verify(e => e.PublishAsync(
            It.IsAny<ShareExpiringEvent>(),
            It.IsAny<Core.Authorization.CallerContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task CheckExpiringSharesAsync_ShareNotExpiringSoon_DoesNotPublish()
    {
        var dbName = Guid.NewGuid().ToString();
        using var setupDb = CreateContext(dbName);

        var node = new FileNode
        {
            Name = "far-future.txt",
            NodeType = FileNodeType.File,
            OwnerId = Guid.NewGuid()
        };
        setupDb.FileNodes.Add(node);

        var share = new FileShare
        {
            FileNodeId = node.Id,
            ShareType = ShareType.PublicLink,
            LinkToken = "far_future_token",
            CreatedByUserId = node.OwnerId,
            ExpiresAt = DateTime.UtcNow.AddDays(7) // Far in the future
        };
        setupDb.FileShares.Add(share);
        await setupDb.SaveChangesAsync();

        var eventBusMock = new Mock<IEventBus>();
        var services = new ServiceCollection();
        services.AddDbContext<FilesDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddSingleton<IEventBus>(eventBusMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var service = new ShareExpiryNotificationService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ShareExpiryNotificationService>.Instance,
            new BackgroundServiceTracker());

        await service.CheckExpiringSharesAsync(CancellationToken.None);

        eventBusMock.Verify(e => e.PublishAsync(
            It.IsAny<ShareExpiringEvent>(),
            It.IsAny<Core.Authorization.CallerContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task CheckExpiringSharesAsync_AlreadyExpired_DoesNotPublish()
    {
        var dbName = Guid.NewGuid().ToString();
        using var setupDb = CreateContext(dbName);

        var node = new FileNode
        {
            Name = "expired.txt",
            NodeType = FileNodeType.File,
            OwnerId = Guid.NewGuid()
        };
        setupDb.FileNodes.Add(node);

        var share = new FileShare
        {
            FileNodeId = node.Id,
            ShareType = ShareType.PublicLink,
            LinkToken = "expired_token",
            CreatedByUserId = node.OwnerId,
            ExpiresAt = DateTime.UtcNow.AddHours(-1) // Already expired
        };
        setupDb.FileShares.Add(share);
        await setupDb.SaveChangesAsync();

        var eventBusMock = new Mock<IEventBus>();
        var services = new ServiceCollection();
        services.AddDbContext<FilesDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddSingleton<IEventBus>(eventBusMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var service = new ShareExpiryNotificationService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ShareExpiryNotificationService>.Instance,
            new BackgroundServiceTracker());

        await service.CheckExpiringSharesAsync(CancellationToken.None);

        eventBusMock.Verify(e => e.PublishAsync(
            It.IsAny<ShareExpiringEvent>(),
            It.IsAny<Core.Authorization.CallerContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
