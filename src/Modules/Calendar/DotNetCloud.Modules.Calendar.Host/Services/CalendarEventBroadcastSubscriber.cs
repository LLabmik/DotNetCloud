using DotNetCloud.Core.Events;
using DotNetCloud.Core.Grpc.Capabilities;
using DotNetCloud.Modules.Calendar.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace DotNetCloud.Modules.Calendar.Host.Services;

/// <summary>
/// Subscribes to calendar CRUD events on startup and dispatches
/// real-time SignalR events + FCM push notifications via Core.Server's
/// CoreCapabilities gRPC service.
/// </summary>
internal sealed class CalendarEventBroadcastSubscriber : IHostedService
{
    private readonly IEventBus _eventBus;
    private readonly CalendarEventBroadcastHandler _handler;
    private readonly ILogger<CalendarEventBroadcastSubscriber> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarEventBroadcastSubscriber"/> class.
    /// </summary>
    public CalendarEventBroadcastSubscriber(
        IEventBus eventBus,
        CoreCapabilities.CoreCapabilitiesClient coreClient,
        ICalendarShareService shareService,
        ILogger<CalendarEventBroadcastSubscriber> logger)
    {
        _eventBus = eventBus;
        _handler = new CalendarEventBroadcastHandler(
            coreClient,
            shareService,
            logger as ILogger<CalendarEventBroadcastHandler>
                ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CalendarEventBroadcastHandler>.Instance);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _eventBus.SubscribeAsync<CalendarEventCreatedEvent>(_handler, cancellationToken);
        await _eventBus.SubscribeAsync<CalendarEventUpdatedEvent>(_handler, cancellationToken);
        await _eventBus.SubscribeAsync<CalendarEventDeletedEvent>(_handler, cancellationToken);

        _logger.LogInformation(
            "CalendarEventBroadcastSubscriber started — subscribed to CalendarEventCreated/Updated/Deleted events");
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _eventBus.UnsubscribeAsync<CalendarEventCreatedEvent>(_handler, cancellationToken);
        await _eventBus.UnsubscribeAsync<CalendarEventUpdatedEvent>(_handler, cancellationToken);
        await _eventBus.UnsubscribeAsync<CalendarEventDeletedEvent>(_handler, cancellationToken);

        _logger.LogInformation("CalendarEventBroadcastSubscriber stopped");
    }
}
