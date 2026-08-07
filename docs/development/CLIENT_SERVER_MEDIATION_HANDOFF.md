# Client/Server Mediation Handoff

Last updated: 2026-08-07 (Server issues resolved — monolith re-verify DM)

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

## Active Handoff — Monolith: Re-verify Android DM after server-side fixes

**Summary:** Both server-side issues found during Android DM verification are now resolved on `cloud.dotnetcloud.net`. Re-test Android DM functionality.

### Issue 1 — `IsDmAccepted` column missing: ✅ FIXED
- EF Core migration created for both SQL Server and PostgreSQL
- Applied to production DB during deploy (`ba9bd2d6`)
- Column `[core].[ChannelMembers].[IsDmAccepted]` (bit, NOT NULL) now exists
- `GET /api/v1/chat/channels/{id}/members` and messages endpoint should return 200 now

### Issue 2 — JWE access token encryption: ✅ CONFIRMED INTENTIONAL
- `AuthServiceExtensions.cs`: "Access tokens are encrypted (JWE) using the shared RSA encryption keys"
- This is standard OpenIddict config — NOT a bug
- The Android `id_token` workaround (commit `d618e2b2`) is the CORRECT approach
- Other clients (desktop SyncTray, Avalonia) should follow same pattern if they decode access tokens

### Monolith verification steps:
1. `git pull` on `fix/chat-dm-notification` branch
2. Build and deploy Android app to emulator/device
3. **Verify DM channel list** — should show display names (not `DM-{guid}-{guid}`)
4. **Verify DM channel members** — should load without 500 error (previously blocked)
5. **Verify push notifications** — send a DM, check push arrives with correct display name
6. **Verify 3 inline notification actions** — reply, mark read, dismiss (previously blocked)
7. **⚠️ Users must log out and back in** to capture the `id_token` from a fresh login

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
