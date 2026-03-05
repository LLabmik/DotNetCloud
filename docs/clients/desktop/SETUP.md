# Desktop Client — Setup & Installation

> **Last Updated:** 2026-03-03

---

## Prerequisites

- **.NET 10 Runtime** — required to run the sync client
- **DotNetCloud server** — a running DotNetCloud instance to connect to
- **Windows 10+** or **Linux** (Debian/Ubuntu recommended)

---

## Installation

### Windows

1. **Build from source:**

   ```powershell
   dotnet publish src\Clients\DotNetCloud.Client.SyncService -c Release -o publish\SyncService
   dotnet publish src\Clients\DotNetCloud.Client.SyncTray -c Release -o publish\SyncTray
   ```

2. **Install SyncService as a Windows Service:**

   ```powershell
   sc.exe create DotNetCloudSync binPath="C:\path\to\publish\SyncService\DotNetCloud.Client.SyncService.exe"
   sc.exe start DotNetCloudSync
   ```

3. **Run SyncTray:** Launch `DotNetCloud.Client.SyncTray.exe` from the publish directory. It runs as a tray icon application.

4. **Auto-start SyncTray on login:** Place a shortcut in `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup`.

### Linux

1. **Build from source:**

   ```bash
   dotnet publish src/Clients/DotNetCloud.Client.SyncService -c Release -o publish/SyncService
   dotnet publish src/Clients/DotNetCloud.Client.SyncTray -c Release -o publish/SyncTray
   ```

2. **Install SyncService as a systemd unit:**

   Create `/etc/systemd/system/dotnetcloud-sync.service`:

   ```ini
   [Unit]
   Description=DotNetCloud Sync Service
   After=network.target

   [Service]
   Type=notify
   ExecStart=/path/to/publish/SyncService/DotNetCloud.Client.SyncService
   Restart=on-failure
   RestartSec=10

   [Install]
   WantedBy=multi-user.target
   ```

   ```bash
   sudo systemctl enable dotnetcloud-sync
   sudo systemctl start dotnetcloud-sync
   ```

3. **Run SyncTray:** Launch the tray app from the publish directory. Configure your desktop environment to auto-start it on login.

---

## Account Setup

### Adding Your First Account

1. **Start SyncTray** — it will display a tray icon
2. **Right-click the tray icon** → click **"Settings..."**
3. In the Settings window, click **"Add Account"**
4. Enter your DotNetCloud server URL (e.g., `https://cloud.example.com`)
5. Your default web browser will open for authentication
6. Log in with your DotNetCloud credentials
7. After successful login, the browser will redirect back and the account will be added
8. Choose a local folder for syncing (default: `~/DotNetCloud/{server-name}`)
9. Click **"Save"** — syncing will begin automatically

### Managing Multiple Accounts

You can connect to multiple DotNetCloud servers:

1. Open **Settings** → **Accounts** tab
2. Click **"Add Account"** for each additional server
3. Each account gets its own sync folder, state database, and authentication tokens
4. Switch the default account by clicking **"Set Default"**

### Removing an Account

1. Open **Settings** → **Accounts** tab
2. Select the account to remove
3. Click **"Remove Account"**
4. Choose whether to keep or delete the local sync folder

---

## Selective Sync

Not all folders need to be synced. To configure selective sync:

1. Open **Settings** → **Sync** tab
2. Click **"Select Folders..."**
3. A folder tree shows all server-side folders with checkboxes
4. Uncheck folders you do not want synced locally
5. Click **"Apply"**

Excluded folders remain on the server but are not downloaded or monitored locally.

---

## General Settings

| Setting | Description | Default |
|---|---|---|
| **Start on login** | Auto-start SyncTray when you log in | Enabled |
| **Full scan interval** | How often to do a complete sync check | 5 minutes |
| **Upload bandwidth limit** | Max upload speed (0 = unlimited) | 0 |
| **Download bandwidth limit** | Max download speed (0 = unlimited) | 0 |
| **Notification preferences** | Which notifications to show | All enabled |

---

## Sync Folder Location

By default, each account syncs to a folder under your home directory:

| Platform | Default Path |
|---|---|
| Windows | `%USERPROFILE%\DotNetCloud\{server-name}` |
| Linux | `~/DotNetCloud/{server-name}` |

You can change the sync folder in **Settings** → **Sync** tab → **"Change Folder..."**.

---

## Verifying Sync Status

- **Tray icon** shows overall status (idle, syncing, error, offline)
- **Tooltip** shows summary (e.g., "3 files syncing, 8.2 GB used")
- **Context menu** → **"Sync now"** forces an immediate sync pass
- **Settings** → **Accounts** shows per-account sync status

---

## Next Steps

- [Troubleshooting](TROUBLESHOOTING.md) — common issues and fixes
- [Sync Protocol Details](SYNC_PROTOCOL.md) — how sync works under the hood
