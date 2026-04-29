using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Events;

/// <summary>
/// Handles Tracks module events by broadcasting real-time activity notifications
/// to Chat users. Enables live product/work-item activity visibility within the Chat UI.
/// </summary>
/// <remarks>
/// <para>
/// Subscribes to key Tracks events (work item lifecycle, sprint lifecycle, assignments)
/// and broadcasts notification signals to connected Chat clients via
/// <see cref="IRealtimeBroadcaster"/>. Clients receive lightweight signals and
/// can display activity toasts, update notification badges, or refresh feeds.
/// </para>
/// <para>
/// This handler does <b>not</b> persist messages — it broadcasts ephemeral
/// real-time signals. Persistent integration (e.g., posting system messages
/// into channels) would be handled by a separate service.
/// </para>
/// </remarks>
internal sealed class TracksActivityChatHandler :
    IEventHandler<WorkItemCreatedEvent>,
    IEventHandler<WorkItemMovedEvent>,
    IEventHandler<WorkItemUpdatedEvent>,
    IEventHandler<WorkItemDeletedEvent>,
    IEventHandler<WorkItemAssignedEvent>,
    IEventHandler<WorkItemCommentAddedEvent>,
    IEventHandler<SprintStartedEvent>,
    IEventHandler<SprintCompletedEvent>,
    IEventHandler<ProductCreatedEvent>,
    IEventHandler<ProductDeletedEvent>
{
    private readonly IRealtimeBroadcaster? _broadcaster;
    private readonly ILogger<TracksActivityChatHandler> _logger;

    private const string TracksActivityGroup = "tracks-activity";
    private const string TracksActivityEventName = "TracksActivityNotification";

    /// <summary>
    /// Initializes a new instance of the <see cref="TracksActivityChatHandler"/> class.
    /// </summary>
    public TracksActivityChatHandler(
        ILogger<TracksActivityChatHandler> logger,
        IRealtimeBroadcaster? broadcaster = null)
    {
        _logger = logger;
        _broadcaster = broadcaster;
    }

    /// <inheritdoc />
    public async Task HandleAsync(WorkItemCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Chat integration: WorkItem created {WorkItemId} type {Type} in product {ProductId}", @event.WorkItemId, @event.Type, @event.ProductId);
        await BroadcastTracksActivityAsync("workitem_created", @event.ProductId, new
        {
            @event.WorkItemId,
            @event.ProductId,
            @event.Type,
            @event.ParentWorkItemId
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(WorkItemMovedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Chat integration: WorkItem moved {WorkItemId} from {From} to {To}", @event.WorkItemId, @event.FromSwimlaneId, @event.ToSwimlaneId);
        await BroadcastTracksActivityAsync("workitem_moved", Guid.Empty, new
        {
            @event.WorkItemId,
            @event.FromSwimlaneId,
            @event.ToSwimlaneId
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(WorkItemUpdatedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Chat integration: WorkItem updated {WorkItemId} type {Type}", @event.WorkItemId, @event.Type);
        await BroadcastTracksActivityAsync("workitem_updated", Guid.Empty, new
        {
            @event.WorkItemId
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(WorkItemDeletedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Chat integration: WorkItem deleted {WorkItemId} type {Type}", @event.WorkItemId, @event.Type);
        await BroadcastTracksActivityAsync("workitem_deleted", Guid.Empty, new
        {
            @event.WorkItemId
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(WorkItemAssignedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Chat integration: WorkItem {WorkItemId} assigned to {UserId}", @event.WorkItemId, @event.UserId);

        await BroadcastTracksActivityAsync("workitem_assigned", Guid.Empty, new
        {
            @event.WorkItemId,
            @event.UserId
        }, cancellationToken);

        // Also send a direct notification to the assigned user
        if (_broadcaster is not null)
        {
            await _broadcaster.SendToUserAsync(@event.UserId, "TracksWorkItemAssignedToYou", new
            {
                @event.WorkItemId
            }, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task HandleAsync(WorkItemCommentAddedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Chat integration: Comment {CommentId} added to WorkItem {WorkItemId}", @event.CommentId, @event.WorkItemId);
        await BroadcastTracksActivityAsync("comment_added", Guid.Empty, new
        {
            @event.CommentId,
            @event.WorkItemId,
            @event.UserId
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(SprintStartedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Chat integration: Sprint {SprintId} started for epic {EpicId}", @event.SprintId, @event.EpicId);
        await BroadcastTracksActivityAsync("sprint_started", @event.EpicId, new
        {
            @event.SprintId,
            @event.EpicId
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(SprintCompletedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Chat integration: Sprint {SprintId} completed for epic {EpicId}", @event.SprintId, @event.EpicId);
        await BroadcastTracksActivityAsync("sprint_completed", @event.EpicId, new
        {
            @event.SprintId,
            @event.EpicId
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(ProductCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Chat integration: Product created {ProductId}", @event.ProductId);
        await BroadcastTracksActivityAsync("product_created", @event.ProductId, new
        {
            @event.ProductId,
            @event.OrganizationId,
            @event.OwnerId
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(ProductDeletedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Chat integration: Product deleted {ProductId}", @event.ProductId);
        await BroadcastTracksActivityAsync("product_deleted", @event.ProductId, new
        {
            @event.ProductId
        }, cancellationToken);
    }

    private async Task BroadcastTracksActivityAsync(string action, Guid productId, object payload, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;

        // Broadcast to the global tracks-activity group (Chat UI subscribes to this)
        await _broadcaster.BroadcastAsync(TracksActivityGroup, TracksActivityEventName, new
        {
            action,
            productId,
            payload,
            timestamp = DateTime.UtcNow
        }, cancellationToken);

        // Also broadcast to the product-specific group for product-scoped Chat views
        if (productId != Guid.Empty)
        {
            await _broadcaster.BroadcastAsync($"tracks-product-chat-{productId}", TracksActivityEventName, new
            {
                action,
                productId,
                payload,
                timestamp = DateTime.UtcNow
            }, cancellationToken);
        }
    }
}
