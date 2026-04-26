using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DotNetCloud.Modules.Video.Services;
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

    public ValueTask<bool> EnqueueAsync(VideoEnrichmentJob job, CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            if (!_activeOrQueuedUsers.Add(job.OwnerId))
                return ValueTask.FromResult(false);
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

        var progress = new Progress<Core.DTOs.EnrichmentProgress>(report =>
        {
            _logger.LogDebug("Video enrichment [{Phase}] {Current}/{Total}: {Item}",
                report.Phase, report.Current, report.Total, report.CurrentItem);
        });

        try
        {
            if (job.FetchPosters)
                await enrichmentService.EnrichVideosWithoutPosterAsync(job.OwnerId, progress, stoppingToken);

            if (job.FetchMetadata)
                await enrichmentService.EnrichAllAsync(job.OwnerId, progress, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Background video enrichment cancelled for user {UserId}", job.OwnerId);
        }
    }
}
