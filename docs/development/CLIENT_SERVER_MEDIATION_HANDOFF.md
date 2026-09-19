# Client/Server Mediation Handoff

Last updated: 2026-09-19 (**Android chat alerts — ✅ SERVER HALF DEPLOYED + VERIFIED; the handoff is now the LIVE E2E ON THE PHONE → client agent `monolith`.** The parked UnifiedPush route is replaced by the app's own **conditional poll** of a new `GET /api/v1/chat/alerts` aggregate — no distributor app, no ntfy, no Google, no foreground service. Server + Android code is written, built and unit-tested here (Chat module 1431 pass; Android 478 pass / 1 skip; arm64 Debug 0 warnings); the server half is **DEPLOYED AND VERIFIED** on `cloud.kimball.home` (build 0 warnings, Chat tests 1432 pass, 15/15 deploy targets, `/health/ready` Healthy 14/14, route proven `401` vs control `404`), so the **Active Handoff is: the live E2E on the phone.** See the `## Active Handoff` section below and plan §12/§12.10. Earlier (2026-09-18): **UnifiedPush push transport (privacy-first) — 🅿️ PARKED BY THE OPERATOR.** The **Android half is built, committed and on-device verified**, but the feature was **parked** the same day: it asks **every user** to install a separate distributor app and point it at the instance, and the zero-setup alternative (background-job polling) is **too slow for chat**. **Nothing is authorised to start — the earlier promotion to the Active Handoff was reverted.** Canonical spec `docs/ANDROID_UNIFIEDPUSH_PLAN.md` (§11 = decision record + revisit options). Earlier: **Notes: note folder assignment** — a note can now be filed at creation time, moved/unfiled afterwards, and is tagged with its folder on the sidebar card; server + Blazor deployed and operator-verified, **Android picker wiring implemented and on-device verified** — **complete, archived**. Earlier: **Notes: note folder assignment** — a note can now be filed at creation time, moved/unfiled afterwards, and is tagged with its folder on the sidebar card; server + Blazor done, deployed and operator-verified from branch `fix/notes-folder-assignment`, Android picker wiring handed to monolith — see the deferred handoff below. Earlier same day: **Trash restore now preserves the original directory path** — server-side fix implemented, tested and deployed to `cloud.dotnetcloud.net` (branch `fix/trash-restore-original-path`); earlier, the client-side "ignore a synced folder" deletion bug was fixed + live-verified on SyncTray `0.6.7` (`7632722c`). Earlier: 2026-09-09 Presence indicators → **4-state** Online/Away/Do-Not-Disturb/Offline — full-stack code on `fix/android-improvements` at `c17c7fa3`, deployed and live-verified on `cloud.kimball.home` (archived below). Plan `docs/PRESENCE_DOTS_4STATE_PLAN.md`.)

Purpose: shared handoff between client-side and server-side agents, mediated by user.

Archived context:

- Historical completed updates are in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Additional history remains available in git.

## Process Rules

**Agent autonomy (CRITICAL):**

- Both client and server agents work autonomously — they do NOT ask the moderator for context or permission.
- Agents pull the branch specified in the relay message, read the **Active Handoff** section, and execute the work described there independently.
- All actionable items, blockers, and technical details go directly in this document.
- **🅿️ Parked (server agent — `cloud.kimball.home`):** `feature/android-unifiedpush` — **UnifiedPush push transport (privacy-first)**. **PARKED BY THE OPERATOR 2026-09-18 — do NOT start.** Reason: every user would have to install a separate distributor app and point it at the instance, and the zero-setup alternative (polling from a background job) is too slow for chat. The **Android half is built, committed (`9b80be85`) and on-device verified**; the **server half is untouched**. Canonical spec `docs/ANDROID_UNIFIEDPUSH_PLAN.md` (§11 = decision record + revisit options).
- **✅ ACTIVE (client agent — `monolith`): `chat-alerts` live E2E — the server half is DEPLOYED; nothing left to build server-side.** The new `GET /api/v1/chat/alerts` aggregate (plan §12.5/§12.10) is implemented **and deployed** to `cloud.kimball.home` (server agent — `cloud`, 2026-09-19). The only outstanding work is the **on-phone E2E** — see the **Active Handoff** section below. **No operator DNS record, certificate entry, ntfy service or firewall rule is involved.**
- **Archived (server agent — `cloud`):** `chat-alerts` server half — `GET /api/v1/chat/alerts` + `IChannelMemberService.GetAlertsAsync` **deployed and verified** on `cloud.kimball.home` 2026-09-19 (build 0 warnings; Chat module tests 1432 pass / 0 fail; `deploy.sh --force --verify` 15/15 targets; `/health/ready` Healthy 14/14 modules; route proof `401` vs control `404`). Android on-device E2E outstanding — Active Handoff above; full detail in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- **Archived (server agent — `cloud`):** `fix/notes-folder-assignment` — Notes folder assignment at create time + move/unfile; server + Blazor deployed and operator-verified, Android half implemented + on-device verified (client agent — `monolith`). **Complete — nothing outstanding**; recorded in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- **Archived (server agent — `cloud`):** `fix/trash-restore-original-path` — trash restore now preserves the original directory path (implemented, tested, deployed to `cloud.dotnetcloud.net` 2026-09-15; archived below)
- **Completed (awaiting moderator PR):** `fix/synctray-ignore-folder` — SyncTray **0.6.7**, "ignore a synced folder" can no longer delete the folder server-side; pushed `7632722c`, installed and live-verified on `mint-OptiPlex-7010`
- **Archived (server agent — `cloud.kimball.home`):** `fix/android-improvements` — Presence 4-state, deployed + live-verified on cloud 2026-09-09 (plan `docs/PRESENCE_DOTS_4STATE_PLAN.md`; archived below)
- **Still pending (mint22, dev):** `feature/module-widgets` — Module Home Widgets (plan `docs/MODULE_WIDGETS_PLAN.md`); kept below as a deferred handoff

## Active Handoff — Client (`monolith`): run the live chat-alerts E2E on the phone — server half ✅ DEPLOYED

**Status:** ✅ **server half deployed and verified** (2026-09-19, server agent — `cloud`). ⏳ **Remaining work = the on-phone E2E only** (client agent — `monolith`).
**Branch:** `feature/android-unifiedpush` (server + client are both on this branch; head `99474bc3`).
**Canonical spec:** `docs/ANDROID_UNIFIEDPUSH_PLAN.md` **§12** (design, adopted) and **§12.10 ("As built" — file list + the three new findings)**. Read §12.10 first; it supersedes the older drafts throughout the document.

### ✅ Server half — DONE, deployed to `cloud.kimball.home` (2026-09-19, server agent — `cloud`)

Nothing is left to write, decide or configure server-side. The deploy shipped `GET /api/v1/chat/alerts` (`ChatController.GetAlertsAsync` → `IChannelMemberService.GetAlertsAsync`): a constant-cost aggregate over `{ v, unread, mentions, unmutedUnread, unmutedMentions, topChannelId, changedAt }`, with a deterministic SHA-256 `ETag` and `If-None-Match` → `304`. Purely additive — `GET /api/v1/chat/unread` and `GetUnreadCountsAsync` are untouched, and there are **no migrations**. Nothing from the parked UnifiedPush server spec (§8) was needed or built.

| Check | Result |
| --- | --- |
| `dotnet build -c Release` | **0 warnings / 0 errors** |
| Chat module tests | **1432 pass / 0 fail** |
| `sudo ./scripts/deploy.sh --force --verify` | **15/15 targets**, module-host assembly hashes verified |
| Deployed Chat host | `/opt/dotnetcloud/modules/dotnetcloud.chat/dotnetcloud.chat.dll` rebuilt **03:43**, `GetAlertsAsync` present |
| Deployed DLL ↔ build output | **md5 identical** — `fc675c009cd853a52bbb0665a0b5c403` (deployed == `Modules.Chat.Host/bin/Release/net10.0`) |
| Deploy marker | `/opt/dotnetcloud/server/.last-deploy-commit` = `99474bc3` = branch HEAD |
| `/health/ready` | **Healthy — 14 module(s), all healthy** |
| `_framework/blazor.web.js` | **200** |
| Migrations | **none** (no schema change) |

**Routing proof through the gateway** (live on `cloud`, `https://localhost:5443`):

```bash
curl -sk -o /dev/null -w '%{http_code}\n' https://localhost:5443/api/v1/chat/alerts         # → 401  (route EXISTS; auth required)
curl -sk -o /dev/null -w '%{http_code}\n' https://localhost:5443/api/v1/chat/zzz-not-a-route  # → 404  (control: bogus route)
```

The `401` vs `404` contrast is the evidence: the core server's prefix route (`["api/v1/chat"] = "dotnetcloud.chat"`) matched **and** the Chat module host resolved the `alerts` endpoint. A stale or unpublished module host would answer `404` exactly like the control.

> ⚠️ **Not run on the server: the authenticated `200` / `If-None-Match` → `304` leg.** `cloud` has no non-interactive way to mint a bearer token — the seeded OIDC clients are `authorization_code` + `refresh_token` plus one `device_code`, there is no CLI token command, and no token was cached. That check is now the first item for `monolith` below.

### Verify (1) — authenticated `200` / `304` (client agent — run this first, takes seconds)

Use any live session's bearer token (the phone's own, or the web client's):

```bash
curl -sS -D- -o- -H "Authorization: Bearer $TOKEN" https://cloud.dotnetcloud.net/api/v1/chat/alerts
```

Expect `200`, the `{ "success": true, "data": { … } }` envelope, correctly spelled camelCase keys, and an `ETag` response header. Then repeat with `-H "If-None-Match: <that etag>"` and expect **`304` with an empty body** — the conditional poll is the whole point of the endpoint, so confirm both legs before the phone test.

### Verify (2) — Android side (needs the phone) — THE REMAINING WORK

1. Install the arm64 Debug build with `adb install -r --no-incremental` **and launch once** while signed in (the poll registers a persisted job, id **3108**, on process start).
2. `adb shell dumpsys jobscheduler | grep -A12 3108` → expect the chat-alert job, `PERSISTED`, with **any** network requirement (not unmetered — that is the media job, 3107).
3. Force-stop the app, then `adb shell cmd jobscheduler run -f net.dotnetcloud.client 3108` and read `adb logcat -s DotNetCloud` → expect the poll to run and re-arm. With the server deployed the run should report an alert (or a `304` when nothing changed).
4. **The real E2E:** force-stop the app, send a chat message from the web client as another user, and within the cadence (60 s while unread) expect a **generic** notification — title "New message" (or "You were mentioned"), **empty body, no sender or channel name** — which opens the channel on tap. Confirm with `dumpsys notification` (channel `chat_messages` / `chat_mentions`) and `dumpsys audio` (a `USAGE_NOTIFICATION` player for the package).
5. Confirm the negative cases: a muted channel produces nothing; reading the channel clears its pending alert without a second notification; **no foreground service** (`dumpsys activity services net.dotnetcloud.client` → expect empty).

### Notes / gotchas

- The client is already on the branch and unit-tested (433 pass / 1 skip after the prune); the arm64 Debug build is 0 warnings / 0 errors.
- **The client wake path is already verified on-device** (2026-09-19, R5CWC356B2K): job **3108** is `PERSISTED` with an any-network constraint, and a forced headless run hit `https://cloud.dotnetcloud.net/api/v1/chat/alerts` — it saw the expected pre-deploy `404`, handled it gracefully with no crash, and re-armed itself. **The `404` → `200`/`304` switch is the only thing left to observe, and the deploy that flips it is now in place.**
- The declined UnifiedPush connector was **deleted** in this branch: do not be surprised that the app no longer contains a receiver/registration state machine. The payload contract + generic renderer remain and are used by both the live SignalR path and the poll.
- The idle poll is **three cheap constant queries** and **zero aggregate queries** (not literally zero queries) — stated accurately in §12.5 so nobody is surprised by the DB cost.
- The Doze path uses `setExactAndAllowWhileIdle`, which the app already requests for calendar reminders; when the user has not granted it the inexact variant is used and only the latency stretches.

## Archived Handoff — Server: deploy the chat-alerts aggregate to `cloud.kimball.home` (2026-09-19) ✅ COMPLETED

**Status:** completed ✅ (2026-09-19, server agent — `cloud`) — deployed and verified. **The Android on-device E2E remains open and is the Active Handoff above.**
**Branch:** `feature/android-unifiedpush` · **Target:** core server + the Chat module host on `cloud.kimball.home`. **No operator DNS record, certificate entry, ntfy service or proxy rule was involved.**

`GET /api/v1/chat/alerts` (`ChatController.GetAlertsAsync` → `IChannelMemberService.GetAlertsAsync`) is live: build **0 warnings**, Chat module tests **1432 pass / 0 fail**, `deploy.sh --force --verify` **15/15 targets** with module-host assembly hashes verified, `/health/ready` **Healthy 14/14 modules**, `blazor.web.js` **200**, **no migrations**. Purely additive — `GET /api/v1/chat/unread` and `GetUnreadCountsAsync` are untouched. Full implementation detail + verification in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.

<details><summary>Original handoff text (superseded — kept for the root-cause record)</summary>

### What was deployed

- `GET /api/v1/chat/alerts` (`ChatController.GetAlertsAsync`) → `{ v, unread, mentions, unmutedUnread, unmutedMentions, topChannelId, changedAt }`, with `ETag` / `If-None-Match` → `304`.
- `IChannelMemberService.GetAlertsAsync` (`ChannelMemberService`) — constant query cost regardless of the caller's channel count, with a deterministic SHA-256 entity tag (**never** `string.GetHashCode()`: .NET randomizes string hashing per process, which would break conditional polling on every module-host restart).
- **Additive:** `GET /api/v1/chat/unread` and `GetUnreadCountsAsync` are untouched, so nothing else changes behaviour. **No migrations** — no schema change at all.
- **No gateway change is needed:** `Core.Server/Program.cs` routes by *prefix* (`["api/v1/chat"] = "dotnetcloud.chat"`), so `/api/v1/chat/alerts` is matched without any edge configuration.
- ⚠️ **Nothing from the parked UnifiedPush server spec (§8) was needed**: no `UnifiedPushHttpTransport`, no device-registration persistence, no ntfy, no `push.<domain>` DNS record or certificate, no proxy route.

### Original deploy steps (executed 2026-09-19)

1. Pull the branch (head of `feature/android-unifiedpush`).
2. `dotnet build -c Release` (expect 0 warnings) and `dotnet test` for the Chat module if you want your own evidence.
3. `sudo ./scripts/deploy.sh --force --verify` on `cloud.kimball.home`.
4. Confirm `/health/ready` Healthy and the usual module count.

</details>

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

## Archived Handoff — SyncTray Linux auto-update fix — live-verified on mint-OptiPlex-7010 (2026-09-02) ✅ PASS

**Status:** completed ✅ (2026-09-02, client agent — `mint-OptiPlex-7010`)
**From:** client agent (`monolith`, Windows 11) — 2026-09-02
**Branch:** `fix/synctray-update-on-linux` (released `v0.4.13`, tag `0d83c77c`; merged to `main`)
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

**Validation (Windows/`monolith` + Linux live):** `Client.Core` + `SyncTray` build clean; `ClientUpdateServiceTests` 22/22 pass (incl. 8 new `BuildLinuxApplyScript` tests); SyncTray update tests 15/15 pass; rendered script passes `bash -n`. **Live-verified on Linux `mint-OptiPlex-7010` — all scenarios PASS** (see results below).

### Verification results on `mint-OptiPlex-7010` (Linux) — ALL PASS ✅ (2026-09-02)

Setup note: because the *applying* client generates the updater script from its own code, the running "old" client must contain the fix. Released `v0.4.12` (`main`) predates `c25924ab`, so a **fixed `0.4.12`** was built from commit `c25924ab` (stamped `0.4.12`, includes the fix) and used as the current client in every scenario; the update target was the published `v0.4.13`.

- ✓ **Prep:** `PatchVersion` bumped `12 → 13` (`0d83c77c`); linux-x64 client published + packaged via `build-desktop-client-bundles.sh 0.4.13`; **GitHub Release `v0.4.13` published as Latest** with `dotnetcloud-desktop-client-linux-x64-0.4.13.tar.gz` (+`.sha256`) — API `/releases/latest` returns `v0.4.13`. (SyncTray update discovery uses the GitHub Releases fallback only — its typed `HttpClient` is registered with no `BaseAddress`, so the server `/updates/check` path is skipped.)
- ✓ **Scenario A — root-owned install (`pkexec` escalation): PASS.** Installed fixed `0.4.12` into a root-owned scratch dir (`/opt/dnc-sync-update-test`, `root:root`), ran the GUI flow (check → download `0.4.13` → "Restart to apply update"). Updater log: "Install directory is root-owned; requesting elevated copy via pkexec" → payload copied → relaunched as **`0.4.13`**, exactly one instance.
- ✓ **Scenario B — per-user/writable install: PASS.** Headless end-to-end against the real release (real `ClientUpdateService`): GitHub check found `0.4.13` → downloaded → applied → waited for the PID to exit → "Install directory is user-writable; copying payload directly" → payload replaced → relaunched as `0.4.13`, one instance (log: `Client version: 0.4.13`).
- ✓ **Failure path — cancel `pkexec`: PASS.** Cancelling the dialog produced updater log `ERROR: failed to copy updated files into ...` and **no relaunch** (the client had to be started manually for the next run); the desktop "Update failed" notification fired.
- ✓ **Regression checks:** `ClientUpdateServiceTests` 22/22 and SyncTray update tests 15/15 pass on Linux; a live sandbox apply of the real generated script confirmed wait-for-exit, direct copy, `chmod`, and detached relaunch of the NEW binary.

**Result: PASS** — no client regressions observed. The machine was reconfigured to **per-user installs** (benk's copy now at `~/.local/share/dotnetcloud-desktop-client/SyncTray`, `0.4.13`, direct-copy updates, no root password); the shared `/opt` install, `/usr/local/bin` launcher, and system `.desktop`/icon were removed. Branch merged to `main`; tag/release `v0.4.13` already points at a commit on `main`'s history.

## Archived Handoff — Blazor form defaults (2026-09-06) ✅ COMPLETED

**Status:** ✅ COMPLETED — Blazor form defaults implemented, deployed to mint22 dev, and user-tested (2026-09-06, server agent mint22). Next: user PR merge to main; client agent may wire more forms via the shared mechanism (plan §6).

**Target agent:** monolith (client, next steps)
**Branch:** `fix/form-submit-handling` (HEAD `5ddc81dd` = implementation)
**Canonical plan:** `docs/FORM_ENTER_SUBMIT_PLAN.md` (read it FIRST — fully self-contained)

### Completion record (2026-09-06, server agent mint22) ✅
- Implemented per the plan on `fix/form-submit-handling` (commit `5ddc81dd`): new `form-defaults.js` (shared, attribute-driven) registered in `App.razor`; auth pages wired (Login autofocus + `autocomplete="current-password"`, MfaVerify `data-autosubmit="6"`, MfaSetup `@bind:event="oninput"` + `@bind:after` auto-submit + autofocus); Files dialogs got `data-enter-submit`/`data-autofocus-first` (per-row for New File) and the four `Handle*KeyDown` C# handlers trimmed to Escape-only.
- **Acceptance fix 1 (Files Enter used stale value):** dialog inputs bound `@bind` (onchange) lagged the model on Enter → added `@bind:event="oninput"` to the four Files dialog inputs so Enter submits with the typed value (Rename `Test.txt`→`Test2.txt`; New File `Test.docx`).
- **Acceptance fix 2 (browser password save):** Login password field had no `autocomplete` → added `autocomplete="current-password"`; Firefox now prompts to save (Edge needs per-site state cleared — not a code issue).
- **Deploy + verify:** `sudo ./scripts/deploy.sh --force` → `/health/ready` + `/health/live` Healthy, no pending migrations; `_content/DotNetCloud.UI.Web/js/form-defaults.js` → 200 text/javascript. Files module tests 757/757.
- **User acceptance:** login autofocus + Enter-submit; Files create/rename via Enter (after fix 1); text areas still insert newline (no submit); no double actions. MFA verify/setup auto-submit at 6 digits implemented via native (`data-autosubmit`) and interactive C# (`@bind:after`) paths; full TOTP flow still needs a real authenticator session.
- **Relay → monolith (client):** implemented + deployed; create the PR to merge `fix/form-submit-handling` → `main`, and extend the mechanism to more forms later (plan §2 out-of-scope list, §6 extension path).

### Context (2026-09-06, from client agent monolith)
User requirement: "Default for forms (login, TOTP, file create name, etc.) should submit when Enter is pressed in a text box (not a text area)." Confirmed scope for this pass:

- **Login**, **TOTP** (verify + MFA-setup verify step), **Files** create/rename dialogs.
- **Shared global mechanism** (attribute-driven JS default), not per-form bespoke C# keydown handlers.
- All listed forms **auto-focus their first text box**.
- **TOTP auto-submits when the 6th digit is filled.**
- Files **New File** = **per-row** primary action (Enter in Document row → create document; Enter in freeform File row → create freeform file).

### What to do (server agent — mint22)
1. Read `docs/FORM_ENTER_SUBMIT_PLAN.md` and implement it on `fix/form-submit-handling`:
   - NEW `src/UI/DotNetCloud.UI.Web/wwwroot/js/form-defaults.js` (full source in plan §4.2).
   - Register it in `src/UI/DotNetCloud.UI.Web/Components/App.razor` (before `_framework/blazor.web.js`; versioned include).
   - Auth: `Login.razor` autofocus Username; `MfaVerify.razor` add `data-autosubmit="6"`; `MfaSetup.razor` (interactive EditForm) → `@bind:event="oninput"` + `@bind:after` auto-submit at 6 digits + autofocus.
   - Files: `UI/FileBrowser.razor` → `data-enter-submit` on the New Folder/rename containers and on **each** `.create-file-row`; `data-autofocus-first` on the dialog containers; trim the four C# `Handle*KeyDown` handlers to **Escape-only** (avoid double actions).
2. Build the changed projects — 0 warnings (`TreatWarningsAsErrors` is on).
3. Deploy to mint22 dev via the usual deploy script; verify `/health/ready` Healthy, no pending migrations, and the new static asset `_content/DotNetCloud.UI.Web/js/form-defaults.js` returns 200.
4. Record server-side verification, then hand the interactive **browser acceptance matrix (plan §7.3)** to the user/moderator (server agent cannot obtain a session).

### Do NOT (this pass)
- Do NOT touch Register/Forgot/Reset password, admin forms, Profile, or other modules' dialogs (plan §2 out-of-scope list). User will request more forms later.

### Notes for the implementer
- Module markup lives under each module RCL `UI/` folder (e.g. `src/Modules/Files/DotNetCloud.Modules.Files/UI/`). If `read_file`/grep tooling looks stale there, read from disk (`git show HEAD:<path>` / `Get-Content`) — files may be open in an editor buffer.
- Interactive `EditForm` auto-submit MUST be C# (`@bind:event="oninput"` + `@bind:after`), NOT JS `data-autosubmit` (JS `requestSubmit()` races the Blazor model round-trip) — plan §4.1-B explains.
- Keep the existing Escape-to-close behavior on the Files dialogs. No schema/CSS/test-project changes expected.

## Archived (deferred) Handoff — Module Home Widgets (mint22, dev) — STILL PENDING

**Status:** ⏳ DEFERRED (kept for mint22) — Module Home Widgets (2026-09-06, client agent). Plan committed on `feature/module-widgets`; awaiting mint22 implementation.

**Target agent:** mint22 (server)
**Branch:** `feature/module-widgets`
**Canonical plan:** `docs/MODULE_WIDGETS_PLAN.md` (read it FIRST — fully self-contained; written to be implementable with no prior context)

### What to do (server agent — mint22)
1. Implement `docs/MODULE_WIDGETS_PLAN.md` on branch `feature/module-widgets`, following it end-to-end:
   - **Phase 0** — shared widget infrastructure: `WidgetUiRegistry`, `WidgetCard`, `WidgetUiRegistrationHostedService`, plus `Program.cs` and `Home.razor` wiring.
   - **Phases 1–3** — the 12 widget projects (`DotNetCloud.Modules.<Module>.Widget`), including the new in-process "recent" service methods (Photos/Notes/Chat/Tracks) and the full gRPC chains for process-isolated modules (Calendar/Contacts/Bookmarks/Email/AI).
   - **Phase 4** — solution + CI filter + Core.Server `ProjectReference`s + `KnownWidgetDescriptors` table.
2. Build `dotnet build DotNetCloud.CI.slnf -c Release` — must be 0 warnings (`TreatWarningsAsErrors` is on).
3. Add + run the unit tests listed in plan §12.1 (`dotnet test` per affected module test project).
4. Deploy to mint22 dev via the usual deploy script; verify `/health/ready` Healthy and all modules Running; verify Home renders the widgets and "Your Apps" is gone.
5. Record server-side verification, then hand the browser acceptance checks (plan §13) to the user/moderator.

### Do NOT (this pass)
- Do NOT add per-user widget show/hide or drag-and-drop reorder (deferred follow-up — plan §2 decision 7).
- Do NOT add widgets for About, Example, or Search (plan §1/§2).

### Notes for the implementer
- `read_file` may return stale editor-buffer content; if files look wrong, read from disk (`git show HEAD:<path>`).
- Process-isolated widgets do NOT build a `CallerContext` — the gRPC `I*ApiClient` resolves the user internally; in-process widgets DO build one (plan §6.3).
- Verify two flagged spots while implementing: Tracks `WorkItemAssignment.UserId` navigation property (plan §9.4) and the Email thread query location behind `ListThreadsAsync` (plan §10.4).
- Module ids in `KnownWidgetDescriptors` must match `InstalledModules.ModuleId` exactly (plan §11.4 table).

## Archived Handoff — Server: trash restore preserves the original directory path (2026-09-15) ✅ COMPLETED

**Status:** completed ✅ — implemented, unit/integration tested and **deployed to `cloud.dotnetcloud.net`** (server agent — `cloud`).
**Branch:** `fix/trash-restore-original-path`
**Client-side counterpart (unchanged):** SyncTray `0.6.7` "ignore a synced folder" fix (`fix/synctray-ignore-folder`, `7632722c`) — this pass was server-only, no client changes were needed.

`TrashService` now resolves the original parent with `IgnoreQueryFilters()` and restores the deleted ancestor chain, so a cascade-deleted subtree returns to its **original path** instead of being flattened into the root; `RestoreSubtreeAsync` recomputes `ParentId`/`MaterializedPath`/`Depth` (and assigns sync sequences) for every descendant; `RestoreAllAsync` restores only top-level trashed nodes (plus orphans of purged parents), never the descendants individually; and `TrashItemDto.OriginalPath` now exposes a **name-based** location (`/Photos/2024`, `/` at root), surfaced as a **Location** column in the Blazor trash UI, which also gained a restore spinner. New endpoint: `POST /api/v1/files/trash/restore-all`.

**Operator note:** the `AutoUpload` / `Gretchen Goes to Nebraska` trees stay unrecoverable (the original flat restore consumed their trash entries). This work makes every *future* restore correct — that is its whole purpose.

Full implementation detail, acceptance-criteria mapping, test counts and deploy evidence: `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.

<details><summary>Original handoff text (superseded — kept for the root-cause record)</summary>

**Target machine:** server agent (`cloud` / `cloud.dotnetcloud.net`). Client-side work is complete and shipped — this is **server-only**.

**Status:** ☐ **NOT STARTED** — awaiting server agent.

### Why this is actionable now

The client-side "ignore a synced folder" bug is **fixed and live** (SyncTray **0.6.7**, branch `fix/synctray-ignore-folder`, commit `7632722c`). That bug had deleted two folders from `cloud.dotnetcloud.net` via `DELETE /api/v1/files/{id}`:

- `AutoUpload` — `019f73be-9788-7f8f-a44a-82e138d4637d` (2026-09-15 04:34:46)
- `Gretchen Goes to Nebraska` — `01a02d90-2db1-7255-97a7-065f681c5776` (2026-09-15 04:34:54)

Both went to trash (soft delete). The operator restored `AutoUpload` from the trash UI and **it restored flat into the root directory instead of recreating the directory structure** — every descendant landed at the root, and the original tree could not be reassembled. `GET /api/v1/files/trash` now returns `{"success":true,"data":[]}`, so that restore consumed the trash: **treat the `AutoUpload` tree as unrecoverable.** This handoff is about making restore correct for the future, not about recovering the two folders.

### Root cause (verified by code reading, `DotNetCloud.Modules.Files`)

1. `src/Modules/Files/DotNetCloud.Modules.Files.Data/Configuration/FileNodeConfiguration.cs:54` — global soft-delete filter: `builder.HasQueryFilter(n => !n.IsDeleted);`
2. `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/TrashService.cs` (`RestoreAsync`, ~line 71) resolves the original parent **without `IgnoreQueryFilters()`**:

   ```csharp
   if (node.OriginalParentId.HasValue)
   {
       var originalParent = await _db.FileNodes          // ← query filter applies: deleted parents are invisible
           .FirstOrDefaultAsync(n => n.Id == node.OriginalParentId.Value, cancellationToken);
       restoreParentId = originalParent?.Id;
   }
   ```

   For a child of a cascade-deleted folder its parent is *itself still trashed*, so this returns `null` → `restoreParentId = null` → execution falls into the "If original parent is gone, restore to root" branch (`node.ParentId = null; node.MaterializedPath = $"/{node.Id}"; node.Depth = 0;`). **Every such node is therefore restored into the root.**
3. `TrashService.RestoreAllAsync` (~line 145) then makes it systemic: it selects `Where(n => n.IsDeleted && ... && n.OriginalParentId != null)`. A folder deleted *at the root* has `OriginalParentId == null` and is **skipped**, while its descendants (whose `OriginalParentId` *is* set) are each restored individually — every one of them hits the null-parent path above. Result: "restore all" recreates nothing and dumps the entire subtree flat into the root. This is exactly the observed `AutoUpload` outcome.
4. `TrashService.RestoreDescendantsAsync` (~line 294) clears `IsDeleted`/`DeletedAt`/`DeletedByUserId` for descendants but never recomputes `MaterializedPath`/`Depth`, and it only runs when the restored node is itself a `Folder`. So descendants restored under a relocated parent keep stale paths.

### Required fix

1. **`RestoreAsync`** — look up `OriginalParentId` with `.IgnoreQueryFilters()`. If that parent is *also* deleted, restore the ancestor chain first (recreate each missing directory level, in order) and then attach the node to the deepest level, instead of silently falling back to the root. Only fall back to the root when no ancestor can be restored (e.g. permanently purged) — and if so, log it, don't do it silently.
2. **`RestoreAllAsync`** — select top-level trashed nodes (folders/files deleted from the root, i.e. `OriginalParentId == null` / `ParentId == null` at delete time) and restore them, letting descendants follow. Do **not** select descendants and restore them individually.
3. **`RestoreDescendantsAsync`** — after the restored root's final location is known, recompute `ParentId`, `MaterializedPath` and `Depth` for every descendant so the subtree is internally consistent.
4. **Original-path exposure** — `TrashItemDto.OriginalPath` is currently `FileNode.MaterializedPath`, which is an **ID path** (`/id/id/...`), not a directory path a user or the trash UI can act on. If the UI is meant to show where an item will be restored to, expose a name-based path (walk `OriginalParentId` up to the root) so the user can see the directory structure is remembered.
5. **Name conflicts** — keep the existing `GetRestoreNameAsync` auto-rename, but apply it per recreated directory level too, so recreating an ancestor chain cannot collide with an existing folder of the same name.

### Acceptance criteria (must be live-verified on the server)

1. Create `A/B/file.txt`; delete folder `A`. `GET /api/v1/files/trash` lists `A`. `POST /api/v1/files/trash/{A}/restore` → `A/B/file.txt` exists **at the original path**.
2. Trash a single file that lives inside a *live* folder → restore returns it to that folder (regression guard for the existing good path).
3. Trash a folder whose parent folder is *also* trashed, then restore the child individually → the ancestor chain is recreated (or the API explicitly reports that the ancestor must be restored first — pick one behaviour and document it).
4. "Restore all" (Blazor trash UI + `RestoreAllAsync`) → the original directory structure is recreated and **nothing** is dumped flat into the root.
5. No regressions: permanent delete, empty trash, 30-day retention cleanup, and quota accounting all still behave.
6. Add regression tests covering the cascade-delete → restore round-trip (the current suite passes while this bug is live, so it has no coverage of it).

### Do NOT (this pass)

- Do not change the client. `IDotNetCloudApiClient` has **no** trash list/restore methods and SyncTray has **no** trash UI — if the client should offer restore, that is a separate client task; raise it instead of adding it here.
- Do not touch the client-side deletion/ignore logic — that path is fixed, unit-tested (312 + 149 tests) and live-verified.

</details>

## Archived (deferred) Handoff — Android Notes: wire the note folder picker (create + move) — DONE, on-device verified (2026-09-15)

**Status:** archived — the **server + Blazor half is complete, tested and deployed** (`fix/notes-folder-assignment`; full implementation detail + verification in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`). **The Android half is implemented, committed and now verified on-device** (client agent — `monolith`): the original commit (`3df31674`) went in with the no-commit-until-verified gate knowingly waived, and the deferred on-device pass on 2026-09-15 found + fixed a filter-chip defect on `fix/android-notes` (see the status block below).

> **Android implementation status (2026-09-15, client agent — `monolith`):**
>
> - `NoteEditViewModel` loads the caller's folders into `FolderOptions` in **create _and_ edit** mode (a leading "None (unfiled)" entry) and mirrors the picker selection onto `SelectedFolderId`; `BuildUpdateDto()` sends **`ClearFolder = true`** whenever the note is left unfiled — that flag is what makes "move to None" actually unfile (a bare `folderId: null` means "no change" to the API).
> - A note filed in a folder **outside** the caller's own list (a shared note's owner folder) gets a "Shared folder" picker entry so saving can never silently unfile it, and the picker is hidden for a non-owner (`ViewerPermission != null`), matching the server's folder-ownership rule.
> - `NotesPage`'s **+** now carries the active folder chip (`NoteEdit?FolderId=…`) so a note created from a folder starts filed, and each note card shows the red folder tag (`NoteFolderTagConverter` + `NoteFolderLabels.Resolve`, falling back to "Shared folder").
> - Offline parity needed no payload change — the queued `OfflineNoteUpdatePayload`/`OfflineNoteCreatePayload` carry the same DTOs; a unit test asserts `ClearFolder` is present in the queued JSON.
> - Tests: **334 pass / 1 skip** (15 `NoteEditViewModelTests`, 5 `NoteFolderLabelsTests`, 4 `NotesViewModelTests` folder-chip tests, 5 `FolderChipBackgroundConverterTests`, 1 PUT-body test proving `clearFolder` reaches the wire); Android arm64 Debug build **0 warnings / 0 errors**.
> - ✅ **On-device pass DONE 2026-09-15 (`fix/android-notes`)** — the earlier emulator sign-in blocker was irrelevant on the phone, which already had a live session. It found one defect: the notes filter-chip row (`NotesPage.xaml`) set `BindableLayout.ItemsSource` with **raw child elements and no `ItemTemplate`**, so `BindableLayout` replaced them with its default label — each folder rendered as the raw `NoteFolderDto { … }` record `ToString()` — and the static "All Notes" chip never rendered at all. Fixed by building the chips in `NotesViewModel.FolderChips` ("All Notes" first, then one per folder) behind an explicit `ItemTemplate`, plus a working selected-chip highlight (`FolderChipBackgroundConverter`; the old "All Notes" chip bound a `bool` straight to `BackgroundColor`, which can never apply). Verified on R5CWC356B2K: chips read "All Notes"/"Finance", folder tap filters + highlights, "All Notes" tap restores the full list, no crashes, operator-verified manually on the same build.
**Target agent:** monolith (client, Windows 11)
**Branch:** `fix/notes-folder-assignment` (server agent — `cloud`)
**Why the split:** the Android MAUI project cannot be built on the Linux server box (no JDK / Android SDK — that is why `DotNetCloud.Client.Android*` is excluded from `DotNetCloud.CI.slnf`), so the Android wiring is handed off rather than written blind.

### Context (server agent — `cloud`)

Notes folders existed as a sidebar filter only: a note could not be filed at creation time and could never be moved out of a folder. The server + Blazor halves are now fixed:

- `UpdateNoteDto.ClearFolder` (`DotNetCloud.Core/DTOs/NoteDtos.cs`) distinguishes "leave the folder alone" from "move to unfiled". `folderId` wins when both are supplied.
- `NoteService.UpdateNoteAsync` applies the move/unfile and validates the target folder belongs to the **note owner** (`NOTE_FOLDER_NOT_FOUND` otherwise).
- `UpdateNoteRequest.clear_folder = 14` (proto) is mapped by `NotesGrpcService`, and `NotesGrpcApiClient.UpdateNoteAsync` now maps `folder_id` **and** `clear_folder` (it previously dropped `folder_id` entirely).
- Blazor `NotesPage.razor`: folder `<select>` in the create/edit form (new notes inherit the active sidebar folder) + an owner-only move/unfile picker in the detail view; sidebar note cards show the folder name as a small red tag (folder labels fall back to "Shared folder" when the folder belongs to the note's owner rather than the caller).
- REST contract: `PUT /api/v1/notes/{noteId}` with `{"clearFolder": true}` unfiles; documented in `docs/api/NOTES.md`.

### The Android gap

`src/Clients/DotNetCloud.Client.Android/Views/NoteEditPage.xaml` already contains a `Picker x:Name="FolderPicker"` (Row 3, "Folder picker"), but it is **wired to nothing** — there is no `FolderPicker` reference in any `.cs` file, no `ItemsSource`, and no selection handler, so it renders empty and does nothing. `NoteEditViewModel.SelectedFolderId` is only ever set from the loaded note.

### What to do (client agent — monolith)

1. **Populate the picker:** expose the user's folders on `NoteEditViewModel` (`ObservableCollection<NoteFolderDto> Folders` + a first "None (unfiled)" option, loaded via `INotesRestClient.ListFoldersAsync`) and bind `FolderPicker.ItemsSource` / `SelectedIndex` (or bind `ItemsSource` + `SelectedItem` to a small option record with `ItemDisplayBinding="{Binding Name}"`).
2. **Create:** ensure folders load in create mode too — `NoteEditViewModel.LoadAsync` currently returns early when `!IsEditing`, so folder loading must happen before that early return.
3. **Update/unfile:** when the user selects "None", send `new UpdateNoteDto { FolderId = null, ClearFolder = true, ... }` — without the flag the server treats `FolderId == null` as "no change", so the note would stay filed.
4. **Offline queue parity:** the two offline payloads in `NoteEditViewModel.SaveAsync` (`OfflineNoteUpdatePayload` / `OfflineNoteCreatePayload`) must carry `ClearFolder` as well, otherwise a queued move-to-unfiled silently no-ops on replay.
5. **Optional parity with Blazor:** pass the currently filtered folder into `NoteEdit` (`Shell.Current.GoToAsync($"NoteEdit?FolderId={...}")` + `[QueryProperty]`) so a note created from a folder chip starts in that folder.
6. **Optional parity — folder tag on the note card:** the Blazor sidebar shows each note's folder name as a small red tag at the card's bottom (`--color-danger`, 10px). To mirror it on the Android notes list, resolve the label from the already-loaded `Folders` collection and fall back to "Shared folder" when the note's folder isn't in the caller's own list (folders belong to their owner — a shared note's folder will not be in the sharee's list).
7. **Verify on device:** create a note with a folder selected; move it between folders; move it to "None" and confirm in the web UI that it becomes unfiled. Add/extend `NotesViewModelTests`-style unit coverage if the VM surface changes.

**Deploy note:** nothing else is required server-side — the REST/gRPC contract above is already live.

---

## Parked Handoff (deferred) — Server: UnifiedPush push transport (privacy-first) → `cloud.kimball.home` — ANDROID HALF BUILT, PARKED BY THE OPERATOR (2026-09-18)

> 🅿️ **PARKED BY THE OPERATOR (2026-09-18) — do NOT start this work.** The design, the spec and every operator answer are complete, and the **Android half is built and verified**, but the feature was parked: it asks **every user** to install and configure a separate distributor app, and the zero-setup alternative (background-job polling) was judged **too slow for chat**. The earlier promotion of this section to the Active Handoff was **reverted** the same day.
> **Source of truth:** `docs/ANDROID_UNIFIEDPUSH_PLAN.md` (if this section and the plan ever disagree, **the plan wins** — update this section from it). **Read the plan's §11 (decision record + transport trade-offs) before proposing a restart.**
> **All operator answers are already in hand (2026-09-18):** every §9 item is resolved; §7.1 step 1 is answered (a URL containing a path is rejected); §9.10 approved the Host-routed `push.<domain>` on the same 443.
> **The Android half is DONE** (client agent — `monolith`, on-device verified), so a restart needs the **server half only** — plus re-deciding whether the per-user distributor setup is acceptable.

**Target machine:** server agent — `cloud.kimball.home` (production, `https://cloud.dotnetcloud.net/`; **real install, not Docker**: systemd unit `dotnetcloud`, layout from `tools/install.sh` (`/opt/dotnetcloud`, `/etc/dotnetcloud`), source deploys via `scripts/deploy.sh`; direct Kestrel on 5443 with the router forwarding 443→5443 and a Let's Encrypt cert; **no reverse proxy in front** — which is why the push server is exposed **on the app's own 443, routed by Host** (Part 1) rather than given its own port and NAT rule; note it still needs a DNS record + a certificate entry for the push subdomain).
**Branch:** `feature/android-unifiedpush` — docs `366fac6f` (plan) + `0bd6a6dd` (markdown formatting), then the **Android implementation** (2026-09-18). Pull this branch to see the client half you are pairing with.
**Canonical plan:** `docs/ANDROID_UNIFIEDPUSH_PLAN.md` — read §3.2 (server blockers S1–S5), §4.2 (payload contract), §7 (verification), §8 (this handoff's spec) **before starting any code**.
**Status:** 🅿️ **PARKED BY THE OPERATOR (2026-09-18) — do NOT start.** Design, spec and every operator decision are complete; the **Android half is built and verified**; the **server half is untouched**. Reason for parking: the per-user distributor setup, and polling being too slow for chat — see the plan's §11.
**Android half:** ✓ **implemented and on-device verified** (2026-09-18): plan Phases 1, 2 and the client-side of Phase 5 — see the plan's "Implementation notes" for the file list and the exact evidence. It is **not** a prerequisite for your work — only the §7 live E2E needs both halves, and that one is handed back to the client agent when you finish.

### Why this exists

The Android app cannot receive background chat alerts, and Google may not be part of the fix:

- The googleplay build ships **no `google-services.json`** → Firebase never initialises → no FCM token can ever be issued (confirmed on-device 2026-09-17).
- **Operator decision (2026-09-17):** Google is out of the push path entirely — **no message content *and* no metadata** through Google. FCM is therefore not acceptable even with encrypted or content-free payloads.
- Transport of record = **UnifiedPush (UP) with a self-hosted ntfy**, with **completely generic notifications** ("New message" — no sender/channel names, no text, nothing that a push server or observer could interpret).
- Foreground alerts already work (`docs/ANDROID_CHAT_ALERT_SOUND_PLAN.md`); this handoff is the background half.
- Do **not** re-introduce a foreground service: the `dataSync` FGS was removed permanently (`faae32c9`, Android 15/16 rolling daily budget) and `remoteMessaging` is not a valid replacement type.

### Scope — do it in this order

1. **Stand up ntfy on loopback and expose it as a Host on the app's own 443** (§7.1, Part 1 below) — nothing else can be live-verified until it exists.
2. **Server code** (§8.1–§8.5).

⚠️ Items 2–5 of the server code are **transport-neutral** — they also repair the existing FCM path and are required for *any* push to work at all. Only §8.1 (the real UP transport) and the ntfy deployment are UP-specific. Do not skip them as "FCM-only later".

---

#### Part 1 — ntfy on loopback, proxied through the app's own port

**Design (decided 2026-09-18; ⚠️ revised 2026-09-18 — the "no extra hostname" part is not achievable, see step 1): push must not add a port or a firewall rule.** The public surface stays on 443 (router → Kestrel/5443); the push **host** is an additional name on that same 443. ntfy runs **loopback-only** and Core.Server routes that host to it — the pattern already shipped for Collabora (`MapCollaboraReverseProxy()` in `Core.Server/Program.cs:1097` maps `/hosting`, `/browser`, `/cool`, `/lool` to a loopback `coolwsd` via `Files:Collabora:ProxyUpstreamUrl`, precisely so "only one firewall port is needed").

**Do not use Docker on this host** and do not add a `docker-compose.yml` service for it — `docker-compose.yml` in the repo is for other users' installs. This box is a real install (apt + systemd + `/opt/dotnetcloud`).

1. **✅ First task — ANSWERED 2026-09-18 by the client agent; do NOT re-test it.** The distributor **rejects a push server URL containing a path**, so `<server-url>/<topic>` with a path cannot be used. Evidence (ntfy Android app against a self-hosted ntfy — the client agent's local rig, `http://192.168.0.154:2586`, was the control):
   - control root URL `http://192.168.0.154:2586` → **accepted** (dialog closes, the value persists);
   - `http://192.168.0.154:2586/push` → inline error **"Enter a valid service URL, e.g. https://ntfy.example.com"** and SAVE is **refused** (the dialog stays open, nothing is stored);
   - `https://ntfy.example.com/push` → the **same** error, so the rejection is **scheme-independent** and not an http-only quirk.
   The field is validated as scheme + host + optional port only.
   - ✅ **APPROVED by the operator (2026-09-18, plan §9.10): use `push.dotnetcloud.net` on the same 443, routed by the `Host` header inside Kestrel.** No new port and no new firewall rule — but it **does** need a DNS A record for the subdomain **and a certificate for that name** (a SAN entry on the existing Let's Encrypt cert, or a second cert). Do **not** build the `/push/{**catch-all}` path route — it cannot work.
   - **Operator constraints confirmed (2026-09-18) — read before choosing a topology:** a second hostname pointing at the **same IP** as `cloud.dotnetcloud.net` is **fine**, but **no new port may be opened — ntfy must be reachable on 443.** The last-resort port fallback below is therefore **NOT available**; the Host route is the only viable design here.
   - **What the Host route costs on this box (checklist):** a DNS A record `push.dotnetcloud.net` → the **same address** as `cloud.dotnetcloud.net`, plus **a certificate that covers the new name** — add it as a SAN on the existing Let's Encrypt cert (or use a wildcard if the operator has one). ⚠️ Make sure the **renewal challenge still works for the new name** (HTTP-01 needs port 80 reachable for it; otherwise switch that certificate to DNS-01) and re-issue **before** wiring the route, or the TLS handshake fails and nothing works. The existing router rule (443→5443 by IP:port) already covers the new name, so **no router change is expected — verify it**. The Host-based forward in step 3 does the rest.
   - A separately-ported ntfy (`listen-https` + key/cert + a new NAT rule) is the **last-resort fallback** only — and per the constraints above it is **ruled out on this deployment**. Patching ntfy to accept a path was considered and rejected: the check that refuses a path lives in the **ntfy Android app** (not the server, which is dual-licensed Apache-2.0/GPLv2 and would be patchable), so it would mean every DotNetCloud user installing a **fork of a third-party distributor app** and us maintaining that fork across ntfy releases. Do not go down that path.
2. **Install ntfy, bound to loopback** — the Debian/Ubuntu archive (`archive.ntfy.sh/apt`, keyring + `sources.list.d`, `sudo apt install ntfy`) or the static binary plus the shipped `ntfy.service` into `/etc/systemd/system/` (`daemon-reload`, `systemctl enable --now ntfy`). Config in `/etc/ntfy/server.yml`:
   - `listen-http: 127.0.0.1:2586` — **loopback only**; nothing else should ever reach it.
   - `base-url: "https://push.dotnetcloud.net"` (Host-based — the path form is rejected, see step 1).
   - ⚠️ **`behind-proxy: true`** — mandatory once it sits behind our route, otherwise every visitor is seen as `127.0.0.1` and all clients share one rate-limit bucket (one abuser starves everyone).
   - `cache-file: /var/cache/ntfy/cache.db` so a queued message survives an ntfy restart (default cache is in-memory, 12 h).
3. **Add the proxy route in Core.Server — Host-based, NOT a path route** — alongside the Collabora one, same `IHttpForwarder` machinery: the configured push host (`push.dotnetcloud.net`) is forwarded to loopback ntfy.
   - destination = `http://127.0.0.1:2586`; WebSocket upgrade allowed; **response buffering disabled**; `X-Forwarded-For` preserved; an activity timeout well above ntfy's keepalive (Collabora uses `TimeSpan.FromMinutes(15)`; ntfy's own keepalive default is 45 s and the Android app times out at 77 s).
   - keep response compression / any short-timeout middleware off these paths, and check that our security-header middleware does not interfere (Collabora's route already normalises frame headers in `OnStarting` — use the same hook if needed).
   - config keys belong in the deploy-safe config channel (see "Config" below), not in `appsettings.json` — `scripts/deploy.sh` republishes Core.Server into `/opt/dotnetcloud/server` too.
4. **Lock the topic down** — `auth-file: /var/lib/ntfy/user.db`, `auth-default-access: deny-all`, then a **dedicated** user for the server rather than anonymous publish:
   - `ntfy user add --role=user dotnetcloud`
   - `ntfy access dotnetcloud "up*" write-only` (all UP topics start with `up`)
   - `ntfy token add dotnetcloud` → that token goes into the server config (runtime only).
   - ⚠️ **Never use the admin user's token**: ntfy tokens grant full access to the *account* (everything except changing the password/deleting it), so a leaked config would be a full account compromise.
   - Fallback if the operator prefers no token: `ntfy access '*' 'up*' write-only` (anonymous publish to unguessable `up*` topics) — record the trade-off in your report.
5. ⚠️ **Do NOT set `firebase-key-file`** — that is ntfy's own FCM option and would put Google straight back into the path. Add a short comment in `server.yml` saying so, so a future admin does not "helpfully" enable it.
6. ⚠️ **Do NOT insert an external reverse proxy in front of Kestrel's 5443.** That port carries **gRPC** (module traffic + desktop clients) and an external proxy misconfiguration breaks currently-working clients. The supported way to share the port is the in-app route in step 3.
7. **Reaching our own endpoint (the old hairpin problem, now nearly free).** The endpoint the phone registers is the *public* URL — which this server also serves — so the DotNetCloud→ntfy POST must not hairpin through the router. Rewrite it (Part 2 / plan §8.1) to go **straight to loopback ntfy** (`https://push.dotnetcloud.net/up…` → `http://localhost:2586/up…`), which needs no DNS or `/etc/hosts` workaround. (On the last-resort standalone topology you *would* need NAT loopback or an `/etc/hosts` entry — hostname preserved so the certificate still validates, never a bare IP.)
8. **Verify:** `curl -s http://127.0.0.1:2586/v1/health` → `{"healthy":true}` **and** `curl -s https://push.dotnetcloud.net/v1/health` → `{"healthy":true` (proves the public Host route works end-to-end); `systemctl is-enabled ntfy` → `enabled` (survives reboot).

---

#### Part 2 — Server code (plan §8.1–§8.5)

> ⚠️ **Observation from the Android half (2026-09-18):** the on-device client's `POST /api/v1/notifications/devices/register` answered **404** against the deployed instance. That is expected if the route does not exist yet — but **if it does exist, treat it as a separate bug** and confirm it during your work. The rest of the registration chain was verified end-to-end on the device.

1. **Real transport** — `UnifiedPushHttpTransport : IUnifiedPushTransport` in `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/`: `POST {endpoint}` with `Content-Type: application/json`, optional `Authorization: Bearer <token>` for the protected topic; `404`/`410` = dead registration (remove it, log/audit, do not retry), `429`/5xx = transient (the provider already retries up to `UnifiedPushOptions.MaxSendAttempts` with backoff). Register it in place of `UnifiedPushLoggingTransport` (`ChatServiceRegistration.cs:59`) when `Chat:Push:UnifiedPush:Enabled`. Verify the deployed ntfy version's publish semantics (plain body vs JSON envelope) — UP only requires that a POST to the capability URL delivers the bytes.
   - **The transport must serialize the ID-only payload only** (plan §4.2: `v`, `type`, `channelId`, `messageId`, `eventId`). It must never send `PushNotification.Title`/`Body`; change its signature to take the payload/`Data` rather than the whole notification so text cannot leak by accident.
   - **Add an endpoint base-URL rewrite** (`EndpointRewriteFrom` → `EndpointRewriteTo`) applied before the POST. Required, not a nicety: the stored endpoint is the *public* URL that this server itself serves, so the hop must bypass it — `https://push.dotnetcloud.net` → `http://localhost:2586` (straight to loopback ntfy, no need to re-enter our own Kestrel). In Docker: `http://ntfy:80` on the bridge network. No `/etc/hosts` workaround is needed on the default topology.
2. **Persist device registrations** (§3.2 S2) — `NotificationRouter._deviceMap` and `FcmPushProvider._registrations` are in-memory, so **every module-host restart forgets every device** (self-healing registration only covers devices that come back online). Add an entity to the **Chat module's own** data layer (it owns this data — do not reach into `Core.Data`'s `UserDevice` from a module), keyed `(UserId, Provider, Token)` with `Endpoint`, `CreatedAt`, `LastSeenAt`; load on startup; prune on unregister/404/410. **Migration needed in both provider layouts**: `DotNetCloud.Modules.Chat.Data/Migrations` (PostgreSQL) **and** `DotNetCloud.Modules.Chat.Data.SqlServer/Migrations` — `dotnetcloud migrate` runs before the service starts, and a failure leaves the service stopped.
3. **ID-only payloads** (§3.2 S4) — the existing builders send human-readable text: `MentionNotificationService.cs:66` (`"{sender} mentioned you in #{channel}"`), `DmChannelCreatedEventHandler.cs:85-90` (`"{initiator} started a chat with you"` **plus** `channelName`/`initiatorName` in `Data`), `CallNotificationEventHandler.cs:59-62` (caller + channel names). Strip all of it down to the §4.2 IDs — including the name fields inside `Data`, which are just as visible to the push server as the title. Add a log/assert guard so a future builder cannot silently reintroduce text.
4. **New chat-message push** (§3.2 S5) — `NotificationCategory.ChatMessage` exists but **no builder produces it**: chat relies on SignalR, so a message sent while the app is closed produces nothing at all. Build it for channel members, honouring mute/DND/preferences and skipping the sender.
5. **Presence suppression** (§3.2 S3) — `NotificationRouter.CanSendPushAsync` suppresses push when `IsOnlineAsync(userId)` is true, and the phone's own hub connection counts — even when the process is frozen and can receive nothing. Either stop counting delivery-only connections (mark the connection via a hub header at connect time) or apply a grace window (a connection that has not sent a heartbeat within the client keepalive interval does not count as online). Android's client keepalive is 2 min; `SignalR:ClientTimeoutSeconds` is now 300.

---

#### Config — put it where a deploy cannot overwrite it

The consumer is the **Chat module host** (separate process at `/opt/dotnetcloud/modules/dotnetcloud.chat`).

- ⛔ **Do not use the module's `appsettings.json`.** `scripts/deploy.sh` republishes that directory and then rsyncs `--include="*.json"` over it, so anything you write there is silently overwritten on the next Chat deploy.
- ✅ Use **`/etc/dotnetcloud/config.json`** — every module host loads it explicitly via `DOTNETCLOUD_CONFIG_DIR` (`AddJsonFile(configDir/config.json)`), and neither `deploy.sh` nor `install.sh` touches `/etc/dotnetcloud`. Add a `Chat:Push:UnifiedPush` section there.
- Alternative: `Environment=` in the `dotnetcloud` unit / `/etc/dotnetcloud/env` (module hosts inherit the parent process environment — that is how `docker-compose.yml` documents it for Docker users), or add the keys to `ProcessSupervisor.ForwardEnvVar` to make it explicit.
- Keys: `Enabled`, `EndpointRewriteFrom`, `EndpointRewriteTo`, `AuthToken` (the dedicated ntfy token), plus the Part 1 proxy-route keys (`ProxyUpstreamUrl`, `PublicBaseUrl`). **Runtime config only — never commit the real hostname or token**; the repo is open source and committed configs must stay generic.
- **Report which channel you used and prove the Chat module host actually sees it** (module hosts read config at startup, so a restart is required; `UnifiedPushProvider` logs "UnifiedPush provider disabled" when it is off).

---

#### Other install paths you must not break (same code serves them)

- **Docker** (`docker-compose.yml`, `Dockerfile`, `deploy/docker/entrypoint.sh`): Core.Server + module hosts run in **one container** (`/app/modules/<id>` via `ProcessSupervisor`, inheriting the container env — the compose file says so explicitly), TLS is terminated by an upstream proxy/ingress, and 8080/5443 are published. There, `ntfy` is a **sibling compose service**: provision it declaratively with `NTFY_*` env (`base-url`, `cache-file`, `auth-file`, `auth-default-access`, `auth-tokens`) so no `server.yml` is needed, and let it sit behind a compose **profile** (mirroring the existing `--profile sqlserver` pattern). The internal hop is `http://ntfy:80` — the endpoint rewrite above.
- **Kubernetes / Helm** (`deploy/helm/dotnetcloud/`): one ingress per host; ntfy needs its own ingress host. No code difference.

---

#### Do NOT (this pass)

- Do not touch the Android client (Phases 1–2 are monolith work) — only keep the server-side contract they will code against.
- Do not *add* anything FCM-specific (FCM is being **deleted** — requirement §1.6, plan Phase 5). If you take the deletion with this pass, do it as a **separate commit** so the transport work stays reviewable.
- Do not add a foreground service, and do not enable ntfy's `firebase-key-file`.
- Do not insert an **external** reverse proxy in front of Kestrel's 5443 on this box (that port carries gRPC). The supported way to share the port is the **in-app** push proxy route — see Part 1.
- Do not commit or push until the verification below passes, and **do not create a PR** (PRs are the moderator's).

---

#### Acceptance / verification (must be done — not deferred)

1. **Build + tests clean:** `dotnet build`; `dotnet test tests/DotNetCloud.Modules.Chat.Tests/` and `tests/DotNetCloud.Core.Server.Tests/`; 0 warnings (warnings are errors in this repo). ⚠️ `DotNetCloud.Core.Server.Tests` has **one known, pre-existing, unrelated failure** (`ProgramRootCaTests.GetRootCaPath_AbsolutePath_ReturnsSiblingRootCa` — verified on a clean base) plus 1 skip; do not chase it, and do not report it as a regression.
2. **Deploy:** `sudo ./scripts/deploy.sh --force --verify`; then `/health/ready` **Healthy**, **14/14** modules, `.last-deploy-commit` = your HEAD.
3. **Persistence proof (S2):** register a device (app or `curl POST /api/v1/notifications/devices/register` with `{"deviceToken":"…","provider":"UnifiedPush","endpoint":"…"}`), restart the module host (`systemctl restart dotnetcloud`), then confirm the registration is still there and a send still attempts delivery. This is the whole point of the persistence work.
4. **Payload proof (S4):** capture the exact bytes the server POSTs (ntfy `log-level: trace` prints message contents — use it briefly, or log the outbound body) and show they contain **only** IDs: no names, no channel names, no message text.
5. **Transport proof:** `curl` the endpoint the way the transport does and confirm ntfy accepts it; `curl -s <ntfy-base>/v1/health` → healthy; with a distributor on the phone the message should arrive (§7.5 — needs the client half).
6. **Proxy-route proof (Part 1):** `curl -s https://cloud.dotnetcloud.net/push/v1/health` → `{"healthy":true}` **through the app's own port**; ntfy reachable on `127.0.0.1:2586` and **not** exposed anywhere else (no listening socket on a public interface); and a distributor subscription held open through the route survives several minutes of idling — no buffering cut-off, WebSocket upgrade intact, nothing dropped mid-view.
7. **Negative cases:** `Chat:Push:UnifiedPush:Enabled=false` → previous behaviour unchanged; a dead endpoint (404/410) prunes the registration; 429/5xx retries up to `MaxSendAttempts` and then gives up cleanly.
8. **Report back** with: branch + commit hash, migration name(s) applied, the config channel used and where it lives, the captured payload, the **public push URL shape you settled on** (path-based `/push`, or the `push.<domain>` hostname fallback — say whether the distributor app accepted a path), the ntfy token's ACL, and anything you could not verify. If background delivery cannot be proven without the client half, say exactly what remains.

> Once 1–7 pass, hand the **§7.5 live E2E** back to the client agent (monolith) — it needs the Android connector + a distributor app on the phone, and is the only remaining step to call the feature verified.

---

## Archived Handoff — Presence indicators 4-state: deployed to `cloud.kimball.home` + live E2E verified (2026-09-09) ✅

**Status:** ✅ **4-state presence LIVE-VERIFIED (both clients)** — server deployed + verified on `cloud.kimball.home` (server agent); operator browser run ✓ green / ✓ red / ✓ gray / ✓ yellow; Android on-device run ✓ green (live event + cold-start snapshot) / ✓ yellow / ✓ gray / ✓ red (DND both directions). Full-stack 4-state presence (Online / Away / Do-Not-Disturb / Offline) implemented on the monolith at the operator's request (server + Blazor + Android), unit-tested, pushed at `c17c7fa3` (+ `f1d45f07` cold-start fix).

> **Remaining (minor):** Admin `PresenceIdleTimeoutMinutes` runtime-pickup check (≤30 s) not exercised, and the Android/web channel-details member-row dots not explicitly spot-checked. Neither blocks the feature.
> **Unrelated on-device issue handled:** Android 15/16 `dataSync` FGS budget crash (`ForegroundServiceDidNotStopInTimeException`) crashed the app in a loop → **temporarily disabled foreground-service promotion** for the chat + media-upload services (policy kill-switch `AndroidForegroundServicePolicy`); proper FGS-type fix planned on a separate branch. Also fixed the `.gitignore` `modules/` rule that silently ignored NEW files under `src/Modules/**` (it caused the first cloud deploy to fail on the missing `PresenceStatusHelpers.cs`).

> This supersedes the prior relay-delay finding (archived below): the operator **accepted** the ~2–3 min relay retention window as the new **yellow (Away)** state, so the separate "presence vs delivery connection" refactor is **NOT** in this scope.

> **Status update (2026-09-09, server agent — `cloud.kimball.home`):** deployed and server-side verified; **operator browser E2E: green ✓ / red ✓ / gray ✓**, yellow (idle) pending. **Next agent: monolith** — finish the Android + idle (yellow) cross-device E2E (see "What to do (client agent — monolith)" and the checklist below). See the server-side verification record below.

### What changed (`c17c7fa3`, plan `docs/PRESENCE_DOTS_4STATE_PLAN.md`)
- **Server (Core/Core.Data/Core.Server):** `PresenceState` enum (names over the wire); `PresenceService` derives 4-state (Offline > DoNotDisturb > Away/Online) with DND persisted-pref caching at connect, `ReportActivityAsync` (real interaction only), and a `PresenceStateChanged` event; `PresenceActivityMonitor` (30 s sweep, reads admin `PresenceIdleTimeoutMinutes` at runtime, default 3, clamp 1–60); `PresenceChangePublisher` broadcasts **`UserPresence` {UserId, Status, Timestamp}** (replaces `UserOnline`/`UserOffline`); DND→presence bridge (`PresenceAwareNotificationPreferenceStore`); SignalR `ClientTimeoutSeconds` 30→300 (fixes the Android ~31 s reconnect churn); `PresenceIdleTimeoutMinutes` seeded in `DbInitializer`.
- **Blazor (in-process, deployed with server):** DM presence dots are now **Direct Message rows only** (Group rows no dot), 4 colors + Online/Idle/Do Not Disturb/Offline labels, DM thread-header dot+label, per-member 4-state member list, global web activity reporter (`presence-activity.js`), and a dedicated **Admin Settings → "Online indicators"** numeric field.
- **Android (client-only):** consumes the new `UserPresence` event + status snapshot; DM + channel-details member dots are 4-state and live; touch/key presence activity reporter.
- **Tests:** Core.Server **725 pass** (1 pre-existing `ProgramRootCaTests` fail + 1 skip — unrelated), Chat module 1406, Android 269, Core 501, Core.Data 177. Full `DotNetCloud.sln` + Android arm64 build clean (0 warnings).

### Do (server agent — `cloud.kimball.home`)
1. `git pull` (branch `fix/android-improvements` at `c17c7fa3`).
2. Deploy: `sudo ./scripts/deploy.sh --force --verify`.
3. Verify: `/health/ready` **Healthy**; **14/14** modules Running; `.last-deploy-commit` = `c17c7fa3`; `PresenceActivityMonitor`/`PresenceChangePublisher` present in the deployed `DotNetCloud.Core.Server.dll` (strings).
4. No DB migration is required (seed-only change — `DbInitializer` inserts the `PresenceIdleTimeoutMinutes` row idempotently on next startup).
5. Server-side automated checks you can run: `dotnet test tests/DotNetCloud.Core.Server.Tests/` (expect 725 pass + the known `ProgramRootCa` fail), `dotnet test tests/DotNetCloud.Modules.Chat.Tests/` (1406 pass).
6. Confirm from server logs that **no ~31 s reconnect churn** remains for Android connections (the heartbeat fix).

### Server-side verification record (2026-09-09, server agent — `cloud.kimball.home`) ✅

Deployed HEAD `72e401d1` (the handoff commit containing `c17c7fa3`) via `sudo ./scripts/deploy.sh --force --verify`:

- ✓ **Deploy blocker found & fixed:** the first deploy attempt **failed** — `DotNetCloud.Modules.Chat` (11 × `CS0103: The name 'PresenceStatusHelpers' does not exist`) because the helper was referenced by 6 Chat UI files + `PresenceStatusHelpersTests` but **never committed**. Server agent authored `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/PresenceStatusHelpers.cs` (`GetCssClass` / `GetLabel` / `ToStatusString`, namespace `DotNetCloud.Modules.Chat.UI`) reconstructed from the test contract + `PresenceState`; Chat module then built **0 warnings / 0 errors**. The server agent **committed** it so the branch builds for the monolith E2E.
- ✓ **Build/deploy:** all **15 targets succeeded** (Core.Server + 14 module hosts + CLI); elapsed 481 s; hash verification passed for `DotNetCloud.Core.Server.dll` + all 14 module host DLLs.
- ✓ **Health:** `/health/ready` → **Healthy** (`startup`, `database`, `linux-resources` Healthy; `modules-aggregate`: **14 module(s) — all healthy**).
- ✓ **Commit marker:** `/opt/dotnetcloud/server/.last-deploy-commit` = `72e401d157f47e801be7b9432c040d2103d38422`; installed version `0.6.04`.
- ✓ **Presence code deployed:** deployed `DotNetCloud.Core.Server.dll` contains `PresenceActivityMonitor`, `PresenceChangePublisher`, `PresenceStateChanged`, `UserPresence` (verified via `strings -e l`); deployed DLL md5 identical to build output (`f94667c668576f0c9adb4004a43f3868`).
- ✓ **Migrations:** none pending (`Core database is up to date`); all module schemas initialized. Seed-only `PresenceIdleTimeoutMinutes` handled idempotently at startup (no migration).
- ✓ **Tests:** `DotNetCloud.Modules.Chat.Tests` → **1406 passed / 0 failed** (incl. 19 `PresenceStatusHelpersTests`); `DotNetCloud.Core.Server.Tests` → **725 passed / 0 failed / 2 skipped** (Linux-only `OnNonLinux` tests; the previously-noted `ProgramRootCa` failure did **not** occur).
- ✓ **Heartbeat fix live:** deployed `appsettings.json` `SignalR.ClientTimeoutSeconds` = **300** (was 30). Fresh log (`dotnetcloud-20260909_005.log`) shows normal presence transitions (2 online / 1 offline, real users) with **no timeout/keepalive disconnect warnings and no ~31 s reconnect churn**.
- ✓ **Operator browser E2E (2026-09-09):** **green ✓, red ✓, gray ✓** confirmed in the browser. **Yellow (idle) not yet observed**, and the Android cross-client checks are unfinished → **handed back to monolith** (see "What to do (client agent — monolith)" below).

### What to do (client agent — monolith): finish the Android + idle (yellow) E2E

Server side is done and deployed on `cloud.kimball.home` (see verification record above). Browser half **green ✓ / red ✓ / gray ✓**; Android green + cold-start snapshot **✓** (incl. the `f1d45f07` cold-start race fix). Remaining work is the idle/yellow window + the remaining cross-device checks:

1. `git pull` — branch `fix/android-improvements`. Note: the server agent committed the previously-missing `PresenceStatusHelpers.cs` (branch did not build without it), so a fresh build is required.
2. Android `.apk` is already installed on the phone (Samsung `R5CWC356B2K`), signed in to `https://cloud.dotnetcloud.net/`.
3. Complete the remaining checklist items below: **idle → yellow** (temporarily set Admin Settings → "Online indicators" to **1 min**); cross-client DND **both** directions; member-row 4-state dots; admin idle-timeout applies ≤30 s.
4. Confirm the ~31 s SignalR reconnect churn is gone on Android (server `SignalR.ClientTimeoutSeconds` is now **300**).
5. Record the results, and hand any client-side fixes / the final verdict back to the server agent + operator.

### Live E2E checklist (operator, two users — web + Android on device) — plan §9
**Operator browser run (2026-09-09):** ✓ green · ✓ red · ✓ gray · ✓ yellow (idle).
**Android on-device run (monolith, 2026-09-09):** ✓ green (live event + cold-start snapshot) · ✓ yellow · ✓ gray · ✓ red (DND both directions).

- ✅ **Green while interacting** — **browser ✓**. Android on-device (Samsung `R5CWC356B2K`): Test Dude online → phone DM dot **green**; other peers gray; channel rows show **no dot** (DM-only layout correct).
- ✅ **Android live event path** — logcat: `SignalRChatClient: UserPresence userId=019f11a9… status=Online` received over the deployed server.
- ✅ **Android snapshot path** — verified after a cold start with Test Dude online and NO live event since launch → dot still green (snapshot `GetPresenceStatusAsync` returns statuses).
  - 🔧 **Bug found + fixed on-device (`f1d45f07`):** on cold start the DM-dot seed ran ~0.5 s **before** the CoreHub connection completed, so the snapshot returned an empty dict and every DM dot stayed gray until a live event happened to arrive. `SignalRChatClient.GetPresenceStatusAsync` now waits (~6 s, bounded) for the hub to connect before querying. No server change required — **no redeploy needed** for this fix.
- ✅ **Idle → yellow** — Android ✓: browser closed; phone logged `UserPresence status=Away` at **19:49:12** = last real interaction (≈19:46:12) + the **default 3 min** threshold (no disconnect yet). Browser ✓.
- ✅ **Tab close → gray** — Android ✓: `UserPresence status=Offline` at **19:49:24** (~12 s after Away). Browser ✓ (after the ~2–3 min relay/circuit retention).
- ✅ **DND → red, both directions** — Blazor top-bar toggle → **Android red ✓**; Android Settings DND → **Blazor red ✓** (operator-verified 2026-09-09).
- ⏳ **Admin change to `PresenceIdleTimeoutMinutes` applies within ≤30 s** (no restart) — **not exercised** (the 1-min change was never made; yellow/gray above ran on the default 3 min).
- ⏳ **Android + web channel-details member rows** show the 4-state dots and update live.
- ⚠️ **Unrelated on-device issue found during the run:** `ChatConnectionService` (`dataSync` FGS) hits `android.app.RemoteServiceException$ForegroundServiceDidNotStopInTimeException` → app crash/restart loop (pre-existing Android 15/16 dataSync fore service budget cap; **not** caused by the presence work). **Temporarily disabled foreground-service promotion** for the chat + media-upload services so Android stops killing the app; the proper FGS-type fix is planned on a separate branch (operator, 2026-09-09).

### Notes / non-goals
- The prior relay-retention finding is **archived below** — intentionally not fixed (yellow represents the retention window per operator decision 2026-09-09).
- No dot on Group/Public/Private **channel rows** in the sidebar (DM rows only); member rows inside a conversation keep dots.
- RED comes from the existing per-user chat **DND** toggle only (no new status picker).
- Android `.apk` for this branch: rebuilt arm64 (0 warnings). Install on the phone before the cross-device E2E.
- Do NOT merge to `main` or create a PR (merge is the operator's job).
- `GHSA-23fw-v26w-5fgq` `NuGetAuditSuppress` (@ `9cf5f579`) still needs a merge to `main` (operator PR).
- Keep `feature/module-widgets` (mint22) untouched (deferred entry above).

---

### Archived — DM presence-dots relay delay (root-caused 2026-09-09, NOT fixed by design)
**Context:** `f566369c` was verified live on cloud but offline after browser close stayed ~3–4.5 min late. Root cause: a logged-in web page's `NotificationBell` opens a per-circuit server→server `RealtimeNotificationClient` CoreHub connection (relay) that ALSO counts as a presence connection in `UserConnectionTracker`; browser close removes the circuit promptly but the relay keeps the user "online" until circuit disposal (~3-min retention).
**Operator decision 2026-09-09:** DO NOT refactor presence-vs-delivery connections now. The 4-state presence feature represents that ~2–3 min window as **Away (yellow)**, then gray when the connection finally drops — accepted behavior. The recommended (deferred) fix, if ever wanted: mark the relay as delivery-only in `UserConnectionTracker` (`AddConnection(userId, connId, isPresence)`), keep `GetConnections` returning all for delivery, add `X-DotNetCloud-Relay: 1` in `RealtimeNotificationClient` and read it in `CoreHub.OnConnectedAsync`. Full detail was archived from the prior Active Handoff (and in server-agent memory `/memories/repo/web-presence-relay-delay.md`).

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
