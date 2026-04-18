using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Photos.Events;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Modules.Photos.Tests;

[TestClass]
public class AlbumSharedNotificationHandlerTests
{
    [TestMethod]
    public async Task HandleAsync_SendsNotification_WhenServiceAvailable()
    {
        var notifMock = new Mock<INotificationService>();
        var handler = new AlbumSharedNotificationHandler(
            Mock.Of<ILogger<AlbumSharedNotificationHandler>>(),
            notifMock.Object);

        var evt = CreateEvent();
        await handler.HandleAsync(evt, CancellationToken.None);

        notifMock.Verify(n => n.SendAsync(
            evt.SharedWithUserId,
            It.Is<NotificationDto>(d =>
                d.UserId == evt.SharedWithUserId &&
                d.Type == NotificationType.Share &&
                d.SourceModuleId == "dotnetcloud.photos" &&
                d.ActionUrl!.Contains(evt.AlbumId.ToString())),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_NoNotificationService_CompletesWithoutError()
    {
        var handler = new AlbumSharedNotificationHandler(
            Mock.Of<ILogger<AlbumSharedNotificationHandler>>(),
            notificationService: null);

        await handler.HandleAsync(CreateEvent(), CancellationToken.None);
    }

    [TestMethod]
    public async Task HandleAsync_NotificationServiceThrows_DoesNotPropagate()
    {
        var notifMock = new Mock<INotificationService>();
        notifMock.Setup(n => n.SendAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Network error"));

        var handler = new AlbumSharedNotificationHandler(
            Mock.Of<ILogger<AlbumSharedNotificationHandler>>(),
            notifMock.Object);

        await handler.HandleAsync(CreateEvent(), CancellationToken.None);
    }

    [TestMethod]
    public async Task HandleAsync_NotificationHasCorrectActionUrl()
    {
        var notifMock = new Mock<INotificationService>();
        var handler = new AlbumSharedNotificationHandler(
            Mock.Of<ILogger<AlbumSharedNotificationHandler>>(),
            notifMock.Object);

        var albumId = Guid.NewGuid();
        var evt = new AlbumSharedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            AlbumId = albumId,
            SharedByUserId = Guid.NewGuid(),
            SharedWithUserId = Guid.NewGuid(),
            Permission = "ReadOnly"
        };

        await handler.HandleAsync(evt, CancellationToken.None);

        notifMock.Verify(n => n.SendAsync(
            It.IsAny<Guid>(),
            It.Is<NotificationDto>(d => d.ActionUrl == $"/photos/albums/{albumId}"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_NotificationIncludesPermissionInfo()
    {
        var notifMock = new Mock<INotificationService>();
        var handler = new AlbumSharedNotificationHandler(
            Mock.Of<ILogger<AlbumSharedNotificationHandler>>(),
            notifMock.Object);

        var evt = new AlbumSharedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            AlbumId = Guid.NewGuid(),
            SharedByUserId = Guid.NewGuid(),
            SharedWithUserId = Guid.NewGuid(),
            Permission = "ReadWrite"
        };

        await handler.HandleAsync(evt, CancellationToken.None);

        notifMock.Verify(n => n.SendAsync(
            It.IsAny<Guid>(),
            It.Is<NotificationDto>(d => d.Message!.Contains("ReadWrite")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_NotificationTargetsCorrectUser()
    {
        var notifMock = new Mock<INotificationService>();
        var handler = new AlbumSharedNotificationHandler(
            Mock.Of<ILogger<AlbumSharedNotificationHandler>>(),
            notifMock.Object);

        var recipientId = Guid.NewGuid();
        var evt = new AlbumSharedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            AlbumId = Guid.NewGuid(),
            SharedByUserId = Guid.NewGuid(),
            SharedWithUserId = recipientId,
            Permission = "ReadOnly"
        };

        await handler.HandleAsync(evt, CancellationToken.None);

        notifMock.Verify(n => n.SendAsync(
            recipientId,
            It.IsAny<NotificationDto>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static AlbumSharedEvent CreateEvent() => new()
    {
        EventId = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
        AlbumId = Guid.NewGuid(),
        SharedByUserId = Guid.NewGuid(),
        SharedWithUserId = Guid.NewGuid(),
        Permission = "ReadOnly"
    };
}
