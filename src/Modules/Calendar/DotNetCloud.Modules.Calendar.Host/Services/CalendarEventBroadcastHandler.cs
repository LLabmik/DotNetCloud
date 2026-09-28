using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Grpc.Capabilities;
using DotNetCloud.Modules.Calendar.Services;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Calendar.Host.Services;

/// <summary>
/// Handles <see cref="CalendarEventCreatedEvent"/>, <see cref="CalendarEventUpdatedEvent"/>,
/// and <see cref="CalendarEventDeletedEvent"/> by forwarding them to Core.Server via gRPC
/// for SignalR broadcast. FCM push delivery is handled by Core.Server's
/// BroadcastRealtimeEvent handler when the user has no active SignalR connections.
/// </summary>
internal sealed class CalendarEventBroadcastHandler :
    IEventHandler<CalendarEventCreatedEvent>,
    IEventHandler<CalendarEventUpdatedEvent>,
    IEventHandler<CalendarEventDeletedEvent>
{
    /// <summary>Module id sent as the <c>module-id</c> gRPC metadata header (required by Core.Server).</summary>
    private readonly string _moduleId =
        Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_ID") ?? "dotnetcloud.calendar";

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
            await BroadcastRealtimeAsync(
                userId, "CalendarEventCreated",
                new { eventId = @event.CalendarEventId.ToString() },
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
    }

    /// <summary>Broadcasts a real-time SignalR event to a specific user.</summary>
    private async Task BroadcastRealtimeAsync(Guid userId, string eventName, object payload, CancellationToken ct)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(payload);

            // Core.Server's AuthenticationInterceptor rejects capability calls without the
            // module-id metadata header (Unauthenticated: "Missing module-id metadata header").
            var metadata = new Metadata { { "module-id", _moduleId } };

            await _coreClient.BroadcastRealtimeEventAsync(new BroadcastRealtimeEventRequest
            {
                Caller = new CallerContextMessage
                {
                    UserId = userId.ToString(),
                    CallerType = "System",
                    ModuleId = _moduleId
                },
                EventName = eventName,
                PayloadJson = json,
                TargetUserId = userId.ToString()
            }, metadata, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast {EventName} for user {UserId}", eventName, userId);
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
