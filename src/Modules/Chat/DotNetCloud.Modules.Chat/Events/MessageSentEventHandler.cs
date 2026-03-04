using DotNetCloud.Core.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Events;

/// <summary>
/// Handles <see cref="MessageSentEvent"/> by logging the message.
/// Modules can subscribe additional handlers for notifications, indexing, etc.
/// </summary>
public sealed class MessageSentEventHandler : IEventHandler<MessageSentEvent>
{
    private readonly ILogger<MessageSentEventHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageSentEventHandler"/> class.
    /// </summary>
    public MessageSentEventHandler(ILogger<MessageSentEventHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task HandleAsync(MessageSentEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Message sent in channel {ChannelId} by user {UserId}: {MessageId}",
            @event.ChannelId,
            @event.SenderUserId,
            @event.MessageId);

        return Task.CompletedTask;
    }
}
