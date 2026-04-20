using System.Diagnostics;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Files.Data.Services.Background;

/// <summary>
/// Background service that periodically recalculates storage quotas for all users.
/// Interval and trashed-item inclusion are controlled via <see cref="QuotaOptions"/>.
/// </summary>
internal sealed class QuotaRecalculationService : BackgroundService
{
    private const string ServiceName = "Quota Recalculation";
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<QuotaOptions> _options;
    private readonly ILogger<QuotaRecalculationService> _logger;
    private readonly IBackgroundServiceTracker _tracker;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuotaRecalculationService"/> class.
    /// </summary>
    public QuotaRecalculationService(
        IServiceScopeFactory scopeFactory,
        IOptions<QuotaOptions> options,
        ILogger<QuotaRecalculationService> logger,
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

        var interval = _options.Value.RecalculationInterval;
        using var timer = new PeriodicTimer(interval);

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
            await RecalculateAllAsync(ct);
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

    private async Task RecalculateAllAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
        var quotaService = scope.ServiceProvider.GetRequiredService<IQuotaService>();

        var userIds = await db.FileQuotas
            .AsNoTracking()
            .Select(q => q.UserId)
            .ToListAsync(cancellationToken);

        var count = 0;
        foreach (var userId in userIds)
        {
            await quotaService.RecalculateAsync(userId, cancellationToken);
            count++;
        }

        if (count > 0)
            _logger.LogInformation("Recalculated quotas for {Count} users", count);
    }
}
