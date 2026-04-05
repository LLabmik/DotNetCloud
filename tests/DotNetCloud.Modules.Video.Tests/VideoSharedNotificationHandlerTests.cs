using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Video.Events;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Modules.Video.Tests;

[TestClass]
public class VideoSharedNotificationHandlerTests
{
    [TestMethod]
    public async Task HandleAsync_VideoEntity_SendsNotification()
    {
        var notifMock = new Mock<INotificationService>();
        var handler = new VideoSharedNotificationHandler(
            Mock.Of<ILogger<VideoSharedNotificationHandler>>(),
            notifMock.Object);

        var evt = CreateEvent("dotnetcloud.video", "Video");
        await handler.HandleAsync(evt, CancellationToken.None);

        notifMock.Verify(n => n.SendAsync(
            evt.SharedWithUserId,
            It.Is<NotificationDto>(d =>
                d.Type == NotificationType.Share &&
                d.SourceModuleId == "dotnetcloud.video"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_VideoCollection_SendsNotification()
    {
        var notifMock = new Mock<INotificationService>();
        var handler = new VideoSharedNotificationHandler(
            Mock.Of<ILogger<VideoSharedNotificationHandler>>(),
            notifMock.Object);

        var evt = CreateEvent("dotnetcloud.video", "VideoCollection");
        await handler.HandleAsync(evt, CancellationToken.None);

        notifMock.Verify(n => n.SendAsync(
            evt.SharedWithUserId,
            It.Is<NotificationDto>(d => d.ActionUrl!.Contains("/video/collections/")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_WrongModule_DoesNotSendNotification()
    {
        var notifMock = new Mock<INotificationService>();
        var handler = new VideoSharedNotificationHandler(
            Mock.Of<ILogger<VideoSharedNotificationHandler>>(),
            notifMock.Object);

        var evt = CreateEvent("dotnetcloud.music", "Video");
        await handler.HandleAsync(evt, CancellationToken.None);

        notifMock.Verify(n => n.SendAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task HandleAsync_UnknownEntityType_DoesNotSendNotification()
    {
        var notifMock = new Mock<INotificationService>();
        var handler = new VideoSharedNotificationHandler(
            Mock.Of<ILogger<VideoSharedNotificationHandler>>(),
            notifMock.Object);

        var evt = CreateEvent("dotnetcloud.video", "Playlist");
        await handler.HandleAsync(evt, CancellationToken.None);

        notifMock.Verify(n => n.SendAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task HandleAsync_NoNotificationService_CompletesWithoutError()
    {
        var handler = new VideoSharedNotificationHandler(
            Mock.Of<ILogger<VideoSharedNotificationHandler>>(),
            notificationService: null);

        var evt = CreateEvent("dotnetcloud.video", "Video");
        await handler.HandleAsync(evt, CancellationToken.None);
    }

    [TestMethod]
    public async Task HandleAsync_NotificationServiceThrows_DoesNotPropagate()
    {
        var notifMock = new Mock<INotificationService>();
        notifMock.Setup(n => n.SendAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Network error"));

        var handler = new VideoSharedNotificationHandler(
            Mock.Of<ILogger<VideoSharedNotificationHandler>>(),
            notifMock.Object);

        var evt = CreateEvent("dotnetcloud.video", "Video");
        await handler.HandleAsync(evt, CancellationToken.None);
    }

    [TestMethod]
    public async Task HandleAsync_VideoActionUrl_ContainsCorrectPath()
    {
        var notifMock = new Mock<INotificationService>();
        var handler = new VideoSharedNotificationHandler(
            Mock.Of<ILogger<VideoSharedNotificationHandler>>(),
            notifMock.Object);

        var entityId = Guid.NewGuid();
        var evt = new ResourceSharedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SharedByUserId = Guid.NewGuid(),
            SharedWithUserId = Guid.NewGuid(),
            SourceModuleId = "dotnetcloud.video",
            EntityType = "Video",
            EntityId = entityId,
            EntityDisplayName = "Holiday Vlog",
            Permission = "ReadOnly"
        };

        await handler.HandleAsync(evt, CancellationToken.None);

        notifMock.Verify(n => n.SendAsync(
            It.IsAny<Guid>(),
            It.Is<NotificationDto>(d => d.ActionUrl == $"/video/{entityId}"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_CollectionActionUrl_ContainsCorrectPath()
    {
        var notifMock = new Mock<INotificationService>();
        var handler = new VideoSharedNotificationHandler(
            Mock.Of<ILogger<VideoSharedNotificationHandler>>(),
            notifMock.Object);

        var entityId = Guid.NewGuid();
        var evt = new ResourceSharedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SharedByUserId = Guid.NewGuid(),
            SharedWithUserId = Guid.NewGuid(),
            SourceModuleId = "dotnetcloud.video",
            EntityType = "VideoCollection",
            EntityId = entityId,
            EntityDisplayName = "Documentary Series",
            Permission = "ReadWrite"
        };

        await handler.HandleAsync(evt, CancellationToken.None);

        notifMock.Verify(n => n.SendAsync(
            It.IsAny<Guid>(),
            It.Is<NotificationDto>(d => d.ActionUrl == $"/video/collections/{entityId}"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_IncludesDisplayNameInMessage()
    {
        var notifMock = new Mock<INotificationService>();
        var handler = new VideoSharedNotificationHandler(
            Mock.Of<ILogger<VideoSharedNotificationHandler>>(),
            notifMock.Object);

        var evt = new ResourceSharedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SharedByUserId = Guid.NewGuid(),
            SharedWithUserId = Guid.NewGuid(),
            SourceModuleId = "dotnetcloud.video",
            EntityType = "Video",
            EntityId = Guid.NewGuid(),
            EntityDisplayName = "Summer Vacation",
            Permission = "ReadOnly"
        };

        await handler.HandleAsync(evt, CancellationToken.None);

        notifMock.Verify(n => n.SendAsync(
            It.IsAny<Guid>(),
            It.Is<NotificationDto>(d => d.Message!.Contains("Summer Vacation")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ResourceSharedEvent CreateEvent(string moduleId, string entityType) => new()
    {
        EventId = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
        SharedByUserId = Guid.NewGuid(),
        SharedWithUserId = Guid.NewGuid(),
        SourceModuleId = moduleId,
        EntityType = entityType,
        EntityId = Guid.NewGuid(),
        EntityDisplayName = "Test Entity",
        Permission = "ReadOnly"
    };
}
