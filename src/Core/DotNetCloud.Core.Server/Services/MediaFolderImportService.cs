using System.Security.Cryptography;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.Modules.Photos.Events;
using DotNetCloud.Modules.Music.Events;
using DotNetCloud.Modules.Video.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Scans local directories for media files, imports them into the Files module,
/// and triggers module-specific indexing via <see cref="FileUploadedEvent"/>.
/// </summary>
public sealed class MediaFolderImportService : IMediaLibraryScanner
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IFileStorageEngine _storageEngine;
    private readonly IEventBus _eventBus;
    private readonly string _storageRootPath;
    private readonly ILogger<MediaFolderImportService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaFolderImportService"/> class.
    /// </summary>
    public MediaFolderImportService(
        IServiceScopeFactory scopeFactory,
        IFileStorageEngine storageEngine,
        IEventBus eventBus,
        IConfiguration configuration,
        ILogger<MediaFolderImportService> logger)
    {
        _scopeFactory = scopeFactory;
        _storageEngine = storageEngine;
        _eventBus = eventBus;
        _storageRootPath = configuration["Files:Storage:RootPath"] ?? Path.GetTempPath();
        _logger = logger;
    }

    /// <summary>
    /// Scans a directory for media files and imports them into the system.
    /// Creates FileNode records and triggers module indexing events.
    /// </summary>
    /// <param name="directoryPath">Absolute path to the directory to scan.</param>
    /// <param name="ownerId">User ID that will own the imported files.</param>
    /// <param name="mediaType">Type of media to scan for (photos, music, video, or all).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Import result with counts of imported and skipped files.</returns>
    public async Task<MediaImportResult> ScanAndImportAsync(
        string directoryPath,
        Guid ownerId,
        MediaType mediaType = MediaType.All,
        CancellationToken cancellationToken = default)
    {
        // Validate and canonicalize path to prevent directory traversal
        var fullPath = Path.GetFullPath(directoryPath);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {fullPath}");
        }

        _logger.LogInformation("Starting media folder scan of {Path} for user {OwnerId} (type: {MediaType})",
            fullPath, ownerId, mediaType);

        var result = new MediaImportResult();
        var extensions = GetExtensionsForMediaType(mediaType);

        var files = Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories)
            .Where(f =>
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                return extensions.Contains(ext);
            })
            .ToList();

        result.TotalFound = files.Count;
        _logger.LogInformation("Found {Count} media files to process in {Path}", files.Count, fullPath);

        foreach (var filePath in files)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                var imported = await ImportSingleFileAsync(filePath, ownerId, cancellationToken);
                if (imported)
                    result.Imported++;
                else
                    result.Skipped++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
                _logger.LogWarning(ex, "Failed to import file {FilePath}", filePath);
            }
        }

        _logger.LogInformation(
            "Media folder scan complete: {Imported} imported, {Skipped} skipped, {Failed} failed out of {Total}",
            result.Imported, result.Skipped, result.Failed, result.TotalFound);

        return result;
    }

    /// <inheritdoc />
    public async Task<MediaScanResult> ScanAsync(string directoryPath, Guid ownerId, string mediaType, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<MediaType>(mediaType, ignoreCase: true, out var parsed))
        {
            throw new ArgumentException($"Invalid media type: {mediaType}", nameof(mediaType));
        }

        var importResult = await ScanAndImportAsync(directoryPath, ownerId, parsed, cancellationToken);

        return new MediaScanResult
        {
            TotalFound = importResult.TotalFound,
            Imported = importResult.Imported,
            Skipped = importResult.Skipped,
            Failed = importResult.Failed,
            Errors = importResult.Errors,
        };
    }

    /// <inheritdoc />
    public async Task<MediaScanResult> ScanFolderAsync(Guid? folderId, Guid ownerId, string mediaType, IProgress<MediaScanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<MediaType>(mediaType, ignoreCase: true, out var parsed))
        {
            throw new ArgumentException($"Invalid media type: {mediaType}", nameof(mediaType));
        }

        var extensions = GetExtensionsForMediaType(parsed);

        using var scope = _scopeFactory.CreateScope();
        var filesDb = scope.ServiceProvider.GetRequiredService<FilesDbContext>();

        // Build query for files under the selected virtual folder
        IQueryable<FileNode> query = filesDb.FileNodes
            .Where(n => n.OwnerId == ownerId && !n.IsDeleted && n.NodeType == FileNodeType.File);

        if (folderId.HasValue)
        {
            // Get the target folder's materialized path for descendant lookup
            var folder = await filesDb.FileNodes
                .Where(n => n.Id == folderId.Value && n.OwnerId == ownerId && !n.IsDeleted)
                .Select(n => new { n.MaterializedPath })
                .FirstOrDefaultAsync(cancellationToken);

            if (folder is null)
            {
                return new MediaScanResult { Errors = ["Folder not found."] };
            }

            var prefix = folder.MaterializedPath + "/";
            query = query.Where(n => n.MaterializedPath.StartsWith(prefix));
        }

        // Pull candidates, then filter by extension in memory (EF can't do HashSet.Contains on ext)
        var candidates = await query
            .Select(n => new { n.Id, n.Name, n.Size, n.MimeType, n.ParentId, n.StoragePath })
            .ToListAsync(cancellationToken);

        var matchingFiles = candidates
            .Where(f =>
            {
                var ext = Path.GetExtension(f.Name).ToLowerInvariant();
                return extensions.Contains(ext);
            })
            .ToList();

        var result = new MediaScanResult { TotalFound = matchingFiles.Count };

        _logger.LogInformation(
            "Virtual folder scan: found {Count} {MediaType} files under folder {FolderId} for user {OwnerId}",
            matchingFiles.Count, parsed, folderId?.ToString() ?? "root", ownerId);

        // ── Pre-filter: skip already-indexed files ──────────────────────────────
        // Build a set of FileNode IDs currently in the index so we can skip
        // files that are already up to date, avoiding redundant stream downloads.
        HashSet<Guid> alreadyIndexedIds = [];
        switch (parsed)
        {
            case MediaType.Music:
            {
                var musicCallback = scope.ServiceProvider.GetService<IMusicIndexingCallback>();
                if (musicCallback is not null)
                    alreadyIndexedIds = await musicCallback.GetIndexedFileNodeIdsAsync(ownerId, cancellationToken);
                break;
            }
            case MediaType.Video:
            {
                var videoCallback = scope.ServiceProvider.GetService<IVideoIndexingCallback>();
                if (videoCallback is not null)
                    alreadyIndexedIds = await videoCallback.GetIndexedFileNodeIdsAsync(ownerId, cancellationToken);
                break;
            }
            case MediaType.Photos:
            {
                var photoCallback = scope.ServiceProvider.GetService<IPhotoIndexingCallback>();
                if (photoCallback is not null)
                    alreadyIndexedIds = await photoCallback.GetIndexedFileNodeIdsAsync(ownerId, cancellationToken);
                break;
            }
        }

        var currentFileNodeIds = matchingFiles.Select(f => f.Id).ToHashSet();
        var filesToIndex = matchingFiles.Where(f => !alreadyIndexedIds.Contains(f.Id)).ToList();
        result.Skipped = matchingFiles.Count - filesToIndex.Count;

        if (result.Skipped > 0)
        {
            _logger.LogInformation(
                "Skipping {Skipped} already-indexed {MediaType} files for user {OwnerId}",
                result.Skipped, parsed, ownerId);
        }

        var filesProcessed = 0;
        foreach (var file in filesToIndex)
        {
            if (cancellationToken.IsCancellationRequested) break;

            progress?.Report(new MediaScanProgress
            {
                Phase = "Indexing media",
                CurrentFile = file.Name,
                FilesProcessed = filesProcessed,
                TotalFiles = filesToIndex.Count,
                Imported = result.Imported,
                Failed = result.Failed,
                PercentComplete = filesToIndex.Count > 0 ? (int)((long)filesProcessed * 100 / filesToIndex.Count) : 0,
            });

            try
            {
                var mime = file.MimeType ?? GetMimeType(file.Name);

                // If the FileNode has a null/stale MimeType, fix it in the database
                // so the content endpoint serves the correct Content-Type for playback.
                if (file.MimeType is null && mime != "application/octet-stream")
                {
                    var node = await filesDb.FileNodes.FindAsync([file.Id], cancellationToken);
                    if (node is not null)
                    {
                        node.MimeType = mime;
                        await filesDb.SaveChangesAsync(cancellationToken);
                    }
                }

                // Resolve actual chunk storage path (files uploaded via chunked API have their
                // data in FileChunks, not at FileNode.StoragePath which may be stale/empty).
                string? resolvedStoragePath = file.StoragePath;
                {
                    var chunkPath = await (
                        from fv in filesDb.FileVersions
                        where fv.FileNodeId == file.Id
                        orderby fv.VersionNumber descending
                        join fvc in filesDb.FileVersionChunks on fv.Id equals fvc.FileVersionId
                        orderby fvc.SequenceIndex
                        join fc in filesDb.FileChunks on fvc.FileChunkId equals fc.Id
                        select fc.StoragePath
                    ).FirstOrDefaultAsync(cancellationToken);
                    if (!string.IsNullOrEmpty(chunkPath))
                        resolvedStoragePath = chunkPath;
                }

                switch (parsed)
                {
                    case MediaType.Photos:
                        var photoCallback = scope.ServiceProvider.GetService<IPhotoIndexingCallback>();
                        if (photoCallback is not null)
                        {
                            await photoCallback.IndexPhotoAsync(file.Id, file.Name, mime, file.Size, ownerId, resolvedStoragePath, cancellationToken);
                        }
                        else
                            _logger.LogWarning("IPhotoIndexingCallback not registered — cannot index {File}", file.Name);
                        break;

                    case MediaType.Music:
                        var musicCallback = scope.ServiceProvider.GetService<IMusicIndexingCallback>();
                        if (musicCallback is not null)
                            await musicCallback.IndexAudioAsync(file.Id, file.Name, mime, file.Size, ownerId, resolvedStoragePath, cancellationToken);
                        else
                            _logger.LogWarning("IMusicIndexingCallback not registered — cannot index {File}", file.Name);
                        break;

                    case MediaType.Video:
                        var videoCallback = scope.ServiceProvider.GetService<IVideoIndexingCallback>();
                        if (videoCallback is not null)
                            await videoCallback.IndexVideoAsync(file.Id, file.Name, mime, file.Size, ownerId, resolvedStoragePath, cancellationToken);
                        else
                            _logger.LogWarning("IVideoIndexingCallback not registered — cannot index {File}", file.Name);
                        break;
                }

                result.Imported++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"{file.Name}: {ex.Message}");
                _logger.LogWarning(ex, "Failed to index media file {FileId}", file.Id);
            }

            filesProcessed++;
        }

        // ── Deletion detection: remove indexed items whose source files no longer exist ─
        if (alreadyIndexedIds.Count > 0 && !cancellationToken.IsCancellationRequested)
        {
            var deletedFileNodeIds = alreadyIndexedIds
                .Where(id => !currentFileNodeIds.Contains(id))
                .ToList();

            if (deletedFileNodeIds.Count > 0)
            {
                _logger.LogInformation(
                    "Detected {Count} deleted {MediaType} files for user {OwnerId} — removing from index",
                    deletedFileNodeIds.Count, parsed, ownerId);

                progress?.Report(new MediaScanProgress
                {
                    Phase = "Removing deleted files",
                    FilesProcessed = filesProcessed,
                    TotalFiles = filesToIndex.Count,
                    Imported = result.Imported,
                    Failed = result.Failed,
                    PercentComplete = 100,
                });

                switch (parsed)
                {
                    case MediaType.Music:
                    {
                        var musicCallback = scope.ServiceProvider.GetService<IMusicIndexingCallback>();
                        if (musicCallback is not null)
                            result.Removed = await musicCallback.RemoveDeletedTracksAsync(deletedFileNodeIds, ownerId, cancellationToken);
                        break;
                    }
                    case MediaType.Video:
                    {
                        var videoCallback = scope.ServiceProvider.GetService<IVideoIndexingCallback>();
                        if (videoCallback is not null)
                            result.Removed = await videoCallback.RemoveDeletedVideosAsync(deletedFileNodeIds, ownerId, cancellationToken);
                        break;
                    }
                    case MediaType.Photos:
                    {
                        var photoCallback = scope.ServiceProvider.GetService<IPhotoIndexingCallback>();
                        if (photoCallback is not null)
                            result.Removed = await photoCallback.RemoveDeletedPhotosAsync(deletedFileNodeIds, ownerId, cancellationToken);
                        break;
                    }
                }
            }
        }

        progress?.Report(new MediaScanProgress
        {
            Phase = "Complete",
            FilesProcessed = filesProcessed,
            TotalFiles = filesToIndex.Count,
            Imported = result.Imported,
            Failed = result.Failed,
            Removed = result.Removed,
            PercentComplete = 100,
        });

        _logger.LogInformation(
            "Virtual folder scan complete: {Imported} indexed, {Skipped} skipped, {Removed} removed, {Failed} failed out of {Total}",
            result.Imported, result.Skipped, result.Removed, result.Failed, result.TotalFound);

        return result;
    }

    private async Task<bool> ImportSingleFileAsync(string filePath, Guid ownerId, CancellationToken ct)
    {
        var fileName = Path.GetFileName(filePath);
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists) return false;

        // Compute content hash for dedup
        await using var stream = File.OpenRead(filePath);
        var contentHash = await ComputeHashAsync(stream, ct);
        var storagePath = ContentHasher.GetFileStoragePath(contentHash);

        using var scope = _scopeFactory.CreateScope();
        var filesDb = scope.ServiceProvider.GetRequiredService<FilesDbContext>();

        // Check if already imported (same hash + same owner)
        var existingNode = await filesDb.FileNodes
            .FirstOrDefaultAsync(n =>
                n.OwnerId == ownerId &&
                n.ContentHash == contentHash &&
                n.Name == fileName &&
                !n.IsDeleted, ct);

        if (existingNode is not null)
        {
            _logger.LogDebug("Skipping already-imported file {FileName} (hash {Hash})", fileName, contentHash[..8]);
            return false;
        }

        // Ensure the file is in hash storage (symlink if on same filesystem, else copy)
        if (!await _storageEngine.ExistsAsync(storagePath, ct))
        {
            await StoreFileAsync(filePath, storagePath, ct);
        }

        var mimeType = GetMimeType(filePath);

        // Create FileNode record
        var fileNode = new FileNode
        {
            Name = fileName,
            NodeType = FileNodeType.File,
            MimeType = mimeType,
            Size = fileInfo.Length,
            ParentId = null, // Root level (imported files)
            OwnerId = ownerId,
            ContentHash = contentHash,
            StoragePath = storagePath,
            Depth = 0,
        };
        fileNode.MaterializedPath = $"/{fileNode.Id}";

        filesDb.FileNodes.Add(fileNode);
        await filesDb.SaveChangesAsync(ct);

        _logger.LogDebug("Created FileNode {Id} for {FileName} ({MimeType}, {Size} bytes)",
            fileNode.Id, fileName, mimeType, fileInfo.Length);

        // Publish event to trigger module indexing
        var caller = CallerContext.CreateSystemContext();
        await _eventBus.PublishAsync(new FileUploadedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = fileNode.Id,
            FileName = fileName,
            Size = fileInfo.Length,
            MimeType = mimeType,
            ParentId = null,
            UploadedByUserId = ownerId,
            StoragePath = storagePath,
        }, caller, ct);

        return true;
    }

    private async Task StoreFileAsync(string sourcePath, string storagePath, CancellationToken ct)
    {
        // Read file and store via storage engine
        // For very large files, this streams in chunks
        var fileBytes = await File.ReadAllBytesAsync(sourcePath, ct);
        await _storageEngine.WriteChunkAsync(storagePath, fileBytes, ct);
    }

    private static async Task<string> ComputeHashAsync(Stream stream, CancellationToken ct)
    {
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexStringLower(hash);
    }

    private static HashSet<string> GetExtensionsForMediaType(MediaType mediaType) => mediaType switch
    {
        MediaType.Photos => PhotoExtensions,
        MediaType.Music => MusicExtensions,
        MediaType.Video => VideoExtensions,
        MediaType.All => [.. PhotoExtensions, .. MusicExtensions, .. VideoExtensions],
        _ => throw new ArgumentOutOfRangeException(nameof(mediaType))
    };

    private static string GetMimeType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            // Photos
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".tiff" or ".tif" => "image/tiff",
            ".svg" => "image/svg+xml",
            ".heic" => "image/heic",
            ".heif" => "image/heif",
            ".raw" or ".cr2" or ".nef" or ".arw" => "image/x-raw",
            // Music
            ".mp3" => "audio/mpeg",
            ".flac" => "audio/flac",
            ".ogg" or ".oga" => "audio/ogg",
            ".opus" => "audio/opus",
            ".aac" => "audio/aac",
            ".m4a" => "audio/mp4",
            ".wav" => "audio/wav",
            ".wma" => "audio/x-ms-wma",
            ".aiff" or ".aif" => "audio/aiff",
            ".wv" => "audio/wavpack",
            ".ape" => "audio/ape",
            // Video
            ".mp4" or ".m4v" => "video/mp4",
            ".mkv" => "video/x-matroska",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            ".wmv" => "video/x-ms-wmv",
            ".flv" => "video/x-flv",
            ".webm" => "video/webm",
            ".3gp" => "video/3gpp",
            ".mpg" or ".mpeg" => "video/mpeg",
            ".ts" => "video/mp2t",
            _ => "application/octet-stream"
        };
    }

    private static readonly HashSet<string> PhotoExtensions =
    [
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tiff", ".tif",
        ".svg", ".heic", ".heif", ".raw", ".cr2", ".nef", ".arw"
    ];

    private static readonly HashSet<string> MusicExtensions =
    [
        ".mp3", ".flac", ".ogg", ".oga", ".opus", ".aac", ".m4a",
        ".wav", ".wma", ".aiff", ".aif", ".wv", ".ape"
    ];

    private static readonly HashSet<string> VideoExtensions =
    [
        ".mp4", ".m4v", ".mkv", ".avi", ".mov", ".wmv", ".flv",
        ".webm", ".3gp", ".mpg", ".mpeg", ".ts"
    ];
}

/// <summary>
/// Type of media to filter for during folder scanning.
/// </summary>
public enum MediaType
{
    /// <summary>All media types.</summary>
    All,
    /// <summary>Photo/image files only.</summary>
    Photos,
    /// <summary>Audio/music files only.</summary>
    Music,
    /// <summary>Video files only.</summary>
    Video
}

/// <summary>
/// Result of a media folder import operation.
/// </summary>
public sealed class MediaImportResult
{
    /// <summary>Total media files found in the directory.</summary>
    public int TotalFound { get; set; }

    /// <summary>Files successfully imported.</summary>
    public int Imported { get; set; }

    /// <summary>Files skipped (already imported).</summary>
    public int Skipped { get; set; }

    /// <summary>Files that failed to import.</summary>
    public int Failed { get; set; }

    /// <summary>Error messages for failed imports.</summary>
    public List<string> Errors { get; set; } = [];
}
