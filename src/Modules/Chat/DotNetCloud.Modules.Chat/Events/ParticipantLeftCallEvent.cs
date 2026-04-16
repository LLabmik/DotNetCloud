using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Chat.Events;

/// <summary>
/// Published when a participant leaves a video call.
/// </summary>
public sealed record ParticipantLeftCallEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The video call ID.</summary>
    public required Guid CallId { get; init; }

    /// <summary>The channel the call belongs to.</summary>
    public required Guid ChannelId { get; init; }

    /// <summary>The user who left the call.</summary>
    public required Guid UserId { get; init; }
}
