using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services.Background;

/// <summary>
/// Background service that checks for shares about to expire and publishes
/// <see cref="ShareExpiringEvent"/> notifications. Only sends one notification
/// per share (tracked via <c>FileShare.ExpiryNotificationSentAt</c>).
/// </summary>
internal sealed class ShareExpiryNotificationService : BackgroundService
{
    /// <summary>How far before expiry to send the notification (24 hours).</summary>
    private static readonly TimeSpan NotificationWindow = TimeSpan.FromHours(24);

    /// <summary>How often to check for expiring shares.</summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ShareExpiryNotificationService> _logger;

    public ShareExpiryNotificationService(
        IServiceScopeFactory scopeFactory,
        ILogger<ShareExpiryNotificationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CheckExpiringSharesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for expiring shares");
            }
        }
    }

    /// <summary>
    /// Finds shares expiring within the notification window that haven't been notified yet,
    /// publishes events, and marks them as notified.
    /// </summary>
    internal async Task CheckExpiringSharesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

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

        foreach (var share in expiringShares)
        {
            await eventBus.PublishAsync(new ShareExpiringEvent
            {
                EventId = Guid.NewGuid(),
                CreatedAt = now,
                FileNodeId = share.FileNodeId,
                FileName = share.FileNode?.Name ?? "Unknown",
                ShareId = share.Id,
                CreatedByUserId = share.CreatedByUserId,
                ExpiresAt = share.ExpiresAt!.Value
            }, CallerContext.CreateSystemContext(), cancellationToken);

            share.ExpiryNotificationSentAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Sent {Count} share expiry notifications", expiringShares.Count);
    }
}
