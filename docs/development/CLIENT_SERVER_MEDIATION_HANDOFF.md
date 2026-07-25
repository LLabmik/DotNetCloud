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

**Summary:** Android battery optimization is complete on the client side (WakeLock removed, SignalR consolidated, FCM push handlers wired). The server now **must** send the SignalR broadcasts and FCM pushes for calendar event changes. The Android client listens for these but nobody is sending them yet.

**Branch:** `fix/android-power-consumption` — commit `4a2d0d6e`

**Context:** Three changes needed on the server:
1. Add `CalendarEvent` to `NotificationCategory` enum
2. Calendar module must broadcast via `IRealtimeBroadcaster` on CRUD
3. Calendar module must send FCM push alongside SignalR for Doze wake-up

The Android side has already been built, committed, and pushed. Server changes must be deployed to production (`cloud.kimball.home`) before end-to-end validation can happen.

### Step 1.1 — Add `CalendarEvent` to `NotificationCategory` enum

- **File:** `src/Core/DotNetCloud.Core.Server/PushNotifications/PushNotificationService.cs`
- Add `CalendarEvent` value to the `NotificationCategory` enum

### Step 1.2 — Calendar module: broadcast via `IRealtimeBroadcaster` on event changes

- **Files:** `src/Modules/Calendar/` — controller(s)/handler(s) that process Create/Update/Delete/Rsvp
- After persisting a calendar event change, inject `IRealtimeBroadcaster` and call:
  - `_broadcaster.SendToUserAsync(ownerUserId, "CalendarEventCreated", new { eventId = evt.Id })` for create
  - `_broadcaster.SendToUserAsync(ownerUserId, "CalendarEventUpdated", new { eventId = evt.Id })` for update/rsvp
  - `_broadcaster.SendToUserAsync(ownerUserId, "CalendarEventDeleted", new { eventId = evt.Id })` for delete
- Also broadcast to shared calendar members (everyone who has access to that calendar)
- The payload `{ eventId: "guid" }` must match what `Android SignalRChatClient` expects (it parses `payload.GetProperty("eventId")`)

### Step 1.3 — Calendar module: send FCM push alongside SignalR broadcast

- **Files:** `src/Modules/Calendar/`
- After the `IRealtimeBroadcaster` call, also call `_pushService.SendAsync(userId, notification)` where:
  - `Title`: "Calendar Updated" / "New Event" / "Event Cancelled"
  - `Body`: event title and time
  - `Category`: `NotificationCategory.CalendarEvent`
  - `Data`: `{ "type": "calendar_event", "eventId": "...", "calendarId": "..." }`
- Push must go to ALL affected users (owner + sharees)
- If push service is unavailable (NoOp), log and continue — do NOT block

### Step 1.4 — Handle `CalendarEvent` push category in delivery pipeline

- The Chat module's gRPC push service must recognize the `CalendarEvent` category and send it through FCM/UnifiedPush
- The endpoint `POST /api/v1/notifications/devices/register` already handles device registration

### Deployment

```bash
# On cloud.kimball.home:
git pull origin fix/android-power-consumption
# Build & publish affected projects
dotnet publish src/Modules/Calendar/Calendar.Host/
dotnet publish src/Core/DotNetCloud.Core.Server/
# Deploy to server directory
sudo systemctl restart dotnetcloud
# Verify health
curl -sk https://localhost:5443/health
```

### Verification

1. Open Blazor UI → create/update/delete a calendar event
2. On Android (with new APK built from `fix/android-power-consumption`):
   - Foreground: verify SignalR update arrives within seconds
   - Backgrounded: verify FCM push wakes device → calendar refreshes
3. Verify chat still works (send message, unread counts, push notifications)

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

<!-- carry-forward contracts and old Android changes archived to CLIENT_SERVER_MEDIATION_ARCHIVE.md -->
