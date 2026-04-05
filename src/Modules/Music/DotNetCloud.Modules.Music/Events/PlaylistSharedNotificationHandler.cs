using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Music.Events;

/// <summary>
/// Handles PlaylistCreatedEvent to send notifications (for shared playlists).
/// </summary>
public sealed class PlaylistSharedNotificationHandler : IEventHandler<ResourceSharedEvent>
{
    private static readonly HashSet<string> MusicEntityTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Playlist", "MusicAlbum", "Track"
    };

    private readonly INotificationService? _notificationService;
    private readonly ILogger<PlaylistSharedNotificationHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistSharedNotificationHandler"/> class.
    /// </summary>
    public PlaylistSharedNotificationHandler(ILogger<PlaylistSharedNotificationHandler> logger, INotificationService? notificationService = null)
    {
        _logger = logger;
        _notificationService = notificationService;
    }

    /// <inheritdoc />
    public async Task HandleAsync(ResourceSharedEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event.SourceModuleId != "dotnetcloud.music" || !MusicEntityTypes.Contains(@event.EntityType))
        {
            return;
        }

        if (_notificationService is null)
        {
            _logger.LogDebug("Notification service not available — skipping music share notification");
            return;
        }

        try
        {
            var notification = new NotificationDto
            {
                Id = Guid.NewGuid(),
                UserId = @event.SharedWithUserId,
                SourceModuleId = "dotnetcloud.music",
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
                "Notification sent for music share: {EntityType} {EntityId} shared with user {UserId}",
                @event.EntityType, @event.EntityId, @event.SharedWithUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification for music share: {EntityType} {EntityId}",
                @event.EntityType, @event.EntityId);
        }
    }

    private static string FormatEntityType(string entityType) => entityType switch
    {
        "Playlist" => "playlist",
        "MusicAlbum" => "album",
        "Track" => "track",
        _ => entityType.ToLowerInvariant()
    };

    private static string GetActionUrl(string entityType, Guid entityId) => entityType switch
    {
        "Playlist" => $"/music/playlists/{entityId}",
        "MusicAlbum" => $"/music/albums/{entityId}",
        "Track" => $"/music/tracks/{entityId}",
        _ => $"/music"
    };
}
