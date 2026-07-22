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

        await _eventBus.SubscribeAsync<CalendarReminderTriggeredEvent>(_reminderHandler, cancellationToken);

        _logger.LogInformation("CalendarReminderEventSubscriber started — subscribed to CalendarReminderTriggeredEvent");
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_reminderHandler is not null)
            await _eventBus.UnsubscribeAsync<CalendarReminderTriggeredEvent>(_reminderHandler, cancellationToken);

        _logger.LogInformation("CalendarReminderEventSubscriber stopped");
    }
}
