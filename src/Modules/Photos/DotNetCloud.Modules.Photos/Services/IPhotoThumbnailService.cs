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
/// <remarks>
/// A sandboxed module cannot read the user's files by path: the original bytes are fetched through
/// the core's module API (<c>IFileDirectory</c>) using the caller's own signed assertion. Every
/// generation path therefore takes the acting user, and only the photo's owner can regenerate its
/// thumbnails — a photo merely shared with the caller is served from the cache the owner produced.
/// </remarks>
public interface IPhotoThumbnailService
{
    /// <summary>
    /// Returns a JPEG thumbnail stream for the given photo at the requested size, generating it from
    /// the original through the core when the cache is empty.
    /// </summary>
    /// <param name="photoId">The photo record ID.</param>
    /// <param name="actingUserId">The verified caller; only the photo's owner may generate.</param>
    /// <param name="size">Desired thumbnail dimension.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A tuple containing the image stream and MIME type (<c>"image/jpeg"</c>),
    /// or <c>(null, null)</c> when unavailable.
    /// </returns>
    Task<(Stream? Data, string? ContentType)> GetThumbnailAsync(
        Guid photoId,
        Guid actingUserId,
        PhotoThumbnailSize size,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates and caches both photo thumbnail sizes (grid + detail) for a photo the acting user
    /// owns. Full size is not cached — the original is served on demand.
    /// </summary>
    /// <param name="photoId">The photo record ID.</param>
    /// <param name="actingUserId">The verified caller; a non-owner is refused.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when both sizes are present afterwards; <c>false</c> otherwise.</returns>
    Task<bool> GenerateThumbnailsAsync(
        Guid photoId,
        Guid actingUserId,
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
    /// The original file is NOT modified (non-destructive).
    /// </summary>
    /// <param name="photoId">The photo record ID.</param>
    /// <param name="actingUserId">The verified caller; a non-owner is refused.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if thumbnails were successfully regenerated; <c>false</c> otherwise.</returns>
    Task<bool> SaveEditsAsync(Guid photoId, Guid actingUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-renders the cached previews from the original, applying whatever is left on the edit stack.
    /// </summary>
    /// <remarks>
    /// Used after an undo or a revert-all, where the stack shrank but the previews were already baked
    /// by an earlier save. An empty stack renders the unedited original, so "revert all" really does
    /// look reverted. Same work as <see cref="SaveEditsAsync"/>; the separate name marks the intent at
    /// the call site.
    /// </remarks>
    /// <param name="photoId">The photo record ID.</param>
    /// <param name="actingUserId">The verified caller; a non-owner is refused.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the previews were rebuilt; <c>false</c> otherwise.</returns>
    Task<bool> RegeneratePreviewsAsync(Guid photoId, Guid actingUserId, CancellationToken cancellationToken = default);
}
