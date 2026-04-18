# Auto-Updates

> **Last Updated:** 2026-04-15

---

## Overview

DotNetCloud can automatically check for new versions across all surfaces — server, CLI, admin panel, desktop client, and Android app. Updates are sourced from [GitHub Releases](https://github.com/LLabmik/DotNetCloud/releases).

---

## Server & CLI

### Checking for Updates via CLI

```bash
# Check if an update is available (exit code: 0 = up to date, 1 = update available)
dotnetcloud update --check

# Check and download the latest release tarball
dotnetcloud update
```

The CLI displays:
- Current server version
- Latest available version
- Release notes summary
- Download URL for your platform

> **Note:** Server self-update is download-only for safety. After downloading, follow the printed instructions to apply the update manually (extract + restart the service).

### Admin Panel

Navigate to **Admin → Updates** (`/admin/updates`) to see:

- **Current version** — the running server version and .NET runtime
- **Latest release** — version, release date, and changelog
- **Status badge** — green "Up to Date" or amber "Update Available"
- **Check Now** button — manually trigger an update check

### Server-Side Settings

| Setting | Default | Description |
|---------|---------|-------------|
| Cache duration | 1 hour | How long GitHub release data is cached before re-fetching |

The server caches GitHub API responses to stay within the public API rate limit (60 requests/hour). Under normal operation, the cache is more than sufficient.

---

## Desktop Client (SyncTray)

### Automatic Background Checks

The SyncTray desktop client checks for updates automatically:

- **Initial delay:** 30 seconds after startup
- **Interval:** Every 24 hours (configurable)
- When an update is found, a system notification appears and the tray context menu shows "Update Available"

### Update Dialog

Click the update notification or select **Check for Updates…** from the tray menu to open the Update Dialog, which shows:

- Current and new version numbers
- Release notes
- Download progress bar
- **Update Now** / **Later** buttons

After downloading, select **Restart to Apply** to complete the update.

### Settings

In **SyncTray Settings → Updates**:

| Setting | Default | Description |
|---------|---------|-------------|
| Auto-check for updates | Enabled | Toggle automatic background update checks |
| Current version | — | Displays the running client version |

---

## Android App

On launch, the Android app checks the server for available updates (once per day maximum).

If an update is available:
- A dismissable banner appears at the top of the app: _"Version X.Y.Z is available"_
- Tap **Update** to open the appropriate store listing (Google Play or F-Droid)
- Tap **Dismiss** to hide the banner for this session

> **Security:** The Android app does not download or install APKs directly. Updates are always obtained through your app store for safety.

---

## How It Works

```
GitHub Releases API (source of truth)
        ↓
  DotNetCloud Server (caches responses, exposes REST API)
   ├── CLI: dotnetcloud update [--check]
   ├── Admin UI: /admin/updates
   └── GET /api/v1/core/updates/check
        ↓
  Clients query server endpoint
   ├── SyncTray: background check → notification → download → self-update
   └── Android: launch check → banner → store link
```

- The server proxies GitHub Releases so clients don't hit GitHub directly
- All version comparisons use semantic versioning (pre-release versions sort lower)
- Downloaded assets are verified via SHA256 checksums published alongside each release

---

## API Endpoints

All update endpoints are **public** (no authentication required) so clients can check before logging in.

| Endpoint | Description |
|----------|-------------|
| `GET /api/v1/core/updates/check` | Check if a newer version is available |
| `GET /api/v1/core/updates/check?currentVersion=0.1.5` | Check against a specific version |
| `GET /api/v1/core/updates/releases` | List recent releases (default: 5, max: 20) |
| `GET /api/v1/core/updates/releases/latest` | Get the latest release details |

### Response Format

```json
{
  "success": true,
  "data": {
    "isUpdateAvailable": true,
    "currentVersion": "0.1.7-alpha",
    "latestVersion": "0.2.0",
    "releaseUrl": "https://github.com/LLabmik/DotNetCloud/releases/tag/v0.2.0",
    "releaseNotes": "## What's New\n- Feature X\n- Bug fix Y",
    "publishedAt": "2026-04-10T12:00:00Z",
    "assets": [
      {
        "name": "dotnetcloud-0.2.0-linux-x64.tar.gz",
        "downloadUrl": "https://github.com/.../dotnetcloud-0.2.0-linux-x64.tar.gz",
        "size": 52428800,
        "contentType": "application/gzip",
        "platform": "linux-x64"
      }
    ]
  }
}
```

---

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Check failed" in CLI or Admin UI | Server can't reach GitHub API | Check network/firewall; cached data is used as fallback |
| SyncTray never shows update notification | Auto-check disabled or server unreachable | Check Settings → Updates; verify server URL is correct |
| Android banner not appearing | Already dismissed for this version, or < 24h since last check | Restart the app or wait for the next daily check |
| Version shows "0.0.0" | Assembly version metadata missing | Ensure you're running an official release build |
