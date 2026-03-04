using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Chat.Events;

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
