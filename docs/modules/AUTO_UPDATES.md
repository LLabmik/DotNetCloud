# Auto-Updates Module Documentation

> **Phase:** 11 | **Status:** In Progress (Phases A, B, D complete; Phase C pending)

---

## Architecture

The auto-update system uses GitHub Releases as the single source of truth. The DotNetCloud server acts as a caching proxy so clients avoid hitting the GitHub API directly.

```
GitHub Releases API (/repos/LLabmik/DotNetCloud/releases)
        │
        ▼
┌─────────────────────────────────────────────┐
│  DotNetCloud Core Server                    │
│  ┌────────────────────────────────────────┐ │
│  │ GitHubUpdateService                    │ │
│  │ - Fetches from GitHub Releases API     │ │
│  │ - MemoryCache (1-hour TTL)             │ │
│  │ - Semantic version comparison          │ │
│  │ - Platform asset matching              │ │
│  └────────────────────────────────────────┘ │
│  ┌────────────────────────────────────────┐ │
│  │ UpdateController (public, no auth)     │ │
│  │ GET /api/v1/core/updates/check         │ │
│  │ GET /api/v1/core/updates/releases      │ │
│  │ GET /api/v1/core/updates/releases/latest│ │
│  └────────────────────────────────────────┘ │
└─────────────────────────────────────────────┘
        │
        ▼
┌──────────────┬──────────────┬──────────────┐
│ CLI          │ SyncTray     │ Android      │
│ dotnetcloud  │ Background   │ Launch check │
│ update       │ 24h timer    │ Daily check  │
│ [--check]    │ + tray notif │ + banner UI  │
└──────────────┴──────────────┴──────────────┘
```

---

## Server-Side Components

### IUpdateService (`DotNetCloud.Core`)

**File:** `src/Core/DotNetCloud.Core/Services/IUpdateService.cs`

```csharp
public interface IUpdateService
{
    Task<UpdateCheckResult> CheckForUpdateAsync(string? currentVersion = null, CancellationToken ct = default);
    Task<ReleaseInfo?> GetLatestReleaseAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ReleaseInfo>> GetRecentReleasesAsync(int count = 5, CancellationToken ct = default);
}
```

### DTOs (`DotNetCloud.Core`)

**File:** `src/Core/DotNetCloud.Core/DTOs/UpdateDtos.cs`

| DTO | Key Properties |
|-----|---------------|
| `UpdateCheckResult` | `IsUpdateAvailable`, `CurrentVersion`, `LatestVersion`, `ReleaseUrl`, `ReleaseNotes`, `PublishedAt`, `Assets` |
| `ReleaseInfo` | `Version`, `TagName`, `ReleaseNotes`, `PublishedAt`, `IsPreRelease`, `Assets`, `ReleaseUrl` |
| `ReleaseAsset` | `Name`, `DownloadUrl`, `Size`, `ContentType`, `Platform` |

DTOs are shared between server and clients via the `DotNetCloud.Core` package reference.

### GitHubUpdateService (`DotNetCloud.Core.Server`)

**File:** `src/Core/DotNetCloud.Core.Server/Services/GitHubUpdateService.cs`

- Queries `https://api.github.com/repos/LLabmik/DotNetCloud/releases`
- `HttpClient` via `IHttpClientFactory` (named client: `"GitHubReleases"`)
- `MemoryCache` with 1-hour TTL (configurable)
- Version comparison: `IsNewerVersion()` handles semantic versioning with pre-release suffixes
- Platform detection: `InferPlatform()` parses asset filenames for `linux-x64`, `win-x64`, `android`, etc.
- Graceful fallback: returns stale cache or empty list if GitHub is unreachable
- Server version: reads from `AssemblyInformationalVersionAttribute` (strips `+sha` suffix)

**DI Registration:**
```csharp
// In SupervisorServiceExtensions.cs
services.AddSingleton<IUpdateService, GitHubUpdateService>();
```

### UpdateController (`DotNetCloud.Core.Server`)

**File:** `src/Core/DotNetCloud.Core.Server/Controllers/UpdateController.cs`

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/v1/core/updates/check` | GET | None | Check for updates (optional `?currentVersion=` query param) |
| `/api/v1/core/updates/releases` | GET | None | Recent releases (optional `?count=` param, clamped 1–20) |
| `/api/v1/core/updates/releases/latest` | GET | None | Latest release (404 if no releases) |

Response format follows the standard API envelope: `{ success: true, data: {...} }`

---

## CLI Integration

**File:** `src/CLI/DotNetCloud.CLI/Commands/MiscCommands.cs`

| Command | Behavior |
|---------|----------|
| `dotnetcloud update --check` | Queries GitHub API directly, prints version info, exits with code 0 (up to date) or 1 (update available) |
| `dotnetcloud update` | Same check + downloads platform tarball to temp directory + prints manual-apply instructions |

> Server self-apply is intentionally deferred — replacing running binaries requires systemd service restart orchestration.

---

## Admin UI

**File:** `src/UI/DotNetCloud.UI.Web.Client/Pages/Admin/Updates.razor`

- Route: `/admin/updates`
- Requires admin authorization (`RequireAdmin` policy)
- Displays: current version card, latest release card, status badge (green/amber), "Check Now" button
- Scoped CSS: `Updates.razor.css`

---

## Desktop Client (SyncTray)

### IClientUpdateService (`DotNetCloud.Client.Core`)

**File:** `src/Clients/DotNetCloud.Client.Core/Services/IClientUpdateService.cs`

```csharp
public interface IClientUpdateService
{
    Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct = default);
    Task<string> DownloadUpdateAsync(ReleaseAsset asset, IProgress<double>? progress = null, CancellationToken ct = default);
    Task ApplyUpdateAsync(string downloadPath, CancellationToken ct = default);
    event EventHandler<UpdateCheckResult>? UpdateAvailable;
}
```

### ClientUpdateService (`DotNetCloud.Client.Core`)

**File:** `src/Clients/DotNetCloud.Client.Core/Services/ClientUpdateService.cs`

- Primary: queries server `/api/v1/core/updates/check?currentVersion={localVer}`
- Fallback: direct GitHub API if server unreachable or no base address configured
- Streaming download with `IProgress<double>` reporting
- SHA256 checksum verification planned for release assets

### Background Service (`DotNetCloud.Client.SyncTray`)

**File:** `src/Clients/DotNetCloud.Client.SyncTray/Services/UpdateCheckBackgroundService.cs`

- 30-second initial delay, then 24-hour interval (configurable)
- Fires `UpdateAvailable` event → TrayViewModel shows notification
- Context menu: "Check for Updates…" item

### Update Dialog (`DotNetCloud.Client.SyncTray`)

**Files:**
- `src/Clients/DotNetCloud.Client.SyncTray/Views/UpdateDialog.axaml`
- `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/UpdateViewModel.cs`

Dark-themed 480×420 Avalonia window showing version cards, status badges, release notes, download progress bar, and action buttons.

---

## Android Client

**Status:** Phase C — Pending

Planned behavior:
- On app launch, check server endpoint (once per day max)
- Display dismissable update banner
- Link to Play Store, F-Droid, or direct APK download (no in-app APK install for security)
- "Don't remind me for this version" preference

---

## Testing

### Unit Tests

| Test Class | Location | Count | Coverage |
|-----------|----------|-------|----------|
| `GitHubUpdateServiceTests` | `tests/DotNetCloud.Core.Server.Tests/` | — | Mock HTTP, version comparison, caching, asset matching |
| `UpdateControllerTests` | `tests/DotNetCloud.Core.Server.Tests/` | — | Response format, edge cases |
| `ClientUpdateServiceTests` | `tests/DotNetCloud.Client.Core.Tests/` | 10 | Server check, fallback, download, events, errors |
| `UpdateCheckBackgroundServiceTests` | `tests/DotNetCloud.Client.SyncTray.Tests/` | 8 | Event firing, error resilience, lifecycle |

### Integration Tests

**File:** `tests/DotNetCloud.Integration.Tests/Api/UpdateEndpointTests.cs`

- Uses `DotNetCloudWebApplicationFactory` (in-memory database, mock dependencies)
- Tests all three public endpoints (`/check`, `/releases`, `/releases/latest`)
- Verifies standard API envelope format
- Tests version parameter, count clamping, and graceful degradation

---

## Configuration

| Scope | Setting | Default | Location |
|-------|---------|---------|----------|
| Server | Cache TTL | 1 hour | `GitHubUpdateService.DefaultCacheDuration` |
| SyncTray | Auto-check enabled | `true` | Local settings (persisted) |
| SyncTray | Check interval | 24 hours | `UpdateCheckBackgroundService` |
| Android | Check frequency | Once per day | App-level preference |

---

## Security Considerations

- **No auth on update endpoints:** Intentional — clients must check before login
- **GitHub rate limits:** 60 requests/hour for public API; 1-hour cache prevents exceeding
- **SHA256 verification:** Checksums published alongside each release in the CI pipeline
- **No Android APK sideloading:** Updates always through official app stores
- **Server self-apply deferred:** Too risky for automated execution; manual apply only
- **HTTPS only:** All GitHub API requests and download URLs use HTTPS

---

## Dependencies

| Component | Depends On |
|-----------|-----------|
| `GitHubUpdateService` | `IHttpClientFactory`, `IMemoryCache` |
| `UpdateController` | `IUpdateService` |
| `ClientUpdateService` | Server `/api/v1/core/updates/*` endpoints (fallback: GitHub API) |
| `UpdateCheckBackgroundService` | `IClientUpdateService` |
| `UpdateDialog` / `UpdateViewModel` | `IClientUpdateService` |
