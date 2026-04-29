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

    private static string ProductGroup(Guid productId) => $"tracks-product-{productId}";
    private static string TeamGroup(Guid teamId) => $"tracks-team-{teamId}";
    private static string ReviewGroup(Guid sessionId) => $"tracks-review-{sessionId}";

    /// <inheritdoc />
    public async Task BroadcastWorkItemActionAsync(Guid productId, Guid workItemId, string action, Guid? fromSwimlaneId, Guid? toSwimlaneId, Guid? targetUserId, CancellationToken cancellationToken)
    {
        if (_broadcaster is not null)
            await _broadcaster.BroadcastAsync(ProductGroup(productId), "TracksWorkItemAction",
                new { productId, workItemId, action, fromSwimlaneId, toSwimlaneId, targetUserId }, cancellationToken);
        _eventBridge.OnWorkItemAction(productId, workItemId, action);
    }

    /// <inheritdoc />
    public async Task BroadcastSwimlaneActionAsync(Guid productId, Guid swimlaneId, string action, CancellationToken cancellationToken)
    {
        if (_broadcaster is not null)
            await _broadcaster.BroadcastAsync(ProductGroup(productId), "TracksSwimlaneAction",
                new { productId, swimlaneId, action }, cancellationToken);
        _eventBridge.OnSwimlaneAction(productId, swimlaneId, action);
    }

    /// <inheritdoc />
    public async Task BroadcastCommentActionAsync(Guid productId, Guid workItemId, Guid commentId, string action, CancellationToken cancellationToken)
    {
        if (_broadcaster is not null)
            await _broadcaster.BroadcastAsync(ProductGroup(productId), "TracksCommentAction",
                new { productId, workItemId, commentId, action }, cancellationToken);
        _eventBridge.OnCommentAction(productId, workItemId, commentId, action);
    }

    /// <inheritdoc />
    public async Task BroadcastSprintActionAsync(Guid epicId, Guid sprintId, string action, CancellationToken cancellationToken)
    {
        if (_broadcaster is not null)
            await _broadcaster.BroadcastAsync(ProductGroup(epicId), "TracksSprintAction",
                new { epicId, sprintId, action }, cancellationToken);
        _eventBridge.OnSprintAction(epicId, sprintId, action);
    }

    /// <inheritdoc />
    public async Task BroadcastActivityAsync(Guid productId, Guid userId, string activityAction, string entityType, Guid entityId, CancellationToken cancellationToken)
    {
        if (_broadcaster is not null)
            await _broadcaster.BroadcastAsync(ProductGroup(productId), "TracksActivity",
                new { productId, userId, action = activityAction, entityType, entityId, timestamp = DateTime.UtcNow }, cancellationToken);
        _eventBridge.OnActivity(productId);
    }

    /// <inheritdoc />
    public async Task BroadcastProductMemberActionAsync(Guid productId, Guid userId, string action, CancellationToken cancellationToken)
    {
        if (_broadcaster is not null)
            await _broadcaster.BroadcastAsync(ProductGroup(productId), "TracksProductMemberAction",
                new { productId, userId, action }, cancellationToken);
        _eventBridge.OnProductMemberAction(productId, userId, action);
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
    public async Task BroadcastReviewItemChangedAsync(Guid sessionId, Guid epicId, Guid itemId, CancellationToken cancellationToken)
    {
        if (_broadcaster is not null)
            await _broadcaster.BroadcastAsync(ReviewGroup(sessionId), "TracksReviewItemChanged",
                new { sessionId, epicId, itemId }, cancellationToken);
        _eventBridge.OnReviewItemChanged(sessionId, epicId, itemId);
    }

    /// <inheritdoc />
    public async Task BroadcastReviewSessionStateAsync(Guid sessionId, Guid epicId, string action, CancellationToken cancellationToken)
    {
        if (_broadcaster is not null)
        {
            // Broadcast to both the review group and the product group so non-participants know a session started/ended
            await _broadcaster.BroadcastAsync(ReviewGroup(sessionId), "TracksReviewSessionState",
                new { sessionId, epicId, action }, cancellationToken);
            await _broadcaster.BroadcastAsync(ProductGroup(epicId), "TracksReviewSessionState",
                new { sessionId, epicId, action }, cancellationToken);
        }
        _eventBridge.OnReviewSessionStateChanged(sessionId, epicId, action);
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
    public async Task BroadcastReviewPokerStateAsync(Guid sessionId, Guid pokerId, Guid epicId, string action, CancellationToken cancellationToken)
    {
        if (_broadcaster is not null)
            await _broadcaster.BroadcastAsync(ReviewGroup(sessionId), "TracksReviewPokerState",
                new { sessionId, pokerId, epicId, action }, cancellationToken);
        _eventBridge.OnReviewPokerStateChanged(sessionId, pokerId, epicId, action);
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
    public async Task AddUserToProductGroupAsync(Guid userId, Guid productId, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.AddToGroupAsync(userId, ProductGroup(productId), cancellationToken);
        _logger.LogDebug("Added user {UserId} to product group {ProductId}", userId, productId);
    }

    /// <inheritdoc />
    public async Task RemoveUserFromProductGroupAsync(Guid userId, Guid productId, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.RemoveFromGroupAsync(userId, ProductGroup(productId), cancellationToken);
        _logger.LogDebug("Removed user {UserId} from product group {ProductId}", userId, productId);
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
