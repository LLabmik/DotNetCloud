using System.Diagnostics;
using DotNetCloud.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services.Background;

/// <summary>
/// Background service that periodically deletes expired shares from the database.
/// Runs every 6 hours and removes shares whose <c>ExpiresAt</c> is in the past.
/// </summary>
internal sealed class ExpiredShareCleanupService : BackgroundService
{
    private const string ServiceName = "Expired Share Cleanup";
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredShareCleanupService> _logger;
    private readonly IBackgroundServiceTracker _tracker;

    public ExpiredShareCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<ExpiredShareCleanupService> logger,
        IBackgroundServiceTracker tracker)
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

        using var timer = new PeriodicTimer(CleanupInterval);

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

        var now = DateTime.UtcNow;

        var expiredShares = await db.FileShares
            .Where(s => s.ExpiresAt != null && s.ExpiresAt.Value < now)
            .ToListAsync(cancellationToken);

        if (expiredShares.Count == 0)
            return;

        db.FileShares.RemoveRange(expiredShares);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted {Count} expired share(s)", expiredShares.Count);
    }
}
