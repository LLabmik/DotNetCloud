using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services.Background;

/// <summary>
/// Drains the default-content provisioning queue. Work is claimed with a five-minute lease so a
/// crashed pass is reclaimed, and every automatic pass is add-only — destructive actions require
/// an explicit administrator/CLI run.
/// </summary>
internal sealed class DefaultUserContentRetryService : BackgroundService
{
    private const string ServiceName = "Default Content Provisioning";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private const int BatchSize = 10;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBackgroundServiceTracker _tracker;
    private readonly ILogger<DefaultUserContentRetryService> _logger;

    public DefaultUserContentRetryService(
        IServiceScopeFactory scopeFactory,
        IBackgroundServiceTracker tracker,
        ILogger<DefaultUserContentRetryService> logger)
    {
        _scopeFactory = scopeFactory;
        _tracker = tracker;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let migrations and the rest of the host settle before the first claim.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(PollInterval);

        do
        {
            await RunCycleAsync(stoppingToken).ConfigureAwait(false);
        }
        while (await WaitForNextTickAsync(timer, stoppingToken).ConfigureAwait(false));
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        _tracker.RecordRunStarting(ServiceName);
        try
        {
            for (var i = 0; i < BatchSize && !cancellationToken.IsCancellationRequested; i++)
            {
                var userId = await ClaimNextAsync(cancellationToken).ConfigureAwait(false);
                if (userId is null)
                    break;

                await ProcessAsync(userId.Value, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, TimeSpan.Zero, success: false, message: ex.Message);
            _logger.LogError(ex, "Default-content provisioning cycle failed.");
        }
    }

    private async Task<Guid?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
        var now = DateTime.UtcNow;

        var candidate = await db.UserContentProvisioning
            .AsNoTracking()
            .Where(p => (p.Status == UserContentProvisioningStatus.Pending
                         || p.Status == UserContentProvisioningStatus.Partial
                         || p.Status == UserContentProvisioningStatus.Failed)
                        && p.Attempts < DefaultUserContentQueue.MaxAttempts
                        || (p.Status == UserContentProvisioningStatus.InProgress
                            && p.LeaseExpiresAtUtc != null
                            && p.LeaseExpiresAtUtc < now))
            .OrderBy(p => p.UpdatedAtUtc)
            .Select(p => p.UserId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (candidate == Guid.Empty)
            return null;

        var lease = now.Add(LeaseDuration);

        if (ChunkReferenceHelper.IsInMemoryProvider(db))
        {
            var row = await db.UserContentProvisioning
                .FirstOrDefaultAsync(p => p.UserId == candidate, cancellationToken)
                .ConfigureAwait(false);

            if (row is null)
                return null;

            row.Status = UserContentProvisioningStatus.InProgress;
            row.LeaseExpiresAtUtc = lease;
            row.UpdatedAtUtc = now;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return candidate;
        }

        // Atomic claim: only the worker that flips the row out of an eligible state proceeds.
        var claimed = await db.UserContentProvisioning
            .Where(p => p.UserId == candidate
                        && (p.Status == UserContentProvisioningStatus.Pending
                            || p.Status == UserContentProvisioningStatus.Partial
                            || p.Status == UserContentProvisioningStatus.Failed
                            || (p.Status == UserContentProvisioningStatus.InProgress
                                && p.LeaseExpiresAtUtc != null
                                && p.LeaseExpiresAtUtc < now)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.Status, UserContentProvisioningStatus.InProgress)
                .SetProperty(p => p.LeaseExpiresAtUtc, lease)
                .SetProperty(p => p.UpdatedAtUtc, now), cancellationToken)
            .ConfigureAwait(false);

        return claimed > 0 ? candidate : null;
    }

    private async Task ProcessAsync(Guid userId, CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
        var provisioner = scope.ServiceProvider.GetRequiredService<DefaultUserContentProvisioner>();
        var settings = scope.ServiceProvider.GetRequiredService<IDefaultUserContentSettingsProvider>();

        var options = await settings.GetAsync(cancellationToken).ConfigureAwait(false);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Timeout);

        try
        {
            var result = await provisioner
                .ReconcileAsync(userId, ReconcileMode.AddOnly, timeout.Token)
                .ConfigureAwait(false);

            _tracker.RecordRun(ServiceName, started, DateTimeOffset.UtcNow - started,
                success: result.Status is not UserContentProvisioningStatus.Failed,
                message: result.Error);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await RecordFailureAsync(db, userId, "The pass exceeded the configured time budget.", cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await RecordFailureAsync(db, userId, ex.Message, cancellationToken).ConfigureAwait(false);
            _logger.LogError(ex, "Default-content pass failed for {UserId}.", userId);
        }
    }

    private static async Task RecordFailureAsync(
        FilesDbContext db,
        Guid userId,
        string message,
        CancellationToken cancellationToken)
    {
        var row = await db.UserContentProvisioning
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
            return;

        row.Attempts++;
        row.Status = row.Attempts >= DefaultUserContentQueue.MaxAttempts
            ? UserContentProvisioningStatus.Failed
            : UserContentProvisioningStatus.Pending;
        row.LastError = message.Length > 1024 ? message[..1024] : message;
        row.LeaseExpiresAtUtc = null;
        row.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
