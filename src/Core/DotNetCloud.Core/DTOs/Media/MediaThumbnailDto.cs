namespace DotNetCloud.Core.DTOs.Media;

/// <summary>
/// Available thumbnail sizes for media preview images.
/// Values correspond to the maximum dimension (width or height) in pixels.
/// </summary>
public enum MediaThumbnailSize
{
    /// <summary>Small icon thumbnail (128 × 128 px).</summary>
    Small = 128,

    /// <summary>Grid thumbnail (300 × 300 px) — used in photo galleries and album art.</summary>
    Grid = 300,

    /// <summary>Medium detail thumbnail (512 × 512 px).</summary>
    Medium = 512,

    /// <summary>Large detail view (1200 px on longest edge).</summary>
    Large = 1200
}

/// <summary>
/// Represents a generated thumbnail for a media item.
/// </summary>
public sealed record MediaThumbnailDto
{
    /// <summary>
    /// The <c>FileNode</c> ID this thumbnail belongs to.
    /// </summary>
    public required Guid FileNodeId { get; init; }

    /// <summary>
    /// The thumbnail size that was generated.
    /// </summary>
    public required MediaThumbnailSize Size { get; init; }

    /// <summary>
    /// MIME type of the thumbnail image (typically "image/jpeg").
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// Width of the thumbnail in pixels.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Height of the thumbnail in pixels.
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Relative URL or path to retrieve the thumbnail.
    /// </summary>
    public string? Url { get; init; }
}
