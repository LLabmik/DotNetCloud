using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Chat.Events;

/// <summary>
/// Published when a message is deleted.
/// </summary>
public sealed record MessageDeletedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the deleted message.</summary>
    public required Guid MessageId { get; init; }

    /// <summary>The channel containing the message.</summary>
    public required Guid ChannelId { get; init; }

    /// <summary>The user who deleted the message.</summary>
    public required Guid DeletedByUserId { get; init; }
}
