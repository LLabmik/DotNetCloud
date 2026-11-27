using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Photos.Events;

/// <summary>
/// Handles AlbumSharedEvent to send notifications to the recipient user, or to every member of a
/// team the album was shared with.
/// </summary>
/// <remarks>
/// The event never leaves this process (a sandboxed module owns its own bus), so this handler is the
/// only notification path — the core's in-process producer no longer sees Photos events. Recipients
/// are told to open the module's framed app, not a core route.
/// </remarks>
public sealed class AlbumSharedNotificationHandler : IEventHandler<AlbumSharedEvent>
{
    /// <summary>Shell path the notification opens (the core frames the module app around it).</summary>
    private const string ModuleShellPath = "/modules/dotnetcloud.photos";

    private readonly INotificationService? _notificationService;
    private readonly ITeamDirectory? _teamDirectory;
    private readonly ILogger<AlbumSharedNotificationHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlbumSharedNotificationHandler"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="notificationService">Notification capability, when granted.</param>
    /// <param name="teamDirectory">Team directory capability, used to fan out a team share.</param>
    public AlbumSharedNotificationHandler(
        ILogger<AlbumSharedNotificationHandler> logger,
        INotificationService? notificationService = null,
        ITeamDirectory? teamDirectory = null)
    {
        _logger = logger;
        _notificationService = notificationService;
        _teamDirectory = teamDirectory;
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
            foreach (var recipient in await ResolveRecipientsAsync(@event, cancellationToken))
            {
                var notification = new NotificationDto
                {
                    Id = Guid.CreateVersion7(),
                    UserId = recipient,
                    SourceModuleId = "dotnetcloud.photos",
                    Type = NotificationType.Share,
                    Title = "A photo album was shared with you",
                    Message = $"An album was shared with you ({@event.Permission}).",
                    Priority = NotificationPriority.Normal,
                    ActionUrl = $"{ModuleShellPath}?album={@event.AlbumId}",
                    RelatedEntityId = @event.AlbumId,
                    CreatedAtUtc = DateTime.UtcNow
                };

                await _notificationService.SendAsync(recipient, notification, cancellationToken);
            }

            _logger.LogInformation("Notifications sent for album {AlbumId}", @event.AlbumId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification for album share: Album {AlbumId}", @event.AlbumId);
        }
    }

    /// <summary>
    /// Returns who should be notified: the direct recipient, or every member of the target team
    /// except the user who shared it.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> ResolveRecipientsAsync(
        AlbumSharedEvent @event,
        CancellationToken cancellationToken)
    {
        if (@event.SharedWithUserId is { } recipientUserId)
        {
            return [recipientUserId];
        }

        if (@event.SharedWithTeamId is not { } teamId || _teamDirectory is null)
        {
            // A team share with no team directory (capability not granted) is not fanned out: better
            // to notify nobody than to guess at membership.
            _logger.LogDebug("Team-targeted album share skipped: no team directory available");
            return [];
        }

        var members = await _teamDirectory.GetTeamMembersAsync(teamId, cancellationToken);
        return members
            .Select(member => member.UserId)
            .Where(userId => userId != @event.SharedByUserId)
            .Distinct()
            .ToArray();
    }
}
