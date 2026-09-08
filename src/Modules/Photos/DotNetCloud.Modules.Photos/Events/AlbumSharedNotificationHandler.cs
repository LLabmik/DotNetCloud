using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Photos.Events;

/// <summary>
/// Handles AlbumSharedEvent to send notifications to the recipient user.
/// </summary>
public sealed class AlbumSharedNotificationHandler : IEventHandler<AlbumSharedEvent>
{
    private readonly INotificationService? _notificationService;
    private readonly ILogger<AlbumSharedNotificationHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlbumSharedNotificationHandler"/> class.
    /// </summary>
    public AlbumSharedNotificationHandler(ILogger<AlbumSharedNotificationHandler> logger, INotificationService? notificationService = null)
    {
        _logger = logger;
        _notificationService = notificationService;
    }

    /// <inheritdoc />
    public async Task HandleAsync(AlbumSharedEvent @event, CancellationToken cancellationToken = default)
    {
        if (_notificationService is null)
        {
            _logger.LogDebug("Notification service not available — skipping album share notification");
            return;
        }

        // Team shares are fanned out per member by the core notification producer
        // (Phase 6); this handler only notifies the direct recipient of a user share.
        if (@event.SharedWithUserId is not { } recipientUserId)
        {
            _logger.LogDebug("Team-targeted album share — skipping direct user notification");
            return;
        }

        try
        {
            var notification = new NotificationDto
            {
                Id = Guid.CreateVersion7(),
                UserId = recipientUserId,
                SourceModuleId = "dotnetcloud.photos",
                Type = NotificationType.Share,
                Title = "A photo album was shared with you",
                Message = $"Album '{@event.AlbumId}' was shared with you ({@event.Permission})",
                Priority = NotificationPriority.Normal,
                ActionUrl = $"/photos/albums/{@event.AlbumId}",
                RelatedEntityId = @event.AlbumId,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _notificationService.SendAsync(recipientUserId, notification, cancellationToken);

            _logger.LogInformation(
                "Notification sent for album share: Album {AlbumId} shared with user {UserId}",
                @event.AlbumId, recipientUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification for album share: Album {AlbumId}", @event.AlbumId);
        }
    }
}
