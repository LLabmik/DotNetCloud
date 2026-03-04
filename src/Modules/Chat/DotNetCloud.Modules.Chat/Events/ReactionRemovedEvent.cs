using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Chat.Events;

/// <summary>
/// Published when a reaction is removed from a message.
/// </summary>
public sealed record ReactionRemovedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The message the reaction was removed from.</summary>
    public required Guid MessageId { get; init; }

    /// <summary>The channel containing the message.</summary>
    public required Guid ChannelId { get; init; }

    /// <summary>The user who removed the reaction.</summary>
    public required Guid UserId { get; init; }

    /// <summary>The emoji that was removed.</summary>
    public required string Emoji { get; init; }
}
