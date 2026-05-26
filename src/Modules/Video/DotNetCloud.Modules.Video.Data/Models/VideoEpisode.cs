namespace DotNetCloud.Modules.Video.Models;

/// <summary>
/// Junction entity linking a video to a season as an episode (for TV series).
/// </summary>
public sealed class VideoEpisode
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The season this episode belongs to.</summary>
    public Guid SeasonId { get; set; }

    /// <summary>The video for this episode.</summary>
    public Guid VideoId { get; set; }

    /// <summary>Episode number within the season.</summary>
    public int EpisodeNumber { get; set; }

    /// <summary>Episode title (may differ from video filename).</summary>
    public string? Title { get; set; }

    /// <summary>Episode-specific overview / description.</summary>
    public string? Overview { get; set; }

    /// <summary>Sort order within the season.</summary>
    public int SortOrder { get; set; }

    /// <summary>When added (UTC).</summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation to the season.</summary>
    public VideoSeason? Season { get; set; }

    /// <summary>Navigation to the video.</summary>
    public Video? Video { get; set; }
}
