namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Represents an invitation for a user to join a private channel.
/// </summary>
public sealed class ChannelInvite
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Channel the user is invited to.</summary>
    public Guid ChannelId { get; set; }

    /// <summary>Navigation property to the channel.</summary>
    public Channel? Channel { get; set; }

    /// <summary>User being invited.</summary>
    public Guid InvitedUserId { get; set; }

    /// <summary>User who sent the invitation.</summary>
    public Guid InvitedByUserId { get; set; }

    /// <summary>Current status of the invitation.</summary>
    public ChannelInviteStatus Status { get; set; } = ChannelInviteStatus.Pending;

    /// <summary>When the invitation was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the invitation was responded to (UTC).</summary>
    public DateTime? RespondedAt { get; set; }

    /// <summary>Optional message from the inviter.</summary>
    public string? Message { get; set; }
}
