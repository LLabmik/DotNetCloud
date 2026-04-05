namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Dashboard widget data for media modules — recent photos, now playing, continue watching.
/// </summary>
public sealed record MediaDashboardDto
{
    /// <summary>Recent photos for the dashboard widget.</summary>
    public required IReadOnlyList<PhotoDto> RecentPhotos { get; init; }

    /// <summary>Recently played tracks for the dashboard widget.</summary>
    public required IReadOnlyList<TrackDto> RecentlyPlayed { get; init; }

    /// <summary>Videos the user has partially watched (for "Continue Watching" widget).</summary>
    public required IReadOnlyList<VideoContinueWatchingDto> ContinueWatching { get; init; }

    /// <summary>Recently added media items across all types.</summary>
    public required IReadOnlyList<RecentMediaItemDto> RecentlyAdded { get; init; }
}

/// <summary>
/// A video with watch progress information for the "Continue Watching" widget.
/// </summary>
public sealed record VideoContinueWatchingDto
{
    /// <summary>Video ID.</summary>
    public required Guid VideoId { get; init; }

    /// <summary>Video title.</summary>
    public required string Title { get; init; }

    /// <summary>Video file name.</summary>
    public required string FileName { get; init; }

    /// <summary>Total duration of the video.</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>The position the user last watched to.</summary>
    public required TimeSpan WatchPosition { get; init; }

    /// <summary>Percentage of the video watched (0.0-1.0).</summary>
    public double ProgressPercent => Duration.TotalSeconds > 0
        ? Math.Min(1.0, WatchPosition.TotalSeconds / Duration.TotalSeconds)
        : 0.0;

    /// <summary>When the user last watched this video.</summary>
    public required DateTime LastWatchedAt { get; init; }
}

/// <summary>
/// A recently added media item for the dashboard (polymorphic across media types).
/// </summary>
public sealed record RecentMediaItemDto
{
    /// <summary>The type of media (Photo, Audio, Video).</summary>
    public required string MediaType { get; init; }

    /// <summary>Entity ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Display title or filename.</summary>
    public required string Title { get; init; }

    /// <summary>When the item was added.</summary>
    public required DateTime AddedAt { get; init; }
}
