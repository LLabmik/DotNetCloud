using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.DTOs;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Service for managing channel invitations. Invites are sent to a single user
/// by a channel admin or owner for private channels.
/// </summary>
public interface IChannelInviteService
{
    /// <summary>
    /// Creates an invitation for a single user to join a private channel.
    /// Only channel admins and owners can send invitations.
    /// </summary>
    Task<ChannelInviteDto> CreateInviteAsync(Guid channelId, CreateChannelInviteDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts a pending invitation. The invitee is added to the channel.
    /// Only the invited user can accept their own invitation.
    /// </summary>
    Task<ChannelInviteDto> AcceptInviteAsync(Guid inviteId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>
    /// Declines a pending invitation.
    /// Only the invited user can decline their own invitation.
    /// </summary>
    Task<ChannelInviteDto> DeclineInviteAsync(Guid inviteId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a pending invitation.
    /// Only the inviter or a channel admin/owner can revoke an invitation.
    /// </summary>
    Task RevokeInviteAsync(Guid inviteId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists pending invitations for the calling user.
    /// </summary>
    Task<IReadOnlyList<ChannelInviteDto>> ListMyInvitesAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists pending invitations for a specific channel.
    /// Only channel admins and owners can view these.
    /// </summary>
    Task<IReadOnlyList<ChannelInviteDto>> ListChannelInvitesAsync(Guid channelId, CallerContext caller, CancellationToken cancellationToken = default);
}
