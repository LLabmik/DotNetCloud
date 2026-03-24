using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Events;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using ValidationException = DotNetCloud.Core.Errors.ValidationException;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// Manages chat channel lifecycle: creation, updates, archiving, deletion, and DM resolution.
/// </summary>
internal sealed class ChannelService : IChannelService
{
    private const string DefaultPublicChannelName = "Public";

    private readonly ChatDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly IChatRealtimeService? _realtimeService;
    private readonly ILogger<ChannelService> _logger;

    public ChannelService(
        ChatDbContext db,
        IEventBus eventBus,
        ILogger<ChannelService> logger,
        IChatRealtimeService? realtimeService = null)
    {
        _db = db;
        _eventBus = eventBus;
        _realtimeService = realtimeService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ChannelDto> CreateChannelAsync(CreateChannelDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(caller);

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Channel name is required.", nameof(dto));

        if (!Enum.TryParse<ChannelType>(dto.Type, ignoreCase: true, out var channelType))
            throw new ArgumentException($"Invalid channel type: {dto.Type}", nameof(dto));

        if (channelType != ChannelType.DirectMessage)
            await ValidateChannelNameUniqueAsync(dto.Name, dto.OrganizationId, excludeChannelId: null, cancellationToken);

        var channel = new Channel
        {
            Name = dto.Name,
            Description = dto.Description,
            Type = channelType,
            Topic = dto.Topic,
            OrganizationId = dto.OrganizationId,
            CreatedByUserId = caller.UserId
        };

        _db.Channels.Add(channel);

        // Creator is always the owner
        _db.ChannelMembers.Add(new ChannelMember
        {
            ChannelId = channel.Id,
            UserId = caller.UserId,
            Role = ChannelMemberRole.Owner
        });

        // Add initial members
        foreach (var memberId in dto.MemberIds)
        {
            if (memberId != caller.UserId)
            {
                _db.ChannelMembers.Add(new ChannelMember
                {
                    ChannelId = channel.Id,
                    UserId = memberId,
                    Role = ChannelMemberRole.Member
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (_realtimeService is not null)
        {
            var memberIds = dto.MemberIds.Append(caller.UserId).Distinct();
            foreach (var memberId in memberIds)
            {
                await _realtimeService.AddUserToChannelGroupAsync(memberId, channel.Id, cancellationToken);
            }
        }

        await _eventBus.PublishAsync(new ChannelCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ChannelId = channel.Id,
            ChannelName = channel.Name,
            ChannelType = channel.Type.ToString(),
            CreatedByUserId = caller.UserId
        }, caller, cancellationToken);

        _logger.LogInformation("Channel {ChannelId} '{Name}' created by {UserId}", channel.Id, channel.Name, caller.UserId);

        var memberCount = 1 + dto.MemberIds.Count(m => m != caller.UserId);
        return ToChannelDto(channel, memberCount);
    }

    /// <inheritdoc />
    public async Task<ChannelDto?> GetChannelAsync(Guid channelId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var channel = await _db.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == channelId, cancellationToken);

        if (channel is null)
            return null;

        // Non-public channels require membership to view
        if (channel.Type != ChannelType.Public && caller.Type != CallerType.System)
        {
            var isMember = await _db.ChannelMembers
                .AnyAsync(m => m.ChannelId == channelId && m.UserId == caller.UserId, cancellationToken);

            if (!isMember)
                return null;
        }

        var memberCount = await _db.ChannelMembers.CountAsync(m => m.ChannelId == channelId, cancellationToken);
        return ToChannelDto(channel, memberCount);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChannelDto>> ListChannelsAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultPublicChannelForUserAsync(caller, cancellationToken);

        var channelIds = await _db.ChannelMembers
            .AsNoTracking()
            .Where(m => m.UserId == caller.UserId)
            .Select(m => m.ChannelId)
            .ToListAsync(cancellationToken);

        var memberCounts = await _db.ChannelMembers
            .AsNoTracking()
            .Where(m => channelIds.Contains(m.ChannelId))
            .GroupBy(m => m.ChannelId)
            .Select(g => new { ChannelId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ChannelId, x => x.Count, cancellationToken);

        var channels = await _db.Channels
            .AsNoTracking()
            .Where(c => channelIds.Contains(c.Id))
            .OrderByDescending(c => c.LastActivityAt ?? c.CreatedAt)
            .ToListAsync(cancellationToken);

        return channels
            .Select(c => ToChannelDto(c, memberCounts.GetValueOrDefault(c.Id, 0)))
            .ToList();
    }

    private async Task EnsureDefaultPublicChannelForUserAsync(CallerContext caller, CancellationToken cancellationToken)
    {
        // One global "Public" channel gives every user a guaranteed common room.
        var defaultChannel = await _db.Channels
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                c => c.OrganizationId == null
                  && c.Type == ChannelType.Public
                  && c.Name == DefaultPublicChannelName,
                cancellationToken);

        var hasChanges = false;

        if (defaultChannel is null)
        {
            defaultChannel = new Channel
            {
                Name = DefaultPublicChannelName,
                Description = "Default public channel for all users.",
                Type = ChannelType.Public,
                CreatedByUserId = caller.UserId
            };

            _db.Channels.Add(defaultChannel);
            hasChanges = true;
        }
        else if (defaultChannel.IsDeleted)
        {
            defaultChannel.IsDeleted = false;
            defaultChannel.DeletedAt = null;
            defaultChannel.IsArchived = false;
            hasChanges = true;
        }

        var membershipExists = await _db.ChannelMembers
            .AnyAsync(m => m.ChannelId == defaultChannel.Id && m.UserId == caller.UserId, cancellationToken);

        if (!membershipExists)
        {
            _db.ChannelMembers.Add(new ChannelMember
            {
                ChannelId = defaultChannel.Id,
                UserId = caller.UserId,
                Role = ChannelMemberRole.Member
            });
            hasChanges = true;
        }

        if (!hasChanges)
        {
            return;
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (_realtimeService is not null)
        {
            await _realtimeService.AddUserToChannelGroupAsync(caller.UserId, defaultChannel.Id, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<ChannelDto> UpdateChannelAsync(Guid channelId, UpdateChannelDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var channel = await _db.Channels.FindAsync([channelId], cancellationToken)
            ?? throw new InvalidOperationException($"Channel {channelId} not found.");

        await EnsureCallerIsAdminOrOwnerAsync(channelId, caller, cancellationToken);

        if (dto.Name is not null && dto.Name != channel.Name && channel.Type != ChannelType.DirectMessage)
            await ValidateChannelNameUniqueAsync(dto.Name, channel.OrganizationId, excludeChannelId: channelId, cancellationToken);

        if (dto.Name is not null)
            channel.Name = dto.Name;
        if (dto.Description is not null)
            channel.Description = dto.Description;
        if (dto.Topic is not null)
            channel.Topic = dto.Topic;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Channel {ChannelId} updated by {UserId}", channelId, caller.UserId);

        var memberCount = await _db.ChannelMembers.CountAsync(m => m.ChannelId == channelId, cancellationToken);
        return ToChannelDto(channel, memberCount);
    }

    /// <inheritdoc />
    public async Task DeleteChannelAsync(Guid channelId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var channel = await _db.Channels.FindAsync([channelId], cancellationToken)
            ?? throw new InvalidOperationException($"Channel {channelId} not found.");

        var memberIds = await _db.ChannelMembers
            .AsNoTracking()
            .Where(m => m.ChannelId == channelId)
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        await EnsureCallerIsAdminOrOwnerAsync(channelId, caller, cancellationToken);

        channel.IsDeleted = true;
        channel.DeletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        if (_realtimeService is not null)
        {
            foreach (var memberId in memberIds)
            {
                await _realtimeService.RemoveUserFromChannelGroupAsync(memberId, channelId, cancellationToken);
            }
        }

        await _eventBus.PublishAsync(new ChannelDeletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ChannelId = channel.Id,
            ChannelName = channel.Name,
            DeletedByUserId = caller.UserId
        }, caller, cancellationToken);

        _logger.LogInformation("Channel {ChannelId} deleted by {UserId}", channelId, caller.UserId);
    }

    /// <inheritdoc />
    public async Task ArchiveChannelAsync(Guid channelId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var channel = await _db.Channels.FindAsync([channelId], cancellationToken)
            ?? throw new InvalidOperationException($"Channel {channelId} not found.");

        await EnsureCallerIsAdminOrOwnerAsync(channelId, caller, cancellationToken);

        channel.IsArchived = true;

        await _db.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new ChannelArchivedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ChannelId = channel.Id,
            ChannelName = channel.Name,
            ArchivedByUserId = caller.UserId
        }, caller, cancellationToken);

        _logger.LogInformation("Channel {ChannelId} archived by {UserId}", channelId, caller.UserId);
    }

    /// <inheritdoc />
    public async Task<ChannelDto> GetOrCreateDirectMessageAsync(Guid otherUserId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Find existing DM between the two users
        var existingChannel = await _db.Channels
            .AsNoTracking()
            .Where(c => c.Type == ChannelType.DirectMessage)
            .Where(c => _db.ChannelMembers.Any(m => m.ChannelId == c.Id && m.UserId == caller.UserId)
                     && _db.ChannelMembers.Any(m => m.ChannelId == c.Id && m.UserId == otherUserId))
            .FirstOrDefaultAsync(cancellationToken);

        if (existingChannel is not null)
        {
            return ToChannelDto(existingChannel, 2);
        }

        // Create new DM channel
        var channel = new Channel
        {
            Name = $"DM-{caller.UserId}-{otherUserId}",
            Type = ChannelType.DirectMessage,
            CreatedByUserId = caller.UserId
        };

        _db.Channels.Add(channel);
        _db.ChannelMembers.Add(new ChannelMember { ChannelId = channel.Id, UserId = caller.UserId, Role = ChannelMemberRole.Member });
        _db.ChannelMembers.Add(new ChannelMember { ChannelId = channel.Id, UserId = otherUserId, Role = ChannelMemberRole.Member });

        await _db.SaveChangesAsync(cancellationToken);

        if (_realtimeService is not null)
        {
            await _realtimeService.AddUserToChannelGroupAsync(caller.UserId, channel.Id, cancellationToken);
            await _realtimeService.AddUserToChannelGroupAsync(otherUserId, channel.Id, cancellationToken);
        }

        _logger.LogInformation("DM channel {ChannelId} created between {User1} and {User2}", channel.Id, caller.UserId, otherUserId);

        return ToChannelDto(channel, 2);
    }

    private async Task ValidateChannelNameUniqueAsync(string name, Guid? organizationId, Guid? excludeChannelId, CancellationToken cancellationToken)
    {
        var query = _db.Channels
            .Where(c => c.Name == name
                     && c.OrganizationId == organizationId
                     && c.Type != ChannelType.DirectMessage);

        if (excludeChannelId.HasValue)
            query = query.Where(c => c.Id != excludeChannelId.Value);

        if (await query.AnyAsync(cancellationToken))
            throw new ValidationException("Name", $"A channel named '{name}' already exists in this organization.");
    }

    private async Task EnsureCallerIsAdminOrOwnerAsync(Guid channelId, CallerContext caller, CancellationToken cancellationToken)
    {
        if (caller.Type == CallerType.System)
            return;

        var membership = await _db.ChannelMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ChannelId == channelId && m.UserId == caller.UserId, cancellationToken);

        if (membership is null || (membership.Role != ChannelMemberRole.Owner && membership.Role != ChannelMemberRole.Admin))
            throw new UnauthorizedAccessException($"User {caller.UserId} is not an owner or admin of channel {channelId}.");
    }

    private static ChannelDto ToChannelDto(Channel channel, int memberCount)
    {
        return new ChannelDto
        {
            Id = channel.Id,
            Name = channel.Name,
            Description = channel.Description,
            Type = channel.Type.ToString(),
            Topic = channel.Topic,
            AvatarUrl = channel.AvatarUrl,
            IsArchived = channel.IsArchived,
            MemberCount = memberCount,
            LastActivityAt = channel.LastActivityAt,
            CreatedAt = channel.CreatedAt,
            CreatedByUserId = channel.CreatedByUserId
        };
    }
}
