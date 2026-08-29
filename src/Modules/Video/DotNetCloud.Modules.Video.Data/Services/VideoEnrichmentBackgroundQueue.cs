using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Video.Models;
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
    private readonly VideoScanProgressState _scanProgress;
    private readonly ILogger<VideoEnrichmentBackgroundService> _logger;

    public VideoEnrichmentBackgroundService(
        IServiceScopeFactory scopeFactory,
        InMemoryVideoEnrichmentBackgroundQueue queue,
        VideoScanProgressState scanProgress,
        ILogger<VideoEnrichmentBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _scanProgress = scanProgress;
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
                // Fast-track jobs never start a scan session, so never complete one —
                // doing so could prematurely end a real scan the user has running.
                if (!job.IsFastTrack)
                    _scanProgress.CompleteScan(job.OwnerId);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Background video enrichment failed for user {UserId}", job.OwnerId);
                if (!job.IsFastTrack)
                    _scanProgress.CompleteScan(job.OwnerId);
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
        var seriesService = scope.ServiceProvider.GetRequiredService<IVideoSeriesService>();
        var db = scope.ServiceProvider.GetRequiredService<VideoDbContext>();
        var scanProgress = scope.ServiceProvider.GetRequiredService<VideoScanProgressState>();

        var caller = new CallerContext(job.OwnerId, ["user"], CallerType.System);

        // Fast-track jobs (small batches of newly added videos) enrich only the
        // specified videos as quickly as possible — no scan-progress reporting,
        // no interaction with the user's scan cancellation token, no series pass.
        if (job.IsFastTrack)
        {
            await RunFastTrackAsync(job, db, enrichmentService, thumbnailService, caller, stoppingToken);
            return;
        }

        var elapsedStopwatch = Stopwatch.StartNew();

        // Start a scan session so VideoScanProgressState tracks IsScanning = true
        // and provides a CancellationTokenSource that the UI's StopScan button can cancel.
        // CompleteScan (called below) owns disposal of this CTS — do NOT dispose it here.
        scanProgress.StartScan(job.OwnerId);
        var enrichmentToken = scanProgress.GetCancellationToken(job.OwnerId);

        // ── Find all videos that still need TMDB enrichment ──
        // Only videos that have NEVER been TMDB-enriched (TmdbId == null) qualify for
        // enrichment. Videos that already have a TMDB ID already have good metadata
        // (title, overview, genres, rating, poster) and must NOT be re-fetched from
        // TMDB — re-fetching wastes API calls and can overwrite manual corrections.
        // Videos with DurationTicks == 0 (missing ffprobe duration from a previous
        // scan) are also included so metadata extraction can backfill the duration.
        var pendingVideos = await db.UserVideos
            .Include(uv => uv.CanonicalVideo)
            .Where(uv => uv.OwnerId == job.OwnerId && !uv.IsDeleted
                && uv.CanonicalVideo != null
                && (uv.CanonicalVideo.TmdbId == null || uv.CanonicalVideo.DurationTicks == 0))
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
            if (stoppingToken.IsCancellationRequested || enrichmentToken.IsCancellationRequested)
                break;

            var userVideo = pendingVideos[i];
            var displayName = userVideo.CanonicalVideo?.Title ?? userVideo.CanonicalVideo?.FileName ?? "Unknown";

            EnrichmentOutcome outcome;
            try
            {
                outcome = await EnrichSingleVideoAsync(
                    db, userVideo, caller, enrichmentService, thumbnailService, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                outcome = EnrichmentOutcome.Failed;
                failed++;
                _logger.LogWarning(ex, "Enrichment failed for video {VideoId}: '{Title}'",
                    userVideo.Id, displayName);
            }

            switch (outcome)
            {
                case EnrichmentOutcome.Tmdb:
                    tmdbEnriched++;
                    _logger.LogDebug("TMDB enrichment succeeded for video {VideoId}: '{Title}'",
                        userVideo.Id, displayName);
                    break;
                case EnrichmentOutcome.ScreenshotFallback:
                    screenshotFallback++;
                    _logger.LogDebug("Screenshot fallback for video {VideoId}: '{Title}'",
                        userVideo.Id, displayName);
                    break;
                case EnrichmentOutcome.Skipped:
                    _logger.LogDebug("Duration-only backfill for video {VideoId}: '{Title}'",
                        userVideo.Id, displayName);
                    break;
                case EnrichmentOutcome.KeptExistingScreenshot:
                    _logger.LogDebug("TMDB still no match for video {VideoId}: '{Title}' — keeping existing screenshot",
                        userVideo.Id, displayName);
                    break;
                case EnrichmentOutcome.Failed:
                    // Already counted above.
                    break;
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
            FilesProcessed = total,
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

        // Enrich all series without TMDB data (Bug 3 fix)
        try
        {
            await seriesService.EnrichAllUnenrichedSeriesAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown requested during series enrichment
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Series enrichment failed during batch completion for user {UserId}", job.OwnerId);
        }

        scanProgress.CompleteScan(job.OwnerId);
    }

    // ── Fast-track (small batch) enrichment ──────────────────────────────

    /// <summary>
    /// Enriches only the videos specified on a fast-track job. Runs without scan
    /// progress reporting so a background fast-track does not flash "scanning" in
    /// the UI or interfere with a real scan's cancellation token.
    /// </summary>
    internal async Task RunFastTrackAsync(
        VideoEnrichmentJob job,
        VideoDbContext db,
        IVideoEnrichmentService enrichmentService,
        IVideoThumbnailService thumbnailService,
        CallerContext caller,
        CancellationToken stoppingToken)
    {
        if (job.VideoIds is not { Count: > 0 })
            return;

        var pendingVideos = await db.UserVideos
            .Include(uv => uv.CanonicalVideo)
            .Where(uv => uv.OwnerId == job.OwnerId && !uv.IsDeleted
                && uv.CanonicalVideo != null
                && job.VideoIds.Contains(uv.Id)
                && (uv.CanonicalVideo.TmdbId == null || uv.CanonicalVideo.DurationTicks == 0))
            .ToListAsync(stoppingToken);

        int tmdbEnriched = 0, screenshotFallback = 0, failed = 0;

        _logger.LogInformation(
            "Fast-track enrichment starting for user {UserId}: {Count} videos",
            job.OwnerId, pendingVideos.Count);

        foreach (var userVideo in pendingVideos)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            var displayName = userVideo.CanonicalVideo?.Title ?? userVideo.CanonicalVideo?.FileName ?? "Unknown";

            EnrichmentOutcome outcome;
            try
            {
                outcome = await EnrichSingleVideoAsync(
                    db, userVideo, caller, enrichmentService, thumbnailService, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                outcome = EnrichmentOutcome.Failed;
                failed++;
                _logger.LogWarning(ex, "Fast-track enrichment failed for video {VideoId}: '{Title}'",
                    userVideo.Id, displayName);
            }

            switch (outcome)
            {
                case EnrichmentOutcome.Tmdb:
                    tmdbEnriched++;
                    _logger.LogDebug("Fast-track TMDB enrichment succeeded for video {VideoId}: '{Title}'",
                        userVideo.Id, displayName);
                    break;
                case EnrichmentOutcome.ScreenshotFallback:
                    screenshotFallback++;
                    _logger.LogDebug("Fast-track screenshot fallback for video {VideoId}: '{Title}'",
                        userVideo.Id, displayName);
                    break;
                case EnrichmentOutcome.Skipped:
                    _logger.LogDebug("Fast-track duration-only backfill for video {VideoId}: '{Title}'",
                        userVideo.Id, displayName);
                    break;
                case EnrichmentOutcome.KeptExistingScreenshot:
                    _logger.LogDebug("Fast-track TMDB still no match for video {VideoId}: '{Title}' — keeping existing screenshot",
                        userVideo.Id, displayName);
                    break;
                case EnrichmentOutcome.Failed:
                    // Already counted above.
                    break;
            }
        }

        _logger.LogInformation(
            "Fast-track enrichment complete for user {UserId}: {Tmdb} TMDB, {Screenshot} screenshot fallback, {Failed} failed out of {Total}",
            job.OwnerId, tmdbEnriched, screenshotFallback, failed, pendingVideos.Count);
    }

    /// <summary>
    /// Enriches a single video: extracts embedded metadata via ffprobe, attempts a
    /// TMDB match, and falls back to local thumbnail/screenshot generation when TMDB
    /// has no match. Returns the outcome.
    /// </summary>
    private static async Task<EnrichmentOutcome> EnrichSingleVideoAsync(
        VideoDbContext db,
        UserVideo userVideo,
        CallerContext caller,
        IVideoEnrichmentService enrichmentService,
        IVideoThumbnailService thumbnailService,
        CancellationToken cancellationToken)
    {
        // Step 0: Extract embedded metadata via ffprobe (populates EmbeddedTitle,
        // EmbeddedTmdbId, EmbeddedImdbId, DurationTicks, etc.)
        await thumbnailService.ExtractMetadataAsync(userVideo.Id, userVideo.FileNodeId, cancellationToken);

        // If this video already has a TMDB poster, skip TMDB re-enrichment.
        // It was only included in this batch because DurationTicks was 0,
        // which ExtractMetadataAsync just fixed.
        if (userVideo.CanonicalVideo?.HasExternalPoster == true)
            return EnrichmentOutcome.Skipped;

        // Step 1: Try TMDB enrichment
        await enrichmentService.EnrichVideoAsync(userVideo.Id, caller, cancellationToken: cancellationToken);

        // Re-fetch to check if TMDB provided a poster
        var updated = await db.UserVideos
            .Include(uv => uv.CanonicalVideo)
            .FirstAsync(uv => uv.Id == userVideo.Id, cancellationToken);

        if (updated.CanonicalVideo?.HasExternalPoster == true)
            return EnrichmentOutcome.Tmdb;

        if (updated.CanonicalVideo?.ThumbnailPosterHash is null)
        {
            // Step 2: TMDB had no match and no screenshot exists yet —
            // generate local thumbnail + screenshots as fallback
            await thumbnailService.GenerateThumbnailAsync(userVideo.Id, userVideo.FileNodeId, cancellationToken);
            await thumbnailService.GenerateScreenshotsAsync(userVideo.Id, userVideo.FileNodeId, cancellationToken);
            return EnrichmentOutcome.ScreenshotFallback;
        }

        // Video already has a screenshot thumbnail but no TMDB match.
        // Keep existing screenshots — no need to regenerate.
        return EnrichmentOutcome.KeptExistingScreenshot;
    }

    /// <summary>
    /// Result of enriching a single video.
    /// </summary>
    private enum EnrichmentOutcome
    {
        /// <summary>TMDB provided metadata/poster.</summary>
        Tmdb,

        /// <summary>TMDB had no match; local thumbnail + screenshots were generated.</summary>
        ScreenshotFallback,

        /// <summary>Video already had a poster; only duration/metadata was backfilled.</summary>
        Skipped,

        /// <summary>TMDB had no match but existing screenshots were kept.</summary>
        KeptExistingScreenshot,

        /// <summary>Enrichment threw and was caught by the caller.</summary>
        Failed
    }
}
