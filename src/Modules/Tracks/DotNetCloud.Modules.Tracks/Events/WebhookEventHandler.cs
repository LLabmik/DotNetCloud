using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Events;

/// <summary>
/// Listens for Tracks domain events and dispatches them to matching webhook subscriptions
/// via <see cref="IWebhookDispatchService"/>.
/// </summary>
internal sealed class WebhookEventHandler :
    IEventHandler<WorkItemCreatedEvent>,
    IEventHandler<WorkItemUpdatedEvent>,
    IEventHandler<WorkItemDeletedEvent>,
    IEventHandler<WorkItemMovedEvent>,
    IEventHandler<WorkItemCommentAddedEvent>,
    IEventHandler<SprintStartedEvent>,
    IEventHandler<SprintCompletedEvent>,
    IEventHandler<MilestoneReachedEvent>
{
    private readonly IWebhookDispatchService _dispatchService;
    private readonly ILogger<WebhookEventHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookEventHandler"/> class.
    /// </summary>
    public WebhookEventHandler(
        IWebhookDispatchService dispatchService,
        ILogger<WebhookEventHandler> logger)
    {
        _dispatchService = dispatchService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(WorkItemCreatedEvent @event, CancellationToken ct)
        => await _dispatchService.DispatchAsync("work_item.created", @event, ct);

    /// <inheritdoc />
    public async Task HandleAsync(WorkItemUpdatedEvent @event, CancellationToken ct)
        => await _dispatchService.DispatchAsync("work_item.updated", @event, ct);

    /// <inheritdoc />
    public async Task HandleAsync(WorkItemDeletedEvent @event, CancellationToken ct)
        => await _dispatchService.DispatchAsync("work_item.deleted", @event, ct);

    /// <inheritdoc />
    public async Task HandleAsync(WorkItemMovedEvent @event, CancellationToken ct)
        => await _dispatchService.DispatchAsync("work_item.moved", @event, ct);

    /// <inheritdoc />
    public async Task HandleAsync(WorkItemCommentAddedEvent @event, CancellationToken ct)
        => await _dispatchService.DispatchAsync("comment.added", @event, ct);

    /// <inheritdoc />
    public async Task HandleAsync(SprintStartedEvent @event, CancellationToken ct)
        => await _dispatchService.DispatchAsync("sprint.started", @event, ct);

    /// <inheritdoc />
    public async Task HandleAsync(SprintCompletedEvent @event, CancellationToken ct)
        => await _dispatchService.DispatchAsync("sprint.completed", @event, ct);

    /// <inheritdoc />
    public async Task HandleAsync(MilestoneReachedEvent @event, CancellationToken ct)
        => await _dispatchService.DispatchAsync("milestone.reached", @event, ct);
}
