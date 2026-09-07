namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Two-tier cache for album art: an in-memory LRU (up to 50 entries) backed by disk.
/// Downloads album art from the server on first access.
/// </summary>
public interface IAlbumArtCache
{
    /// <summary>Gets the album art for the specified album, downloading and caching if needed.</summary>
    Task<ImageSource?> GetAlbumArtAsync(Guid albumId, string serverBaseUrl, string accessToken, CancellationToken ct = default);

    /// <summary>Removes a specific album's art from both memory and disk caches.</summary>
    void Invalidate(Guid albumId);

    /// <summary>Clears the entire cache (memory + disk).</summary>
    void Clear();

    /// <summary>
    /// Drops all in-memory entries so their <see cref="ImageSource"/> references can be
    /// reclaimed. Called from <c>OnTrimMemory</c> under system memory pressure. Disk
    /// contents are retained so the next lookup re-reads from disk instead of re-downloading.
    /// </summary>
    void TrimMemory();
}
