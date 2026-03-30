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

    /// <summary>Adds a user to a board's broadcast group.</summary>
    Task AddUserToBoardGroupAsync(Guid userId, Guid boardId, CancellationToken cancellationToken = default);

    /// <summary>Removes a user from a board's broadcast group.</summary>
    Task RemoveUserFromBoardGroupAsync(Guid userId, Guid boardId, CancellationToken cancellationToken = default);
}
