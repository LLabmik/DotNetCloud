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
    IEventHandler<ShareExpiringEvent>,
    IEventHandler<AlbumSharedEvent>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public NotificationProducer(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public async Task HandleAsync(ResourceSharedEvent e, CancellationToken ct = default)
    {
        if (e.SharedWithUserId is { } userId)
        {
            // User-targeted share → single recipient notification.
            await SendAsync(new NotificationDto
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
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
            return;
        }

        // Team-targeted share → fan out to every team member except the sharer.
        if (e.SharedWithTeamId is { } teamId)
        {
            await SendTeamFanOutAsync(
                teamId,
                e.SharedByUserId,
                e.SourceModuleId,
                title: $"{e.EntityType} shared with your team",
                message: $"{e.EntityDisplayName} was shared with your team.",
                actionUrl: BuildActionUrl(e.EntityType, e.EntityId),
                relatedEntityType: MapEntityType(e.EntityType),
                relatedEntityId: e.EntityId,
                createdAtUtc: e.CreatedAt,
                ct);
        }
    }

    /// <inheritdoc />
    public async Task HandleAsync(AlbumSharedEvent e, CancellationToken ct = default)
    {
        if (e.SharedWithUserId is { } userId)
        {
            await SendAsync(new NotificationDto
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                SourceModuleId = "dotnetcloud.photos",
                Type = NotificationType.Share,
                Title = "Album shared with you",
                Message = $"An album was shared with you (permission: {e.Permission}).",
                Priority = NotificationPriority.Normal,
                ActionUrl = $"/photos?album={e.AlbumId}",
                RelatedEntityId = e.AlbumId,
                CreatedAtUtc = e.CreatedAt
            }, ct);
            return;
        }

        if (e.SharedWithTeamId is { } teamId)
        {
            await SendTeamFanOutAsync(
                teamId,
                e.SharedByUserId,
                "dotnetcloud.photos",
                title: "Album shared with your team",
                message: "An album was shared with your team.",
                actionUrl: $"/photos?album={e.AlbumId}",
                relatedEntityType: null,
                relatedEntityId: e.AlbumId,
                createdAtUtc: e.CreatedAt,
                ct);
        }
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
        if (e.SharedWithUserId is { } userId)
        {
            // User-targeted file share.
            await SendAsync(new NotificationDto
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                SourceModuleId = "dotnetcloud.files",
                Type = NotificationType.Share,
                Title = "File shared with you",
                Message = $"\"{e.FileName}\" has been shared with you.",
                Priority = NotificationPriority.Normal,
                ActionUrl = $"/apps/files?node={e.FileNodeId}",
                CreatedAtUtc = e.CreatedAt
            }, ct);
            return;
        }

        // Team-targeted file share → fan out to every team member except the sharer.
        if (e.SharedWithTeamId is { } teamId)
        {
            await SendTeamFanOutAsync(
                teamId,
                e.SharedByUserId,
                "dotnetcloud.files",
                title: "File shared with your team",
                message: $"\"{e.FileName}\" has been shared with your team.",
                actionUrl: $"/apps/files?node={e.FileNodeId}",
                relatedEntityType: null,
                relatedEntityId: e.FileNodeId,
                createdAtUtc: e.CreatedAt,
                ct);
        }
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

    /// <summary>
    /// Fans a team share out to every team member except the sharer — one in-app
    /// notification per member. No-op when the team can't be resolved or has no
    /// other members.
    /// </summary>
    private async Task SendTeamFanOutAsync(
        Guid teamId,
        Guid sharedByUserId,
        string sourceModuleId,
        string title,
        string message,
        string? actionUrl,
        CrossModuleLinkType? relatedEntityType,
        Guid relatedEntityId,
        DateTime createdAtUtc,
        CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var teamDirectory = scope.ServiceProvider.GetService<ITeamDirectory>();
        if (teamDirectory is null)
        {
            return;
        }

        IReadOnlyList<TeamMemberInfo> members;
        try
        {
            members = await teamDirectory.GetTeamMembersAsync(teamId, ct);
        }
        catch (Exception)
        {
            // A membership failure must never break the share operation — no-op.
            return;
        }

        var recipients = members
            .Where(m => m.UserId != sharedByUserId)
            .Select(m => m.UserId)
            .Distinct()
            .ToArray();

        if (recipients.Length == 0)
        {
            return;
        }

        var service = scope.ServiceProvider.GetRequiredService<INotificationService>();
        foreach (var recipientId in recipients)
        {
            await service.SendAsync(recipientId, new NotificationDto
            {
                Id = Guid.CreateVersion7(),
                UserId = recipientId,
                SourceModuleId = sourceModuleId,
                Type = NotificationType.Share,
                Title = title,
                Message = message,
                Priority = NotificationPriority.Normal,
                ActionUrl = actionUrl,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = relatedEntityId,
                CreatedAtUtc = createdAtUtc
            }, ct);
        }
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
