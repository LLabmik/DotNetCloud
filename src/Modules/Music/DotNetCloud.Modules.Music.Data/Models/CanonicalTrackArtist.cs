namespace DotNetCloud.Modules.Music.Models;

/// <summary>
/// Canonical junction table for track-artist many-to-many relationships.
/// No OwnerId — shared across all users.
/// </summary>
public sealed class CanonicalTrackArtist
{
    /// <summary>The canonical track content hash.</summary>
    public required string TrackContentHash { get; set; }

    /// <summary>The canonical artist ID.</summary>
    public Guid ArtistId { get; set; }

    /// <summary>Whether this is the primary artist for the track.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Navigation to the canonical track.</summary>
    public CanonicalTrack? Track { get; set; }

    /// <summary>Navigation to the canonical artist.</summary>
    public CanonicalArtist? Artist { get; set; }
}
