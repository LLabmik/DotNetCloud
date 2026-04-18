namespace DotNetCloud.Modules.Video.Models;

/// <summary>
/// Tracks the user's watch progress on a specific video (for resume playback).
/// </summary>
public sealed class WatchProgress
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user watching the video.</summary>
    public Guid UserId { get; set; }

    /// <summary>The video being watched.</summary>
    public Guid VideoId { get; set; }

    /// <summary>Current position in ticks.</summary>
    public long PositionTicks { get; set; }

    /// <summary>Whether the video has been fully watched.</summary>
    public bool IsCompleted { get; set; }

    /// <summary>When the progress was last updated (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation to the video.</summary>
    public Video? Video { get; set; }
}
