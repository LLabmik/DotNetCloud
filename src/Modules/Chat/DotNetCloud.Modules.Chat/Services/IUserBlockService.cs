using DotNetCloud.Core.Authorization;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// DTO for a blocked user entry.
/// </summary>
public sealed record BlockedUserDto
{
    /// <summary>The blocked user's ID.</summary>
    public Guid BlockedUserId { get; init; }

    /// <summary>The blocked user's display name.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>When the block was created.</summary>
    public DateTime BlockedAtUtc { get; init; }
}

/// <summary>
/// Service for managing per-user call blocking.
/// Blocked users' calls are silently rejected (caller sees "User unavailable").
/// </summary>
public interface IUserBlockService
{
    /// <summary>Blocks a user from calling the caller.</summary>
    Task BlockUserAsync(Guid targetUserId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Unblocks a previously blocked user.</summary>
    Task UnblockUserAsync(Guid targetUserId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Checks whether <paramref name="targetUserId"/> has blocked <paramref name="callerId"/>.</summary>
    Task<bool> IsBlockedAsync(Guid callerId, Guid targetUserId, CancellationToken cancellationToken = default);

    /// <summary>Gets all users blocked by the caller.</summary>
    Task<IReadOnlyList<BlockedUserDto>> GetBlockedUsersAsync(CallerContext caller, CancellationToken cancellationToken = default);
}
