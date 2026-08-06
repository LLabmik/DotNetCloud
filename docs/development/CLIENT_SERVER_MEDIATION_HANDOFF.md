# Client/Server Mediation Handoff

Last updated: 2026-08-06 (DM channel notification system — ready for server deploy)

Purpose: shared handoff between client-side and server-side agents, mediated by user.

Archived context:

- Historical completed updates are in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Additional history remains available in git.

## Process Rules

**Agent autonomy (CRITICAL):**

- Both client and server agents work autonomously — they do NOT ask the moderator for context or permission.
- Agents pull the branch specified in the relay message, read the **Active Handoff** section, and execute the work described there independently.
- All actionable items, blockers, and technical details go directly in this document.
- **Current active branch:** `fix/chat-dm-notification`

## Active Handoff — Server: Deploy DM Channel Notification System

**Summary:** DM channel notification system is implemented and needs deployment to production (`cloud.kimball.home`). When a user creates a DM channel, the target receives a high-priority push notification (Android) + in-app toast (Blazor) with 4 actions: Reply & Join, Reply without Joining, Ignore, Set Do Not Disturb.

**Deploy steps:**
1. `git pull` on `cloud.kimball.home` — branch `fix/chat-dm-notification` (or merge to main first)
2. Publish Chat.Host + Core.Server:
   ```
   sudo ./scripts/deploy.sh
   ```
   This rebuilds and deploys `DotNetCloud.Modules.Chat.Host` (new endpoints + event handler) and `DotNetCloud.Core.Server` (updated `IChatMessageNotifier`).

**New API endpoints to verify:**
```
POST /api/v1/chat/dm/{channelId}/accept   → { accepted: true [, message: {...} ] }
POST /api/v1/chat/dm/{channelId}/reply     → { replied: true, message: {...} }
POST /api/v1/chat/dm/{channelId}/ignore    → { acknowledged: true }
GET  /api/v1/notifications/preferences     → { pushEnabled, doNotDisturb, mutedChannelIds }
PUT  /api/v1/notifications/preferences     → { updated: true }
```

**Server-side changes summary (13 files, +351 lines):**
- `IPushNotificationService.cs` — Added `DmChannelCreated` notification category
- `IChatMessageNotifier.cs` — Added `DmChannelCreatedNotification` record, event, notify method
- `DmChannelCreatedEventHandler.cs` (NEW) — Sends push + in-process notification on DM creation
- `ChatEventSubscriber.cs` — Wired new handler with DI dependencies
- `ChannelMember.cs` — Added `IsDmAccepted` property
- `ChannelService.cs` — DM target gets `IsDmAccepted = false`
- `IChannelMemberService.cs` / `ChannelMemberService.cs` — Added `SetDmAcceptedAsync`
- `ChatController.cs` — 3 new endpoints (accept/reply/ignore)
- `ChatDtos.cs` — Added `AcceptDmDto`, `ReplyToDmDto`
- `GlobalChatNotificationState.cs` — DM notification state, timer, accept/dismiss
- `DmNotification.razor/.cs/.css` (NEW) — Blazor DM toast overlay with 4 action buttons
- `GlobalChatNotifications.razor/.cs` — Wired DM notification into global overlay
- `UserDndToggle.razor` (NEW) — Quick DND toggle in top bar user menu
- `MainLayout.razor` — Wired DND toggle

**Android client changes (already in branch, no server deploy needed):**
- `MainApplication.cs` — DM notification channel (High importance)
- `FcmMessagingService.cs` / `UnifiedPushReceiver.cs` — `dm_channel_created` push handler
- `DmNotificationActionReceiver.cs` (NEW) — Handles notification action intents
- `IChatRestClient.cs` / `HttpChatRestClient.cs` — Accept/Reply/Ignore/DND API methods
- `SettingsViewModel.cs` / `SettingsPage.xaml` — DND toggle in settings

**Verification:** `dotnet test` — 1301/1301 Chat tests pass. 576/576 Core.Server tests pass. All projects build clean.

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
- ✅ **DM notification flow:** `DmChannelCreatedEventHandler` subscribes to `ChannelCreatedEvent`. For `DirectMessage` channels only, it sends push via `IPushNotificationService` and raises `IChatMessageNotifier.DmChannelCreated` for in-process Blazor. `GlobalChatNotificationState` handles the Blazor-side toast. Android handles the push-side with 3 inline notification actions.

<!-- carry-forward contracts and old Android changes archived to CLIENT_SERVER_MEDIATION_ARCHIVE.md -->
