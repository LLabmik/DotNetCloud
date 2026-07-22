using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Forwards <see cref="CalendarEventCreatedEvent"/> to the creating user's
/// connected clients via SignalR so e.g. Android clients can refresh their
/// event list and schedule alarms for the new event.
/// </summary>
internal sealed class CalendarEventCreatedRealtimeHandler : IEventHandler<CalendarEventCreatedEvent>
{
    private readonly IRealtimeBroadcaster _broadcaster;
    private readonly ILogger<CalendarEventCreatedRealtimeHandler> _logger;

    public CalendarEventCreatedRealtimeHandler(
        IRealtimeBroadcaster broadcaster,
        ILogger<CalendarEventCreatedRealtimeHandler> logger)
    {
        _broadcaster = broadcaster;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(CalendarEventCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await _broadcaster.SendToUserAsync(
                @event.CreatedByUserId,
                "CalendarEventCreated",
                new { eventId = @event.CalendarEventId.ToString(), calendarId = @event.CalendarId.ToString() },
                cancellationToken);

            _logger.LogDebug(
                "Broadcast CalendarEventCreated for event {EventId} to user {UserId}.",
                @event.CalendarEventId, @event.CreatedByUserId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast CalendarEventCreated for event {EventId}.",
                @event.CalendarEventId);
        }
    }
}
