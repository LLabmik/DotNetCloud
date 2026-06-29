using System.Collections.Concurrent;
using System.Net.Http.Headers;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// <see cref="IAlbumArtCache"/> implementation with two-tier caching: in-memory LRU (50 entries) + disk.
/// Downloads album art from <c>/api/v1/music/albums/{albumId}/cover</c> on first access.
/// </summary>
internal sealed class AlbumArtCache : IAlbumArtCache
{
    private const int MaxMemoryEntries = 50;
    private readonly ConcurrentDictionary<Guid, CachedEntry> _memory = new();
    private readonly HttpClient _http;
    private readonly string _diskDir;

    /// <summary>Initializes a new <see cref="AlbumArtCache"/>.</summary>
    public AlbumArtCache(HttpClient http)
    {
        _http = http;
        _diskDir = GetCacheDirectory();
        Directory.CreateDirectory(_diskDir);
    }

    private static string GetCacheDirectory()
    {
        try
        {
            return Path.Combine(FileSystem.CacheDirectory, "albumart");
        }
        catch (NotImplementedException)
        {
            // Portable assembly fallback (test context) — use temp directory
            return Path.Combine(Path.GetTempPath(), "albumart");
        }
    }

    /// <inheritdoc />
    public async Task<ImageSource?> GetAlbumArtAsync(
        Guid albumId, string serverBaseUrl, string accessToken, CancellationToken ct = default)
    {
        // 1. Memory hit
        if (_memory.TryGetValue(albumId, out var entry))
        {
            entry.LastAccess = DateTime.UtcNow;
            return entry.Source;
        }

        // 2. Disk hit
        var diskPath = GetDiskPath(albumId);
        if (File.Exists(diskPath))
        {
            var src = ImageSource.FromFile(diskPath);
            AddToMemory(albumId, src);
            return src;
        }

        // 3. Download
        try
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
            var url = $"{serverBaseUrl.TrimEnd('/')}/api/v1/music/albums/{albumId}/cover";
            var bytes = await _http.GetByteArrayAsync(url, ct).ConfigureAwait(false);
            await File.WriteAllBytesAsync(diskPath, bytes, ct).ConfigureAwait(false);
            var source = ImageSource.FromStream(() => new MemoryStream(bytes));
            AddToMemory(albumId, source);
            return source;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void Invalidate(Guid albumId)
    {
        _memory.TryRemove(albumId, out _);
        try { File.Delete(GetDiskPath(albumId)); } catch { }
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

    private string GetDiskPath(Guid albumId) =>
        Path.Combine(_diskDir, $"{albumId:N}.jpg");

    private void AddToMemory(Guid key, ImageSource source)
    {
        if (_memory.Count >= MaxMemoryEntries)
        {
            var lru = _memory.OrderBy(kvp => kvp.Value.LastAccess).First();
            _memory.TryRemove(lru.Key, out _);
        }

        _memory[key] = new CachedEntry(source, DateTime.UtcNow);
    }

    private sealed class CachedEntry(ImageSource source, DateTime lastAccess)
    {
        public ImageSource Source { get; } = source;
        public DateTime LastAccess { get; set; } = lastAccess;
    }
}
