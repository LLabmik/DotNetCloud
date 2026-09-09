# Android Media Auto-Upload Redesign — Status & Resume Plan

> ⚠️ **STATUS: IN PROGRESS (2026-09-09) — code implemented, NOT committed.** Read this file + `/memories/repo/DotNetCloud.md` (`## 🚧 RESUME HERE` section) before doing anything in a new session.

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

## Verification state (2026-09-09)
- Build: `dotnet build src\Clients\DotNetCloud.Client.Android -f net10.0-android -c Debug -r android-arm64 /p:AndroidSdkDirectory="C:\Program Files (x86)\Android\android-sdk"` → clean, 0 warnings/errors.
- Tests: `dotnet test tests\DotNetCloud.Client.Android.Tests` → **283 passed / 1 skipped**.
- On-device (R5CWC356B2K, cloud.dotnetcloud.net): permission grant via Fix worked; watcher uploaded a large backfill in the foreground (progress notification 3001 counted to 40+); **no** media FGS; app stable; leftover JobScheduler test job purged.
- ⚠️ **Not fully verified / blocked today:** clean end-to-end after the Android 15/16 `dataSync` FGS budget reset; quota-full notification path (needs a tiny quota); background sync when the app is closed.

## Known issues / gotchas
- **`dataSync` FGS 24-h budget (Android 15/16):** rolling per-24 h window, persisted across reboots (reboot does NOT reset). When exhausted the platform throws `ForegroundServiceDidNotStopInTimeException` and can crash-loop via Sticky restarts. Today the **ChatConnectionService** (still `dataSync`, pre-existing) is the recurring crash source — separate fix needed (non-`dataSync` FGS type, e.g. `remoteMessaging` on API 34+, or stop Sticky restart when exhausted).
- **Background-sync experiment (WorkManager / JobScheduler) was reverted & purged for today:** `Xamarin.AndroidX.Work.Runtime 2.10.1` fails restore (transitive AndroidX conflicts with googleplay flavor); native `JobScheduler` `JobService` registered fine but **crashed on headless cold-start** (MAUI startup assumes an Activity). Revisit with a headless-safe worker (guard MAUI app build when no Activity / separate minimal host).
- C#/Android namespace gotcha: inside `namespace DotNetCloud.Client.Android.*`, bare `Android.Content.*`/`Android.Content.PM.Permission` binds to `DotNetCloud.Client.Android` → use `global::Android.*` or `using Android.Content;`.
- `IFileRestClient.ListChildrenAsync` param is `folderId`, not `parentId`.
- `PendingIntent`/`PendingIntentFlags` live in `Android.App` (fully qualify in the Services namespace).
- CA1416: wrap platform-guarded calls in `#pragma warning disable` with SDK-check comments (repo style).

## Not done (deferred, needs device/decisions)
- **P4 capture→gallery single path:** Files-tab camera should launch the system camera and land the photo in the shared gallery so ONE watcher path uploads it (spike: `MediaScannerConnection.ScanFile` vs no-`EXTRA_OUTPUT`). Current capture path still works (uploads to AutoUpload when enabled).
- **P5 background sync** (the "max files/day" ask): headless-safe periodic job (JobScheduler or compatible WorkManager) + larger batches on WiFi/charging.
- Chat FGS `dataSync` type change.
- Server `AutoUpload` may contain test screenshots `dnc_*.png` from this session — deletable.

## Resume checklist (tomorrow or next session)
1. `git status --short` — confirm the expected uncommitted files (listed below). Never delete untracked `.cs`.
2. Re-read this doc's "Verification state" and re-verify device stability (dataSync budget resets on a rolling ~24 h).
3. On-device E2E: grant photos access → take a stock-camera photo → confirm it lands in server `AutoUpload/YYYY/MM`; Files-tab capture → no double upload; backfill → no duplicates; `pidof` stable with no MediaStore crash.
4. Verify the quota-full notification (temporarily set a tiny quota via admin API on a test user, or accept the 9 unit tests as coverage).
5. Decide + implement chat FGS type fix and the P5 headless-safe background sync.
6. Update docs (`IMPLEMENTATION_CHECKLIST.md`, `MASTER_PROJECT_PLAN.md`) with targeted edits, then commit (per repo rules only after full verification).

## Files changed (uncommitted)
- `src/Clients/DotNetCloud.Client.Android/Services/IMediaPermissionService.cs` (new)
- `src/Clients/DotNetCloud.Client.Android/Platforms/Android/AndroidMediaPermissionService.cs` (new)
- `src/Clients/DotNetCloud.Client.Android/Platforms/Android/MediaLibraryReadPermission.cs` (new)
- `src/Clients/DotNetCloud.Client.Android/Services/MediaUploadIndex.cs` (new)
- `src/Clients/DotNetCloud.Client.Android/Services/QuotaGate.cs` (new)
- `src/Clients/DotNetCloud.Client.Android/Services/MediaAutoUploadService.cs` (rewritten)
- `src/Clients/DotNetCloud.Client.Android/ViewModels/SettingsViewModel.cs`
- `src/Clients/DotNetCloud.Client.Android/Views/SettingsPage.xaml` + `SettingsPage.xaml.cs`
- `src/Clients/DotNetCloud.Client.Android/ViewModels/FileBrowserViewModel.cs`
- `src/Clients/DotNetCloud.Client.Android/MauiProgram.cs`
- `src/Clients/DotNetCloud.Client.Android/App.xaml.cs`
- `src/Clients/DotNetCloud.Client.Android/Platforms/Android/AndroidManifest.xml`
- `tests/DotNetCloud.Client.Android.Tests/DotNetCloud.Client.Android.Tests.csproj`
- `tests/DotNetCloud.Client.Android.Tests/ViewModels/SettingsViewModelTests.cs`
- `tests/DotNetCloud.Client.Android.Tests/Services/QuotaGateTests.cs` (new)
