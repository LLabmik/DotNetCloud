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
    private readonly ILogger<TracksRealtimeService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TracksRealtimeService"/> class.
    /// </summary>
    public TracksRealtimeService(ILogger<TracksRealtimeService> logger, IRealtimeBroadcaster? broadcaster = null)
    {
        _broadcaster = broadcaster;
        _logger = logger;
    }

    private static string BoardGroup(Guid boardId) => $"tracks-board-{boardId}";
    private static string TeamGroup(Guid teamId) => $"tracks-team-{teamId}";

    /// <inheritdoc />
    public async Task BroadcastCardActionAsync(Guid boardId, Guid cardId, string action, Guid? fromListId, Guid? toListId, Guid? targetUserId, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.BroadcastAsync(BoardGroup(boardId), "TracksCardAction",
            new { boardId, cardId, action, fromListId, toListId, targetUserId }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task BroadcastListActionAsync(Guid boardId, Guid listId, string action, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.BroadcastAsync(BoardGroup(boardId), "TracksListAction",
            new { boardId, listId, action }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task BroadcastCommentActionAsync(Guid boardId, Guid cardId, Guid commentId, string action, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.BroadcastAsync(BoardGroup(boardId), "TracksCommentAction",
            new { boardId, cardId, commentId, action }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task BroadcastSprintActionAsync(Guid boardId, Guid sprintId, string action, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.BroadcastAsync(BoardGroup(boardId), "TracksSprintAction",
            new { boardId, sprintId, action }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task BroadcastActivityAsync(Guid boardId, Guid userId, string activityAction, string entityType, Guid entityId, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.BroadcastAsync(BoardGroup(boardId), "TracksActivity",
            new { boardId, userId, action = activityAction, entityType, entityId, timestamp = DateTime.UtcNow }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task BroadcastBoardMemberActionAsync(Guid boardId, Guid userId, string action, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.BroadcastAsync(BoardGroup(boardId), "TracksBoardMemberAction",
            new { boardId, userId, action }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task BroadcastTeamActionAsync(Guid teamId, string action, Guid? targetUserId, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;
        await _broadcaster.BroadcastAsync(TeamGroup(teamId), "TracksTeamAction",
            new { teamId, action, targetUserId }, cancellationToken);
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
}
