namespace DotNetCloud.Modules.Music.Models;

/// <summary>
/// Canonical (shared) artist — intrinsic artist metadata stored once.
/// No OwnerId — shared across all users.
/// </summary>
public sealed class CanonicalArtist
{
    /// <summary>Unique identifier for this canonical artist.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Artist name.</summary>
    public required string Name { get; set; }

    /// <summary>Sort name for alphabetical ordering (e.g. "Beatles, The").</summary>
    public string? SortName { get; set; }

    /// <summary>MusicBrainz artist identifier.</summary>
    public string? MusicBrainzId { get; set; }

    /// <summary>Artist biography from MusicBrainz annotation or Wikidata.</summary>
    public string? Biography { get; set; }

    /// <summary>Artist image URL (from Cover Art Archive or fanart).</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Wikipedia URL extracted from MusicBrainz URL relations.</summary>
    public string? WikipediaUrl { get; set; }

    /// <summary>Discogs URL extracted from MusicBrainz URL relations.</summary>
    public string? DiscogsUrl { get; set; }

    /// <summary>Official website from MusicBrainz URL relations.</summary>
    public string? OfficialUrl { get; set; }

    /// <summary>When the canonical artist was last enriched from external sources (UTC).</summary>
    public DateTime? LastEnrichedAt { get; set; }

    /// <summary>When the canonical artist record was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the canonical artist record was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation: user artists referencing this canonical artist.</summary>
    public ICollection<UserArtist> UserArtists { get; set; } = new List<UserArtist>();

    /// <summary>Track associations for this canonical artist.</summary>
    public ICollection<CanonicalTrackArtist> TrackArtists { get; set; } = new List<CanonicalTrackArtist>();

    /// <summary>Album associations for this canonical artist.</summary>
    public ICollection<CanonicalAlbumArtist> AlbumArtists { get; set; } = new List<CanonicalAlbumArtist>();
}
