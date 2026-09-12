# Android Media Auto-Upload Redesign — Status & Resume Plan

> ⚠️ **STATUS: IN PROGRESS (2026-09-12) — committed on `feature/android-media-auto-upload`; E2E verified on device.** Read this file + `/memories/repo/DotNetCloud.md` (`## 🚧 RESUME HERE` section) before doing anything in a new session.

## Goal

Fix Android media auto-upload (photos/videos → server `AutoUpload/YYYY/MM`), which had not uploaded anything "since last month", and make it robust: take a picture/video, have it back up to the server automatically, respecting the user's storage quota.

## Root cause found (fixed)

The app **declared but never requested** the Android 13+ runtime permissions `READ_MEDIA_IMAGES`/`READ_MEDIA_VIDEO`. On Android 13+ (device API 34/36), MediaStore queries for other apps' photos silently return empty without them → the watcher saw an empty gallery → nothing ever uploaded. Compounded by a fragile watermark + unbounded `Preferences` dedup design and a Samsung-prone observer (stack-overflow fixed separately).

## What was implemented (all uncommitted working-tree changes)

1. **Permissions** — `Services/IMediaPermissionService.cs`, `Platforms/Android/AndroidMediaPermissionService.cs`, `Platforms/Android/MediaLibraryReadPermission.cs` (MAUI custom permission). Runtime request on enabling auto-upload + Settings "PHOTOS ACCESS" card (`SettingsViewModel`: `ShowMediaAccessCard`, `RequestMediaPermissionCommand`, `RefreshMediaPermission`; `SettingsPage.xaml` card; `SettingsPage.xaml.cs` refresh on appear). Manifest: `READ_EXTERNAL_STORAGE maxSdkVersion=32` for API 29–32. Registered in `MauiProgram.cs`; `IMediaPermissionService` added to `Android.Tests.csproj` Compile includes.
2. **Watcher rewrite** — `Services/MediaAutoUploadService.cs`:
   - SQLite index `Services/MediaUploadIndex.cs` (`media_upload.db3`, key = `name|size|dateAddedSec`; recorded only AFTER a successful upload → failures retried naturally). Replaces lastTs watermark + fingerprint prefs.
   - Read-only MediaStore enumeration (`QueryMediaCandidates`, images+video), semaphore-serialized scans, per-pass cap 40 + 1-min backlog cadence; wake-channel (`RunScanAsync`) so manual sync / post-grant / observer drain a backlog fast.
   - Backfill = automatic when index empty; server-side dedup seed (`EnsureServerSeedLoadedAsync` walks the server AutoUpload tree once/process, skips name+size already present).
   - `PrefLastSuccessTs` = `media_upload_last_success`; Files banner reads it (with legacy fallback).
   - **No `dataSync` foreground service** (auto-start removed from `SettingsViewModel.OnAutoUploadEnabledChanged` and `App.NavigateToStartPageAsync`) → watcher runs **in-process** (loop + observer while app alive + catch-up scan on launch). `MediaUploadForegroundService` class + manifest entry remain but are inert (never started).
3. **Quota watch + notify** — `Services/QuotaGate.cs` (+ `tests/.../Services/QuotaGateTests.cs`, 9 tests). Watcher queries `GetQuotaAsync` each pass, caps each batch to the remaining bytes, and posts an **"Auto-upload paused — storage full"** notification (id 3003, used/total) once per transition when full; clears when room frees; stops the pass on server `409`. Server semantics: `MaxBytes == 0` = unlimited; over-quota upload = `409` + `FILES_QUOTA_EXCEEDED`. ben.kimball account = 50 GB (finite).

## Root cause #2 found & fixed (2026-09-12) — stall after the permission fix

The permission fix worked but the watcher then made **zero progress for ~2.3 days** (`media_upload_last_success` frozen at 2026-09-09 22:31). Three compounding causes:

1. **Head-of-queue blocking (the primary bug).** `QueryMediaCandidates` sorted `date_added ASC` (oldest first) and `MediaUploadIndex` records a row **only after a fully successful upload**. The oldest un-uploaded item was `20240815_203816.mp4` — **1.72 GB**. At `MaxConcurrency = 1` (sequential, deliberately, to avoid HTTP 429) with 4 MB chunks that is ~430 chunk PUTs. Every pass restarted on that same file, so **nothing was ever recorded** until it finished — and it never finished.
2. **Process death mid-upload.** lmkd reaped the app during that long window (the device was in a foreground-service churn storm — Samsung Health / a fitness-band app / Phone Link each cycling an FGS ~every 60 s; Samsung services alone held ~5.6 GB across 52 processes).
3. **Watcher never restarted after process death.** `App.NavigateToStartPageAsync` (the only caller of `watcher.StartAsync`) runs solely from `App.OnStart`, i.e. only when an **Activity** starts. A background process start (push, calendar alarm) left auto-upload dormant forever. Confirmed in logs: pid 15938 seeded the index and began uploading; its replacement pid 22182 logged calendar/chat work but **never seeded**, and chunk PUTs froze.

### Fixes applied (2026-09-12)

1. **`MaxSingleItemBytes = 500 MB`** (`MediaAutoUploadService`) — items above the cap are skipped and reported (`Skipped N media item(s) larger than 500 MB.`). Verified on device: **17 oversized items skipped**, including the 1.72 GB video.
2. **Smallest-first ordering** — `QueryMediaCandidates` now returns `OrderBy(c => c.Size)`, so quick wins are recorded immediately and a single huge file can never block the queue. Verified: uploaded sizes increased monotonically (3457215 → 3710543 bytes) during a pass.
3. **Watcher starts on process start** — `MainApplication.OnCreate` now calls `StartMediaAutoUploadWatcher()` (guarded by `media_upload_enabled`; `StartAsync` is idempotent, so the existing navigation-path call is harmless). Auto-upload no longer depends on the UI lifecycle.
4. **Logcat observability (permanent)** — new `Platforms/Android/AndroidLogLoggerProvider.cs`, registered in `MauiProgram.CreateMauiApp`. `AddDebug()` only writes to an attached debugger, so every `ILogger` call — including all of the watcher's `skipped because…` branches — was invisible via `adb logcat`. Now visible under the `DotNetCloud` tag in Debug **and** Release. This is what made the diagnosis possible.

### `dataSync` 24-h FGS budget — verified NOT consumed (2026-09-12)

| Path                           | FGS type                                       | Budget impact                                                                                               |
| ------------------------------ | ---------------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| Media auto-upload watcher      | **none** (in-process loop + `ContentObserver`) | ✓ zero                                                                                                      |
| `ChatConnectionService`        | **none — FGS removed entirely**                | ✓ zero — `dataSync` declaration, promotion block, notification builder and `OnTimeout` override all deleted |
| `MediaUploadForegroundService` | — class deleted                                | ✓ gone (file + manifest entry both removed)                                                                 |
| `MusicPlaybackService`         | `mediaPlayback`                                | separate budget; only during playback                                                                       |

There is **no** `StartForegroundService` call site left in the app any more, and `FOREGROUND_SERVICE_DATA_SYNC` was dropped from the manifest (only `FOREGROUND_SERVICE` + `FOREGROUND_SERVICE_MEDIA_PLAYBACK` remain). Live check after the change (2026-09-12): `dumpsys activity services net.dotnetcloud.client` → `ChatConnectionService` present with **`startForegroundCount=0`** and no `foregroundServiceType`, and SignalR still connects (`EnsureSignalRConnectedAsync: SignalR connected successfully!`).

## P5 background sync — implemented & verified (2026-09-12)

**Problem:** the watcher is in-process only, so nothing runs while the app is closed. The trigger was missing — not the ability to scan.

**Design:** a persisted **one-shot** `JobScheduler` job that re-arms itself after every run, on an **adaptive cadence** — every 5 minutes while media is still queued, hourly once the queue is empty. A _periodic_ job cannot express the active cadence because `JobInfo.Builder.SetPeriodic` is clamped to a 15-minute platform minimum, whereas `SetMinimumLatency` (one-shot) has no such floor. Deliberately **not** WorkManager (would need a new central package; the earlier `Xamarin.AndroidX.Work.Runtime` attempt hit transitive-AndroidX conflicts) and **not** a foreground service (would spend the `dataSync` budget). `JobScheduler` is Doze-aware, honours network constraints, survives reboots, and is accounted separately from the foreground-service budget.

The cadence is driven by `IMediaAutoUploadService.HasPendingWork`, which is true when a pass left media queued — either a backlog beyond the 40-item per-pass cap, or items the pass selected but failed to upload (failures are not recorded in the index, so they remain pending).

**Files:**

- `Platforms/Android/MediaUploadJobService.cs` — `JobService` (id `3107`). `OnStartJob` returns `true` (keeps the process alive + wake-locked until `JobFinished`) and caps a run at 8 minutes. Its `finally` calls `JobFinished` and then `ScheduleAfterRun(upload.HasPendingWork)` to re-arm the chain with the active or idle delay. A timed-out or failed pass reports pending work so the queue cannot stall behind the idle interval.
- `Platforms/Android/AndroidBackgroundMediaSync.cs` — `IBackgroundMediaSync` implementation. `Schedule()` is idempotent (checks `AllPendingJobs` first) and runs on every process start, which also re-arms the chain if a run was killed before it could reschedule itself; `ScheduleAfterRun(pendingWork)` picks 5 min vs 1 h. Uses `SetPersisted(true)`, `SetRequiredNetworkType(Unmetered)`, `SetMinimumLatency`.
- `Services/IBackgroundMediaSync.cs` — interface so `SettingsViewModel` stays testable on plain `net10.0` (it is compiled into `DotNetCloud.Client.Android.Tests`; an Android-only type there would break that build). Added to the test csproj `Compile` list.
- `MainApplication.OnCreate` — schedules when `media_upload_enabled`, cancels otherwise.
- `SettingsViewModel.OnAutoUploadEnabledChanged` — keeps the job in step with the toggle.

**Verified on R5CWC356B2K (app process dead for every step):**

- Job registers as a **one-shot** with `PERSISTED`, `Network type: NOT_METERED&INTERNET` and a `SetMinimumLatency` window (no `PERIODIC` line).
- **Adaptive cadence verified end-to-end both ways** (app process dead throughout):
  - Empty queue → `Background media sync scheduled in 60m (result=1)`.
  - 81-photo backlog → `Found 81 new photo(s)/video(s); uploading 40 this pass` → `finished one scan pass (pending work: True)` → **`scheduled in 5m (result=1)`**.
  - **~5 minutes later the job fired on its own** (process dead): `Found 35 … uploading 35` → `pending work: False` → **`scheduled in 60m (result=1)`**. 81 files uploaded across the cycle.
- ⚠️ **A one-shot job needs an override deadline.** With only `setMinimumLatency`, the platform deferred our job indefinitely: `JobStatus{…#u0a464/3107 … TIME=-1m50s38ms …}` while `RUNNABLE`, standby bucket `ACTIVE (10)`, screen awake — it never dispatched. `SetOverrideDeadline(delay + 2 min)` fixes it; the JobInfo now reports `Minimum latency: +1h0m0s0ms` **and** `Max execution delay: +1h2m0s0ms` / `Has late constraint`.
- **Survives reboot** — present again after `adb reboot` with the app never launched.
- `cmd jobscheduler run -f net.dotnetcloud.client 3107` with the app dead → process starts cold, `Background media sync job started (headless)`, full scan runs, **no crash**.
- **Real headless upload:** photo `20260912_005339.jpg` (3,001,462 bytes) captured _while the app was not running_, then the job alone backed it up — `Found 1 new photo(s)/video(s); uploading 1 this pass` → `Uploaded 20260912_005339.jpg`. `media_upload_last_success` advanced to 14 s before wall clock.
- **Zero foreground services:** `dumpsys activity services net.dotnetcloud.client` → empty; no `startForegroundCount`. The `dataSync` 24-h budget is untouched.

⚠️ **Install with `adb install -r --no-incremental` when testing.** Without it, fast-deploy/incremental packaging causes a `SIGSEGV` in `libmonodroid.so` (`EmbeddedAssemblies::open_from_bundles`) on a `.NET TP Worker` thread during headless cold-start. That crash was previously misattributed to MAUI needing an Activity.

## Verification state (2026-09-09)

- Build: `dotnet build src\Clients\DotNetCloud.Client.Android -f net10.0-android -c Debug -r android-arm64 /p:AndroidSdkDirectory="C:\Program Files (x86)\Android\android-sdk"` → clean, 0 warnings/errors.
- Tests: `dotnet test tests\DotNetCloud.Client.Android.Tests` → **283 passed / 1 skipped**.
- On-device (R5CWC356B2K, cloud.dotnetcloud.net): permission grant via Fix worked; watcher uploaded a large backfill in the foreground (progress notification 3001 counted to 40+); **no** media FGS; app stable; leftover JobScheduler test job purged.

## Verification state (2026-09-12) — E2E PASSING

- Build: arm64 Debug → **0 warnings / 0 errors**. Tests: `dotnet test tests\DotNetCloud.Client.Android.Tests` → **283 passed / 1 skipped / 0 failed**.
- Merge of `origin/main` resolved (in-process watcher kept; no `dataSync` auto-start).
- **End-to-end verified on R5CWC356B2K:** watcher seeded 734 server files → `Skipped 17 media item(s) larger than 500 MB` → `Found 113 new … uploading 40 this pass` → **40/40 uploaded in one pass**, all 40 distinct (no duplicates). Index grew 40,960 → 45,056 bytes; new destination folders `2024/09`, `2024/10`, `2024/11`, `2026/02`, `2026/09` appeared (newest was previously `2024/08`). `media_upload_last_success` advanced 1788993076 → **1789191395** (14 s before wall clock). Next pass found **73** pending (113 − 40) and continued on the 1-minute backlog cadence. Process stable (`pidof` unchanged across passes), **zero foreground services**.
- Still open: quota-full notification path (needs a tiny quota); true background/headless sync when the app is closed (P5).

## Known issues / gotchas

- **`dataSync` FGS 24-h budget (Android 15/16):** rolling per-24 h window, persisted across reboots (reboot does NOT reset). When exhausted the platform throws `ForegroundServiceDidNotStopInTimeException` and can crash-loop via Sticky restarts. **Fixed 2026-09-12 by removing the FGS from `ChatConnectionService` entirely** rather than changing its type. Deleted: the `ForegroundServiceType = DataSync` attribute, the `AndroidForegroundServicePolicy` promotion branch, the `OnTimeout(int)` override, `BuildNotification()`, the `MediaUploadForegroundService` class, and the `FOREGROUND_SERVICE_DATA_SYNC` manifest permission. The change is **behavior-preserving** — promotion was already disabled by the kill-switch, and FCM/UnifiedPush already own background delivery. Live check: `startForegroundCount=0`, no `foregroundServiceType`, SignalR connects, process stable.
  - ⚠️ **Do NOT re-add a foreground service to `ChatConnectionService`.** `remoteMessaging` (`FOREGROUND_SERVICE_TYPE_REMOTE_MESSAGING`) was evaluated and **rejected** — it is for _device-to-device_ transfer (phone↔watch apps), not server-push messaging, and does not describe a SignalR hub connection. `specialUse` would require a Play Store justification. A Sticky, non-promoted service is the intended end state.
- **Background-sync experiment re-diagnosed (2026-09-12):** the earlier note claimed a native `JobScheduler` `JobService` "crashed on headless cold-start (MAUI startup assumes an Activity)". **That diagnosis was wrong.** The crash was `signal 11 (SIGSEGV)` in `libmonodroid.so` → `EmbeddedAssemblies::open_from_bundles()` on a `.NET TP Worker` thread, and it was an **incremental-install artifact** (the dropbox record literally says `Incremental: Yes`, with fast-deploy noise like `.__override__` and `open_from_update_dir: assembly file DOES NOT EXIST`). Reinstalling with `adb install -r --no-incremental` and repeating the identical headless start runs **cleanly** — no crash. MAUI has always started headlessly fine (proven by `FcmMessagingService` / `CalendarBootReceiver`). ⚠️ **Always install with `--no-incremental` when testing this app**, or you will chase phantom native crashes in the assembly loader.
- C#/Android namespace gotcha: inside `namespace DotNetCloud.Client.Android.*`, bare `Android.Content.*`/`Android.Content.PM.Permission` binds to `DotNetCloud.Client.Android` → use `global::Android.*` or `using Android.Content;`.
- `IFileRestClient.ListChildrenAsync` param is `folderId`, not `parentId`.
- `PendingIntent`/`PendingIntentFlags` live in `Android.App` (fully qualify in the Services namespace).
- CA1416: wrap platform-guarded calls in `#pragma warning disable` with SDK-check comments (repo style).

## Not done (deferred, needs device/decisions)

- ~~**P4 capture→gallery single path**~~ — **DONE 2026-09-12**, by removing the Files-tab capture path entirely rather than redirecting it into the gallery. See "Media ownership" below. The spike question (`MediaScannerConnection.ScanFile` vs no-`EXTRA_OUTPUT`) is moot: we do not write to MediaStore at all, which also sidesteps the Samsung API 34 MediaStore-insert `SIGSEGV` recorded in repo memory.
- ~~**P5 background sync** (the "max files/day" ask)~~ — **DONE 2026-09-12** (see below).
- ~~**Chat FGS `dataSync`**~~ — **DONE 2026-09-12**: the FGS was removed outright (not retyped). See the `dataSync` budget table + known-issues note above.
- ~~Server `AutoUpload` test photos~~ — **DONE 2026-09-12**: the ~132 verification test photos and `dnc_*.png` screenshots were deleted from the server `AutoUpload` tree by the operator. No cleanup outstanding.

## Media ownership — one path only (2026-09-12)

**Decision (operator):** DotNetCloud no longer controls the camera. The platform camera app owns capture, MediaStore owns storage and the gallery, and `MediaAutoUploadService` observes MediaStore and uploads whatever appears. Backing up the camera roll is the ONLY media path.

This replaced a two-path design where the Files tab ran its own capture pipeline:

1. `MediaPicker.CapturePhotoAsync()/CaptureVideoAsync()` launched the camera with `EXTRA_OUTPUT` pointed at an **app-private cache file** — so a photo taken from the Files tab never reached the gallery and no other app could back it up.
2. That file was spooled to `PendingUploads/` and uploaded down a **second, parallel upload path** that re-implemented the rules slightly differently: it honoured only the "auto-upload enabled" preference and ignored the Wi-Fi-only, charging-only and battery-threshold gates, the 500 MB per-item cap, and the storage-quota gate.

**Removed:** `CapturePhotoCommand`, `CaptureVideoCommand`, `UploadMediaFileAsync`, `SaveToPendingUploadsAsync`, `ReadMediaStreamAsync`, `RequestLocationPermissionAsync`, `BuildAndroidStyleFileName`, the `PendingUploads` spool (writer + `UploadPendingFilesAsync` flush), `IMediaAutoUploadService.ResolveUploadTargetFolderAsync` (no callers left), `FileItemViewModel.IsPending` + the Files-tab "Pending" badge, and the two camera toolbar buttons.

**Permissions dropped:** `android.permission.CAMERA` and `android.permission.ACCESS_FINE_LOCATION` (the latter existed only so the camera app could embed GPS EXIF). Verified absent from the installed package. Dropping `CAMERA` also removes an Android quirk: if an app *declares* `CAMERA`, then `ACTION_IMAGE_CAPTURE` requires the runtime grant to be present.

**Kept:** `MessageListViewModel.AttachFileAsync` still uses `MediaPicker.Default.PickPhotosAsync()` — that *picks* from the gallery to attach to a chat message; it does not drive the camera.

**Verified on device (R5CWC356B2K, `--no-incremental`):** `dumpsys package` shows no `CAMERA`/`ACCESS_FINE_LOCATION`; the Files toolbar renders only New Folder + Upload; the watcher seeded 845 server files, skipped 17 oversized items and uploaded a video into `AutoUpload/YYYY/MM`; SignalR connected; zero foreground services; zero crashes.

## ⚠️ Debug interpreter disabled — native-crash root cause (2026-09-12)

The `open_from_bundles` / `strcasecmp` native-crash family was **not** (as first believed) only an incremental-install artifact. Full `data_app_native_crash` history: six crashes on 2026-09-09 with **no** `Incremental` field, and one on 09-12 **with** `Incremental: Yes`. Two independent problems, identical-looking signature.

The real cause is the **Mono interpreter**, which `<AndroidUseInterpreter>true</AndroidUseInterpreter>` enabled in the `Debug` PropertyGroup (from `02b64607`, 2026-03-18). It resolves field/method types **lazily on the executing thread**, matching the symbolized backtrace:

```
mono_field_resolve_type → mono_class_get_checked → mono_class_from_typeref_checked
  → mono_assembly_load_reference → mono_assembly_request_byname → strcasecmp  ← garbage pointer
```

The other variant is `EmbeddedAssemblies::open_from_bundles` reading past its buffer. One variant recurred with a **byte-identical fault address** across runs, so it is deterministic rather than a race. Release was never affected — it uses AOT (`RunAOTCompilation=true`) and never enables the interpreter.

A fix was found on 2026-09-09 (interpreter off → 0 crashes in 10 forced restarts) but **was never committed**; `git log --since=2026-09-01` on the csproj shows only version bumps and the R8/AOT commit. It is now applied and soaked.

**Soak (2026-09-12, this change):** 12 iterations of HOME → `am kill` (process confirmed `DEAD`) → `cmd jobscheduler run -f <pkg> 3107` (headless cold start). **12 distinct pids, 0 crashes** in the crash buffer, 0 new `data_app_native_crash` dropbox entries, 0 DotNetCloud `has died` events. Screen stayed awake and unlocked (`locked=0` every iteration) with Doze `ACTIVE`, so no sleep/Doze confound. ⚠️ Caveat: the crash previously ran ~4×/day, so 12 clean starts is strong evidence but not absolute proof — re-run the soak if this family ever reappears.

⚠️ **Two testing traps hit while doing this:** (1) `am kill` will **not** kill a foreground process, so a soak without `KEYCODE_HOME` first silently reports the same pid every iteration and proves nothing — always assert the process is actually `DEAD` before forcing the job. (2) `.Trim()` on the output of `adb shell pidof` throws when the process is correctly dead (null) — handle null.

## ⚠️ Music player crash — reproduced, root-caused, fixed (2026-09-12)

The operator reported a crash while listening to music (2026-09-11, 02:00–04:15). **The 9/11 record itself is unrecoverable** — dropbox retains only 09-09 and 09-12 (all 09-10/09-11 entries rotated out before the 09-12 00:52 reboot), and the 13 `tombstone_*.pb` files were emptied at **09-11 03:54** (inside the window), so the content is gone. The timestamp confirms an event occurred but not its cause.

The bug was therefore found by auditing the music path, then **reproduced live**: 8 rapid track changes killed the process with `SIGABRT`.

### Root cause

```
FATAL UNHANDLED EXCEPTION: Java.Lang.IllegalStateException
  at Android.Media.MediaPlayer.get_AudioSessionId()
  at MusicPlayerService.get_AudioSessionId()
  at AndroidEqualizerService.OnPlaybackStateChanged()
  at MusicPlayerService.<StartPositionTimer>b__72_0()      ← the 1-second tick timer
  at System.Threading.TimerQueueTimer...                    ← thread-pool thread, no caller to catch
```

`AudioSessionId` was `_mediaPlayer?.AudioSessionId ?? 0` — a **live read of the current player**. During a track switch the previous player is `Release()`d while still referenced, so a timer tick landing in that window called `getAudioSessionId()` on a released player. Android throws `Java.Lang.IllegalStateException` for that. Because the read happened inside a `Timer` callback on a thread-pool thread there is no caller to catch it, so it surfaced as an unhandled exception and the .NET runtime called `abort()` → `SIGABRT`. Observed directly in the log: the main thread released the player at `02:23:28.313` and the timer read it at `.319`.

### Fixes

1. **Cache the audio session id** (`_audioSessionId`), captured in `OnTrackPrepared` while the player is guaranteed valid. `AudioSessionId` no longer touches the player at all — this is the real fix.
2. **Publish `null` before releasing** in `PrepareAndStartAsync` (`_mediaPlayer = null` + `_audioSessionId = 0` *before* `previousPlayer.Release()`), narrowing the window for any reader that already observed the old instance.
3. **`CurrentPosition`/`Duration` degrade instead of throwing** — they had the identical hazard (`getCurrentPosition()` on a released player throws too) and would have crashed eventually via the ViewModel.
4. **Exceptions cannot escape the timer callback or the equalizer handler** — `RaisePlaybackStateChangedSafely()` and a try/catch around `AndroidEqualizerService.OnPlaybackStateChanged`. An exception thrown from a `Timer` callback is fatal by construction.

### Also fixed: wake lock leak (`MusicPlaybackService.AcquireWakeLock`)

It created and acquired a **new** `PowerManager.WakeLock` on every `OnStartCommand`, overwriting `_wakeLock` without releasing the previous instance. Wake-lock acquisitions are **not** deduplicated by tag, so each one needs its own release, and only the newest was reachable from `OnDestroy` — so a session leaked one partial wake lock per track start plus one per pause/resume and per notification button, pinning the CPU awake (battery drain, `PowerManagerService: Excessive delay in releasing WakeLock`). Now idempotent (`if (_wakeLock?.IsHeld == true) return;`) with a matching `ReleaseWakeLock()`, and the lock is only acquired when a track is actually loaded.

### Also fixed: background `StartForegroundService` crash path

`MusicPlayerService.UpdateNotification()` used `Context.StartForegroundService` for what is only a notification repaint. On Android 12+ that throws `ForegroundServiceStartNotAllowedException` when called from the background with no running foreground service — exactly the state after playback stops, so a late pause/resume or a notification media-button press could crash the app (a screen-off, backgrounded-music scenario). It now uses `StartService`. It also returned `Sticky` unconditionally, so a system restart with a **null intent** reinstated a `mediaPlayback` FGS with a permanent "Loading…" notification and a wake lock but no playback; that path now releases the lock and stops itself.

### Verification (R5CWC356B2K, `--no-incremental`)

**Before:** 8 rapid track changes → `SIGABRT`, process dead.

**After:** driven via UI automation across **two artists and two albums** (ABBA / *Voyage* 2021 → AC-DC / *Live* 1992), **26+ track changes** producing **39 distinct audio session ids** (`257`…`641`, plus `0`).

| Check | Result |
| ----------------------------------- | ------------------------------------------------ |
| Process | **pid 14219 unchanged throughout** (never restarted) |
| Fatal signals / unhandled exceptions | **0** |
| Wake lock | **exactly 1**, acquired once at 02:27:10, held 4m59s |
| `wake lock acquired` / `released` | **1 / 0** — balanced, no leak |
| Foreground services | 1 (`mediaPlayback`), as intended |

The `0` among the session ids is the significant one: it is the cached session cleared during a switch — the exact state that used to dereference a released player.

⚠️ **Device-testing note:** when the Music tab is missing from the drawer, the module did not register — tap **Rescan Modules** (drawer footer) and it returns. It is not a display quirk.

## Resume checklist (tomorrow or next session)

1. `git status --short` — confirm the expected uncommitted files (listed below). Never delete untracked `.cs`.
2. Re-read this doc's "Verification state" and re-verify device stability (dataSync budget resets on a rolling ~24 h).
3. On-device E2E: grant photos access → take a **stock-camera** photo → confirm it lands in server `AutoUpload/YYYY/MM`; backfill → no duplicates; `pidof` stable with no MediaStore crash. (There is no Files-tab capture to test any more.)
4. Verify the quota-full notification (temporarily set a tiny quota via admin API on a test user, or accept the 9 unit tests as coverage).
5. ~~Decide + implement chat FGS type fix and the P5 headless-safe background sync.~~ — **both DONE 2026-09-12** (P5 adaptive job; chat FGS removed along with the `AndroidForegroundServicePolicy` kill-switch).
6. Update docs (`IMPLEMENTATION_CHECKLIST.md`, `MASTER_PROJECT_PLAN.md`) with targeted edits, then commit (per repo rules only after full verification).

## Files changed

Watcher / permission / quota work and P5 — **committed** `e77d20f6` (media stall fix) and `d92dff02` (P5 headless sync + adaptive cadence), pushed to `feature/android-media-auto-upload`.

Chat FGS removal (`feature/android-media-auto-upload`) — **committed** `faae32c9`:

- `src/Clients/DotNetCloud.Client.Android/Platforms/Android/ChatConnectionService.cs` — FGS attribute, promotion block, `OnTimeout`, `BuildNotification()` removed
- `src/Clients/DotNetCloud.Client.Android/Platforms/Android/AndroidManifest.xml` — `foregroundServiceType` dropped; `FOREGROUND_SERVICE_DATA_SYNC` permission removed
- `src/Clients/DotNetCloud.Client.Android/Platforms/Android/AndroidForegroundServicePolicy.cs` — **deleted** (kill-switch no longer needed)
- `src/Clients/DotNetCloud.Client.Android/Platforms/Android/MediaUploadForegroundService.cs` — **deleted** (dead code)
- `src/Clients/DotNetCloud.Client.Android/App.xaml.cs`, `Views/LoginPage.xaml.cs`, `Platforms/Android/MainActivity.cs` — call sites now `StartService` directly

Camera ownership removal + Debug interpreter fix (`feature/android-media-auto-upload`) — **pending commit**:

- `src/Clients/DotNetCloud.Client.Android/DotNetCloud.Client.Android.csproj` — `AndroidUseInterpreter` `true` → **`false`** in the Debug PropertyGroup (with a comment recording the crash signature and why)
- `src/Clients/DotNetCloud.Client.Android/ViewModels/FileBrowserViewModel.cs` — capture commands + all capture/spool helpers removed; `IsPending` and the pending-count status removed
- `src/Clients/DotNetCloud.Client.Android/Views/FileBrowserPage.xaml` — camera photo/video buttons removed, grid `Auto,*,Auto,Auto,Auto,Auto` → `Auto,*,Auto,Auto`; "Pending" badge removed
- `src/Clients/DotNetCloud.Client.Android/Platforms/Android/AndroidManifest.xml` — `CAMERA` + `ACCESS_FINE_LOCATION` removed
- `src/Clients/DotNetCloud.Client.Android/Services/MediaAutoUploadService.cs` — `UploadPendingFilesAsync`, `PendingUploadsDirName` and `ResolveUploadTargetFolderAsync` removed
- `src/Clients/DotNetCloud.Client.Android/Services/IMediaAutoUploadService.cs` — `ResolveUploadTargetFolderAsync` removed from the interface
- `src/Clients/DotNetCloud.Client.Android/Services/MediaUploadIndex.cs` — `MediaKey` doc no longer mentions spooled captures

Music crash fix (`feature/android-media-auto-upload`) — **pending commit**:

- `src/Clients/DotNetCloud.Client.Android/Services/MusicPlayerService.cs` — cached `_audioSessionId` (+ `TryReadPlayer` for `CurrentPosition`/`Duration`), publish-null-before-release, capture session in `OnTrackPrepared`, `RaisePlaybackStateChangedSafely` for the timer, `UpdateNotification` → `StartService`
- `src/Clients/DotNetCloud.Client.Android/Services/AndroidEqualizerService.cs` — `OnPlaybackStateChanged` try/catch + single defensive session read; `CreateEqualizer(int)` takes the session id
- `src/Clients/DotNetCloud.Client.Android/Platforms/Android/MusicPlaybackService.cs` — idempotent `AcquireWakeLock` + `ReleaseWakeLock`, wake lock only when a track is loaded, null-intent sticky-restart guard, release on `ActionStop`
