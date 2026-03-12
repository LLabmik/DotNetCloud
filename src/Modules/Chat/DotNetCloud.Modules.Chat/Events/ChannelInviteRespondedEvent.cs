using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Models;

namespace DotNetCloud.Modules.Chat.Events;

/// <summary>
/// Published when a channel invitation is accepted, declined, or revoked.
/// </summary>
public sealed record ChannelInviteRespondedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The invite ID.</summary>
    public required Guid InviteId { get; init; }

    /// <summary>The channel the invite belongs to.</summary>
    public required Guid ChannelId { get; init; }

    /// <summary>The user who was invited.</summary>
    public required Guid InvitedUserId { get; init; }

    /// <summary>The new status of the invite.</summary>
    public required ChannelInviteStatus NewStatus { get; init; }
}
