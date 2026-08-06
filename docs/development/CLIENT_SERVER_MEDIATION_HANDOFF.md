# Client/Server Mediation Handoff

Last updated: 2026-08-05 (Chat user search endpoint — cloud deploy needed before Android client continues)

Purpose: shared handoff between client-side and server-side agents, mediated by user.

Archived context:

- Historical completed updates are in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Additional history remains available in git.

## Process Rules

**Agent autonomy (CRITICAL):**

- Both client and server agents work autonomously — they do NOT ask the moderator for context or permission.
- Agents pull the branch specified in the relay message, read the **Active Handoff** section, and execute the work described there independently.
- All actionable items, blockers, and technical details go directly in this document.
- **Current active branch:** `feature/android-chat-direct-conversation`

## Active Handoff — Deploy Chat User Search Endpoint to Production (Critical Path)

**Summary:** The Android chat DM feature requires a user search REST endpoint. This endpoint (`GET api/v1/chat/users/search`) has been implemented and committed on `feature/android-chat-direct-conversation`. It must be deployed to production (`cloud.kimball.home`) before the Android client work can continue. The Android client phases 1.3–4 are blocked on this endpoint being live.

**Branch:** `feature/android-chat-direct-conversation` — commit `de26f1aa`

**What changed:**

1. **`src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Controllers/ChatController.cs`**
   - Added `IUserDirectory _userDirectory` field and constructor injection
   - New endpoint: `GET api/v1/chat/users/search?q={query}&maxResults=20`
   - Calls `IUserDirectory.SearchUsersAsync` + `GetAvatarUrlsAsync` for avatar enrichment
   - Returns `{ success: true, data: [{ userId, displayName, email, avatarUrl }] }`

2. **`tests/DotNetCloud.Modules.Chat.Tests/ChatControllerTests.cs`**
   - Added `Mock<IUserDirectory>` + 6 new test methods (empty query, whitespace, success, no results, no avatars, maxResults)
3. **`ChatImageUploadControllerTests.cs`, `DirectCallAndDmTests.cs`, `VideoCallControllerTests.cs`**
   - Updated constructor calls with `new Mock<IUserDirectory>().Object`

**Verification:**
```
dotnet test tests/DotNetCloud.Modules.Chat.Tests/
  → Passed! - Failed: 0, Passed: 1301, Skipped: 0, Total: 1301

curl -sk "https://cloud.dotnetcloud.net/api/v1/chat/users/search?q=alice"
  → 200 { "success": true, "data": [...] }
```

**Deploy steps:**
```bash
git pull origin feature/android-chat-direct-conversation
dotnet publish src/Modules/Chat/DotNetCloud.Modules.Chat.Host/
sudo systemctl restart dotnetcloud
curl -sk https://localhost:5443/health
```

**After deploy:** Android client work (Phases 1.3–4) unblocks and continues on `monolith`.

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
