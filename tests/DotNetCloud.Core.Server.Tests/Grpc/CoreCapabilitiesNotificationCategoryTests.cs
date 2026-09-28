using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Server.Grpc.Services;

namespace DotNetCloud.Core.Server.Tests.Grpc;

/// <summary>
/// Tests for the notification type and priority derived from the category a module supplies when it
/// sends a notification through the <c>SendNotification</c> capability.
/// </summary>
/// <remarks>
/// The category used to be accepted and then ignored: every module-sent notification was stored as a
/// generic <see cref="NotificationType.Info"/> / <see cref="NotificationPriority.Normal"/> entry,
/// including calendar reminders, which the bell is meant to present as reminders.
/// </remarks>
[TestClass]
public class CoreCapabilitiesNotificationCategoryTests
{
    [TestMethod]
    [DataRow("Reminder")]
    [DataRow("reminder")]
    [DataRow("REMINDER")]
    public void MapNotificationCategory_Reminder_IsHighPriorityReminder(string category)
    {
        var (type, priority) = CoreCapabilitiesServiceImpl.MapNotificationCategory(category);

        Assert.AreEqual(NotificationType.Reminder, type);
        Assert.AreEqual(NotificationPriority.High, priority);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("Info")]
    [DataRow("ChatMention")]
    [DataRow("IncomingCall")]
    public void MapNotificationCategory_OtherCategories_KeepTheGenericDefaults(string? category)
    {
        var (type, priority) = CoreCapabilitiesServiceImpl.MapNotificationCategory(category);

        Assert.AreEqual(NotificationType.Info, type);
        Assert.AreEqual(NotificationPriority.Normal, priority);
    }
}
