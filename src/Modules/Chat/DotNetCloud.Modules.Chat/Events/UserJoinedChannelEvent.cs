using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Chat.Events;

/// <summary>
/// Published when a user joins a channel.
/// </summary>
public sealed record UserJoinedChannelEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The channel the user joined.</summary>
    public required Guid ChannelId { get; init; }

    /// <summary>The user who joined.</summary>
    public required Guid UserId { get; init; }

    /// <summary>The user who added the member (may differ from the joining user).</summary>
    public required Guid AddedByUserId { get; init; }
}
