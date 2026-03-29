# Desktop Client — Troubleshooting

> **Last Updated:** 2026-03-29

> **Architecture note:** SyncService has been merged into SyncTray. There is now a **single process** — the Avalonia tray app owns the full sync lifecycle (sync engine, file watcher, chunked upload/download). No separate service, no IPC. Single-instance is enforced via file lock.

---

## Common Issues

### SyncTray Shows "Not syncing" or Sync Not Starting

**Cause:** SyncTray may have failed to start the sync engine, or no account is configured.

**Fix:**

- Ensure an account is configured: right-click tray icon → Settings → Accounts
- Check SyncTray logs for errors (see [Logs](#logs) section below)
- Try restarting SyncTray
- If running from source: `dotnet run --project src/Clients/DotNetCloud.Client.SyncTray/DotNetCloud.Client.SyncTray.csproj`

### Tray Icon Shows Error (Red Exclamation)

**Cause:** A sync error occurred. Common causes include:

1. **Authentication failure** — token expired and refresh failed
2. **Network error** — server unreachable
3. **Disk full** — local storage is full
4. **Permission denied** — file is locked by another process

**Fix:**

1. Hover over the tray icon to see the error summary
2. Open **Settings** → check account status
3. Try **"Sync now"** from the context menu
4. If authentication failed, remove and re-add the account

### Files Not Syncing

**Cause:** Sync may be paused, or the file is in an excluded folder.

**Fix:**

1. Check if sync is paused (tray icon shows yellow pause)
2. Resume sync via context menu → **"Resume syncing"**
3. Check selective sync settings — the folder may be excluded
4. Force a sync: context menu → **"Sync now"**
5. Check SyncTray logs for errors (see [Logs](#logs) section below)

### Conflict Files Appearing

**Cause:** The same file was modified on both the local machine and the server since the last sync.

**Expected behavior:** DotNetCloud creates a conflict copy:

```
report.docx                           ← remote version
report (conflict - Ben - 2025-07-14).docx  ← local version
```

**Resolution:**

1. Open both files and compare
2. Keep the correct version, delete the other
3. If the conflict copy is the correct one, delete the original and rename the conflict copy

### Slow Sync Performance

**Possible causes:**

1. **Large number of small files** — FileSystemWatcher generates many events
2. **Bandwidth limits** — check Settings → bandwidth limits
3. **Server under load** — check server health

**Fix:**

1. Increase the full scan interval if many files change frequently
2. Remove bandwidth limits if not needed
3. Check server-side performance (health endpoint, logs)

### Account Login Fails

**Cause:** OAuth2 PKCE flow issues.

**Fix:**

1. Ensure the server URL is correct — include `https://` and port if non-standard (e.g., `https://cloud.example.com:5443/`)
2. Check that the server is reachable from your machine (try opening the URL in a browser)
3. Ensure your browser is not blocking the localhost redirect
4. Try clearing browser cookies for the DotNetCloud server
5. If using a firewall, ensure the localhost OAuth callback port is not blocked

### Self-Signed Certificate Warnings

**Cause:** Self-hosted servers often use self-signed TLS certificates that are not trusted by the OS.

**Fix:**

1. **Windows:** Import the server's certificate into the Trusted Root Certification Authorities store:
   - Export the cert from the server (or download the `.crt` file from your admin)
   - Double-click the `.crt` file → **Install Certificate** → **Local Machine** → "Place all certificates in the following store" → **Trusted Root Certification Authorities**
2. **Linux:** Copy the `.crt` to `/usr/local/share/ca-certificates/` and run `sudo update-ca-certificates`
3. After installing the cert, restart the sync service and try Add Account again

### SyncTray Crashes on Startup

**Fix:**

1. Check the logs:
   - **Windows:** `%LOCALAPPDATA%\DotNetCloud\logs\sync-tray*.log`
   - **Linux:** `~/.local/share/DotNetCloud/logs/sync-tray*.log`
   - Or run from a terminal to see console output
2. Common causes:
   - Missing .NET runtime (if not using self-contained publish)
   - Corrupt sync state database
   - Another SyncTray instance already running (file lock conflict)

---

## Logs

### SyncTray Logs

SyncTray uses Serilog for structured logging. Since the sync engine runs in-process, all sync logs are in the SyncTray log files.

- **Windows:** `%LOCALAPPDATA%\DotNetCloud\logs\sync-tray*.log`
- **Linux:** `~/.local/share/DotNetCloud/logs/sync-tray*.log`

Logs include:

- Sync pass start/complete with duration
- File upload/download operations
- Conflict detection events
- Authentication token refresh
- FSW (FileSystemWatcher) events and debounce
- File stability checks
- Error details with stack traces

### Viewing Logs

```powershell
# Windows — view recent log entries
Get-Content "$env:LOCALAPPDATA\DotNetCloud\logs\sync-tray*.log" -Tail 50
```

```bash
# Linux — view recent log entries
tail -50 ~/.local/share/DotNetCloud/logs/sync-tray*.log
```

---

## Diagnostics

### Check Sync Status

Right-click the tray icon → the tooltip shows:

- Current state (idle, syncing, paused, error, offline)
- Number of files pending sync
- Storage quota usage

### Force Full Resync

If sync state becomes inconsistent:

1. Close SyncTray
2. Delete the SQLite state database for the affected context:
   - **Windows:** `%LOCALAPPDATA%\DotNetCloud\Sync\{contextId}\state.db` (also `-wal` and `-shm` files)
   - **Linux:** `~/.local/share/DotNetCloud/Sync/{contextId}/state.db`
3. Restart SyncTray
4. The next sync pass will do a full reconciliation

To list existing sync contexts:

```powershell
# Windows
Get-ChildItem "$env:ProgramData\DotNetCloud\Sync" -Directory | Where-Object { $_.Name -ne "logs" }
```

```bash
# Linux official release install
ls -d /var/lib/dotnetcloud/sync/*/

# Linux local/source install
ls -d ~/.local/share/DotNetCloud/Sync/*/
```

**Warning:** This may re-download all files. Ensure you have sufficient bandwidth.

---

## Getting Help

1. Check the [FAQ section](#faq) below
2. Review the [Sync Protocol](SYNC_PROTOCOL.md) for technical details
3. Open an issue on the DotNetCloud repository with:
   - OS and version
   - SyncTray version and OS
   - Relevant log entries
   - Steps to reproduce

---

## FAQ

**Q: Can I sync the same folder from multiple machines?**

A: Yes. Each machine has its own sync context. Changes propagate through the server. Conflicts are handled automatically.

**Q: What happens if I delete a file locally?**

A: The file is deleted on the server (moved to server trash). Other synced machines will also delete their local copy on the next sync pass.

**Q: Does sync work over a VPN?**

A: Yes, as long as the DotNetCloud server is reachable via HTTPS.

**Q: How much bandwidth does sync use?**

A: Only changed chunks (4 MB each) are transferred. Identical content is deduplicated. You can set bandwidth limits in Settings.

**Q: Can I pause sync temporarily?**

A: Yes. Right-click the tray icon → **"Pause syncing"**. Changes are queued locally and will sync when you resume.

**Q: What file types are not synced?**

A: All file types are synced. There is no file type filtering. System files, hidden files, and temporary files are synced unless excluded via selective sync.
