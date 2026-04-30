using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Background service that periodically retries failed webhook deliveries
/// with exponential backoff (1min → 5min → 15min → 1h → 6h → 24h → 24h, max 7 retries).
/// </summary>
public sealed class WebhookRetryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebhookRetryBackgroundService> _logger;
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookRetryBackgroundService"/> class.
    /// </summary>
    public WebhookRetryBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<WebhookRetryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WebhookRetryBackgroundService started. Tick interval: {Interval}s",
            TickInterval.TotalSeconds);

        // Warmup delay to let system stabilize
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        using var timer = new PeriodicTimer(TickInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var deliveryService = scope.ServiceProvider.GetRequiredService<WebhookDeliveryService>();
                var webhookService = scope.ServiceProvider.GetRequiredService<WebhookService>();

                var pendingRetries = await deliveryService.GetPendingRetriesAsync(stoppingToken);

                if (pendingRetries.Count > 0)
                {
                    _logger.LogInformation("Processing {Count} pending webhook retries", pendingRetries.Count);

                    foreach (var delivery in pendingRetries)
                    {
                        if (stoppingToken.IsCancellationRequested) break;

                        if (delivery.Subscription is null || !delivery.Subscription.IsActive)
                            continue;

                        try
                        {
                            await deliveryService.RetryDeliveryAsync(delivery, delivery.Subscription, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Webhook retry failed for delivery {DeliveryId}", delivery.Id);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in webhook retry loop");
            }
        }

        _logger.LogInformation("WebhookRetryBackgroundService stopped.");
    }
}
