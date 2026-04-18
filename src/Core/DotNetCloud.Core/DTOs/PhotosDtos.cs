namespace DotNetCloud.Core.DTOs;

using DotNetCloud.Core.DTOs.Media;

// ── Photo DTOs ──────────────────────────────────────────────────────

/// <summary>
/// Represents a photo in the gallery.
/// </summary>
public sealed record PhotoDto
{
    /// <summary>Unique identifier for this photo.</summary>
    public required Guid Id { get; init; }

    /// <summary>The FileNode ID that this photo references.</summary>
    public required Guid FileNodeId { get; init; }

    /// <summary>The user who owns this photo.</summary>
    public required Guid OwnerId { get; init; }

    /// <summary>Original filename.</summary>
    public required string FileName { get; init; }

    /// <summary>MIME type (e.g. "image/jpeg").</summary>
    public required string MimeType { get; init; }

    /// <summary>File size in bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>Image width in pixels.</summary>
    public int? Width { get; init; }

    /// <summary>Image height in pixels.</summary>
    public int? Height { get; init; }

    /// <summary>Whether this photo is marked as a favorite.</summary>
    public bool IsFavorite { get; init; }

    /// <summary>Date the photo was taken (from EXIF), or upload date if unavailable.</summary>
    public required DateTime TakenAt { get; init; }

    /// <summary>When the photo was uploaded (UTC).</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>When the photo record was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>Extracted metadata (EXIF, GPS, camera info).</summary>
    public PhotoMetadataDto? Metadata { get; init; }

    /// <summary>Tags applied to this photo.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Whether there are pending edits on this photo.</summary>
    public bool HasEdits { get; init; }
}

/// <summary>
/// Metadata extracted from a photo's EXIF data.
/// </summary>
public sealed record PhotoMetadataDto
{
    /// <summary>Camera manufacturer (e.g. "Canon").</summary>
    public string? CameraMake { get; init; }

    /// <summary>Camera model (e.g. "EOS R5").</summary>
    public string? CameraModel { get; init; }

    /// <summary>Lens description.</summary>
    public string? LensModel { get; init; }

    /// <summary>Focal length in millimetres.</summary>
    public double? FocalLengthMm { get; init; }

    /// <summary>Aperture as an f-number (e.g. 2.8).</summary>
    public double? Aperture { get; init; }

    /// <summary>Shutter speed as a string (e.g. "1/250").</summary>
    public string? ShutterSpeed { get; init; }

    /// <summary>ISO sensitivity.</summary>
    public int? Iso { get; init; }

    /// <summary>Whether the flash fired.</summary>
    public bool? FlashFired { get; init; }

    /// <summary>EXIF orientation value (1–8).</summary>
    public int? Orientation { get; init; }

    /// <summary>GPS coordinates if available.</summary>
    public GeoCoordinate? Location { get; init; }
}

// ── Album DTOs ──────────────────────────────────────────────────────

/// <summary>
/// Represents a photo album.
/// </summary>
public sealed record AlbumDto
{
    /// <summary>Unique identifier for this album.</summary>
    public required Guid Id { get; init; }

    /// <summary>The user who owns this album.</summary>
    public required Guid OwnerId { get; init; }

    /// <summary>Album title.</summary>
    public required string Title { get; init; }

    /// <summary>Optional album description.</summary>
    public string? Description { get; init; }

    /// <summary>Cover photo ID (displayed as album thumbnail).</summary>
    public Guid? CoverPhotoId { get; init; }

    /// <summary>Number of photos in this album.</summary>
    public int PhotoCount { get; init; }

    /// <summary>When the album was created (UTC).</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>When the album was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>Whether this album has been shared with other users.</summary>
    public bool IsShared { get; init; }
}

/// <summary>
/// Request to create a new album.
/// </summary>
public sealed record CreateAlbumDto
{
    /// <summary>Album title.</summary>
    public required string Title { get; init; }

    /// <summary>Optional album description.</summary>
    public string? Description { get; init; }

    /// <summary>Optional cover photo ID.</summary>
    public Guid? CoverPhotoId { get; init; }
}

/// <summary>
/// Request to update an album.
/// </summary>
public sealed record UpdateAlbumDto
{
    /// <summary>Updated album title.</summary>
    public string? Title { get; init; }

    /// <summary>Updated album description.</summary>
    public string? Description { get; init; }

    /// <summary>Updated cover photo ID.</summary>
    public Guid? CoverPhotoId { get; init; }
}

// ── Edit Operation DTOs ─────────────────────────────────────────────

/// <summary>
/// Represents a non-destructive photo edit operation.
/// </summary>
public sealed record PhotoEditOperationDto
{
    /// <summary>The type of edit operation.</summary>
    public required PhotoEditType OperationType { get; init; }

    /// <summary>Operation-specific parameters as key-value pairs.</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Types of non-destructive photo edit operations.
/// </summary>
public enum PhotoEditType
{
    /// <summary>Crop to a rectangular region.</summary>
    Crop,

    /// <summary>Rotate by 90, 180, or 270 degrees.</summary>
    Rotate,

    /// <summary>Flip horizontally or vertically.</summary>
    Flip,

    /// <summary>Adjust brightness.</summary>
    Brightness,

    /// <summary>Adjust contrast.</summary>
    Contrast,

    /// <summary>Adjust saturation.</summary>
    Saturation,

    /// <summary>Apply sharpening.</summary>
    Sharpen,

    /// <summary>Apply blur.</summary>
    Blur
}

// ── Geo Cluster DTOs ────────────────────────────────────────────────

/// <summary>
/// A cluster of photos grouped by geographic proximity.
/// </summary>
public sealed record GeoClusterDto
{
    /// <summary>Center latitude of the cluster.</summary>
    public double Latitude { get; init; }

    /// <summary>Center longitude of the cluster.</summary>
    public double Longitude { get; init; }

    /// <summary>Number of photos in this cluster.</summary>
    public int PhotoCount { get; init; }

    /// <summary>Representative photo ID for the cluster thumbnail.</summary>
    public Guid? RepresentativePhotoId { get; init; }

    /// <summary>Bounding box radius in metres.</summary>
    public double RadiusMetres { get; init; }
}

// ── Share DTOs ──────────────────────────────────────────────────────

/// <summary>
/// Represents a photo or album share.
/// </summary>
public sealed record PhotoShareDto
{
    /// <summary>Unique share ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Photo ID being shared (null if album share).</summary>
    public Guid? PhotoId { get; init; }

    /// <summary>Album ID being shared (null if photo share).</summary>
    public Guid? AlbumId { get; init; }

    /// <summary>User this is shared with (null for public link).</summary>
    public Guid? SharedWithUserId { get; init; }

    /// <summary>Permission level for the share.</summary>
    public required PhotoSharePermission Permission { get; init; }

    /// <summary>When the share was created.</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>When the share expires (null for permanent).</summary>
    public DateTime? ExpiresAt { get; init; }
}

/// <summary>
/// Permission levels for photo/album sharing.
/// </summary>
public enum PhotoSharePermission
{
    /// <summary>View only.</summary>
    ReadOnly,

    /// <summary>View and download.</summary>
    Download,

    /// <summary>View, download, and add photos (album shares only).</summary>
    Contribute
}

// ── Slideshow DTOs ──────────────────────────────────────────────────

/// <summary>
/// Slideshow configuration.
/// </summary>
public sealed record SlideshowDto
{
    /// <summary>Unique slideshow ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Album or photo selection this slideshow is based on.</summary>
    public Guid? AlbumId { get; init; }

    /// <summary>Ordered photo IDs in the slideshow.</summary>
    public required IReadOnlyList<Guid> PhotoIds { get; init; }

    /// <summary>Auto-play interval in seconds.</summary>
    public int IntervalSeconds { get; init; } = 5;

    /// <summary>Transition type between slides.</summary>
    public SlideshowTransition Transition { get; init; } = SlideshowTransition.Fade;
}

/// <summary>
/// Slideshow transition types.
/// </summary>
public enum SlideshowTransition
{
    /// <summary>No transition.</summary>
    None,

    /// <summary>Fade in/out.</summary>
    Fade,

    /// <summary>Slide horizontally.</summary>
    SlideHorizontal,

    /// <summary>Slide vertically.</summary>
    SlideVertical
}
