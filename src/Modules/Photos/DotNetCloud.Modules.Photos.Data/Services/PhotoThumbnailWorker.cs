using System.Diagnostics;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Photos.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Photos.Data.Services;

/// <summary>
/// Drains the <see cref="IPhotoThumbnailQueue"/> and generates thumbnails off the import path.
/// Runs in whichever host performs photo indexing (Core.Server for library imports, the Photos
/// module host for upload events), and is safe to run in more than one process because each
/// process owns its own queue.
/// </summary>
internal sealed class PhotoThumbnailWorker : BackgroundService
{
    private readonly IPhotoThumbnailQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBackgroundServiceTracker? _tracker;
    private readonly ILogger<PhotoThumbnailWorker> _logger;

    private const string ServiceName = "Photo Thumbnail Worker";

    /// <summary>Initializes the background thumbnail worker.</summary>
    public PhotoThumbnailWorker(
        IPhotoThumbnailQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<PhotoThumbnailWorker> logger,
        IBackgroundServiceTracker? tracker = null)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _tracker = tracker;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            PhotoThumbnailWorkItem item;
            try
            {
                item = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var stopwatch = Stopwatch.StartNew();
            _tracker?.RecordRunStarting(ServiceName);
            var runSuccess = true;
            string? runMessage = null;
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var thumbnails = scope.ServiceProvider.GetRequiredService<IPhotoThumbnailService>();
                await thumbnails.GenerateThumbnailsAsync(
                    item.PhotoId, item.StoragePath, item.MimeType, stoppingToken);

                _logger.LogDebug(
                    "Background thumbnails generated for photo {PhotoId} in {ElapsedMs} ms ({Pending} queued)",
                    item.PhotoId, stopwatch.ElapsedMilliseconds, _queue.PendingCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                runSuccess = false;
                runMessage = ex.Message;

                // A single unreadable/corrupt image must not stop thumbnail generation for the rest.
                _logger.LogWarning(ex, "Background thumbnail generation failed for photo {PhotoId}", item.PhotoId);
            }
            finally
            {
                _tracker?.RecordRun(ServiceName, DateTimeOffset.UtcNow, stopwatch.Elapsed, runSuccess, runMessage);
            }
        }
    }
}
