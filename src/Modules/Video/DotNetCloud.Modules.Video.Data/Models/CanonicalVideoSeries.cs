namespace DotNetCloud.Modules.Video.Models;

/// <summary>
/// Canonical series — TV series or movie franchise metadata from TMDB.
/// No OwnerId — shared across all users.
/// </summary>
public sealed class CanonicalVideoSeries
{
    /// <summary>Unique identifier for this canonical series.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Series name (e.g. "Breaking Bad", "Star Wars").</summary>
    public required string Name { get; set; }

    /// <summary>Optional description / overview.</summary>
    public string? Description { get; set; }

    /// <summary>Whether this is a TV series or a movie franchise.</summary>
    public SeriesType Type { get; set; } = SeriesType.TvSeries;

    /// <summary>Content hash of the poster image.</summary>
    public string? PosterHash { get; set; }

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

    /// <summary>Series status from TMDB (e.g. "Ended", "Returning Series", "Released").</summary>
    public string? Status { get; set; }

    /// <summary>Number of seasons (for TV series).</summary>
    public int TotalSeasons { get; set; }

    /// <summary>Total number of episodes across all seasons.</summary>
    public int TotalEpisodes { get; set; }

    /// <summary>When the series record was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the series record was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Seasons in this series (for TV series).</summary>
    public ICollection<CanonicalVideoSeason> Seasons { get; set; } = new List<CanonicalVideoSeason>();

    /// <summary>Direct video items in this series (for movie franchises).</summary>
    public ICollection<CanonicalVideoSeriesItem> Items { get; set; } = new List<CanonicalVideoSeriesItem>();
}
