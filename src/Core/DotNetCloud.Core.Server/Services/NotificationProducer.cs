using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Builds and persists an in-app notification for every cross-module notification event.
/// This is the single producer of bell notifications.
/// </summary>
internal sealed class NotificationProducer :
    IEventHandler<ResourceSharedEvent>,
    IEventHandler<UserMentionedEvent>,
    IEventHandler<ReminderTriggeredEvent>,
    IEventHandler<FileSharedEvent>,
    IEventHandler<QuotaWarningEvent>,
    IEventHandler<QuotaCriticalEvent>,
    IEventHandler<PublicLinkAccessedEvent>,
    IEventHandler<ShareExpiringEvent>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public NotificationProducer(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public async Task HandleAsync(ResourceSharedEvent e, CancellationToken ct = default)
    {
        await SendAsync(new NotificationDto
        {
            Id = Guid.CreateVersion7(),
            UserId = e.SharedWithUserId,
            SourceModuleId = e.SourceModuleId,
            Type = NotificationType.Share,
            Title = $"{e.EntityType} shared with you",
            Message = $"{e.EntityDisplayName} was shared with permission: {e.Permission}.",
            Priority = NotificationPriority.Normal,
            ActionUrl = BuildActionUrl(e.EntityType, e.EntityId),
            RelatedEntityType = MapEntityType(e.EntityType),
            RelatedEntityId = e.EntityId,
            CreatedAtUtc = e.CreatedAt
        }, ct);
    }

    /// <inheritdoc />
    public async Task HandleAsync(UserMentionedEvent e, CancellationToken ct = default)
    {
        await SendAsync(new NotificationDto
        {
            Id = Guid.CreateVersion7(),
            UserId = e.MentionedUserId,
            SourceModuleId = e.SourceModuleId,
            Type = NotificationType.Mention,
            Title = "You were mentioned",
            Message = e.ContentTitle,
            Priority = NotificationPriority.High,
            ActionUrl = BuildActionUrl(e.ContentType, e.ContentId),
            RelatedEntityType = MapEntityType(e.ContentType),
            RelatedEntityId = e.ContentId,
            CreatedAtUtc = e.CreatedAt
        }, ct);
    }

    /// <inheritdoc />
    public async Task HandleAsync(ReminderTriggeredEvent e, CancellationToken ct = default)
    {
        await SendAsync(new NotificationDto
        {
            Id = Guid.CreateVersion7(),
            UserId = e.UserId,
            SourceModuleId = e.SourceModuleId,
            Type = NotificationType.Reminder,
            Title = e.Title,
            Message = e.DueAtUtc.HasValue ? $"Due at {e.DueAtUtc.Value:u}" : "Reminder",
            Priority = NotificationPriority.High,
            ActionUrl = BuildActionUrl(e.EntityType, e.EntityId),
            RelatedEntityType = MapEntityType(e.EntityType),
            RelatedEntityId = e.EntityId,
            CreatedAtUtc = e.CreatedAt
        }, ct);
    }

    /// <inheritdoc />
    public async Task HandleAsync(FileSharedEvent e, CancellationToken ct = default)
    {
        // Only user-targeted shares; public-link shares do not target a user.
        if (e.SharedWithUserId is null)
            return;

        await SendAsync(new NotificationDto
        {
            Id = Guid.CreateVersion7(),
            UserId = e.SharedWithUserId.Value,
            SourceModuleId = "dotnetcloud.files",
            Type = NotificationType.Share,
            Title = "File shared with you",
            Message = $"\"{e.FileName}\" has been shared with you.",
            Priority = NotificationPriority.Normal,
            ActionUrl = $"/apps/files?node={e.FileNodeId}",
            CreatedAtUtc = e.CreatedAt
        }, ct);
    }

    /// <inheritdoc />
    public async Task HandleAsync(QuotaWarningEvent e, CancellationToken ct = default)
    {
        await SendAsync(new NotificationDto
        {
            Id = Guid.CreateVersion7(),
            UserId = e.UserId,
            SourceModuleId = "dotnetcloud.files",
            Type = NotificationType.SystemAlert,
            Title = "Storage almost full",
            Message = $"You're using {FormatBytes(e.UsedBytes)} of {FormatBytes(e.MaxBytes)} ({e.UsagePercent:F0}%).",
            Priority = NotificationPriority.High,
            ActionUrl = "/apps/files",
            CreatedAtUtc = e.CreatedAt
        }, ct);
    }

    /// <inheritdoc />
    public async Task HandleAsync(QuotaCriticalEvent e, CancellationToken ct = default)
    {
        await SendAsync(new NotificationDto
        {
            Id = Guid.CreateVersion7(),
            UserId = e.UserId,
            SourceModuleId = "dotnetcloud.files",
            Type = NotificationType.SystemAlert,
            Title = "Storage nearly full",
            Message = $"You're using {FormatBytes(e.UsedBytes)} of {FormatBytes(e.MaxBytes)} ({e.UsagePercent:F0}%). Free up space to continue uploading.",
            Priority = NotificationPriority.Urgent,
            ActionUrl = "/apps/files",
            CreatedAtUtc = e.CreatedAt
        }, ct);
    }

    /// <inheritdoc />
    public async Task HandleAsync(PublicLinkAccessedEvent e, CancellationToken ct = default)
    {
        await SendAsync(new NotificationDto
        {
            Id = Guid.CreateVersion7(),
            UserId = e.CreatedByUserId,
            SourceModuleId = "dotnetcloud.files",
            Type = NotificationType.Info,
            Title = "Public link accessed",
            Message = $"Your public link for \"{e.FileName}\" was accessed.",
            Priority = NotificationPriority.Normal,
            ActionUrl = $"/apps/files?node={e.FileNodeId}",
            CreatedAtUtc = e.CreatedAt
        }, ct);
    }

    /// <inheritdoc />
    public async Task HandleAsync(ShareExpiringEvent e, CancellationToken ct = default)
    {
        await SendAsync(new NotificationDto
        {
            Id = Guid.CreateVersion7(),
            UserId = e.CreatedByUserId,
            SourceModuleId = "dotnetcloud.files",
            Type = NotificationType.SystemAlert,
            Title = "Share expiring soon",
            Message = $"Your share for \"{e.FileName}\" expires at {e.ExpiresAt:u}.",
            Priority = NotificationPriority.High,
            ActionUrl = $"/apps/files?node={e.FileNodeId}",
            CreatedAtUtc = e.CreatedAt
        }, ct);
    }

    private async Task SendAsync(NotificationDto notification, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<INotificationService>();
        await service.SendAsync(notification.UserId, notification, ct);
    }

    // Keep the URL shapes from the original InAppNotificationEventHandler.
    private static string BuildActionUrl(string entityType, Guid entityId) =>
        entityType.ToLowerInvariant() switch
        {
            "contact" => $"/contacts?id={entityId}",
            "calendar" => $"/calendar?id={entityId}",
            "calendarevent" => $"/calendar?eventId={entityId}",
            "note" => $"/notes?id={entityId}",
            _ => "/"
        };

    private static CrossModuleLinkType? MapEntityType(string entityType) =>
        entityType.ToLowerInvariant() switch
        {
            "contact" => CrossModuleLinkType.Contact,
            "note" => CrossModuleLinkType.Note,
            "calendarevent" => CrossModuleLinkType.CalendarEvent,
            _ => null
        };

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
        >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
        >= 1_024 => $"{bytes / 1_024.0:F1} KB",
        _ => $"{bytes} B"
    };
}
