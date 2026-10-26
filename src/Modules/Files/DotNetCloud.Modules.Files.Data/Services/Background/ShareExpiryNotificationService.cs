using System.Diagnostics;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FileShare = DotNetCloud.Modules.Files.Models.FileShare;

namespace DotNetCloud.Modules.Files.Data.Services.Background;

/// <summary>
/// Background service that checks for shares about to expire and publishes
/// <see cref="ShareExpiringEvent"/> notifications. Only sends one notification
/// per share (tracked via <c>FileShare.ExpiryNotificationSentAt</c>).
/// </summary>
internal sealed class ShareExpiryNotificationService : BackgroundService
{
    private const string ServiceName = "Share Expiry Notification";

    /// <summary>How far before expiry to send the notification (24 hours).</summary>
    private static readonly TimeSpan NotificationWindow = TimeSpan.FromHours(24);

    /// <summary>How often to check for expiring shares.</summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ShareExpiryNotificationService> _logger;
    private readonly IBackgroundServiceTracker _tracker;

    public ShareExpiryNotificationService(
        IServiceScopeFactory scopeFactory,
        ILogger<ShareExpiryNotificationService> logger,
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

        using var timer = new PeriodicTimer(CheckInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCycleAsync("scheduled", stoppingToken);
        }
    }

    private async Task RunCycleAsync(string trigger, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        _tracker.RecordRunStarting(ServiceName);
        try
        {
            _logger.LogInformation("{Service} cycle starting ({Trigger})", ServiceName, trigger);
            await CheckExpiringSharesAsync(ct);
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

    /// <summary>
    /// Finds shares expiring within the notification window that haven't been notified yet, raises a
    /// platform event for each, and marks the published ones as notified.
    /// </summary>
    /// <remarks>
    /// The module owns the decision (which share is about to expire) and the bookkeeping
    /// (<c>ExpiryNotificationSentAt</c>). Delivery belongs to the core: the event goes out over the
    /// authenticated module transport and the core's notification pipeline decides the channels. A
    /// share is only marked as notified once the core has accepted its event, so a failure is retried
    /// on the next cycle instead of being silently dropped.
    /// </remarks>
    internal async Task CheckExpiringSharesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPlatformEventPublisher>();

        var now = DateTime.UtcNow;
        var windowEnd = now + NotificationWindow;

        var expiringShares = await db.FileShares
            .Include(s => s.FileNode)
            .Where(s => s.ExpiresAt != null
                        && s.ExpiresAt.Value > now
                        && s.ExpiresAt.Value <= windowEnd
                        && s.ExpiryNotificationSentAt == null)
            .ToListAsync(cancellationToken);

        if (expiringShares.Count == 0)
            return;

        _logger.LogInformation("Found {Count} shares expiring within {Window}h",
            expiringShares.Count, NotificationWindow.TotalHours);

        var published = new List<FileShare>();

        foreach (var share in expiringShares)
        {
            try
            {
                await publisher.PublishAsync(new ShareExpiringEvent
                {
                    EventId = Guid.CreateVersion7(),
                    CreatedAt = now,
                    FileNodeId = share.FileNodeId,
                    FileName = share.FileNode?.Name ?? "Unknown",
                    ShareId = share.Id,
                    CreatedByUserId = share.CreatedByUserId,
                    ExpiresAt = share.ExpiresAt!.Value
                }, cancellationToken);

                published.Add(share);
            }
            catch (PlatformEventPublishException ex)
            {
                // Leave ExpiryNotificationSentAt unset so the next cycle retries this share.
                _logger.LogWarning(ex,
                    "Share expiry notification was not published for share {ShareId}; will retry",
                    share.Id);
            }
        }

        if (published.Count == 0)
            return;

        foreach (var share in published)
            share.ExpiryNotificationSentAt = now;

        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Published {Count} share expiry notifications", published.Count);
    }
}
