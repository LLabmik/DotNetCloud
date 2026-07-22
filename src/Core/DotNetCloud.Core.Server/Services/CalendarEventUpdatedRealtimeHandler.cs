using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Forwards <see cref="CalendarEventUpdatedEvent"/> to the updating user's
/// connected clients via SignalR so e.g. Android clients can reschedule alarms.
/// </summary>
internal sealed class CalendarEventUpdatedRealtimeHandler : IEventHandler<CalendarEventUpdatedEvent>
{
    private readonly IRealtimeBroadcaster _broadcaster;
    private readonly ILogger<CalendarEventUpdatedRealtimeHandler> _logger;

    public CalendarEventUpdatedRealtimeHandler(
        IRealtimeBroadcaster broadcaster,
        ILogger<CalendarEventUpdatedRealtimeHandler> logger)
    {
        _broadcaster = broadcaster;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(CalendarEventUpdatedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await _broadcaster.SendToUserAsync(
                @event.UpdatedByUserId,
                "CalendarEventUpdated",
                new { eventId = @event.CalendarEventId.ToString(), calendarId = @event.CalendarId.ToString() },
                cancellationToken);

            _logger.LogDebug(
                "Broadcast CalendarEventUpdated for event {EventId} to user {UserId}.",
                @event.CalendarEventId, @event.UpdatedByUserId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast CalendarEventUpdated for event {EventId}.",
                @event.CalendarEventId);
        }
    }
}
