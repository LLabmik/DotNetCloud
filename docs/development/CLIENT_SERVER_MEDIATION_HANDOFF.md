# Client/Server Mediation Handoff

Last updated: 2026-07-20 (Android Calendar Alarm Reminders — end of session, handoff to new session)

Purpose: shared handoff between client-side and server-side agents, mediated by user.

Archived context:

- Historical completed updates are in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Additional history remains available in git.
- VFS Phase 3 (Windows Cloud Filter API) completed on Windows11-TestDNC (2026-05-12).
- VFS Phase 2 (core abstraction layer) completed on Windows11-TestDNC (previously).
- **2026-07-19:** Android Chat Channel Mute — client-side E2E testing (archived).
- **2026-07-20:** Android Calendar Alarm Reminders — initial implementation + partial E2E test

## Process Rules

**Agent autonomy (CRITICAL):**

- Both client and server agents work autonomously — they do NOT ask the moderator for context or permission.
- Agents pull the branch specified in the relay message, read the **Active Handoff** section, and execute the work described there independently.
- All actionable items, blockers, and technical details go directly in this document.
- **Current active branch:** `feature/android-chat-channel-mute`

## Active Handoff

**Summary:** Android Calendar Alarm Reminders — scheduler works, alarm fires, but notification not appearing on device.

**Branch:** `feature/android-chat-channel-mute`

**Context:** Full calendar reminder system implemented. Server-side `ReminderDispatchService` deployed to `cloud.kimball.home`. Android client has local `AlarmManager` scheduling, `CalendarAlarmReceiver`, `CalendarBootReceiver`, reminder picker in event editor, exact alarm permission UI, and timezone-aware display. E2E testing confirmed scheduling works and broadcasts are received, but notifications don't appear on screen (not even when app is closed).

**All changes committed in this session (commit pending):**

### What's Implemented (all client-side)

| Component | Files | Status |
|-----------|-------|--------|
| Notification channel `calendar_reminders` (High + alarm sound) | `MainApplication.cs` | ✅ |
| `SCHEDULE_EXACT_ALARM` + `RECEIVE_BOOT_COMPLETED` permissions | `AndroidManifest.xml` | ✅ |
| `CalendarAlarmReceiver` — alarm broadcast → notification with deep-link | `Platforms/Android/CalendarAlarmReceiver.cs` | ✅ |
| `CalendarBootReceiver` — reschedule alarms after reboot | `Platforms/Android/CalendarBootReceiver.cs` | ✅ |
| `CalendarReminderScheduler` — `AlarmManager.setExactAndAllowWhileIdle()` with permission-aware fallback | `Services/CalendarReminderScheduler.cs` | ✅ |
| Reminder picker in event editor (blue bordered REMINDER section right below Title) | `EventEditViewModel.cs` + `EventEditPage.xaml` | ✅ |
| Auto-adjust end time to start+1h | `EventEditViewModel.cs` | ✅ |
| Timezone: save as UTC, display as local time | `EventEditViewModel.cs` + `CalendarViewModel.cs` | ✅ |
| Scheduler logs via `Android.Util.Log.Info("DotNetCloud", ...)` for logcat | `CalendarReminderScheduler.cs` | ✅ |
| `IExactAlarmPermissionService` + Settings card with Fix button | `AndroidExactAlarmPermissionService.cs` + `SettingsViewModel.cs` + `SettingsPage.xaml` | ✅ |
| `type=calendar_reminder` push handler in FCM + UnifiedPush | `FcmMessagingService.cs` + `UnifiedPushReceiver.cs` | ✅ |
| Foreground suppression removed (alarms sound always) | `CalendarAlarmReceiver.cs`, `FcmMessagingService.cs`, `UnifiedPushReceiver.cs` | ✅ |

### What's Working (confirmed)

- ✅ Event creation/deletion on server
- ✅ Reminder picker visible and functional
- ✅ `ScheduleRemindersAsync` processes events and schedules `AlarmManager` alarms
- ✅ `SCHEDULE_EXACT_ALARM` permission — exact alarms confirmed working (`window=0 exactAllowReason=permission`)
- ✅ `CalendarAlarmReceiver` receives broadcasts — confirmed in logcat (3 broadcasts at 23:42, 23:43, 23:53)
- ✅ Server-side `ReminderDispatchService` deployed on `cloud.kimball.home`

### What's NOT Working

- ❌ **Notification never appears on device.** `CalendarAlarmReceiver.OnReceive` receives the broadcast but no notification is shown. Need to debug:
  - Is `ShowReminderNotification` being called? (no Android.Util.Log in that method)
  - Is `NotificationManager.Notify()` failing?
  - Is the notification channel configured correctly?
  - Add logcat logging to `ShowReminderNotification` to trace execution

- ❌ **Monthly calendar only shows event count, not titles.** `CalendarDayItem` shows `"{Events.Count} events"` label. Needs event dots/bars in month view cells.

### Build Notes

**CRITICAL:** `dotnet build` without `-r android-arm64` only builds for x64 (emulator). The arm64 APK at `bin/Debug/net10.0-android/android-arm64/` stays stale. Always use:
```powershell
dotnet build ... -f net10.0-android -c Debug -r android-arm64 /p:AndroidSdkDirectory="C:\Program Files (x86)\Android\android-sdk"
```

### Server Status

`cloud.kimball.home` has `ReminderDispatchService` (30s scan, 24h lookahead, `ReminderMethod.Notification` filter, `ReminderLog` dedup table, recurrence expansion). No FCM credentials configured — push falls back to SignalR for connected clients and in-app notifications.

### Next Session Priorities

1. **Debug notification not showing** — add logcat logging in `ShowReminderNotification`, verify `NotificationManager.Notify()` executes
2. **Enable battery optimization exemption** — app is in standby bucket 30 (RESTRICTED), which defers alarm delivery. User needs to tap "Fix" on Settings → Battery Optimization card
3. **Test with app closed** — after battery fix, create event with 2min reminder, close app, verify notification with alarm sound
4. **Fix monthly multi-event display** — show event titles/dots in month cells
5. **Test recurring events** — verify dedup via `ReminderLog`
6. **Test server-push** — verify `type=calendar_reminder` handler
7. **Record these findings to repo memory**

**Active Handoff format (MANDATORY):**

Every Active Handoff MUST use per-machine action blocks. Actions are grouped by the machine that executes them, using the exact machine names from the Environment table.

```markdown
### Active Handoff

**Summary:** [one-line description of what's happening]

[Context/background — what changed, why, relevant commits]

---

### Server Actions — `cloud.kimball.home`

- [ ] Action 1 with exact commands
- [ ] Action 2

### Client Actions — `mint-OptiPlex-7010`

- [ ] Action 1 with exact commands
- [ ] Action 2
```

**Critical rules:**
- Each agent ONLY executes actions in the block matching their machine name (from the Environment table).
- If no action block matches your machine, the handoff is not for you — relay it to the moderator.
- Always include exact commands (ready to copy-paste).
- Mark blocks with `✓` when complete; update status inline.
- One handoff may have 1 or 2 action blocks depending on workflow stage.

**Handoff management:**

- Put all technical findings, debugging conclusions, and next-step details in this document.
- Assistant (current agent) commits their findings/work and updates the **Active Handoff** section with actionable next steps for the other client.
- Assistant pushes commits to `feature/android-files-photo-thumbnails`.
- Unexpected untracked content rule (MANDATORY): remove unexpected untracked files/directories before commit; only keep intentional tracked changes for the handoff update.
- Handoff readiness gate (MANDATORY): all executable tests must pass before marking a handoff as ready.
- Environment-gated tests are allowed to be skipped, but must be explicitly identified as gated with the required environment/runtime prerequisites documented in the handoff.
- Runtime verification gate (MANDATORY): before declaring a server-side blocker fixed, verify the running service is on current binaries (not stale publish output) and document the verification command/output in handoff notes.
- OAuth contract check (MANDATORY when auth is involved): verify `client_id`, `redirect_uri`, and requested scopes exactly match server-registered OpenIddict client permissions before requesting cross-machine retries.
- Secret handling rule (MANDATORY): never commit raw bearer tokens/refresh tokens; share token acquisition steps and sanitized outputs only.
- Moderator relays a short "check for updates" message to the other machine.
- Moderator handoff prompt rule (MANDATORY): every ready-to-relay message must explicitly state the target machine name (for example: `cloud.kimball.home`, `mint-dnc-client`, `Windows11-TestDNC`).
- Other agent pulls latest, reads the handoff, and takes action without asking questions.

**Document maintenance:**

- Pre-commit archive rule (MANDATORY): before committing this file, move all completed/older handoff tasks to `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Keep only the single current task in **Active Handoff** (one active block only).
- If a task is completed, archive it first, then replace **Active Handoff** with the next task.

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

<!-- carry-forward contracts and old Android changes archived to CLIENT_SERVER_MEDIATION_ARCHIVE.md -->
