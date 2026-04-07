namespace DotNetCloud.Modules.Photos.Models;

/// <summary>
/// Represents a photo in the gallery. Links to a FileNode in the Files module.
/// </summary>
public sealed class Photo
{
    /// <summary>Unique identifier for this photo.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The FileNode ID this photo references (from Files module).</summary>
    public Guid FileNodeId { get; set; }

    /// <summary>The user who owns this photo.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Original filename.</summary>
    public required string FileName { get; set; }

    /// <summary>MIME type (e.g. "image/jpeg").</summary>
    public required string MimeType { get; set; }

    /// <summary>File size in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Image width in pixels.</summary>
    public int? Width { get; set; }

    /// <summary>Image height in pixels.</summary>
    public int? Height { get; set; }

    /// <summary>Whether this photo is marked as a favorite.</summary>
    public bool IsFavorite { get; set; }

    /// <summary>Date the photo was taken (from EXIF), or upload date if unavailable.</summary>
    public DateTime TakenAt { get; set; } = DateTime.UtcNow;

    /// <summary>Whether the photo has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Timestamp when soft-deleted.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>When the photo record was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the photo record was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>JPEG thumbnail for gallery grid view (300px). Null if not yet generated.</summary>
    public byte[]? ThumbnailGrid { get; set; }

    /// <summary>JPEG thumbnail for detail/lightbox view (1200px). Null if not yet generated.</summary>
    public byte[]? ThumbnailDetail { get; set; }

    /// <summary>Metadata for this photo (EXIF, GPS, camera info).</summary>
    public PhotoMetadata? Metadata { get; set; }

    /// <summary>Albums this photo belongs to.</summary>
    public ICollection<AlbumPhoto> AlbumPhotos { get; set; } = new List<AlbumPhoto>();

    /// <summary>Tags applied to this photo.</summary>
    public ICollection<PhotoTag> Tags { get; set; } = new List<PhotoTag>();

    /// <summary>Shares for this photo.</summary>
    public ICollection<PhotoShare> Shares { get; set; } = new List<PhotoShare>();

    /// <summary>Non-destructive edit history.</summary>
    public ICollection<PhotoEditRecord> EditRecords { get; set; } = new List<PhotoEditRecord>();
}
