using DotNetCloud.Core.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Fans a persisted notification out to all configured delivery channels.
/// Per-channel failures are logged and do not affect other channels.
/// </summary>
internal sealed class NotificationFanOutDispatcher : IEventHandler<NotificationCreatedEvent>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationFanOutDispatcher> _logger;

    public NotificationFanOutDispatcher(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationFanOutDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(NotificationCreatedEvent e, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var channels = scope.ServiceProvider.GetRequiredService<IEnumerable<INotificationChannel>>();

        foreach (var channel in channels)
        {
            try
            {
                await channel.DeliverAsync(e.Notification, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notification channel {Channel} failed for notification {NotificationId}",
                    channel.GetType().Name, e.Notification.Id);
            }
        }
    }
}
