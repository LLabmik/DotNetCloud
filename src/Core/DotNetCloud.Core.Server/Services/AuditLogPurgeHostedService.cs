using DotNetCloud.Core.Constants;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Audit;
using DotNetCloud.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Daily background job that enforces the audit-log retention window (SOC 2 C2 / P6).
/// </summary>
/// <remarks>
/// <para>
/// Every 24 hours this service deletes <see cref="AuditLog"/> rows older than
/// <c>core.AuditLogRetentionDays</c> (default 365) and logs how many rows were purged
/// — so the purge action itself is visible to operators and auditors.
/// </para>
/// <para>
/// The retention window is read from the <c>SystemSetting</c> store so administrators
/// can change it at runtime without redeploying.
/// </para>
/// </remarks>
public sealed class AuditLogPurgeHostedService : BackgroundService
{
    private const string Module = "dotnetcloud.core";
    private const string ServiceName = "Audit Log Purge Hosted Service";
    private static readonly TimeSpan PurgeInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBackgroundServiceTracker _tracker;
    private readonly ILogger<AuditLogPurgeHostedService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogPurgeHostedService"/> class.
    /// </summary>
    public AuditLogPurgeHostedService(
        IServiceScopeFactory scopeFactory,
        IBackgroundServiceTracker tracker,
        ILogger<AuditLogPurgeHostedService> logger)
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
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var ok = true;
            var message = string.Empty;
            try
            {
                var purged = await PurgeExpiredAuditLogsAsync(stoppingToken);
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

            sw.Stop();
            _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, sw.Elapsed, ok, message);

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
    /// Reads the configured retention window and deletes audit rows older than it.
    /// </summary>
    /// <returns>The number of rows purged.</returns>
    public async Task<long> PurgeExpiredAuditLogsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<IAdminSettingsService>();

        var retentionDays = await ReadRetentionDaysAsync(settings);
        if (retentionDays <= 0)
        {
            _logger.LogInformation("{ServiceName}: retention disabled (0), skipping purge.", ServiceName);
            return 0;
        }

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

        // Delete in batches to avoid long-running transactions / table locks.
        long totalPurged = 0;
        while (true)
        {
            var batch = await db.AuditLogs
                .Where(a => a.TimestampUtc < cutoff)
                .OrderBy(a => a.TimestampUtc)
                .Take(500)
                .Select(a => a.Id)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
                break;

            await db.AuditLogs
                .Where(a => batch.Contains(a.Id))
                .ExecuteDeleteAsync(cancellationToken);

            totalPurged += batch.Count;
        }

        if (totalPurged > 0)
        {
            _logger.LogInformation(
                "{ServiceName}: purged {Count} audit log rows older than {Cutoff} (retention {Days} days).",
                ServiceName, totalPurged, cutoff.ToString("O"), retentionDays);
        }
        else
        {
            _logger.LogDebug("{ServiceName}: no audit rows older than {Cutoff} (retention {Days} days).",
                ServiceName, cutoff.ToString("O"), retentionDays);
        }

        return totalPurged;
    }

    private static async Task<int> ReadRetentionDaysAsync(IAdminSettingsService settings)
    {
        var setting = await settings.GetSettingAsync(Module, SystemSettingKeys.AuditLogRetentionDays);
        if (setting is null || string.IsNullOrWhiteSpace(setting.Value))
            return int.Parse(SystemSettingKeys.AuditLogRetentionDaysDefault);

        return int.TryParse(setting.Value, out var days) ? days : int.Parse(SystemSettingKeys.AuditLogRetentionDaysDefault);
    }
}
