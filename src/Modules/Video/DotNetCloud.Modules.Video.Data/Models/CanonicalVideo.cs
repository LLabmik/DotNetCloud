namespace DotNetCloud.Modules.Video.Models;

/// <summary>
/// Canonical (shared) video — intrinsic video file properties stored once per ContentHash.
/// No OwnerId — shared across all users who index the same file.
/// </summary>
public sealed class CanonicalVideo
{
    /// <summary>SHA-256 content hash of the underlying video file (primary key).</summary>
    public required string ContentHash { get; set; }

    /// <summary>Video title.</summary>
    public required string Title { get; set; }

    /// <summary>Original filename.</summary>
    public required string FileName { get; set; }

    /// <summary>MIME type (e.g. "video/mp4").</summary>
    public required string MimeType { get; set; }

    /// <summary>File size in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Video duration in ticks.</summary>
    public long DurationTicks { get; set; }

    /// <summary>Content hash of the thumbnail poster (references .media-cache/images/).</summary>
    public string? ThumbnailPosterHash { get; set; }

    /// <summary>Whether an external poster (TMDB) has been fetched.</summary>
    public bool HasExternalPoster { get; set; }

    /// <summary>Content hash of the external poster (references .media-cache/images/).</summary>
    public string? ExternalPosterHash { get; set; }

    /// <summary>Title from embedded file metadata (ffprobe format.tags.title).</summary>
    public string? EmbeddedTitle { get; set; }

    /// <summary>IMDB ID from embedded file metadata (ffprobe format.tags.IMDB).</summary>
    public string? EmbeddedImdbId { get; set; }

    /// <summary>TMDB ID from embedded file metadata (ffprobe format.tags.TMDB).</summary>
    public int? EmbeddedTmdbId { get; set; }

    /// <summary>Date from embedded file metadata (ffprobe format.tags.date/creation_time).</summary>
    public string? EmbeddedDate { get; set; }

    /// <summary>Language from embedded file metadata (ffprobe format.tags.language).</summary>
    public string? EmbeddedLanguage { get; set; }

    /// <summary>When the canonical video record was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the canonical video record was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation to canonical metadata.</summary>
    public CanonicalVideoMetadata? Metadata { get; set; }

    /// <summary>Subtitles for this video (intrinsic to the file).</summary>
    public ICollection<CanonicalSubtitle> Subtitles { get; set; } = new List<CanonicalSubtitle>();

    /// <summary>User video junctions referencing this canonical video.</summary>
    public ICollection<UserVideo> UserVideos { get; set; } = new List<UserVideo>();
}
