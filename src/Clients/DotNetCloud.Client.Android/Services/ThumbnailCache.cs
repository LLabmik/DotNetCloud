using System.Collections.Concurrent;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// <see cref="IThumbnailCache"/> implementation with two-tier caching: in-memory LRU (100 entries) + disk.
/// Downloads thumbnails from <c>/api/v1/files/{fileNodeId}/thumbnail?size=small</c> on first access.
/// </summary>
internal sealed class ThumbnailCache : IThumbnailCache
{
    private const int MaxMemoryEntries = 100;
    private readonly ConcurrentDictionary<Guid, CachedEntry> _memory = new();
    private readonly HttpClient _http;
    private readonly ILogger<ThumbnailCache> _logger;
    private readonly string _diskDir;

    /// <summary>Initializes a new <see cref="ThumbnailCache"/>.</summary>
    public ThumbnailCache(HttpClient http, ILogger<ThumbnailCache> logger)
    {
        _http = http;
        _logger = logger;
        _diskDir = GetCacheDirectory();
        Directory.CreateDirectory(_diskDir);
    }

    private static string GetCacheDirectory()
    {
        try
        {
            return Path.Combine(FileSystem.CacheDirectory, "thumbnails");
        }
        catch (NotImplementedException)
        {
            // Portable assembly fallback (test context) — use temp directory
            return Path.Combine(Path.GetTempPath(), "thumbnails");
        }
    }

    /// <inheritdoc />
    public async Task<ImageSource?> GetThumbnailAsync(
        Guid fileNodeId,
        string serverBaseUrl,
        string accessToken,
        CancellationToken ct = default)
    {
        // 1. Memory hit
        if (_memory.TryGetValue(fileNodeId, out var entry))
        {
            entry.LastAccess = DateTime.UtcNow;
            return entry.Source;
        }

        // 2. Disk hit
        var diskPath = GetDiskPath(fileNodeId);
        if (File.Exists(diskPath))
        {
            // Use FromStream — FromFile does not work reliably with absolute paths
            // on Android's CacheDirectory (it looks in app resources/assets).
            var src = ImageSource.FromStream(() => File.OpenRead(diskPath));
            AddToMemory(fileNodeId, src);
            return src;
        }

        // 3. Download thumbnail from server
        try
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
            var baseUrl = serverBaseUrl.TrimEnd('/');
            var url = $"{baseUrl}/api/v1/files/{fileNodeId}/thumbnail?size=small";
            var bytes = await _http.GetByteArrayAsync(url, ct).ConfigureAwait(false);
            if (bytes.Length == 0)
                return null;
            await File.WriteAllBytesAsync(diskPath, bytes, ct).ConfigureAwait(false);
            // Use FromStream — FromFile does not work reliably with absolute paths
            // on Android's CacheDirectory (it looks in app resources/assets).
            var source = ImageSource.FromStream(() => new MemoryStream(bytes));
            AddToMemory(fileNodeId, source);
            return source;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to download thumbnail for {FileNodeId}", fileNodeId);
            return null;
        }
    }

    /// <inheritdoc />
    public void Invalidate(Guid fileNodeId)
    {
        _memory.TryRemove(fileNodeId, out _);
        try
        { File.Delete(GetDiskPath(fileNodeId)); }
        catch { }
    }

    /// <inheritdoc />
    public void Clear()
    {
        _memory.Clear();
        try
        {
            Directory.Delete(_diskDir, true);
            Directory.CreateDirectory(_diskDir);
        }
        catch { }
    }

    private string GetDiskPath(Guid fileNodeId) =>
        Path.Combine(_diskDir, $"{fileNodeId:N}.jpg");

    private void AddToMemory(Guid key, ImageSource source)
    {
        if (_memory.Count >= MaxMemoryEntries)
        {
            // Evict oldest entry
            var oldest = _memory.MinBy(kvp => kvp.Value.LastAccess);
            if (oldest.Key != default)
                _memory.TryRemove(oldest.Key, out _);
        }

        _memory[key] = new CachedEntry(source);
    }

    private sealed class CachedEntry(ImageSource source)
    {
        public ImageSource Source { get; } = source;
        public DateTime LastAccess { get; set; } = DateTime.UtcNow;
    }
}
