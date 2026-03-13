# Desktop Client — Setup & Installation

> **Last Updated:** 2026-03-03

---

## Prerequisites

- **DotNetCloud server** — a running DotNetCloud instance to connect to
- **Windows 10+** or **Linux** (Debian/Ubuntu recommended)

> End users should install from release installers. No .NET SDK is required on client machines.

> Required model: one desktop-client installation and account setup per OS user account.
> On shared machines, each OS user must install/run the client from their own login session.
> Shared-machine hard isolation across multiple concurrently active OS users is planned but not yet enforced in the service IPC/context layer.

---

## Installation

### Windows

1. **Recommended (MSIX):** Download and install

   - `dotnetcloud-sync-tray-win-x64-<version>.msix`

   This installs both the SyncTray desktop app and registers the `DotNetCloudSync` Windows Service automatically.

2. **Alternative (ZIP + installer scripts):** Download installer bundle from GitHub Releases:

   - `dotnetcloud-desktop-client-win-x64-<version>.zip`

3. **Extract** the zip to a folder.

4. **Install** from an elevated Command Prompt (`cmd.exe`):

   ```cmd
   install.cmd
   ```

   PowerShell fallback (if script execution is allowed):

   ```powershell
   .\Install-DesktopClient.ps1
   ```

5. **Verify service is running:**

   ```powershell
   Get-Service DotNetCloudSync
   ```

6. **Run SyncTray:**

   ```powershell
   & "$env:ProgramFiles\DotNetCloud\DesktopClient\SyncTray\dotnetcloud-sync-tray.exe"
   ```

7. **Auto-start SyncTray on login:** Installer registers a per-user startup entry.


### Linux

1. **Download installer bundle** from GitHub Releases:

   - `dotnetcloud-desktop-client-linux-x64-<version>.tar.gz`

2. **Extract** the archive:

   ```bash
   tar -xzf dotnetcloud-desktop-client-linux-x64-<version>.tar.gz
   cd linux-x64
   ```

3. **Install service + launcher:**

   ```bash
   sudo ./install.sh
   ```

4. **Verify service is running:**

   ```bash
   sudo systemctl status dotnetcloud-sync --no-pager
   ```

5. **Run SyncTray** in your desktop session:

   ```bash
   dotnetcloud-sync-tray
   ```

6. **Auto-start SyncTray on login:** Configure your desktop environment startup applications if desired.

## Updating to a New Client Version

### Windows

1. Download the new `dotnetcloud-desktop-client-win-x64-<version>.zip`.
2. Extract and run `Install-DesktopClient.ps1` again as Administrator.
3. The installer updates binaries and service configuration in place, then restarts `DotNetCloudSync`.

### Linux

1. Download the new `dotnetcloud-desktop-client-linux-x64-<version>.tar.gz`.
2. Extract and run `sudo ./install.sh` again.
3. The installer stops `dotnetcloud-sync`, replaces binaries, and restarts the service.

## Multi-User Machine Guidance (Current)

- Install and run the desktop client under the specific OS account that owns the sync data.
- Example: if `alice` and `bob` both use the same PC, `alice` installs/configures while logged in as `alice`, and `bob` repeats installer + setup while logged in as `bob`.
- Do not treat a single system-level install as safely isolated for multiple concurrently active desktop users yet.
- If multiple people share a machine today, use separate OS accounts and validate one account at a time during testing.

## Build From Source (Maintainers/Developers)

If you are building installers yourself (CI or local packaging host), use:

```powershell
.\tools\packaging\build-desktop-client-bundles.ps1 -Version "<version>" -Configuration "Release"

# Optional: Build zip/tar bundles and MSIX together (Windows host)
.\tools\packaging\build-desktop-client-bundles.ps1 -Version "<version>" -Configuration "Release" -BuildMsix

# Optional: Build SyncTray MSIX (Windows only)
.\tools\packaging\build-desktop-client-msix.ps1 -Version "<version>" -Configuration "Release"
```

Linux/macOS developers can run the bash wrapper:

```bash
./tools/packaging/build-desktop-client-bundles.sh <version> Release ./artifacts/installers
```

Example:

```bash
./tools/packaging/build-desktop-client-bundles.sh 0.1.0-alpha Release ./artifacts/installers
```

Output artifacts:

- `artifacts/installers/dotnetcloud-desktop-client-linux-x64-<version>.tar.gz`
- `artifacts/installers/dotnetcloud-desktop-client-win-x64-<version>.zip`
- `artifacts/installers/dotnetcloud-sync-tray-win-x64-<version>.msix`
- `artifacts/installers/dotnetcloud-desktop-client-linux-x64-<version>.tar.gz.sha256`
- `artifacts/installers/dotnetcloud-desktop-client-win-x64-<version>.zip.sha256`
- `artifacts/installers/dotnetcloud-sync-tray-win-x64-<version>.msix.sha256`

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
