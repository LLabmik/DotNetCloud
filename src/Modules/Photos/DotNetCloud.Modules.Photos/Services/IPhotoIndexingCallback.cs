namespace DotNetCloud.Modules.Photos.Services;

/// <summary>
/// Callback interface for photo indexing — implemented in the Data layer, injected via DI.
/// Avoids a direct dependency from the Module layer to the Data layer.
/// </summary>
public interface IPhotoIndexingCallback
{
    /// <summary>Records an image node as a Photo in the user's library (idempotent).</summary>
    /// <param name="fileNodeId">The Files-module node ID.</param>
    /// <param name="fileName">Display file name.</param>
    /// <param name="mimeType">MIME type of the image.</param>
    /// <param name="sizeBytes">File size in bytes.</param>
    /// <param name="ownerId">Owner user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task IndexPhotoAsync(Guid fileNodeId, string fileName, string mimeType, long sizeBytes, Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the set of FileNode IDs already indexed in the photo library for the given owner.
    /// Used by the scanner to skip files that have not changed since the last scan.
    /// </summary>
    /// <param name="ownerId">Owner user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HashSet<Guid>> GetIndexedFileNodeIdsAsync(Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes Photo records whose source FileNodes no longer exist in the Files module.
    /// Also removes related junction data (album membership, tags, shares, edit records).
    /// </summary>
    /// <param name="deletedFileNodeIds">FileNode IDs whose backing files have been deleted.</param>
    /// <param name="ownerId">Owner user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of photos removed.</returns>
    Task<int> RemoveDeletedPhotosAsync(IReadOnlyCollection<Guid> deletedFileNodeIds, Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all photo library metadata for a specific owner from the database (photos, albums, metadata, tags,
    /// shares, edit records). The actual image files are NOT affected.
    /// Other users' libraries are never touched.
    /// </summary>
    /// <param name="ownerId">Owner whose library will be reset.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ResetCollectionAsync(Guid ownerId, CancellationToken cancellationToken = default);
}
