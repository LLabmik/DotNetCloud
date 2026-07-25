# Client/Server Mediation Handoff

Last updated: 2026-07-25 (Android battery optimization + calendar push infrastructure — server-side SignalR & FCM push pending)

Purpose: shared handoff between client-side and server-side agents, mediated by user.

Archived context:

- Historical completed updates are in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Additional history remains available in git.

## Process Rules

**Agent autonomy (CRITICAL):**

- Both client and server agents work autonomously — they do NOT ask the moderator for context or permission.
- Agents pull the branch specified in the relay message, read the **Active Handoff** section, and execute the work described there independently.
- All actionable items, blockers, and technical details go directly in this document.
- **Current active branch:** `fix/android-power-consumption`

## Active Handoff — Server-Side Calendar Broadcasting (Production Deploy Required)

**Summary:** Android battery optimization is complete on the client side. The server now **must** broadcast calendar events via SignalR and send FCM pushes so the Android client gets real-time updates + Doze wake-up. Follow the exact `CalendarReminderEventHandler` pattern below — it already does the same thing for reminders.

**Branch:** `fix/android-power-consumption` — commit `25958dff`

**Architecture:** The Calendar module is process-isolated. It uses `CoreCapabilities.CoreCapabilitiesClient` (gRPC) to call back into Core.Server. Two RPCs are available:
- `BroadcastRealtimeEventAsync` — pushes SignalR events to connected clients (maps to `IRealtimeBroadcaster`)
- `PublishEventAsync` — publishes events on Core.Server's event bus

However, `SendNotificationAsync` only persists DB records and does NOT send FCM pushes. For FCM push delivery, the `BroadcastRealtimeEvent` handler in Core.Server must also call `_pushService.SendAsync()` for calendar events. Both server-side files need changes:
1. **Calendar module**: New `CalendarEventBroadcastHandler` + `CalendarEventBroadcastSubscriber` (like `CalendarReminderEventHandler`)
2. **Core.Server**: Modify `BroadcastRealtimeEvent` gRPC handler to send FCM push for calendar events

---

### Step 1 — Create `CalendarEventBroadcastHandler`

**New file:** `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/Services/CalendarEventBroadcastHandler.cs`

Copy the exact pattern from `CalendarReminderEventHandler.cs`:

```csharp
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Grpc.Capabilities;
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
    private readonly CoreCapabilities.CoreCapabilitiesClient _coreClient;
    private readonly ILogger<CalendarEventBroadcastHandler> _logger;

    public CalendarEventBroadcastHandler(
        CoreCapabilities.CoreCapabilitiesClient coreClient,
        ILogger<CalendarEventBroadcastHandler> logger)
    {
        _coreClient = coreClient;
        _logger = logger;
    }

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

    private async Task<IReadOnlyList<Guid>> GetAffectedUserIdsAsync(Guid calendarId, Guid currentUserId)
    {
        try
        {
            var shareService = GetShareService();
            if (shareService is not null)
            {
                var caller = new DotNetCloud.Core.Authorization.CallerContext(
                    currentUserId, Array.Empty<string>(), DotNetCloud.Core.Authorization.CallerType.User);
                var shares = await shareService.ListSharesAsync(calendarId, caller);
                var userIds = new List<Guid> { currentUserId };
                foreach (var share in shares)
                {
                    if (share.UserId.HasValue && share.UserId.Value != currentUserId)
                        userIds.Add(share.UserId.Value);
                }
                return userIds;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get calendar shares, falling back to owner only.");
        }
        return [currentUserId];
    }

    private DotNetCloud.Modules.Calendar.Services.ICalendarShareService? GetShareService()
    {
        try
        {
            return CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default
                .GetService<DotNetCloud.Modules.Calendar.Services.ICalendarShareService>();
        }
        catch { return null; }
    }
}
```

### Step 2 — Create `CalendarEventBroadcastSubscriber`

**New file:** `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/Services/CalendarEventBroadcastSubscriber.cs`

Same pattern as `CalendarReminderEventSubscriber.cs`:

```csharp
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Grpc.Capabilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace DotNetCloud.Modules.Calendar.Host.Services;

internal sealed class CalendarEventBroadcastSubscriber : IHostedService
{
    private readonly IEventBus _eventBus;
    private readonly CalendarEventBroadcastHandler _handler;
    private readonly ILogger<CalendarEventBroadcastSubscriber> _logger;

    public CalendarEventBroadcastSubscriber(
        IEventBus eventBus,
        CoreCapabilities.CoreCapabilitiesClient coreClient,
        ILogger<CalendarEventBroadcastSubscriber> logger)
    {
        _eventBus = eventBus;
        _handler = new CalendarEventBroadcastHandler(coreClient,
            logger as ILogger<CalendarEventBroadcastHandler>
            ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CalendarEventBroadcastHandler>.Instance);
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _eventBus.SubscribeAsync<CalendarEventCreatedEvent>(_handler, cancellationToken);
        await _eventBus.SubscribeAsync<CalendarEventUpdatedEvent>(_handler, cancellationToken);
        await _eventBus.SubscribeAsync<CalendarEventDeletedEvent>(_handler, cancellationToken);
        _logger.LogInformation("CalendarEventBroadcastSubscriber started");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _eventBus.UnsubscribeAsync<CalendarEventCreatedEvent>(_handler, cancellationToken);
        await _eventBus.UnsubscribeAsync<CalendarEventUpdatedEvent>(_handler, cancellationToken);
        await _eventBus.UnsubscribeAsync<CalendarEventDeletedEvent>(_handler, cancellationToken);
        _logger.LogInformation("CalendarEventBroadcastSubscriber stopped");
    }
}
```

### Step 3 — Register subscriber in Calendar Program.cs

**File:** `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/Program.cs`

Find the line:
```csharp
builder.Services.AddHostedService<CalendarReminderEventSubscriber>();
```

Add after it:
```csharp
builder.Services.AddHostedService<CalendarEventBroadcastSubscriber>();
```

### Step 4 — Add FCM push in Core.Server's BroadcastRealtimeEvent handler ⚠️ CRITICAL

**File:** `src/Core/DotNetCloud.Core.Server/Grpc/Services/GrpcHealthServiceImpl.cs`

The `BroadcastRealtimeEvent` gRPC handler sends SignalR events but does NOT send FCM pushes. When the device is dozed (no active SignalR connection), the event is silently dropped. We need to add an FCM push trigger for calendar events.

Add a new `using` at the top:
```csharp
using DotNetCloud.Core.Server.PushNotifications;
```

Then modify the per-user delivery block in `BroadcastRealtimeEvent` (around line 355) to send FCM push for calendar events:

**BEFORE:**
```csharp
await broadcaster.SendToUserAsync(targetUserId, request.EventName, payload ?? request.PayloadJson, context.CancellationToken);
```

**AFTER:**
```csharp
await broadcaster.SendToUserAsync(targetUserId, request.EventName, payload ?? request.PayloadJson, context.CancellationToken);

// For calendar events, always send FCM push as fallback for dozed devices
if (request.EventName is "CalendarEventCreated" or "CalendarEventUpdated" or "CalendarEventDeleted")
{
    try
    {
        var pushService = _serviceProvider.GetRequiredService<IPushNotificationService>();
        var eventId = string.Empty;
        if (!string.IsNullOrEmpty(request.PayloadJson))
        {
            using var jsonDoc = JsonDocument.Parse(request.PayloadJson);
            if (jsonDoc.RootElement.TryGetProperty("eventId", out var evtIdProp))
                eventId = evtIdProp.GetString() ?? string.Empty;
        }

        await pushService.SendAsync(targetUserId, new PushNotification
        {
            Title = request.EventName switch
            {
                "CalendarEventCreated" => "New Event",
                "CalendarEventUpdated" => "Calendar Updated",
                "CalendarEventDeleted" => "Event Cancelled",
                _ => "Calendar Update"
            },
            Body = string.Empty,
            Category = NotificationCategory.CalendarEvent,
            Data = new Dictionary<string, string>
            {
                ["type"] = "calendar_event",
                ["eventId"] = eventId
            }
        }, context.CancellationToken);
    }
    catch (Exception pushEx)
    {
        _logger.LogWarning(pushEx, "Failed to send FCM push for calendar event {EventName} to user {UserId}",
            request.EventName, targetUserId);
    }
}
```

### Step 5 — Build, Deploy, Verify

```bash
# Pull branch
git pull origin fix/android-power-consumption

# Build both projects
dotnet publish src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/
dotnet publish src/Core/DotNetCloud.Core.Server/

# Deploy to production
sudo systemctl restart dotnetcloud

# Verify health
curl -sk https://localhost:5443/health

# E2E test:
# 1. Open Blazor UI Calendar → create/update/delete an event
# 2. On Android (with APK built from fix/android-power-consumption):
#    - Foreground: calendar should refresh within seconds via SignalR
#    - Background + Doze: FCM push should wake → calendar refreshes
```

## Moderator Communication (Minimal)

**Moderator relays ONLY ONE OF THESE messages — nothing more:**

- `New handoff update for <target-machine>. Pull main and resume from 'Active Handoff' section.`
- `<Commit hash> — New handoff update for <target-machine>. Pull and check docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md Active Handoff.`

**No moderator task:** Moderator provides zero context, zero explanation. The handoff document has everything the receiving agent needs.

## Environment

| Role           | Machine              | Detail                                                                             |
| -------------- | -------------------- | ---------------------------------------------------------------------------------- |
| Server         | `cloud.kimball.home` | `https://cloud.dotnetcloud.net/` (production)                                      |
| Server         | `mint22`             | `https://mint22:5443/` (dev)                                                       |
| Client         | `Windows11-TestDNC`  | Sync dir: `C:\Users\benk\synctray`                                       |
| Client         | `mint-dnc-client`    | Linux Mint 22 validation host for desktop sync client implementation + E2E testing |
| Client         | `mint-OptiPlex-7010` | production client connected to `cloud.dotnetcloud.net`              |
| Android Client | `monolith`           | Android MAUI app development + emulator testing (Windows 11)                       |

## Key Carry-Forward Contracts

- Auth: Files module host uses a policy scheme (`DotNetCloud.Module`) that auto-selects between `OpenIddict.Validation.AspNetCore` (JWT Bearer) and `Identity.Application` (cookie) based on the `Authorization` header. Controllers use plain `[Authorize]`. All module hosts must follow this pattern.
- API envelope: middleware wraps responses; clients should unwrap via envelope helpers.
- Sync flow: changes -> tree -> reconcile -> chunk manifest -> chunk download -> file assembly.
- Desktop OAuth constant: `OAuthConstants.ClientId = "dotnetcloud-desktop"`.
- ✅ **SignalR channel group naming:** `chat-channel-{channelId}` (used by `ChatHub.ChannelGroup()`, `CoreHub.JoinGroupAsync()`, and Android `SignalRChatClient`).
- **Controller discovery:** Core.Server references Files.Host and Chat.Host via `ProjectReference`. ASP.NET Core auto-discovers controllers from referenced assemblies. Do NOT create duplicate controllers in Core.Server for routes already served by module Host assemblies.
- ✅ **Calendar event broadcasting pattern:** Follow `CalendarReminderEventHandler` (`CalendarReminderEventSubscriber` + `CalendarEventBroadcastHandler`) as the reference implementation. It calls `CoreCapabilitiesClient.BroadcastRealtimeEventAsync` for SignalR and `SendNotificationAsync` for FCM push.

<!-- carry-forward contracts and old Android changes archived to CLIENT_SERVER_MEDIATION_ARCHIVE.md -->
