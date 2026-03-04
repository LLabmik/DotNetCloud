using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Chat.Events;

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
