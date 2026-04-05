namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Provides read-only access to videos and collections for cross-module references.
/// Modules use this capability to resolve video item links without direct data access.
/// </summary>
/// <remarks>
/// <para>
/// <b>Capability tier:</b> Public — automatically granted to all modules.
/// </para>
/// <para>
/// This capability exposes a minimal read-only view of video data. Modules that
/// need to create or modify videos must use the Video module API directly.
/// </para>
/// <para>
/// <b>Optional module:</b> The Video module may not be installed. Callers should
/// handle null/empty results gracefully.
/// </para>
/// </remarks>
public interface IVideoDirectory : ICapabilityInterface
{
    /// <summary>
    /// Gets the title of a video by its ID.
    /// </summary>
    /// <param name="videoId">The video ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The video title if found; otherwise <c>null</c>.</returns>
    Task<string?> GetVideoTitleAsync(Guid videoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets titles for a batch of video IDs.
    /// IDs that do not map to a video are omitted from the result.
    /// </summary>
    /// <param name="videoIds">The video IDs to resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyDictionary<Guid, string>> GetVideoTitlesAsync(
        IEnumerable<Guid> videoIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the title of a collection by its ID.
    /// </summary>
    /// <param name="collectionId">The collection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The collection title if found; otherwise <c>null</c>.</returns>
    Task<string?> GetCollectionTitleAsync(Guid collectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches videos accessible to a user by title (case-insensitive substring match).
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="query">Search query string.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Video IDs and titles matching the query.</returns>
    Task<IReadOnlyList<(Guid VideoId, string Title)>> SearchVideosAsync(
        Guid userId,
        string query,
        int maxResults = 20,
        CancellationToken cancellationToken = default);
}
