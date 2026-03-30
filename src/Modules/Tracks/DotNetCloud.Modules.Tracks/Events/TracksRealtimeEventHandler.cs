using DotNetCloud.Core.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Events;

/// <summary>
/// Handles Tracks board and card events by broadcasting real-time action signals to connected clients
/// via <see cref="Services.ITracksRealtimeService"/>.
/// </summary>
internal sealed class TracksRealtimeEventHandler :
    IEventHandler<CardCreatedEvent>,
    IEventHandler<CardUpdatedEvent>,
    IEventHandler<CardMovedEvent>,
    IEventHandler<CardDeletedEvent>,
    IEventHandler<CardAssignedEvent>,
    IEventHandler<CardCommentAddedEvent>,
    IEventHandler<BoardCreatedEvent>,
    IEventHandler<BoardDeletedEvent>,
    IEventHandler<SprintStartedEvent>,
    IEventHandler<SprintCompletedEvent>,
    IEventHandler<TeamCreatedEvent>,
    IEventHandler<TeamDeletedEvent>
{
    private readonly Services.ITracksRealtimeService _realtimeService;
    private readonly ILogger<TracksRealtimeEventHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TracksRealtimeEventHandler"/> class.
    /// </summary>
    public TracksRealtimeEventHandler(Services.ITracksRealtimeService realtimeService, ILogger<TracksRealtimeEventHandler> logger)
    {
        _realtimeService = realtimeService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(CardCreatedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting card created: {CardId} on board {BoardId}", @event.CardId, @event.BoardId);
        await _realtimeService.BroadcastCardActionAsync(@event.BoardId, @event.CardId, "created",
            toSwimlaneId: @event.SwimlaneId, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(CardUpdatedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting card updated: {CardId} on board {BoardId}", @event.CardId, @event.BoardId);
        await _realtimeService.BroadcastCardActionAsync(@event.BoardId, @event.CardId, "updated",
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(CardMovedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting card moved: {CardId} from {FromSwimlane} to {ToSwimlane}", @event.CardId, @event.FromSwimlaneId, @event.ToSwimlaneId);
        await _realtimeService.BroadcastCardActionAsync(@event.BoardId, @event.CardId, "moved",
            fromSwimlaneId: @event.FromSwimlaneId, toSwimlaneId: @event.ToSwimlaneId, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(CardDeletedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting card deleted: {CardId} on board {BoardId}", @event.CardId, @event.BoardId);
        await _realtimeService.BroadcastCardActionAsync(@event.BoardId, @event.CardId, "deleted",
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(CardAssignedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting card assigned: {CardId} to user {UserId}", @event.CardId, @event.AssignedUserId);
        await _realtimeService.BroadcastCardActionAsync(@event.BoardId, @event.CardId, "assigned",
            targetUserId: @event.AssignedUserId, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(CardCommentAddedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting comment added: {CommentId} on card {CardId}", @event.CommentId, @event.CardId);
        await _realtimeService.BroadcastCommentActionAsync(@event.BoardId, @event.CardId, @event.CommentId, "added",
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(BoardCreatedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Board created: {BoardId} '{Title}'", @event.BoardId, @event.Title);
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task HandleAsync(BoardDeletedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Board deleted: {BoardId}", @event.BoardId);
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task HandleAsync(SprintStartedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting sprint started: {SprintId} on board {BoardId}", @event.SprintId, @event.BoardId);
        await _realtimeService.BroadcastSprintActionAsync(@event.BoardId, @event.SprintId, "started", cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(SprintCompletedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting sprint completed: {SprintId} on board {BoardId}", @event.SprintId, @event.BoardId);
        await _realtimeService.BroadcastSprintActionAsync(@event.BoardId, @event.SprintId, "completed", cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(TeamCreatedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting team created: {TeamId}", @event.TeamId);
        await _realtimeService.BroadcastTeamActionAsync(@event.TeamId, "created", cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(TeamDeletedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting team deleted: {TeamId}", @event.TeamId);
        await _realtimeService.BroadcastTeamActionAsync(@event.TeamId, "deleted", cancellationToken: cancellationToken);
    }
}
