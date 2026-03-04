using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Chat.Events;

/// <summary>
/// Published when a message is edited.
/// </summary>
public sealed record MessageEditedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the edited message.</summary>
    public required Guid MessageId { get; init; }

    /// <summary>The channel containing the message.</summary>
    public required Guid ChannelId { get; init; }

    /// <summary>The user who edited the message.</summary>
    public required Guid EditedByUserId { get; init; }

    /// <summary>The new message content.</summary>
    public required string NewContent { get; init; }
}
