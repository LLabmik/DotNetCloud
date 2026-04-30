using DotNetCloud.Modules.Tracks.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Implements <see cref="IWebhookDispatchService"/> — dispatches domain events
/// to matching webhook subscriptions using scoped <see cref="WebhookService"/>
/// and <see cref="WebhookDeliveryService"/>.
/// </summary>
public sealed class WebhookDispatchService : IWebhookDispatchService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebhookDispatchService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookDispatchService"/> class.
    /// </summary>
    public WebhookDispatchService(IServiceScopeFactory scopeFactory, ILogger<WebhookDispatchService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task DispatchAsync(string eventType, object eventPayload, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var webhookService = scope.ServiceProvider.GetRequiredService<WebhookService>();
            var deliveryService = scope.ServiceProvider.GetRequiredService<WebhookDeliveryService>();

            var subscriptions = await webhookService.GetMatchingSubscriptionsAsync(eventType, ct);

            if (subscriptions.Count == 0) return;

            _logger.LogDebug("Dispatching {EventType} to {Count} webhook subscriptions",
                eventType, subscriptions.Count);

            foreach (var subscription in subscriptions)
            {
                try
                {
                    await deliveryService.DeliverAsync(subscription, eventType, eventPayload, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deliver webhook {EventType} to subscription {SubscriptionId}",
                        eventType, subscription.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dispatching webhook event {EventType}", eventType);
        }
    }
}
