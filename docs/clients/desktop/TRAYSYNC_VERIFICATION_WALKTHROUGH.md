# TraySync Client Build, Install, and Verification Walkthrough

> **Last Updated:** 2026-03-06

This guide walks you through building, installing, and validating DotNetCloud desktop tray sync across your test machines.

## Test Topology

- **DotNetCloud server:** `mint22`
- **Linux client:** `mint-dnc-client`
- **Windows client:** `Windows11-TestDNC`
- **Server URL used in examples:** `https://mint22:15443`

## What You Will Validate

- ✓ Linux SyncService + SyncTray installer install
- ✓ Windows SyncService + SyncTray installer install
- ✓ Both clients connect to `mint22`
- ✓ Server-mediated bidirectional file sync: `mint-dnc-client` <-> `mint22` <-> `Windows11-TestDNC` (no direct client-to-client sync)
- ✓ Conflict handling, delete propagation, and offline/reconnect behavior
- ✓ Upgrade from one client version to a newer one

## Prerequisites

- Network connectivity from both clients to `mint22:15443`
- DotNetCloud account credentials for `mint22`
- Desktop session available on each client to run tray app
- Access to GitHub Releases for downloading installer bundles

## User Model for This Walkthrough

- Required: install and configure the desktop client separately for each OS user account.
- If a machine has multiple users (for example `alice` and `bob`), each user must run installer + account setup in their own OS session.
- Do not assume one shared install is safely isolated across concurrently active OS users.
- All test steps in this walkthrough are executed from the single owning OS account on each client machine.

## 0) Generate Installers Locally (Developer Machine)

Use this when you want to test installers before creating a GitHub tag/release.

### PowerShell (Windows or PowerShell 7 on Linux)

```powershell
.\tools\packaging\build-desktop-client-bundles.ps1 -Version "0.1.0-alpha-local.1" -Configuration "Release" -OutputDir "./artifacts/installers"
```

### Bash (Linux/macOS)

```bash
./tools/packaging/build-desktop-client-bundles.sh 0.1.0-alpha-local.1 Release ./artifacts/installers
```

### Local output files

- `artifacts/installers/dotnetcloud-desktop-client-linux-x64-0.1.0-alpha-local.1.tar.gz`
- `artifacts/installers/dotnetcloud-desktop-client-win-x64-0.1.0-alpha-local.1.zip`
- `artifacts/installers/dotnetcloud-desktop-client-linux-x64-0.1.0-alpha-local.1.tar.gz.sha256`
- `artifacts/installers/dotnetcloud-desktop-client-win-x64-0.1.0-alpha-local.1.zip.sha256`

Copy these artifacts to `mint-dnc-client` and `Windows11-TestDNC`, then continue with install steps below.

## 1) Install on Linux (`mint-dnc-client`)

Run all commands on `mint-dnc-client`.

If `mint-dnc-client` has multiple OS users, repeat install + account setup per user session.

### 1.1 Download + extract installer bundle

```bash
wget "https://github.com/LLabmik/DotNetCloud/releases/download/v<VERSION>/dotnetcloud-desktop-client-linux-x64-<VERSION>.tar.gz"
tar -xzf dotnetcloud-desktop-client-linux-x64-<VERSION>.tar.gz
cd linux-x64
```

### 1.2 Install SyncService + launcher

```bash
sudo ./install.sh
sudo systemctl status dotnetcloud-sync --no-pager
```

### 1.3 Run SyncTray

```bash
dotnetcloud-sync-tray
```

Optional autostart in desktop session:

```bash
mkdir -p ~/.config/autostart
cat > ~/.config/autostart/dotnetcloud-sync-tray.desktop <<'EOF'
[Desktop Entry]
Type=Application
Name=DotNetCloud SyncTray
Exec=dotnetcloud-sync-tray
X-GNOME-Autostart-enabled=true
EOF
```

## 2) Install on Windows (`Windows11-TestDNC`)

Run all commands in **PowerShell** on `Windows11-TestDNC`.

If `Windows11-TestDNC` has multiple OS users, each user must perform installation/account setup from their own Windows login.

### 2.1 Download + extract installer bundle

```powershell
Invoke-WebRequest "https://github.com/LLabmik/DotNetCloud/releases/download/v<VERSION>/dotnetcloud-desktop-client-win-x64-<VERSION>.zip" -OutFile "dotnetcloud-desktop-client-win-x64-<VERSION>.zip"
Expand-Archive -Path ".\dotnetcloud-desktop-client-win-x64-<VERSION>.zip" -DestinationPath ".\dotnetcloud-desktop-client-win-x64-<VERSION>" -Force
Set-Location ".\dotnetcloud-desktop-client-win-x64-<VERSION>"
```

### 2.2 Install SyncService + tray startup shortcut

```powershell
.\Install-DesktopClient.ps1
Get-Service DotNetCloudSync
```

### 2.3 Run SyncTray

```powershell
& "$env:ProgramFiles\DotNetCloud\DesktopClient\SyncTray\dotnetcloud-sync-tray.exe"
```

## 3) First-Time Account Connection (Both Clients)

Perform on both `mint-dnc-client` and `Windows11-TestDNC`:

1. Launch SyncTray.
2. Open tray menu -> `Settings...` -> `Add Account`.
3. Enter server URL: `https://mint22:15443`.
4. Complete browser login.
5. Choose local sync root:
   - Linux: `~/DotNetCloud/mint22`
   - Windows: `%USERPROFILE%\DotNetCloud\mint22`
6. Save and wait for initial sync.

## 4) Pre-Flight Health Checks

### Linux (`mint-dnc-client`)

```bash
systemctl is-active dotnetcloud-sync
ls -l /run/dotnetcloud/sync.sock
curl -k -I https://mint22:15443
```

### Windows (`Windows11-TestDNC`)

```powershell
Get-Service DotNetCloudSync
Test-NetConnection mint22 -Port 15443
```

## 5) Cross-Machine Verification Matrix

Use the same synced folder path on each machine (for example, `DotNetCloud/mint22/Verification`).

### 5.1 Create -> Propagate

1. On `mint-dnc-client`, create `linux-create.txt` with unique text.
2. Confirm file appears on server view.
3. Confirm file appears on `Windows11-TestDNC` with identical contents.

### 5.2 Modify -> Propagate

1. On `Windows11-TestDNC`, edit `linux-create.txt`.
2. Confirm updated content on server.
3. Confirm updated content on `mint-dnc-client`.

### 5.3 Delete -> Propagate

1. On `mint-dnc-client`, delete `linux-create.txt`.
2. Confirm delete reaches server (or server trash behavior).
3. Confirm deletion on `Windows11-TestDNC`.

### 5.4 Folder Rename

1. On `Windows11-TestDNC`, rename `Verification` to `Verification-Renamed`.
2. Confirm rename reflected on server.
3. Confirm renamed folder on `mint-dnc-client`.

### 5.5 Large File Transfer

1. Create a test file (for example, 500 MB to 1 GB) on `mint-dnc-client`.
2. Confirm upload completion.
3. Confirm successful download on `Windows11-TestDNC`.
4. Compare hashes (`sha256sum` or `Get-FileHash`).

### 5.6 Conflict Scenario

1. Disconnect both clients from network (or pause sync).
2. Edit same file on both machines.
3. Reconnect/resume sync.
4. Verify one base file plus one conflict copy exists.

### 5.7 Offline/Reconnect Recovery

1. Stop network on one client during active sync.
2. Make file changes locally.
3. Restore network.
4. Verify queued operations are eventually applied.

## 6) Evidence Collection Template

Record each test as you run it:

| Test | Initiator | Expected | Actual | Result |
|---|---|---|---|---|
| Create -> Propagate | mint-dnc-client | File appears on server + Windows11-TestDNC |  | ☐ Pass / ☐ Fail |
| Modify -> Propagate | Windows11-TestDNC | Updated content appears on server + mint-dnc-client |  | ☐ Pass / ☐ Fail |
| Delete -> Propagate | mint-dnc-client | Delete reflected on server + Windows11-TestDNC |  | ☐ Pass / ☐ Fail |
| Folder Rename | Windows11-TestDNC | Rename reflected everywhere |  | ☐ Pass / ☐ Fail |
| Large File | mint-dnc-client | File transfers and hash matches |  | ☐ Pass / ☐ Fail |
| Conflict | both | Conflict copy created and no data loss |  | ☐ Pass / ☐ Fail |
| Offline/Reconnect | either | Pending ops sync after reconnection |  | ☐ Pass / ☐ Fail |

## 7) Useful Diagnostics During Testing

### Linux

```bash
sudo systemctl status dotnetcloud-sync --no-pager
sudo journalctl -u dotnetcloud-sync --no-pager -n 200
tail -50 /var/log/dotnetcloud/sync-service-*.log
```

### Windows

```powershell
Get-Service DotNetCloudSync
Get-Content "$env:ProgramData\DotNetCloud\logs\sync-service-*.log" -Tail 50
Get-Content "$env:LOCALAPPDATA\DotNetCloud\logs\sync-tray-*.log" -Tail 50
```

## 8) Quick Uninstall / Reset (If Needed)

### Linux

```bash
sudo ./uninstall.sh
```

### Windows

```powershell
.\Uninstall-DesktopClient.ps1
```

## 9) Upgrade Validation (Future Version Support)

### Linux (`mint-dnc-client`)

1. Install version `A` and complete baseline sync checks.
2. Download version `B` (`B > A`) installer archive.
3. Run `sudo ./install.sh` from the version `B` extracted folder.
4. Verify `dotnetcloud-sync` restarts and account configuration remains intact.
5. Verify new file changes still sync to `mint22` and `Windows11-TestDNC`.

### Windows (`Windows11-TestDNC`)

1. Install version `A` and complete baseline sync checks.
2. Download version `B` (`B > A`) installer zip.
3. Run `Install-DesktopClient.ps1` from the version `B` extracted folder (elevated PowerShell).
4. Verify `DotNetCloudSync` service is running and tray launches normally.
5. Verify new file changes still sync to `mint22` and `mint-dnc-client`.

## Notes

- Published executable names are:
  - `dotnetcloud-sync-service`
  - `dotnetcloud-sync-tray`
- Installer filenames are versioned:
  - `dotnetcloud-desktop-client-linux-x64-<version>.tar.gz`
  - `dotnetcloud-desktop-client-win-x64-<version>.zip`
- Linux IPC socket path is `/run/dotnetcloud/sync.sock`.
- If `mint22` uses a self-signed TLS cert, browser/API trust warnings may appear until CA trust is configured on each client.
