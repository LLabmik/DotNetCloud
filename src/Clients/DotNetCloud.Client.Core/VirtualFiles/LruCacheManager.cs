// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Core.VirtualFiles;

/// <summary>
/// LRU (Least-Recently-Used) content chunk cache for local file content.
/// Used by the Linux FUSE filesystem to cache recently-accessed file chunks.
/// Pinned files are exempt from eviction.
/// </summary>
public sealed class LruCacheManager : IDisposable
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ReaderWriterLockSlim _evictionLock = new();
    private long _currentSizeBytes;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="LruCacheManager"/>.
    /// </summary>
    /// <param name="settings">Virtual file settings providing the max cache size.</param>
    /// <param name="logger">Logger.</param>
    public LruCacheManager(
        VirtualFileSettings settings,
        ILogger<LruCacheManager> logger)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the virtual file settings.
    /// </summary>
    public VirtualFileSettings Settings { get; }

    private ILogger<LruCacheManager> Logger { get; }

    /// <summary>
    /// Gets the current total size of cached content in bytes.
    /// </summary>
    public long CurrentSizeBytes => Interlocked.Read(ref _currentSizeBytes);

    /// <summary>
    /// Gets the number of entries currently in the cache.
    /// </summary>
    public int EntryCount => _cache.Count;

    /// <summary>
    /// Gets or sets the content data for a file path.
    /// If adding data would exceed the cache limit, older entries are evicted first.
    /// Pinned file entries are never evicted.
    /// When setting, use null to remove the entry.
    /// </summary>
    /// <returns>The cached data, or null if not found.</returns>
    public byte[]? this[string localPath]
    {
        get
        {
            if (_cache.TryGetValue(localPath, out var entry))
            {
                entry.LastAccess = DateTime.UtcNow;
                return entry.Data;
            }
            return null;
        }
        set
        {
            if (value == null)
            {
                Remove(localPath);
                return;
            }

            var newEntry = new CacheEntry(value, Settings.PinList.Contains(localPath));
            var oldEntry = _cache.AddOrUpdate(
                localPath,
                _ =>
                {
                    Interlocked.Add(ref _currentSizeBytes, value.Length);
                    return newEntry;
                },
                (_, existing) =>
                {
                    // Replace existing entry
                    Interlocked.Add(ref _currentSizeBytes, value.Length - existing.Data.Length);
                    return newEntry;
                });

            if (oldEntry != null && !ReferenceEquals(oldEntry, newEntry))
            {
                // If our factory delegate was NOT the one that won, we need
                // to undo our size tracking (the winning factory handled it).
                if (_cache.TryGetValue(localPath, out var winner) && !ReferenceEquals(winner, newEntry))
                {
                    Interlocked.Add(ref _currentSizeBytes, -value.Length);
                }
            }

            EvictIfNeeded();
        }
    }

    /// <summary>
    /// Attempts to get cached data for a file path without updating the LRU timestamp.
    /// </summary>
    /// <param name="localPath">Full local path to the file.</param>
    /// <param name="data">When returned, the cached data if found; otherwise null.</param>
    /// <returns>True if the data was found in the cache.</returns>
    public bool TryPeek(string localPath, out byte[]? data)
    {
        if (_cache.TryGetValue(localPath, out var entry))
        {
            data = entry.Data;
            return true;
        }

        data = null;
        return false;
    }

    /// <summary>
    /// Removes a file's content from the cache.
    /// </summary>
    /// <param name="localPath">Full local path to remove.</param>
    /// <returns>True if the entry was removed.</returns>
    public bool Remove(string localPath)
    {
        if (_cache.TryRemove(localPath, out var entry))
        {
            Interlocked.Add(ref _currentSizeBytes, -entry.Data.Length);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Clears all entries from the cache.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        Interlocked.Exchange(ref _currentSizeBytes, 0);
    }

    /// <summary>
    /// Evicts entries from the cache until the total size is below the maximum,
    /// or until only pinned entries remain. Eviction order is LRU.
    /// </summary>
    /// <returns>The number of entries evicted.</returns>
    public int EvictIfNeeded()
    {
        if (Settings.MaxCacheSizeBytes <= 0)
            return 0;

        if (Interlocked.Read(ref _currentSizeBytes) <= Settings.MaxCacheSizeBytes)
            return 0;

        int evicted = 0;

        _evictionLock.EnterWriteLock();
        try
        {
            // Re-check inside lock in case another thread already evicted
            while (Interlocked.Read(ref _currentSizeBytes) > Settings.MaxCacheSizeBytes)
            {
                // Find the least-recently-used unpinned entry
                CacheEntry? lruEntry = null;
                string? lruKey = null;

                foreach (var kvp in _cache)
                {
                    if (kvp.Value.IsPinned)
                        continue;

                    if (lruEntry == null || kvp.Value.LastAccess < lruEntry.LastAccess)
                    {
                        lruEntry = kvp.Value;
                        lruKey = kvp.Key;
                    }
                }

                if (lruKey == null)
                    break; // Only pinned entries remain; can't evict further

                _cache.TryRemove(lruKey, out _);
                Interlocked.Add(ref _currentSizeBytes, -lruEntry!.Data.Length);
                evicted++;

                Logger.LogTrace("Evicted cache entry for {Path} ({Size} bytes)", lruKey, lruEntry.Data.Length);
            }
        }
        finally
        {
            _evictionLock.ExitWriteLock();
        }

        if (evicted > 0)
        {
            Logger.LogDebug(
                "Evicted {Count} entries from cache. Current size: {Size} bytes",
                evicted, Interlocked.Read(ref _currentSizeBytes));
        }

        return evicted;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _evictionLock.Dispose();
            _cache.Clear();
            Interlocked.Exchange(ref _currentSizeBytes, 0);
        }
    }

    /// <summary>
    /// A cache entry holding file content data and metadata.
    /// </summary>
    private sealed class CacheEntry
    {
        public CacheEntry(byte[] data, bool isPinned)
        {
            Data = data;
            IsPinned = isPinned;
            LastAccess = DateTime.UtcNow;
        }

        public byte[] Data { get; }

        public bool IsPinned { get; }

        public DateTime LastAccess { get; set; }
    }
}
