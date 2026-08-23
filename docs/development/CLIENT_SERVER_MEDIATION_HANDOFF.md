# Client/Server Mediation Handoff

Last updated: 2026-08-23 (SyncTray multi-folder sync — server deployed ✅, hand back to client mint-OptiPlex-7010 for testing)

Purpose: shared handoff between client-side and server-side agents, mediated by user.

Archived context:

- Historical completed updates are in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Additional history remains available in git.

## Process Rules

**Agent autonomy (CRITICAL):**

- Both client and server agents work autonomously — they do NOT ask the moderator for context or permission.
- Agents pull the branch specified in the relay message, read the **Active Handoff** section, and execute the work described there independently.
- All actionable items, blockers, and technical details go directly in this document.
- **Current active branch:** `fix/synctray-issues`

## Active Handoff — mint-OptiPlex-7010: Test SyncTray Multi-Folder Sync Client

**Target:** `mint-OptiPlex-7010` (production client → `https://cloud.dotnetcloud.net/`)
**Branch:** `fix/synctray-issues`
**Server deploy:** ✅ COMPLETE — deployed & verified on `cloud.kimball.home` (v0.4.07, HEAD `ffe882d5`; feature commit `a470c992`)

**Summary:** The server-side multi-folder sync + folder size limit feature is **deployed and verified** on production. Hand back to the client: pull the latest client code and test the SyncTray multi-folder sync changes against `cloud.dotnetcloud.net`.

### Server status (completed — verified 2026-08-23)

- ✅ Deployed via `scripts/deploy.sh --force --verify` on `cloud.kimball.home` — all 15 targets succeeded, assembly hashes verified.
- ✅ `/health` + `/health/ready` → Healthy, 14/14 modules (Files Running).
- ✅ New table `[core].[SyncFolderRegistrations]` created on SQL Server (hyperdrive): columns `Id, UserId, RemoteFolderNodeId, RemoteFolderPath, CreatedAt, UpdatedAt, IsActive`; PK + user-id index + unique `(UserId, RemoteFolderNodeId)` index.
- ✅ `GET /api/v1/files/sync/folders` and `GET /api/v1/files/sync/changes` routes registered (401 unauthenticated — not 404).
- ⚠️ Authenticated 200 checks (`{ success = true, data: [] }`) still pending — confirm during client testing below.

### Client task — test SyncTray multi-folder sync against cloud.dotnetcloud.net

1. Pull `fix/synctray-issues` on `mint-OptiPlex-7010` and ensure the client build is current.
2. **Multi-folder add flow:** register 2+ local folders for sync; verify each registers via `POST /api/v1/files/sync/folders` (re-registration is idempotent), appears in the tray, and syncs correctly.
3. **Folder size limit prompt:** verify the client enforces the server folder size limit and prompts when a registered folder exceeds it.
4. **Per-root tray "Open Folder" entries:** verify each synced root has its own tray "Open Folder" entry that opens the correct local folder.
5. **Remote-overlap validation:** confirm the server rejects registering an equal/descendant/ancestor folder.
6. **Regression — existing single-folder sync:** confirm `GET /api/v1/files/sync/changes` (now recursive scoping) still syncs the original folder with no regressions.

### Useful server contract details for testing

- Endpoints: `GET/POST/DELETE /api/v1/files/sync/folders`; `GET /api/v1/files/sync/changes`.
- Responses are wrapped in the API envelope — unwrap via envelope helpers.
- Desktop OAuth client id: `OAuthConstants.ClientId = "dotnetcloud-desktop"`.
- Server version: 0.4.07.

### Follow-up

After client testing, report results (pass/fail + any blockers) here and hand back to the server agent for fixes if needed.

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
