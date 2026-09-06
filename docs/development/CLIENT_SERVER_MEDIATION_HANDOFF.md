# Client/Server Mediation Handoff

Last updated: 2026-09-06 (Blazor form defaults implemented + deployed by server agent mint22 → back to client agent for PR/next)

Purpose: shared handoff between client-side and server-side agents, mediated by user.

Archived context:

- Historical completed updates are in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Additional history remains available in git.

## Process Rules

**Agent autonomy (CRITICAL):**

- Both client and server agents work autonomously — they do NOT ask the moderator for context or permission.
- Agents pull the branch specified in the relay message, read the **Active Handoff** section, and execute the work described there independently.
- All actionable items, blockers, and technical details go directly in this document.
- **Current active branch:** `fix/form-submit-handling` (Blazor form defaults — implemented commit `5ddc81dd`, deployed to mint22 dev; awaiting user PR merge → client agent `monolith` for next steps)

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

## Active Handoff

**Status:** ✅ COMPLETED — Blazor form defaults implemented, deployed to mint22 dev, and user-tested (2026-09-06, server agent mint22). Next: user PR merge to main; client agent may wire more forms via the shared mechanism (plan §6).

**Target agent:** monolith (client, next steps)
**Branch:** `fix/form-submit-handling` (HEAD `5ddc81dd` = implementation)
**Canonical plan:** `docs/FORM_ENTER_SUBMIT_PLAN.md` (read it FIRST — fully self-contained)

### Completion record (2026-09-06, server agent mint22) ✅
- Implemented per the plan on `fix/form-submit-handling` (commit `5ddc81dd`): new `form-defaults.js` (shared, attribute-driven) registered in `App.razor`; auth pages wired (Login autofocus + `autocomplete="current-password"`, MfaVerify `data-autosubmit="6"`, MfaSetup `@bind:event="oninput"` + `@bind:after` auto-submit + autofocus); Files dialogs got `data-enter-submit`/`data-autofocus-first` (per-row for New File) and the four `Handle*KeyDown` C# handlers trimmed to Escape-only.
- **Acceptance fix 1 (Files Enter used stale value):** dialog inputs bound `@bind` (onchange) lagged the model on Enter → added `@bind:event="oninput"` to the four Files dialog inputs so Enter submits with the typed value (Rename `Test.txt`→`Test2.txt`; New File `Test.docx`).
- **Acceptance fix 2 (browser password save):** Login password field had no `autocomplete` → added `autocomplete="current-password"`; Firefox now prompts to save (Edge needs per-site state cleared — not a code issue).
- **Deploy + verify:** `tools/redeploy-baremetal.sh` → `/health/ready` + `/health/live` Healthy, no pending migrations; `_content/DotNetCloud.UI.Web/js/form-defaults.js` → 200 text/javascript. Files module tests 757/757.
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
