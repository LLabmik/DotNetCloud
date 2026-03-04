using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Chat.Events;

/// <summary>
/// Published when a message is sent in a channel.
/// </summary>
public sealed record MessageSentEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the sent message.</summary>
    public required Guid MessageId { get; init; }

    /// <summary>The channel the message was sent to.</summary>
    public required Guid ChannelId { get; init; }

    /// <summary>The user who sent the message.</summary>
    public required Guid SenderUserId { get; init; }

    /// <summary>The message content.</summary>
    public required string Content { get; init; }

    /// <summary>The message type.</summary>
    public required string MessageType { get; init; }
}
