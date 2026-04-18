using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using IUserDirectory = DotNetCloud.Core.Capabilities.IUserDirectory;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// Manages per-user call blocking. Blocked users' calls are silently rejected.
/// </summary>
internal sealed class UserBlockService : IUserBlockService
{
    private readonly ChatDbContext _db;
    private readonly IUserDirectory? _userDirectory;
    private readonly IChatRealtimeService? _realtimeService;
    private readonly IChatMessageNotifier? _notifier;
    private readonly ILogger<UserBlockService> _logger;

    public UserBlockService(
        ChatDbContext db,
        ILogger<UserBlockService> logger,
        IUserDirectory? userDirectory = null,
        IChatRealtimeService? realtimeService = null,
        IChatMessageNotifier? notifier = null)
    {
        _db = db;
        _userDirectory = userDirectory;
        _realtimeService = realtimeService;
        _notifier = notifier;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task BlockUserAsync(Guid targetUserId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        if (targetUserId == caller.UserId)
            throw new InvalidOperationException("Cannot block yourself.");

        var exists = await _db.BlockedUsers
            .AnyAsync(b => b.UserId == caller.UserId && b.BlockedUserId == targetUserId, cancellationToken);

        if (exists)
            return; // Already blocked — idempotent

        _db.BlockedUsers.Add(new BlockedUser
        {
            UserId = caller.UserId,
            BlockedUserId = targetUserId
        });

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} blocked user {BlockedUserId} from calls", caller.UserId, targetUserId);

        // Notify the blocked user in real-time
        await NotifyBlockStatusAsync(targetUserId, caller.UserId, isBlocked: true, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UnblockUserAsync(Guid targetUserId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var block = await _db.BlockedUsers
            .FirstOrDefaultAsync(b => b.UserId == caller.UserId && b.BlockedUserId == targetUserId, cancellationToken);

        if (block is null)
            return; // Not blocked — idempotent

        _db.BlockedUsers.Remove(block);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} unblocked user {BlockedUserId}", caller.UserId, targetUserId);

        // Notify the unblocked user in real-time
        await NotifyBlockStatusAsync(targetUserId, caller.UserId, isBlocked: false, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> IsBlockedAsync(Guid callerId, Guid targetUserId, CancellationToken cancellationToken = default)
    {
        return await _db.BlockedUsers
            .AsNoTracking()
            .AnyAsync(b => b.UserId == targetUserId && b.BlockedUserId == callerId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BlockedUserDto>> GetBlockedUsersAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var blocks = await _db.BlockedUsers
            .AsNoTracking()
            .Where(b => b.UserId == caller.UserId)
            .OrderByDescending(b => b.BlockedAtUtc)
            .ToListAsync(cancellationToken);

        var blockedUserIds = blocks.Select(b => b.BlockedUserId).Distinct();
        var displayNames = _userDirectory is not null
            ? await _userDirectory.GetDisplayNamesAsync(blockedUserIds, cancellationToken)
            : new Dictionary<Guid, string>();

        return blocks.Select(b => new BlockedUserDto
        {
            BlockedUserId = b.BlockedUserId,
            DisplayName = displayNames.GetValueOrDefault(b.BlockedUserId, b.BlockedUserId.ToString()[..8]),
            BlockedAtUtc = b.BlockedAtUtc
        }).ToList();
    }

    private async Task NotifyBlockStatusAsync(Guid targetUserId, Guid blockerUserId, bool isBlocked, CancellationToken cancellationToken)
    {
        try
        {
            if (_realtimeService is not null)
            {
                await _realtimeService.SendBlockStatusChangedAsync(targetUserId, blockerUserId, isBlocked, cancellationToken);
            }

            _notifier?.NotifyUserBlockStatusChanged(new UserBlockStatusChangedNotification(blockerUserId, targetUserId, isBlocked));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send block status notification to user {TargetUserId}", targetUserId);
        }
    }
}
