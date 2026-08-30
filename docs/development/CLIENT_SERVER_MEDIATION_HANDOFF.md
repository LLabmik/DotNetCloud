# Client/Server Mediation Handoff

Last updated: 2026-08-29 (Server Phase A handoff completed + archived; new Active Handoff: client verify AI REST API end-to-end + Android AI tab Phases B–F)

Purpose: shared handoff between client-side and server-side agents, mediated by user.

Archived context:

- Historical completed updates are in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Additional history remains available in git.

## Process Rules

**Agent autonomy (CRITICAL):**

- Both client and server agents work autonomously — they do NOT ask the moderator for context or permission.
- Agents pull the branch specified in the relay message, read the **Active Handoff** section, and execute the work described there independently.
- All actionable items, blockers, and technical details go directly in this document.
- **Current active branch:** `feature/android-ai-tab`

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

## Active Handoff

### Client: verify the AI REST API end-to-end + proceed with the Android AI tab (Phases B–F)

**Status:** client Phases B–F **DONE** (built + unit-tested + on-device verified); **BLOCKED on server-side REST/Bearer 500** — see "Server agent action required" below
**Branch:** `feature/android-ai-tab`
**From:** server agent (`cloud.kimball.home`), 2026-08-29
**Canonical plan:** `docs/ANDROID_AI_TAB_PLAN.md`
**Target:** Android client dev on `monolith`, server `cloud.kimball.home` (`https://cloud.dotnetcloud.net/`).

### Client work — DONE (monolith, 2026-08-29)

Phases B–F of `docs/ANDROID_AI_TAB_PLAN.md` are implemented on `feature/android-ai-tab` and verified as far as possible:

- **Build:** arm64 Debug APK builds clean. `DotNetCloud.Client.Android.Tests`: **240 total, 239 passed / 1 skipped (pre-existing) / 0 failed** — includes new AI, Markdown, and module-availability tests.
- **On-device** (Samsung R5CWC356B2K, logged into `https://cloud.dotnetcloud.net/`):
  - ✅ Music + AI modules detected via the full-id availability endpoint (`dotnetcloud.music` / `dotnetcloud.ai` → `installed:true`); **AI tab appears** (under the "More" overflow — MAUI 7-tab layout).
  - ✅ AI page renders (conversation list, model picker, new-chat, swipe rename/delete, streaming bubble, Ollama warning banner).
  - ✅ `GET /api/v1/ai/models` returns **401 without a token** (proxy + auth reachable).
  - ⚠️ **BLOCKER:** `GET /api/v1/ai/models` **and** `GET /api/v1/ai/conversations` return **500 (Internal Server Error)** with a valid Bearer token.
- **Blazor AI chat works** (moderator-confirmed) — so the AI module, its DB, and Ollama are functional and the module host is up. Blazor uses the module's **gRPC/in-process** path (`IAiApiClient`), **not** the REST proxy — so the **REST + Bearer path is specifically broken**.
- **Client-side mitigation already applied:** `AiViewModel.LoadAsync` no longer hard-fails when the provider 500s — it still loads the DB-backed conversation list and shows the Ollama banner instead of a hard error (unit-tested).

### 🔴 Server agent action required (cloud.kimball.home): AI REST Bearer path returns 500

**Symptom (verified live from the Android client, valid Bearer token, 2026-08-29):**
- `GET /api/v1/ai/models` → **500**
- `GET /api/v1/ai/conversations` → **500**
- (no token → **401**; so the proxy route + `[Authorize]` + controller route are all present → the deployed AI host IS running the Phase A code)

**What this rules out / narrows to:**
- The AI module itself is fine: **Blazor AI chat works** (moderator-confirmed). Blazor uses the module's **gRPC/in-process** path (`IAiApiClient`), **not** the REST proxy — so only the REST + Bearer path is broken.
- A **500 (not 404)** on both endpoints means the request reaches the controller and the action throws before/at `GetCallerContext()`.
- `AiChatController.GetCallerContext()` throws `UnauthorizedAccessException` when `User.Identity.IsAuthenticated != true`, and the host has **no exception handler**, so an unauthenticated/invalid identity surfaces as **500** (not 401). `src/Modules/AI/DotNetCloud.Modules.AI.Host/Program.cs` also still has `app.UseDeveloperExceptionPage();` — the real exception is printed in the AI module host logs.

**Diagnose (in order):**
1. **Read the AI module host logs** on cloud.kimball.home for the stack trace behind the 500 (DeveloperExceptionPage prints it). Fastest path to root cause.
2. Confirm the AI module host process was restarted with the Phase A binaries (route `api/v1/ai`, `AddTokenIntrospection()`, `DotNetCloud.Module` policy scheme). (Stale code would give 404, not 500 — so it is likely running the new code.)
3. **Probe auth:** `GET /api/v1/ai/models` with a deliberately **invalid** Bearer token:
   - invalid → **401**: auth is enforced; the 500 is a valid-token-specific failure (e.g., `GetCallerContext()` claim extraction, or a downstream service call).
   - invalid → **500**: the authentication handler itself is throwing (e.g., introspection gRPC call to Core.Server failing, or `DotNetCloud.Module` policy scheme / `PermissionAuthorizationHandler` throwing).
4. Compare with the **Music module host**, which uses the identical auth pattern (`AddTokenIntrospection()` + `DotNetCloud.Module` policy scheme + `AuthorizationPolicies.Configure` + `PermissionAuthorizationHandler`) and **works via Bearer** from the same Android client — the auth pattern is proven; any difference is AI-host-specific.

**Fix expectations (once resolved):**
- `GET /api/v1/ai/models` → **200** with a valid Bearer token.
- `GET /api/v1/ai/health/ollama` → **200**, or **503** while Ollama isn't reachable from cloud (expected).
- Remove/guard `app.UseDeveloperExceptionPage()` for production (currently leaks exception details).
- After the fix, update the Active Handoff; the Android on-device round-trip (create → stream → list → rename → delete) is then re-run to complete the AI tab E2E.

### API contract (unchanged)

API base `/api/v1/ai/` (proxied by Core.Server, excluded from the response envelope; auth = Bearer introspection or cookie).

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
