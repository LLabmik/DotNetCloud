# Phase 11 — Auto-Updates Implementation Plan

## TL;DR

Implement auto-update functionality across all DotNetCloud surfaces: server CLI, admin panel, desktop client (SyncTray), and Android client. GitHub Releases API is the single source of truth for version info. The server acts as a proxy/cache so clients don't hit GitHub directly.

---

## Architecture Overview

```
GitHub Releases API (source of truth)
        ↓
  Core Server (caches, proxies, exposes API)
   ├── CLI: `dotnetcloud update [--check]`
   ├── Admin UI: Updates.razor page
   └── /api/v1/core/updates/check endpoint
        ↓
  Clients query server endpoint
   ├── SyncTray (Avalonia): background check → download → self-update
   └── Android (MAUI): notification → Play Store / APK link
```

---

## Phase A: Core Update Infrastructure (Server-Side)

### Step 11.1 — IUpdateService Interface & DTOs

**Files to create/modify:**
- `src/Core/DotNetCloud.Core/Services/IUpdateService.cs` — new interface
- `src/Core/DotNetCloud.Core/DTOs/UpdateDtos.cs` — new DTOs

**Interface design:**
```csharp
IUpdateService
  - Task<UpdateCheckResult> CheckForUpdateAsync(string? currentVersion = null, CancellationToken ct)
  - Task<ReleaseInfo?> GetLatestReleaseAsync(CancellationToken ct)
  - Task<IReadOnlyList<ReleaseInfo>> GetRecentReleasesAsync(int count = 5, CancellationToken ct)
```

**DTOs:**
- `UpdateCheckResult` — `IsUpdateAvailable`, `CurrentVersion`, `LatestVersion`, `ReleaseUrl`, `ReleaseNotes`, `PublishedAt`, `Assets` (list of download links per platform)
- `ReleaseInfo` — `Version`, `TagName`, `ReleaseNotes`, `PublishedAt`, `IsPreRelease`, `Assets`
- `ReleaseAsset` — `Name`, `DownloadUrl`, `Size`, `ContentType`, `Platform` (linux-x64, win-x64, android)

**Patterns to follow:**
- Same style as `IAdminSettingsService` / `IAdminModuleService` in `src/Core/DotNetCloud.Core/Services/`
- DTOs follow `SettingsDtos.cs` record style

---

### Step 11.2 — GitHubUpdateService Implementation

**Files to create/modify:**
- `src/Core/DotNetCloud.Core.Server/Services/GitHubUpdateService.cs` — new implementation
- `src/Core/DotNetCloud.Core.Server/Extensions/SupervisorServiceExtensions.cs` — register service

**Design:**
- Queries `https://api.github.com/repos/LLabmik/DotNetCloud/releases` (public, no auth needed)
- **Caching:** `MemoryCache` with 1-hour TTL (configurable via admin settings)
- **Version comparison:** Use `System.Version` or NuGet `SemanticVersion` for proper semver compare
- Current version from `Assembly.GetExecutingAssembly().GetInformationalVersion()`
- Platform asset matching: parse asset filename patterns (e.g., `dotnetcloud-0.1.7-linux-x64.tar.gz`)
- Graceful failure: if GitHub unreachable, return cached or "unknown" state
- Rate limit awareness: GitHub public API = 60 req/hr; cache prevents exceeding
- HttpClient via `IHttpClientFactory` (already used in the project)

**Registration pattern (follow existing):**
```csharp
services.AddSingleton<IUpdateService, GitHubUpdateService>();
```

---

### Step 11.3 — Update Check API Endpoint

**Files to create/modify:**
- `src/Core/DotNetCloud.Core.Server/Controllers/UpdateController.cs` — new controller

**Endpoints:**
- `GET /api/v1/core/updates/check` — Public (no auth), returns `UpdateCheckResult`
  - Query param: `?currentVersion=0.1.5` (optional, defaults to server version)
  - Clients use this to check for their own updates
- `GET /api/v1/core/updates/releases` — Public (no auth), returns recent releases list
- `GET /api/v1/core/updates/releases/latest` — Public (no auth), returns latest release

**Why public:** Self-hosted clients need to check without being authenticated (e.g., before login, or SyncTray tray icon check).

**Follow pattern of:** `AdminController.cs` for response format (`{ success: true, data: {...} }`)

---

### Step 11.4 — CLI `dotnetcloud update` Implementation

**Files to modify:**
- `src/CLI/DotNetCloud.CLI/Commands/MiscCommands.cs` — replace stub

**Behavior:**
- `dotnetcloud update --check` → Calls `IUpdateService.CheckForUpdateAsync()`, prints result
  - Shows: current version, latest version, changelog summary, download URL
  - Exit code: 0 = up to date, 1 = update available
- `dotnetcloud update` (without --check) → Same check + prompts to download
  - Downloads tarball to temp dir
  - Prints instructions for manual apply (not auto-applying server updates for safety)
  - Future: `--apply` flag for automatic server self-update (deferred, risky)

**Note:** Server self-update is inherently dangerous (replacing running binaries). Initial implementation will only CHECK and DOWNLOAD. Actual apply would need systemd service restart orchestration — defer to a later iteration.

---

### Step 11.5 — Admin UI Updates Page

**Files to create/modify:**
- `src/UI/DotNetCloud.UI.Web.Client/Pages/Admin/Updates.razor` — new page
- `src/UI/DotNetCloud.UI.Web.Client/Pages/Admin/Updates.razor.css` — new styles

**UI Design:**
- Route: `/admin/updates`
- **Current Version card:** Shows running server version, build date, .NET version
- **Latest Release card:** Version, release date, changelog (rendered markdown), download links per platform
- **Update History:** List of recent releases with expandable changelogs
- **Settings section:** Check frequency (admin setting: `core:update_check_interval`), auto-check enable/disable
- **Status indicator:** "Up to date" (green) / "Update available" (amber) badge

**Follow pattern of:** `SearchAdmin.razor`, `BackupSettings.razor` in `src/UI/DotNetCloud.UI.Web.Client/Pages/Admin/`

**Navigation:** Add to admin sidebar (check how other admin pages are linked)

---

### Step 11.6 — Unit Tests (Server-Side)

**Files to create:**
- `tests/DotNetCloud.Core.Server.Tests/Services/GitHubUpdateServiceTests.cs`
- `tests/DotNetCloud.Core.Server.Tests/Controllers/UpdateControllerTests.cs`

**Test coverage:**
- Mock GitHub API responses (use `MockHttpMessageHandler`)
- Version comparison logic (newer, same, older, pre-release)
- Cache behavior (TTL, stale on failure)
- Asset platform matching
- Controller response format
- Edge cases: GitHub down, empty releases, malformed response

---

## Phase B: Desktop Client Auto-Update (SyncTray)

### Step 11.7 — IClientUpdateService Interface

**Files to create:**
- `src/Clients/DotNetCloud.Client.Core/Services/IClientUpdateService.cs` — new interface

**Interface:**
```csharp
IClientUpdateService
  - Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct)
  - Task<string> DownloadUpdateAsync(ReleaseAsset asset, IProgress<double>? progress, CancellationToken ct)
  - Task ApplyUpdateAsync(string downloadPath, CancellationToken ct)
  - event EventHandler<UpdateCheckResult>? UpdateAvailable
```

**Reuse `UpdateCheckResult` and `ReleaseAsset` DTOs from `DotNetCloud.Core`** (shared via the Core package reference that clients already have).

---

### Step 11.8 — ClientUpdateService Implementation

**Files to create:**
- `src/Clients/DotNetCloud.Client.Core/Services/ClientUpdateService.cs`

**Design:**
- Checks server `/api/v1/core/updates/check?currentVersion={localVer}`
- Fallback: direct GitHub API check if server unreachable
- Downloads platform-appropriate asset to temp directory
- Progress reporting via `IProgress<double>`
- **Linux apply:** Extract tarball → replace files in install dir → signal systemd restart
- **Windows apply:** Extract zip → launch updater helper (`dotnetcloud-updater.exe`) that:
  1. Waits for main process to exit
  2. Copies new files over old
  3. Launches new version
- Verify download integrity via SHA256 checksum (checksums are published alongside releases)

---

### Step 11.9 — Background Update Checker (SyncTray)

**Files to modify:**
- `src/Clients/DotNetCloud.Client.SyncTray/App.axaml.cs` — register service + hosted timer
- `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/TrayViewModel.cs` — update badge/notification

**Files to create:**
- `src/Clients/DotNetCloud.Client.SyncTray/Services/UpdateCheckBackgroundService.cs`

**Behavior:**
- Periodic timer (default: every 24 hours, configurable in settings)
- On update found: fire `UpdateAvailable` event → TrayViewModel shows notification
- Tray icon badge or system notification: "DotNetCloud v0.2.0 available"
- User clicks → opens update dialog

---

### Step 11.10 — SyncTray Update UI

**Files to create:**
- `src/Clients/DotNetCloud.Client.SyncTray/Views/UpdateDialog.axaml` — Avalonia dialog
- `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/UpdateViewModel.cs`

**Modify:**
- `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/SettingsViewModel.cs` — add update settings tab

**UI:**
- Update dialog shows: current version, new version, changelog, download progress bar, "Update Now" / "Later" buttons
- Settings tab: check frequency, auto-download toggle, notification preference
- During download: progress bar with cancel option
- After download: "Restart to apply" button

---

### Step 11.11 — Desktop Client Update Tests

**Files to create:**
- `tests/DotNetCloud.Client.Core.Tests/Services/ClientUpdateServiceTests.cs`
- `tests/DotNetCloud.Client.SyncTray.Tests/Services/UpdateCheckBackgroundServiceTests.cs`

---

## Phase C: Android Client Update Notification

### Step 11.12 — Android Update Check Service

**Files to create:**
- `src/Clients/DotNetCloud.Client.Android/Services/AndroidUpdateService.cs` (or in Client.Core if shared)

**Design:**
- On app launch, check server `/api/v1/core/updates/check?currentVersion={appVer}&platform=android`
- If update available:
  - **Google Play build:** Show in-app banner → "Update available" → opens Play Store listing
  - **F-Droid build:** Show in-app banner → opens F-Droid listing or direct APK download URL
- Respects "don't remind me for this version" user preference
- Check frequency: once per day max

---

### Step 11.13 — Android Update UI

**Files to create/modify:**
- `src/UI/DotNetCloud.UI.Android/` — update banner component (or in shared UI)

**Minimal UI:** A dismissable banner at top of app: "Version X.Y.Z is available. [Update] [Dismiss]"

---

### Step 11.14 — Android Update Tests

**Files to create:**
- `tests/DotNetCloud.Client.Android.Tests/Services/AndroidUpdateServiceTests.cs`

---

## Phase D: Documentation & Integration

### Step 11.15 — Documentation

- Update `docs/architecture/ARCHITECTURE.md` Phase 8 section to note auto-updates are now Phase 11
- Update `docs/IMPLEMENTATION_CHECKLIST.md` with Phase 11 checklist items
- Update `docs/MASTER_PROJECT_PLAN.md` with Phase 11 steps and status table
- Update `README.md` roadmap table to add Phase 11
- Add user-facing docs for update configuration

### Step 11.16 — Integration Testing

- End-to-end test: mock GitHub releases → server caches → client checks → notification flow
- Verify backward compatibility: old clients hitting new endpoint, new clients hitting old server (graceful degradation)

---

## Step Dependencies

```
11.1 (interfaces/DTOs) ← blocks everything
11.2 (service impl) ← depends on 11.1
11.3 (API endpoint) ← depends on 11.2
11.4 (CLI) ← depends on 11.2
11.5 (Admin UI) ← depends on 11.3
11.6 (server tests) ← depends on 11.2, 11.3

11.7 (client interface) ← depends on 11.1 (shared DTOs)
11.8 (client impl) ← depends on 11.3, 11.7
11.9 (background checker) ← depends on 11.8
11.10 (SyncTray UI) ← depends on 11.9
11.11 (desktop tests) ← depends on 11.8, 11.9

11.12 (Android service) ← depends on 11.3, 11.7
11.13 (Android UI) ← depends on 11.12
11.14 (Android tests) ← depends on 11.12

11.15 (docs) ← parallel with any step
11.16 (integration tests) ← depends on 11.3, 11.8, 11.12
```

**Parallelism opportunities:**
- 11.4 (CLI) and 11.5 (Admin UI) can run in parallel after 11.2/11.3
- 11.7–11.11 (Desktop) and 11.12–11.14 (Android) can run in parallel after 11.3
- 11.15 (docs) can start immediately

---

## Relevant Files (Existing — Modify or Reference)

| File | Purpose |
|------|---------|
| `src/Core/DotNetCloud.Core/Services/IAdminSettingsService.cs` | Interface pattern to follow |
| `src/Core/DotNetCloud.Core/DTOs/SettingsDtos.cs` | DTO record pattern to follow |
| `src/Core/DotNetCloud.Core.Server/Controllers/AdminController.cs` | Controller pattern, response format |
| `src/Core/DotNetCloud.Core.Server/Extensions/SupervisorServiceExtensions.cs` | Service registration |
| `src/CLI/DotNetCloud.CLI/Commands/MiscCommands.cs` | Existing `update` stub to replace |
| `src/Clients/DotNetCloud.Client.SyncTray/App.axaml.cs` | Service registration for desktop |
| `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/SettingsViewModel.cs` | Add update settings |
| `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/TrayViewModel.cs` | Update notification |
| `src/UI/DotNetCloud.UI.Web.Client/Pages/Admin/SearchAdmin.razor` | Admin page pattern |
| `Directory.Build.props` | Version source |
| `.github/workflows/release.yml` | Release pipeline (SHA256 checksums already generated) |
| `tools/packaging/build-desktop-client-bundles.ps1` | Packaging reference |

---

## Verification

1. **Unit tests:** `dotnet test` — all new test classes pass
2. **Manual CLI test:** `dotnetcloud update --check` returns correct version info against real GitHub API
3. **Manual Admin UI test:** Navigate to `/admin/updates`, verify version display and release info
4. **Manual SyncTray test:** Launch SyncTray → verify background check fires → shows notification if update available
5. **Mock test:** GitHub API mock returns newer version → all surfaces show "update available"
6. **Edge case:** GitHub unreachable → graceful degradation (cached or "check failed" message)
7. **Build verification:** `dotnet build` succeeds for all modified projects

---

## Decisions & Scope

| Decision | Rationale |
|----------|-----------|
| **Included:** Update checking, notification, download for all surfaces. Desktop self-update mechanism. | Core auto-update experience |
| **Excluded (deferred):** Server self-update (`dotnetcloud update --apply`) | Too risky for v1; requires careful systemd orchestration. CLI will download only, user applies manually. |
| **GitHub Releases API** (public, no auth) | 60 req/hr limit mitigated by server-side 1-hour cache. No auth token management needed. |
| **No custom update server** | GitHub Releases is sufficient; no need to host our own update manifest. |
| **SHA256 verification** | Already generated by release pipeline; will be checked on download. |
| **Android: no in-app APK install** | Security risk on modern Android. Links to Play Store / F-Droid / website only. |
| **Public update endpoints** (no auth) | Clients need pre-login access (e.g., SyncTray tray check, first-run experience). |
