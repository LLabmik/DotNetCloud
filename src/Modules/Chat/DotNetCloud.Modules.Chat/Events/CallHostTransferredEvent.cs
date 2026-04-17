using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Chat.Events;

/// <summary>
/// Published when the host role of a video call is transferred to another participant.
/// </summary>
public sealed record CallHostTransferredEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The video call ID.</summary>
    public required Guid CallId { get; init; }

    /// <summary>The channel the call belongs to.</summary>
    public required Guid ChannelId { get; init; }

    /// <summary>The user who was previously the host.</summary>
    public required Guid PreviousHostUserId { get; init; }

    /// <summary>The user who is now the host.</summary>
    public required Guid NewHostUserId { get; init; }
}
