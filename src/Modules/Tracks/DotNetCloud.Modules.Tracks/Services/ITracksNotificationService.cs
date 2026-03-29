namespace DotNetCloud.Modules.Tracks.Services;

/// <summary>
/// Sends user-facing notifications for Tracks module events such as card assignments,
/// @mentions in comments, sprint lifecycle changes, and team membership updates.
/// </summary>
public interface ITracksNotificationService
{
    /// <summary>Notifies a user they have been assigned to a card.</summary>
    Task NotifyCardAssignedAsync(Guid boardId, Guid cardId, string cardTitle, Guid assignedUserId, Guid assignedByUserId, CancellationToken cancellationToken = default);

    /// <summary>Notifies users @mentioned in a comment.</summary>
    Task NotifyMentionsAsync(Guid boardId, Guid cardId, string cardTitle, Guid commentAuthorId, string commentContent, CancellationToken cancellationToken = default);

    /// <summary>Notifies board members that a sprint has started.</summary>
    Task NotifySprintStartedAsync(Guid boardId, string sprintTitle, Guid startedByUserId, IReadOnlyList<Guid> boardMemberIds, CancellationToken cancellationToken = default);

    /// <summary>Notifies board members that a sprint has been completed.</summary>
    Task NotifySprintCompletedAsync(Guid boardId, string sprintTitle, Guid completedByUserId, IReadOnlyList<Guid> boardMemberIds, CancellationToken cancellationToken = default);

    /// <summary>Notifies a user they have been added to a team.</summary>
    Task NotifyTeamMemberAddedAsync(Guid teamId, string teamName, Guid addedUserId, Guid addedByUserId, CancellationToken cancellationToken = default);

    /// <summary>Notifies a user they have been removed from a team.</summary>
    Task NotifyTeamMemberRemovedAsync(Guid teamId, string teamName, Guid removedUserId, CancellationToken cancellationToken = default);

    /// <summary>Notifies card assignees that a comment was added.</summary>
    Task NotifyCommentAddedAsync(Guid boardId, Guid cardId, string cardTitle, Guid commentAuthorId, IReadOnlyList<Guid> cardAssigneeIds, CancellationToken cancellationToken = default);
}
