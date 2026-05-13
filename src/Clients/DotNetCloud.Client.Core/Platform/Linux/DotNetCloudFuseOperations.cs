// Licensed under the Apache License, Version 2.0.

#if !WINDOWS_BUILD

#pragma warning disable CA1416 // PosixResult is only supported on Linux — file is conditionally compiled

using System.Collections.Concurrent;
using System.Text;
using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.LocalState;
using DotNetCloud.Client.Core.Sync;
using DotNetCloud.Client.Core.Transfer;
using DotNetCloud.Client.Core.VirtualFiles;
using FuseDotNet;
using LTRData.Extensions.Native.Memory;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Core.Platform.Linux;

/// <summary>
/// FUSE operations implementation for the DotNetCloud virtual file system.
/// Maps FUSE kernel callbacks to queries against <see cref="ILocalStateDb"/>
/// and content hydration via <see cref="IChunkedTransferClient"/>.
/// </summary>
public sealed class DotNetCloudFuseOperations : IFuseOperations
{
    private readonly ILocalStateDb _localStateDb;
    private readonly IChunkedTransferClient _chunkedTransfer;
    private readonly VirtualFileSettings _settings;
    private readonly LruCacheManager _cache;
    private readonly ILogger<DotNetCloudFuseOperations> _logger;

    // Tracks files currently being hydrated to avoid duplicate downloads.
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _hydrationLocks = new(StringComparer.OrdinalIgnoreCase);

    // Base path of the sync folder (used to compute relative paths).
    private string _syncRootPath = string.Empty;

    // Database path for this context.
    private string _dbPath = string.Empty;

    /// <summary>
    /// Initializes a new instance of <see cref="DotNetCloudFuseOperations"/>.
    /// </summary>
    public DotNetCloudFuseOperations(
        ILocalStateDb localStateDb,
        IChunkedTransferClient chunkedTransfer,
        VirtualFileSettings settings,
        LruCacheManager cache,
        ILogger<DotNetCloudFuseOperations> logger)
    {
        _localStateDb = localStateDb ?? throw new ArgumentNullException(nameof(localStateDb));
        _chunkedTransfer = chunkedTransfer ?? throw new ArgumentNullException(nameof(chunkedTransfer));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Sets the context paths used by this operations instance.
    /// Called once during mount initialization.
    /// </summary>
    public void SetContextPaths(string syncRootPath, string dbPath)
    {
        _syncRootPath = syncRootPath;
        _dbPath = dbPath;
    }

    /// <summary>
    /// Gets the sync root path.
    /// </summary>
    public string SyncRootPath => _syncRootPath;

    /// <inheritdoc/>
    public void Init(ref FuseConnInfo fuseConnInfo)
    {
        _logger.LogInformation("FUSE filesystem initialized (protocol {Major}.{Minor})",
            fuseConnInfo.proto_major, fuseConnInfo.proto_minor);
    }

    /// <inheritdoc/>
    public PosixResult GetAttr(ReadOnlyNativeMemory<byte> fileNamePtr, out FuseFileStat stat, ref FuseFileInfo fileInfo)
    {
        var path = GetPath(fileNamePtr);

        try
        {
            var tsNow = TimeSpec.Now();

            // Root directory
            if (path == "/" || path.Length == 0)
            {
                stat = MakeDirectoryStat(tsNow);
                return PosixResult.Success;
            }

            var localPath = FusePathToLocalPath(path);
            var record = _localStateDb.GetFileRecordAsync(_dbPath, localPath).GetAwaiter().GetResult();
            if (record == null)
            {
                _logger.LogTrace("GetAttr: file not found in metadata: {Path}", path);
                stat = default;
                return PosixResult.ENOENT;
            }

            if (record.SyncStateTag == "Directory")
            {
                stat = MakeDirectoryStat(tsNow);
            }
            else
            {
                var fileSize = record.ContentHash != null
                    ? _cache.TryPeek(localPath, out var cached) && cached != null ? cached.Length : 0
                    : 0;

                stat = MakeFileStat(fileSize, tsNow);
            }

            return PosixResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAttr failed for {Path}", path);
            stat = default;
            return PosixResult.EIO;
        }
    }

    private static FuseFileStat MakeDirectoryStat(TimeSpec ts)
    {
        return default(FuseFileStat) with
        {
            st_mode = PosixFileMode.Directory | PosixFileMode.OwnerAll | PosixFileMode.GroupReadExecute | PosixFileMode.OthersReadExecute,
            st_nlink = 2,
            st_uid = 0,
            st_gid = 0,
            st_atim = ts,
            st_mtim = ts,
            st_ctim = ts,
        };
    }

    private static FuseFileStat MakeFileStat(long size, TimeSpec ts)
    {
        return default(FuseFileStat) with
        {
            st_mode = PosixFileMode.Regular | PosixFileMode.OwnerReadWrite | PosixFileMode.GroupRead | PosixFileMode.OthersRead,
            st_nlink = 1,
            st_size = size,
            st_uid = 0,
            st_gid = 0,
            st_atim = ts,
            st_mtim = ts,
            st_ctim = ts,
        };
    }

    /// <inheritdoc/>
    public PosixResult ReadDir(
        ReadOnlyNativeMemory<byte> fileNamePtr,
        out IEnumerable<FuseDirEntry> entries,
        ref FuseFileInfo fileInfo,
        long offset,
        FuseReadDirFlags flags)
    {
        entries = [];
        var path = GetPath(fileNamePtr);

        try
        {
            var dirEntries = new List<FuseDirEntry>();

            // Always include . and ..
            dirEntries.Add(new FuseDirEntry(".", 0, (FuseFillDirFlags)0, default));
            dirEntries.Add(new FuseDirEntry("..", 0, (FuseFillDirFlags)0, default));

            var localDirPath = path == "/" ? _syncRootPath : FusePathToLocalPath(path);

            // Query all records and filter by parent path
            var allRecords = _localStateDb.GetAllFileRecordsAsync(_dbPath).GetAwaiter().GetResult();
            var prefix = localDirPath.TrimEnd('/') + "/";

            foreach (var record in allRecords)
            {
                var localPath = record.LocalPath.Replace('\\', '/');
                if (!localPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                var relativeName = localPath[prefix.Length..];
                // Only include direct children (not nested)
                if (relativeName.Contains('/'))
                    continue;

                var isDir = record.SyncStateTag == "Directory";

                var entryStat = default(FuseFileStat);
                if (isDir)
                {
                    entryStat.st_mode = PosixFileMode.Directory | PosixFileMode.OwnerAll | PosixFileMode.GroupReadExecute | PosixFileMode.OthersReadExecute;
                }
                else
                {
                    entryStat.st_mode = PosixFileMode.Regular | PosixFileMode.OwnerReadWrite | PosixFileMode.GroupRead | PosixFileMode.OthersRead;
                    entryStat.st_size = 0; // Will be filled on getattr
                }

                dirEntries.Add(new FuseDirEntry(
                    relativeName,
                    0,
                    (FuseFillDirFlags)0,
                    entryStat));
            }

            entries = dirEntries;
            return PosixResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReadDir failed for {Path}", path);
            return PosixResult.EIO;
        }
    }

    /// <inheritdoc/>
    public PosixResult Open(ReadOnlyNativeMemory<byte> fileNamePtr, ref FuseFileInfo fileInfo)
    {
        var path = GetPath(fileNamePtr);
        var localPath = FusePathToLocalPath(path);

        _logger.LogDebug("Open: {Path}", path);

        try
        {
            // Trigger hydration if file is cloud-only and being opened for reading
            var record = _localStateDb.GetFileRecordAsync(_dbPath, localPath).GetAwaiter().GetResult();
            if (record == null)
                return PosixResult.ENOENT;

            if (record.HydrationState == HydrationState.CloudOnly ||
                record.HydrationState == HydrationState.Downloading)
            {
                // Hydrate synchronously - this blocks the FUSE thread
                var hydrationResult = HydrateFileSync(localPath, record.NodeId);
                if (hydrationResult != PosixResult.Success)
                    return hydrationResult;
            }

            fileInfo.Context = localPath;
            return PosixResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open failed for {Path}", path);
            return PosixResult.EIO;
        }
    }

    /// <inheritdoc/>
    public PosixResult Read(
        ReadOnlyNativeMemory<byte> fileNamePtr,
        NativeMemory<byte> buffer,
        long position,
        out int readLength,
        ref FuseFileInfo fileInfo)
    {
        readLength = 0;
        var path = GetPath(fileNamePtr);
        var localPath = FusePathToLocalPath(path);

        try
        {
            // Try to get from cache
            var data = _cache[localPath];
            if (data == null)
            {
                // Not in cache - check if we need to hydrate
                var record = _localStateDb.GetFileRecordAsync(_dbPath, localPath).GetAwaiter().GetResult();
                if (record == null)
                    return PosixResult.ENOENT;

                if (record.HydrationState == HydrationState.CloudOnly)
                {
                    var hydrationResult = HydrateFileSync(localPath, record.NodeId);
                    if (hydrationResult != PosixResult.Success)
                        return hydrationResult;

                    data = _cache[localPath];
                }

                if (data == null)
                    return PosixResult.EIO;
            }

            if (position >= data.Length)
            {
                readLength = 0;
                return PosixResult.Success;
            }

            var available = (int)Math.Min(buffer.Length, data.Length - position);
            if (available <= 0)
            {
                readLength = 0;
                return PosixResult.Success;
            }

            // Copy data to the FUSE buffer
            new ReadOnlySpan<byte>(data, (int)position, available).CopyTo(buffer.Span);
            readLength = available;

            return PosixResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read failed for {Path}", path);
            return PosixResult.EIO;
        }
    }

    /// <inheritdoc/>
    public PosixResult Write(
        ReadOnlyNativeMemory<byte> fileNamePtr,
        ReadOnlyNativeMemory<byte> buffer,
        long position,
        out int writtenLength,
        ref FuseFileInfo fileInfo)
    {
        writtenLength = 0;
        var path = GetPath(fileNamePtr);
        var localPath = FusePathToLocalPath(path);

        try
        {
            // Get-or-create cached content
            var existing = _cache[localPath];
            byte[] newData;

            if (existing == null)
            {
                newData = buffer.Span.ToArray();
            }
            else
            {
                // Handle extending or writing within the existing buffer
                var endPosition = position + buffer.Length;
                if (endPosition > existing.Length)
                {
                    newData = new byte[endPosition];
                    Buffer.BlockCopy(existing, 0, newData, 0, (int)position);
                }
                else
                {
                    newData = new byte[existing.Length];
                    Buffer.BlockCopy(existing, 0, newData, 0, existing.Length);
                }
            }

            buffer.Span.CopyTo(new Span<byte>(newData, (int)position, buffer.Length));
            _cache[localPath] = newData;
            writtenLength = buffer.Length;

            // Mark file as modified — queue pending upload
            var record = _localStateDb.GetFileRecordAsync(_dbPath, localPath).GetAwaiter().GetResult();
            if (record != null)
            {
                record.HydrationState = HydrationState.Hydrated;
                record.ContentHash = null; // Invalidate hash — needs re-upload
                _localStateDb.UpsertFileRecordAsync(_dbPath, record).GetAwaiter().GetResult();

                _localStateDb.QueueOperationAsync(_dbPath,
                    new PendingUpload { LocalPath = localPath, NodeId = record.NodeId }).GetAwaiter().GetResult();
            }

            return PosixResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Write failed for {Path}", path);
            return PosixResult.EIO;
        }
    }

    /// <inheritdoc/>
    public PosixResult Create(
        ReadOnlyNativeMemory<byte> fileNamePtr,
        int mode,
        ref FuseFileInfo fileInfo)
    {
        var path = GetPath(fileNamePtr);
        var localPath = FusePathToLocalPath(path);

        _logger.LogDebug("Create: {Path}", path);

        try
        {
            var record = new LocalFileRecord
            {
                LocalPath = localPath,
                NodeId = Guid.NewGuid(),
                SyncStateTag = "Pending",
                HydrationState = HydrationState.Hydrated,
                LastSyncedAt = DateTime.UtcNow,
                LocalModifiedAt = DateTime.UtcNow,
            };

            _localStateDb.UpsertFileRecordAsync(_dbPath, record).GetAwaiter().GetResult();

            // Initialize empty content in cache
            _cache[localPath] = [];

            // Queue upload
            _localStateDb.QueueOperationAsync(_dbPath,
                new PendingUpload { LocalPath = localPath, NodeId = record.NodeId }).GetAwaiter().GetResult();

            fileInfo.Context = localPath;
            return PosixResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Create failed for {Path}", path);
            return PosixResult.EIO;
        }
    }

    /// <inheritdoc/>
    public PosixResult Unlink(ReadOnlyNativeMemory<byte> fileNamePtr)
    {
        var path = GetPath(fileNamePtr);
        var localPath = FusePathToLocalPath(path);

        _logger.LogDebug("Unlink: {Path}", path);

        try
        {
            var record = _localStateDb.GetFileRecordAsync(_dbPath, localPath).GetAwaiter().GetResult();
            if (record == null)
                return PosixResult.ENOENT;

            // Remove from cache
            _cache.Remove(localPath);

            // Queue delete for server propagation
            _localStateDb.QueueOperationAsync(_dbPath,
                new PendingDelete { NodeId = record.NodeId, LocalPath = record.LocalPath }).GetAwaiter().GetResult();

            // Remove from local state
            _localStateDb.RemoveFileRecordAsync(_dbPath, localPath).GetAwaiter().GetResult();

            return PosixResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unlink failed for {Path}", path);
            return PosixResult.EIO;
        }
    }

    /// <inheritdoc/>
    public PosixResult Rename(ReadOnlyNativeMemory<byte> from, ReadOnlyNativeMemory<byte> to)
    {
        var fromPath = GetPath(from);
        var toPath = GetPath(to);
        var fromLocal = FusePathToLocalPath(fromPath);
        var toLocal = FusePathToLocalPath(toPath);

        _logger.LogDebug("Rename: {From} -> {To}", fromPath, toPath);

        try
        {
            var record = _localStateDb.GetFileRecordAsync(_dbPath, fromLocal).GetAwaiter().GetResult();
            if (record == null)
                return PosixResult.ENOENT;

            // Move cached data
            var cached = _cache[fromLocal];
            if (cached != null)
            {
                _cache[toLocal] = cached;
                _cache.Remove(fromLocal);
            }

            // Update local record
            record.LocalPath = toLocal;
            _localStateDb.UpsertFileRecordAsync(_dbPath, record).GetAwaiter().GetResult();
            _localStateDb.RemoveFileRecordAsync(_dbPath, fromLocal).GetAwaiter().GetResult();

            // Queue rename operation via delete + upload
            _localStateDb.QueueOperationAsync(_dbPath,
                new PendingUpload { LocalPath = toLocal, NodeId = record.NodeId }).GetAwaiter().GetResult();

            return PosixResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rename failed: {From} -> {To}", fromPath, toPath);
            return PosixResult.EIO;
        }
    }

    /// <inheritdoc/>
    public PosixResult MkDir(ReadOnlyNativeMemory<byte> fileNamePtr, PosixFileMode mode)
    {
        var path = GetPath(fileNamePtr);
        var localPath = FusePathToLocalPath(path);

        _logger.LogDebug("MkDir: {Path}", path);

        try
        {
            var record = new LocalFileRecord
            {
                LocalPath = localPath,
                NodeId = Guid.NewGuid(),
                SyncStateTag = "Directory",
                HydrationState = HydrationState.Hydrated,
                LastSyncedAt = DateTime.UtcNow,
                LocalModifiedAt = DateTime.UtcNow,
            };

            _localStateDb.UpsertFileRecordAsync(_dbPath, record).GetAwaiter().GetResult();

            // Queue create for server
            _localStateDb.QueueOperationAsync(_dbPath,
                new PendingUpload { LocalPath = localPath, NodeId = record.NodeId }).GetAwaiter().GetResult();

            return PosixResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MkDir failed for {Path}", path);
            return PosixResult.EIO;
        }
    }

    /// <inheritdoc/>
    public PosixResult RmDir(ReadOnlyNativeMemory<byte> fileNamePtr)
    {
        var path = GetPath(fileNamePtr);
        var localPath = FusePathToLocalPath(path);

        _logger.LogDebug("RmDir: {Path}", path);

        try
        {
            var record = _localStateDb.GetFileRecordAsync(_dbPath, localPath).GetAwaiter().GetResult();
            if (record == null)
                return PosixResult.ENOENT;

            // Queue delete for server
            _localStateDb.QueueOperationAsync(_dbPath,
                new PendingDelete { NodeId = record.NodeId, LocalPath = record.LocalPath }).GetAwaiter().GetResult();

            // Remove all children
            _localStateDb.RemoveFileRecordsUnderPathAsync(_dbPath, localPath).GetAwaiter().GetResult();
            _localStateDb.RemoveFileRecordAsync(_dbPath, localPath).GetAwaiter().GetResult();

            return PosixResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RmDir failed for {Path}", path);
            return PosixResult.EIO;
        }
    }

    /// <inheritdoc/>
    public PosixResult Truncate(ReadOnlyNativeMemory<byte> fileNamePtr, long size)
    {
        var path = GetPath(fileNamePtr);
        var localPath = FusePathToLocalPath(path);

        try
        {
            var existing = _cache[localPath];
            if (existing == null)
            {
                // Initialize empty if not cached
                _cache[localPath] = size > 0 ? new byte[size] : [];
            }
            else if (size < existing.Length)
            {
                var truncated = new byte[size];
                Buffer.BlockCopy(existing, 0, truncated, 0, (int)size);
                _cache[localPath] = truncated;
            }
            else if (size > existing.Length)
            {
                var extended = new byte[size];
                Buffer.BlockCopy(existing, 0, extended, 0, existing.Length);
                _cache[localPath] = extended;
            }

            return PosixResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Truncate failed for {Path}", path);
            return PosixResult.EIO;
        }
    }

    /// <inheritdoc/>
    public PosixResult UTime(
        ReadOnlyNativeMemory<byte> fileNamePtr,
        TimeSpec atime,
        TimeSpec mtime,
        ref FuseFileInfo fileInfo)
    {
        var path = GetPath(fileNamePtr);
        var localPath = FusePathToLocalPath(path);

        try
        {
            var record = _localStateDb.GetFileRecordAsync(_dbPath, localPath).GetAwaiter().GetResult();
            if (record != null)
            {
                record.LocalModifiedAt = DateTimeOffset.FromUnixTimeSeconds((long)mtime.tv_sec).UtcDateTime;
                _localStateDb.UpsertFileRecordAsync(_dbPath, record).GetAwaiter().GetResult();
            }

            return PosixResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UTime failed for {Path}", path);
            return PosixResult.EIO;
        }
    }

    /// <inheritdoc/>
    public PosixResult Release(ReadOnlyNativeMemory<byte> fileNamePtr, ref FuseFileInfo fileInfo)
    {
        // No special cleanup needed for file handles in our implementation
        return PosixResult.Success;
    }

    /// <inheritdoc/>
    public PosixResult Flush(ReadOnlyNativeMemory<byte> fileNamePtr, ref FuseFileInfo fileInfo)
    {
        // Data is already written to cache in Write() — no additional flush needed
        return PosixResult.Success;
    }

    /// <inheritdoc/>
    public PosixResult Access(ReadOnlyNativeMemory<byte> fileNamePtr, PosixAccessMode mask)
    {
        var path = GetPath(fileNamePtr);

        // Root is always accessible
        if (path == "/" || path.Length == 0)
            return PosixResult.Success;

        var localPath = FusePathToLocalPath(path);
        var record = _localStateDb.GetFileRecordAsync(_dbPath, localPath).GetAwaiter().GetResult();

        return record != null ? PosixResult.Success : PosixResult.ENOENT;
    }

    /// <inheritdoc/>
    public PosixResult StatFs(ReadOnlyNativeMemory<byte> fileNamePtr, out FuseVfsStat statvfs)
    {
        statvfs = default;
        return PosixResult.Success;
    }

    /// <inheritdoc/>
    public PosixResult ReadLink(ReadOnlyNativeMemory<byte> fileNamePtr, NativeMemory<byte> target)
    {
        return PosixResult.ENOENT; // Symlinks not yet supported
    }

    /// <inheritdoc/>
    public PosixResult SymLink(ReadOnlyNativeMemory<byte> from, ReadOnlyNativeMemory<byte> to)
    {
        return PosixResult.ENOENT; // Symlinks not yet supported
    }

    /// <inheritdoc/>
    public PosixResult Link(ReadOnlyNativeMemory<byte> from, ReadOnlyNativeMemory<byte> to)
    {
        return PosixResult.ENOENT; // Hard links not yet supported
    }

    /// <inheritdoc/>
    public PosixResult OpenDir(ReadOnlyNativeMemory<byte> fileNamePtr, ref FuseFileInfo fileInfo)
    {
        return PosixResult.Success;
    }

    /// <inheritdoc/>
    public PosixResult ReleaseDir(ReadOnlyNativeMemory<byte> fileNamePtr, ref FuseFileInfo fileInfo)
    {
        return PosixResult.Success;
    }

    /// <inheritdoc/>
    public PosixResult FSync(ReadOnlyNativeMemory<byte> fileNamePtr, bool datasync, ref FuseFileInfo fileInfo)
    {
        return PosixResult.Success;
    }

    /// <inheritdoc/>
    public PosixResult FSyncDir(ReadOnlyNativeMemory<byte> fileNamePtr, bool datasync, ref FuseFileInfo fileInfo)
    {
        return PosixResult.Success;
    }

    /// <inheritdoc/>
    public PosixResult ChMod(NativeMemory<byte> fileNamePtr, PosixFileMode mode)
    {
        return PosixResult.Success;
    }

    /// <inheritdoc/>
    public PosixResult ChOwn(NativeMemory<byte> fileNamePtr, int uid, int gid)
    {
        return PosixResult.Success;
    }

    /// <inheritdoc/>
    public PosixResult FAllocate(
        NativeMemory<byte> fileNamePtr,
        FuseAllocateMode mode,
        long offset,
        long length,
        ref FuseFileInfo fileInfo)
    {
        return PosixResult.Success;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Clean up hydration locks
        foreach (var kvp in _hydrationLocks)
        {
            kvp.Value.Dispose();
        }
        _hydrationLocks.Clear();
    }

    /// <inheritdoc/>
    public PosixResult IoCtl(
        ReadOnlyNativeMemory<byte> fileNamePtr,
        int cmd,
        IntPtr arg,
        ref FuseFileInfo fileInfo,
        FuseIoctlFlags flags,
        IntPtr data)
    {
        return PosixResult.ENOSYS;
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private static string GetPath(ReadOnlyNativeMemory<byte> fileNamePtr)
    {
        // The path is a null-terminated UTF-8 byte sequence
        var span = fileNamePtr.Span;
        var nullTerminator = span.IndexOf((byte)0);
        if (nullTerminator >= 0)
            span = span[..nullTerminator];

        return span.Length > 0
            ? Encoding.UTF8.GetString(span)
            : "/";
    }

    private string FusePathToLocalPath(string fusePath)
    {
        // Convert FUSE path (e.g., "/Documents/file.txt") to local path
        var normalized = fusePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        return string.IsNullOrEmpty(normalized)
            ? _syncRootPath
            : Path.Combine(_syncRootPath, normalized);
    }

    private PosixResult HydrateFileSync(string localPath, Guid nodeId)
    {
        // Use per-file semaphore to prevent duplicate downloads
        var semaphore = _hydrationLocks.GetOrAdd(localPath, _ => new SemaphoreSlim(1, 1));

        if (!semaphore.Wait(0))
        {
            // Another thread is already hydrating this file
            _logger.LogDebug("Hydration already in progress for {LocalPath}, waiting...", localPath);
            semaphore.Wait();
            semaphore.Release();
            // Check if hydration succeeded
            return _cache[localPath] != null ? PosixResult.Success : PosixResult.EBUSY;
        }

        try
        {
            // Check cache first
            if (_cache[localPath] != null)
            {
                _logger.LogTrace("File already cached: {LocalPath}", localPath);
                return PosixResult.Success;
            }

            _logger.LogInformation("Hydrating file: {LocalPath} (NodeId={NodeId})", localPath, nodeId);

            // Update state to Downloading
            var record = _localStateDb.GetFileRecordAsync(_dbPath, localPath).GetAwaiter().GetResult();
            if (record != null)
            {
                record.HydrationState = HydrationState.Downloading;
                _localStateDb.UpsertFileRecordAsync(_dbPath, record).GetAwaiter().GetResult();
            }

            // Download content
            using var contentStream = _chunkedTransfer.DownloadAsync(
                nodeId,
                cancellationToken: default).GetAwaiter().GetResult();

            if (contentStream == null)
            {
                _logger.LogWarning("Download returned null for {LocalPath}", localPath);
                return PosixResult.EIO;
            }

            using var ms = new MemoryStream();
            contentStream.CopyTo(ms);
            var data = ms.ToArray();

            // Store in cache
            _cache[localPath] = data;

            // Update hydration state
            if (record != null)
            {
                record.HydrationState = HydrationState.Hydrated;
                _localStateDb.UpsertFileRecordAsync(_dbPath, record).GetAwaiter().GetResult();
            }

            _logger.LogInformation("Successfully hydrated {LocalPath} ({Length} bytes)", localPath, data.Length);
            return PosixResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hydrate {LocalPath}", localPath);
            return PosixResult.EIO;
        }
        finally
        {
            semaphore.Release();
        }
    }
}

#endif
