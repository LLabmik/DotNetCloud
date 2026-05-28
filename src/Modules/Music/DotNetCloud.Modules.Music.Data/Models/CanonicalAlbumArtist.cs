namespace DotNetCloud.Modules.Music.Models;

/// <summary>
/// Canonical junction table for album-artist many-to-many relationships.
/// No OwnerId — shared across all users.
/// </summary>
public sealed class CanonicalAlbumArtist
{
    /// <summary>The canonical album ID.</summary>
    public Guid AlbumId { get; set; }

    /// <summary>The canonical artist ID.</summary>
    public Guid ArtistId { get; set; }

    /// <summary>Whether this is the primary artist for the album.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Navigation to the canonical album.</summary>
    public CanonicalAlbum? Album { get; set; }

    /// <summary>Navigation to the canonical artist.</summary>
    public CanonicalArtist? Artist { get; set; }
}
