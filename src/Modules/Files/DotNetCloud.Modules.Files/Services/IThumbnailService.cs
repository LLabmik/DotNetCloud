namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Available thumbnail sizes for generated preview images.
/// </summary>
public enum ThumbnailSize
{
    /// <summary>Small icon thumbnail (128 × 128 px).</summary>
    Small = 128,

    /// <summary>Medium grid thumbnail (256 × 256 px).</summary>
    Medium = 256,

    /// <summary>Large detail thumbnail (512 × 512 px).</summary>
    Large = 512
}

/// <summary>
/// Generates, caches, and retrieves file thumbnails for supported media types.
/// Supports raster image formats (JPEG, PNG, GIF, WebP, BMP, TIFF) and
/// video first-frame extraction for common video MIME types, plus
/// PDF first-page rendering.
/// </summary>
public interface IThumbnailService
{
    /// <summary>
    /// Returns a JPEG thumbnail stream for the given file node at the requested size,
    /// or <c>null</c> if no cached thumbnail exists or the file type is unsupported.
    /// </summary>
    /// <param name="fileNodeId">The file node to retrieve a thumbnail for.</param>
    /// <param name="size">Desired thumbnail dimension.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A tuple containing the image stream and MIME type (<c>"image/jpeg"</c>),
    /// or <c>(null, null)</c> when unavailable.
    /// </returns>
    Task<(Stream? Data, string? ContentType)> GetThumbnailAsync(
        Guid fileNodeId,
        ThumbnailSize size,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates and caches all thumbnail sizes for a newly uploaded file.
    /// Supported image and video MIME types are processed; other types are silently skipped.
    /// Should be called after a chunked upload session completes.
    /// </summary>
    /// <param name="fileNodeId">The newly created file node ID.</param>
    /// <param name="storagePath">Absolute path of the assembled file on disk.</param>
    /// <param name="mimeType">MIME type of the file (e.g. <c>"image/jpeg"</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task GenerateThumbnailAsync(
        Guid fileNodeId,
        string storagePath,
        string mimeType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all cached thumbnails for the specified file node.
    /// Should be called when a file is permanently deleted.
    /// </summary>
    /// <param name="fileNodeId">The file node whose thumbnails should be purged.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteThumbnailsAsync(Guid fileNodeId, CancellationToken cancellationToken = default);
}
