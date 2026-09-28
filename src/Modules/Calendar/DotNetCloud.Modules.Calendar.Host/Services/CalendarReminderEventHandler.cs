using DotNetCloud.Core.Events;
using DotNetCloud.Core.Grpc.Capabilities;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Calendar.Host.Services;

/// <summary>
/// Handles <see cref="CalendarReminderTriggeredEvent"/> by dispatching
/// in-app notifications and real-time SignalR events via Core.Server's
/// CoreCapabilities gRPC service.
/// </summary>
internal sealed class CalendarReminderEventHandler : IEventHandler<CalendarReminderTriggeredEvent>
{
    /// <summary>Module id sent as the <c>module-id</c> gRPC metadata header (required by Core.Server).</summary>
    private readonly string _moduleId =
        Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_ID") ?? "dotnetcloud.calendar";

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

        // 1. Send in-app notification via Core.Server's INotificationService.
        // Core.Server's AuthenticationInterceptor rejects capability calls that do not carry the
        // module-id metadata header (Unauthenticated: "Missing module-id metadata header").
        try
        {
            var metadata = new Metadata { { "module-id", _moduleId } };

            var notifyResponse = await _coreClient.SendNotificationAsync(new SendNotificationRequest
            {
                Caller = new CallerContextMessage
                {
                    UserId = @event.UserId.ToString(),
                    CallerType = "System",
                    ModuleId = _moduleId
                },
                RecipientUserIds = { @event.UserId.ToString() },
                Title = @event.EventTitle,
                Body = body,
                Category = "Reminder",
                Link = $"/apps/calendar/events/{@event.CalendarEventId}"
            }, metadata, cancellationToken: cancellationToken);

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
            var metadata = new Metadata { { "module-id", _moduleId } };

            var broadcastResponse = await _coreClient.BroadcastRealtimeEventAsync(new BroadcastRealtimeEventRequest
            {
                Caller = new CallerContextMessage
                {
                    UserId = @event.UserId.ToString(),
                    CallerType = "System",
                    ModuleId = _moduleId
                },
                EventName = "CalendarReminder",
                PayloadJson = json,
                TargetUserId = @event.UserId.ToString()
            }, metadata, cancellationToken: cancellationToken);

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
