using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Events;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Events;

/// <summary>
/// Handles Chat module <see cref="MessageSentEvent"/> by broadcasting real-time
/// activity notifications to Tracks board views. Enables live chat activity
/// visibility within the Tracks UI.
/// </summary>
/// <remarks>
/// <para>
/// When a message is sent in Chat, this handler broadcasts a lightweight signal
/// to connected Tracks clients. Board views can display a "chat activity" indicator
/// showing that team discussion is happening. The handler uses the global
/// <c>chat-tracks-activity</c> broadcast group.
/// </para>
/// <para>
/// This handler does <b>not</b> create board activity records — it broadcasts
/// ephemeral real-time signals only. Persistent integration (e.g., linking chat
/// messages to cards) would be handled by a separate service.
/// </para>
/// </remarks>
internal sealed class ChatMessageTracksHandler :
    IEventHandler<MessageSentEvent>,
    IEventHandler<ChannelCreatedEvent>,
    IEventHandler<ChannelDeletedEvent>
{
    private readonly ITracksRealtimeService _realtimeService;
    private readonly ILogger<ChatMessageTracksHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatMessageTracksHandler"/> class.
    /// </summary>
    public ChatMessageTracksHandler(
        ITracksRealtimeService realtimeService,
        ILogger<ChatMessageTracksHandler> logger)
    {
        _realtimeService = realtimeService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(MessageSentEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Tracks integration: Message {MessageId} sent in channel {ChannelId} by user {UserId}",
            @event.MessageId, @event.ChannelId, @event.SenderUserId);

        // Broadcast chat activity to any Tracks board views listening for cross-module updates
        await _realtimeService.BroadcastActivityAsync(
            productId: Guid.Empty, // Global — not product-specific; clients filter by relevance
            userId: @event.SenderUserId,
            activityAction: "chat_message_sent",
            entityType: "ChatMessage",
            entityId: @event.MessageId,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(ChannelCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Tracks integration: Channel '{ChannelName}' created by user {UserId}",
            @event.ChannelName, @event.CreatedByUserId);

        await _realtimeService.BroadcastActivityAsync(
            productId: Guid.Empty,
            userId: @event.CreatedByUserId,
            activityAction: "chat_channel_created",
            entityType: "ChatChannel",
            entityId: @event.ChannelId,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(ChannelDeletedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Tracks integration: Channel '{ChannelName}' deleted by user {UserId}",
            @event.ChannelName, @event.DeletedByUserId);

        await _realtimeService.BroadcastActivityAsync(
            productId: Guid.Empty,
            userId: @event.DeletedByUserId,
            activityAction: "chat_channel_deleted",
            entityType: "ChatChannel",
            entityId: @event.ChannelId,
            cancellationToken);
    }
}
