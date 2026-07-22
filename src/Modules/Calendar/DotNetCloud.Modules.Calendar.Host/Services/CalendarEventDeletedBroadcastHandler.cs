using DotNetCloud.Core.Events;
using DotNetCloud.Core.Grpc.Capabilities;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Calendar.Host.Services;

/// <summary>
/// Handles <see cref="CalendarEventDeletedEvent"/> from the Calendar module's
/// local event bus and forwards it to Core.Server via gRPC
/// <see cref="CoreCapabilities.CoreCapabilitiesClient"/>
/// so that connected SignalR clients (e.g. Android) receive the notification
/// and can cancel alarms for the deleted event.
/// </summary>
internal sealed class CalendarEventDeletedBroadcastHandler : IEventHandler<CalendarEventDeletedEvent>
{
    private readonly CoreCapabilities.CoreCapabilitiesClient _coreClient;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarEventDeletedBroadcastHandler"/> class.
    /// </summary>
    public CalendarEventDeletedBroadcastHandler(
        CoreCapabilities.CoreCapabilitiesClient coreClient,
        ILogger logger)
    {
        _coreClient = coreClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(CalendarEventDeletedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                eventId = @event.CalendarEventId.ToString(),
                calendarId = @event.CalendarId.ToString(),
                isPermanent = @event.IsPermanent
            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);

            var response = await _coreClient.BroadcastRealtimeEventAsync(new BroadcastRealtimeEventRequest
            {
                Caller = new CallerContextMessage
                {
                    UserId = @event.DeletedByUserId.ToString(),
                    CallerType = "System",
                    ModuleId = "dotnetcloud.calendar"
                },
                EventName = "CalendarEventDeleted",
                PayloadJson = json,
                TargetUserId = @event.DeletedByUserId.ToString()
            }, cancellationToken: cancellationToken);

            _logger.LogDebug(
                "Broadcast CalendarEventDeleted for event {EventId} to user {UserId}: Success={Success}",
                @event.CalendarEventId, @event.DeletedByUserId, response.Success);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast CalendarEventDeleted for event {EventId}.",
                @event.CalendarEventId);
        }
    }
}
