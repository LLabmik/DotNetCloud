using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Background worker that retries queued push notifications with backoff.
/// </summary>
internal sealed class NotificationDeliveryBackgroundService : BackgroundService
{
    private const int MaxAttempts = 3;

    private readonly INotificationDeliveryQueue _queue;
    private readonly IQueuedNotificationDispatcher _dispatcher;
    private readonly ILogger<NotificationDeliveryBackgroundService> _logger;

    public NotificationDeliveryBackgroundService(
        INotificationDeliveryQueue queue,
        IQueuedNotificationDispatcher dispatcher,
        ILogger<NotificationDeliveryBackgroundService> logger)
    {
        _queue = queue;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                var delay = item.NextAttemptUtc - DateTime.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, stoppingToken);
                }

                var delivered = await _dispatcher.DispatchQueuedAsync(item.UserId, item.Notification, stoppingToken);
                if (delivered)
                {
                    continue;
                }

                await RequeueWithBackoffAsync(item, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Queued push delivery failed unexpectedly for user {UserId}", item.UserId);
                await RequeueWithBackoffAsync(item, stoppingToken);
            }
        }
    }

    private async Task RequeueWithBackoffAsync(QueuedPushNotification item, CancellationToken cancellationToken)
    {
        if (item.Attempt >= MaxAttempts)
        {
            _logger.LogWarning(
                "Queued push dropped after {Attempts} attempts for user {UserId}",
                item.Attempt,
                item.UserId);
            return;
        }

        var nextAttempt = item.Attempt + 1;
        var backoffSeconds = Math.Min(30, (int)Math.Pow(2, item.Attempt));

        await _queue.EnqueueAsync(
            item with
            {
                Attempt = nextAttempt,
                NextAttemptUtc = DateTime.UtcNow.AddSeconds(backoffSeconds)
            },
            cancellationToken);
    }
}
