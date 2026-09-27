using System.Diagnostics;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Files.Data.Services.Background;

/// <summary>
/// Background service that schedules the file version retention pass.
/// </summary>
/// <remarks>
/// <para>The policy itself lives in <see cref="IVersionRetentionService"/>: the newest version of a
/// file is always kept, unlabeled versions beyond
/// <see cref="VersionRetentionOptions.MaxVersionCount"/> are pruned, unlabeled versions older than
/// <see cref="VersionRetentionOptions.RetentionDays"/> are pruned, and labeled versions are never
/// auto-deleted. Those values are resolved from the administrator settings on <c>/admin/files</c>
/// (falling back to the <c>Files:VersionRetention</c> configuration section), so an administrator can
/// change them without a restart.</para>
/// <para>The write paths (upload completion, WOPI save, restore) apply the policy immediately as
/// well, so this scheduled pass mainly reclaims versions that aged out since the last run.</para>
/// </remarks>
internal sealed class VersionCleanupService : BackgroundService
{
    private const string ServiceName = "Version Cleanup";
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<VersionRetentionOptions> _options;
    private readonly ILogger<VersionCleanupService> _logger;
    private readonly IBackgroundServiceTracker _tracker;

    public VersionCleanupService(
        IServiceScopeFactory scopeFactory,
        IOptions<VersionRetentionOptions> options,
        ILogger<VersionCleanupService> logger,
        IBackgroundServiceTracker tracker)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
        _tracker = tracker;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run immediately on startup
        await RunCycleAsync("initial", stoppingToken);

        var opts = _options.Value;
        using var timer = new PeriodicTimer(opts.CleanupInterval);

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

    /// <summary>
    /// Runs one retention pass. Exposed internally for testing.
    /// </summary>
    internal async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var retention = scope.ServiceProvider.GetRequiredService<IVersionRetentionService>();

        var result = await retention.RunAsync(cancellationToken);

        if (result.VersionsDeleted > 0)
        {
            _logger.LogInformation("{Service}: removed {Count} versions", ServiceName, result.VersionsDeleted);
        }
    }
}
