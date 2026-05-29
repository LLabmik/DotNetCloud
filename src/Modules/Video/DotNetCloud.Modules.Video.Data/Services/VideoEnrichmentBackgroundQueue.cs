using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Video.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// In-memory video enrichment queue. One active/queued job per user at a time.
/// </summary>
internal sealed class InMemoryVideoEnrichmentBackgroundQueue : IVideoEnrichmentBackgroundQueue
{
    private readonly Channel<VideoEnrichmentJob> _channel = Channel.CreateUnbounded<VideoEnrichmentJob>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

    private readonly HashSet<Guid> _activeOrQueuedUsers = [];
    private readonly object _syncRoot = new();
    private readonly ILogger<InMemoryVideoEnrichmentBackgroundQueue> _logger;

    public InMemoryVideoEnrichmentBackgroundQueue(ILogger<InMemoryVideoEnrichmentBackgroundQueue> logger)
    {
        _logger = logger;
    }

    public ValueTask<bool> EnqueueAsync(VideoEnrichmentJob job, CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            if (!_activeOrQueuedUsers.Add(job.OwnerId))
            {
                _logger.LogDebug("Duplicate enrichment job rejected for user {UserId}", job.OwnerId);
                return ValueTask.FromResult(false);
            }
        }

        return EnqueueCoreAsync(job, cancellationToken);
    }

    public async IAsyncEnumerable<VideoEnrichmentJob> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var job in _channel.Reader.ReadAllAsync(cancellationToken))
            yield return job;
    }

    public void MarkCompleted(Guid userId)
    {
        lock (_syncRoot)
        {
            _activeOrQueuedUsers.Remove(userId);
        }
    }

    private async ValueTask<bool> EnqueueCoreAsync(VideoEnrichmentJob job, CancellationToken cancellationToken)
    {
        await _channel.Writer.WriteAsync(job, cancellationToken);
        return true;
    }
}

/// <summary>
/// Hosted service that processes background video enrichment jobs one at a time.
/// </summary>
internal sealed class VideoEnrichmentBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly InMemoryVideoEnrichmentBackgroundQueue _queue;
    private readonly ILogger<VideoEnrichmentBackgroundService> _logger;

    public VideoEnrichmentBackgroundService(
        IServiceScopeFactory scopeFactory,
        InMemoryVideoEnrichmentBackgroundQueue queue,
        ILogger<VideoEnrichmentBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await RunJobAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Background video enrichment failed for user {UserId}", job.OwnerId);
            }
            finally
            {
                _queue.MarkCompleted(job.OwnerId);
            }
        }
    }

    private async Task RunJobAsync(VideoEnrichmentJob job, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var enrichmentService = scope.ServiceProvider.GetRequiredService<IVideoEnrichmentService>();
        var thumbnailService = scope.ServiceProvider.GetRequiredService<IVideoThumbnailService>();
        var db = scope.ServiceProvider.GetRequiredService<VideoDbContext>();
        var scanProgress = scope.ServiceProvider.GetRequiredService<VideoScanProgressState>();

        var caller = new CallerContext(job.OwnerId, ["user"], CallerType.System);
        var elapsedStopwatch = Stopwatch.StartNew();

        // ── Find all videos that still need enrichment ──
        // A video needs enrichment if it has no TMDB poster (HasExternalPoster == false)
        // AND no locally generated thumbnail (ThumbnailPosterHash == null).
        var pendingVideos = await db.UserVideos
            .Include(uv => uv.CanonicalVideo)
            .Where(uv => uv.OwnerId == job.OwnerId && !uv.IsDeleted
                && uv.CanonicalVideo != null
                && !uv.CanonicalVideo.HasExternalPoster
                && uv.CanonicalVideo.ThumbnailPosterHash == null)
            .ToListAsync(stoppingToken);

        var total = pendingVideos.Count;
        int tmdbEnriched = 0, screenshotFallback = 0, failed = 0;

        _logger.LogInformation(
            "Batch enrichment starting for user {UserId}: {Count} videos pending",
            job.OwnerId, total);

        if (total == 0)
        {
            scanProgress.UpdateProgress(job.OwnerId, new LibraryScanProgress
            {
                Phase = "Enrichment complete — all videos already enriched",
                TotalFiles = job.TotalFiles,
                TracksAdded = job.VideosAdded,
                TracksSkipped = job.VideosSkipped,
                TracksFailed = job.VideosFailed,
                TracksRemoved = job.VideosRemoved,
                TmdbEnriched = 0,
                ScreenshotFallback = 0,
                FilesProcessed = 0,
                PercentComplete = 100,
                ElapsedTime = elapsedStopwatch.Elapsed
            });
            scanProgress.CompleteScan(job.OwnerId);
            return;
        }

        // Report initial state
        scanProgress.UpdateProgress(job.OwnerId, new LibraryScanProgress
        {
            Phase = "Enriching videos from TMDB…",
            CurrentFile = pendingVideos[0].CanonicalVideo?.Title ?? pendingVideos[0].CanonicalVideo?.FileName ?? "Unknown",
            TotalFiles = total,
            TracksAdded = job.VideosAdded,
            TracksSkipped = job.VideosSkipped,
            TracksFailed = job.VideosFailed,
            TracksRemoved = job.VideosRemoved,
            FilesProcessed = 0,
            PercentComplete = 0,
            ElapsedTime = elapsedStopwatch.Elapsed
        });

        for (var i = 0; i < pendingVideos.Count; i++)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            var userVideo = pendingVideos[i];
            var displayName = userVideo.CanonicalVideo?.Title ?? userVideo.CanonicalVideo?.FileName ?? "Unknown";

            try
            {
                // Step 1: Try TMDB enrichment
                await enrichmentService.EnrichVideoAsync(userVideo.Id, caller, cancellationToken: stoppingToken);

                // Re-fetch to check if TMDB provided a poster
                var updated = await db.UserVideos
                    .Include(uv => uv.CanonicalVideo)
                    .FirstAsync(uv => uv.Id == userVideo.Id, stoppingToken);

                if (updated.CanonicalVideo?.HasExternalPoster == true)
                {
                    tmdbEnriched++;
                    _logger.LogDebug("TMDB enrichment succeeded for video {VideoId}: '{Title}'",
                        userVideo.Id, displayName);
                }
                else
                {
                    // Step 2: TMDB had no match — generate local thumbnail + screenshots
                    await thumbnailService.GenerateThumbnailAsync(userVideo.Id, userVideo.FileNodeId, stoppingToken);
                    await thumbnailService.GenerateScreenshotsAsync(userVideo.Id, userVideo.FileNodeId, stoppingToken);
                    screenshotFallback++;
                    _logger.LogDebug("Screenshot fallback for video {VideoId}: '{Title}'",
                        userVideo.Id, displayName);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogWarning(ex, "Enrichment failed for video {VideoId}: '{Title}'",
                    userVideo.Id, displayName);
            }

            // Report progress
            var processed = i + 1;
            scanProgress.UpdateProgress(job.OwnerId, new LibraryScanProgress
            {
                Phase = failed > 0 ? "Enriching videos from TMDB…" : "Enriching videos from TMDB…",
                CurrentFile = processed < pendingVideos.Count
                    ? (pendingVideos[processed].CanonicalVideo?.Title ?? pendingVideos[processed].CanonicalVideo?.FileName ?? "Unknown")
                    : null,
                FilesProcessed = processed,
                TotalFiles = total,
                TracksAdded = job.VideosAdded,
                TracksSkipped = job.VideosSkipped,
                TracksFailed = job.VideosFailed + failed,
                TracksRemoved = job.VideosRemoved,
                TmdbEnriched = tmdbEnriched,
                ScreenshotFallback = screenshotFallback,
                PercentComplete = (int)((double)processed / total * 100),
                ElapsedTime = elapsedStopwatch.Elapsed
            });
        }

        // ── Final report ──
        scanProgress.UpdateProgress(job.OwnerId, new LibraryScanProgress
        {
            Phase = "Enrichment complete",
            FilesProcessed = tmdbEnriched + screenshotFallback + failed,
            TotalFiles = total,
            TracksAdded = job.VideosAdded,
            TracksSkipped = job.VideosSkipped,
            TracksFailed = job.VideosFailed + failed,
            TracksRemoved = job.VideosRemoved,
            TmdbEnriched = tmdbEnriched,
            ScreenshotFallback = screenshotFallback,
            PercentComplete = 100,
            ElapsedTime = elapsedStopwatch.Elapsed
        });

        _logger.LogInformation(
            "Batch enrichment complete for user {UserId}: {Tmdb} TMDB, {Screenshot} screenshot fallback, {Failed} failed out of {Total}",
            job.OwnerId, tmdbEnriched, screenshotFallback, failed, total);

        scanProgress.CompleteScan(job.OwnerId);
    }
}
