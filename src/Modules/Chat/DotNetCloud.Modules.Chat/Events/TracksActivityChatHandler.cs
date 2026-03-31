using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Events;

/// <summary>
/// Handles Tracks module events by broadcasting real-time activity notifications
/// to Chat users. Enables live board activity visibility within the Chat UI.
/// </summary>
/// <remarks>
/// <para>
/// Subscribes to key Tracks events (card lifecycle, sprint lifecycle, assignments)
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
    IEventHandler<CardCreatedEvent>,
    IEventHandler<CardMovedEvent>,
    IEventHandler<CardUpdatedEvent>,
    IEventHandler<CardDeletedEvent>,
    IEventHandler<CardAssignedEvent>,
    IEventHandler<CardCommentAddedEvent>,
    IEventHandler<SprintStartedEvent>,
    IEventHandler<SprintCompletedEvent>,
    IEventHandler<BoardCreatedEvent>,
    IEventHandler<BoardDeletedEvent>
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
    public async Task HandleAsync(CardCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Chat integration: Card created {CardId} on board {BoardId}", @event.CardId, @event.BoardId);
        await BroadcastTracksActivityAsync("card_created", @event.BoardId, new
        {
            @event.CardId,
            @event.BoardId,
            @event.Title,
            @event.SwimlaneId,
            UserId = @event.CreatedByUserId
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(CardMovedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Chat integration: Card moved {CardId} from {From} to {To}", @event.CardId, @event.FromSwimlaneId, @event.ToSwimlaneId);
        await BroadcastTracksActivityAsync("card_moved", @event.BoardId, new
        {
            @event.CardId,
            @event.BoardId,
            @event.FromSwimlaneId,
            @event.ToSwimlaneId,
            UserId = @event.MovedByUserId
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(CardUpdatedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Chat integration: Card updated {CardId} on board {BoardId}", @event.CardId, @event.BoardId);
        await BroadcastTracksActivityAsync("card_updated", @event.BoardId, new
        {
            @event.CardId,
            @event.BoardId,
            UserId = @event.UpdatedByUserId
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(CardDeletedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Chat integration: Card deleted {CardId} on board {BoardId}", @event.CardId, @event.BoardId);
        await BroadcastTracksActivityAsync("card_deleted", @event.BoardId, new
        {
            @event.CardId,
            @event.BoardId,
            UserId = @event.DeletedByUserId
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(CardAssignedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Chat integration: Card {CardId} assigned to {UserId}", @event.CardId, @event.AssignedUserId);

        // Broadcast to board group
        await BroadcastTracksActivityAsync("card_assigned", @event.BoardId, new
        {
            @event.CardId,
            @event.BoardId,
            @event.AssignedUserId,
            @event.AssignedByUserId
        }, cancellationToken);

        // Also send a direct notification to the assigned user
        if (_broadcaster is not null)
        {
            await _broadcaster.SendToUserAsync(@event.AssignedUserId, "TracksCardAssignedToYou", new
            {
                @event.CardId,
                @event.BoardId,
                @event.AssignedByUserId
            }, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task HandleAsync(CardCommentAddedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Chat integration: Comment {CommentId} added to card {CardId}", @event.CommentId, @event.CardId);
        await BroadcastTracksActivityAsync("comment_added", @event.BoardId, new
        {
            @event.CommentId,
            @event.CardId,
            @event.BoardId,
            UserId = @event.UserId
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(SprintStartedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Chat integration: Sprint {SprintId} started on board {BoardId}", @event.SprintId, @event.BoardId);
        await BroadcastTracksActivityAsync("sprint_started", @event.BoardId, new
        {
            @event.SprintId,
            @event.BoardId,
            @event.Title,
            UserId = @event.StartedByUserId
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(SprintCompletedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Chat integration: Sprint {SprintId} completed on board {BoardId}", @event.SprintId, @event.BoardId);
        await BroadcastTracksActivityAsync("sprint_completed", @event.BoardId, new
        {
            @event.SprintId,
            @event.BoardId,
            @event.Title,
            @event.CompletedCardCount,
            @event.TotalCardCount,
            UserId = @event.CompletedByUserId
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(BoardCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Chat integration: Board created {BoardId} '{Title}'", @event.BoardId, @event.Title);
        await BroadcastTracksActivityAsync("board_created", @event.BoardId, new
        {
            @event.BoardId,
            @event.Title,
            UserId = @event.OwnerId
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(BoardDeletedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Chat integration: Board deleted {BoardId}", @event.BoardId);
        await BroadcastTracksActivityAsync("board_deleted", @event.BoardId, new
        {
            @event.BoardId,
            UserId = @event.DeletedByUserId
        }, cancellationToken);
    }

    private async Task BroadcastTracksActivityAsync(string action, Guid boardId, object payload, CancellationToken cancellationToken)
    {
        if (_broadcaster is null) return;

        // Broadcast to the global tracks-activity group (Chat UI subscribes to this)
        await _broadcaster.BroadcastAsync(TracksActivityGroup, TracksActivityEventName, new
        {
            action,
            boardId,
            payload,
            timestamp = DateTime.UtcNow
        }, cancellationToken);

        // Also broadcast to the board-specific group for board-scoped Chat views
        await _broadcaster.BroadcastAsync($"tracks-board-chat-{boardId}", TracksActivityEventName, new
        {
            action,
            boardId,
            payload,
            timestamp = DateTime.UtcNow
        }, cancellationToken);
    }
}
