using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Server.Services;
using DotNetCloud.Core.Services.ModuleApis;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

[TestClass]
public class PushNotificationChannelTests
{
    private Mock<IChatApiClient> _chatApiClient = null!;
    private PushNotificationChannel _channel = null!;

    [TestInitialize]
    public void Setup()
    {
        _chatApiClient = new Mock<IChatApiClient>();
        _channel = new PushNotificationChannel(_chatApiClient.Object);
    }

    [TestMethod]
    public async Task DeliverAsync_FileShareFromFiles_MapsToFileSharedCategory()
    {
        var notification = CreateNotification(NotificationType.Share, "dotnetcloud.files");

        await _channel.DeliverAsync(notification);

        _chatApiClient.Verify(c => c.SendPushNotificationAsync(
            notification.UserId,
            notification.Title,
            notification.Message!,
            "FileShared",
            It.Is<IReadOnlyDictionary<string, string>>(d =>
                d["type"] == "Share" &&
                d["actionUrl"] == notification.ActionUrl &&
                d["sourceModuleId"] == "dotnetcloud.files"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DeliverAsync_ShareFromOtherModule_MapsToResourceSharedCategory()
    {
        var notification = CreateNotification(NotificationType.Share, "dotnetcloud.notes");

        await _channel.DeliverAsync(notification);

        _chatApiClient.Verify(c => c.SendPushNotificationAsync(
            notification.UserId,
            notification.Title,
            notification.Message!,
            "ResourceShared",
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DeliverAsync_Mention_MapsToMentionCategory()
    {
        var notification = CreateNotification(NotificationType.Mention, "dotnetcloud.notes");

        await _channel.DeliverAsync(notification);

        _chatApiClient.Verify(c => c.SendPushNotificationAsync(
            notification.UserId,
            notification.Title,
            notification.Message!,
            "Mention",
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DeliverAsync_Reminder_MapsToReminderCategory()
    {
        var notification = CreateNotification(NotificationType.Reminder, "dotnetcloud.calendar");

        await _channel.DeliverAsync(notification);

        _chatApiClient.Verify(c => c.SendPushNotificationAsync(
            notification.UserId,
            notification.Title,
            notification.Message!,
            "Reminder",
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DeliverAsync_Invitation_MapsToCalendarInvitationCategory()
    {
        var notification = CreateNotification(NotificationType.Invitation, "dotnetcloud.calendar");

        await _channel.DeliverAsync(notification);

        _chatApiClient.Verify(c => c.SendPushNotificationAsync(
            notification.UserId,
            notification.Title,
            notification.Message!,
            "CalendarInvitation",
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DeliverAsync_SystemAlert_MapsToSystemCategory()
    {
        var notification = CreateNotification(NotificationType.SystemAlert, "dotnetcloud.files");

        await _channel.DeliverAsync(notification);

        _chatApiClient.Verify(c => c.SendPushNotificationAsync(
            notification.UserId,
            notification.Title,
            notification.Message!,
            "System",
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DeliverAsync_Info_MapsToSystemCategory()
    {
        var notification = CreateNotification(NotificationType.Info, "dotnetcloud.files");

        await _channel.DeliverAsync(notification);

        _chatApiClient.Verify(c => c.SendPushNotificationAsync(
            notification.UserId,
            notification.Title,
            notification.Message!,
            "System",
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static NotificationDto CreateNotification(NotificationType type, string sourceModuleId) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = Guid.CreateVersion7(),
        SourceModuleId = sourceModuleId,
        Type = type,
        Title = "Test title",
        Message = "Test body",
        Priority = NotificationPriority.Normal,
        ActionUrl = "/apps/files?node=abc",
        CreatedAtUtc = DateTime.UtcNow
    };
}
