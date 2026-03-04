using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Chat.Events;

/// <summary>
/// Published when a reaction is added to a message.
/// </summary>
public sealed record ReactionAddedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The message that was reacted to.</summary>
    public required Guid MessageId { get; init; }

    /// <summary>The channel containing the message.</summary>
    public required Guid ChannelId { get; init; }

    /// <summary>The user who added the reaction.</summary>
    public required Guid UserId { get; init; }

    /// <summary>The emoji that was added.</summary>
    public required string Emoji { get; init; }
}
