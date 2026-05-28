namespace DotNetCloud.Modules.Video.Models;

/// <summary>
/// Canonical TMDB enrichment data — stored once per TMDB movie ID.
/// Shared across all users who have a video matching this TMDB entry.
/// </summary>
public sealed class CanonicalTmdbData
{
    /// <summary>TMDB movie ID (primary key).</summary>
    public int TmdbId { get; set; }

    /// <summary>Movie title from TMDB.</summary>
    public required string TmdbTitle { get; set; }

    /// <summary>Movie overview/description from TMDB.</summary>
    public string? Overview { get; set; }

    /// <summary>Release date from TMDB.</summary>
    public DateTime? ReleaseDate { get; set; }

    /// <summary>Average vote from TMDB (0-10).</summary>
    public double? TmdbRating { get; set; }

    /// <summary>Comma-separated genres from TMDB.</summary>
    public string? Genres { get; set; }

    /// <summary>Content hash of the external poster (references .media-cache/images/).</summary>
    public string? ExternalPosterHash { get; set; }

    /// <summary>When the TMDB data was last refreshed (UTC).</summary>
    public DateTime? LastEnrichedAt { get; set; }

    /// <summary>When the record was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the record was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
