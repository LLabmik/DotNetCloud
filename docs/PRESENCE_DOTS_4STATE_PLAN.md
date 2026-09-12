# Presence Indicators — 4-State (Online / Idle / Do-Not-Disturb / Offline)

**Status:** implemented (server + Blazor + Android code) — live E2E (§9) pending deploy to `cloud.kimball.home`
**Date:** 2026-09-09
**Branch:** `fix/android-improvements`
**Scope:** Server (Core.Server) + Blazor Chat UI + Android Chat UI + Admin settings. Done on monolith at operator's request (full-stack feature, not a mint22 handoff). Companion to `docs/ANDROID_CHAT_PRESENCE_DOTS_PLAN.md`.

> ⚠️ **Status note:** plan reviewed by operator on 2026-09-09 — the product decisions below (including the four follow-ups) are **locked**. Server + Blazor + Android code implemented 2026-09-09 and unit-tested (see §8); **not yet live-verified**. Per repo rules, nothing is pushed until the live E2E verification in §9 passes on the deployed server. Implementation summary:
> - Server: `PresenceState` enum (Core), 4-state `PresenceService` + DND cache/activity + `PresenceStateChanged` event, `PresenceActivityMonitor` (30 s sweep + runtime admin threshold), `PresenceChangePublisher` ("UserPresence" broadcasts), DND bridge (`PresenceAwareNotificationPreferenceStore`), SignalR `ClientTimeoutSeconds` 30→300, `PresenceIdleTimeoutMinutes` setting + seed.
> - Blazor: DM-only presence dots (4 colors + label), DM thread-header dot/label, per-member 4-state member list, global `presence-activity.js` web activity reporter.
> - Android: `UserPresence` + status snapshot + `ReportActivityAsync` (Ping), DM + member-row 4-state dots, touch/key presence activity reporter.
> - Tests added/updated: `PresenceServiceTests`, `PresenceActivityMonitorTests`, `PresenceIdleTimeoutTests`, `PresenceAwareNotificationPreferenceStoreTests`, `CoreHubTests`, `PresenceCircuitHandlerTests`, `PresenceStatusHelpersTests`, `SignalROptionsTests` — Core.Server 725 pass (1 pre-existing `ProgramRootCa` fail), Chat 1406, Android 269, Core 501, Core.Data 177. Full `DotNetCloud.sln` + Android arm64 build clean (0 warnings).

---

## 0. Confirmed product decisions (from operator Q&A)

1. **RED (Do Not Disturb) source of truth = the existing DND toggle** (the per-user _chat-notification_ DND that both clients already have: Blazor top-bar `UserDndToggle` and Android Settings → DND → `PUT /api/v1/notifications/preferences`). When a user turns DND **on**, they appear **red** to peers while online. No new "status picker" UI.
2. **Activity = real interaction.** Only genuine use of a DotNetCloud client (tap / click / key / scroll / nav / send) resets the idle timer. A phone left on the chat list or a browser tab left open _does_ go yellow after the threshold.
3. **Scope of dots = all existing presence indicators**, not just the DM list: Blazor DM sidebar dots, DM thread-header dot + status text, member list; Android DM-list dots and channel-details member rows.
4. **Include the deferred SignalR heartbeat fix** (server `ClientTimeoutSeconds: 30` vs Android `WithKeepAliveInterval(2 min)` reconnect churn) in the same change so idle detection doesn't fight the transport.
5. **Follow-up decisions (operator, 2026-09-09):**
   - **Dedicated admin field** for the idle threshold (§3.5) — not just a raw settings row.
   - **No presence dots on Group rows** — align Blazor to Android; dots appear on **Direct Message rows only**.
   - **True yellow `#EAB308`** for the `Away` state.
   - **Android channel-details member rows** are made **live + 4-state** (in scope).

---

## 1. Goal & Acceptance Criteria

Upgrade the presence "online dot" from **2 states (green/gray)** to **4 states** in both the Blazor web chat and the Android chat, backed by a single server-authoritative presence state:

| State          | Meaning                                                            | Color (canonical palette) |
| -------------- | ------------------------------------------------------------------ | ------------------------- |
| `Online`       | Connected **and** active (real interaction within the idle window) | Green `#22C55E`           |
| `Away`         | Connected, but **no real interaction** for ≥ N minutes             | Yellow `#EAB308`          |
| `DoNotDisturb` | Connected **and** user enabled DND (chat notifications muted)      | Red `#EF4444`             |
| `Offline`      | No active connection                                               | Gray `#64748B`            |

The idle threshold **N defaults to 3 minutes** and is an **admin-adjustable system setting** (`dotnetcloud.core` / `PresenceIdleTimeoutMinutes`) editable on the admin settings page.

**Acceptance criteria:**

- ☐ Server exposes a per-user 4-state presence value (not a boolean) to Blazor (in-process) and to Android (CoreHub snapshot + realtime events).
- ☐ Green dot while the user interacts; dot turns **yellow** automatically ~N minutes after their last real interaction, without the user going offline.
- ☐ Any new real interaction turns the dot back **green** (server-authoritative transition, pushed to peers).
- ☐ Enabling DND (either client) turns the user **red** for peers while they are online; disabling DND returns them to green/yellow. Red does **not** show while offline (gray wins).
- ☐ Disconnect / browser-close eventually yields **gray** (offline) — via the existing disconnect flow (prompt offline) plus the ~2–3 min circuit/relay retention window now being represented accurately by yellow→gray.
- ☐ Presence dots appear on **Direct Message rows only** — Group, Public, and Private channel rows show **no dot**, consistently on both clients.
- ☐ Admin settings page exposes a **dedicated "Online indicators" numeric field** for the idle threshold (default 3, clamped 1–60).
- ☐ All dot locations in scope render the 4 colors + an accurate text label/tooltip (`Online` / `Idle` / `Do Not Disturb` / `Offline`).
- ☐ Idle threshold read from the admin system setting at runtime (default 3, no restart required after admin change; no restart needed to _read_ the default when absent).
- ☐ SignalR heartbeat mismatch fixed: idle CoreHub connections are no longer dropped every ~30 s (Android keepalive now compatible with server timeouts).
- ☐ No behavior regression for push suppression: `NotificationRouter` still treats `Away`/`DoNotDisturb`/`Online` as "online" (suppress pushes while any connection exists) exactly as today.
- ☐ Unit tests pass; Android arm64 app builds clean; Blazor + server build clean; live cross-device E2E verified before commit (repo critical rules).

---

## 2. Current state (what exists today)

### 2.1 Server presence (Core.Server, in-process)

- `RealTime/UserConnectionTracker.cs` — userId → active connection IDs (CoreHub connections **and** Blazor circuits). `IsOnline(userId)` = has ≥1 connection.
- `RealTime/PresenceService.cs` (implements Core capability `IPresenceTracker`):
  - `_lastSeen` (`ConcurrentDictionary<Guid, DateTime>`) — updated only on connect/disconnect and the **currently unused** `UpdateLastSeenAsync` (`CoreHub.PingAsync` — nothing calls it).
  - `_presence` (`ConcurrentDictionary<Guid, PresenceDto>`) — per-user `{ Status: "Online"/"Away"/"DoNotDisturb"/"Offline", StatusMessage, LastSeenAt }`. `AllowedStatuses` already contains all four.
  - `UserConnectedAsync/UserDisconnectedAsync` (fired only on **first/last** connection transitions) force `Status = "Online"/"Offline"`.
  - `SetPresenceAsync(userId, status, msg)` exists but is **not called by any client**.
  - Capability methods: `IsOnlineAsync` (bool), `GetOnlineStatusAsync` → `IReadOnlyDictionary<Guid,bool>`, `GetLastSeenAsync`, `GetOnlineUsersAsync`, `GetActiveConnectionCountAsync`.
- `RealTime/CoreHub.cs` (`/hubs/core`):
  - On first/last connection: `_presenceService.UserConnectedAsync/UserDisconnectedAsync` + broadcast `"UserOnline"`/`"UserOffline"` `{UserId, Timestamp}` to `Clients.Others` + in-process `_chatMessageNotifier.NotifyUserPresenceChanged(new UserPresenceChangedNotification(userId, IsOnline: bool))`.
  - `GetPresenceStatusAsync(IReadOnlyList<Guid>)` → delegates to `GetOnlineStatusAsync` (bool dict). Used by Android to seed dots.
  - `PingAsync()` → `UpdateLastSeenAsync(userId)` (unused today).
- `RealTime/PresenceCircuitHandler.cs` — Blazor circuit lifecycle (connect/reconnect/down/closed) mirrors the same first/last logic into `UserConnectionTracker` + `PresenceService` and broadcasts `"UserOnline"`/`"UserOffline"` to `_hubContext<CoreHub>.Clients.All` (so **web** users reach Android).
- SignalR config: `SignalRConfiguration` defaults `KeepAliveIntervalSeconds = 15`, `ClientTimeoutSeconds = 30` (Core.Server `appsettings.json`). Android client uses `WithKeepAliveInterval(TimeSpan.FromMinutes(2))` → server drops idle clients every ~30 s → reconnect churn (~31 s cadence, documented in repo memory).

### 2.2 Chat module (Models/DTOs — mostly dormant scaffolding)

- `DotNetCloud.Modules.Chat/Models/PresenceStatus.cs` — unused enum `Online / Away / DoNotDisturb / Offline`.
- Chat member DTOs carry **no** presence field. Android's private JSON `ChannelMemberDto.IsOnline` never matches server JSON (server never emits it) → Android channel-details member dots are effectively always gray today.

### 2.3 Blazor UI (in-process; Core.Server hosts Chat module UI services)

- `ChatPageLayout.razor.cs` — injects `IPresenceTracker`; subscribes `IChatMessageNotifier.UserPresenceChanged`:
  - Seeds member status (`member.Status = "Online"` when `GetOnlineStatusAsync(...)[id] == true`) and DM peer status (`dm.PresenceStatus = "Online"`), both **bool** based (~lines 1411–1420, 2206–2217).
  - `OnUserPresenceChanged` sets `member.Status` / `channel.PresenceStatus = notification.IsOnline ? "Online" : "Offline"`.
- `ChannelList.razor` + `.razor.cs` + `.razor.css` — DM/Group rows render `<span class="presence-dot @GetPresenceClass(channel)" title="@channel.PresenceStatus">`. CSS already defines `.presence-online #16a34a`, `.presence-away #d97706`, `.presence-offline #6b7280` (no red). `GetPresenceClass` maps `Online→presence-online`, `Away→presence-away`, else `presence-offline`.
- `ViewModels.cs` — `ChannelViewModel.PresenceStatus` (string, default `"Offline"`), `MemberViewModel.Status` (string, default `"Offline"`).
- `DirectMessageView.razor` — DM thread header `status-dot` + `dm-user-status` text bound to `OtherUser.Status` (currently binary `online/offline` class).
- `MemberListPanel.razor` — groups members `Online/Away` → green group vs `Offline/DoNotDisturb` → gray group; per-row dot color is fixed by group (all "online" members green, all "offline" gray) — imprecise for 4-state.

### 2.4 Android UI (remote client via CoreHub)

- `Chat/SignalRChatClient.cs` — subscribes `"UserOnline"`/`"UserOffline"` → raises `OnUserPresenceChanged(UserId, IsOnline: bool)`; `GetPresenceStatusAsync(peerIds)` → `Dictionary<Guid,bool>`.
- `ViewModels/ChannelListViewModel.cs` — seeds DM dots after channel load via `GetPresenceStatusAsync` (`RefreshPresenceAsync`), updates `ChannelItemViewModel.IsOnline` from the presence event; re-seeds on `Reconnected`.
- `ChannelItemViewModel` — `IsDirectMessage`, `OtherUserId`, `[ObservableProperty] bool _isOnline`.
- `Views/ChannelListPage.xaml` — 9 px `Ellipse` for DM rows: `Fill="{Binding IsOnline, Converter={StaticResource OnlineStatusToColor}}"` (green `#22C55E` / gray `#475569`).
- `Views/ChannelDetailsPage.xaml` — member rows bind the same converter to `IsOnline` (stale/no-op source today).
- `Converters/AppConverters.cs` — `OnlineStatusToColorConverter` = bool→green/gray.
- SignalR config: `SignalRChatClient.ConnectAsync` `.WithKeepAliveInterval(TimeSpan.FromMinutes(2))`.

### 2.5 DND (the RED source) — separate system, both clients, DB-persisted

- Server funnel: `NotificationsController` (`PUT /api/v1/notifications/preferences`) + in-process Blazor `UserDndToggle` both end at `INotificationPreferenceStore.Update(userId, prefs)` → DB-backed `DbNotificationPreferenceStore` (Chat schema, shared across processes/machines). `UserNotificationPreferences.DoNotDisturb` (default false).
- Blazor toggle: `UI.Web/Components/Shared/UserDndToggle.razor` (calls the store in-process). Android: `SettingsViewModel` + DM-notification DND action → `SetDoNotDisturbAsync` → the REST controller.
- DND currently affects **push/notification delivery only** (`NotificationRouter.CanSendPushAsync`), never presence.

### 2.6 Admin settings

- `IAdminSettingsService` / `AdminSettingsService` (EF `SystemSettings` module/key/value), routes in `AdminController` (`api/v1/core/admin/settings`), web client methods on `DotNetCloudApiClient` (`ListSettingsAsync/GetSettingAsync/UpsertSettingAsync/DeleteSettingAsync`).
- Admin page: `src/UI/DotNetCloud.UI.Web.Client/Pages/Admin/Settings.razor` (generic module/key/value table with Edit dialog).
- Core settings constants: `Constants/SystemSettingKeys.cs` (`CoreModule = "dotnetcloud.core"` etc.). Core defaults seeded in `Core.Data/Initialization/DbInitializer.cs`.

---

## 3. Design

### 3.1 Server-authoritative 4-state model

Keep `IsOnline` (connection-based bool) untouched for `NotificationRouter`, online-user counts, etc. Add a **display state** derived per user:

```
state(user):
  if not online(user)                    -> Offline   (gray)
  else if DND(user)                      -> DoNotDisturb (red)
  else if (UtcNow - lastActivity(user)) <= idle  -> Online   (green)
  else                                    -> Away     (yellow)
```

- `online(user)` = `UserConnectionTracker.IsOnline` (unchanged).
- `lastActivity(user)` = existing `PresenceService._lastSeen`, now maintained by **real activity reports** (see §3.3) in addition to connect/disconnect.
- `DND(user)` = cached view of the persisted `UserNotificationPreferences.DoNotDisturb`, refreshed on connect and on DND change (see §3.4).
- `idle` = admin setting `PresenceIdleTimeoutMinutes` (default 3), read at runtime (see §3.5).

Priorities: **Offline > DoNotDisturb > Away/Online** — i.e. a DND user who disconnects shows gray; an inactive-but-DND user shows red (DND wins over Away while connected).

### 3.2 State plumbing (replace boolean presence end-to-end)

Adopt one canonical representation across the wire so both UIs and the in-process UI can share it. Recommend promoting the Chat module's dormant `Models/PresenceStatus.cs` enum into `DotNetCloud.Core` (e.g. `DTOs/PresenceState.cs`) and serializing as its name string (`"Online"`, `"Away"`, `"DoNotDisturb"`, `"Offline"`) — matching the strings already used by `PresenceDto.Status`, `MemberViewModel.Status`, `ChannelViewModel.PresenceStatus`, and `MemberListPanel`. Concretely:

- ☐ `IPresenceTracker.GetOnlineStatusAsync` → return `IReadOnlyDictionary<Guid, PresenceState>` (or add `GetPresenceStatesAsync`; keep `IsOnlineAsync` bool).
- ☐ `CoreHub.GetPresenceStatusAsync` → return statuses (Android maps `Dictionary<Guid,string>`).
- ☐ Replace the `UserOnline`/`UserOffline` broadcasts + `UserPresenceChangedNotification(IsOnline: bool)` with a **single status-carrying event** (recommended event name `"UserPresence"`, payload `{ UserId, Status, Timestamp }`; in-process `UserPresenceChangedNotification` gains `PresenceState Status`). All in-repo consumers are updated together; SyncTray/other clients do not subscribe to these presence events (verify) so no external break.
- ☐ Chat `Models/PresenceStatus.cs` either removed or re-pointed at the Core enum.

### 3.3 Activity signals (green ↔ yellow)

Only genuine client interaction reports activity. Server side:

- ☐ `PresenceService.ReportActivityAsync(userId)` — updates `_lastSeen`/`LastSeenAt` (this is today's `UpdateLastSeenAsync`; wire `CoreHub.PingAsync` to it and add an in-process equivalent for web).
- ☐ **Idle sweep monitor** (`RealTime/PresenceActivityMonitor`, singleton hosted/`BackgroundService`, `PeriodicTimer` ~ every 30 s): for each online user, compute desired state from §3.1; when it differs from the current tracked status, apply + raise the state-changed event (§3.2 broadcast). This makes green→yellow automatic ~N minutes after the last interaction, and yellow→green immediate on the next activity report (no dependency on any per-connection heartbeat).

Client activity reporters (throttled, ~1 report/20–30 s + immediate on key actions):

- ☐ **Android:** `IActivityReporter` (or extend `ICoreHubClient`/`SignalRChatClient`) that invokes `CoreHub.PingAsync` while the app is foregrounded and the user interacts. Sources: MAUI app lifecycle (`App.OnStart/OnResume` start, `OnSleep` stop), Shell page-activation/navigation, and a low-frequency timer while a page is visible. Stop reporting when backgrounded so an idle/backgrounded phone goes yellow (then gray when the socket drops).
- ☐ **Blazor (web):** in-process reporter — an `IPresenceActivityReporter` implemented in Core.Server (wraps `PresenceService.ReportActivityAsync`) plus a tiny global JS interaction listener (`activity.js`, attached in `MainLayout`/`App.razor`) that throttles pointer/key/scroll/touch and calls back into Blazor (JS-invokable) to report the signed-in user's activity. Because web presence lives on Blazor circuits (not CoreHub), this stays in-process — no new hub needed for web.

> This also cleanly represents the documented post-browser-close window: when the tab closes, the circuit/relay connection lingers (~2–3 min) but activity stops → user shows **yellow** during that retention window, then **gray** when the connection is finally dropped. (Explicitly a _non-goal_ to separate presence-bearing vs delivery-only relay connections — the operator accepted yellow for that window.)

### 3.4 RED = existing DND (single switch)

- ☐ Server connects DND → presence at the single convergence point: `INotificationPreferenceStore.Update`. Both entry paths (Blazor in-process `UserDndToggle` and `NotificationsController` PUT for Android/DM-action) already funnel here.
  - Recommended: raise a change notification from the DB-backed `DbNotificationPreferenceStore` (or wrap the registration with an eventing decorator in Core.Server) when `DoNotDisturb` flips → subscriber calls `PresenceService.OnDndChangedAsync(userId, enabled)` which caches the flag, recomputes state, and broadcasts the transition immediately (no waiting for the 30 s sweep).
  - Also cache DND **at connect**: `UserConnectedAsync` reads the persisted pref once (via an `IDbContextFactory<ChatDbContext>`-scoped read — avoiding the singleton-DB trap) so a DND user who connects appears red immediately.
- ☐ Disabling DND returns the user to green/yellow per §3.1.

### 3.5 Admin setting: idle threshold

- ☐ `SystemSettingKeys.PresenceIdleTimeoutMinutes` (CoreModule, type int, default `"3"`, sane clamp 1–60). Seed default in `DbInitializer` (alongside DemoMode/ClosedSystem) so the row is visible/editable in the Admin Settings page.
- ☐ Runtime read: `PresenceActivityMonitor` resolves the value each sweep cycle via `IAdminSettingsService` (scoped factory read) with fallback to the constant default; clamp invalid values. Admin changes apply on the next sweep (≤30 s) with no restart.
- ☐ **Admin page UX (DECIDED — dedicated field):** add a dedicated **"Online indicators"** section to the admin settings page (`src/UI/DotNetCloud.UI.Web.Client/Pages/Admin/Settings.razor`) with a labeled numeric input for the idle threshold (minutes), loaded via `DotNetCloudApiClient.GetSettingAsync(dotnetcloud.core, PresenceIdleTimeoutMinutes)` and saved via `UpsertSettingAsync`. Validate/clamp 1–60 client-side and re-clamp server-side; show the current effective value and a description. The underlying system-setting row (seeded by `DbInitializer`) remains the storage, so the generic table still shows it too.

### 3.6 Canonical palette + labels

| State          | Dot color | Blazor class                                 | Android brush | Label (title/status text) |
| -------------- | --------- | -------------------------------------------- | ------------- | ------------------------- |
| `Online`       | `#22C55E` | `.presence-online`                           | green         | Online                    |
| `Away`         | `#EAB308` | `.presence-away` (recolor from `#d97706`)    | yellow        | Idle                      |
| `DoNotDisturb` | `#EF4444` | `.presence-dnd` (new)                        | red           | Do Not Disturb            |
| `Offline`      | `#64748B` | `.presence-offline` (recolor from `#6b7280`) | gray          | Offline                   |

Existing `status-dot`/`member-status-dot` CSS (Blazor) and `OnlineStatusToColorConverter` (Android) are updated to the 4-state mapping.

> **DECIDED:** `Away` uses **true yellow `#EAB308`** (Blazor's existing `.presence-away` is recolored from amber `#d97706`). Green/yellow/red/gray palette is applied identically on both clients.

---

## 4. Server changes (Core.Server)

| #   | File / area                                                        | Change                                                                                                                                                                                                                       |
| --- | ------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | `Core/DotNetCloud.Core/DTOs/PresenceState.cs` (new)                | Canonical enum `Online, Away, DoNotDisturb, Offline` (moved from Chat `Models/PresenceStatus.cs`) with JSON-as-name.                                                                                                         |
| 2   | `Core/Capabilities/IPresenceTracker.cs`                            | `GetOnlineStatusAsync` → statuses; add `ReportActivityAsync(Guid)`; keep `IsOnlineAsync` bool.                                                                                                                               |
| 3   | `RealTime/PresenceService.cs`                                      | Derive display state (§3.1); maintain `_lastSeen` via `ReportActivityAsync`; cache DND; `UserConnectedAsync` seeds DND/Online correctly; expose `RecomputeAsync(userId)` + internal state-changed event.                     |
| 4   | `RealTime/PresenceActivityMonitor.cs` (new)                        | Periodic sweep; DND/idle transitions; publishes state changes.                                                                                                                                                               |
| 5   | `RealTime/CoreHub.cs`                                              | `GetPresenceStatusAsync` returns statuses; `PingAsync` = activity; unify broadcasts to `"UserPresence" {UserId, Status}`; connect/disconnect paths route through the same recompute/broadcast so DND/idle seeds are correct. |
| 6   | `RealTime/PresenceCircuitHandler.cs`                               | Mirror state/status in connect/disconnect; include status in broadcasts.                                                                                                                                                     |
| 7   | `RealTime/RealtimeBroadcasterService.cs` (or new publisher)        | Fan out state changes to `IHubContext<CoreHub>` clients + in-process `IChatMessageNotifier`.                                                                                                                                 |
| 8   | Chat notifier record                                               | `UserPresenceChangedNotification` → carry `PresenceState Status` (bool removed or kept as convenience).                                                                                                                      |
| 9   | DND → presence bridge                                              | `DbNotificationPreferenceStore` change event (or eventing decorator in Core.Server `Program.cs` DI) → `PresenceService.OnDndChangedAsync`.                                                                                   |
| 10  | `SignalRServiceExtensions.cs` / DI                                 | Register `PresenceActivityMonitor`, bridge, reporter; resolve DND cache via `IDbContextFactory<ChatDbContext>` (avoid scoped-from-singleton).                                                                                |
| 11  | `SignalRConfiguration` default (`appsettings.json` + config class) | Raise `ClientTimeoutSeconds` 30 → ~300 s (server-side half of the heartbeat fix).                                                                                                                                            |
| 12  | `Constants/SystemSettingKeys.cs`, `DbInitializer.cs`               | New `PresenceIdleTimeoutMinutes` key + seeded default `"3"`.                                                                                                                                                                 |
| 13  | Chat `Models/PresenceStatus.cs`                                    | Remove or alias to Core enum.                                                                                                                                                                                                |

---

## 5. Blazor UI changes

| #   | File                                                       | Change                                                                                                                                                                                                 |
| --- | ---------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 1   | `UI/ChatPageLayout.razor.cs`                               | Seed + handle 4-state (`Online/Away/DoNotDisturb/Offline`) for `member.Status` and `channel.PresenceStatus`; report web activity (JS listener → in-process reporter); map presence events to statuses. |
| 2   | `UI/ChannelList.razor` + `.razor.cs`                       | Render the presence dot on **Direct Message rows only** (drop Group rows — aligns with Android); `GetPresenceClass` handles `DoNotDisturb→presence-dnd`.                                               |
| 3   | `UI/ChannelList.razor.css`                                 | Add `.presence-dnd` (red); recolor away→`#EAB308` and offline→`#64748B` (canonical palette).                                                                                                           |
| 4   | `UI/DirectMessageView.razor` + `.css` + `.razor.cs`        | 4-state `status-dot` class + status text label for the DM peer header.                                                                                                                                 |
| 5   | `UI/MemberListPanel.razor` + `.css`                        | Per-member 4-state dots (not group-fixed); regroup by status (or keep 2 groups but color per-member dots correctly and label DND/Idle).                                                                |
| 6   | `UI/ViewModels.cs`                                         | (Strings already fit) — ensure `MemberViewModel`/`ChannelViewModel` label formatting for `DoNotDisturb`/`Away` ("Idle").                                                                               |
| 7   | Web activity                                               | New `wwwroot/js/activity.js` + JS-interop activity reporter (global, throttled).                                                                                                                       |
| 8   | `UI.Web.Client/Pages/Admin/Settings.razor` (+ `.razor.cs`) | **Dedicated "Online indicators" field** (numeric, 1–60) backed by `GetSettingAsync`/`UpsertSettingAsync` on `dotnetcloud.core/PresenceIdleTimeoutMinutes`.                                             |

---

## 6. Android changes

| #   | File                                                                      | Change                                                                                                                                                                                                                                      |
| --- | ------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | `Chat/SignalRChatClient.cs` / `Services/ICoreHubClient.cs`                | Handle `"UserPresence" {userId,status}` (replaces `UserOnline/UserOffline`); `GetPresenceStatusAsync` → statuses; expose `ReportActivityAsync()` (invoke `PingAsync`).                                                                      |
| 2   | `Client.Core/IChatSignalRClient.cs`                                       | `UserPresenceChangedEventArgs` → carry status string/enum (Android-only usage; keep SyncTray unaffected).                                                                                                                                   |
| 3   | `ViewModels/ChannelListViewModel.cs` + `ChannelItemViewModel`             | Replace `bool IsOnline` with `PresenceState Status`; seed from snapshot; update from event; label text.                                                                                                                                     |
| 4   | `Views/ChannelListPage.xaml`                                              | Dot binds status → 4-state converter (DM rows).                                                                                                                                                                                             |
| 5   | `ViewModels/ChannelDetailsViewModel.cs` + `Views/ChannelDetailsPage.xaml` | Make member dots **live** + 4-state by resolving each member's presence via the CoreHub snapshot/events (same pattern as `ChannelListViewModel`); drop reliance on the dead `IsOnline` REST field.                                          |
| 6   | `Converters/AppConverters.cs` (`OnlineStatusToColorConverter`)            | bool → status: green/yellow/red/gray + optional label converter.                                                                                                                                                                            |
| 7   | Activity reporter                                                         | New `IActivityReporter`/heartbeat service driven by MAUI lifecycle + interaction; calls `SignalRChatClient.ReportActivityAsync` (throttled). Register in `MauiProgram.cs`.                                                                  |
| 8   | `SignalRChatClient.cs` keepalive                                          | Either lower `.WithKeepAliveInterval` (~15–20 s) **or** rely on server `ClientTimeoutSeconds` raise; pick per §7 so idle sockets aren't dropped (recommend server-side raise + leave Android keepalive, since activity pings are separate). |
| 9   | Android.Tests csproj                                                      | Add any new Compile-includes for files referenced by compiled ViewModels (repo gotcha).                                                                                                                                                     |

---

## 7. SignalR heartbeat fix (included, per decision 4)

- ☐ **Recommended:** raise server `SignalR:ClientTimeoutSeconds` 30 → ~300 s (config default + `SignalRConfiguration`). Battery-friendly clients that only transport-keepalive every 2 min are then no longer dropped every ~30 s. Tradeoff (stale sockets linger up to the longer timeout on abrupt death) is acceptable because presence is now last-activity-driven and the 30 s sweep reconciles state.
- ☐ Android keeps `.WithKeepAliveInterval(2 min)` (or optionally ~60 s). Activity pings (`PingAsync`, throttled) are **user-activity** reports, independent of transport keepalive.
- ☐ Verify via logcat that the ~31 s "hub reconnected → re-querying presence" churn is gone (repo memory: `fix/android-improvements` regression note).

---

## 8. Tests

### Server (`tests/DotNetCloud.Core.Server.Tests`)

- ☐ `PresenceServiceTests` (new): state derivation priority (offline>DND>away>online); connect with DND → DoNotDisturb; activity report → Online; recompute after idle → Away; disconnect → Offline (DND not shown offline).
- ☐ `PresenceActivityMonitorTests` (new): sweep transitions active→away at threshold, away→active on activity; honors admin setting value changes + default; clamping invalid values.
- ☐ `CoreHubTests` (extend): `GetPresenceStatusAsync` returns statuses; `PingAsync` updates lastActivity; `"UserPresence"` broadcast payload.
- ☐ `PresenceCircuitHandlerTests` (extend): connect/disconnect produce status broadcasts; DND red seeding.
- ☐ DND bridge test: store `Update(DoNotDisturb=true)` → presence state DoNotDisturb + broadcast; false → back to Online/Away.
- ☐ `NotificationRouterTests` (regression): `IsOnline` still suppresses push for Online/Away/DND users (bool semantics unchanged).

### Blazor (Chat module UI tests, `tests/DotNetCloud.Modules.Chat.Tests`)

- ☐ `GetPresenceClass` mapping incl. `DoNotDisturb → presence-dnd` (new test file).
- ☐ `ChannelList` renders a presence dot for **DM rows only** — no dot for Group/Public/Private rows.
- ☐ `MemberListPanel` / DM-header grouping/label rendering for the 4 states.
- ☐ `ChatPageLayout` presence-event handling: Away/DND set member + channel statuses correctly.

### Android (`tests/DotNetCloud.Client.Android.Tests`)

- ☐ Converter: status → color/label (green/yellow/red/gray).
- ☐ `ChannelListViewModel`: snapshot + event map to status; DM-only dot semantics unchanged.
- ☐ `ChannelDetailsViewModel`: member presence resolution (if logic extracted for testability).
- ☐ Activity reporter throttling/lifecycle (pure-logic parts).

---

## 9. Verification (live, before commit — repo critical rules)

1. **Builds:** `dotnet build` (server + Chat module + UI.Web) and Android arm64 `dotnet build src\Clients\DotNetCloud.Client.Android -f net10.0-android -c Debug -r android-arm64` (0 warnings).
2. **Unit tests:** Core.Server, Chat module, Android.Tests suites pass (watch the pre-existing `ProgramRootCaTests` failure + known skips; they are unrelated).
3. **Live E2E (two users, Blazor + Android on device against the running server):**
   - ☐ Green while both interact.
   - ☐ Let one user go idle (no interaction, e.g. set threshold temporarily to 1 min via admin) → their dot turns **yellow** in the other client without disconnecting; interaction returns it to **green**.
   - ☐ Enable DND from Blazor → Android sees **red**; disable → back to green/yellow. Repeat from Android Settings → Blazor sees red.
   - ☐ Close the browser tab → peer shows yellow during retention, then **gray**.
   - ☐ Admin changes `PresenceIdleTimeoutMinutes` on the settings page → new threshold applies within ≤30 s (no restart).
   - ☐ logcat: no ~31 s reconnect churn after the heartbeat fix.
4. **Docs:** update `docs/IMPLEMENTATION_CHECKLIST.md` + `docs/MASTER_PROJECT_PLAN.md` (targeted edits, ✓/☐) and flip this plan's status after implementation.
5. Only then commit (and push to the working branch, not `main`).

---

## 10. Non-goals / notes

- ☐ **No connection-type separation** for relay vs presence (NotificationBell per-circuit relay). Accepted operator behavior: yellow represents the ~2–3 min retention window before gray.
- ☐ No new user-facing "set my status" picker; RED is driven by the existing chat DND toggle only.
- ☐ **No presence dot** on Public/Private **channel** rows or **Group** rows (a group is not a single user). Blazor's sidebar currently renders a dot on Group rows — this feature removes it so both clients show dots on **Direct Message rows only**. Member rows inside a conversation's member list are per-user and keep dots.
- ☐ `SetPresenceAsync`/`PresenceChanged`/"chat-presence" group / `PresenceChangedEvent` and the `StatusMessage` concept remain dormant (kept for a future custom-status feature); this feature does not add a status message UI.
- ⚠️ Android.Tests csproj compiles source files by explicit `<Compile Include>` — every new Android `.cs` referenced by compiled ViewModels must be added there.
- ⚠️ Editing `ChatPageLayout.razor` triggers Razor format-on-save reflows; keep diffs clean (git checkout + reapply pattern from repo memory).
- ⚠️ `read_file` editor-buffer-cache gotcha — verify disk state via `Get-Content`/`git diff` before editing files possibly changed outside the editor.

---

## 11. Resolved follow-up decisions (operator, 2026-09-09)

- ✓ **Admin UX:** dedicated "Online indicators" numeric field/section on the admin settings page (§3.5, §5.8).
- ✓ **Group rows:** no dots — aligned to DM-rows-only on both clients (§5.2, §10).
- ✓ **`Away` color:** true yellow `#EAB308` (§3.6).
- ✓ **Android channel-details member rows:** made live + 4-state — in scope (§6.5).

No open items remain.
