namespace DotNetCloud.Modules.Video.Models;

/// <summary>
/// Per-user video junction — lightweight record linking a user to a canonical video.
/// All intrinsic video properties live on <see cref="CanonicalVideo"/>.
/// </summary>
public sealed class UserVideo
{
    /// <summary>Unique identifier for this user video record.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>The user who owns this video.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>The FileNode ID this video references (from Files module).</summary>
    public Guid FileNodeId { get; set; }

    /// <summary>Content hash referencing the canonical video.</summary>
    public required string CanonicalContentHash { get; set; }

    /// <summary>Whether the video is marked as a favorite (user-specific).</summary>
    public bool IsFavorite { get; set; }

    /// <summary>View count (user-specific).</summary>
    public int ViewCount { get; set; }

    /// <summary>Watch position in ticks for resume playback. Null = never watched or reset.</summary>
    public long? WatchPositionTicks { get; set; }

    /// <summary>Whether the video has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Timestamp when soft-deleted.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>When the video record was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the video record was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation to the canonical video.</summary>
    public CanonicalVideo? CanonicalVideo { get; set; }
}
