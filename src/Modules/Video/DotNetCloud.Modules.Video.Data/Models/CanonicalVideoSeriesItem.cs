namespace DotNetCloud.Modules.Video.Models;

/// <summary>
/// Canonical junction entity linking a canonical video (by ContentHash) directly to a series
/// (for movie franchises). No OwnerId — shared across all users.
/// </summary>
public sealed class CanonicalVideoSeriesItem
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The canonical series this item belongs to.</summary>
    public Guid SeriesId { get; set; }

    /// <summary>The canonical video content hash in this series slot.</summary>
    public required string VideoContentHash { get; set; }

    /// <summary>Sort order within the series (chronological order).</summary>
    public int SortOrder { get; set; }

    /// <summary>Optional episode title (e.g. "Episode IV – A New Hope").</summary>
    public string? EpisodeTitle { get; set; }

    /// <summary>When added to the series (UTC).</summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation to the canonical series.</summary>
    public CanonicalVideoSeries? Series { get; set; }
}
