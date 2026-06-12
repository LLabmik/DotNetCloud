namespace DotNetCloud.Modules.Video.Models;

/// <summary>
/// Canonical season within a TV series — shared across all users.
/// </summary>
public sealed class CanonicalVideoSeason
{
    /// <summary>Unique identifier for this season.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>The canonical series this season belongs to.</summary>
    public Guid SeriesId { get; set; }

    /// <summary>Season number (1-based).</summary>
    public int SeasonNumber { get; set; }

    /// <summary>Season name (e.g. "Season 1", or a specific subtitle).</summary>
    public string? Name { get; set; }

    /// <summary>Optional overview / description.</summary>
    public string? Overview { get; set; }

    /// <summary>Content hash of the season poster image.</summary>
    public string? PosterHash { get; set; }

    /// <summary>TMDB season ID.</summary>
    public int? TmdbId { get; set; }

    /// <summary>Number of episodes in this season.</summary>
    public int EpisodeCount { get; set; }

    /// <summary>Original air date (UTC).</summary>
    public DateTime? AirDate { get; set; }

    /// <summary>When the season was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the season was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation to the parent canonical series.</summary>
    public CanonicalVideoSeries? Series { get; set; }

    /// <summary>Episodes in this season.</summary>
    public ICollection<CanonicalVideoEpisode> Episodes { get; set; } = new List<CanonicalVideoEpisode>();
}
