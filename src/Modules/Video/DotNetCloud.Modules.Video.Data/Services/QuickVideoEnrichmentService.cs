using DotNetCloud.Modules.Video.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Background service that fast-tracks enrichment for small batches of newly added
/// videos. When a user adds ≤ <see cref="QuickVideoEnrichmentPolicy.MaxQuickBatchSize"/>
/// videos in quick succession, they are enqueued for immediate TMDB enrichment instead
/// of waiting for the daily job. Larger bursts are left to the daily job.
/// </summary>
/// <remarks>
/// Owned by the module host process only (registered in <c>AddVideoServices</c>). The
/// service is single-threaded — <see cref="ProcessPendingBurstsAsync"/> is only invoked
/// from <see cref="ExecuteAsync"/>, so its in-memory attempted-tracking dictionary needs
/// no synchronization.
/// </remarks>
internal sealed class QuickVideoEnrichmentService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IVideoEnrichmentBackgroundQueue _queue;
    private readonly ILogger<QuickVideoEnrichmentService> _logger;

    // Video IDs already fast-tracked in this process lifetime (id → created-at UTC),
    // so videos that fail a TMDB match are not re-enqueued on every poll. Entries age
    // out of the lookback window and are pruned each cycle.
    private readonly Dictionary<Guid, DateTime> _attemptedVideoIds = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="QuickVideoEnrichmentService"/> class.
    /// </summary>
    public QuickVideoEnrichmentService(
        IServiceScopeFactory scopeFactory,
        IVideoEnrichmentBackgroundQueue queue,
        ILogger<QuickVideoEnrichmentService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingBurstsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Quick video enrichment cycle failed");
            }

            await Task.Delay(QuickVideoEnrichmentPolicy.PollInterval, stoppingToken);
        }
    }

    /// <summary>
    /// Runs one polling cycle: finds recently added, unenriched videos, groups them into
    /// per-user bursts, and fast-tracks small quiet bursts. Exposed as internal so tests
    /// can drive it without running the whole background loop.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task ProcessPendingBurstsAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var recentCutoff = nowUtc - QuickVideoEnrichmentPolicy.LookbackWindow;

        // Prune attempted markers that have aged out of the lookback window.
        foreach (var id in _attemptedVideoIds
                     .Where(kv => kv.Value < recentCutoff)
                     .Select(kv => kv.Key)
                     .ToList())
        {
            _attemptedVideoIds.Remove(id);
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VideoDbContext>();

        // Recently created videos that have never been TMDB-enriched.
        var recent = await db.UserVideos
            .Where(uv => !uv.IsDeleted
                && uv.CanonicalVideo != null
                && uv.CanonicalVideo.TmdbId == null
                && uv.CreatedAt >= recentCutoff)
            .Select(uv => new { uv.Id, uv.OwnerId, uv.CreatedAt })
            .ToListAsync(cancellationToken);

        // Group by owner, excluding videos already fast-tracked in this process.
        var bursts = recent
            .Where(x => !_attemptedVideoIds.ContainsKey(x.Id))
            .GroupBy(x => x.OwnerId)
            .ToList();

        foreach (var group in bursts)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var newestCreatedAtUtc = group.Max(x => x.CreatedAt);
            var sinceNewest = nowUtc - newestCreatedAtUtc;
            var count = group.Count();

            // Burst still accumulating — wait for it to go quiet before deciding.
            if (sinceNewest < QuickVideoEnrichmentPolicy.QuietPeriod)
                continue;

            // Too many for the fast path — leave for the daily job.
            if (QuickVideoEnrichmentPolicy.ExceedsThreshold(count))
            {
                _logger.LogInformation(
                    "Quick enrichment skipped for user {UserId}: {Count} recent videos exceeds threshold {Max}",
                    group.Key, count, QuickVideoEnrichmentPolicy.MaxQuickBatchSize);
                continue;
            }

            if (!QuickVideoEnrichmentPolicy.ShouldFastTrack(count, sinceNewest))
                continue;

            var videoIds = group.Select(x => x.Id).ToList();

            var accepted = await _queue.EnqueueAsync(new VideoEnrichmentJob
            {
                OwnerId = group.Key,
                FetchPosters = true,
                FetchMetadata = true,
                StartedAtUtc = DateTimeOffset.UtcNow,
                VideoIds = videoIds,
                IsFastTrack = true
            }, cancellationToken);

            if (!accepted)
            {
                // A full/other job is already queued or running for this user — it will
                // handle these videos. Don't mark them attempted (in case it doesn't).
                _logger.LogDebug(
                    "Quick enrichment skipped for user {UserId}: a job is already queued/running",
                    group.Key);
                continue;
            }

            // Mark attempted so we don't re-enqueue videos that fail a TMDB match.
            foreach (var item in group)
                _attemptedVideoIds[item.Id] = item.CreatedAt;

            _logger.LogInformation(
                "Quick enrichment queued for user {UserId}: {Count} newly added videos",
                group.Key, count);
        }
    }
}
