using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Chat.Events;

/// <summary>
/// Published when a participant joins an active video call.
/// </summary>
public sealed record ParticipantJoinedCallEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The video call ID.</summary>
    public required Guid CallId { get; init; }

    /// <summary>The channel the call belongs to.</summary>
    public required Guid ChannelId { get; init; }

    /// <summary>The user who joined the call.</summary>
    public required Guid UserId { get; init; }
}
