using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Music.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Music.Data.Services;

internal sealed class InMemoryMusicEnrichmentBackgroundQueue : IMusicEnrichmentBackgroundQueue
{
    private readonly Channel<MusicEnrichmentJob> _channel = Channel.CreateUnbounded<MusicEnrichmentJob>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

    private readonly HashSet<Guid> _activeOrQueuedUsers = [];
    private readonly object _syncRoot = new();

    public ValueTask<bool> EnqueueAsync(MusicEnrichmentJob job, CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            if (!_activeOrQueuedUsers.Add(job.OwnerId))
            {
                return ValueTask.FromResult(false);
            }
        }

        return EnqueueCoreAsync(job, cancellationToken);
    }

    public async IAsyncEnumerable<MusicEnrichmentJob> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var job in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return job;
        }
    }

    public void MarkCompleted(Guid userId)
    {
        lock (_syncRoot)
        {
            _activeOrQueuedUsers.Remove(userId);
        }
    }

    private async ValueTask<bool> EnqueueCoreAsync(MusicEnrichmentJob job, CancellationToken cancellationToken)
    {
        await _channel.Writer.WriteAsync(job, cancellationToken);
        return true;
    }
}

internal sealed class MusicEnrichmentBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly InMemoryMusicEnrichmentBackgroundQueue _queue;
    private readonly ScanProgressState _scanProgressState;
    private readonly ILogger<MusicEnrichmentBackgroundService> _logger;

    public MusicEnrichmentBackgroundService(
        IServiceScopeFactory scopeFactory,
        InMemoryMusicEnrichmentBackgroundQueue queue,
        ScanProgressState scanProgressState,
        ILogger<MusicEnrichmentBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _scanProgressState = scanProgressState;
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
                _logger.LogWarning(ex, "Background music enrichment failed for user {UserId}", job.OwnerId);
                _scanProgressState.CompleteScan(job.OwnerId);
            }
            finally
            {
                _queue.MarkCompleted(job.OwnerId);
            }
        }
    }

    private async Task RunJobAsync(MusicEnrichmentJob job, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var enrichmentService = scope.ServiceProvider.GetRequiredService<IMetadataEnrichmentService>();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            stoppingToken,
            _scanProgressState.GetCancellationToken(job.OwnerId));

        IProgress<EnrichmentProgress> progress = new CallbackProgress<EnrichmentProgress>(report =>
        {
            _scanProgressState.UpdateProgress(job.OwnerId, new LibraryScanProgress
            {
                Phase = report.Phase,
                CurrentFile = report.CurrentItem,
                FilesProcessed = job.TotalFiles,
                TotalFiles = job.TotalFiles,
                TracksAdded = job.TracksAdded,
                TracksUpdated = job.TracksUpdated,
                TracksSkipped = job.TracksSkipped,
                TracksFailed = job.TracksFailed,
                TracksRemoved = job.TracksRemoved,
                AlbumArtFetched = report.AlbumArtFound,
                AlbumArtRemaining = report.AlbumArtRemaining,
                PercentComplete = 100,
                ElapsedTime = DateTimeOffset.UtcNow - job.StartedAtUtc
            });
        });

        try
        {
            if (job.FetchAlbumArt)
            {
                await enrichmentService.EnrichAlbumsWithoutArtAsync(job.OwnerId, progress, linkedCts.Token);
            }

            if (job.FetchMetadata)
            {
                await enrichmentService.EnrichAllAsync(job.OwnerId, progress, linkedCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Background music enrichment cancelled for user {UserId}", job.OwnerId);
        }
        finally
        {
            _scanProgressState.CompleteScan(job.OwnerId);
        }
    }

    private sealed class CallbackProgress<T> : IProgress<T>
    {
        private readonly Action<T> _callback;

        public CallbackProgress(Action<T> callback)
        {
            _callback = callback;
        }

        public void Report(T value)
        {
            _callback(value);
        }
    }
}