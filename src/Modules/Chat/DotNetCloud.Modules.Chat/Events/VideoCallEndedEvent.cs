using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Chat.Events;

/// <summary>
/// Published when a video call ends.
/// </summary>
public sealed record VideoCallEndedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The video call ID.</summary>
    public required Guid CallId { get; init; }

    /// <summary>The channel the call belonged to.</summary>
    public required Guid ChannelId { get; init; }

    /// <summary>The reason the call ended.</summary>
    public required string EndReason { get; init; }

    /// <summary>Duration of the call in seconds, null if never connected.</summary>
    public int? DurationSeconds { get; init; }
}
