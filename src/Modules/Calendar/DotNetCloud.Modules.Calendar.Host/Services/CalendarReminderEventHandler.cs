using DotNetCloud.Core.Events;
using DotNetCloud.Core.Grpc.Capabilities;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Calendar.Host.Services;

/// <summary>
/// Handles <see cref="CalendarReminderTriggeredEvent"/> by dispatching
/// in-app notifications and real-time SignalR events via Core.Server's
/// CoreCapabilities gRPC service.
/// </summary>
internal sealed class CalendarReminderEventHandler : IEventHandler<CalendarReminderTriggeredEvent>
{
    private readonly CoreCapabilities.CoreCapabilitiesClient _coreClient;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarReminderEventHandler"/> class.
    /// </summary>
    public CalendarReminderEventHandler(
        CoreCapabilities.CoreCapabilitiesClient coreClient,
        ILogger logger)
    {
        _coreClient = coreClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(CalendarReminderTriggeredEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Calendar reminder triggered: event {EventId} '{Title}' at {StartUtc} for user {UserId}",
            @event.CalendarEventId, @event.EventTitle, @event.EventStartUtc, @event.UserId);

        var minutesFromNow = (int)(@event.EventStartUtc - DateTime.UtcNow).TotalMinutes;
        var body = minutesFromNow <= 0
            ? "Starting now"
            : $"Starts in {minutesFromNow} minute{(minutesFromNow == 1 ? "" : "s")}";

        // 1. Send in-app notification via Core.Server's INotificationService
        try
        {
            var notifyResponse = await _coreClient.SendNotificationAsync(new SendNotificationRequest
            {
                Caller = new CallerContextMessage
                {
                    UserId = @event.UserId.ToString(),
                    CallerType = "System",
                    ModuleId = "dotnetcloud.calendar"
                },
                RecipientUserIds = { @event.UserId.ToString() },
                Title = @event.EventTitle,
                Body = body,
                Category = "Reminder",
                Link = $"/apps/calendar/events/{@event.CalendarEventId}"
            }, cancellationToken: cancellationToken);

            _logger.LogDebug(
                "SendNotification response: Success={Success}, Delivered={Count}",
                notifyResponse.Success, notifyResponse.DeliveredCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send in-app notification for event {EventId}", @event.CalendarEventId);
        }

        // 2. Broadcast real-time SignalR event to connected clients
        try
        {
            var realtimePayload = new
            {
                type = "calendar_reminder",
                eventId = @event.CalendarEventId.ToString(),
                title = @event.EventTitle,
                body,
                startUtc = @event.EventStartUtc.ToString("O")
            };

            var json = System.Text.Json.JsonSerializer.Serialize(realtimePayload);

            var broadcastResponse = await _coreClient.BroadcastRealtimeEventAsync(new BroadcastRealtimeEventRequest
            {
                Caller = new CallerContextMessage
                {
                    UserId = @event.UserId.ToString(),
                    CallerType = "System",
                    ModuleId = "dotnetcloud.calendar"
                },
                EventName = "CalendarReminder",
                PayloadJson = json,
                TargetUserId = @event.UserId.ToString()
            }, cancellationToken: cancellationToken);

            _logger.LogDebug(
                "BroadcastRealtimeEvent response: Success={Success}",
                broadcastResponse.Success);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast realtime event for event {EventId}", @event.CalendarEventId);
        }
    }
}
