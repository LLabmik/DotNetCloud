using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Events;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IUserDirectory = DotNetCloud.Core.Capabilities.IUserDirectory;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// Manages channel memberships, roles, notification preferences, and unread tracking.
/// </summary>
internal sealed class ChannelMemberService : IChannelMemberService
{
    private const string ChannelNotFoundError = "Channel not found.";

    private readonly ChatDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly IChatRealtimeService? _realtimeService;
    private readonly IUserDirectory? _userDirectory;
    private readonly ILogger<ChannelMemberService> _logger;

    public ChannelMemberService(
        ChatDbContext db,
        IEventBus eventBus,
        ILogger<ChannelMemberService> logger,
        IChatRealtimeService? realtimeService = null,
        IUserDirectory? userDirectory = null)
    {
        _db = db;
        _eventBus = eventBus;
        _realtimeService = realtimeService;
        _userDirectory = userDirectory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task AddMemberAsync(Guid channelId, Guid userId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var channel = await _db.Channels
            .FirstOrDefaultAsync(c => c.Id == channelId, cancellationToken)
            ?? throw new InvalidOperationException(ChannelNotFoundError);

        await EnsureCallerCanManageMembersAsync(channelId, caller, cancellationToken);

        var exists = await _db.ChannelMembers
            .AnyAsync(m => m.ChannelId == channelId && m.UserId == userId, cancellationToken);

        if (exists)
            return;

        _db.ChannelMembers.Add(new ChannelMember
        {
            ChannelId = channelId,
            UserId = userId,
            Role = ChannelMemberRole.Member
        });

        // DM → Group auto-conversion: when a 3rd member is added to a DirectMessage channel,
        // automatically escalate to a Group channel so all members see prior messages.
        if (channel.Type == ChannelType.DirectMessage)
        {
            var currentMemberCount = await _db.ChannelMembers
                .CountAsync(m => m.ChannelId == channelId, cancellationToken);

            // currentMemberCount doesn't include the new member yet (not saved),
            // so if there are already 2 members, adding a 3rd triggers conversion.
            if (currentMemberCount >= 2)
            {
                channel.Type = ChannelType.Group;
                _logger.LogInformation(
                    "Channel {ChannelId} auto-converted from DirectMessage to Group (3rd member {UserId} added by {AddedBy})",
                    channelId, userId, caller.UserId);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (_realtimeService is not null)
        {
            await _realtimeService.AddUserToChannelGroupAsync(userId, channelId, cancellationToken);
        }

        await _eventBus.PublishAsync(new UserJoinedChannelEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ChannelId = channelId,
            UserId = userId,
            AddedByUserId = caller.UserId
        }, caller, cancellationToken);

        _logger.LogInformation("User {UserId} added to channel {ChannelId} by {AddedBy}", userId, channelId, caller.UserId);
    }

    /// <inheritdoc />
    public async Task RemoveMemberAsync(Guid channelId, Guid userId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await EnsureChannelExistsAsync(channelId, cancellationToken);

        var membership = await _db.ChannelMembers
            .FirstOrDefaultAsync(m => m.ChannelId == channelId && m.UserId == userId, cancellationToken);

        if (membership is null)
            return;

        if (!IsSystemCaller(caller) && caller.UserId != userId)
        {
            await EnsureCallerCanManageMembersAsync(channelId, caller, cancellationToken);
        }

        if (membership.Role == ChannelMemberRole.Owner)
        {
            var ownerCount = await _db.ChannelMembers
                .CountAsync(m => m.ChannelId == channelId && m.Role == ChannelMemberRole.Owner, cancellationToken);

            if (ownerCount <= 1)
            {
                throw new InvalidOperationException("Cannot remove the last owner from the channel.");
            }
        }

        _db.ChannelMembers.Remove(membership);
        await _db.SaveChangesAsync(cancellationToken);

        if (_realtimeService is not null)
        {
            await _realtimeService.RemoveUserFromChannelGroupAsync(userId, channelId, cancellationToken);
        }

        await _eventBus.PublishAsync(new UserLeftChannelEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ChannelId = channelId,
            UserId = userId,
            RemovedByUserId = caller.UserId
        }, caller, cancellationToken);

        _logger.LogInformation("User {UserId} removed from channel {ChannelId} by {RemovedBy}", userId, channelId, caller.UserId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChannelMemberDto>> ListMembersAsync(Guid channelId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await EnsureChannelExistsAsync(channelId, cancellationToken);
        await EnsureCallerCanAccessChannelAsync(channelId, caller, cancellationToken);

        var members = await _db.ChannelMembers
            .AsNoTracking()
            .Where(m => m.ChannelId == channelId)
            .OrderBy(m => m.JoinedAt)
            .ToListAsync(cancellationToken);

        var userIds = members.Select(m => m.UserId).Distinct();
        var displayNames = _userDirectory is not null
            ? await _userDirectory.GetDisplayNamesAsync(userIds, cancellationToken)
            : new Dictionary<Guid, string>();

        return members.Select(m => new ChannelMemberDto
        {
            UserId = m.UserId,
            DisplayName = displayNames.GetValueOrDefault(m.UserId, m.UserId.ToString()[..8]),
            Role = m.Role.ToString(),
            JoinedAt = m.JoinedAt,
            IsMuted = m.IsMuted,
            NotificationPref = m.NotificationPref.ToString()
        }).ToList();
    }

    /// <inheritdoc />
    public async Task UpdateMemberRoleAsync(Guid channelId, Guid userId, ChannelMemberRole role, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await EnsureChannelExistsAsync(channelId, cancellationToken);
        await EnsureCallerCanManageMembersAsync(channelId, caller, cancellationToken);

        var membership = await _db.ChannelMembers
            .FirstOrDefaultAsync(m => m.ChannelId == channelId && m.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException($"User {userId} is not a member of channel {channelId}.");

        if (membership.Role == ChannelMemberRole.Owner && role != ChannelMemberRole.Owner)
        {
            var ownerCount = await _db.ChannelMembers
                .CountAsync(m => m.ChannelId == channelId && m.Role == ChannelMemberRole.Owner, cancellationToken);

            if (ownerCount <= 1)
            {
                throw new InvalidOperationException("Cannot demote the last owner of the channel.");
            }
        }

        membership.Role = role;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} role updated to {Role} in channel {ChannelId}", userId, role, channelId);
    }

    /// <inheritdoc />
    public async Task UpdateNotificationPreferenceAsync(Guid channelId, NotificationPreference pref, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await EnsureChannelExistsAsync(channelId, cancellationToken);

        var membership = await _db.ChannelMembers
            .FirstOrDefaultAsync(m => m.ChannelId == channelId && m.UserId == caller.UserId, cancellationToken)
            ?? throw new InvalidOperationException($"User {caller.UserId} is not a member of channel {channelId}.");

        membership.NotificationPref = pref;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task MarkAsReadAsync(Guid channelId, Guid messageId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await EnsureChannelExistsAsync(channelId, cancellationToken);

        var membership = await _db.ChannelMembers
            .FirstOrDefaultAsync(m => m.ChannelId == channelId && m.UserId == caller.UserId, cancellationToken)
            ?? throw new InvalidOperationException($"User {caller.UserId} is not a member of channel {channelId}.");

        var messageExists = await _db.Messages
            .AsNoTracking()
            .AnyAsync(m => m.Id == messageId && m.ChannelId == channelId, cancellationToken);

        if (!messageExists)
            throw new InvalidOperationException($"Message {messageId} not found in channel {channelId}.");

        membership.LastReadAt = DateTime.UtcNow;
        membership.LastReadMessageId = messageId;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UnreadCountDto>> GetUnreadCountsAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var memberships = await _db.ChannelMembers
            .AsNoTracking()
            .Where(m => m.UserId == caller.UserId)
            .ToListAsync(cancellationToken);

        var result = new List<UnreadCountDto>(memberships.Count);

        foreach (var membership in memberships)
        {
            var unreadQuery = _db.Messages
                .AsNoTracking()
                .Where(m => m.ChannelId == membership.ChannelId);

            if (membership.LastReadAt.HasValue)
            {
                unreadQuery = unreadQuery.Where(m => m.SentAt > membership.LastReadAt.Value);
            }

            var unreadCount = await unreadQuery.CountAsync(cancellationToken);

            var mentionCount = 0;
            var mentionQuery = _db.MessageMentions
                .AsNoTracking()
                .Join(_db.Messages, mm => mm.MessageId, m => m.Id, (mm, m) => new { mm, m })
                .Where(x => x.m.ChannelId == membership.ChannelId
                         && (x.mm.MentionedUserId == caller.UserId
                             || x.mm.Type == MentionType.All
                             || x.mm.Type == MentionType.Channel));

            if (membership.LastReadAt.HasValue)
            {
                mentionQuery = mentionQuery.Where(x => x.m.SentAt > membership.LastReadAt.Value);
            }

            mentionCount = await mentionQuery.CountAsync(cancellationToken);

            result.Add(new UnreadCountDto
            {
                ChannelId = membership.ChannelId,
                UnreadCount = unreadCount,
                MentionCount = mentionCount
            });
        }

        return result;
    }

    private async Task EnsureChannelExistsAsync(Guid channelId, CancellationToken cancellationToken)
    {
        var exists = await _db.Channels
            .AsNoTracking()
            .AnyAsync(c => c.Id == channelId, cancellationToken);

        if (!exists)
            throw new InvalidOperationException(ChannelNotFoundError);
    }

    /// <inheritdoc />
    public async Task<bool> IsMemberAsync(Guid channelId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        return await _db.ChannelMembers
            .AsNoTracking()
            .AnyAsync(m => m.ChannelId == channelId && m.UserId == caller.UserId, cancellationToken);
    }

    private async Task EnsureCallerCanAccessChannelAsync(Guid channelId, CallerContext caller, CancellationToken cancellationToken)
    {
        if (IsSystemCaller(caller))
            return;

        var isMember = await _db.ChannelMembers
            .AsNoTracking()
            .AnyAsync(m => m.ChannelId == channelId && m.UserId == caller.UserId, cancellationToken);

        if (!isMember)
        {
            _logger.LogWarning(
                "Denied channel access. ChannelId={ChannelId} CallerUserId={CallerUserId}",
                channelId,
                caller.UserId);
            throw new UnauthorizedAccessException($"User {caller.UserId} is not a member of channel {channelId}.");
        }
    }

    private async Task EnsureCallerCanManageMembersAsync(Guid channelId, CallerContext caller, CancellationToken cancellationToken)
    {
        if (IsSystemCaller(caller))
            return;

        var membership = await _db.ChannelMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ChannelId == channelId && m.UserId == caller.UserId, cancellationToken);

        if (membership is null || (membership.Role != ChannelMemberRole.Owner && membership.Role != ChannelMemberRole.Admin))
        {
            _logger.LogWarning(
                "Denied member-management action. ChannelId={ChannelId} CallerUserId={CallerUserId} CallerRole={CallerRole}",
                channelId,
                caller.UserId,
                membership?.Role);
            throw new UnauthorizedAccessException($"User {caller.UserId} is not an owner or admin of channel {channelId}.");
        }
    }

    private static bool IsSystemCaller(CallerContext caller)
        => caller.Type == CallerType.System;
}
