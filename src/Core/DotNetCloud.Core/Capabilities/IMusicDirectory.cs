namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Provides read-only access to artists, albums, and tracks for cross-module references.
/// Modules use this capability to resolve music item links without direct data access.
/// </summary>
/// <remarks>
/// <para>
/// <b>Capability tier:</b> Public — automatically granted to all modules.
/// </para>
/// <para>
/// This capability exposes a minimal read-only view of music data. Modules that
/// need to create or modify music items must use the Music module API directly.
/// </para>
/// <para>
/// <b>Optional module:</b> The Music module may not be installed. Callers should
/// handle null/empty results gracefully.
/// </para>
/// </remarks>
public interface IMusicDirectory : ICapabilityInterface
{
    /// <summary>
    /// Gets the name of an artist by their ID.
    /// </summary>
    /// <param name="artistId">The artist ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The artist name if found; otherwise <c>null</c>.</returns>
    Task<string?> GetArtistNameAsync(Guid artistId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets names for a batch of artist IDs.
    /// IDs that do not map to an artist are omitted from the result.
    /// </summary>
    /// <param name="artistIds">The artist IDs to resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyDictionary<Guid, string>> GetArtistNamesAsync(
        IEnumerable<Guid> artistIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the title of an album by its ID.
    /// </summary>
    /// <param name="albumId">The album ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The album title if found; otherwise <c>null</c>.</returns>
    Task<string?> GetAlbumTitleAsync(Guid albumId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the title of a track by its ID.
    /// </summary>
    /// <param name="trackId">The track ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The track title if found; otherwise <c>null</c>.</returns>
    Task<string?> GetTrackTitleAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches tracks accessible to a user by title (case-insensitive substring match).
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="query">Search query string.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Track IDs and titles matching the query.</returns>
    Task<IReadOnlyList<(Guid TrackId, string Title)>> SearchTracksAsync(
        Guid userId,
        string query,
        int maxResults = 20,
        CancellationToken cancellationToken = default);
}
