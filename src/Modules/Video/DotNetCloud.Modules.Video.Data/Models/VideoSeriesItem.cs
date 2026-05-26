namespace DotNetCloud.Modules.Video.Models;

/// <summary>
/// Junction entity linking a video directly to a series (for movie franchises).
/// </summary>
public sealed class VideoSeriesItem
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The series this item belongs to.</summary>
    public Guid SeriesId { get; set; }

    /// <summary>The video in this series slot.</summary>
    public Guid VideoId { get; set; }

    /// <summary>Sort order within the series (chronological order).</summary>
    public int SortOrder { get; set; }

    /// <summary>Optional episode title (e.g. "Episode IV – A New Hope").</summary>
    public string? EpisodeTitle { get; set; }

    /// <summary>When added to the series (UTC).</summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation to the series.</summary>
    public VideoSeries? Series { get; set; }

    /// <summary>Navigation to the video.</summary>
    public Video? Video { get; set; }
}
