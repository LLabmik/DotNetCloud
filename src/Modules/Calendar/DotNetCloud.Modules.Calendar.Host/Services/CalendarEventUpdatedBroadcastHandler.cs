using DotNetCloud.Core.Events;
using DotNetCloud.Core.Grpc.Capabilities;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Calendar.Host.Services;

/// <summary>
/// Handles <see cref="CalendarEventUpdatedEvent"/> from the Calendar module's
/// local event bus and forwards it to Core.Server via gRPC
/// <see cref="CoreCapabilities.CoreCapabilitiesClient"/>
/// so that connected SignalR clients (e.g. Android) receive the notification
/// and can refresh the updated event details.
/// </summary>
internal sealed class CalendarEventUpdatedBroadcastHandler : IEventHandler<CalendarEventUpdatedEvent>
{
    private readonly CoreCapabilities.CoreCapabilitiesClient _coreClient;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarEventUpdatedBroadcastHandler"/> class.
    /// </summary>
    public CalendarEventUpdatedBroadcastHandler(
        CoreCapabilities.CoreCapabilitiesClient coreClient,
        ILogger logger)
    {
        _coreClient = coreClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(CalendarEventUpdatedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                eventId = @event.CalendarEventId.ToString(),
                calendarId = @event.CalendarId.ToString()
            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);

            var response = await _coreClient.BroadcastRealtimeEventAsync(new BroadcastRealtimeEventRequest
            {
                Caller = new CallerContextMessage
                {
                    UserId = @event.UpdatedByUserId.ToString(),
                    CallerType = "System",
                    ModuleId = "dotnetcloud.calendar"
                },
                EventName = "CalendarEventUpdated",
                PayloadJson = json,
                TargetUserId = @event.UpdatedByUserId.ToString()
            }, cancellationToken: cancellationToken);

            _logger.LogDebug(
                "Broadcast CalendarEventUpdated for event {EventId} to user {UserId}: Success={Success}",
                @event.CalendarEventId, @event.UpdatedByUserId, response.Success);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast CalendarEventUpdated for event {EventId}.",
                @event.CalendarEventId);
        }
    }
}
