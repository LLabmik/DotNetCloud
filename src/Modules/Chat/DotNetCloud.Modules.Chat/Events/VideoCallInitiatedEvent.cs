using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Chat.Events;

/// <summary>
/// Published when a video call is initiated in a channel.
/// </summary>
public sealed record VideoCallInitiatedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The video call ID.</summary>
    public required Guid CallId { get; init; }

    /// <summary>The channel the call was initiated in.</summary>
    public required Guid ChannelId { get; init; }

    /// <summary>The user who initiated the call.</summary>
    public required Guid InitiatorUserId { get; init; }

    /// <summary>The type of media for the call.</summary>
    public required string MediaType { get; init; }

    /// <summary>Whether this is a group call.</summary>
    public bool IsGroupCall { get; init; }
}
