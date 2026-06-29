using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs.Media;
using DotNetCloud.Core.Services;
using DotNetCloud.Core.Services.ModuleApis;
using DotNetCloud.Modules.Photos.Events;
using DotNetCloud.Modules.Music.Events;
using DotNetCloud.Modules.Video.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Coordinates media library scanning across process-isolated modules.
/// Uses gRPC to discover file candidates from the Files module,
/// then calls into module-specific indexing callbacks (Photos, Music, Video).
/// </summary>
public sealed class MediaFolderImportService : IMediaLibraryScanner
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IFilesApiClient _filesApiClient;
    private readonly ILogger<MediaFolderImportService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaFolderImportService"/> class.
    /// </summary>
    public MediaFolderImportService(
        IServiceScopeFactory scopeFactory,
        IFilesApiClient filesApiClient,
        ILogger<MediaFolderImportService> logger)
    {
        _scopeFactory = scopeFactory;
        _filesApiClient = filesApiClient;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>
    /// This method scans local filesystem directories. For process-isolated module
    /// scanning, prefer <see cref="ScanSourcesAsync"/> which discovers files via
    /// the Files module's gRPC interface.
    /// </remarks>
    public async Task<MediaScanResult> ScanAsync(
        string directoryPath, Guid ownerId, string mediaType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "ScanAsync (local filesystem) called for {Path} by {OwnerId} ({MediaType}). " +
            "This path is deprecated—prefer ScanSourcesAsync for gRPC-based virtual scanning.",
            directoryPath, ownerId, mediaType);

        return new MediaScanResult
        {
            TotalFound = 0,
            Imported = 0,
            Skipped = 0,
            Failed = 0,
            Errors = ["Local filesystem scanning is not supported in process-isolated mode. Use ScanSourcesAsync instead."],
        };
    }

    /// <inheritdoc />
    public async Task<MediaScanResult> ScanFolderAsync(
        Guid? folderId, Guid ownerId, string mediaType,
        IProgress<MediaScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<MediaLibrarySource> sources =
        [
            new MediaLibrarySource
            {
                SourceKind = MediaLibrarySourceKind.OwnedFileNode,
                FolderId = folderId,
                DisplayPath = folderId.HasValue ? $"/{folderId.Value:D}" : "/",
                DisplayName = folderId.HasValue ? "Selected Folder" : "Home",
                Enabled = true,
            }
        ];

        return await ScanSourcesAsync(sources, ownerId, mediaType, progress, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MediaScanResult> ScanSourcesAsync(
        IReadOnlyCollection<MediaLibrarySource> sources,
        Guid ownerId,
        string mediaType,
        IProgress<MediaScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<MediaScanType>(mediaType, ignoreCase: true, out var parsed))
        {
            throw new ArgumentException($"Invalid media type: {mediaType}", nameof(mediaType));
        }

        var result = new MediaScanResult();

        // Report initial discovery progress
        progress?.Report(new MediaScanProgress
        {
            Phase = "Discovering files via Files module...",
            FilesDiscovered = 0,
            PercentComplete = 0,
        });

        // ── Clone from existing user first (Music only) ──
        if (parsed == MediaScanType.Music)
        {
            using var cloneScope = _scopeFactory.CreateScope();
            var musicCallback = cloneScope.ServiceProvider.GetService<IMusicIndexingCallback>();
            if (musicCallback is not null)
            {
                progress?.Report(new MediaScanProgress
                {
                    Phase = "Cloning music library...",
                    FilesDiscovered = 0,
                    PercentComplete = 0,
                });

                var cloned = await musicCallback.CloneLibraryFromExistingAsync(ownerId, progress, cancellationToken);
                if (cloned > 0)
                {
                    _logger.LogInformation(
                        "Cloned {Count} {MediaType} tracks from existing user for {OwnerId} — continuing to discover remaining files",
                        cloned, parsed, ownerId);
                    result.Imported = cloned;
                }
            }
        }

        // ── Discover file candidates via gRPC ──
        progress?.Report(new MediaScanProgress
        {
            Phase = "Querying Files module for media files...",
            FilesDiscovered = 0,
            PercentComplete = 0,
        });

        var scanResult = await _filesApiClient.ScanMediaFoldersAsync(
            sources, ownerId, mediaType, cancellationToken);

        if (!scanResult.Success)
        {
            result.Errors.Add(scanResult.ErrorMessage ?? "Media folder scan failed.");
            _logger.LogWarning("Media folder scan via gRPC failed: {Error}", scanResult.ErrorMessage);
            return result;
        }

        result.TotalFound = scanResult.TotalFound;
        _logger.LogInformation(
            "Media source scan: found {Count} {MediaType} files via gRPC for user {OwnerId}",
            result.TotalFound, parsed, ownerId);

        // ── Determine already-indexed files ──
        var alreadyIndexedIds = await GetAlreadyIndexedIdsAsync(parsed, ownerId, cancellationToken);
        var currentFileNodeIds = scanResult.Candidates.Select(c => c.Id).ToHashSet();
        var filesToIndex = scanResult.Candidates
            .Where(candidate => !alreadyIndexedIds.Contains(candidate.Id))
            .OrderBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        result.Skipped = result.TotalFound - filesToIndex.Count;

        // ── Index new files ──
        var filesProcessed = 0;
        foreach (var file in filesToIndex)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            progress?.Report(new MediaScanProgress
            {
                Phase = "Indexing media",
                CurrentFile = file.Name,
                FilesDiscovered = result.TotalFound,
                FilesProcessed = filesProcessed,
                TotalFiles = filesToIndex.Count,
                Imported = result.Imported,
                Failed = result.Failed,
                PercentComplete = filesToIndex.Count > 0
                    ? (int)((long)filesProcessed * 100 / filesToIndex.Count)
                    : 0,
            });

            try
            {
                await IndexCandidateAsync(file, ownerId, parsed, cancellationToken);
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

        // ── Clean up deleted files ──
        if (alreadyIndexedIds.Count > 0 && !cancellationToken.IsCancellationRequested)
        {
            var deletedFileNodeIds = alreadyIndexedIds
                .Where(id => !currentFileNodeIds.Contains(id))
                .ToList();

            if (deletedFileNodeIds.Count > 0)
            {
                _logger.LogInformation(
                    "Detected {Count} deleted {MediaType} files for user {OwnerId} — removing from index. FileNodeIds: {FileNodeIds}",
                    deletedFileNodeIds.Count, parsed, ownerId,
                    string.Join(",", deletedFileNodeIds.Take(10)) + (deletedFileNodeIds.Count > 10 ? "..." : ""));

                progress?.Report(new MediaScanProgress
                {
                    Phase = "Removing deleted files",
                    FilesProcessed = filesProcessed,
                    TotalFiles = filesToIndex.Count,
                    Imported = result.Imported,
                    Failed = result.Failed,
                    PercentComplete = 100,
                });

                result.Removed = await RemoveDeletedAsync(parsed, deletedFileNodeIds, ownerId, cancellationToken);
                _logger.LogInformation("Removed {Count} {MediaType} tracks for user {OwnerId}", result.Removed, parsed, ownerId);
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
            "Media source scan complete: {Imported} indexed, {Skipped} skipped, {Removed} removed, {Failed} failed out of {Total}",
            result.Imported, result.Skipped, result.Removed, result.Failed, result.TotalFound);

        return result;
    }

    private async Task<HashSet<Guid>> GetAlreadyIndexedIdsAsync(
        MediaScanType mediaType, Guid ownerId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        switch (mediaType)
        {
            case MediaScanType.Music:
            {
                var musicCallback = sp.GetService<IMusicIndexingCallback>();
                return musicCallback is null
                    ? []
                    : await musicCallback.GetIndexedFileNodeIdsAsync(ownerId, cancellationToken);
            }

            case MediaScanType.Video:
            {
                var videoCallback = sp.GetService<IVideoIndexingCallback>();
                return videoCallback is null
                    ? []
                    : await videoCallback.GetIndexedFileNodeIdsAsync(ownerId, cancellationToken);
            }

            case MediaScanType.Photos:
            {
                var photoCallback = sp.GetService<IPhotoIndexingCallback>();
                return photoCallback is null
                    ? []
                    : await photoCallback.GetIndexedFileNodeIdsAsync(ownerId, cancellationToken);
            }

            default:
                return [];
        }
    }

    private async Task IndexCandidateAsync(
        MediaFileCandidateDto candidate,
        Guid ownerId,
        MediaScanType mediaType,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        switch (mediaType)
        {
            case MediaScanType.Photos:
            {
                var photoCallback = sp.GetService<IPhotoIndexingCallback>();
                if (photoCallback is not null)
                {
                    await photoCallback.IndexPhotoAsync(
                        candidate.Id, candidate.Name, candidate.MimeType, candidate.Size,
                        ownerId, storagePath: null, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("IPhotoIndexingCallback not registered — cannot index {File}", candidate.Name);
                }
                break;
            }

            case MediaScanType.Music:
            {
                var musicCallback = sp.GetService<IMusicIndexingCallback>();
                if (musicCallback is not null)
                {
                    await musicCallback.IndexAudioAsync(
                        candidate.Id, candidate.Name, candidate.MimeType, candidate.Size,
                        ownerId, storagePath: null, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("IMusicIndexingCallback not registered — cannot index {File}", candidate.Name);
                }
                break;
            }

            case MediaScanType.Video:
            {
                var videoCallback = sp.GetService<IVideoIndexingCallback>();
                if (videoCallback is not null)
                {
                    await videoCallback.IndexVideoAsync(
                        candidate.Id, candidate.Name, candidate.MimeType, candidate.Size,
                        ownerId, storagePath: null,
                        candidate.SourceName, candidate.SubFolderPath, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("IVideoIndexingCallback not registered — cannot index {File}", candidate.Name);
                }
                break;
            }
        }
    }

    private async Task<int> RemoveDeletedAsync(
        MediaScanType mediaType,
        IReadOnlyCollection<Guid> deletedFileNodeIds,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        switch (mediaType)
        {
            case MediaScanType.Music:
            {
                var musicCallback = sp.GetService<IMusicIndexingCallback>();
                return musicCallback is null
                    ? 0
                    : await musicCallback.RemoveDeletedTracksAsync(deletedFileNodeIds, ownerId, cancellationToken);
            }

            case MediaScanType.Video:
            {
                var videoCallback = sp.GetService<IVideoIndexingCallback>();
                return videoCallback is null
                    ? 0
                    : await videoCallback.RemoveDeletedVideosAsync(deletedFileNodeIds, ownerId, cancellationToken);
            }

            case MediaScanType.Photos:
            {
                var photoCallback = sp.GetService<IPhotoIndexingCallback>();
                return photoCallback is null
                    ? 0
                    : await photoCallback.RemoveDeletedPhotosAsync(deletedFileNodeIds, ownerId, cancellationToken);
            }

            default:
                return 0;
        }
    }
}
