using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Music.Events;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Modules.Music.Tests;

[TestClass]
public class PlaylistSharedNotificationHandlerTests
{
    [TestMethod]
    public async Task HandleAsync_MusicPlaylist_SendsNotification()
    {
        var notifMock = new Mock<INotificationService>();
        var handler = new PlaylistSharedNotificationHandler(
            Mock.Of<ILogger<PlaylistSharedNotificationHandler>>(),
            notifMock.Object);

        var evt = CreateEvent("dotnetcloud.music", "Playlist");
        await handler.HandleAsync(evt, CancellationToken.None);

        notifMock.Verify(n => n.SendAsync(
            evt.SharedWithUserId,
            It.Is<NotificationDto>(d =>
                d.Type == NotificationType.Share &&
                d.SourceModuleId == "dotnetcloud.music"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_MusicAlbum_SendsNotification()
    {
        var notifMock = new Mock<INotificationService>();
        var handler = new PlaylistSharedNotificationHandler(
            Mock.Of<ILogger<PlaylistSharedNotificationHandler>>(),
            notifMock.Object);

        var evt = CreateEvent("dotnetcloud.music", "MusicAlbum");
        await handler.HandleAsync(evt, CancellationToken.None);

        notifMock.Verify(n => n.SendAsync(
            evt.SharedWithUserId,
            It.Is<NotificationDto>(d => d.ActionUrl!.Contains("/music/albums/")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_MusicTrack_SendsNotification()
    {
        var notifMock = new Mock<INotificationService>();
        var handler = new PlaylistSharedNotificationHandler(
            Mock.Of<ILogger<PlaylistSharedNotificationHandler>>(),
            notifMock.Object);

        var evt = CreateEvent("dotnetcloud.music", "Track");
        await handler.HandleAsync(evt, CancellationToken.None);

        notifMock.Verify(n => n.SendAsync(
            evt.SharedWithUserId,
            It.Is<NotificationDto>(d => d.ActionUrl!.Contains("/music/tracks/")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_WrongModule_DoesNotSendNotification()
    {
        var notifMock = new Mock<INotificationService>();
        var handler = new PlaylistSharedNotificationHandler(
            Mock.Of<ILogger<PlaylistSharedNotificationHandler>>(),
            notifMock.Object);

        var evt = CreateEvent("dotnetcloud.video", "Playlist");
        await handler.HandleAsync(evt, CancellationToken.None);

        notifMock.Verify(n => n.SendAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task HandleAsync_UnknownEntityType_DoesNotSendNotification()
    {
        var notifMock = new Mock<INotificationService>();
        var handler = new PlaylistSharedNotificationHandler(
            Mock.Of<ILogger<PlaylistSharedNotificationHandler>>(),
            notifMock.Object);

        var evt = CreateEvent("dotnetcloud.music", "Unknown");
        await handler.HandleAsync(evt, CancellationToken.None);

        notifMock.Verify(n => n.SendAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task HandleAsync_NoNotificationService_CompletesWithoutError()
    {
        var handler = new PlaylistSharedNotificationHandler(
            Mock.Of<ILogger<PlaylistSharedNotificationHandler>>(),
            notificationService: null);

        var evt = CreateEvent("dotnetcloud.music", "Playlist");
        await handler.HandleAsync(evt, CancellationToken.None);
    }

    [TestMethod]
    public async Task HandleAsync_NotificationServiceThrows_DoesNotPropagate()
    {
        var notifMock = new Mock<INotificationService>();
        notifMock.Setup(n => n.SendAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Network error"));

        var handler = new PlaylistSharedNotificationHandler(
            Mock.Of<ILogger<PlaylistSharedNotificationHandler>>(),
            notifMock.Object);

        var evt = CreateEvent("dotnetcloud.music", "Playlist");
        await handler.HandleAsync(evt, CancellationToken.None);
    }

    [TestMethod]
    public async Task HandleAsync_PlaylistActionUrl_ContainsCorrectPath()
    {
        var notifMock = new Mock<INotificationService>();
        var handler = new PlaylistSharedNotificationHandler(
            Mock.Of<ILogger<PlaylistSharedNotificationHandler>>(),
            notifMock.Object);

        var entityId = Guid.NewGuid();
        var evt = new ResourceSharedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SharedByUserId = Guid.NewGuid(),
            SharedWithUserId = Guid.NewGuid(),
            SourceModuleId = "dotnetcloud.music",
            EntityType = "Playlist",
            EntityId = entityId,
            EntityDisplayName = "My Favorites",
            Permission = "ReadOnly"
        };

        await handler.HandleAsync(evt, CancellationToken.None);

        notifMock.Verify(n => n.SendAsync(
            It.IsAny<Guid>(),
            It.Is<NotificationDto>(d => d.ActionUrl == $"/music/playlists/{entityId}"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_IncludesDisplayNameInMessage()
    {
        var notifMock = new Mock<INotificationService>();
        var handler = new PlaylistSharedNotificationHandler(
            Mock.Of<ILogger<PlaylistSharedNotificationHandler>>(),
            notifMock.Object);

        var evt = new ResourceSharedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SharedByUserId = Guid.NewGuid(),
            SharedWithUserId = Guid.NewGuid(),
            SourceModuleId = "dotnetcloud.music",
            EntityType = "Playlist",
            EntityId = Guid.NewGuid(),
            EntityDisplayName = "Road Trip Mix",
            Permission = "ReadWrite"
        };

        await handler.HandleAsync(evt, CancellationToken.None);

        notifMock.Verify(n => n.SendAsync(
            It.IsAny<Guid>(),
            It.Is<NotificationDto>(d => d.Message!.Contains("Road Trip Mix")),
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
