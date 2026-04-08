namespace DotNetCloud.Modules.Photos.Services;

/// <summary>
/// Photo-specific thumbnail sizes, extending the base Files thumbnail pattern
/// with dimensions optimized for photo gallery display.
/// </summary>
public enum PhotoThumbnailSize
{
    /// <summary>Grid thumbnail for gallery view (300 × 300 px).</summary>
    Grid = 300,

    /// <summary>Detail thumbnail for photo detail / lightbox preview (1200 × 1200 px).</summary>
    Detail = 1200,

    /// <summary>Full resolution — returns the original image without resizing.</summary>
    Full = 0
}

/// <summary>
/// Generates, caches, and retrieves photo-specific thumbnails at sizes optimized
/// for gallery display: grid (300px), detail (1200px), and full resolution.
/// </summary>
public interface IPhotoThumbnailService
{
    /// <summary>
    /// Returns a JPEG thumbnail stream for the given photo at the requested size,
    /// or <c>null</c> if no cached thumbnail exists or the photo is not found.
    /// </summary>
    /// <param name="photoId">The photo record ID.</param>
    /// <param name="size">Desired thumbnail dimension.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A tuple containing the image stream and MIME type (<c>"image/jpeg"</c>),
    /// or <c>(null, null)</c> when unavailable.
    /// </returns>
    Task<(Stream? Data, string? ContentType)> GetThumbnailAsync(
        Guid photoId,
        PhotoThumbnailSize size,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates and caches all photo thumbnail sizes (grid + detail) for a newly indexed photo.
    /// Should be called after Photo record creation or when thumbnails need regeneration.
    /// Full size is not cached — it reads the original on demand.
    /// </summary>
    /// <param name="photoId">The photo record ID.</param>
    /// <param name="storagePath">Absolute path of the image file on disk.</param>
    /// <param name="mimeType">MIME type of the image (e.g. <c>"image/jpeg"</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task GenerateThumbnailsAsync(
        Guid photoId,
        string storagePath,
        string mimeType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all cached thumbnails for the specified photo.
    /// Should be called when a photo is permanently deleted.
    /// </summary>
    /// <param name="photoId">The photo whose thumbnails should be purged.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteThumbnailsAsync(Guid photoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the current edit stack to the original image and regenerates
    /// both grid and detail thumbnails with the edits baked in.
    /// The original file on disk is NOT modified (non-destructive).
    /// </summary>
    /// <param name="photoId">The photo record ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if thumbnails were successfully regenerated; <c>false</c> otherwise.</returns>
    Task<bool> SaveEditsAsync(Guid photoId, CancellationToken cancellationToken = default);
}
