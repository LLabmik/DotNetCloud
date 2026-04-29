namespace DotNetCloud.Modules.Tracks.Services;

/// <summary>
/// Broadcasts real-time product state updates to connected clients via <see cref="Core.Capabilities.IRealtimeBroadcaster"/>.
/// Uses lightweight action signals — clients receive the signal and refresh data from the API.
/// </summary>
public interface ITracksRealtimeService
{
    /// <summary>Broadcasts a work item action (created, updated, moved, deleted, assigned, unassigned) to product members.</summary>
    Task BroadcastWorkItemActionAsync(Guid productId, Guid workItemId, string action, Guid? fromSwimlaneId = null, Guid? toSwimlaneId = null, Guid? targetUserId = null, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a swimlane action (created, updated, deleted) to product members.</summary>
    Task BroadcastSwimlaneActionAsync(Guid productId, Guid swimlaneId, string action, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a comment action (added, updated, deleted) to product members.</summary>
    Task BroadcastCommentActionAsync(Guid productId, Guid workItemId, Guid commentId, string action, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a sprint status change to epic members.</summary>
    Task BroadcastSprintActionAsync(Guid epicId, Guid sprintId, string action, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a new activity entry to product members.</summary>
    Task BroadcastActivityAsync(Guid productId, Guid userId, string activityAction, string entityType, Guid entityId, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a product member change (added, removed, role_updated) to product members.</summary>
    Task BroadcastProductMemberActionAsync(Guid productId, Guid userId, string action, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a team change (created, deleted, member_added, member_removed) to team members.</summary>
    Task BroadcastTeamActionAsync(Guid teamId, string action, Guid? targetUserId = null, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts that the host changed the current item in a review session.</summary>
    Task BroadcastReviewItemChangedAsync(Guid sessionId, Guid epicId, Guid itemId, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a review session state change (started, ended, paused).</summary>
    Task BroadcastReviewSessionStateAsync(Guid sessionId, Guid epicId, string action, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a poker vote status update during a review session (per-vote notification without revealing the value).</summary>
    Task BroadcastPokerVoteStatusAsync(Guid sessionId, Guid pokerId, Guid userId, bool hasVoted, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a poker session state change during a review (started, revealed, completed, cancelled).</summary>
    Task BroadcastReviewPokerStateAsync(Guid sessionId, Guid pokerId, Guid epicId, string action, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a participant joining or leaving a review session.</summary>
    Task BroadcastReviewParticipantChangedAsync(Guid sessionId, Guid userId, string action, CancellationToken cancellationToken = default);

    /// <summary>Adds a user to a product's broadcast group.</summary>
    Task AddUserToProductGroupAsync(Guid userId, Guid productId, CancellationToken cancellationToken = default);

    /// <summary>Removes a user from a product's broadcast group.</summary>
    Task RemoveUserFromProductGroupAsync(Guid userId, Guid productId, CancellationToken cancellationToken = default);

    /// <summary>Adds a user to a review session's broadcast group.</summary>
    Task AddUserToReviewGroupAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>Removes a user from a review session's broadcast group.</summary>
    Task RemoveUserFromReviewGroupAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
}
