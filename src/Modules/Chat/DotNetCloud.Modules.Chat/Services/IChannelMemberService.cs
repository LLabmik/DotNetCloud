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

    /// <summary>Sets the mute state for the caller's membership in a channel.</summary>
    Task SetMuteAsync(Guid channelId, bool muted, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Marks a channel as read up to a specific message.</summary>
    Task MarkAsReadAsync(Guid channelId, Guid messageId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets unread message counts for all channels the caller belongs to.</summary>
    Task<IReadOnlyList<UnreadCountDto>> GetUnreadCountsAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Sets whether the caller has accepted a direct message channel invitation.</summary>
    Task SetDmAcceptedAsync(Guid channelId, bool accepted, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the aggregate chat alert summary for the caller, in a number of queries that does not
    /// depend on how many channels the caller belongs to.
    /// </summary>
    /// <param name="caller">The calling user.</param>
    /// <param name="knownToken">
    /// The entity-tag the caller received from its previous call (typically echoed from
    /// <c>If-None-Match</c>), or <c>null</c>/empty for an unconditional read. When it matches the current
    /// state the aggregate is not recomputed and the result is flagged
    /// <see cref="ChatAlertsCheckResult.NotModified"/>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ChatAlertsCheckResult> GetAlertsAsync(CallerContext caller, string? knownToken = null, CancellationToken cancellationToken = default);
}
