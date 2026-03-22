using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Models;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Service for managing channel memberships, roles, notifications, and unread counts.
/// </summary>
public interface IChannelMemberService
{
    /// <summary>Adds a user to a channel.</summary>
    Task AddMemberAsync(Guid channelId, Guid userId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Removes a user from a channel.</summary>
    Task RemoveMemberAsync(Guid channelId, Guid userId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Checks whether the caller is a member of the specified channel.</summary>
    Task<bool> IsMemberAsync(Guid channelId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists members of a channel.</summary>
    Task<IReadOnlyList<ChannelMemberDto>> ListMembersAsync(Guid channelId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Updates a member's role in a channel.</summary>
    Task UpdateMemberRoleAsync(Guid channelId, Guid userId, ChannelMemberRole role, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Updates the caller's notification preference for a channel.</summary>
    Task UpdateNotificationPreferenceAsync(Guid channelId, NotificationPreference pref, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Marks a channel as read up to a specific message.</summary>
    Task MarkAsReadAsync(Guid channelId, Guid messageId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets unread message counts for all channels the caller belongs to.</summary>
    Task<IReadOnlyList<UnreadCountDto>> GetUnreadCountsAsync(CallerContext caller, CancellationToken cancellationToken = default);
}
