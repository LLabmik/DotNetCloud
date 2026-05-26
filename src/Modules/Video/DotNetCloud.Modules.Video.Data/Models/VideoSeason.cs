namespace DotNetCloud.Modules.Video.Models;

/// <summary>
/// Represents a season within a TV series.
/// </summary>
public sealed class VideoSeason
{
    /// <summary>Unique identifier for this season.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The series this season belongs to.</summary>
    public Guid SeriesId { get; set; }

    /// <summary>Season number (1-based).</summary>
    public int SeasonNumber { get; set; }

    /// <summary>Season name (e.g. "Season 1", or a specific subtitle).</summary>
    public string? Name { get; set; }

    /// <summary>Optional overview / description.</summary>
    public string? Overview { get; set; }

    /// <summary>Season poster thumbnail JPEG bytes.</summary>
    public byte[]? ThumbnailPoster { get; set; }

    /// <summary>Whether an external poster (TMDB) is available.</summary>
    public bool HasExternalPoster { get; set; }

    /// <summary>TMDB season ID.</summary>
    public int? TmdbId { get; set; }

    /// <summary>Number of episodes in this season.</summary>
    public int EpisodeCount { get; set; }

    /// <summary>Original air date (UTC).</summary>
    public DateTime? AirDate { get; set; }

    /// <summary>Whether the season has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Timestamp when soft-deleted.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>When the season was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the season was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation to the parent series.</summary>
    public VideoSeries? Series { get; set; }

    /// <summary>Episodes in this season.</summary>
    public ICollection<VideoEpisode> Episodes { get; set; } = new List<VideoEpisode>();
}
