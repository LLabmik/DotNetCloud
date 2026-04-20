using System.Diagnostics;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services.Background;

/// <summary>
/// Background service that periodically cleans up expired upload sessions and orphaned chunks.
/// Runs every hour.
/// </summary>
internal sealed class UploadSessionCleanupService : BackgroundService
{
    private const string ServiceName = "Upload Session Cleanup";
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UploadSessionCleanupService> _logger;
    private readonly IBackgroundServiceTracker _tracker;

    public UploadSessionCleanupService(IServiceScopeFactory scopeFactory, ILogger<UploadSessionCleanupService> logger, IBackgroundServiceTracker tracker)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _tracker = tracker;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run immediately on startup
        await RunCycleAsync("initial", stoppingToken);

        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCycleAsync("scheduled", stoppingToken);
        }
    }

    private async Task RunCycleAsync(string trigger, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("{Service} cycle starting ({Trigger})", ServiceName, trigger);
            await CleanupAsync(ct);
            sw.Stop();
            _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, sw.Elapsed, success: true);
            _logger.LogInformation("{Service} cycle completed in {Elapsed:F1}s", ServiceName, sw.Elapsed.TotalSeconds);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
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

    internal async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
        var storageEngine = scope.ServiceProvider.GetRequiredService<IFileStorageEngine>();

        var now = DateTime.UtcNow;

        // Expire stale in-progress sessions
        var expiredSessions = await db.UploadSessions
            .Where(s => s.Status == UploadSessionStatus.InProgress && s.ExpiresAt < now)
            .ToListAsync(cancellationToken);

        foreach (var session in expiredSessions)
        {
            session.Status = UploadSessionStatus.Expired;
            session.UpdatedAt = now;
        }

        if (expiredSessions.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Expired {Count} stale upload sessions", expiredSessions.Count);
        }

        // GC orphaned chunks: chunks with no file version references (ReferenceCount = 0)
        // These arise from sessions that were cancelled, expired, or had network failures
        // Also verify no actual FK references exist (guards against stale ReferenceCount)
        var orphanChunks = await db.FileChunks
            .Where(c => c.ReferenceCount <= 0)
            .Where(c => !db.FileVersionChunks.Any(vc => vc.FileChunkId == c.Id))
            .ToListAsync(cancellationToken);

        foreach (var chunk in orphanChunks)
        {
            try
            {
                await storageEngine.DeleteAsync(chunk.StoragePath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete orphan chunk storage at {Path}", chunk.StoragePath);
            }

            db.FileChunks.Remove(chunk);
        }

        if (orphanChunks.Count > 0)
        {
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Garbage-collected {Count} orphaned chunks from failed upload sessions", orphanChunks.Count);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another service (e.g. TrashCleanup) already deleted some of these chunks
                _logger.LogDebug("Chunk GC had concurrency conflict (another service already cleaned some chunks)");
            }
        }
    }
}
