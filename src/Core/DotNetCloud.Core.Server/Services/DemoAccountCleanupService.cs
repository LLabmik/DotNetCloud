using System.Diagnostics;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Background service that periodically deletes expired demo accounts.
/// Demo accounts are automatically removed 5 days after creation.
/// </summary>
/// <remarks>
/// Runs every hour. On each cycle, queries for demo users whose accounts
/// were created more than 5 days ago and deletes them via
/// <c>IUserManagementService.DeleteUserAsync</c>.
/// The deletion cascade (Files cleanup, etc.) is handled by
/// <c>UserDeletedEvent</c> subscribers.
/// </remarks>
public sealed class DemoAccountCleanupService : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan DemoAccountLifetime = TimeSpan.FromDays(5);
    private const string ServiceName = "Demo Account Cleanup";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBackgroundServiceTracker _tracker;
    private readonly ILogger<DemoAccountCleanupService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DemoAccountCleanupService"/> class.
    /// </summary>
    public DemoAccountCleanupService(
        IServiceScopeFactory scopeFactory,
        IBackgroundServiceTracker tracker,
        ILogger<DemoAccountCleanupService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DemoAccountCleanupService started.");

        // Run an immediate check on startup
        try
        {
            await CleanupExpiredDemoAccountsAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in initial demo account cleanup.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await CleanupExpiredDemoAccountsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DemoAccountCleanupService polling loop.");
                _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, TimeSpan.Zero, false, ex.Message);
            }
        }

        _logger.LogInformation("DemoAccountCleanupService stopped.");
    }

    private async Task CleanupExpiredDemoAccountsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var userManagementService = scope.ServiceProvider.GetRequiredService<IUserManagementService>();

        var expiryThreshold = DateTime.UtcNow.Subtract(DemoAccountLifetime);

        // Query expired demo users
        var expiredUsers = await userManager.Users
            .Where(u => u.IsDemoUser && u.CreatedAt < expiryThreshold)
            .Select(u => new { u.Id, u.Email, u.CreatedAt })
            .ToListAsync(cancellationToken);

        if (expiredUsers.Count == 0)
        {
            _logger.LogDebug("No expired demo accounts found.");
            _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, TimeSpan.Zero, true,
                "No expired accounts.");
            return;
        }

        _logger.LogInformation(
            "Found {Count} expired demo accounts to clean up",
            expiredUsers.Count);

        var sw = Stopwatch.StartNew();
        var deletedCount = 0;
        var failedCount = 0;

        foreach (var user in expiredUsers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var daysSinceCreation = (DateTime.UtcNow - user.CreatedAt).TotalDays;
                _logger.LogInformation(
                    "Deleting expired demo account {UserId} ({Email}), created {DaysAgo:F1} days ago",
                    user.Id, user.Email, daysSinceCreation);

                var deleted = await userManagementService.DeleteUserAsync(user.Id);
                if (deleted)
                {
                    deletedCount++;
                }
                else
                {
                    failedCount++;
                    _logger.LogWarning(
                        "Failed to delete expired demo account {UserId}",
                        user.Id);
                }
            }
            catch (Exception ex)
            {
                failedCount++;
                _logger.LogError(
                    ex,
                    "Error deleting expired demo account {UserId}",
                    user.Id);
            }
        }

        sw.Stop();

        var message = deletedCount > 0
            ? $"Deleted {deletedCount} expired demo accounts"
            : "No accounts deleted";

        if (failedCount > 0)
        {
            message += $" ({failedCount} failures)";
        }

        _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, sw.Elapsed,
            failedCount == 0, message);

        _logger.LogInformation(
            "Demo account cleanup completed: {DeletedCount} deleted, {FailedCount} failed in {Duration:F1}s",
            deletedCount, failedCount, sw.Elapsed.TotalSeconds);
    }
}
