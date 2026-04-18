using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Events;

/// <summary>
/// Handles ResourceSharedEvent to send notifications for video shares.
/// </summary>
public sealed class VideoSharedNotificationHandler : IEventHandler<ResourceSharedEvent>
{
    private static readonly HashSet<string> VideoEntityTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Video", "VideoCollection"
    };

    private readonly INotificationService? _notificationService;
    private readonly ILogger<VideoSharedNotificationHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoSharedNotificationHandler"/> class.
    /// </summary>
    public VideoSharedNotificationHandler(ILogger<VideoSharedNotificationHandler> logger, INotificationService? notificationService = null)
    {
        _logger = logger;
        _notificationService = notificationService;
    }

    /// <inheritdoc />
    public async Task HandleAsync(ResourceSharedEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event.SourceModuleId != "dotnetcloud.video" || !VideoEntityTypes.Contains(@event.EntityType))
        {
            return;
        }

        if (_notificationService is null)
        {
            _logger.LogDebug("Notification service not available — skipping video share notification");
            return;
        }

        try
        {
            var notification = new NotificationDto
            {
                Id = Guid.NewGuid(),
                UserId = @event.SharedWithUserId,
                SourceModuleId = "dotnetcloud.video",
                Type = NotificationType.Share,
                Title = $"A {FormatEntityType(@event.EntityType)} was shared with you",
                Message = $"'{@event.EntityDisplayName}' was shared with you ({@event.Permission})",
                Priority = NotificationPriority.Normal,
                ActionUrl = GetActionUrl(@event.EntityType, @event.EntityId),
                RelatedEntityId = @event.EntityId,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _notificationService.SendAsync(@event.SharedWithUserId, notification, cancellationToken);

            _logger.LogInformation(
                "Notification sent for video share: {EntityType} {EntityId} shared with user {UserId}",
                @event.EntityType, @event.EntityId, @event.SharedWithUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification for video share: {EntityType} {EntityId}",
                @event.EntityType, @event.EntityId);
        }
    }

    private static string FormatEntityType(string entityType) => entityType switch
    {
        "Video" => "video",
        "VideoCollection" => "video collection",
        _ => entityType.ToLowerInvariant()
    };

    private static string GetActionUrl(string entityType, Guid entityId) => entityType switch
    {
        "Video" => $"/video/{entityId}",
        "VideoCollection" => $"/video/collections/{entityId}",
        _ => $"/video"
    };
}
