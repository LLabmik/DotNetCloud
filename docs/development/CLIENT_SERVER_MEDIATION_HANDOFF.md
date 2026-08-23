# Client/Server Mediation Handoff

Last updated: 2026-08-23 (SyncTray multi-folder sync — client testing COMPLETE ✅ on mint-OptiPlex-7010; all flows PASS, one client fix shipped; hand back to server agent for review)

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

## Active Handoff — mint-OptiPlex-7010: SyncTray Multi-Folder Sync — Client Testing COMPLETE ✅

**Target:** `mint-OptiPlex-7010` (production client → `https://cloud.dotnetcloud.net/`)
**Branch:** `fix/synctray-issues`
**Server deploy:** ✅ COMPLETE — deployed & verified on `cloud.kimball.home` (v0.4.07, HEAD `ffe882d5`; feature commit `a470c992`)
**Client deploy:** ✅ COMPLETE — client rebuilt (v0.4.07) + deployed to `/opt/dotnetcloud-desktop-client/SyncTray/` on mint-OptiPlex-7010, restarted, healthy.

**Summary:** All client testing completed and PASSED against `cloud.dotnetcloud.net`. One real client bug was found and fixed during testing (state DB schema init before selective-sync load on pre-existing DBs). Production client restored to original single-folder state after testing.

### Client testing report (2026-08-23) — ALL PASS ✅

1. **Build & deploy** ✅ — Client rebuilt with feature (`a470c992`) at v0.4.07, deployed to `/opt/dotnetcloud-desktop-client/SyncTray/`, launched clean (1 context, engine started, SSE connected, sync pass OK). Version bump applied (`Directory.Build.props` 0.4.06→0.4.07; Android csproj display 0.4.07 / versionCode 4).
2. **Multi-folder add flow** ✅ — Used the real client `SyncContextManager.AddFolderAsync` to add 2 folders (`/home/benk/synctray-test-a` → `ClientTest-A`, `-b` → `ClientTest-B`). Each registered via `POST /api/v1/files/sync/folders`, appeared in the tray (RefreshAccounts: 3 contexts), and synced correctly (marker files appeared in the scoped server trees). Re-registration is idempotent (same registration id, 1 occurrence). Cleaned up after test.
3. **Folder size limit** ✅ — Enabled limit (32 KB), created a 64 KB folder+file on the server. `SizeLimitDecisionRequested` fired; decision (skip) persisted as a `SizeLimit` rule; **no re-prompt** on subsequent passes (once-per-folder). `SyncFolderSizePlanner` verified: deepest over-limit folders excluded, parents + root kept. **Over-limit file content was NOT downloaded.** Minor cosmetic note: an empty parent folder directory may be created during the prompt pass (file content still skipped) — non-blocking.
4. **Per-root tray "Open Folder" entries** ✅ — Code-verified `RefreshOpenFolderMenu()` creates one "Open Folder" entry per synced root using each account's `LocalFolderPath`; runtime-verified with 3 contexts loaded (`RefreshAccounts: received 3 context(s)`).
5. **Remote-overlap validation** ✅ — Server rejects descendant (folder inside a registered folder) and ancestor (folder containing a registered folder) with **HTTP 409 Conflict**; equal = idempotent return (by design); disjoint registrations succeed. Verified against production API.
6. **Regression — existing single-folder sync** ✅ — Original `/home/benk/synctray` whole-account context continues to sync cleanly (tree/changes/ack OK, no errors) after upgrade; legacy `.selective-sync.json` imported once into `state.db` and removed.

### Client fix shipped during testing (IMPORTANT — review)

- **Bug:** On an existing (pre-feature) `state.db`, `StartContextInternalAsync` called `SelectiveSyncConfig.LoadAsync` (which queries `SyncFolderRules`) BEFORE the state DB schema evolution ran — engine failed to start with `SQLite Error 1: no such table: SyncFolderRules`.
- **Fix:** `src/Clients/DotNetCloud.Client.Core/Sync/SyncContextManager.cs` — `StartContextInternalAsync` now calls `stateDb.InitializeAsync(stateDatabasePath, …)` before `SelectiveSyncConfig.LoadAsync`. Idempotent (engine also initializes later).
- **Verification:** After fix, engine starts, `SyncFolderRules` created, legacy import runs, sync passes complete.

### Server status (completed — verified 2026-08-23)

- ✅ Deployed via `scripts/deploy.sh --force --verify` on `cloud.kimball.home` — all 15 targets succeeded, assembly hashes verified.
- ✅ `/health` + `/health/ready` → Healthy, 14/14 modules (Files Running).
- ✅ New table `[core].[SyncFolderRegistrations]` created on SQL Server (hyperdrive).
- ✅ Authenticated 200 checks now confirmed during client testing: `GET /api/v1/files/sync/folders` returns `{ success = true, data: [] }` (empty list), POST/DELETE work.

### Notes / hand back to server agent

- Minor observation for the server agent: gRPC streaming upload failed in this environment (`Status(StatusCode="Unknown")` on `UploadFileStreamAsync`) and correctly **fell back to HTTP chunked upload** — file still synced. Pre-existing behavior, not a regression.
- Empty parent folder directory may be created during the pass in which the size-limit prompt fires (file content still skipped). Optional polish.
- Test artifacts cleaned up: 2 test contexts removed, local test folders deleted, remote `ClientTest-A/B` scratch folders + registrations deleted. Production client restored to single-folder state and left running.
- **Hand back:** server agent may review the client fix above; no server-side changes needed. Ready for next handoff item.

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
