namespace DotNetCloud.Modules.Music.Models;

/// <summary>
/// Represents a music album.
/// </summary>
public sealed class MusicAlbum
{
    /// <summary>Unique identifier for this album.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Album title.</summary>
    public required string Title { get; set; }

    /// <summary>Primary artist ID.</summary>
    public Guid ArtistId { get; set; }

    /// <summary>The user who owns this album record.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Release year.</summary>
    public int? Year { get; set; }

    /// <summary>Whether album art is available.</summary>
    public bool HasCoverArt { get; set; }

    /// <summary>Path to cached cover art file (relative to module storage).</summary>
    public string? CoverArtPath { get; set; }

    /// <summary>Total duration of all tracks in ticks.</summary>
    public long TotalDurationTicks { get; set; }

    /// <summary>Whether the album has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Timestamp when soft-deleted.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>When the album record was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the album record was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>MusicBrainz release group identifier (album concept).</summary>
    public string? MusicBrainzReleaseGroupId { get; set; }

    /// <summary>MusicBrainz release identifier (specific release, needed for Cover Art Archive lookup).</summary>
    public string? MusicBrainzReleaseId { get; set; }

    /// <summary>When the album was last enriched from external sources (UTC).</summary>
    public DateTime? LastEnrichedAt { get; set; }

    /// <summary>Navigation to the primary artist.</summary>
    public Artist? Artist { get; set; }

    /// <summary>Tracks on this album.</summary>
    public ICollection<Track> Tracks { get; set; } = new List<Track>();
}
