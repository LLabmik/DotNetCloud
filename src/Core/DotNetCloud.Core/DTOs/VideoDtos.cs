namespace DotNetCloud.Core.DTOs;

// ── Video DTOs ──────────────────────────────────────────────────────

/// <summary>
/// Represents a video in the library.
/// </summary>
public sealed record VideoDto
{
    /// <summary>Unique identifier for this video.</summary>
    public required Guid Id { get; init; }

    /// <summary>The user who owns this video.</summary>
    public required Guid OwnerId { get; init; }

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

    /// <summary>Whether a thumbnail image is available for this video.</summary>
    public bool HasThumbnail { get; init; }

    /// <summary>TMDB movie overview/description.</summary>
    public string? Overview { get; init; }

    /// <summary>Movie rating from TMDB (0-10).</summary>
    public double? TmdbRating { get; init; }

    /// <summary>Genres as comma-separated string.</summary>
    public string? Genres { get; init; }

    /// <summary>Release date from TMDB.</summary>
    public DateTime? ReleaseDate { get; init; }

    /// <summary>Short promotional tagline from TMDB.</summary>
    public string? TmdbTagline { get; init; }

    /// <summary>Number of votes the rating is based on.</summary>
    public int? TmdbVoteCount { get; init; }

    /// <summary>Original language code (e.g. "en", "ja") from TMDB.</summary>
    public string? TmdbOriginalLanguage { get; init; }

    /// <summary>Original title in the source language from TMDB.</summary>
    public string? TmdbOriginalTitle { get; init; }
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

// ── Video Series DTOs ───────────────────────────────────────────────

/// <summary>
/// Represents a video series (TV series or movie franchise).
/// </summary>
public sealed record VideoSeriesDto
{
    /// <summary>Unique identifier for this series.</summary>
    public required Guid Id { get; init; }

    /// <summary>Series name.</summary>
    public required string Name { get; init; }

    /// <summary>Optional description.</summary>
    public string? Description { get; init; }

    /// <summary>Series type (MovieFranchise or TvSeries).</summary>
    public required string Type { get; init; }

    /// <summary>Release or start year.</summary>
    public int? Year { get; init; }

    /// <summary>Average vote from TMDB (0-10).</summary>
    public double? TmdbRating { get; init; }

    /// <summary>Comma-separated genres.</summary>
    public string? Genres { get; init; }

    /// <summary>Series status (e.g. "Ended", "Returning Series", "Released").</summary>
    public string? Status { get; init; }

    /// <summary>Number of seasons (for TV series).</summary>
    public int TotalSeasons { get; init; }

    /// <summary>Total number of episodes across all seasons.</summary>
    public int TotalEpisodes { get; init; }

    /// <summary>Whether an external poster (TMDB) is available.</summary>
    public bool HasExternalPoster { get; init; }

    /// <summary>When the series was created (UTC).</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>When the series was last modified (UTC).</summary>
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Request to create a new video series.
/// </summary>
public sealed record CreateVideoSeriesDto
{
    /// <summary>Series name.</summary>
    public required string Name { get; init; }

    /// <summary>Optional description.</summary>
    public string? Description { get; init; }

    /// <summary>Series type. Defaults to TvSeries if not specified.</summary>
    public string? Type { get; init; }

    /// <summary>Release or start year.</summary>
    public int? Year { get; init; }
}

/// <summary>
/// Request to update a video series.
/// </summary>
public sealed record UpdateVideoSeriesDto
{
    /// <summary>Updated name.</summary>
    public string? Name { get; init; }

    /// <summary>Updated description.</summary>
    public string? Description { get; init; }

    /// <summary>Updated series type.</summary>
    public string? Type { get; init; }

    /// <summary>Updated release or start year.</summary>
    public int? Year { get; init; }
}

// ── Video Season DTOs ───────────────────────────────────────────────

/// <summary>
/// Represents a season within a TV series.
/// </summary>
public sealed record VideoSeasonDto
{
    /// <summary>Unique identifier for this season.</summary>
    public required Guid Id { get; init; }

    /// <summary>The series this season belongs to.</summary>
    public required Guid SeriesId { get; init; }

    /// <summary>Season number (1-based).</summary>
    public required int SeasonNumber { get; init; }

    /// <summary>Season name (e.g. "Season 1").</summary>
    public string? Name { get; init; }

    /// <summary>Optional overview.</summary>
    public string? Overview { get; init; }

    /// <summary>Number of episodes in this season.</summary>
    public int EpisodeCount { get; init; }

    /// <summary>Whether an external poster (TMDB) is available.</summary>
    public bool HasExternalPoster { get; init; }

    /// <summary>Original air date.</summary>
    public DateTime? AirDate { get; init; }

    /// <summary>When the season was created (UTC).</summary>
    public required DateTime CreatedAt { get; init; }
}

/// <summary>
/// Request to create a new season within a TV series.
/// </summary>
public sealed record CreateVideoSeasonDto
{
    /// <summary>The series ID this season belongs to.</summary>
    public required Guid SeriesId { get; init; }

    /// <summary>Season number (1-based).</summary>
    public required int SeasonNumber { get; init; }

    /// <summary>Season name.</summary>
    public string? Name { get; init; }

    /// <summary>Optional overview.</summary>
    public string? Overview { get; init; }
}

/// <summary>
/// Request to update a season.
/// </summary>
public sealed record UpdateVideoSeasonDto
{
    /// <summary>Updated season number.</summary>
    public int? SeasonNumber { get; init; }

    /// <summary>Updated name.</summary>
    public string? Name { get; init; }

    /// <summary>Updated overview.</summary>
    public string? Overview { get; init; }
}

// ── Video Series Item DTOs ──────────────────────────────────────────

/// <summary>
/// Represents a video item in a movie franchise series.
/// </summary>
public sealed record VideoSeriesItemDto
{
    /// <summary>Unique identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>The series this item belongs to.</summary>
    public required Guid SeriesId { get; init; }

    /// <summary>The video in this slot.</summary>
    public required Guid VideoId { get; init; }

    /// <summary>Sort order within the series.</summary>
    public int SortOrder { get; init; }

    /// <summary>Optional episode title (e.g. "Episode IV – A New Hope").</summary>
    public string? EpisodeTitle { get; init; }

    /// <summary>Nested video details.</summary>
    public VideoDto? Video { get; init; }
}

/// <summary>
/// Request to add a video to a movie franchise series.
/// </summary>
public sealed record AddVideoToSeriesDto
{
    /// <summary>The video ID to add.</summary>
    public required Guid VideoId { get; init; }

    /// <summary>Optional sort order. Auto-appended if not specified.</summary>
    public int? SortOrder { get; init; }

    /// <summary>Optional episode title.</summary>
    public string? EpisodeTitle { get; init; }
}

// ── Video Episode DTOs ──────────────────────────────────────────────

/// <summary>
/// Represents an episode in a TV series season.
/// </summary>
public sealed record VideoEpisodeDto
{
    /// <summary>Unique identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>The season this episode belongs to.</summary>
    public required Guid SeasonId { get; init; }

    /// <summary>The video for this episode.</summary>
    public required Guid VideoId { get; init; }

    /// <summary>Episode number within the season.</summary>
    public required int EpisodeNumber { get; init; }

    /// <summary>Episode title.</summary>
    public string? Title { get; init; }

    /// <summary>Episode overview.</summary>
    public string? Overview { get; init; }

    /// <summary>Sort order within the season.</summary>
    public int SortOrder { get; init; }

    /// <summary>Nested video details.</summary>
    public VideoDto? Video { get; init; }
}

/// <summary>
/// Request to add a video as an episode to a season.
/// </summary>
public sealed record AddEpisodeDto
{
    /// <summary>The video ID to add as an episode.</summary>
    public required Guid VideoId { get; init; }

    /// <summary>Episode number within the season.</summary>
    public required int EpisodeNumber { get; init; }

    /// <summary>Optional episode title.</summary>
    public string? Title { get; init; }

    /// <summary>Optional episode overview.</summary>
    public string? Overview { get; init; }
}

/// <summary>
/// Request to reorder items within a series or season.
/// </summary>
public sealed record ReorderRequestDto
{
    /// <summary>New sort order value.</summary>
    public required int NewSortOrder { get; init; }
}

// ── Combined Listing DTOs ───────────────────────────────────────────

/// <summary>
/// Combined library content — series cards + standalone videos.
/// Series are returned first (sorted by name), followed by standalone videos (sorted by title).
/// The server applies two-phase paging: series slots are consumed first, then video slots.
/// </summary>
public sealed record VideoLibraryContentDto
{
    /// <summary>Series on the current page (sorted by name).</summary>
    public IReadOnlyList<VideoSeriesDto> Series { get; init; } = [];

    /// <summary>Standalone videos on the current page (sorted by title, not part of any series).</summary>
    public IReadOnlyList<VideoDto> StandaloneVideos { get; init; } = [];

    /// <summary>Total number of series across all pages.</summary>
    public int TotalSeries { get; init; }

    /// <summary>Total number of standalone videos across all pages.</summary>
    public int TotalStandaloneVideos { get; init; }
}

/// <summary>
/// Combined collection content — series cards replace grouped series items, plus standalone videos.
/// </summary>
public sealed record VideoCollectionContentDto
{
    /// <summary>The collection metadata.</summary>
    public required VideoCollectionDto Collection { get; init; }

    /// <summary>Series that have videos in this collection (deduplicated).</summary>
    public IReadOnlyList<VideoSeriesDto> Series { get; init; } = [];

    /// <summary>Standalone videos in the collection (not part of any series).</summary>
    public IReadOnlyList<VideoDto> StandaloneVideos { get; init; } = [];

    /// <summary>Total items in the collection (for display).</summary>
    public int TotalItems { get; init; }
}

/// <summary>
/// Search results for the video module — includes both series and standalone video matches.
/// </summary>
public sealed record VideoSearchResultDto
{
    /// <summary>Series matching the search query.</summary>
    public IReadOnlyList<VideoSeriesDto> Series { get; init; } = [];

    /// <summary>Standalone videos matching the search query.</summary>
    public IReadOnlyList<VideoDto> StandaloneVideos { get; init; } = [];
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

/// <summary>
/// An audio stream available for a video (for the audio-track selector).
/// <see cref="Index"/> is the 0-based positional index within the audio streams,
/// matching ffmpeg's <c>-map 0:a:N</c> selector and the <c>audioStreamIndex</c>
/// query parameter used by the stream endpoint.
/// </summary>
public sealed record VideoAudioStreamDto
{
    /// <summary>0-based positional index of this audio stream (within audio streams).</summary>
    public required int Index { get; init; }

    /// <summary>Audio codec name (e.g. "aac", "ac3", "dts").</summary>
    public string? Codec { get; init; }

    /// <summary>ISO 639 language code (e.g. "eng", "jpn"), may be absent.</summary>
    public string? Language { get; init; }

    /// <summary>Optional human-readable track title (e.g. "Stereo", "Commentary").</summary>
    public string? Title { get; init; }

    /// <summary>Number of audio channels (e.g. 2 for stereo, 6 for 5.1).</summary>
    public int? Channels { get; init; }

    /// <summary>Whether this is the default audio stream.</summary>
    public bool IsDefault { get; init; }
}

/// <summary>
/// Request to manually correct a video's displayed metadata. Used when automatic
/// TMDB enrichment matched the wrong title/overview/etc., so the user can fix it.
/// Only the properties that are provided (non-null) are updated — partial edits
/// are supported.
/// </summary>
public sealed record UpdateVideoDetailsDto
{
    /// <summary>Corrected video title. Empty/whitespace values are ignored.</summary>
    public string? Title { get; init; }

    /// <summary>Corrected overview/description.</summary>
    public string? Overview { get; init; }

    /// <summary>Corrected comma-separated genres.</summary>
    public string? Genres { get; init; }

    /// <summary>Corrected release date.</summary>
    public DateTime? ReleaseDate { get; init; }

    /// <summary>Corrected tagline.</summary>
    public string? Tagline { get; init; }
}
