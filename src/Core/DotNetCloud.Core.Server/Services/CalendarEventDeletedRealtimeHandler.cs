using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Forwards <see cref="CalendarEventDeletedEvent"/> to the deleted user's
/// connected clients via SignalR so e.g. Android clients can cancel pending alarms.
/// </summary>
internal sealed class CalendarEventDeletedRealtimeHandler : IEventHandler<CalendarEventDeletedEvent>
{
    private readonly IRealtimeBroadcaster _broadcaster;
    private readonly ILogger<CalendarEventDeletedRealtimeHandler> _logger;

    public CalendarEventDeletedRealtimeHandler(
        IRealtimeBroadcaster broadcaster,
        ILogger<CalendarEventDeletedRealtimeHandler> logger)
    {
        _broadcaster = broadcaster;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(CalendarEventDeletedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await _broadcaster.SendToUserAsync(
                @event.DeletedByUserId,
                "CalendarEventDeleted",
                new { eventId = @event.CalendarEventId.ToString(), calendarId = @event.CalendarId.ToString() },
                cancellationToken);

            _logger.LogDebug(
                "Broadcast CalendarEventDeleted for event {EventId} to user {UserId}.",
                @event.CalendarEventId, @event.DeletedByUserId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast CalendarEventDeleted for event {EventId}.",
                @event.CalendarEventId);
        }
    }
}
