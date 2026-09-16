# Client/Server Mediation Handoff

Last updated: 2026-09-15 (**Notes: note folder assignment** — a note can now be filed at creation time, moved/unfiled afterwards, and is tagged with its folder on the sidebar card; server + Blazor done, deployed and operator-verified from branch `fix/notes-folder-assignment`, Android picker wiring handed to monolith — see the deferred handoff below. Earlier same day: **Trash restore now preserves the original directory path** — server-side fix implemented, tested and deployed to `cloud.dotnetcloud.net` (branch `fix/trash-restore-original-path`); earlier, the client-side "ignore a synced folder" deletion bug was fixed + live-verified on SyncTray `0.6.7` (`7632722c`). Earlier: 2026-09-09 Presence indicators → **4-state** Online/Away/Do-Not-Disturb/Offline — full-stack code committed on `fix/android-improvements` at `c17c7fa3`; server + Blazor + Admin changes are ready to deploy to `cloud.kimball.home` and require the live E2E in the Active Handoff. Plan `docs/PRESENCE_DOTS_4STATE_PLAN.md`; see Active Handoff.)

Purpose: shared handoff between client-side and server-side agents, mediated by user.

Archived context:

- Historical completed updates are in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Additional history remains available in git.

## Process Rules

**Agent autonomy (CRITICAL):**

- Both client and server agents work autonomously — they do NOT ask the moderator for context or permission.
- Agents pull the branch specified in the relay message, read the **Active Handoff** section, and execute the work described there independently.
- All actionable items, blockers, and technical details go directly in this document.
- **Latest (server agent — `cloud`):** `fix/notes-folder-assignment` — Notes folder assignment at create time + move/unfile; server + Blazor done and deployed, **Android half implemented and committed but with the on-device E2E still outstanding** (client agent — `monolith`; committed at the operator's explicit instruction — detail in the deferred handoff below)
- **Archived (server agent — `cloud`):** `fix/trash-restore-original-path` — trash restore now preserves the original directory path (implemented, tested, deployed to `cloud.dotnetcloud.net` 2026-09-15; archived below)
- **Completed (awaiting moderator PR):** `fix/synctray-ignore-folder` — SyncTray **0.6.7**, "ignore a synced folder" can no longer delete the folder server-side; pushed `7632722c`, installed and live-verified on `mint-OptiPlex-7010`
- **Pending deploy:** `fix/android-improvements` (Presence 4-state — server deploy to `cloud.kimball.home`; plan `docs/PRESENCE_DOTS_4STATE_PLAN.md`)
- **Still pending (mint22, dev):** `feature/module-widgets` — Module Home Widgets (plan `docs/MODULE_WIDGETS_PLAN.md`); kept below as a deferred handoff

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

## Archived (deferred) Handoff — Android Notes: wire the note folder picker (create + move) — STILL PENDING (2026-09-15)

**Status:** archived — the **server + Blazor half is complete, tested and deployed** (`fix/notes-folder-assignment`; full implementation detail + verification in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`). **The Android half is IMPLEMENTED and committed, but NOT verified on-device** (client agent — `monolith`; committed + pushed at the operator's explicit instruction, i.e. with the repo's no-commit-until-verified gate knowingly waived — see the status block below).

> **Android implementation status (2026-09-15, client agent — `monolith`):**
>
> - `NoteEditViewModel` loads the caller's folders into `FolderOptions` in **create _and_ edit** mode (a leading "None (unfiled)" entry) and mirrors the picker selection onto `SelectedFolderId`; `BuildUpdateDto()` sends **`ClearFolder = true`** whenever the note is left unfiled — that flag is what makes "move to None" actually unfile (a bare `folderId: null` means "no change" to the API).
> - A note filed in a folder **outside** the caller's own list (a shared note's owner folder) gets a "Shared folder" picker entry so saving can never silently unfile it, and the picker is hidden for a non-owner (`ViewerPermission != null`), matching the server's folder-ownership rule.
> - `NotesPage`'s **+** now carries the active folder chip (`NoteEdit?FolderId=…`) so a note created from a folder starts filed, and each note card shows the red folder tag (`NoteFolderTagConverter` + `NoteFolderLabels.Resolve`, falling back to "Shared folder").
> - Offline parity needed no payload change — the queued `OfflineNoteUpdatePayload`/`OfflineNoteCreatePayload` carry the same DTOs; a unit test asserts `ClearFolder` is present in the queued JSON.
> - Tests: **325 pass / 1 skip** (15 `NoteEditViewModelTests`, 5 `NoteFolderLabelsTests`, 1 PUT-body test proving `clearFolder` reaches the wire); Android arm64 Debug build **0 warnings / 0 errors**.
> - ⏳ **Remaining = on-device E2E only** (item 7 below) — **not done**. The emulator route was attempted (`pixel_7_-_api_36_0`, x64 build installed): the app installed and ran, but signing in to `cloud.dotnetcloud.net` from the emulator's Chrome kept returning the server's generic `invalid-credentials` message while the same account signs in from the operator's desktop browser, so the OAuth callback never reached the app and the picker could not be exercised. Server-side branch confirmed by reading `AuthSessionController.LoginAsync`: the message is the `invalid-credentials` branch (`IsLockedOut` / `IsNotAllowed` / MFA all render different text), and the emulator's typed fields were verified byte-accurate (username 11 chars, password 10 chars ending `0x24`, no surrounding whitespace) — i.e. the rejection is a credential mismatch on the server, not an emulator input artefact. **Committed + pushed anyway at the operator's explicit direction**, so the on-device verification is still owed. To finish: get a working sign-in (compare against Chrome's saved password rather than typed-from-memory) and run the checklist in item 7 (create-in-folder → move → "None (unfiled)" confirmed after a server-driven list reload).
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

## Active Handoff — Presence indicators 4-state: deploy `c17c7fa3` to `cloud.kimball.home` + live E2E (2026-09-09)

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
