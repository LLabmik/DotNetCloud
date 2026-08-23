# Client/Server Mediation Handoff

Last updated: 2026-08-23 (SyncTray multi-folder sync — server deploy to cloud.kimball.home)

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

## Active Handoff — cloud.kimball.home: Deploy Multi-Folder Sync Server Code

**Target:** `cloud.kimball.home` (production `https://cloud.dotnetcloud.net/`)
**Branch:** `fix/synctray-issues`
**Commit:** `a470c992`

**Summary:** Deploy the server-side portion of the SyncTray multi-folder sync + folder size limit feature. After the deploy is confirmed, client testing happens on `mint-OptiPlex-7010`.

### Server changes included (all under `src/Modules/Files/`)

1. **New table + migrations:** `SyncFolderRegistration` entity + EF config + `FilesDbContext` DbSet. Migrations committed for **both providers**:
   - PostgreSQL: `src/Modules/Files/DotNetCloud.Modules.Files.Data/Migrations/20260823053819_SyncFolderRegistration.cs`
   - SQL Server: `src/Modules/Files/DotNetCloud.Modules.Files.Data/Migrations/SqlServer/20260823053837_SyncFolderRegistration_SqlServer.cs`
2. **New REST endpoints:** `api/v1/files/sync/folders` (GET list / POST register / DELETE unregister) via `SyncFoldersController` + `ISyncFolderRegistrationService`. Validates folder ownership, folder type, and **remote-overlap** (rejects equal/descendant/ancestor registrations via `MaterializedPath`). Re-registration is idempotent.
3. **Recursive folder scoping:** `SyncService.GetChangesSinceAsync` / `GetChangesSinceCursorAsync` now scope `folderId` to a folder **and all descendants** (previously one level deep).
4. **DI:** `ISyncFolderRegistrationService` registered in `FilesServiceRegistration.AddFilesServices`.

### Deploy steps (cloud.kimball.home)

1. `git fetch origin && git checkout fix/synctray-issues && git pull`
2. **Back up before migrating** (production is SQL Server): DB backup + file storage + config per `docs/admin/server/UPGRADING.md`.
3. **Apply the Files migration** (SQL Server production):
   ```bash
   dotnet ef database update \
     --project src/Modules/Files/DotNetCloud.Modules.Files.Data \
     --context FilesDbContext
   ```
   Set `DOTNETCLOUD_DB_CONNECTION` to the SQL Server connection so the SqlServer design-time factory is selected. Apply the PostgreSQL variant too if a PG database is in use.
4. **Build/publish + deploy** (existing pattern):
   ```bash
   sudo systemctl stop dotnetcloud
   dotnet publish DotNetCloud.CI.slnf -c Release -o /tmp/dotnetcloud-publish
   sudo cp -r /tmp/dotnetcloud-publish/* /opt/dotnetcloud/server/
   sudo systemctl restart dotnetcloud
   ```
5. **Verify:**
   - Service healthy; module hosts pass `/health` and `/health/ready`
   - New table exists: `[core].[SyncFolderRegistrations]` (SQL Server)
   - `GET /api/v1/files/sync/folders` returns 200 `{ success = true, data: [] }` for an authenticated user
   - `GET /api/v1/files/sync/changes` still returns 200 (recursive scoping did not break existing sync)

### Follow-up

After the server deploy is confirmed, **hand back to `mint-OptiPlex-7010`** to test the SyncTray client changes against `cloud.dotnetcloud.net`: multi-folder add flow, folder size limit prompt, per-root tray "Open Folder" entries.

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
