using DotNetCloud.Modules.Video.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Background service that runs once daily to enqueue enrichment for any
/// videos that haven't been enriched yet (HasExternalPoster == false).
/// This ensures newly imported files eventually get TMDB posters and
/// fallback thumbnails without requiring a manual scan.
/// </summary>
internal sealed class DailyVideoEnrichmentService : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly InMemoryVideoEnrichmentBackgroundQueue _queue;
    private readonly ILogger<DailyVideoEnrichmentService> _logger;

    public DailyVideoEnrichmentService(
        IServiceScopeFactory scopeFactory,
        InMemoryVideoEnrichmentBackgroundQueue queue,
        ILogger<DailyVideoEnrichmentService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait on startup to let the system settle before running.
        await Task.Delay(StartupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnqueuePendingEnrichmentJobsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Daily video enrichment cycle failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task EnqueuePendingEnrichmentJobsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VideoDbContext>();

        // Find users who have videos that still need enrichment.
        var userIds = await db.UserVideos
            .Where(uv => !uv.IsDeleted
                && uv.CanonicalVideo != null
                && !uv.CanonicalVideo.HasExternalPoster)
            .Select(uv => uv.OwnerId)
            .Distinct()
            .ToListAsync(stoppingToken);

        if (userIds.Count == 0)
        {
            _logger.LogInformation("Daily enrichment: no users with pending videos");
            return;
        }

        var enqueued = 0;
        foreach (var userId in userIds)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            var accepted = await _queue.EnqueueAsync(new VideoEnrichmentJob
            {
                OwnerId = userId,
                FetchPosters = true,
                FetchMetadata = true,
                StartedAtUtc = DateTimeOffset.UtcNow,
            }, stoppingToken);

            if (accepted)
            {
                enqueued++;
                _logger.LogInformation("Daily enrichment queued for user {UserId}", userId);
            }
            else
            {
                _logger.LogDebug("Daily enrichment skipped for user {UserId} — job already queued or running", userId);
            }
        }

        _logger.LogInformation(
            "Daily enrichment cycle complete: {Enqueued} of {Total} users with pending videos",
            enqueued, userIds.Count);
    }
}
