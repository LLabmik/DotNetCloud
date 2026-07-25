using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Grpc.Capabilities;
using DotNetCloud.Modules.Calendar.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Calendar.Host.Services;

/// <summary>
/// Handles <see cref="CalendarEventCreatedEvent"/>, <see cref="CalendarEventUpdatedEvent"/>,
/// and <see cref="CalendarEventDeletedEvent"/> by forwarding them to Core.Server via gRPC
/// for SignalR broadcast + FCM push notification delivery.
/// Uses the same pattern as <see cref="CalendarReminderEventHandler"/>.
/// </summary>
internal sealed class CalendarEventBroadcastHandler :
    IEventHandler<CalendarEventCreatedEvent>,
    IEventHandler<CalendarEventUpdatedEvent>,
    IEventHandler<CalendarEventDeletedEvent>
{
    private readonly CoreCapabilities.CoreCapabilitiesClient _coreClient;
    private readonly ICalendarShareService _shareService;
    private readonly ILogger<CalendarEventBroadcastHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarEventBroadcastHandler"/> class.
    /// </summary>
    public CalendarEventBroadcastHandler(
        CoreCapabilities.CoreCapabilitiesClient coreClient,
        ICalendarShareService shareService,
        ILogger<CalendarEventBroadcastHandler> logger)
    {
        _coreClient = coreClient;
        _shareService = shareService;
        _logger = logger;
    }

    /// <summary>Forward created event to Core.Server.</summary>
    public async Task HandleAsync(CalendarEventCreatedEvent @event, CancellationToken ct = default)
    {
        _logger.LogInformation("Calendar event created: {EventId} '{Title}' by user {UserId}",
            @event.CalendarEventId, @event.Title, @event.CreatedByUserId);

        var usersToNotify = await GetAffectedUserIdsAsync(@event.CalendarId, @event.CreatedByUserId);

        foreach (var userId in usersToNotify)
        {
            // 1. SignalR broadcast for connected clients
            await BroadcastRealtimeAsync(
                userId, "CalendarEventCreated",
                new { eventId = @event.CalendarEventId.ToString() },
                ct);

            // 2. FCM push notification for dozed/wake-up delivery
            await SendPushNotificationAsync(
                userId,
                "New Event",
                $"{@event.Title}",
                @event.CalendarEventId.ToString(),
                @event.CalendarId.ToString(),
                ct);
        }
    }

    /// <summary>Forward updated event to Core.Server.</summary>
    public async Task HandleAsync(CalendarEventUpdatedEvent @event, CancellationToken ct = default)
    {
        _logger.LogInformation("Calendar event updated: {EventId} by user {UserId}",
            @event.CalendarEventId, @event.UpdatedByUserId);

        var usersToNotify = await GetAffectedUserIdsAsync(@event.CalendarId, @event.UpdatedByUserId);

        foreach (var userId in usersToNotify)
        {
            await BroadcastRealtimeAsync(
                userId, "CalendarEventUpdated",
                new { eventId = @event.CalendarEventId.ToString() },
                ct);

            await SendPushNotificationAsync(
                userId,
                "Calendar Updated",
                "An event was modified",
                @event.CalendarEventId.ToString(),
                @event.CalendarId.ToString(),
                ct);
        }
    }

    /// <summary>Forward deleted event to Core.Server.</summary>
    public async Task HandleAsync(CalendarEventDeletedEvent @event, CancellationToken ct = default)
    {
        _logger.LogInformation("Calendar event deleted: {EventId} by user {UserId}",
            @event.CalendarEventId, @event.DeletedByUserId);

        var usersToNotify = await GetAffectedUserIdsAsync(@event.CalendarId, @event.DeletedByUserId);

        foreach (var userId in usersToNotify)
        {
            await BroadcastRealtimeAsync(
                userId, "CalendarEventDeleted",
                new { eventId = @event.CalendarEventId.ToString() },
                ct);
        }
        // No push for deletions — the SignalR broadcast will trigger alarm cancellation.
    }

    /// <summary>Broadcasts a real-time SignalR event to a specific user.</summary>
    private async Task BroadcastRealtimeAsync(Guid userId, string eventName, object payload, CancellationToken ct)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            await _coreClient.BroadcastRealtimeEventAsync(new BroadcastRealtimeEventRequest
            {
                Caller = new CallerContextMessage
                {
                    UserId = userId.ToString(),
                    CallerType = "System",
                    ModuleId = "dotnetcloud.calendar"
                },
                EventName = eventName,
                PayloadJson = json,
                TargetUserId = userId.ToString()
            }, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast {EventName} for user {UserId}", eventName, userId);
        }
    }

    /// <summary>Sends an FCM push notification to a user.</summary>
    private async Task SendPushNotificationAsync(Guid userId, string title, string body,
        string eventId, string calendarId, CancellationToken ct)
    {
        try
        {
            var notifyPayload = new
            {
                type = "calendar_event",
                eventId,
                calendarId
            };

            await _coreClient.SendNotificationAsync(new SendNotificationRequest
            {
                Caller = new CallerContextMessage
                {
                    UserId = userId.ToString(),
                    CallerType = "System",
                    ModuleId = "dotnetcloud.calendar"
                },
                RecipientUserIds = { userId.ToString() },
                Title = title,
                Body = body,
                Category = "CalendarEvent",
                Link = $"/apps/calendar/events/{eventId}"
            }, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send push notification for event {EventId}", eventId);
        }
    }

    /// <summary>
    /// Gets all user IDs that should be notified about changes to this calendar.
    /// Includes the owner and all sharees. Falls back to just the current user.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> GetAffectedUserIdsAsync(Guid calendarId, Guid currentUserId)
    {
        try
        {
            var caller = new CallerContext(
                currentUserId, [], CallerType.User);
            var shares = await _shareService.ListSharesAsync(calendarId, caller);

            var userIds = new List<Guid> { currentUserId };
            foreach (var share in shares)
            {
                if (share.SharedWithUserId.HasValue && share.SharedWithUserId.Value != currentUserId)
                    userIds.Add(share.SharedWithUserId.Value);
            }

            return userIds;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get calendar shares, falling back to owner only.");
        }

        return [currentUserId];
    }
}
