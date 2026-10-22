using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Services.ModuleApis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Converges every existing account against the default-content template at start-up and daily.
/// </summary>
/// <remarks>
/// The sweep only enqueues work (add-only); the Files module performs the copies. It retries until
/// the module answers so an account created while the module was down is caught up, and it never
/// blocks boot because it runs as a hosted background service.
/// </remarks>
internal sealed class StartupUserContentSweepService : BackgroundService
{
    private const int BatchSize = 1000;
    private const int MaxStartupAttempts = 20;
    private static readonly TimeSpan StartupRetryDelay = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DailyInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StartupUserContentSweepService> _logger;

    public StartupUserContentSweepService(
        IServiceScopeFactory scopeFactory,
        ILogger<StartupUserContentSweepService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, stoppingToken);

            await ConvergeWithRetryAsync(stoppingToken);

            using var timer = new PeriodicTimer(DailyInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ConvergeWithRetryAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutting down.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Default-content convergence sweep stopped unexpectedly.");
        }
    }

    private async Task ConvergeWithRetryAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 1; attempt <= MaxStartupAttempts; attempt++)
        {
            var summary = await SweepOnceAsync(stoppingToken);

            if (summary.TotalUsers == 0 || summary.Queued + summary.Skipped > 0)
            {
                _logger.LogInformation(
                    "Default-content sweep: {Users} user(s), {Queued} queued, {Skipped} already queued.",
                    summary.TotalUsers, summary.Queued, summary.Skipped);
                return;
            }

            _logger.LogInformation(
                "Files module did not accept the default-content sweep yet (attempt {Attempt}/{Max}); retrying in {Delay}.",
                attempt, MaxStartupAttempts, StartupRetryDelay);

            try
            {
                await Task.Delay(StartupRetryDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        _logger.LogWarning(
            "Default-content sweep gave up after {Attempts} attempts; users will converge on the next daily pass.",
            MaxStartupAttempts);
    }

    private async Task<SweepSummary> SweepOnceAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var client = scope.ServiceProvider.GetService<IFilesApiClient>();

        var totalUsers = await db.Users.AsNoTracking().CountAsync(stoppingToken);
        if (totalUsers == 0 || client is null)
            return new SweepSummary(0, 0, 0);

        var queued = 0;
        var skipped = 0;
        var offset = 0;

        while (offset < totalUsers && !stoppingToken.IsCancellationRequested)
        {
            var batch = await db.Users
                .AsNoTracking()
                .OrderBy(u => u.Id)
                .Skip(offset)
                .Take(BatchSize)
                .Select(u => u.Id)
                .ToListAsync(stoppingToken);

            if (batch.Count == 0)
                break;

            var result = await client.EnqueueProvisioningAsync(batch, allowDeletions: false, stoppingToken);
            queued += result.Queued;
            skipped += result.Skipped;

            offset += batch.Count;
        }

        return new SweepSummary(totalUsers, queued, skipped);
    }

    private readonly record struct SweepSummary(int TotalUsers, int Queued, int Skipped);
}
