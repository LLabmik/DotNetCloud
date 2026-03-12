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
/// Manages channel invitations for private channels.
/// Each invite targets a single user and must be sent by an admin or owner.
/// </summary>
internal sealed class ChannelInviteService : IChannelInviteService
{
    private readonly ChatDbContext _db;
    private readonly IChannelMemberService _memberService;
    private readonly IEventBus _eventBus;
    private readonly IChatRealtimeService? _realtimeService;
    private readonly IUserDirectory? _userDirectory;
    private readonly ILogger<ChannelInviteService> _logger;

    public ChannelInviteService(
        ChatDbContext db,
        IChannelMemberService memberService,
        IEventBus eventBus,
        ILogger<ChannelInviteService> logger,
        IChatRealtimeService? realtimeService = null,
        IUserDirectory? userDirectory = null)
    {
        _db = db;
        _memberService = memberService;
        _eventBus = eventBus;
        _realtimeService = realtimeService;
        _userDirectory = userDirectory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ChannelInviteDto> CreateInviteAsync(Guid channelId, CreateChannelInviteDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var channel = await _db.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == channelId, cancellationToken)
            ?? throw new InvalidOperationException("Channel not found.");

        if (channel.Type != ChannelType.Private)
            throw new InvalidOperationException("Invitations can only be sent for private channels.");

        await EnsureCallerIsAdminOrOwnerAsync(channelId, caller, cancellationToken);

        // Check the target user isn't already a member
        var alreadyMember = await _db.ChannelMembers
            .AsNoTracking()
            .AnyAsync(m => m.ChannelId == channelId && m.UserId == dto.UserId, cancellationToken);

        if (alreadyMember)
            throw new InvalidOperationException("User is already a member of this channel.");

        // Check for an existing pending invite
        var existingPending = await _db.ChannelInvites
            .AsNoTracking()
            .AnyAsync(i => i.ChannelId == channelId
                        && i.InvitedUserId == dto.UserId
                        && i.Status == ChannelInviteStatus.Pending, cancellationToken);

        if (existingPending)
            throw new InvalidOperationException("A pending invitation already exists for this user.");

        var invite = new ChannelInvite
        {
            ChannelId = channelId,
            InvitedUserId = dto.UserId,
            InvitedByUserId = caller.UserId,
            Message = dto.Message
        };

        _db.ChannelInvites.Add(invite);
        await _db.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new ChannelInviteCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            InviteId = invite.Id,
            ChannelId = channelId,
            InvitedUserId = dto.UserId,
            InvitedByUserId = caller.UserId
        }, caller, cancellationToken);

        // Send real-time notification to the invited user only
        if (_realtimeService is not null)
        {
            await _realtimeService.SendInviteNotificationAsync(
                dto.UserId,
                ToDto(invite, channel.Name),
                cancellationToken);
        }

        _logger.LogInformation(
            "Channel invite created. InviteId={InviteId} ChannelId={ChannelId} InvitedUser={InvitedUser} InvitedBy={InvitedBy}",
            invite.Id, channelId, dto.UserId, caller.UserId);

        return ToDto(invite, channel.Name);
    }

    /// <inheritdoc />
    public async Task<ChannelInviteDto> AcceptInviteAsync(Guid inviteId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var invite = await _db.ChannelInvites
            .Include(i => i.Channel)
            .FirstOrDefaultAsync(i => i.Id == inviteId, cancellationToken)
            ?? throw new InvalidOperationException("Invitation not found.");

        if (invite.InvitedUserId != caller.UserId)
            throw new UnauthorizedAccessException("Only the invited user can accept this invitation.");

        if (invite.Status != ChannelInviteStatus.Pending)
            throw new InvalidOperationException($"Invitation is no longer pending (status: {invite.Status}).");

        invite.Status = ChannelInviteStatus.Accepted;
        invite.RespondedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        // Add the user to the channel as a Member using a system caller
        // so that the membership check doesn't fail (the invitee isn't an admin)
        var systemCaller = new CallerContext(Guid.Empty, [], CallerType.System);
        await _memberService.AddMemberAsync(invite.ChannelId, caller.UserId, systemCaller, cancellationToken);

        await _eventBus.PublishAsync(new ChannelInviteRespondedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            InviteId = invite.Id,
            ChannelId = invite.ChannelId,
            InvitedUserId = caller.UserId,
            NewStatus = ChannelInviteStatus.Accepted
        }, caller, cancellationToken);

        _logger.LogInformation("Invite {InviteId} accepted by user {UserId}", inviteId, caller.UserId);

        return ToDto(invite, invite.Channel?.Name);
    }

    /// <inheritdoc />
    public async Task<ChannelInviteDto> DeclineInviteAsync(Guid inviteId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var invite = await _db.ChannelInvites
            .Include(i => i.Channel)
            .FirstOrDefaultAsync(i => i.Id == inviteId, cancellationToken)
            ?? throw new InvalidOperationException("Invitation not found.");

        if (invite.InvitedUserId != caller.UserId)
            throw new UnauthorizedAccessException("Only the invited user can decline this invitation.");

        if (invite.Status != ChannelInviteStatus.Pending)
            throw new InvalidOperationException($"Invitation is no longer pending (status: {invite.Status}).");

        invite.Status = ChannelInviteStatus.Declined;
        invite.RespondedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new ChannelInviteRespondedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            InviteId = invite.Id,
            ChannelId = invite.ChannelId,
            InvitedUserId = caller.UserId,
            NewStatus = ChannelInviteStatus.Declined
        }, caller, cancellationToken);

        _logger.LogInformation("Invite {InviteId} declined by user {UserId}", inviteId, caller.UserId);

        return ToDto(invite, invite.Channel?.Name);
    }

    /// <inheritdoc />
    public async Task RevokeInviteAsync(Guid inviteId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var invite = await _db.ChannelInvites
            .FirstOrDefaultAsync(i => i.Id == inviteId, cancellationToken)
            ?? throw new InvalidOperationException("Invitation not found.");

        if (invite.Status != ChannelInviteStatus.Pending)
            throw new InvalidOperationException($"Invitation is no longer pending (status: {invite.Status}).");

        // Only the inviter or a channel admin/owner can revoke
        if (invite.InvitedByUserId != caller.UserId)
        {
            await EnsureCallerIsAdminOrOwnerAsync(invite.ChannelId, caller, cancellationToken);
        }

        invite.Status = ChannelInviteStatus.Revoked;
        invite.RespondedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Invite {InviteId} revoked by user {UserId}", inviteId, caller.UserId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChannelInviteDto>> ListMyInvitesAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var invites = await _db.ChannelInvites
            .AsNoTracking()
            .Include(i => i.Channel)
            .Where(i => i.InvitedUserId == caller.UserId && i.Status == ChannelInviteStatus.Pending)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        return invites.Select(i => ToDto(i, i.Channel?.Name)).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChannelInviteDto>> ListChannelInvitesAsync(Guid channelId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await EnsureCallerIsAdminOrOwnerAsync(channelId, caller, cancellationToken);

        var invites = await _db.ChannelInvites
            .AsNoTracking()
            .Include(i => i.Channel)
            .Where(i => i.ChannelId == channelId && i.Status == ChannelInviteStatus.Pending)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        return invites.Select(i => ToDto(i, i.Channel?.Name)).ToList();
    }

    private async Task EnsureCallerIsAdminOrOwnerAsync(Guid channelId, CallerContext caller, CancellationToken cancellationToken)
    {
        if (caller.Type == CallerType.System)
            return;

        var membership = await _db.ChannelMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ChannelId == channelId && m.UserId == caller.UserId, cancellationToken);

        if (membership is null || (membership.Role != ChannelMemberRole.Owner && membership.Role != ChannelMemberRole.Admin))
        {
            throw new UnauthorizedAccessException($"User {caller.UserId} is not an owner or admin of channel {channelId}.");
        }
    }

    private static ChannelInviteDto ToDto(ChannelInvite invite, string? channelName) => new()
    {
        Id = invite.Id,
        ChannelId = invite.ChannelId,
        ChannelName = channelName ?? string.Empty,
        InvitedUserId = invite.InvitedUserId,
        InvitedByUserId = invite.InvitedByUserId,
        Status = invite.Status.ToString(),
        CreatedAt = invite.CreatedAt,
        RespondedAt = invite.RespondedAt,
        Message = invite.Message
    };
}
