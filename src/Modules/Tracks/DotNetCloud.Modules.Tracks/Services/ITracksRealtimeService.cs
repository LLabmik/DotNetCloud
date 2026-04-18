namespace DotNetCloud.Modules.Tracks.Services;

/// <summary>
/// Broadcasts real-time board state updates to connected clients via <see cref="Core.Capabilities.IRealtimeBroadcaster"/>.
/// Uses lightweight action signals — clients receive the signal and refresh data from the API.
/// </summary>
public interface ITracksRealtimeService
{
    /// <summary>Broadcasts a card action (created, updated, moved, deleted, assigned, unassigned) to board members.</summary>
    Task BroadcastCardActionAsync(Guid boardId, Guid cardId, string action, Guid? fromSwimlaneId = null, Guid? toSwimlaneId = null, Guid? targetUserId = null, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a swimlane action (created, updated, deleted) to board members.</summary>
    Task BroadcastSwimlaneActionAsync(Guid boardId, Guid swimlaneId, string action, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a comment action (added, updated, deleted) to board members.</summary>
    Task BroadcastCommentActionAsync(Guid boardId, Guid cardId, Guid commentId, string action, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a sprint status change to board members.</summary>
    Task BroadcastSprintActionAsync(Guid boardId, Guid sprintId, string action, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a new activity entry to board members.</summary>
    Task BroadcastActivityAsync(Guid boardId, Guid userId, string activityAction, string entityType, Guid entityId, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a board member change (added, removed, role_updated) to board members.</summary>
    Task BroadcastBoardMemberActionAsync(Guid boardId, Guid userId, string action, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a team change (created, deleted, member_added, member_removed, board_transferred) to team members.</summary>
    Task BroadcastTeamActionAsync(Guid teamId, string action, Guid? targetUserId = null, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts that the host changed the current card in a review session.</summary>
    Task BroadcastReviewCardChangedAsync(Guid sessionId, Guid boardId, Guid cardId, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a review session state change (started, ended, paused).</summary>
    Task BroadcastReviewSessionStateAsync(Guid sessionId, Guid boardId, string action, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a poker vote status update during a review session (per-vote notification without revealing the value).</summary>
    Task BroadcastPokerVoteStatusAsync(Guid sessionId, Guid pokerId, Guid userId, bool hasVoted, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a poker session state change during a review (started, revealed, completed, cancelled).</summary>
    Task BroadcastReviewPokerStateAsync(Guid sessionId, Guid pokerId, Guid boardId, string action, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a participant joining or leaving a review session.</summary>
    Task BroadcastReviewParticipantChangedAsync(Guid sessionId, Guid userId, string action, CancellationToken cancellationToken = default);

    /// <summary>Adds a user to a board's broadcast group.</summary>
    Task AddUserToBoardGroupAsync(Guid userId, Guid boardId, CancellationToken cancellationToken = default);

    /// <summary>Removes a user from a board's broadcast group.</summary>
    Task RemoveUserFromBoardGroupAsync(Guid userId, Guid boardId, CancellationToken cancellationToken = default);

    /// <summary>Adds a user to a review session's broadcast group.</summary>
    Task AddUserToReviewGroupAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>Removes a user from a review session's broadcast group.</summary>
    Task RemoveUserFromReviewGroupAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
}
