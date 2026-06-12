namespace DotNetCloud.Modules.Music.Models;

/// <summary>
/// Per-user track junction — lightweight record linking a user to a canonical track.
/// Stores only user-specific data (play count, soft-delete status).
/// All intrinsic track properties live on <see cref="CanonicalTrack"/>.
/// </summary>
public sealed class UserTrack
{
    /// <summary>Unique identifier for this user track record.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>The user who owns this track record.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>The FileNode ID this track references (from Files module).</summary>
    public Guid FileNodeId { get; set; }

    /// <summary>Content hash referencing the canonical track.</summary>
    public required string CanonicalTrackHash { get; set; }

    /// <summary>Canonical album ID this user track belongs to.</summary>
    public Guid? CanonicalAlbumId { get; set; }

    /// <summary>Denormalized content hash for quick lookup.</summary>
    public required string ContentHash { get; set; }

    /// <summary>Play count for this track (user-specific).</summary>
    public int PlayCount { get; set; }

    /// <summary>Whether the track has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Timestamp when soft-deleted.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>When the track was added to the library (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the track record was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation to the canonical track.</summary>
    public CanonicalTrack? CanonicalTrack { get; set; }

    /// <summary>Navigation to the canonical album.</summary>
    public CanonicalAlbum? CanonicalAlbum { get; set; }

    /// <summary>Playlist associations for this user track.</summary>
    public ICollection<PlaylistTrack> PlaylistTracks { get; set; } = new List<PlaylistTrack>();
}
