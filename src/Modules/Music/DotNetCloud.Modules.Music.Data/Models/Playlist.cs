namespace DotNetCloud.Modules.Music.Models;

/// <summary>
/// Represents a user-created playlist.
/// </summary>
public sealed class Playlist
{
    /// <summary>Unique identifier for this playlist.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user who owns this playlist.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Playlist name.</summary>
    public required string Name { get; set; }

    /// <summary>Optional playlist description.</summary>
    public string? Description { get; set; }

    /// <summary>Whether this playlist is public (visible to all users).</summary>
    public bool IsPublic { get; set; }

    /// <summary>Whether the playlist has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Timestamp when soft-deleted.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>When the playlist was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the playlist was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Tracks in this playlist.</summary>
    public ICollection<PlaylistTrack> PlaylistTracks { get; set; } = new List<PlaylistTrack>();
}
