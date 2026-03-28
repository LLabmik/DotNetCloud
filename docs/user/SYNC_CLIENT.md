# DotNetCloud Files — Desktop Sync Client

> **Last Updated:** 2026-03-22

---

## What Is the Sync Client?

The DotNetCloud sync client keeps a folder on your computer automatically synchronized with your DotNetCloud server. Any changes you make locally appear on the server (and vice versa) within seconds.

**Key features:**
- Automatic bidirectional sync
- Works in the background — no manual action needed
- Tray icon shows sync status at a glance
- Supports multiple DotNetCloud accounts
- Choose which folders to sync (selective sync)

---

## Installation

### Windows (Recommended: MSIX Package)

1. Download the latest `dotnetcloud-sync-tray-win-x64-<version>.msix` from [GitHub Releases](https://github.com/LLabmik/DotNetCloud/releases)
2. Double-click the `.msix` file to launch the installer
3. Click **Install** — this installs the SyncTray app, which owns sync directly in the desktop process
4. After installation, the SyncTray icon appears in your system tray (bottom-right of your taskbar; click the `^` arrow if you don't see it)
5. SyncTray starts automatically on login

> **Note:** If the Install button is grayed out, your administrator may need to trust the signing certificate first. Contact your DotNetCloud server admin for the certificate file.

### Windows (Alternative: ZIP Bundle)

1. Download `dotnetcloud-desktop-client-win-x64-<version>.zip` from GitHub Releases
2. Extract the zip to a folder
3. Run `install.cmd` from an elevated Command Prompt to install desktop client files
4. Run SyncTray from the Start menu or the extracted folder

### Linux

1. Download `dotnetcloud-desktop-client-linux-x64-<version>.tar.gz` from GitHub Releases
2. Extract and install:

   ```bash
   tar -xzf dotnetcloud-desktop-client-linux-x64-<version>.tar.gz
   cd linux-x64
   sudo ./install.sh
   ```

3. Launch the tray app from your applications menu or run `dotnetcloud-sync-tray`

---

## Connecting Your Account

1. **Right-click the tray icon** → click **"Settings..."**
2. Click **"Add Account"**
3. Enter your DotNetCloud server URL:
   - Standard: `https://cloud.example.com`
   - With custom port: `https://cloud.example.com:5443/`
   - Include `https://` and port number if your server uses a non-standard port
4. Your web browser opens — log in with your DotNetCloud credentials
5. After login, the browser redirects back and the account is connected
6. Choose a local folder for syncing (defaults listed below)
7. Click **Save** — syncing begins immediately

> **Self-signed certificates:** If your server uses a self-signed certificate, you may need to install the certificate on your machine first. See the [Troubleshooting Guide](../clients/desktop/TROUBLESHOOTING.md#self-signed-certificate-warnings) for instructions.

### Default Sync Folder Locations

| Platform | Default Path | Example |
|---|---|---|
| Windows | `%USERPROFILE%\DotNetCloud\{server}` | `C:\Users\Ben\DotNetCloud\cloud.example.com` |
| Linux | `~/DotNetCloud/{server}` | `/home/ben/DotNetCloud/cloud.example.com` |

---

## Tray Icon Status

The tray icon shows the current sync status:

| Icon | Meaning |
|---|---|
| ✓ Green check | All files are synced |
| ↻ Spinning arrows | Sync in progress |
| ⏸ Yellow pause | Sync is paused |
| ⚠ Red exclamation | An error occurred |
| ○ Gray circle | Server unreachable (offline) |

Hover over the icon to see a summary (e.g., "3 files syncing, 8.2 GB used").

---

## Using the Sync Folder

Your sync folder works like any other folder on your computer:

- **Save a file** in the sync folder → it uploads to the server automatically
- **Delete a file** → it moves to the server's trash
- **Rename or move a file** → the change is reflected on the server
- **Files changed on the server** (by you on another device, or by someone who shared with you) → they download automatically

---

## Selective Sync

You don't have to sync everything. To choose which folders to sync:

1. Open **Settings** → **Sync** tab
2. Click **"Select Folders..."**
3. Uncheck folders you don't want on this computer
4. Click **Apply**

Unchecked folders remain on the server but are not downloaded locally.

---

## Pausing and Resuming

- **Pause:** Right-click the tray icon → **"Pause syncing"**
- **Resume:** Right-click the tray icon → **"Resume syncing"**

While paused, changes are queued and will sync when you resume.

---

## Sync Now

To force an immediate sync without waiting for automatic detection:

Right-click the tray icon → **"Sync now"**

---

## Conflict Handling

If you and someone else edit the same file at the same time, a **conflict** occurs. DotNetCloud handles this automatically:

1. Your local version is saved as a conflict copy:

   ```
   report (conflict - YourName - 2025-07-14).docx
   ```

2. The server version is downloaded to the original file name
3. You receive a notification about the conflict
4. Both versions are preserved — compare them and keep the one you want

---

## Multiple Accounts

You can connect to multiple DotNetCloud servers:

1. Open **Settings** → **Accounts** tab
2. Click **"Add Account"** for each additional server
3. Each account syncs to its own folder

---

## Bandwidth Limits

If sync is using too much bandwidth:

1. Open **Settings** → **General** tab
2. Set **Upload limit** (e.g., 5 MB/s)
3. Set **Download limit** (e.g., 10 MB/s)
4. Set either to `0` for unlimited

---

## Notifications

The sync client shows desktop notifications for:

- **Sync completed** — after a sync pass finishes
- **Conflict detected** — when a file conflict occurs
- **Error** — when sync encounters a problem
- **Quota warning** — when your storage is getting full

Configure which notifications you receive in **Settings** → **General** → **Notifications**.

---

## Removing an Account

1. Open **Settings** → **Accounts** tab
2. Select the account
3. Click **"Remove Account"**
4. Choose whether to **keep** or **delete** the local sync folder

---

## Troubleshooting

### Files Not Syncing

1. Check the tray icon — is sync paused or showing an error?
2. Right-click → **"Sync now"** to force a sync
3. Check if the file is in a folder excluded by selective sync
4. Check your internet connection

### Tray Icon Shows Error

1. Hover over the icon to see the error message
2. Common causes: server unreachable, authentication expired, disk full
3. If authentication expired: remove and re-add the account

### Slow Sync

1. Check bandwidth limit settings
2. Large files sync via 4 MB chunks — this is normal for big files
3. Many small files may take longer due to per-file overhead

For more details, see the [Troubleshooting Guide](../clients/desktop/TROUBLESHOOTING.md).

---

## Updating the Client

### Windows (MSIX)

1. Download the new `.msix` file from GitHub Releases
2. Double-click to install — it upgrades the existing installation in place
3. Sync resumes automatically with the new version

### Linux

1. Download the new `.tar.gz` and re-run `sudo ./install.sh`
2. The installer replaces binaries and launcher integration

---

## Uninstalling

### Windows (MSIX)

1. Open **Settings** → **Apps** → **Installed apps**
2. Search for **DotNetCloud SyncTray**
3. Click the three-dot menu → **Uninstall**

Or via PowerShell:

```powershell
Get-AppxPackage -Name "DotNetCloud.SyncTray" | Remove-AppxPackage
```

### Windows (ZIP install)

Run from an elevated Command Prompt:

```cmd
uninstall.cmd
```

### Linux

```bash
sudo ./uninstall.sh
```

> **Your files are safe:** Uninstalling removes the tray app but does **not** delete your local sync folder or any files on the server.

---

## Data & Log Locations

| Item | Windows | Linux |
|---|---|---|
| Sync folder | `%USERPROFILE%\DotNetCloud\{server}` | `~/DotNetCloud/{server}` |
| Sync state database | `%ProgramData%\DotNetCloud\Sync\{contextId}\state.db` | `/var/lib/dotnetcloud/sync/{contextId}/state.db` |
| Sync engine logs | `%ProgramData%\DotNetCloud\Sync\logs\` | `/var/log/dotnetcloud/` |
| Tray app logs | `%LOCALAPPDATA%\DotNetCloud\logs\` | `~/.local/share/dotnetcloud/logs/` |

---

## Next Steps

- [Getting Started with Files](GETTING_STARTED.md) — web UI basics
- [Online Document Editing](DOCUMENT_EDITING.md) — edit documents in the browser
