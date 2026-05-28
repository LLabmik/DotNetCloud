namespace DotNetCloud.Modules.Music.Models;

/// <summary>
/// Canonical (shared) album — intrinsic album metadata stored once.
/// No OwnerId — shared across all users.
/// </summary>
public sealed class CanonicalAlbum
{
    /// <summary>Unique identifier for this canonical album.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Album title.</summary>
    public required string Title { get; set; }

    /// <summary>Release year.</summary>
    public int? Year { get; set; }

    /// <summary>Whether album art is available in the media cache.</summary>
    public bool HasCoverArt { get; set; }

    /// <summary>Content hash of the cover art image (references .media-cache/images/).</summary>
    public string? CoverArtHash { get; set; }

    /// <summary>Total duration of all tracks in ticks.</summary>
    public long TotalDurationTicks { get; set; }

    /// <summary>MusicBrainz release group identifier (album concept).</summary>
    public string? MusicBrainzReleaseGroupId { get; set; }

    /// <summary>MusicBrainz release identifier (specific release, needed for Cover Art Archive lookup).</summary>
    public string? MusicBrainzReleaseId { get; set; }

    /// <summary>When the canonical album was last enriched from external sources (UTC).</summary>
    public DateTime? LastEnrichedAt { get; set; }

    /// <summary>When the canonical album record was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the canonical album record was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation: user albums referencing this canonical album.</summary>
    public ICollection<UserAlbum> UserAlbums { get; set; } = new List<UserAlbum>();

    /// <summary>Artist associations for this album.</summary>
    public ICollection<CanonicalAlbumArtist> AlbumArtists { get; set; } = new List<CanonicalAlbumArtist>();
}
