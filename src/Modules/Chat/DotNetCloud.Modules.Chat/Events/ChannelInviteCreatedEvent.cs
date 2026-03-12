using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Chat.Events;

/// <summary>
/// Published when a channel invitation is created (sent to a single user).
/// </summary>
public sealed record ChannelInviteCreatedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The invite ID.</summary>
    public required Guid InviteId { get; init; }

    /// <summary>The channel the user is invited to.</summary>
    public required Guid ChannelId { get; init; }

    /// <summary>The user being invited.</summary>
    public required Guid InvitedUserId { get; init; }

    /// <summary>The user who sent the invitation.</summary>
    public required Guid InvitedByUserId { get; init; }
}
