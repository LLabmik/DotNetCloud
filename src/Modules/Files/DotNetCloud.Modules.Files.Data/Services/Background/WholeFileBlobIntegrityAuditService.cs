using System.Diagnostics;
using DotNetCloud.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services.Background;

/// <summary>
/// Periodically audits whole-file media blobs and reports any version whose blob is missing.
/// </summary>
/// <remarks>
/// <see cref="WholeFileBlobSweepService"/> reclaims blobs that nothing references; this service is
/// the mirror image — it looks for whole-file versions that have no blob at all. A whole-file version
/// is normally the only copy of its content, so that state is silent data loss: the file still lists
/// normally and only fails when it is read. Reporting it here surfaces the loss as an error in the
/// logs and in the background-service status instead of leaving a user to discover it as a 404.
/// </remarks>
internal sealed class WholeFileBlobIntegrityAuditService : BackgroundService
{
    private const string ServiceName = "Whole-File Integrity Audit";
    private const int MaxReportedDefects = 50;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(12);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WholeFileBlobIntegrityAuditService> _logger;
    private readonly IBackgroundServiceTracker _tracker;

    /// <summary>
    /// Initializes a new instance of the <see cref="WholeFileBlobIntegrityAuditService"/> class.
    /// </summary>
    /// <param name="scopeFactory">Scope factory for resolving the scoped audit service.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="tracker">Background service run tracker.</param>
    public WholeFileBlobIntegrityAuditService(
        IServiceScopeFactory scopeFactory,
        ILogger<WholeFileBlobIntegrityAuditService> logger,
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
            var report = await AuditOnceAsync(cancellationToken);
            sw.Stop();
            RecordOutcome(report, sw.Elapsed);
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
    /// Runs a single audit against a fresh scope. Exposed for tests; the hosted service calls it on a timer.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task<WholeFileBlobIntegrityReport> AuditOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<IWholeFileBlobIntegrityService>();
        return await audit.AuditAsync(MaxReportedDefects, cancellationToken);
    }

    private void RecordOutcome(WholeFileBlobIntegrityReport report, TimeSpan elapsed)
    {
        if (report.IsHealthy)
        {
            _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, elapsed, success: true,
                message: $"{report.ScannedVersions} whole-file version(s) verified");
            _logger.LogDebug("{Service}: {Scanned} whole-file version(s) verified", ServiceName, report.ScannedVersions);
            return;
        }

        var summary =
            $"{report.Defects.Count}{(report.Truncated ? "+" : string.Empty)} of {report.ScannedVersions} whole-file version(s) " +
            $"have NO content on disk ({report.UnrecoverableCount} unrecoverable, {report.RecoverableCount} rebuildable from chunks)";

        _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, elapsed, success: false, message: summary);
        _logger.LogError("{Service}: {Summary}", ServiceName, summary);

        foreach (var defect in report.Defects.Take(MaxReportedDefects))
        {
            _logger.LogError(
                "{Service}: missing content — '{FileName}' (owner {OwnerId}, expected {Size} byte(s)) version {VersionId} at {StoragePath}: {Recovery}",
                ServiceName, defect.FileName, defect.OwnerId, defect.Size, defect.VersionId, defect.StoragePath,
                defect.IsRecoverable
                    ? "rebuildable from surviving chunks — the next download self-heals it"
                    : "UNRECOVERABLE — the file must be re-uploaded or restored from a backup");
        }
    }
}
