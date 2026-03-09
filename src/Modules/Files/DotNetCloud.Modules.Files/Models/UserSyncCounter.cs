namespace DotNetCloud.Modules.Files.Models;

/// <summary>
/// Per-user monotonic sequence counter for cursor-based delta sync.
/// Every file mutation increments <see cref="CurrentSequence"/> so clients can
/// request changes since a given sequence number instead of a timestamp.
/// </summary>
public sealed class UserSyncCounter
{
    /// <summary>Owning user ID (primary key).</summary>
    public Guid UserId { get; set; }

    /// <summary>Current monotonically increasing sequence number.</summary>
    public long CurrentSequence { get; set; }

    /// <summary>When the counter was last incremented (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
