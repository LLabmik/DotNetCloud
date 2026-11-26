using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Background job that deletes expired OpenIddict token rows from <c>core.OpenIddictTokens</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every access token, refresh token and identity token issued by the server is persisted as a row.
/// Nothing else deletes those rows once they expire, so the table grows without bound — on a long
/// running instance this is one of the largest tables in the database even though every row in it is
/// already unusable.
/// </para>
/// <para>
/// Only rows whose <c>ExpirationDate</c> has already passed are removed. An expired token can never be
/// redeemed, so this cannot affect an active session, and OpenIddict refresh-token reuse detection
/// (which only matters while a token is still within its validity window) is unaffected.
/// </para>
/// <para>
/// Deletion is batched to avoid long-running transactions and to keep the delete log small.
/// </para>
/// </remarks>
public sealed class ExpiredTokenPurgeHostedService : BackgroundService
{
    private const string ServiceName = "Expired Token Purge Hosted Service";
    private const int BatchSize = 1000;

    private static readonly TimeSpan PurgeInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBackgroundServiceTracker _tracker;
    private readonly ILogger<ExpiredTokenPurgeHostedService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpiredTokenPurgeHostedService"/> class.
    /// </summary>
    /// <param name="scopeFactory">Factory used to create a scope per purge pass.</param>
    /// <param name="tracker">Tracks the outcome of each pass for the admin system page.</param>
    /// <param name="logger">Logger for the pass outcome.</param>
    public ExpiredTokenPurgeHostedService(
        IServiceScopeFactory scopeFactory,
        IBackgroundServiceTracker tracker,
        ILogger<ExpiredTokenPurgeHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{ServiceName} started.", ServiceName);

        // Give the system time to fully start before the first purge pass.
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var ok = true;
            var message = string.Empty;
            _tracker.RecordRunStarting(ServiceName);
            try
            {
                var purged = await PurgeExpiredTokensAsync(stoppingToken);
                message = $"Purged {purged} rows";
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                ok = false;
                message = ex.Message;
                _logger.LogError(ex, "{ServiceName} failed during purge pass.", ServiceName);
            }

            stopwatch.Stop();
            _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, stopwatch.Elapsed, ok, message);

            try
            {
                await Task.Delay(PurgeInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("{ServiceName} stopped.", ServiceName);
    }

    /// <summary>
    /// Deletes every token row whose expiry has already passed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the pass.</param>
    /// <returns>The number of token rows deleted.</returns>
    public async Task<long> PurgeExpiredTokensAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var cutoff = DateTime.UtcNow;
        long totalPurged = 0;

        while (true)
        {
            var batch = await db.OpenIddictTokens
                .Where(t => t.ExpirationDate != null && t.ExpirationDate < cutoff)
                .OrderBy(t => t.ExpirationDate)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
                break;

            db.OpenIddictTokens.RemoveRange(batch);
            await db.SaveChangesAsync(cancellationToken);
            totalPurged += batch.Count;

            if (batch.Count < BatchSize)
                break;
        }

        if (totalPurged > 0)
        {
            _logger.LogInformation(
                "{ServiceName}: purged {Count} expired token rows (cutoff {Cutoff}).",
                ServiceName, totalPurged, cutoff.ToString("O"));
        }
        else
        {
            _logger.LogDebug("{ServiceName}: no expired token rows to purge.", ServiceName);
        }

        return totalPurged;
    }
}
