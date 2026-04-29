using DotNetCloud.Core.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Events;

/// <summary>
/// Handles Tracks product and work item events by broadcasting real-time action signals to connected clients
/// via <see cref="Services.ITracksRealtimeService"/>.
/// </summary>
internal sealed class TracksRealtimeEventHandler :
    IEventHandler<WorkItemCreatedEvent>,
    IEventHandler<WorkItemUpdatedEvent>,
    IEventHandler<WorkItemMovedEvent>,
    IEventHandler<WorkItemDeletedEvent>,
    IEventHandler<WorkItemAssignedEvent>,
    IEventHandler<WorkItemCommentAddedEvent>,
    IEventHandler<ProductCreatedEvent>,
    IEventHandler<ProductDeletedEvent>,
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
    public async Task HandleAsync(WorkItemCreatedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting work item created: {WorkItemId} in product {ProductId}", @event.WorkItemId, @event.ProductId);
        await _realtimeService.BroadcastWorkItemActionAsync(@event.ProductId, @event.WorkItemId, "created",
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(WorkItemUpdatedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting work item updated: {WorkItemId} type {Type}", @event.WorkItemId, @event.Type);
        await _realtimeService.BroadcastWorkItemActionAsync(Guid.Empty, @event.WorkItemId, "updated",
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(WorkItemMovedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting work item moved: {WorkItemId} from {FromSwimlane} to {ToSwimlane}", @event.WorkItemId, @event.FromSwimlaneId, @event.ToSwimlaneId);
        await _realtimeService.BroadcastWorkItemActionAsync(Guid.Empty, @event.WorkItemId, "moved",
            fromSwimlaneId: @event.FromSwimlaneId, toSwimlaneId: @event.ToSwimlaneId, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(WorkItemDeletedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting work item deleted: {WorkItemId} type {Type}", @event.WorkItemId, @event.Type);
        await _realtimeService.BroadcastWorkItemActionAsync(Guid.Empty, @event.WorkItemId, "deleted",
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(WorkItemAssignedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting work item assigned: {WorkItemId} to user {UserId}", @event.WorkItemId, @event.UserId);
        await _realtimeService.BroadcastWorkItemActionAsync(Guid.Empty, @event.WorkItemId, "assigned",
            targetUserId: @event.UserId, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(WorkItemCommentAddedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting comment added: {CommentId} on work item {WorkItemId}", @event.CommentId, @event.WorkItemId);
        await _realtimeService.BroadcastCommentActionAsync(Guid.Empty, @event.WorkItemId, @event.CommentId, "added",
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(ProductCreatedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Product created: {ProductId}", @event.ProductId);
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task HandleAsync(ProductDeletedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Product deleted: {ProductId}", @event.ProductId);
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task HandleAsync(SprintStartedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting sprint started: {SprintId} for epic {EpicId}", @event.SprintId, @event.EpicId);
        await _realtimeService.BroadcastSprintActionAsync(@event.EpicId, @event.SprintId, "started", cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(SprintCompletedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting sprint completed: {SprintId} for epic {EpicId}", @event.SprintId, @event.EpicId);
        await _realtimeService.BroadcastSprintActionAsync(@event.EpicId, @event.SprintId, "completed", cancellationToken);
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
