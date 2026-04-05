namespace DotNetCloud.Modules.Video.Models;

/// <summary>
/// Records a video viewing event in the user's history.
/// </summary>
public sealed class WatchHistory
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user who watched the video.</summary>
    public Guid UserId { get; set; }

    /// <summary>The video that was watched.</summary>
    public Guid VideoId { get; set; }

    /// <summary>When the video was watched (UTC).</summary>
    public DateTime WatchedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Duration watched in seconds.</summary>
    public int DurationWatchedSeconds { get; set; }

    /// <summary>Navigation to the video.</summary>
    public Video? Video { get; set; }
}
