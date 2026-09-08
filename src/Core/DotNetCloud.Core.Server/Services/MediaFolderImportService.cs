using DotNetCloud.Core.Authorization;
using DotNetCloud.Core;
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
            directoryPath, ownerId, LogSanitizer.Sanitize(mediaType));

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

        // ── Clone from existing user first (Music only, empty library only) ──
        if (parsed == MediaScanType.Music)
        {
            using var cloneScope = _scopeFactory.CreateScope();
            var musicCallback = cloneScope.ServiceProvider.GetService<IMusicIndexingCallback>();
            if (musicCallback is not null)
            {
                // Only clone when the user's library is empty (initial import).
                // On re-scan the library already has tracks — just discover new/removed files.
                var existingIds = await musicCallback.GetIndexedFileNodeIdsAsync(ownerId, cancellationToken);
                if (existingIds.Count == 0)
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
        }

        // ── Discover file candidates via gRPC ──
        progress?.Report(new MediaScanProgress
        {
            Phase = "Querying Files module for media files...",
            FilesDiscovered = 0,
            PercentComplete = 0,
        });

        var discovery = await DiscoverCandidatesAsync(sources, ownerId, mediaType, parsed, cancellationToken);

        if (!discovery.Success)
        {
            result.Errors.Add(discovery.ErrorMessage ?? "Media folder scan failed.");
            _logger.LogWarning("Media folder scan via gRPC failed: {Error}", LogSanitizer.Sanitize(discovery.ErrorMessage ?? ""));
            return result;
        }

        result.TotalFound = discovery.TotalFound;
        _logger.LogInformation(
            "Media source scan: found {Count} {MediaType} files via gRPC for user {OwnerId}",
            result.TotalFound, parsed, ownerId);

        // ── Determine already-indexed files ──
        var alreadyIndexedIds = discovery.AlreadyIndexedIds;
        var currentFileNodeIds = discovery.Candidates.Select(c => c.Id).ToHashSet();
        var filesToIndex = discovery.FilesToIndex;

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
                    deletedFileNodeIds.Count, LogSanitizer.Sanitize(parsed.ToString()), ownerId,
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

    /// <inheritdoc />
    public async Task<MediaDiscoveryResult> DiscoverNewMediaFilesAsync(
        IReadOnlyCollection<MediaLibrarySource> sources,
        Guid ownerId,
        string mediaType,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<MediaScanType>(mediaType, ignoreCase: true, out var parsed))
        {
            throw new ArgumentException($"Invalid media type: {mediaType}", nameof(mediaType));
        }

        if (sources.Count == 0)
        {
            _logger.LogDebug("Media discovery: no {MediaType} sources for user {OwnerId} — nothing to detect", parsed, ownerId);
            return new MediaDiscoveryResult { Success = true };
        }

        _logger.LogInformation(
            "User {OwnerId} requested a {MediaType} discovery check across {SourceCount} configured sources",
            ownerId, parsed, sources.Count);

        var discovery = await DiscoverCandidatesAsync(sources, ownerId, mediaType, parsed, cancellationToken);

        if (!discovery.Success)
        {
            _logger.LogWarning("Media discovery failed for user {OwnerId}: {Error}",
                ownerId, LogSanitizer.Sanitize(discovery.ErrorMessage ?? ""));
            return new MediaDiscoveryResult { Success = false, ErrorMessage = discovery.ErrorMessage };
        }

        var newFiles = discovery.FilesToIndex;
        return new MediaDiscoveryResult
        {
            Success = true,
            TotalFound = discovery.TotalFound,
            AlreadyIndexed = discovery.TotalFound - newFiles.Count,
            NewFileCount = newFiles.Count,
            SampleFileNames = newFiles.Take(20).Select(file => file.Name).ToList(),
        };
    }

    /// <inheritdoc />
    public async Task<int> CountLibraryItemsNotInSourcesAsync(
        IReadOnlyCollection<MediaLibrarySource> remainingSources,
        Guid ownerId,
        string mediaType,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<MediaScanType>(mediaType, ignoreCase: true, out var parsed))
        {
            throw new ArgumentException($"Invalid media type: {mediaType}", nameof(mediaType));
        }

        var orphanedIds = await GetOrphanedFileNodeIdsAsync(remainingSources, ownerId, mediaType, parsed, cancellationToken);
        return orphanedIds.Count;
    }

    /// <inheritdoc />
    public async Task<int> RemoveLibraryItemsNotInSourcesAsync(
        IReadOnlyCollection<MediaLibrarySource> remainingSources,
        Guid ownerId,
        string mediaType,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<MediaScanType>(mediaType, ignoreCase: true, out var parsed))
        {
            throw new ArgumentException($"Invalid media type: {mediaType}", nameof(mediaType));
        }

        var orphanedIds = await GetOrphanedFileNodeIdsAsync(remainingSources, ownerId, mediaType, parsed, cancellationToken);
        if (orphanedIds.Count == 0)
        {
            return 0;
        }

        _logger.LogInformation(
            "Removing {Count} {MediaType} items no longer reachable under the remaining sources for user {OwnerId}",
            orphanedIds.Count, parsed, ownerId);

        return await RemoveDeletedAsync(parsed, orphanedIds, ownerId, cancellationToken);
    }

    /// <summary>
    /// Computes the set of the user's indexed file node IDs that are not discoverable under the
    /// given (remaining) sources — i.e., the items a source removal would prune.
    /// </summary>
    private async Task<List<Guid>> GetOrphanedFileNodeIdsAsync(
        IReadOnlyCollection<MediaLibrarySource> remainingSources,
        Guid ownerId,
        string mediaType,
        MediaScanType parsed,
        CancellationToken cancellationToken)
    {
        // No remaining sources → nothing is discoverable, so every indexed item is orphaned.
        if (remainingSources.Count == 0)
        {
            return (await GetAlreadyIndexedIdsAsync(parsed, ownerId, cancellationToken)).ToList();
        }

        var discovery = await DiscoverCandidatesAsync(remainingSources, ownerId, mediaType, parsed, cancellationToken);
        if (!discovery.Success)
        {
            throw new InvalidOperationException(discovery.ErrorMessage ?? "Media folder scan failed.");
        }

        var reachableIds = discovery.Candidates.Select(candidate => candidate.Id).ToHashSet();
        return discovery.AlreadyIndexedIds
            .Where(id => !reachableIds.Contains(id))
            .ToList();
    }

    /// <summary>
    /// Runs the shared discovery pass: queries the Files module via gRPC for matching file
    /// candidates and diffs them against the module's already-indexed file node IDs.
    /// No files are indexed, updated, or removed here.
    /// </summary>
    private async Task<MediaDiscovery> DiscoverCandidatesAsync(
        IReadOnlyCollection<MediaLibrarySource> sources,
        Guid ownerId,
        string mediaType,
        MediaScanType parsed,
        CancellationToken cancellationToken)
    {
        var scanResult = await _filesApiClient.ScanMediaFoldersAsync(sources, ownerId, mediaType, cancellationToken);

        if (!scanResult.Success)
        {
            return MediaDiscovery.Failed(scanResult.ErrorMessage ?? "Media folder scan failed.");
        }

        var alreadyIndexedIds = await GetAlreadyIndexedIdsAsync(parsed, ownerId, cancellationToken);
        var filesToIndex = scanResult.Candidates
            .Where(candidate => !alreadyIndexedIds.Contains(candidate.Id))
            .OrderBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new MediaDiscovery(
            Success: true,
            ErrorMessage: null,
            TotalFound: scanResult.TotalFound,
            Candidates: scanResult.Candidates,
            AlreadyIndexedIds: alreadyIndexedIds,
            FilesToIndex: filesToIndex);
    }

    /// <summary>
    /// Internal carrier for a discovery pass: the gRPC scan outcome plus the already-indexed
    /// set and the ordered list of files still to be indexed.
    /// </summary>
    private sealed record MediaDiscovery(
        bool Success,
        string? ErrorMessage,
        int TotalFound,
        IReadOnlyList<MediaFileCandidateDto> Candidates,
        HashSet<Guid> AlreadyIndexedIds,
        List<MediaFileCandidateDto> FilesToIndex)
    {
        /// <summary>Creates a failed discovery result.</summary>
        public static MediaDiscovery Failed(string errorMessage) =>
            new(false, errorMessage, 0, [], [], []);
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
