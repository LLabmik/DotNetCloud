namespace DotNetCloud.Core.DTOs;

// ── Video DTOs ──────────────────────────────────────────────────────

/// <summary>
/// Represents a video in the library.
/// </summary>
public sealed record VideoDto
{
    /// <summary>Unique identifier for this video.</summary>
    public required Guid Id { get; init; }

    /// <summary>The FileNode ID that this video references.</summary>
    public required Guid FileNodeId { get; init; }

    /// <summary>Video title.</summary>
    public required string Title { get; init; }

    /// <summary>Original filename.</summary>
    public required string FileName { get; init; }

    /// <summary>MIME type (e.g. "video/mp4").</summary>
    public required string MimeType { get; init; }

    /// <summary>File size in bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>Video duration.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Video width in pixels.</summary>
    public int? Width { get; init; }

    /// <summary>Video height in pixels.</summary>
    public int? Height { get; init; }

    /// <summary>Whether this video is starred by the current user.</summary>
    public bool IsFavorite { get; init; }

    /// <summary>View count.</summary>
    public int ViewCount { get; init; }

    /// <summary>Watch progress position in ticks for the current user (for resume).</summary>
    public long? WatchPositionTicks { get; init; }

    /// <summary>When the video was added to the library (UTC).</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>Whether an external poster (TMDB) is available for this video.</summary>
    public bool HasExternalPoster { get; init; }

    /// <summary>TMDB movie overview/description.</summary>
    public string? Overview { get; init; }

    /// <summary>Movie rating from TMDB (0-10).</summary>
    public double? TmdbRating { get; init; }

    /// <summary>Genres as comma-separated string.</summary>
    public string? Genres { get; init; }

    /// <summary>Release date from TMDB.</summary>
    public DateTime? ReleaseDate { get; init; }
}

// ── Video Collection DTOs ───────────────────────────────────────────

/// <summary>
/// Represents a video collection (series, playlist, etc.).
/// </summary>
public sealed record VideoCollectionDto
{
    /// <summary>Unique identifier for this collection.</summary>
    public required Guid Id { get; init; }

    /// <summary>Collection name.</summary>
    public required string Name { get; init; }

    /// <summary>Optional description.</summary>
    public string? Description { get; init; }

    /// <summary>Number of videos in this collection.</summary>
    public int VideoCount { get; init; }

    /// <summary>Total duration of all videos in the collection.</summary>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>When the collection was created (UTC).</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>When the collection was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Request to create a new video collection.
/// </summary>
public sealed record CreateVideoCollectionDto
{
    /// <summary>Collection name.</summary>
    public required string Name { get; init; }

    /// <summary>Optional description.</summary>
    public string? Description { get; init; }
}

/// <summary>
/// Request to update a video collection.
/// </summary>
public sealed record UpdateVideoCollectionDto
{
    /// <summary>Updated name.</summary>
    public string? Name { get; init; }

    /// <summary>Updated description.</summary>
    public string? Description { get; init; }
}

// ── Subtitle DTOs ───────────────────────────────────────────────────

/// <summary>
/// Represents a subtitle track associated with a video.
/// </summary>
public sealed record SubtitleDto
{
    /// <summary>Unique identifier for this subtitle.</summary>
    public required Guid Id { get; init; }

    /// <summary>The video this subtitle belongs to.</summary>
    public required Guid VideoId { get; init; }

    /// <summary>Language code (e.g. "en", "fr", "es").</summary>
    public required string Language { get; init; }

    /// <summary>Optional label (e.g. "English (SDH)", "Forced").</summary>
    public string? Label { get; init; }

    /// <summary>Format: "srt" or "vtt".</summary>
    public required string Format { get; init; }

    /// <summary>Whether this is the default subtitle track.</summary>
    public bool IsDefault { get; init; }

    /// <summary>When the subtitle was uploaded (UTC).</summary>
    public required DateTime CreatedAt { get; init; }
}

/// <summary>
/// Request to upload a subtitle.
/// </summary>
public sealed record UploadSubtitleDto
{
    /// <summary>Language code.</summary>
    public required string Language { get; init; }

    /// <summary>Optional label.</summary>
    public string? Label { get; init; }

    /// <summary>Format: "srt" or "vtt".</summary>
    public required string Format { get; init; }

    /// <summary>Subtitle file content.</summary>
    public required string Content { get; init; }

    /// <summary>Whether this should be the default track.</summary>
    public bool IsDefault { get; init; }
}

// ── Watch Progress DTOs ─────────────────────────────────────────────

/// <summary>
/// Represents a user's watch progress on a video.
/// </summary>
public sealed record WatchProgressDto
{
    /// <summary>The video ID.</summary>
    public required Guid VideoId { get; init; }

    /// <summary>Video title.</summary>
    public required string VideoTitle { get; init; }

    /// <summary>Current position in ticks.</summary>
    public long PositionTicks { get; init; }

    /// <summary>Total video duration in ticks.</summary>
    public long DurationTicks { get; init; }

    /// <summary>Progress percentage (0-100).</summary>
    public double ProgressPercent { get; init; }

    /// <summary>When the progress was last updated (UTC).</summary>
    public DateTime LastWatchedAt { get; init; }
}

/// <summary>
/// Request to update watch progress.
/// </summary>
public sealed record UpdateWatchProgressDto
{
    /// <summary>Current position in ticks.</summary>
    public long PositionTicks { get; init; }
}

// ── Video Metadata DTO ──────────────────────────────────────────────

/// <summary>
/// Detailed metadata for a video file.
/// </summary>
public sealed record VideoMetadataDto
{
    /// <summary>The video ID.</summary>
    public required Guid VideoId { get; init; }

    /// <summary>Video width in pixels.</summary>
    public int Width { get; init; }

    /// <summary>Video height in pixels.</summary>
    public int Height { get; init; }

    /// <summary>Frame rate (frames per second).</summary>
    public double FrameRate { get; init; }

    /// <summary>Video codec name (e.g. "h264", "hevc", "vp9").</summary>
    public string? VideoCodec { get; init; }

    /// <summary>Audio codec name (e.g. "aac", "opus", "ac3").</summary>
    public string? AudioCodec { get; init; }

    /// <summary>Video bitrate in bps.</summary>
    public long Bitrate { get; init; }

    /// <summary>Number of audio tracks.</summary>
    public int AudioTrackCount { get; init; }

    /// <summary>Number of subtitle tracks.</summary>
    public int SubtitleTrackCount { get; init; }

    /// <summary>Container format (e.g. "mp4", "mkv", "webm").</summary>
    public string? ContainerFormat { get; init; }
}
