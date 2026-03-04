using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Events;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// Manages channel memberships, roles, notification preferences, and unread tracking.
/// </summary>
internal sealed class ChannelMemberService : IChannelMemberService
{
    private readonly ChatDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ChannelMemberService> _logger;

    public ChannelMemberService(ChatDbContext db, IEventBus eventBus, ILogger<ChannelMemberService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task AddMemberAsync(Guid channelId, Guid userId, CallerContext caller, CancellationToken cancellationToken = default)
    {
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

        await _db.SaveChangesAsync(cancellationToken);

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
        var membership = await _db.ChannelMembers
            .FirstOrDefaultAsync(m => m.ChannelId == channelId && m.UserId == userId, cancellationToken);

        if (membership is null)
            return;

        _db.ChannelMembers.Remove(membership);
        await _db.SaveChangesAsync(cancellationToken);

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
        var members = await _db.ChannelMembers
            .AsNoTracking()
            .Where(m => m.ChannelId == channelId)
            .OrderBy(m => m.JoinedAt)
            .ToListAsync(cancellationToken);

        return members.Select(m => new ChannelMemberDto
        {
            UserId = m.UserId,
            Role = m.Role.ToString(),
            JoinedAt = m.JoinedAt,
            IsMuted = m.IsMuted,
            NotificationPref = m.NotificationPref.ToString()
        }).ToList();
    }

    /// <inheritdoc />
    public async Task UpdateMemberRoleAsync(Guid channelId, Guid userId, ChannelMemberRole role, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var membership = await _db.ChannelMembers
            .FirstOrDefaultAsync(m => m.ChannelId == channelId && m.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException($"User {userId} is not a member of channel {channelId}.");

        membership.Role = role;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} role updated to {Role} in channel {ChannelId}", userId, role, channelId);
    }

    /// <inheritdoc />
    public async Task UpdateNotificationPreferenceAsync(Guid channelId, NotificationPreference pref, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var membership = await _db.ChannelMembers
            .FirstOrDefaultAsync(m => m.ChannelId == channelId && m.UserId == caller.UserId, cancellationToken)
            ?? throw new InvalidOperationException($"User {caller.UserId} is not a member of channel {channelId}.");

        membership.NotificationPref = pref;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task MarkAsReadAsync(Guid channelId, Guid messageId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var membership = await _db.ChannelMembers
            .FirstOrDefaultAsync(m => m.ChannelId == channelId && m.UserId == caller.UserId, cancellationToken)
            ?? throw new InvalidOperationException($"User {caller.UserId} is not a member of channel {channelId}.");

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
            if (membership.LastReadAt.HasValue)
            {
                mentionCount = await _db.MessageMentions
                    .AsNoTracking()
                    .Join(_db.Messages, mm => mm.MessageId, m => m.Id, (mm, m) => new { mm, m })
                    .Where(x => x.m.ChannelId == membership.ChannelId
                             && x.m.SentAt > membership.LastReadAt!.Value
                             && (x.mm.MentionedUserId == caller.UserId || x.mm.Type == MentionType.All))
                    .CountAsync(cancellationToken);
            }

            result.Add(new UnreadCountDto
            {
                ChannelId = membership.ChannelId,
                UnreadCount = unreadCount,
                MentionCount = mentionCount
            });
        }

        return result;
    }
}
