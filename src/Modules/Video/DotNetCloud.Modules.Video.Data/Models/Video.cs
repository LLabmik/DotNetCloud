namespace DotNetCloud.Modules.Video.Models;

/// <summary>
/// Represents a video file. Links to a FileNode in the Files module.
/// </summary>
public sealed class Video
{
    /// <summary>Unique identifier for this video.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The FileNode ID this video references (from Files module).</summary>
    public Guid FileNodeId { get; set; }

    /// <summary>The user who owns this video.</summary>
    public Guid OwnerId { get; set; }

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

    /// <summary>Whether the video is marked as a favorite.</summary>
    public bool IsFavorite { get; set; }

    /// <summary>View count.</summary>
    public int ViewCount { get; set; }

    /// <summary>Whether the video has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Timestamp when soft-deleted.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>When the video record was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the video record was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Metadata for this video (resolution, codecs, etc.).</summary>
    public VideoMetadata? Metadata { get; set; }

    /// <summary>Subtitles for this video.</summary>
    public ICollection<Subtitle> Subtitles { get; set; } = new List<Subtitle>();

    /// <summary>Collections this video belongs to.</summary>
    public ICollection<VideoCollectionItem> CollectionItems { get; set; } = new List<VideoCollectionItem>();

    /// <summary>Watch history records for this video.</summary>
    public ICollection<WatchHistory> WatchHistories { get; set; } = new List<WatchHistory>();

    /// <summary>Poster thumbnail JPEG bytes (300px wide, extracted from video via FFmpeg).</summary>
    public byte[]? ThumbnailPoster { get; set; }

    /// <summary>Watch progress records for this video.</summary>
    public ICollection<WatchProgress> WatchProgresses { get; set; } = new List<WatchProgress>();

    /// <summary>Share records for this video.</summary>
    public ICollection<VideoShare> Shares { get; set; } = new List<VideoShare>();
}
