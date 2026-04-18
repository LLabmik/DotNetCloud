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

        try
        {
            var notification = new NotificationDto
            {
                Id = Guid.NewGuid(),
                UserId = @event.SharedWithUserId,
                SourceModuleId = "dotnetcloud.photos",
                Type = NotificationType.Share,
                Title = "A photo album was shared with you",
                Message = $"Album '{@event.AlbumId}' was shared with you ({@event.Permission})",
                Priority = NotificationPriority.Normal,
                ActionUrl = $"/photos/albums/{@event.AlbumId}",
                RelatedEntityId = @event.AlbumId,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _notificationService.SendAsync(@event.SharedWithUserId, notification, cancellationToken);

            _logger.LogInformation(
                "Notification sent for album share: Album {AlbumId} shared with user {UserId}",
                @event.AlbumId, @event.SharedWithUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification for album share: Album {AlbumId}", @event.AlbumId);
        }
    }
}
