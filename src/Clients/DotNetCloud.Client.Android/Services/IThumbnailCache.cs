namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Two-tier cache for file thumbnails: in-memory LRU backed by disk.
/// Downloads thumbnails from <c>/api/v1/files/{fileNodeId}/thumbnail?size=small</c>
/// on first access using the <see cref="HttpClient"/> provided at construction.
/// </summary>
public interface IThumbnailCache
{
    /// <summary>
    /// Gets the thumbnail for the specified file node, downloading and caching if needed.
    /// </summary>
    /// <param name="fileNodeId">The file node ID.</param>
    /// <param name="serverBaseUrl">Base URL of the DotNetCloud server.</param>
    /// <param name="accessToken">Bearer access token for authentication.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="ImageSource"/> for display, or <c>null</c> on failure.</returns>
    Task<ImageSource?> GetThumbnailAsync(
        Guid fileNodeId,
        string serverBaseUrl,
        string accessToken,
        CancellationToken ct = default);

    /// <summary>Removes a specific file's thumbnail from both memory and disk caches.</summary>
    void Invalidate(Guid fileNodeId);

    /// <summary>Clears the entire cache (memory + disk).</summary>
    void Clear();
}
