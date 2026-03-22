# DotNetCloud Desktop Client

> **Last Updated:** 2026-03-10

---

## Overview

The DotNetCloud desktop client provides bidirectional file synchronization between a local folder and a DotNetCloud server. It consists of two components:

| Component | Project | Type | Purpose |
|---|---|---|---|
| **SyncService** | `DotNetCloud.Client.SyncService` | .NET Worker Service | Background sync engine (Windows Service / systemd) |
| **SyncTray** | `DotNetCloud.Client.SyncTray` | Avalonia Desktop App | Tray icon, settings, notifications |

Both components use the shared `DotNetCloud.Client.Core` library for sync logic, API communication, authentication, and local state management.

---

## Features

- **Bidirectional sync** — changes flow both ways automatically
- **Chunked transfer** — 4 MB chunks with SHA-256 deduplication
- **Delta sync** — only changed chunks are transferred
- **Conflict detection** — conflict copies preserve both versions
- **Selective sync** — choose which folders to sync
- **Single-account today** — one desktop client account per OS user install (multi-account support is not yet available)
- **Resume** — interrupted transfers resume automatically
- **Cross-platform** — Windows (Service + tray) and Linux (systemd + tray)

---

## Architecture

```
DotNetCloud.Client.Core           (shared library)
├── Api/                          API client + models
├── Auth/                         OAuth2 PKCE + token storage
├── Sync/                         SyncEngine, SyncContext, SyncStatus
├── Transfer/                     ChunkedTransferClient
├── Conflict/                     ConflictResolver
├── LocalState/                   SQLite state database
└── SelectiveSync/                Folder include/exclude configuration

DotNetCloud.Client.SyncService    (background service)
├── ContextManager/               Multi-context lifecycle management
├── Ipc/                          Named Pipe / Unix socket server
├── SyncWorker.cs                 BackgroundService entry point
└── Program.cs

DotNetCloud.Client.SyncTray       (Avalonia tray app)
├── Views/                        Settings window, Add Account dialog
├── ViewModels/                   MVVM view models
├── Notifications/                Windows balloon tip (current) / Linux libnotify
├── Ipc/                          IPC client connection
├── TrayIconManager.cs            Tray icon + context menu
├── App.axaml.cs
└── Program.cs
```

Current status note:
- The Windows desktop notification path currently uses `Shell_NotifyIcon` balloon notifications.
- Phase 2.9 chat follow-up work is planned to migrate Windows notifications to toast notifications and add grouped notifications plus quick reply support.

---

## How It Works

1. **SyncService** runs as a system-level background service
2. It manages one or more **SyncContexts** (each = one server account ↔ one local folder)
3. Each context runs a **SyncEngine** that uses:
   - `FileSystemWatcher` for instant local change detection
   - Periodic full scans as a safety net (default: every 5 minutes)
   - REST API calls for server-side change detection
4. **SyncTray** connects to SyncService via IPC to display status and accept user commands
5. **Conflicts** are resolved by creating conflict copies — both versions are preserved

---

## Tray Icon States

| State | Icon | Description |
|---|---|---|
| **Idle** | ✓ Green check | All files synced |
| **Syncing** | ↻ Spinner | Transfer in progress |
| **Paused** | ⏸ Yellow | Sync paused by user |
| **Error** | ⚠ Red | Sync error occurred |
| **Offline** | ○ Gray | Server unreachable |

---

## Getting Started

See [Setup Guide](SETUP.md) for installation and account configuration.

Desktop client installers are published as versioned GitHub Release assets:

- `dotnetcloud-desktop-client-linux-x64-<version>.tar.gz`
- `dotnetcloud-desktop-client-win-x64-<version>.zip`
- `dotnetcloud-sync-tray-win-x64-<version>.msix`

---

## Related Documentation

- [Setup & Installation](SETUP.md)
- [TraySync Build + Verification Walkthrough](TRAYSYNC_VERIFICATION_WALKTHROUGH.md)
- [Sync Protocol Details](SYNC_PROTOCOL.md)
- [Troubleshooting](TROUBLESHOOTING.md)
- [Files Module Sync Architecture](../../modules/files/SYNC.md)
