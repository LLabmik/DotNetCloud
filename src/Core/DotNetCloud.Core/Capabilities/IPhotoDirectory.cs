using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Provides read-only access to photos and albums for cross-module references.
/// Modules use this capability to resolve photo/album links without direct data access.
/// </summary>
/// <remarks>
/// <para>
/// <b>Capability tier:</b> Public — automatically granted to all modules.
/// </para>
/// <para>
/// This capability exposes a minimal read-only view of photos and albums. Modules that
/// need to create or modify photos must use the Photos module API directly.
/// </para>
/// <para>
/// <b>Optional module:</b> The Photos module may not be installed. Callers should
/// handle null/empty results gracefully.
/// </para>
/// </remarks>
public interface IPhotoDirectory : ICapabilityInterface
{
    /// <summary>
    /// Gets the title/filename of a photo by its ID.
    /// </summary>
    /// <param name="photoId">The photo ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The photo filename if found; otherwise <c>null</c>.</returns>
    Task<string?> GetPhotoTitleAsync(Guid photoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets titles for a batch of photo IDs.
    /// IDs that do not map to a photo are omitted from the result.
    /// </summary>
    /// <param name="photoIds">The photo IDs to resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyDictionary<Guid, string>> GetPhotoTitlesAsync(
        IEnumerable<Guid> photoIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the title of an album by its ID.
    /// </summary>
    /// <param name="albumId">The album ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The album title if found; otherwise <c>null</c>.</returns>
    Task<string?> GetAlbumTitleAsync(Guid albumId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets titles for a batch of album IDs.
    /// IDs that do not map to an album are omitted from the result.
    /// </summary>
    /// <param name="albumIds">The album IDs to resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyDictionary<Guid, string>> GetAlbumTitlesAsync(
        IEnumerable<Guid> albumIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches photos accessible to a user by filename (case-insensitive substring match).
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="query">Search query string.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Photo IDs and filenames matching the query.</returns>
    Task<IReadOnlyList<(Guid PhotoId, string FileName)>> SearchPhotosAsync(
        Guid userId,
        string query,
        int maxResults = 20,
        CancellationToken cancellationToken = default);
}
