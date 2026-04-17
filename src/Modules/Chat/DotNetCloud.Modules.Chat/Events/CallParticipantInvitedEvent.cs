using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Chat.Events;

/// <summary>
/// Published when a participant is invited to join an active video call mid-call.
/// </summary>
public sealed record CallParticipantInvitedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The video call ID.</summary>
    public required Guid CallId { get; init; }

    /// <summary>The channel the call belongs to.</summary>
    public required Guid ChannelId { get; init; }

    /// <summary>The user who was invited to the call.</summary>
    public required Guid InvitedUserId { get; init; }

    /// <summary>The user who sent the invitation (the call Host).</summary>
    public required Guid InvitedByUserId { get; init; }
}
