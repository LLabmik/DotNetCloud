namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Status of a channel invitation.
/// </summary>
public enum ChannelInviteStatus
{
    /// <summary>Invitation is pending a response.</summary>
    Pending,

    /// <summary>Invitation was accepted; user joined the channel.</summary>
    Accepted,

    /// <summary>Invitation was declined by the invitee.</summary>
    Declined,

    /// <summary>Invitation was revoked by the inviter or an admin.</summary>
    Revoked
}
