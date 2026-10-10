using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services.Background;

/// <summary>
/// One-shot, bounded backfill of <see cref="FileNode.CapturedAtUtc"/> for image nodes that do not yet
/// have it, reading the EXIF capture timestamp from each image's content.
/// </summary>
/// <remarks>
/// <para>
/// The core supervisor never sends the module lifecycle <c>Initialize</c> RPC, so a module host's
/// <c>FileUploadedEvent</c> handlers (thumbnail generation, capture) never run in production. New
/// uploads are captured inline at upload completion; this service covers media that predates that hook
/// (and any upload path that did not capture it) so photo listings show the taken date immediately.
/// </para>
/// <para>
/// The sweep is keyset-paginated (ordered by <c>CreatedAt</c>) so it always advances past images that
/// carry no EXIF date, and is capped per run so a large library cannot stall the host indefinitely.
/// </para>
/// </remarks>
internal sealed class MediaCaptureBackfillService : BackgroundService
{
    private const string ServiceName = "Media Capture-Time Backfill";
    private const int BatchSize = 25;
    private const int MaxNodesPerRun = 5000;
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MediaCaptureBackfillService> _logger;
    private readonly IBackgroundServiceTracker _tracker;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaCaptureBackfillService"/> class.
    /// </summary>
    /// <param name="scopeFactory">Scope factory for resolving scoped services.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="tracker">Background service run tracker.</param>
    public MediaCaptureBackfillService(
        IServiceScopeFactory scopeFactory,
        ILogger<MediaCaptureBackfillService> logger,
        IBackgroundServiceTracker tracker)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _tracker = tracker;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // AddFilesServices is also registered by the Photo/Music/Video hosts (they reuse Files
        // services), so without this guard the sweep would run — and duplicate the disk work — in
        // every one of those processes. Run only in the Files host.
        var moduleId = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_ID");
        if (!string.IsNullOrEmpty(moduleId)
            && !string.Equals(moduleId, "dotnetcloud.files", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("{Service} skipped: running in host '{ModuleId}', not dotnetcloud.files.", ServiceName, moduleId);
            return;
        }

        try
        {
            // Let the host finish starting (DB migrations, other startup work) before the sweep.
            await Task.Delay(StartupDelay, stoppingToken);
            await BackfillAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host is shutting down — nothing to do.
        }
        catch (Exception ex)
        {
            _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, TimeSpan.Zero, success: false, message: ex.Message);
            _logger.LogError(ex, "Error during {Service}", ServiceName);
        }
    }

    /// <summary>
    /// Runs the whole backfill once. Exposed for tests; the hosted service calls it shortly after startup.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of image nodes examined.</returns>
    internal async Task<int> BackfillAsync(CancellationToken cancellationToken)
    {
        var total = 0;
        DateTime? after = null;

        while (!cancellationToken.IsCancellationRequested && total < MaxNodesPerRun)
        {
            var batch = await ProcessBatchAsync(after, cancellationToken);
            if (batch.Count == 0)
                break;

            total += batch.Count;
            after = batch[^1].CreatedAt;
        }

        if (total > 0)
        {
            _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, TimeSpan.Zero, success: true);
            _logger.LogInformation("{Service}: examined {Count} image(s) for capture time.", ServiceName, total);
        }

        return total;
    }

    private async Task<List<Candidate>> ProcessBatchAsync(DateTime? after, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
        var downloadService = scope.ServiceProvider.GetRequiredService<IDownloadService>();
        var captureTimeService = scope.ServiceProvider.GetRequiredService<IMediaCaptureTimeService>();

        var batch = await db.FileNodes
            .AsNoTracking()
            .Where(n => n.CapturedAtUtc == null
                        && n.MimeType != null
                        && n.MimeType.StartsWith("image/")
                        && (after == null || n.CreatedAt > after))
            .OrderBy(n => n.CreatedAt)
            .Take(BatchSize)
            .Select(n => new Candidate(n.Id, n.OwnerId, n.MimeType!, n.CreatedAt))
            .ToListAsync(cancellationToken);

        foreach (var node in batch)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var caller = new CallerContext(node.OwnerId, [], CallerType.System);
                await using var content = await downloadService.DownloadCurrentAsync(node.Id, caller, cancellationToken);
                await captureTimeService.TryCaptureFromStreamAsync(node.Id, content, node.MimeType, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Content may be unavailable (unsupported format, missing blob) — skip and keep going.
                _logger.LogWarning(ex, "Failed to backfill capture time for node {FileNodeId}.", node.Id);
            }
        }

        return batch;
    }

    private sealed record Candidate(Guid Id, Guid OwnerId, string MimeType, DateTime CreatedAt);
}
