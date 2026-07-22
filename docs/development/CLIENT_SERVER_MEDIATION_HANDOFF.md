# Client/Server Mediation Handoff

Last updated: 2026-07-22 (Server deployment done — client actions on monolith pending)

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

**Summary:** Server deploy needed — new `CalendarEventCreatedRealtimeHandler` to enable SignalR broadcast when events are created from Blazor UI.

**Branch:** `fix/android-calendar-alarm`

**Context:** Android client now auto-refreshes on `CalendarEventCreated`/`CalendarEventUpdated`/`CalendarEventDeleted` via SignalR. Client-side APK built and installed on `monolith` — timezone label ("UTC-5") confirmed working, event create/delete scheduling verified. Server is missing the `CalendarEventCreatedRealtimeHandler` — events created from Blazor UI are not broadcast to Android clients yet.

### Server Actions — `cloud.kimball.home`

- [ ] `git pull origin fix/android-calendar-alarm`
- [ ] `dotnet build src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj -c Release`
- [ ] `dotnet publish src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj -c Release -o /opt/dotnetcloud/publish`
- [ ] `sudo systemctl restart dotnetcloud`
- [ ] Verify: `curl -k https://localhost:5443/health` → 200 Healthy
- [ ] Verify: Create a calendar event from Blazor UI → Android client should show it immediately (no refresh needed)

**New server files deployed this round:**
- `Services/CalendarEventCreatedRealtimeHandler.cs` — broadcasts `CalendarEventCreated` to the creating user's connected clients via SignalR
- Modified: `Services/NotificationEventSubscriber.cs` — subscribes `CalendarEventCreatedEvent` alongside existing `Deleted` and `Updated` handlers

**Previously deployed files (from prior handoff round):**
- `Services/CalendarEventDeletedRealtimeHandler.cs`
- `Services/CalendarEventUpdatedRealtimeHandler.cs`

### Client Actions — `monolith` (Android client)

- [x] APK built and installed
- [x] Timezone label verified: "UTC-5" shown on event detail page
- [x] Event create/delete with reminder: Scheduling and cleanup confirmed (logcat shows event count changing)
- [ ] **Pending until server deploy:** Test Blazor-originated event create → auto-appears on Android (requires server to broadcast `CalendarEventCreated`)
- [ ] **Pending until server deploy:** Test Blazor-originated event delete → alarm cancelled on Android (requires server to broadcast `CalendarEventDeleted`)

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
