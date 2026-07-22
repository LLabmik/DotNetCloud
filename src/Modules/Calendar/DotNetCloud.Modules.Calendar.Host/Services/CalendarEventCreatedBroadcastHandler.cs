using DotNetCloud.Core.Events;
using DotNetCloud.Core.Grpc.Capabilities;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Calendar.Host.Services;

/// <summary>
/// Handles <see cref="CalendarEventCreatedEvent"/> from the Calendar module's
/// local event bus and forwards it to Core.Server via gRPC
/// <see cref="CoreCapabilities.CoreCapabilitiesClient"/>
/// so that connected SignalR clients (e.g. Android) receive the notification.
/// </summary>
internal sealed class CalendarEventCreatedBroadcastHandler : IEventHandler<CalendarEventCreatedEvent>
{
    private readonly CoreCapabilities.CoreCapabilitiesClient _coreClient;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarEventCreatedBroadcastHandler"/> class.
    /// </summary>
    public CalendarEventCreatedBroadcastHandler(
        CoreCapabilities.CoreCapabilitiesClient coreClient,
        ILogger logger)
    {
        _coreClient = coreClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(CalendarEventCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                eventId = @event.CalendarEventId.ToString(),
                calendarId = @event.CalendarId.ToString(),
                title = @event.Title,
                startUtc = @event.StartUtc.ToString("O"),
                endUtc = @event.EndUtc.ToString("O"),
                isRecurring = @event.IsRecurring
            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);

            var response = await _coreClient.BroadcastRealtimeEventAsync(new BroadcastRealtimeEventRequest
            {
                Caller = new CallerContextMessage
                {
                    UserId = @event.CreatedByUserId.ToString(),
                    CallerType = "System",
                    ModuleId = "dotnetcloud.calendar"
                },
                EventName = "CalendarEventCreated",
                PayloadJson = json,
                TargetUserId = @event.CreatedByUserId.ToString()
            }, cancellationToken: cancellationToken);

            _logger.LogDebug(
                "Broadcast CalendarEventCreated for event {EventId} to user {UserId}: Success={Success}",
                @event.CalendarEventId, @event.CreatedByUserId, response.Success);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast CalendarEventCreated for event {EventId}.",
                @event.CalendarEventId);
        }
    }
}
