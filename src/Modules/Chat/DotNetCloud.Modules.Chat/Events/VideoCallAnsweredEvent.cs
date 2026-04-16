using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Chat.Events;

/// <summary>
/// Published when a video call is answered by a participant.
/// </summary>
public sealed record VideoCallAnsweredEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The video call ID.</summary>
    public required Guid CallId { get; init; }

    /// <summary>The channel the call belongs to.</summary>
    public required Guid ChannelId { get; init; }

    /// <summary>The user who answered the call.</summary>
    public required Guid AnsweredByUserId { get; init; }
}
