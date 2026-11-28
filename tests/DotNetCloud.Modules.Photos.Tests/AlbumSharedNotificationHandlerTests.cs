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
            evt.SharedWithUserId!.Value,
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
    public async Task HandleAsync_TeamShareWithoutATeamDirectory_NotifiesNobody()
    {
        var notifMock = new Mock<INotificationService>();
        var handler = new AlbumSharedNotificationHandler(
            Mock.Of<ILogger<AlbumSharedNotificationHandler>>(),
            notifMock.Object);

        var evt = new AlbumSharedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            AlbumId = Guid.CreateVersion7(),
            SharedByUserId = Guid.CreateVersion7(),
            SharedWithUserId = null,
            SharedWithTeamId = Guid.CreateVersion7(),
            Permission = "ReadOnly"
        };

        await handler.HandleAsync(evt, CancellationToken.None);

        notifMock.Verify(
            n => n.SendAsync(It.IsAny<Guid>(), It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task HandleAsync_NotificationHasCorrectActionUrl()
    {
        var notifMock = new Mock<INotificationService>();
        var handler = new AlbumSharedNotificationHandler(
            Mock.Of<ILogger<AlbumSharedNotificationHandler>>(),
            notifMock.Object);

        var albumId = Guid.CreateVersion7();
        var evt = new AlbumSharedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            AlbumId = albumId,
            SharedByUserId = Guid.CreateVersion7(),
            SharedWithUserId = Guid.CreateVersion7(),
            Permission = "ReadOnly"
        };

        await handler.HandleAsync(evt, CancellationToken.None);

        notifMock.Verify(n => n.SendAsync(
            It.IsAny<Guid>(),
            It.Is<NotificationDto>(d =>
                d.ActionUrl == $"/modules/dotnetcloud.photos?album={albumId}"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_TeamShare_FansOutToEveryMemberExceptTheSharer()
    {
        var notifMock = new Mock<INotificationService>();
        var teamId = Guid.CreateVersion7();
        var sharerId = Guid.CreateVersion7();
        var memberOne = Guid.CreateVersion7();
        var memberTwo = Guid.CreateVersion7();

        var teamDirectory = new Mock<ITeamDirectory>();
        teamDirectory
            .Setup(d => d.GetTeamMembersAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new TeamMemberInfo { TeamId = teamId, UserId = sharerId, JoinedAt = DateTime.UtcNow },
                new TeamMemberInfo { TeamId = teamId, UserId = memberOne, JoinedAt = DateTime.UtcNow },
                new TeamMemberInfo { TeamId = teamId, UserId = memberTwo, JoinedAt = DateTime.UtcNow },
            ]);

        var handler = new AlbumSharedNotificationHandler(
            Mock.Of<ILogger<AlbumSharedNotificationHandler>>(),
            notifMock.Object,
            teamDirectory.Object);

        await handler.HandleAsync(
            new AlbumSharedEvent
            {
                EventId = Guid.CreateVersion7(),
                CreatedAt = DateTime.UtcNow,
                AlbumId = Guid.CreateVersion7(),
                SharedByUserId = sharerId,
                SharedWithTeamId = teamId,
                Permission = "ReadOnly"
            },
            CancellationToken.None);

        notifMock.Verify(
            n => n.SendAsync(memberOne, It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
        notifMock.Verify(
            n => n.SendAsync(memberTwo, It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
        notifMock.Verify(
            n => n.SendAsync(sharerId, It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            AlbumId = Guid.CreateVersion7(),
            SharedByUserId = Guid.CreateVersion7(),
            SharedWithUserId = Guid.CreateVersion7(),
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

        var recipientId = Guid.CreateVersion7();
        var evt = new AlbumSharedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            AlbumId = Guid.CreateVersion7(),
            SharedByUserId = Guid.CreateVersion7(),
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
        EventId = Guid.CreateVersion7(),
        CreatedAt = DateTime.UtcNow,
        AlbumId = Guid.CreateVersion7(),
        SharedByUserId = Guid.CreateVersion7(),
        SharedWithUserId = Guid.CreateVersion7(),
        Permission = "ReadOnly"
    };
}
