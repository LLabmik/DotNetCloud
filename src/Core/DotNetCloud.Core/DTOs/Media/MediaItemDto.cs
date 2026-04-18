namespace DotNetCloud.Core.DTOs.Media;

/// <summary>
/// Represents a media item (photo, audio track, or video) with its core file
/// information and extracted metadata. This is the shared cross-module DTO
/// returned by media capability interfaces.
/// </summary>
public sealed record MediaItemDto
{
    /// <summary>
    /// Unique identifier for this media item (same as the underlying <c>FileNode</c> ID).
    /// </summary>
    public required Guid FileNodeId { get; init; }

    /// <summary>
    /// The type of media (Photo, Audio, or Video).
    /// </summary>
    public required MediaType MediaType { get; init; }

    /// <summary>
    /// Original file name including extension.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// MIME type of the file (e.g. "image/jpeg", "audio/flac", "video/mp4").
    /// </summary>
    public required string MimeType { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long SizeBytes { get; init; }

    /// <summary>
    /// User who owns this media item.
    /// </summary>
    public required Guid OwnerId { get; init; }

    /// <summary>
    /// When the media file was uploaded or created (UTC).
    /// </summary>
    public required DateTime CreatedAtUtc { get; init; }

    /// <summary>
    /// When the media record was last modified (UTC).
    /// </summary>
    public DateTime? ModifiedAtUtc { get; init; }

    /// <summary>
    /// Extracted metadata (dimensions, duration, tags, EXIF, etc.).
    /// May be <c>null</c> if metadata extraction has not yet completed.
    /// </summary>
    public MediaMetadataDto? Metadata { get; init; }
}
