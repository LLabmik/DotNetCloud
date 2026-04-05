using DotNetCloud.Core.Capabilities;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Services;

/// <summary>
/// Implementation of <see cref="ITracksRealtimeService"/> that delegates to <see cref="IRealtimeBroadcaster"/>.
/// When no broadcaster is available (module running standalone), operations are no-ops.
/// Broadcasts lightweight action signals — clients receive the signal and refresh data from the API.
/// </summary>
internal sealed class TracksRealtimeService : ITracksRealtimeService
{
    private readonly IRealtimeBroadcaster? _broadcaster;
    private readonly TracksInProcessSignalRService _eventBridge;
    private readonly ILogger<TracksRealtimeService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TracksRealtimeService"/> class.
    /// </summary>
    public TracksRealtimeService(ILogger<TracksRealtimeService> logger, TracksInProcessSignalRService eventBridge, IRealtimeBroadcaster? broadcaster = null)
    {
        _broadcaster = broadcaster;
        _eventBridge = eventBridge;
        _logger = logger;
    }

    private static string BoardGroup(Guid boardId) => $"tracks-board-{boardId}";
    private static string TeamGroup(Guid teamId) => $"tracks-team-{teamId}";
    private static string ReviewGroup(Guid sessionId) => $"tracks-review-{sessionId}";

    /// <inheritdoc />
    public async Task BroadcastCardActionAsync(Guid boardId, Guid cardId, string action, Guid? fromSwimlaneId, Guid? toSwimlaneId, Guid? targetUserId, CancellationToken cancellationToken)
    {
        if (_broadcaster is not null)
            await _broadcaster.BroadcastAsync(BoardGroup(boardId), "TracksCardAction",
                new { boardId, cardId, action, fromSwimlaneId, toSwimlaneId, targetUserId }, cancellationToken);
        _eventBridge.OnCardAction(boardId, cardId, action);
    }

    /// <inheritdoc />
    public async Task BroadcastSwimlaneActionAsync(Guid boardId, Guid swimlaneId, string action, CancellationToken cancellationToken)
    {
        if (_broadcaster is not null)
            await _broadcaster.BroadcastAsync(BoardGroup(boardId), "TracksSwimlaneAction",
                new { boardId, swimlaneId, action }, cancellationToken);
        _eventBridge.OnSwimlaneAction(boardId, swimlaneId, action);
    }

    /// <inheritdoc />
    public async Task BroadcastCommentActionAsync(Guid boardId, Guid cardId, Guid commentId, string action, CancellationToken cancellationToken)
    {
        if (_broadcaster is not null)
            await _broadcaster.BroadcastAsync(BoardGroup(boardId), "TracksCommentAction",
                new { boardId, cardId, commentId, action }, cancellationToken);
        _eventBridge.OnCommentAction(boardId, cardId, commentId, action);
    }

    /// <inheritdoc />
    public async Task BroadcastSprintActionAsync(Guid boardId, Guid sprintId, string action, CancellationToken cancellationToken)
    {
        if (_broadcaster is not null)
            await _broadcaster.BroadcastAsync(BoardGroup(boardId), "TracksSprintAction",
                new { boardId, sprintId, action }, cancellationToken);
        _eventBridge.OnSprintAction(boardId, sprintId, action);
    }

    /// <inheritdoc />
    public async Task BroadcastActivityAsync(Guid boardId, Guid userId, string activityAction, string entityType, Guid entityId, CancellationToken cancellationToken)
    {
        if (_broadcaster is not null)
            await _broadcaster.BroadcastAsync(BoardGroup(boardId), "TracksActivity",
                new { boardId, userId, action = activityAction, entityType, entityId, timestamp = DateTime.UtcNow }, cancellationToken);
        _eventBridge.OnActivity(boardId);
    }

    /// <inheritdoc />
    public async Task BroadcastBoardMemberActionAsync(Guid boardId, Guid userId, string action, CancellationToken cancellationToken)
    {
        if (_broadcaster is not null)
            await _broadcaster.BroadcastAsync(BoardGroup(boardId), "TracksBoardMemberAction",
                new { boardId, userId, action }, cancellationToken);
        _eventBridge.OnBoardMemberAction(boardId, userId, action);
    }

    /// <inheritdoc />
    public async Task BroadcastTeamActionAsync(Guid teamId, string action, Guid? targetUserId, CancellationToken cancellationToken)
    {
        if (_broadcaster is not null)
            await _broadcaster.BroadcastAsync(TeamGroup(teamId), "TracksTeamAction",
                new { teamId, action, targetUserId }, cancellationToken);
        _eventBridge.OnTeamAction(teamId, action);
    }

    /// <inheritdoc />
    public async Task BroadcastReviewCardChangedAsync(Guid sessionId, Guid boardId, Guid cardId, CancellationToken cancellationToken)
    {
        if (_broadcaster is not null)
            await _broadcaster.BroadcastAsync(ReviewGroup(sessionId), "TracksReviewCardChanged",
                new { sessionId, boardId, cardId }, cancellationToken);
        _eventBridge.OnReviewCardChanged(sessionId, boardId, cardId);
    }

    /// <inheritdoc />
    public async Task BroadcastReviewSessionStateAsync(Guid sessionId, Guid boardId, string action, CancellationToken cancellationToken)
    {
        if (_broadcaster is not null)
        {
            // Broadcast to both the review group and the board group so non-participants know a session started/ended
            await _broadcaster.BroadcastAsync(ReviewGroup(sessionId), "TracksReviewSessionState",
                new { sessionId, boardId, action }, cancellationToken);
            await _broadcaster.BroadcastAsync(BoardGroup(boardId), "TracksReviewSessionState",
                new { sessionId, boardId, action }, cancellationToken);
        }
        _eventBridge.OnReviewSessionStateChanged(sessionId, boardId, action);
    }

    /// <inheritdoc />
    public async Task BroadcastPokerVoteStatusAsync(Guid sessionId, Guid pokerId, Guid userId, bool hasVoted, CancellationToken cancellationToken)
    {
        if (_broadcaster is not null)
            await _broadcaster.BroadcastAsync(ReviewGroup(sessionId), "TracksPokerVoteStatus",
                new { sessionId, pokerId, userId, hasVoted }, cancellationToken);
        _eventBridge.OnPokerVoteStatus(sessionId, pokerId, userId, hasVoted);
    }

    /// <inheritdoc />
    public async Task BroadcastReviewPokerStateAsync(Guid sessionId, Guid pokerId, Guid boardId, string action, CancellationToken cancellationToken)
    {
        if (_broadcaster is not null)
            await _broadcaster.BroadcastAsync(ReviewGroup(sessionId), "TracksReviewPokerState",
                new { sessionId, pokerId, boardId, action }, cancellationToken);
        _eventBridge.OnReviewPokerStateChanged(sessionId, pokerId, boardId, action);
    }

    /// <inheritdoc />
    public async Task BroadcastReviewParticipantChangedAsync(Guid sessionId, Guid userId, string action, CancellationToken cancellationToken)
    {
        if (_broadcaster is not null)
            await _broadcaster.BroadcastAsync(ReviewGroup(sessionId), "TracksReviewParticipantChanged",
                new { sessionId, userId, action }, cancellationToken);
        _eventBridge.OnReviewParticipantChanged(sessionId, userId, action);
    }

    /// <inheritdoc />
    public async Task AddUserToBoardGroupAsync(Guid userId, Guid boardId, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.AddToGroupAsync(userId, BoardGroup(boardId), cancellationToken);
        _logger.LogDebug("Added user {UserId} to board group {BoardId}", userId, boardId);
    }

    /// <inheritdoc />
    public async Task RemoveUserFromBoardGroupAsync(Guid userId, Guid boardId, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.RemoveFromGroupAsync(userId, BoardGroup(boardId), cancellationToken);
        _logger.LogDebug("Removed user {UserId} from board group {BoardId}", userId, boardId);
    }

    /// <inheritdoc />
    public async Task AddUserToReviewGroupAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.AddToGroupAsync(userId, ReviewGroup(sessionId), cancellationToken);
        _logger.LogDebug("Added user {UserId} to review group {SessionId}", userId, sessionId);
    }

    /// <inheritdoc />
    public async Task RemoveUserFromReviewGroupAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.RemoveFromGroupAsync(userId, ReviewGroup(sessionId), cancellationToken);
        _logger.LogDebug("Removed user {UserId} from review group {SessionId}", userId, sessionId);
    }
}
