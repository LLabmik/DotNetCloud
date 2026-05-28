namespace DotNetCloud.Modules.Music.Models;

/// <summary>
/// Canonical junction table for track-genre many-to-many relationships.
/// No OwnerId — shared across all users.
/// </summary>
public sealed class CanonicalTrackGenre
{
    /// <summary>The canonical track content hash.</summary>
    public required string TrackContentHash { get; set; }

    /// <summary>The canonical genre ID.</summary>
    public Guid GenreId { get; set; }

    /// <summary>Navigation to the canonical track.</summary>
    public CanonicalTrack? Track { get; set; }

    /// <summary>Navigation to the canonical genre.</summary>
    public CanonicalGenre? Genre { get; set; }
}
