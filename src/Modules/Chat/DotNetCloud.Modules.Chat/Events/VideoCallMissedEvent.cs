using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Chat.Events;

/// <summary>
/// Published when a video call is missed (not answered within the ring timeout).
/// </summary>
public sealed record VideoCallMissedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The video call ID.</summary>
    public required Guid CallId { get; init; }

    /// <summary>The channel the call belonged to.</summary>
    public required Guid ChannelId { get; init; }

    /// <summary>The user who initiated the missed call.</summary>
    public required Guid InitiatorUserId { get; init; }
}
