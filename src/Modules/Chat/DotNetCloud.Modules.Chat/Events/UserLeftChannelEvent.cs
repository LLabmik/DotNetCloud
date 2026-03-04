using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Chat.Events;

/// <summary>
/// Published when a user leaves a channel.
/// </summary>
public sealed record UserLeftChannelEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The channel the user left.</summary>
    public required Guid ChannelId { get; init; }

    /// <summary>The user who left.</summary>
    public required Guid UserId { get; init; }

    /// <summary>The user who removed the member (may differ if kicked).</summary>
    public required Guid RemovedByUserId { get; init; }
}
