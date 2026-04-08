using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Video.Events;
using DotNetCloud.Modules.Video.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<VideoIndexingCallback> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoIndexingCallback"/> class.
    /// </summary>
    public VideoIndexingCallback(VideoService videoService, VideoDbContext db, IServiceScopeFactory scopeFactory, ILogger<VideoIndexingCallback> logger)
    {
        _videoService = videoService;
        _db = db;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task IndexVideoAsync(Guid fileNodeId, string fileName, string mimeType, long sizeBytes, Guid ownerId, string? storagePath = null, CancellationToken cancellationToken = default)
    {
        var caller = new CallerContext(ownerId, ["user"], CallerType.System);
        var video = await _videoService.CreateVideoAsync(fileNodeId, fileName, mimeType, sizeBytes, ownerId, caller, cancellationToken);

        // Generate poster thumbnail in a new DI scope (fire-and-forget — failure is non-fatal).
        // Must use a separate scope because the request scope will be disposed before the background work completes.
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var thumbnailService = scope.ServiceProvider.GetRequiredService<IVideoThumbnailService>();
                await thumbnailService.GenerateThumbnailAsync(video.Id, fileNodeId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Thumbnail generation failed for video {VideoId}", video.Id);
            }
        }, CancellationToken.None);

        _logger.LogDebug("Video indexed for FileNode {FileNodeId} by user {OwnerId}", fileNodeId, ownerId);
    }

    /// <inheritdoc />
    public async Task ResetCollectionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resetting video collection — deleting all metadata");

        // Delete in FK-safe order: junction/child tables first, then parents.
        // Use IgnoreQueryFilters to include soft-deleted records.
        _db.WatchHistories.RemoveRange(await _db.WatchHistories.IgnoreQueryFilters().ToListAsync(cancellationToken));
        _db.WatchProgresses.RemoveRange(await _db.WatchProgresses.IgnoreQueryFilters().ToListAsync(cancellationToken));
        _db.VideoShares.RemoveRange(await _db.VideoShares.IgnoreQueryFilters().ToListAsync(cancellationToken));
        _db.Subtitles.RemoveRange(await _db.Subtitles.IgnoreQueryFilters().ToListAsync(cancellationToken));
        _db.VideoCollectionItems.RemoveRange(await _db.VideoCollectionItems.IgnoreQueryFilters().ToListAsync(cancellationToken));
        _db.VideoCollections.RemoveRange(await _db.VideoCollections.IgnoreQueryFilters().ToListAsync(cancellationToken));
        _db.VideoMetadata.RemoveRange(await _db.VideoMetadata.IgnoreQueryFilters().ToListAsync(cancellationToken));
        _db.Videos.RemoveRange(await _db.Videos.IgnoreQueryFilters().ToListAsync(cancellationToken));

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Video collection reset complete");
    }
}
