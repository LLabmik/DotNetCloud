using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Video.Events;
using DotNetCloud.Modules.Video.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Implements the video indexing callback — bridges the Module → Data layer gap.
/// Called by FileUploadedVideoHandler when a video file is uploaded.
/// </summary>
public sealed class VideoIndexingCallback : IVideoIndexingCallback
{
    private readonly VideoService _videoService;
    private readonly VideoDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VideoIndexingCallback> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoIndexingCallback"/> class.
    /// </summary>
    public VideoIndexingCallback(VideoService videoService, VideoDbContext db, IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<VideoIndexingCallback> logger)
    {
        _videoService = videoService;
        _db = db;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task IndexVideoAsync(Guid fileNodeId, string fileName, string mimeType, long sizeBytes, Guid ownerId, string? storagePath = null, CancellationToken cancellationToken = default)
    {
        var caller = new CallerContext(ownerId, ["user"], CallerType.System);
        var video = await _videoService.CreateVideoAsync(fileNodeId, fileName, mimeType, sizeBytes, ownerId, caller, cancellationToken);

        // TMDB enrichment (fire-and-forget — network-dependent, graceful failure).
        // Thumbnail and screenshots are only generated as a fallback when TMDB doesn't provide a poster,
        // avoiding unnecessary ffmpeg work against every video file.
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var enrichmentService = scope.ServiceProvider.GetRequiredService<IVideoEnrichmentService>();
                var caller = new CallerContext(ownerId, ["user"], CallerType.System);
                await enrichmentService.EnrichVideoAsync(video.Id, caller, cancellationToken: CancellationToken.None);

                // Only generate local poster fallback if TMDB didn't provide one
                var db = scope.ServiceProvider.GetRequiredService<VideoDbContext>();
                var hasPoster = await db.Videos
                    .Where(v => v.Id == video.Id)
                    .Select(v => v.HasExternalPoster)
                    .FirstOrDefaultAsync(CancellationToken.None);

                if (!hasPoster)
                {
                    var thumbnailService = scope.ServiceProvider.GetRequiredService<IVideoThumbnailService>();
                    await thumbnailService.GenerateThumbnailAsync(video.Id, fileNodeId, CancellationToken.None);
                    await thumbnailService.GenerateScreenshotsAsync(video.Id, fileNodeId, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TMDB enrichment failed for video {VideoId}", video.Id);

                // Generate local poster fallback when TMDB is unavailable
                try
                {
                    await using var fallbackScope = _scopeFactory.CreateAsyncScope();
                    var thumbnailService = fallbackScope.ServiceProvider.GetRequiredService<IVideoThumbnailService>();
                    await thumbnailService.GenerateThumbnailAsync(video.Id, fileNodeId, CancellationToken.None);
                    await thumbnailService.GenerateScreenshotsAsync(video.Id, fileNodeId, CancellationToken.None);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogWarning(fallbackEx, "Poster fallback generation failed for video {VideoId}", video.Id);
                }
            }
        }, CancellationToken.None);

        // Metadata extraction (fire-and-forget — uses ffprobe, no network dependency)
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var thumbnailService = scope.ServiceProvider.GetRequiredService<IVideoThumbnailService>();
                await thumbnailService.ExtractMetadataAsync(video.Id, fileNodeId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Metadata extraction failed for video {VideoId}", video.Id);
            }
        }, CancellationToken.None);

        _logger.LogDebug("Video indexed for FileNode {FileNodeId} by user {OwnerId}", fileNodeId, ownerId);
    }

    /// <inheritdoc />
    public async Task ResetCollectionAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("RESET: Deleting video library metadata for owner {OwnerId}", ownerId);

        // Get all video IDs owned by this user
        var ownedVideoIds = await _db.Videos
            .IgnoreQueryFilters()
            .Where(v => v.OwnerId == ownerId)
            .Select(v => v.Id)
            .ToListAsync(cancellationToken);
        var ownedVideoIdSet = ownedVideoIds.ToHashSet();

        // Clean up screenshot and poster cache directories
        var storageRoot = _configuration["Files:Storage:RootPath"] ?? Path.GetTempPath();
        foreach (var dir in new[] { ".video-screenshots", ".video-posters" })
        {
            var path = Path.Combine(storageRoot, dir);
            if (Directory.Exists(path))
            {
                try
                { Directory.Delete(path, recursive: true); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete directory {Path} during collection reset", path); }
            }
        }

        // Delete in FK-safe order: junction/child tables first, then parents.
        // All deletes scoped to ownerId via owned video IDs.
        var watchHistories = await _db.WatchHistories.IgnoreQueryFilters()
            .Where(w => ownedVideoIdSet.Contains(w.VideoId)).ToListAsync(cancellationToken);
        _db.WatchHistories.RemoveRange(watchHistories);

        var watchProgresses = await _db.WatchProgresses.IgnoreQueryFilters()
            .Where(w => ownedVideoIdSet.Contains(w.VideoId)).ToListAsync(cancellationToken);
        _db.WatchProgresses.RemoveRange(watchProgresses);

        var shares = await _db.VideoShares.IgnoreQueryFilters()
            .Where(s => ownedVideoIdSet.Contains(s.VideoId)).ToListAsync(cancellationToken);
        _db.VideoShares.RemoveRange(shares);

        var subtitles = await _db.Subtitles.IgnoreQueryFilters()
            .Where(s => ownedVideoIdSet.Contains(s.VideoId)).ToListAsync(cancellationToken);
        _db.Subtitles.RemoveRange(subtitles);

        var collectionItems = await _db.VideoCollectionItems.IgnoreQueryFilters()
            .Where(ci => ownedVideoIdSet.Contains(ci.VideoId)).ToListAsync(cancellationToken);
        _db.VideoCollectionItems.RemoveRange(collectionItems);

        // Collections: scoped to owner
        var collections = await _db.VideoCollections.IgnoreQueryFilters()
            .Where(c => c.OwnerId == ownerId).ToListAsync(cancellationToken);
        _db.VideoCollections.RemoveRange(collections);

        var metadata = await _db.VideoMetadata.IgnoreQueryFilters()
            .Where(m => ownedVideoIdSet.Contains(m.VideoId)).ToListAsync(cancellationToken);
        _db.VideoMetadata.RemoveRange(metadata);

        var videos = await _db.Videos.IgnoreQueryFilters()
            .Where(v => v.OwnerId == ownerId).ToListAsync(cancellationToken);
        _db.Videos.RemoveRange(videos);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("RESET complete for owner {OwnerId}: {VideoCount} videos, {CollectionCount} collections",
            ownerId, videos.Count, collections.Count);
    }

    /// <inheritdoc />
    public async Task<HashSet<Guid>> GetIndexedFileNodeIdsAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var ids = await _db.Videos
            .Where(v => v.OwnerId == ownerId)
            .Select(v => v.FileNodeId)
            .ToListAsync(cancellationToken);
        return [.. ids];
    }

    /// <inheritdoc />
    public async Task<int> RemoveDeletedVideosAsync(IReadOnlyCollection<Guid> deletedFileNodeIds, Guid ownerId, CancellationToken cancellationToken = default)
    {
        var videos = await _db.Videos
            .Where(v => v.OwnerId == ownerId && deletedFileNodeIds.Contains(v.FileNodeId) && !v.IsDeleted)
            .ToListAsync(cancellationToken);

        if (videos.Count == 0)
            return 0;

        var videoIds = videos.Select(v => v.Id).ToHashSet();

        // Remove related junction/child records before soft-deleting parent.
        var collectionItems = await _db.VideoCollectionItems
            .Where(i => videoIds.Contains(i.VideoId))
            .ToListAsync(cancellationToken);
        _db.VideoCollectionItems.RemoveRange(collectionItems);

        var watchProgresses = await _db.WatchProgresses
            .Where(w => videoIds.Contains(w.VideoId))
            .ToListAsync(cancellationToken);
        _db.WatchProgresses.RemoveRange(watchProgresses);

        var watchHistories = await _db.WatchHistories
            .Where(w => videoIds.Contains(w.VideoId))
            .ToListAsync(cancellationToken);
        _db.WatchHistories.RemoveRange(watchHistories);

        var shares = await _db.VideoShares
            .Where(s => videoIds.Contains(s.VideoId))
            .ToListAsync(cancellationToken);
        _db.VideoShares.RemoveRange(shares);

        var subtitles = await _db.Subtitles
            .Where(s => videoIds.Contains(s.VideoId))
            .ToListAsync(cancellationToken);
        _db.Subtitles.RemoveRange(subtitles);

        var metadatas = await _db.VideoMetadata
            .Where(m => videoIds.Contains(m.VideoId))
            .ToListAsync(cancellationToken);
        _db.VideoMetadata.RemoveRange(metadatas);

        // Soft-delete the video records.
        var now = DateTime.UtcNow;
        foreach (var video in videos)
        {
            video.IsDeleted = true;
            video.DeletedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Removed {Count} deleted video records for user {OwnerId}",
            videos.Count, ownerId);

        return videos.Count;
    }
}
