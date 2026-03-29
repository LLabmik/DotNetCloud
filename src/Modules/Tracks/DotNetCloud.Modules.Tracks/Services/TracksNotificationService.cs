using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Services;

/// <summary>
/// Sends Tracks-specific notifications via <see cref="INotificationService"/>.
/// Parses @mentions from content using <see cref="MentionParser"/> and resolves
/// usernames via <see cref="IUserDirectory"/>.
/// </summary>
internal sealed class TracksNotificationService : ITracksNotificationService
{
    private const string ModuleId = "dotnetcloud.tracks";

    private readonly INotificationService? _notificationService;
    private readonly IUserDirectory? _userDirectory;
    private readonly ILogger<TracksNotificationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TracksNotificationService"/> class.
    /// </summary>
    public TracksNotificationService(
        ILogger<TracksNotificationService> logger,
        INotificationService? notificationService = null,
        IUserDirectory? userDirectory = null)
    {
        _logger = logger;
        _notificationService = notificationService;
        _userDirectory = userDirectory;
    }

    /// <inheritdoc />
    public async Task NotifyCardAssignedAsync(Guid boardId, Guid cardId, string cardTitle, Guid assignedUserId, Guid assignedByUserId, CancellationToken cancellationToken)
    {
        if (_notificationService is null) return;
        if (assignedUserId == assignedByUserId) return; // Don't notify for self-assignment

        var notification = new NotificationDto
        {
            Id = Guid.NewGuid(),
            UserId = assignedUserId,
            SourceModuleId = ModuleId,
            Type = NotificationType.Update,
            Title = $"You were assigned to card \"{cardTitle}\"",
            Message = "A card has been assigned to you on a Tracks board.",
            Priority = NotificationPriority.Normal,
            ActionUrl = $"/apps/tracks?board={boardId}&card={cardId}",
            CreatedAtUtc = DateTime.UtcNow
        };

        try
        {
            await _notificationService.SendAsync(assignedUserId, notification, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send card assignment notification to user {UserId}", assignedUserId);
        }
    }

    /// <inheritdoc />
    public async Task NotifyMentionsAsync(Guid boardId, Guid cardId, string cardTitle, Guid commentAuthorId, string commentContent, CancellationToken cancellationToken)
    {
        if (_notificationService is null || _userDirectory is null) return;

        var usernames = MentionParser.ParseMentions(commentContent);
        if (usernames.Count == 0) return;

        foreach (var username in usernames)
        {
            try
            {
                var userId = await _userDirectory.FindUserIdByUsernameAsync(username, cancellationToken);
                if (userId is null || userId.Value == commentAuthorId) continue;

                var notification = new NotificationDto
                {
                    Id = Guid.NewGuid(),
                    UserId = userId.Value,
                    SourceModuleId = ModuleId,
                    Type = NotificationType.Mention,
                    Title = $"You were mentioned in a comment on \"{cardTitle}\"",
                    Message = $"@{username} was mentioned in a Tracks card comment.",
                    Priority = NotificationPriority.Normal,
                    ActionUrl = $"/apps/tracks?board={boardId}&card={cardId}",
                    CreatedAtUtc = DateTime.UtcNow
                };

                await _notificationService.SendAsync(userId.Value, notification, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send mention notification for @{Username}", username);
            }
        }
    }

    /// <inheritdoc />
    public async Task NotifySprintStartedAsync(Guid boardId, string sprintTitle, Guid startedByUserId, IReadOnlyList<Guid> boardMemberIds, CancellationToken cancellationToken)
    {
        if (_notificationService is null) return;

        var recipients = boardMemberIds.Where(id => id != startedByUserId).ToList();
        if (recipients.Count == 0) return;

        var notification = new NotificationDto
        {
            Id = Guid.NewGuid(),
            UserId = Guid.Empty, // Overridden per-user by SendToManyAsync
            SourceModuleId = ModuleId,
            Type = NotificationType.Update,
            Title = $"Sprint \"{sprintTitle}\" has started",
            Message = "A new sprint has been started on your board.",
            Priority = NotificationPriority.Normal,
            ActionUrl = $"/apps/tracks?board={boardId}",
            CreatedAtUtc = DateTime.UtcNow
        };

        try
        {
            await _notificationService.SendToManyAsync(recipients, notification, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send sprint started notifications for board {BoardId}", boardId);
        }
    }

    /// <inheritdoc />
    public async Task NotifySprintCompletedAsync(Guid boardId, string sprintTitle, Guid completedByUserId, IReadOnlyList<Guid> boardMemberIds, CancellationToken cancellationToken)
    {
        if (_notificationService is null) return;

        var recipients = boardMemberIds.Where(id => id != completedByUserId).ToList();
        if (recipients.Count == 0) return;

        var notification = new NotificationDto
        {
            Id = Guid.NewGuid(),
            UserId = Guid.Empty,
            SourceModuleId = ModuleId,
            Type = NotificationType.Update,
            Title = $"Sprint \"{sprintTitle}\" completed",
            Message = "A sprint on your board has been completed.",
            Priority = NotificationPriority.Normal,
            ActionUrl = $"/apps/tracks?board={boardId}",
            CreatedAtUtc = DateTime.UtcNow
        };

        try
        {
            await _notificationService.SendToManyAsync(recipients, notification, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send sprint completed notifications for board {BoardId}", boardId);
        }
    }

    /// <inheritdoc />
    public async Task NotifyTeamMemberAddedAsync(Guid teamId, string teamName, Guid addedUserId, Guid addedByUserId, CancellationToken cancellationToken)
    {
        if (_notificationService is null) return;
        if (addedUserId == addedByUserId) return;

        var notification = new NotificationDto
        {
            Id = Guid.NewGuid(),
            UserId = addedUserId,
            SourceModuleId = ModuleId,
            Type = NotificationType.Update,
            Title = $"You were added to team \"{teamName}\"",
            Message = "You have been added as a member of a Tracks team.",
            Priority = NotificationPriority.Normal,
            ActionUrl = "/apps/tracks?view=teams",
            CreatedAtUtc = DateTime.UtcNow
        };

        try
        {
            await _notificationService.SendAsync(addedUserId, notification, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send team member added notification to user {UserId}", addedUserId);
        }
    }

    /// <inheritdoc />
    public async Task NotifyTeamMemberRemovedAsync(Guid teamId, string teamName, Guid removedUserId, CancellationToken cancellationToken)
    {
        if (_notificationService is null) return;

        var notification = new NotificationDto
        {
            Id = Guid.NewGuid(),
            UserId = removedUserId,
            SourceModuleId = ModuleId,
            Type = NotificationType.Update,
            Title = $"You were removed from team \"{teamName}\"",
            Message = "You have been removed from a Tracks team.",
            Priority = NotificationPriority.Normal,
            ActionUrl = "/apps/tracks?view=teams",
            CreatedAtUtc = DateTime.UtcNow
        };

        try
        {
            await _notificationService.SendAsync(removedUserId, notification, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send team member removed notification to user {UserId}", removedUserId);
        }
    }

    /// <inheritdoc />
    public async Task NotifyCommentAddedAsync(Guid boardId, Guid cardId, string cardTitle, Guid commentAuthorId, IReadOnlyList<Guid> cardAssigneeIds, CancellationToken cancellationToken)
    {
        if (_notificationService is null) return;

        var recipients = cardAssigneeIds.Where(id => id != commentAuthorId).ToList();
        if (recipients.Count == 0) return;

        var notification = new NotificationDto
        {
            Id = Guid.NewGuid(),
            UserId = Guid.Empty,
            SourceModuleId = ModuleId,
            Type = NotificationType.Update,
            Title = $"New comment on \"{cardTitle}\"",
            Message = "A comment was added to a card you are assigned to.",
            Priority = NotificationPriority.Normal,
            ActionUrl = $"/apps/tracks?board={boardId}&card={cardId}",
            CreatedAtUtc = DateTime.UtcNow
        };

        try
        {
            await _notificationService.SendToManyAsync(recipients, notification, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send comment notification for card {CardId}", cardId);
        }
    }
}
