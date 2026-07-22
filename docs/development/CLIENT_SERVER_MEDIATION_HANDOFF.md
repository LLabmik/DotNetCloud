# Client/Server Mediation Handoff

Last updated: 2026-07-22 (Server redeploy complete — CalendarEventCreatedRealtimeHandler active, all 14 modules healthy)

Purpose: shared handoff between client-side and server-side agents, mediated by user.

Archived context:

- Historical completed updates are in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Additional history remains available in git.

## Process Rules

**Agent autonomy (CRITICAL):**

- Both client and server agents work autonomously — they do NOT ask the moderator for context or permission.
- Agents pull the branch specified in the relay message, read the **Active Handoff** section, and execute the work described there independently.
- All actionable items, blockers, and technical details go directly in this document.
- **Current active branch:** `fix/android-calendar-alarm`

## Active Handoff

**Summary:** Fixed root cause of Blazor→Android calendar sync. Calendar events never crossed process boundary — Calendar module publishes events on its local event bus, but handlers were subscribed to Core.Server's event bus. Added gRPC bridge handlers.

**Branch:** `fix/android-calendar-alarm`

**Context:** Three new broadcast handlers (`CalendarEventCreatedBroadcastHandler`, `CalendarEventDeletedBroadcastHandler`, `CalendarEventUpdatedBroadcastHandler`) added in the Calendar module's Host project. These subscribe to calendar CRUD events on the Calendar module's local `InProcessEventBus` and forward them to Core.Server via gRPC `BroadcastRealtimeEvent`, which pushes to connected SignalR clients.

The Calendar module and its Host need to be rebuilt and redeployed to `cloud.kimball.home`.

### Server Actions — `cloud.kimball.home`

- [ ] `git pull origin fix/android-calendar-alarm`
- [ ] Build + publish the Calendar module host:
  ```
  dotnet publish src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/DotNetCloud.Modules.Calendar.Host.csproj -c Release -o /opt/dotnetcloud/modules/dotnetcloud.calendar
  ```
- [ ] `sudo systemctl restart dotnetcloud`
- [ ] Verify all modules healthy: `curl -sk https://localhost:5443/health` → 200 Healthy
- [ ] Verify end-to-end: Create calendar event from Blazor → Android auto-refreshes

**New Calendar module files deployed:**
- `Services/CalendarEventCreatedBroadcastHandler.cs` — forwards `CalendarEventCreated` via gRPC
- `Services/CalendarEventDeletedBroadcastHandler.cs` — forwards `CalendarEventDeleted` via gRPC
- `Services/CalendarEventUpdatedBroadcastHandler.cs` — forwards `CalendarEventUpdated` via gRPC
- Modified: `Services/CalendarReminderEventSubscriber.cs` — subscribes all three new handlers

### Client Actions — `monolith` (Android client)

- [x] APK already built and installed
- [x] Timezone label "UTC-5" verified
- [x] Android-originated event create/delete scheduling verified
- [ ] **Pending server deploy:** Blazor-originated event create → auto-appears on Android
- [ ] **Pending server deploy:** Blazor-originated event delete → alarm cancelled on Android

### Build Notes

**CRITICAL:** `dotnet build` without `-r android-arm64` only builds for x64 (emulator). The arm64 APK at `bin/Debug/net10.0-android/android-arm64/` stays stale. Always use:
```powershell
dotnet build ... -f net10.0-android -c Debug -r android-arm64 /p:AndroidSdkDirectory="C:\Program Files (x86)\Android\android-sdk"
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

<!-- carry-forward contracts and old Android changes archived to CLIENT_SERVER_MEDIATION_ARCHIVE.md -->
