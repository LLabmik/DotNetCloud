using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Chat.Events;

/// <summary>
/// Published when a channel is archived.
/// </summary>
public sealed record ChannelArchivedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the archived channel.</summary>
    public required Guid ChannelId { get; init; }

    /// <summary>The channel name.</summary>
    public required string ChannelName { get; init; }

    /// <summary>The user who archived the channel.</summary>
    public required Guid ArchivedByUserId { get; init; }
}
