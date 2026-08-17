using DotNetCloud.Core.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using IEventBus = DotNetCloud.Core.Events.IEventBus;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Subscribes cross-module notification event handlers on startup
/// and unsubscribes them on shutdown.
/// </summary>
internal sealed class NotificationEventSubscriber : IHostedService
{
    private readonly IEventBus _eventBus;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerFactory _loggerFactory;
    private NotificationProducer? _producer;
    private NotificationFanOutDispatcher? _fanOutDispatcher;

    public NotificationEventSubscriber(
        IEventBus eventBus,
        IServiceScopeFactory scopeFactory,
        ILoggerFactory loggerFactory)
    {
        _eventBus = eventBus;
        _scopeFactory = scopeFactory;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _producer = new NotificationProducer(_scopeFactory);

        _fanOutDispatcher = new NotificationFanOutDispatcher(
            _scopeFactory,
            _loggerFactory.CreateLogger<NotificationFanOutDispatcher>());

        await _eventBus.SubscribeAsync<FileSharedEvent>(_producer, cancellationToken);
        await _eventBus.SubscribeAsync<QuotaWarningEvent>(_producer, cancellationToken);
        await _eventBus.SubscribeAsync<QuotaCriticalEvent>(_producer, cancellationToken);
        await _eventBus.SubscribeAsync<PublicLinkAccessedEvent>(_producer, cancellationToken);
        await _eventBus.SubscribeAsync<ShareExpiringEvent>(_producer, cancellationToken);
        await _eventBus.SubscribeAsync<ResourceSharedEvent>(_producer, cancellationToken);
        await _eventBus.SubscribeAsync<UserMentionedEvent>(_producer, cancellationToken);
        await _eventBus.SubscribeAsync<ReminderTriggeredEvent>(_producer, cancellationToken);

        await _eventBus.SubscribeAsync<NotificationCreatedEvent>(_fanOutDispatcher, cancellationToken);

        _loggerFactory.CreateLogger<NotificationEventSubscriber>()
            .LogInformation("Notification producers + fan-out dispatcher subscribed (8 events -> 1 pipeline)");
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_producer is not null)
        {
            await _eventBus.UnsubscribeAsync<FileSharedEvent>(_producer, cancellationToken);
            await _eventBus.UnsubscribeAsync<QuotaWarningEvent>(_producer, cancellationToken);
            await _eventBus.UnsubscribeAsync<QuotaCriticalEvent>(_producer, cancellationToken);
            await _eventBus.UnsubscribeAsync<PublicLinkAccessedEvent>(_producer, cancellationToken);
            await _eventBus.UnsubscribeAsync<ShareExpiringEvent>(_producer, cancellationToken);
            await _eventBus.UnsubscribeAsync<ResourceSharedEvent>(_producer, cancellationToken);
            await _eventBus.UnsubscribeAsync<UserMentionedEvent>(_producer, cancellationToken);
            await _eventBus.UnsubscribeAsync<ReminderTriggeredEvent>(_producer, cancellationToken);
        }

        if (_fanOutDispatcher is not null)
        {
            await _eventBus.UnsubscribeAsync<NotificationCreatedEvent>(_fanOutDispatcher, cancellationToken);
        }
    }
}
