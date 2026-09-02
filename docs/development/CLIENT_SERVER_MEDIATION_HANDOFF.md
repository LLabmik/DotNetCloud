# Client/Server Mediation Handoff

Last updated: 2026-09-02 (SyncTray Linux auto-update fix — branch `fix/synctray-update-on-linux`, commit `c25924ab`; awaiting live verification on Linux `mint-OptiPlex-7010`)

Purpose: shared handoff between client-side and server-side agents, mediated by user.

Archived context:

- Historical completed updates are in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Additional history remains available in git.

## Process Rules

**Agent autonomy (CRITICAL):**

- Both client and server agents work autonomously — they do NOT ask the moderator for context or permission.
- Agents pull the branch specified in the relay message, read the **Active Handoff** section, and execute the work described there independently.
- All actionable items, blockers, and technical details go directly in this document.
- **Current active branch:** `fix/synctray-update-on-linux`

## Archived Handoff — SyncTray test machine: DB Outage SyncTray Simulation (plan §11.4) ✅ PASS

**Status:** completed ✅ (2026-08-24, client agent — `Windows11-DNC`)
**Branch:** `fix/database-offline-recovery`
**Canonical plan:** `docs/DB_OUTAGE_RESILIENCE_PLAN.md` §11.4 (SyncTray simulation)

### Client pre-requisite (done)
- SyncTray **0.4.07** rebuilt from HEAD (`cbddab37`) and installed to `C:\Program Files\DotNetCloud\DesktopClient\SyncTray` (updater 0.4.07 too; old 0.4.02 backed up to `SyncTray.bak-0.4.02`).
- Server verified Healthy before outage: `/health/ready` Healthy, `database` Healthy, 14/14 modules.
- SyncTray running, 1 account, token valid, sync idle (0 changes) → tray green.

### Outage phase (moderator: `sudo systemctl stop dotnetcloud`)
- ✅ Server confirmed down: `cloud.dotnetcloud.net:443` connection refused.
- ✅ **Tray → gray** within one backoff interval: `10:04:40 WRN Server unreachable while syncing context` → `SyncEngine` sets `SyncState.Offline` → tray `TrayState.Offline` (gray); tooltip "DotNetCloud Sync — server unreachable, retrying automatically".
- ✅ **Automatic retry/backoff**: SSE reconnects 2s → 4s → 8s → 16s → 32s → 60s (attempts 1–8), then holds at 60s.
- ✅ **"Sync now" fast-fail**: connection-refused fails fast; `TimeoutHandler` caps requests at 30s; sync pass observed failing in ~12s. No hang.

### Recovery phase (moderator: `sudo systemctl start dotnetcloud`)
- ✅ Server recovered: `/health/ready` Healthy, `database` Healthy, 14/14 modules.
- ✅ **Automatic recovery, no manual restart**: failing sync passes at 10:07:25/10:07:45 (server still down) → successful pass at 10:08:04 (`Sync pass complete, RemoteChanges=0, LocalQueued=0, LocalApplied=0`) → `SyncState.Idle`.
- ✅ SSE reconnected automatically at **10:08:52** (within the 60s backoff cap).
- ✅ Tray returned to **green/idle** (visually confirmed by moderator).

**Result: PASS** — no client regressions observed. Evidence in `%LOCALAPPDATA%\DotNetCloud\logs\sync-tray20260824.log`.

## Archived Handoff — Android test machine: DB Outage Android Simulation (plan §11.5) ✅ PASS

**Status:** completed ✅ (2026-08-24, client agent — `monolith`)
**Branch:** `fix/database-offline-recovery`
**Canonical plan:** `docs/DB_OUTAGE_RESILIENCE_PLAN.md` §11.5 (Android simulation)
**Prerequisite (DONE — server agent):** Server deploy verified on `cloud.kimball.home` (`https://cloud.dotnetcloud.net/`): resilience code live, `/health/ready` Healthy, `database` Healthy, 14/14 modules. §11.2/§11.3 + integration tests passed. SyncTray client simulation §11.4 → PASS (archived above).

### Client pre-requisite (done)
- Android **0.4.07** rebuilt from HEAD (`8bdc1f57`) arm64-debug and installed to physical phone (Samsung S24 Ultra, `R5CWC356B2K`). Logged in to `https://cloud.dotnetcloud.net/`, SignalR connected, channels loaded.
- Server verified Healthy before outage: `/health/ready` Healthy, `database` Healthy, 14/14 modules.

### Outage phase (moderator: `sudo systemctl stop dotnetcloud`)
- ✅ Server confirmed down: `cloud.dotnetcloud.net:443` connection refused.
- ✅ **Global red banner appeared** ("Can't reach server — showing cached data. Changes will be queued.") over the channel list.
- ✅ **Chat showed cached messages** (Test 6/7/8, Aug 5 "Posting remotely" msg, etc.) with the banner still visible.
- ✅ **Sending a message queued it**: `OFFLINE_QUEUE_TEST_1301` appeared in the list with "just now" + the "Message queued — will send when you're back online." banner.

### Recovery phase (moderator: `sudo systemctl start dotnetcloud`)
- ✅ Server recovered: `/health/ready` Healthy, `database` Healthy, 14/14 modules.
- ✅ **Banner cleared automatically** (≤ ~20 s probe interval; confirmed via UI dump — banner gone).
- ✅ **Queued message flushed**: `GetMessagesAsync` fetched `OFFLINE_QUEUE_TEST_1301` from the server; message shows as sent ("1m ago", no queued indicator).
- ✅ SignalR reconnected automatically (`JoinChannelGroupAsync` joined `chat-channel-…`).

### Client bugs found & fixed during the sim (committed on this branch)
1. **Android receiver/service `Name` bug → cold-start crash.** `CalendarBootReceiver`, `CalendarAlarmReceiver`, `FcmMessagingService`, `UnifiedPushReceiver` were declared both manually in `AndroidManifest.xml` (`.X` → `net.dotnetcloud.client.X`) and via `[BroadcastReceiver]`/`[Service]` attributes without explicit `Name`, so the generated Java class landed in a `crc…` package → `ClassNotFoundException` when Android instantiated it (the sticky `BOOT_COMPLETED` broadcast crashed every cold start). Fixed by adding `Name = "net.dotnetcloud.client.X"` to the attributes (mirrors the working `[Service(Name=…)]` pattern).
2. **Phase E banner overlay crashed launch.** Wrapping the `Shell` in a `Grid` violates MAUI's "Parent of a Page must also be a Page". Replaced with a native Android platform overlay on `Android.Resource.Id.Content` driven by `ConnectivityViewModel`, offset below the status bar (`ResolveStatusBarHeight`). Old `ConnectivityBannerView.xaml` deleted.

**Result: PASS** — no client regressions. Evidence: `dnc-banner-visible.png`, `dnc-message-queued.png`, `dnc-recovered.png` (on monolith), UI-dump text nodes, and logcat (`adb logcat`).

## Archived Handoff — Android AI tab Phases B–F + E2E verification (2026-08-29) ✅ PASS

**Status:** completed ✅ (2026-08-29, client agent — `monolith`; server agent — `cloud.kimball.home`)
**Branch:** `feature/android-ai-tab`
**Canonical plan:** `docs/ANDROID_AI_TAB_PLAN.md`
Android AI tab implemented; server-side REST/Bearer 500 fixed + deployed; full E2E verified on-device (Samsung R5CWC356B2K). Full detail in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.

## Archived Handoff — AI request queueing: deploy to cloud.kimball.home ✅

**Status:** completed ✅ (2026-09-01, server agent — `cloud.kimball.home`)
**Branch:** `feature/ai-queuing` (deployed HEAD `846e3b17`)
**Canonical plan:** `docs/AI_REQUEST_QUEUEING_PLAN.md`
**From:** client agent (`monolith`), 2026-09-01

### Result (server-side verified on cloud.kimball.home)

- Deployed via `scripts/deploy.sh` (full build, all 15 targets). `dotnetcloud.ai.dll` + `DotNetCloud.Modules.AI.Data.dll` + AI RCL hash-verified in all 3 deploy locations.
- `/health/ready` **Healthy**; **14/14 modules** Running (incl. `dotnetcloud.ai`); `blazor.web.js` 200 (no static-asset regression).
- `dotnet test tests/DotNetCloud.Modules.AI.Tests/` → **35/35 passed** (incl. `AiCompletionQueue`).
- `GET /api/v1/ai/settings` → **401** without token (route live, `[Authorize]`).
- DB-backed settings confirmed in `dbo.SystemSettings`: `DefaultModel=gemma4:12b`, `Provider=ollama`, `ApiBaseUrl=http://monolith.kimball.home:11434/`.
- Ollama on `monolith.kimball.home:11434` reachable and serving `gemma4:12b` (matches DB).
- ⚠️ Token-authenticated settings response + Blazor "Generating…"/queue-position UI checks require a real user session (server agent cannot obtain a token without a password) — **left for user/browser verification** (see Active Handoff).

## Archived Handoff — AI request queueing: deployed to cloud + user-verified (2026-09-01) ✅ PASS

**Status:** completed ✅ (2026-09-01, server agent — `cloud.kimball.home`; user verification)
**Branch:** `feature/ai-queuing` (deployed commit `846e3b17`; HEAD `bda72641`)
**Canonical plan:** `docs/AI_REQUEST_QUEUEING_PLAN.md`
AI request queueing (FIFO `AiCompletionQueue`, live queue-position status, DB-backed DefaultModel, "Generating…" status fix) deployed to cloud and verified working in the browser. Full detail in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.

## Archived Handoff — Server: Blazor AI chat Abort button + auto-scroll (2026-09-01) ✅

**Status:** completed ✅ (2026-09-01, server agent — `cloud.kimball.home`)
**Branch:** `feature/ai-queuing` (deployed HEAD `76d9f1f4`; + new commit with the implementation)
**From:** client agent (`monolith`), 2026-09-01
**Canonical plan:** `docs/AI_REQUEST_QUEUEING_PLAN.md`
**Reference (Android impl):** commits `471e0c3b` (Abort button + stream-silence watchdog) and `c1b98997` (Abort visible during generating + auto-scroll streaming output)
**Target:** server `cloud.kimball.home` (`https://cloud.dotnetcloud.net/`)

**Task:** add two UX improvements to the Blazor AI chat in `src/Modules/AI/DotNetCloud.Modules.AI/UI/AiChatPage.razor` (+ `.razor.css` + collocated `AiChatPage.razor.js`), mirroring the Android client:
1. **Abort/Cancel button** next to the "In queue: position X of Y" status — visible while queued AND while generating — that cancels the request (removes it from the queue if still queued, or aborts the Ollama call if generating).
2. **Auto-scroll** the chat (and the internally-scrollable streaming region) to the bottom as tokens stream.

### Implemented (server — Blazor AI module)
- `CancellationTokenSource _streamCts` → token passed to `SendMessageStreamingAsync` (was `CancellationToken.None`).
- Abort button in `.ai-stream-actions` next to the queue pill, visible while `_isStreaming` (queued AND generating); queue pill gated on `_isQueued`.
- `AbortStream()` cancels `_streamCts` → gRPC stream cancelled → module host `AiChatService.SendMessageStreamingAsync` sees the cancelled token → `AiCompletionQueue` linked CTS removes the queued item (gives up its place) or aborts the in-flight Ollama call. Clears `_isQueued`/`_isStreaming`/`_isModelLoading`, cancels `_modelLoadCts`.
- `OperationCanceledException` handled quietly for user-initiated aborts (no error surfaced); partial/final message not persisted on abort.
- **Stream-silence watchdog** mirrored from Android (`471e0c3b`): 60s with no chunk (queue status or content) cancels the stream and surfaces "AI stream timed out — no response received. Try again." — no frozen "Generating…".
- Auto-scroll: collocated `UI/AiChatPage.razor.js` (`scrollChatToBottom`) imported via the `import` helper; invoked after each streamed chunk. Streaming output wrapped in a `max-height: 320px` internally-scrollable region (mirrors Android).

### Deploy + verify (cloud.kimball.home)
- `sudo ./scripts/deploy.sh --force --verify` → **all 15 targets succeeded** (deployed commit `76d9f1f4`).
- `/health/ready` → **Healthy**; **14/14 modules** Running (incl. `dotnetcloud.ai`); `blazor.web.js` → 200 (no static-asset regression).
- New static asset `_content/DotNetCloud.Modules.AI/UI/AiChatPage.razor.js` → **200, text/javascript**.
- `dotnet test tests/DotNetCloud.Modules.AI.Tests/` → **35/35 passed**.
- Migrations: none pending (no schema change).

**Pending user verification (browser):** send a message → chat auto-scrolls as tokens arrive; send a second message so it queues → **Cancel** button appears → tap Cancel → request leaves the queue and the first keeps generating; Cancel also available during generation. (Server agent cannot obtain a browser session/token without credentials.)

## Active Handoff

**Status:** ⏳ Awaiting live verification on Linux (`mint-OptiPlex-7010`)
**From:** client agent (`monolith`, Windows 11) — 2026-09-02
**Branch:** `fix/synctray-update-on-linux` (HEAD commit `c25924ab`) — **PULL THIS BRANCH**
**Topic:** SyncTray Linux auto-update fix — after clicking **"Restart to Update"** the **previous version kept running**.

### Root cause

`ClientUpdateService.ApplyUpdateLinuxAsync` (`src/Clients/DotNetCloud.Client.Core/Services/ClientUpdateService.cs`) generated a bash script that ran as the **unprivileged user** and copied the new payload into the **root-owned** install dir (`/opt/dotnetcloud-desktop-client/SyncTray`). The copy failed silently (script had no `set -e`/error handling), then the script `exec`'d the still-present **OLD binary** → the previous version kept running. (Windows avoids this by running its updater helper elevated via `requireAdministrator`; Linux had no equivalent.) Secondary bug: the script used a fixed `sleep 1` and never waited for the running client to exit, which raced the **single-instance file lock** (`Program.cs`) and made the relaunched instance quit immediately.

### Fix (committed `c25924ab` on `fix/synctray-update-on-linux`)

The rewritten Linux updater script now:

1. **Waits for the running client PID to fully exit** before touching the install dir (polls `kill -0`, 60 s cap) — no more fixed `sleep 1` / single-instance-lock race.
2. **Copies directly when the install dir is user-writable**; when it is **root-owned (`/opt`) it escalates ONLY the copy** via `pkexec` (PolicyKit auth dialog; `sudo -n` fallback), then relaunches as the **current user** so the desktop session (DISPLAY/Wayland/D-Bus) is preserved.
3. **On copy/elevation failure:** logs + shows a `notify-send` and **does NOT relaunch** (never silently starts the previous version).
4. Relaunches the updated client **detached** (`nohup … &`).
5. Writes an updater log to `/tmp/DotNetCloud/updates/apply-<guid>.log` and normalizes the generated script to **LF** (a CRLF checkout would otherwise break bash).

**Validation done so far (Windows/`monolith`):** `Client.Core` + `SyncTray` build clean (0 warnings); `ClientUpdateServiceTests` 22/22 pass (incl. 8 new `BuildLinuxApplyScript` tests); SyncTray update tests 15/15 pass; rendered script passes `bash -n`. **Not yet live-verified on Linux** — that is this handoff.

### Verification task for `mint-OptiPlex-7010` (Linux)

**0) Get the fixed build:**

```bash
cd /path/to/DotNetCloud
 git fetch origin
 git checkout fix/synctray-update-on-linux
```

**1) The update check needs a NEWER version than the one installed** (the client compares its assembly `InformationalVersion`; committed HEAD is `0.4.12`). To trigger an update:

- ☐ Bump `PatchVersion` `12 → 13` in `/Directory.Build.props` (and the Android csproj if building Android — not needed here).
- ☐ Publish + package the **linux-x64 desktop client** from this branch, e.g. on Windows/monolith run:

  ```powershell
  .\tools\packaging\build-desktop-client-bundles.ps1 -Version 0.4.13
  ```

  (or on Linux: `dotnet publish src/Clients/DotNetCloud.Client.SyncTray/DotNetCloud.Client.SyncTray.csproj -c Release -r linux-x64 --self-contained true -o <dir>` and tar the `linux-x64/payload` tree).
- ☐ Make the `0.4.13` asset discoverable by the updater: GitHub Release asset named `dotnetcloud-desktop-client-linux-x64-0.4.13.tar.gz` (the client checks the server `/api/v1/core/updates/check`, falling back to GitHub Releases) — or host it wherever the test server's update proxy points.

**2) Scenario A — official `/opt` install (the actual bug):**

- ☐ Install the **old `0.4.12`** release as root: `sudo ./install.sh` (goes to `/opt/dotnetcloud-desktop-client`), then run `dotnetcloud-sync-tray`.
- ☐ In SyncTray → Updates: **Check for updates** → **download** `0.4.13` → click **"Restart to apply update…"**.
- ✅ **Expected:** a **pkexec "Authentication Required"** dialog appears (enter the user's password) → the old client **fully exits** → the client **relaunches as `0.4.13`**. Verify: About/version shows `0.4.13`; `pgrep -af dotnetcloud-sync-tray` shows **exactly one** instance; `/opt/dotnetcloud-desktop-client/SyncTray/` payload actually replaced.

**3) Scenario B — per-user/writable install (dev-style):**

- ☐ Extract the bundle to a user-writable dir (e.g. `~/dnc-test/linux-x64`) and run `payload/SyncTray/dotnetcloud-sync-tray` directly; repeat the check/download/restart flow.
- ✅ **Expected:** no `pkexec` prompt (install dir is writable); old client exits; client relaunches as `0.4.13`; one instance.

**4) Failure-path check:**

- ☐ Repeat Scenario A but **cancel the `pkexec` dialog**.
- ✅ **Expected:** a **"Update failed"** desktop notification (`notify-send`) and **no** relaunch of the old binary (previous behavior silently restarted the stale old version).

**5) Diagnostics if anything looks wrong:**

- Updater log: `/tmp/DotNetCloud/updates/apply-*.log`
- Client log: `~/.local/share/DotNetCloud/logs/sync-tray*.log`
- Report back: PASS/FAIL per scenario + any log excerpts. On PASS, the moderator will merge the branch.

**Post-verification:** mark this handoff **completed ✅** in the doc header/Active section and (per moderator workflow) archive the detail.

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
