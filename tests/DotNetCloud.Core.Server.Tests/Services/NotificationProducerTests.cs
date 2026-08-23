using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

[TestClass]
public class NotificationProducerTests
{
    private Mock<INotificationService> _notificationService = null!;
    private NotificationProducer _producer = null!;

    [TestInitialize]
    public void Setup()
    {
        _notificationService = new Mock<INotificationService>();

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(sp => sp.GetService(typeof(INotificationService)))
            .Returns(_notificationService.Object);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        _producer = new NotificationProducer(scopeFactory.Object);
    }

    [TestMethod]
    public async Task FileSharedEvent_NullRecipient_ProducesNoNotification()
    {
        var @event = new FileSharedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = Guid.CreateVersion7(),
            FileName = "report.pdf",
            ShareId = Guid.CreateVersion7(),
            ShareType = "PublicLink",
            SharedWithUserId = null,
            SharedByUserId = Guid.CreateVersion7()
        };

        await _producer.HandleAsync(@event);

        _notificationService.Verify(
            n => n.SendAsync(It.IsAny<Guid>(), It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task FileSharedEvent_WithUser_ProducesShareNotification()
    {
        var fileNodeId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var @event = new FileSharedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = fileNodeId,
            FileName = "report.pdf",
            ShareId = Guid.CreateVersion7(),
            ShareType = "User",
            SharedWithUserId = userId,
            SharedByUserId = Guid.CreateVersion7()
        };

        await _producer.HandleAsync(@event);

        _notificationService.Verify(
            n => n.SendAsync(
                userId,
                It.Is<NotificationDto>(d =>
                    d.Type == NotificationType.Share &&
                    d.SourceModuleId == "dotnetcloud.files" &&
                    d.Title == "File shared with you" &&
                    d.ActionUrl == $"/apps/files?node={fileNodeId}" &&
                    d.Priority == NotificationPriority.Normal),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task QuotaWarningEvent_ProducesSystemAlertHigh()
    {
        var userId = Guid.CreateVersion7();
        var @event = new QuotaWarningEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            UserId = userId,
            UsedBytes = 8589934592,   // 8 GB
            MaxBytes = 10737418240,   // 10 GB
            UsagePercent = 80
        };

        await _producer.HandleAsync(@event);

        _notificationService.Verify(
            n => n.SendAsync(
                userId,
                It.Is<NotificationDto>(d =>
                    d.Type == NotificationType.SystemAlert &&
                    d.Priority == NotificationPriority.High &&
                    d.Title == "Storage almost full" &&
                    d.ActionUrl == "/apps/files"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task QuotaCriticalEvent_ProducesSystemAlertUrgent()
    {
        var userId = Guid.CreateVersion7();
        var @event = new QuotaCriticalEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            UserId = userId,
            UsedBytes = 10200547328,  // ~9.5 GB
            MaxBytes = 10737418240,   // 10 GB
            UsagePercent = 95
        };

        await _producer.HandleAsync(@event);

        _notificationService.Verify(
            n => n.SendAsync(
                userId,
                It.Is<NotificationDto>(d =>
                    d.Type == NotificationType.SystemAlert &&
                    d.Priority == NotificationPriority.Urgent &&
                    d.Title == "Storage nearly full"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task PublicLinkAccessedEvent_TargetsCreatedByUser()
    {
        var createdByUserId = Guid.CreateVersion7();
        var fileNodeId = Guid.CreateVersion7();
        var @event = new PublicLinkAccessedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = fileNodeId,
            FileName = "brochure.pdf",
            ShareId = Guid.CreateVersion7(),
            CreatedByUserId = createdByUserId
        };

        await _producer.HandleAsync(@event);

        _notificationService.Verify(
            n => n.SendAsync(
                createdByUserId,
                It.Is<NotificationDto>(d =>
                    d.Type == NotificationType.Info &&
                    d.Priority == NotificationPriority.Normal &&
                    d.ActionUrl == $"/apps/files?node={fileNodeId}"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ShareExpiringEvent_TargetsCreatedByUser()
    {
        var createdByUserId = Guid.CreateVersion7();
        var fileNodeId = Guid.CreateVersion7();
        var @event = new ShareExpiringEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = fileNodeId,
            FileName = "archive.zip",
            ShareId = Guid.CreateVersion7(),
            CreatedByUserId = createdByUserId,
            ExpiresAt = DateTime.UtcNow.AddDays(2)
        };

        await _producer.HandleAsync(@event);

        _notificationService.Verify(
            n => n.SendAsync(
                createdByUserId,
                It.Is<NotificationDto>(d =>
                    d.Type == NotificationType.SystemAlert &&
                    d.Priority == NotificationPriority.High &&
                    d.ActionUrl == $"/apps/files?node={fileNodeId}"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ResourceSharedEvent_ProducesShareNotificationWithLinkMetadata()
    {
        var userId = Guid.CreateVersion7();
        var entityId = Guid.CreateVersion7();
        var @event = new ResourceSharedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            SharedByUserId = Guid.CreateVersion7(),
            SharedWithUserId = userId,
            SourceModuleId = "dotnetcloud.notes",
            EntityType = "Note",
            EntityId = entityId,
            EntityDisplayName = "My Important Note",
            Permission = "ReadWrite"
        };

        await _producer.HandleAsync(@event);

        _notificationService.Verify(
            n => n.SendAsync(
                userId,
                It.Is<NotificationDto>(d =>
                    d.Type == NotificationType.Share &&
                    d.Priority == NotificationPriority.Normal &&
                    d.Title == "Note shared with you" &&
                    d.ActionUrl == $"/notes?id={entityId}" &&
                    d.RelatedEntityType == CrossModuleLinkType.Note &&
                    d.RelatedEntityId == entityId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task UserMentionedEvent_ProducesMentionNotification()
    {
        var userId = Guid.CreateVersion7();
        var contentId = Guid.CreateVersion7();
        var @event = new UserMentionedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            MentionedUserId = userId,
            MentionedByUserId = Guid.CreateVersion7(),
            SourceModuleId = "dotnetcloud.notes",
            ContentType = "Note",
            ContentId = contentId,
            ContentTitle = "Sprint Planning"
        };

        await _producer.HandleAsync(@event);

        _notificationService.Verify(
            n => n.SendAsync(
                userId,
                It.Is<NotificationDto>(d =>
                    d.Type == NotificationType.Mention &&
                    d.Priority == NotificationPriority.High &&
                    d.Title == "You were mentioned" &&
                    d.Message == "Sprint Planning" &&
                    d.ActionUrl == $"/notes?id={contentId}"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ReminderTriggeredEvent_WithDueDate_ProducesReminderNotification()
    {
        var userId = Guid.CreateVersion7();
        var entityId = Guid.CreateVersion7();
        var dueAt = DateTime.UtcNow.AddMinutes(15);
        var @event = new ReminderTriggeredEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            UserId = userId,
            SourceModuleId = "dotnetcloud.calendar",
            EntityType = "CalendarEvent",
            EntityId = entityId,
            Title = "Team Meeting in 15 minutes",
            DueAtUtc = dueAt
        };

        await _producer.HandleAsync(@event);

        _notificationService.Verify(
            n => n.SendAsync(
                userId,
                It.Is<NotificationDto>(d =>
                    d.Type == NotificationType.Reminder &&
                    d.Priority == NotificationPriority.High &&
                    d.Title == "Team Meeting in 15 minutes" &&
                    d.ActionUrl == $"/calendar?eventId={entityId}" &&
                    d.RelatedEntityType == CrossModuleLinkType.CalendarEvent &&
                    d.RelatedEntityId == entityId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ReminderTriggeredEvent_NoDueDate_ProducesReminderNotification()
    {
        var userId = Guid.CreateVersion7();
        var @event = new ReminderTriggeredEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            UserId = userId,
            SourceModuleId = "dotnetcloud.calendar",
            EntityType = "CalendarEvent",
            EntityId = Guid.CreateVersion7(),
            Title = "Overdue Task"
        };

        await _producer.HandleAsync(@event);

        _notificationService.Verify(
            n => n.SendAsync(
                userId,
                It.Is<NotificationDto>(d =>
                    d.Type == NotificationType.Reminder &&
                    d.Priority == NotificationPriority.High &&
                    d.Title == "Overdue Task" &&
                    d.Message == "Reminder"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
