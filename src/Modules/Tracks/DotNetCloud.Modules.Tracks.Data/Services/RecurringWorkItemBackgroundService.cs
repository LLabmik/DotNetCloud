using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Background service that periodically checks for due recurring work item rules
/// and creates work items for them.
/// </summary>
public sealed class RecurringWorkItemBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecurringWorkItemBackgroundService> _logger;
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(15);

    public RecurringWorkItemBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<RecurringWorkItemBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RecurringWorkItemBackgroundService started. Tick interval: {Interval} minutes",
            TickInterval.TotalMinutes);

        // Wait a short time on startup to let the system stabilize
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        using var timer = new PeriodicTimer(TickInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<RecurringWorkItemService>();
                var createdIds = await service.ProcessDueRecurringItemsAsync(stoppingToken);

                if (createdIds.Count > 0)
                {
                    _logger.LogInformation("Created {Count} work items from recurring rules: {Ids}",
                        createdIds.Count, string.Join(", ", createdIds));
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing recurring work items");
            }
        }

        _logger.LogInformation("RecurringWorkItemBackgroundService stopped.");
    }
}
