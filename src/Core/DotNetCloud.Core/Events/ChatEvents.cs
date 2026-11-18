namespace DotNetCloud.Core.Events;

/// <summary>
/// Published when a message is sent in a channel.
/// </summary>
/// <remarks>
/// This contract lives in <c>DotNetCloud.Core</c> rather than in the Chat module because any module
/// (first-party or third-party) may subscribe to it, and a module must never reference another
/// module's assembly to obtain an event shape.
/// </remarks>
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

/// <summary>
/// Published when a new channel is created.
/// </summary>
public sealed record ChannelCreatedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the created channel.</summary>
    public required Guid ChannelId { get; init; }

    /// <summary>The channel name.</summary>
    public required string ChannelName { get; init; }

    /// <summary>The channel type.</summary>
    public required string ChannelType { get; init; }

    /// <summary>The user who created the channel.</summary>
    public required Guid CreatedByUserId { get; init; }
}

/// <summary>
/// Published when a channel is deleted.
/// </summary>
public sealed record ChannelDeletedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the deleted channel.</summary>
    public required Guid ChannelId { get; init; }

    /// <summary>The channel name.</summary>
    public required string ChannelName { get; init; }

    /// <summary>The user who deleted the channel.</summary>
    public required Guid DeletedByUserId { get; init; }
}
