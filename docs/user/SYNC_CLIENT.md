# DotNetCloud Files — Desktop Sync Client

> **Last Updated:** 2026-03-03

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

### Windows

1. Download the latest release from your DotNetCloud server or build from source
2. Run the installer
3. The sync service starts automatically as a Windows Service
4. The tray icon appears in your system tray

### Linux

1. Download the latest release or build from source
2. Install the systemd service:

   ```bash
   sudo systemctl enable dotnetcloud-sync
   sudo systemctl start dotnetcloud-sync
   ```

3. Launch the tray app from your applications menu

---

## Connecting Your Account

1. **Right-click the tray icon** → click **"Settings..."**
2. Click **"Add Account"**
3. Enter your DotNetCloud server URL (e.g., `https://cloud.example.com`)
4. Your web browser opens — log in with your DotNetCloud credentials
5. After login, the browser redirects back and the account is connected
6. Choose a local folder for syncing (default: `~/DotNetCloud/{server}`)
7. Click **Save** — syncing begins immediately

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

For more details, see the [Troubleshooting Guide](../../clients/desktop/TROUBLESHOOTING.md).

---

## Next Steps

- [Getting Started with Files](GETTING_STARTED.md) — web UI basics
- [Online Document Editing](DOCUMENT_EDITING.md) — edit documents in the browser
