namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Represents a user blocking another user from calling them.
/// Blocked calls are silently rejected — the caller sees "User unavailable"
/// rather than being informed of the block.
/// </summary>
public sealed class BlockedUser
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user who created the block.</summary>
    public Guid UserId { get; set; }

    /// <summary>The user who is blocked.</summary>
    public Guid BlockedUserId { get; set; }

    /// <summary>When the block was created (UTC).</summary>
    public DateTime BlockedAtUtc { get; set; } = DateTime.UtcNow;
}
