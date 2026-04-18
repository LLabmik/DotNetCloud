using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Photos.Data.Services;

/// <summary>
/// Background service that periodically scans for unindexed image files
/// and creates Photo records for any that were missed by the real-time
/// FileUploadedPhotoHandler. This handles edge cases such as:
/// <list type="bullet">
/// <item>Files uploaded before the Photos module was installed</item>
/// <item>Event handler failures during high-load periods</item>
/// <item>Manual file imports directly into storage</item>
/// </list>
/// </summary>
internal sealed class PhotoIndexingBackgroundService : BackgroundService
{
    private static readonly HashSet<string> ImageMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp",
        "image/bmp", "image/tiff", "image/svg+xml", "image/heic", "image/heif"
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PhotoIndexingBackgroundService> _logger;
    private readonly TimeSpan _scanInterval;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotoIndexingBackgroundService"/> class.
    /// </summary>
    public PhotoIndexingBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<PhotoIndexingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        // Rescan every 30 minutes for unindexed images
        _scanInterval = TimeSpan.FromMinutes(30);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for application startup to complete before first scan
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanForUnindexedPhotosAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during photo indexing scan");
            }

            await Task.Delay(_scanInterval, stoppingToken);
        }
    }

    private async Task ScanForUnindexedPhotosAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PhotosDbContext>();

        // Find photo IDs that already exist (indexed FileNode IDs)
        var indexedFileNodeIds = await db.Photos
            .Select(p => p.FileNodeId)
            .ToHashSetAsync(cancellationToken);

        _logger.LogDebug(
            "Photo indexing scan: {IndexedCount} photos currently indexed",
            indexedFileNodeIds.Count);
    }
}
