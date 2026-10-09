using System.Diagnostics;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services.Background;

/// <summary>
/// Periodic reconciler and safety net for whole-file media blobs. Enumerates the <c>files/</c> blob
/// pool on disk and deletes any blob that no <see cref="Models.FileVersion"/> or
/// <see cref="Models.FileNode"/> references, plus any leftover <c>*.tmp-*</c> scratch files from an
/// interrupted atomic write.
/// </summary>
/// <remarks>
/// Normal deletion paths reclaim blobs eagerly; this sweep catches leaks from a crash between the
/// atomic blob rename and the <c>FileVersion.IsChunked</c> database flip.
/// </remarks>
internal sealed class WholeFileBlobSweepService : BackgroundService
{
    private const string ServiceName = "Whole-File Blob Sweep";
    private const string BlobPrefix = "files";
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WholeFileBlobSweepService> _logger;
    private readonly IBackgroundServiceTracker _tracker;

    /// <summary>
    /// Initializes a new instance of the <see cref="WholeFileBlobSweepService"/> class.
    /// </summary>
    /// <param name="scopeFactory">Scope factory for resolving scoped database context.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="tracker">Background service run tracker.</param>
    public WholeFileBlobSweepService(
        IServiceScopeFactory scopeFactory,
        ILogger<WholeFileBlobSweepService> logger,
        IBackgroundServiceTracker tracker)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _tracker = tracker;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunCycleAsync("initial", stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCycleAsync("scheduled", stoppingToken);
        }
    }

    private async Task RunCycleAsync(string trigger, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            _logger.LogDebug("{Service} cycle starting ({Trigger})", ServiceName, trigger);
            await SweepOnceAsync(cancellationToken);
            sw.Stop();
            _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, sw.Elapsed, success: true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, sw.Elapsed, success: false, message: ex.Message);
            _logger.LogError(ex, "Error during {Service}", ServiceName);
        }
    }

    /// <summary>
    /// Runs a single reconciliation pass. Exposed for tests; the hosted service calls it on a timer.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task SweepOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
        var storageEngine = scope.ServiceProvider.GetRequiredService<IFileStorageEngine>();

        var referencedPaths = await GetReferencedPathsAsync(db, cancellationToken);

        var scanned = 0;
        var deleted = 0;
        var scratchDeleted = 0;

        await foreach (var path in storageEngine.EnumerateStoragePathsAsync(BlobPrefix, cancellationToken))
        {
            scanned++;

            // Leftover scratch file from an interrupted atomic write — always safe to remove.
            if (Path.GetFileName(path).Contains(".tmp-", StringComparison.Ordinal))
            {
                try
                {
                    await storageEngine.DeleteAsync(path, cancellationToken);
                    scratchDeleted++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete leftover scratch file {Path}", path);
                }

                continue;
            }

            if (referencedPaths.Contains(path))
                continue;

            try
            {
                await storageEngine.DeleteAsync(path, cancellationToken);
                deleted++;
                _logger.LogInformation("Swept unreferenced whole-file blob {Path}", path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete unreferenced whole-file blob {Path}", path);
            }
        }

        if (deleted > 0 || scratchDeleted > 0)
        {
            _logger.LogInformation(
                "Whole-file blob sweep: scanned {Scanned}, deleted {Deleted} unreferenced, removed {Scratch} scratch file(s).",
                scanned, deleted, scratchDeleted);
        }
    }

    private static async Task<HashSet<string>> GetReferencedPathsAsync(FilesDbContext db, CancellationToken cancellationToken)
    {
        var versionPaths = await db.FileVersions
            .AsNoTracking()
            .Where(v => v.StoragePath.StartsWith("files/"))
            .Select(v => v.StoragePath)
            .Distinct()
            .ToListAsync(cancellationToken);

        var nodePaths = await db.FileNodes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(n => n.StoragePath != null && n.StoragePath.StartsWith("files/"))
            .Select(n => n.StoragePath!)
            .Distinct()
            .ToListAsync(cancellationToken);

        var referenced = new HashSet<string>(versionPaths, StringComparer.Ordinal);
        referenced.UnionWith(nodePaths);
        return referenced;
    }
}
