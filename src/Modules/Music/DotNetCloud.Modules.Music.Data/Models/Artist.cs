namespace DotNetCloud.Modules.Music.Models;

/// <summary>
/// Represents a music artist.
/// </summary>
public sealed class Artist
{
    /// <summary>Unique identifier for this artist.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Artist name.</summary>
    public required string Name { get; set; }

    /// <summary>Sort name for alphabetical ordering (e.g. "Beatles, The").</summary>
    public string? SortName { get; set; }

    /// <summary>The user who owns this artist record.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Whether the artist has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Timestamp when soft-deleted.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>When the artist record was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the artist record was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

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

    /// <summary>When the artist was last enriched from external sources (UTC).</summary>
    public DateTime? LastEnrichedAt { get; set; }

    /// <summary>Albums by this artist.</summary>
    public ICollection<MusicAlbum> Albums { get; set; } = new List<MusicAlbum>();

    /// <summary>Track associations for this artist.</summary>
    public ICollection<TrackArtist> TrackArtists { get; set; } = new List<TrackArtist>();
}
