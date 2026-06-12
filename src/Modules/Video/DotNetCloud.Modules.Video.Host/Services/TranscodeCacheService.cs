using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DotNetCloud.Modules.Video.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Host.Services;

/// <summary>
/// Content-addressed cache for transcoded video outputs.
///
/// Cache key = SHA256(source file path + JSON of transcode parameters).
/// This means the same file transcoded with the same settings always produces
/// the same cache key, regardless of when or by whom it was requested.
///
/// Thread-safe. Registered as singleton.
/// </summary>
public sealed class TranscodeCacheService
{
    private readonly VideoTranscodingOptions _options;
    private readonly ILogger<TranscodeCacheService> _logger;
    private readonly string _cacheRoot;

    // Prevents concurrent transcode of the same cache key
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TranscodeCacheService"/> class.
    /// </summary>
    public TranscodeCacheService(
        VideoTranscodingOptions options,
        ILogger<TranscodeCacheService> logger)
    {
        _options = options;
        _logger = logger;

        // Use the configured temp directory, or fall back to a subfolder of the system temp
        _cacheRoot = !string.IsNullOrWhiteSpace(options.TempDirectory)
            ? Path.Combine(options.TempDirectory, "transcode-cache")
            : Path.Combine(Path.GetTempPath(), "dotnetcloud-transcode-cache");

        Directory.CreateDirectory(_cacheRoot);
    }

    /// <summary>
    /// Computes the cache key for a given source file and transcoding options.
    /// </summary>
    public string ComputeCacheKey(string sourceFilePath, VideoTranscodingOptions options)
    {
        var optionsObj = new
        {
            options.VideoCodec,
            options.VideoCrf,
            options.EncoderPreset,
            options.MaxWidth,
            options.MaxHeight,
            options.AudioCodec,
            options.AudioBitrateKbps
        };
        var optionsJson = JsonSerializer.Serialize(optionsObj);

        var input = sourceFilePath + "|" + optionsJson;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Returns the cached output file path if it exists and is not expired.
    /// Returns null on cache miss.
    /// </summary>
    public string? GetCachedPath(string cacheKey)
    {
        var path = GetCacheFilePath(cacheKey);
        if (!File.Exists(path))
            return null;

        // Check TTL expiration
        if (_options.CacheTtlHours > 0)
        {
            var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(path);
            if (age.TotalHours > _options.CacheTtlHours)
            {
                _logger.LogDebug("Cache entry expired: {CacheKey}", cacheKey);
                TryDelete(path);
                return null;
            }
        }

        _logger.LogDebug("Cache hit: {CacheKey} -> {Path}", cacheKey, path);
        return path;
    }

    /// <summary>
    /// Acquires an exclusive lock for a given cache key.
    /// Prevents multiple concurrent transcode processes for the same output.
    /// Caller MUST dispose the returned IDisposable to release the lock.
    /// </summary>
    public async Task<IDisposable> LockCacheKeyAsync(string cacheKey, CancellationToken ct = default)
    {
        var semaphore = _keyLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);
        return new SemaphoreReleaser(semaphore, cacheKey, _keyLocks);
    }

    /// <summary>
    /// Registers a successfully transcoded file in the cache.
    /// Moves (or copies) the source file into the cache directory.
    /// </summary>
    public void RegisterCachedFile(string cacheKey, string sourcePath)
    {
        var cachePath = GetCacheFilePath(cacheKey);
        if (!File.Exists(sourcePath))
        {
            _logger.LogWarning("Cannot register cache entry — source file missing: {Path}", sourcePath);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);

        // Move (or copy if move fails) the file to cache location
        try
        {
            File.Move(sourcePath, cachePath, overwrite: true);
        }
        catch (IOException)
        {
            File.Copy(sourcePath, cachePath, overwrite: true);
            TryDelete(sourcePath);
        }

        _logger.LogInformation("Cache entry created: {CacheKey} -> {Path}", cacheKey, cachePath);

        // Trigger cleanup if cache is too large
        _ = Task.Run(() => EnforceMaxSizeAsync());
    }

    /// <summary>
    /// Returns the file system path where a cache entry with the given key should be stored.
    /// </summary>
    public string GetCacheFilePath(string cacheKey)
    {
        // Use subdirectories based on first 4 chars to avoid too many files in one dir
        var subDir = cacheKey[..Math.Min(4, cacheKey.Length)];
        return Path.Combine(_cacheRoot, subDir, cacheKey + ".mp4");
    }

    /// <summary>
    /// Deletes old cache entries if total size exceeds MaxCacheSizeBytes.
    /// Oldest entries are deleted first (LRU-like eviction).
    /// </summary>
    private async Task EnforceMaxSizeAsync()
    {
        if (_options.MaxCacheSizeBytes <= 0)
            return;

        try
        {
            var dirInfo = new DirectoryInfo(_cacheRoot);
            if (!dirInfo.Exists)
                return;

            var files = dirInfo.GetFiles("*.mp4", SearchOption.AllDirectories)
                .OrderBy(f => f.LastWriteTimeUtc)
                .ToList();

            long totalSize = files.Sum(f => f.Length);

            while (totalSize > _options.MaxCacheSizeBytes && files.Count > 0)
            {
                var oldest = files[0];
                totalSize -= oldest.Length;
                _logger.LogDebug("Cache eviction: {Path} ({Size} bytes)", oldest.FullName, oldest.Length);
                TryDelete(oldest.FullName);
                files.RemoveAt(0);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during transcode cache size enforcement");
        }
    }

    private static void TryDelete(string path)
    {
        try
        { File.Delete(path); }
        catch { /* best effort */ }
    }

    private sealed class SemaphoreReleaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly string _key;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks;
        private bool _disposed;

        public SemaphoreReleaser(
            SemaphoreSlim semaphore,
            string key,
            ConcurrentDictionary<string, SemaphoreSlim> keyLocks)
        {
            _semaphore = semaphore;
            _key = key;
            _keyLocks = keyLocks;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _semaphore.Release();
        }
    }
}
