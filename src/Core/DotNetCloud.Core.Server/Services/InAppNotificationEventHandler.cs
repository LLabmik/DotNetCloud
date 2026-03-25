using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Persists in-app notifications for common notification events.
/// Uses <see cref="IServiceScopeFactory"/> to resolve the scoped
/// <see cref="INotificationService"/> per event invocation.
/// </summary>
internal sealed class InAppNotificationEventHandler :
    IEventHandler<ResourceSharedEvent>,
    IEventHandler<UserMentionedEvent>,
    IEventHandler<ReminderTriggeredEvent>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public InAppNotificationEventHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task HandleAsync(ResourceSharedEvent @event, CancellationToken cancellationToken = default)
    {
        var notification = new NotificationDto
        {
            Id = Guid.NewGuid(),
            UserId = @event.SharedWithUserId,
            SourceModuleId = @event.SourceModuleId,
            Type = NotificationType.Share,
            Title = $"{@event.EntityType} shared with you",
            Message = $"{ @event.EntityDisplayName } was shared with permission: {@event.Permission}.",
            Priority = NotificationPriority.Normal,
            ActionUrl = BuildActionUrl(@event.EntityType, @event.EntityId),
            RelatedEntityType = MapEntityType(@event.EntityType),
            RelatedEntityId = @event.EntityId,
            CreatedAtUtc = @event.CreatedAt
        };

        await using var scope = _scopeFactory.CreateAsyncScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        await notificationService.SendAsync(@event.SharedWithUserId, notification, cancellationToken);
    }

    public async Task HandleAsync(UserMentionedEvent @event, CancellationToken cancellationToken = default)
    {
        var notification = new NotificationDto
        {
            Id = Guid.NewGuid(),
            UserId = @event.MentionedUserId,
            SourceModuleId = @event.SourceModuleId,
            Type = NotificationType.Mention,
            Title = "You were mentioned",
            Message = @event.ContentTitle,
            Priority = NotificationPriority.High,
            ActionUrl = BuildActionUrl(@event.ContentType, @event.ContentId),
            RelatedEntityType = MapEntityType(@event.ContentType),
            RelatedEntityId = @event.ContentId,
            CreatedAtUtc = @event.CreatedAt
        };

        await using var scope = _scopeFactory.CreateAsyncScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        await notificationService.SendAsync(@event.MentionedUserId, notification, cancellationToken);
    }

    public async Task HandleAsync(ReminderTriggeredEvent @event, CancellationToken cancellationToken = default)
    {
        var notification = new NotificationDto
        {
            Id = Guid.NewGuid(),
            UserId = @event.UserId,
            SourceModuleId = @event.SourceModuleId,
            Type = NotificationType.Reminder,
            Title = @event.Title,
            Message = @event.DueAtUtc.HasValue
                ? $"Due at {@event.DueAtUtc.Value:u}"
                : "Reminder",
            Priority = NotificationPriority.High,
            ActionUrl = BuildActionUrl(@event.EntityType, @event.EntityId),
            RelatedEntityType = MapEntityType(@event.EntityType),
            RelatedEntityId = @event.EntityId,
            CreatedAtUtc = @event.CreatedAt
        };

        await using var scope = _scopeFactory.CreateAsyncScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        await notificationService.SendAsync(@event.UserId, notification, cancellationToken);
    }

    private static string BuildActionUrl(string entityType, Guid entityId)
    {
        return entityType.ToLowerInvariant() switch
        {
            "contact" => $"/contacts?id={entityId}",
            "calendar" => $"/calendar?id={entityId}",
            "calendarevent" => $"/calendar?eventId={entityId}",
            "note" => $"/notes?id={entityId}",
            _ => "/"
        };
    }

    private static CrossModuleLinkType? MapEntityType(string entityType)
    {
        return entityType.ToLowerInvariant() switch
        {
            "contact" => CrossModuleLinkType.Contact,
            "note" => CrossModuleLinkType.Note,
            "calendarevent" => CrossModuleLinkType.CalendarEvent,
            _ => null
        };
    }
}
