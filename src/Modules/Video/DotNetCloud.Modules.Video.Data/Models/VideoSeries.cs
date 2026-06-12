namespace DotNetCloud.Modules.Video.Models;

/// <summary>
/// Represents a series — either a TV series (with seasons/episodes) or a movie franchise.
/// </summary>
public sealed class VideoSeries
{
    /// <summary>Unique identifier for this series.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>The user who owns this series.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Series name (e.g. "Breaking Bad", "Star Wars").</summary>
    public required string Name { get; set; }

    /// <summary>Optional description / overview.</summary>
    public string? Description { get; set; }

    /// <summary>Whether this is a TV series or a movie franchise.</summary>
    public SeriesType Type { get; set; } = SeriesType.TvSeries;

    /// <summary>Poster thumbnail JPEG bytes.</summary>
    public byte[]? ThumbnailPoster { get; set; }

    /// <summary>Whether an external poster (TMDB) is available.</summary>
    public bool HasExternalPoster { get; set; }

    /// <summary>External poster path from TMDB (relative).</summary>
    public string? ExternalPosterPath { get; set; }

    /// <summary>TMDB ID — TV series ID for TV, Collection ID for movie franchises.</summary>
    public int? TmdbId { get; set; }

    /// <summary>Series name from TMDB (may differ from user-provided name).</summary>
    public string? TmdbName { get; set; }

    /// <summary>Series overview from TMDB.</summary>
    public string? TmdbOverview { get; set; }

    /// <summary>Average vote from TMDB (0-10).</summary>
    public double? TmdbRating { get; set; }

    /// <summary>Comma-separated genres from TMDB.</summary>
    public string? Genres { get; set; }

    /// <summary>Release or start year.</summary>
    public int? Year { get; set; }

    /// <summary>Series status from TMDB (e.g. "Ended", "Returning Series", "Released").</summary>
    public string? Status { get; set; }

    /// <summary>Number of seasons (for TV series).</summary>
    public int TotalSeasons { get; set; }

    /// <summary>Total number of episodes across all seasons.</summary>
    public int TotalEpisodes { get; set; }

    /// <summary>Whether the series has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Timestamp when soft-deleted.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>When the series was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the series was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Seasons in this series (for TV series).</summary>
    public ICollection<VideoSeason> Seasons { get; set; } = new List<VideoSeason>();

    /// <summary>Direct video items in this series (for movie franchises).</summary>
    public ICollection<VideoSeriesItem> Items { get; set; } = new List<VideoSeriesItem>();
}
