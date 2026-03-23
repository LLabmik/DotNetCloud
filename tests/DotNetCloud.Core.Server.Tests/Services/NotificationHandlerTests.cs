using DotNetCloud.Core.Events;
using DotNetCloud.Core.Server.Services;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

[TestClass]
public class NotificationHandlerTests
{
    private Mock<IPushNotificationService> _pushService = null!;

    [TestInitialize]
    public void Setup()
    {
        _pushService = new Mock<IPushNotificationService>();
    }

    [TestMethod]
    public async Task ResourceSharedHandler_SendsNotification()
    {
        var handler = new ResourceSharedNotificationHandler(
            _pushService.Object,
            NullLogger<ResourceSharedNotificationHandler>.Instance);

        var userId = Guid.NewGuid();
        var @event = new ResourceSharedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SharedByUserId = Guid.NewGuid(),
            SharedWithUserId = userId,
            SourceModuleId = "dotnetcloud.notes",
            EntityType = "Note",
            EntityId = Guid.NewGuid(),
            EntityDisplayName = "My Important Note",
            Permission = "ReadWrite"
        };

        await handler.HandleAsync(@event);

        _pushService.Verify(p => p.SendAsync(
            userId,
            It.Is<PushNotification>(n =>
                n.Title.Contains("Note") &&
                n.Body.Contains("My Important Note") &&
                n.Category == NotificationCategory.ResourceShared),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task UserMentionedHandler_SendsNotification()
    {
        var handler = new UserMentionedNotificationHandler(
            _pushService.Object,
            NullLogger<UserMentionedNotificationHandler>.Instance);

        var userId = Guid.NewGuid();
        var @event = new UserMentionedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            MentionedUserId = userId,
            MentionedByUserId = Guid.NewGuid(),
            SourceModuleId = "dotnetcloud.notes",
            ContentType = "Note",
            ContentId = Guid.NewGuid(),
            ContentTitle = "Sprint Planning"
        };

        await handler.HandleAsync(@event);

        _pushService.Verify(p => p.SendAsync(
            userId,
            It.Is<PushNotification>(n =>
                n.Title.Contains("mentioned") &&
                n.Body.Contains("Sprint Planning") &&
                n.Category == NotificationCategory.Mention),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ReminderHandler_SendsNotification()
    {
        var handler = new ReminderNotificationHandler(
            _pushService.Object,
            NullLogger<ReminderNotificationHandler>.Instance);

        var userId = Guid.NewGuid();
        var @event = new ReminderTriggeredEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UserId = userId,
            SourceModuleId = "dotnetcloud.calendar",
            EntityType = "CalendarEvent",
            EntityId = Guid.NewGuid(),
            Title = "Team Meeting in 15 minutes",
            DueAtUtc = DateTime.UtcNow.AddMinutes(15)
        };

        await handler.HandleAsync(@event);

        _pushService.Verify(p => p.SendAsync(
            userId,
            It.Is<PushNotification>(n =>
                n.Title == "Reminder" &&
                n.Body == "Team Meeting in 15 minutes" &&
                n.Category == NotificationCategory.Reminder &&
                n.Data.ContainsKey("dueAtUtc")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ReminderHandler_NoDueDate_OmitsDueAtFromData()
    {
        var handler = new ReminderNotificationHandler(
            _pushService.Object,
            NullLogger<ReminderNotificationHandler>.Instance);

        var @event = new ReminderTriggeredEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UserId = Guid.NewGuid(),
            SourceModuleId = "dotnetcloud.calendar",
            EntityType = "CalendarEvent",
            EntityId = Guid.NewGuid(),
            Title = "Overdue Task"
        };

        await handler.HandleAsync(@event);

        _pushService.Verify(p => p.SendAsync(
            It.IsAny<Guid>(),
            It.Is<PushNotification>(n => !n.Data.ContainsKey("dueAtUtc")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
