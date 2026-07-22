using DotNetCloud.Core.Events;
using DotNetCloud.Core.Grpc.Capabilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace DotNetCloud.Modules.Calendar.Host.Services;

/// <summary>
/// Subscribes to calendar reminder events on startup and dispatches
/// in-app notifications and real-time SignalR events via Core.Server's
/// CoreCapabilities gRPC service.
/// </summary>
internal sealed class CalendarReminderEventSubscriber : IHostedService
{
    private readonly IEventBus _eventBus;
    private readonly CoreCapabilities.CoreCapabilitiesClient _coreClient;
    private readonly ILogger<CalendarReminderEventSubscriber> _logger;
    private CalendarReminderEventHandler? _reminderHandler;
    private CalendarEventCreatedBroadcastHandler? _createdHandler;
    private CalendarEventDeletedBroadcastHandler? _deletedHandler;
    private CalendarEventUpdatedBroadcastHandler? _updatedHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarReminderEventSubscriber"/> class.
    /// </summary>
    public CalendarReminderEventSubscriber(
        IEventBus eventBus,
        CoreCapabilities.CoreCapabilitiesClient coreClient,
        ILogger<CalendarReminderEventSubscriber> logger)
    {
        _eventBus = eventBus;
        _coreClient = coreClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _reminderHandler = new CalendarReminderEventHandler(_coreClient, _logger);
        _createdHandler = new CalendarEventCreatedBroadcastHandler(_coreClient, _logger);
        _deletedHandler = new CalendarEventDeletedBroadcastHandler(_coreClient, _logger);
        _updatedHandler = new CalendarEventUpdatedBroadcastHandler(_coreClient, _logger);

        await _eventBus.SubscribeAsync<CalendarReminderTriggeredEvent>(_reminderHandler, cancellationToken);
        await _eventBus.SubscribeAsync<CalendarEventCreatedEvent>(_createdHandler, cancellationToken);
        await _eventBus.SubscribeAsync<CalendarEventDeletedEvent>(_deletedHandler, cancellationToken);
        await _eventBus.SubscribeAsync<CalendarEventUpdatedEvent>(_updatedHandler, cancellationToken);

        _logger.LogInformation(
            "CalendarReminderEventSubscriber started — subscribed to ReminderTriggered, Created, Deleted, Updated events");
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_reminderHandler is not null)
            await _eventBus.UnsubscribeAsync<CalendarReminderTriggeredEvent>(_reminderHandler, cancellationToken);

        if (_createdHandler is not null)
            await _eventBus.UnsubscribeAsync<CalendarEventCreatedEvent>(_createdHandler, cancellationToken);

        if (_deletedHandler is not null)
            await _eventBus.UnsubscribeAsync<CalendarEventDeletedEvent>(_deletedHandler, cancellationToken);

        if (_updatedHandler is not null)
            await _eventBus.UnsubscribeAsync<CalendarEventUpdatedEvent>(_updatedHandler, cancellationToken);

        _logger.LogInformation("CalendarReminderEventSubscriber stopped");
    }
}
