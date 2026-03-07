# =============================================================================
# DotNetCloud - Desktop Client Bundle Builder
# =============================================================================
# Usage:
#   .\build-desktop-client-bundles.ps1 [-Version "0.1.0-alpha"] [-Configuration "Release"] [-BuildMsix]
#
# Purpose:
#   Builds self-contained desktop client artifacts for end-user installation.
#   End users do NOT need .NET runtime or SDK installed.
#
# Output:
#   ./artifacts/installers/dotnetcloud-desktop-client-linux-x64-<version>.tar.gz
#   ./artifacts/installers/dotnetcloud-desktop-client-win-x64-<version>.zip
#   ./artifacts/installers/dotnetcloud-sync-tray-win-x64-<version>.msix (optional via -BuildMsix)
# =============================================================================

param(
    [string]$Version = "0.1.0-alpha",
    [string]$Configuration = "Release",
    [string]$OutputDir = "./artifacts/installers",
    [switch]$BuildMsix
)

$ErrorActionPreference = "Stop"

Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " DotNetCloud - Desktop Client Bundle Builder" -ForegroundColor Cyan
Write-Host " Version: $Version" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan

$SolutionRoot = (Resolve-Path "$PSScriptRoot/../..").Path
$OutputRoot = (Resolve-Path (New-Item -ItemType Directory -Force -Path $OutputDir)).Path
$StagingRoot = Join-Path (Join-Path $SolutionRoot "artifacts/desktop-client-staging") $Version

$SyncServiceProject = Join-Path $SolutionRoot "src/Clients/DotNetCloud.Client.SyncService/DotNetCloud.Client.SyncService.csproj"
$SyncTrayProject = Join-Path $SolutionRoot "src/Clients/DotNetCloud.Client.SyncTray/DotNetCloud.Client.SyncTray.csproj"

$LinuxRoot = Join-Path $StagingRoot "linux-x64"
$LinuxPayload = Join-Path $LinuxRoot "payload"
$LinuxServicePublish = Join-Path $LinuxPayload "SyncService"
$LinuxTrayPublish = Join-Path $LinuxPayload "SyncTray"

$WindowsRoot = Join-Path $StagingRoot "win-x64"
$WindowsPayload = Join-Path $WindowsRoot "payload"
$WindowsServicePublish = Join-Path $WindowsPayload "SyncService"
$WindowsTrayPublish = Join-Path $WindowsPayload "SyncTray"

$LinuxArchive = Join-Path $OutputRoot "dotnetcloud-desktop-client-linux-x64-$Version.tar.gz"
$WindowsArchive = Join-Path $OutputRoot "dotnetcloud-desktop-client-win-x64-$Version.zip"

if (Test-Path $StagingRoot)
{
    Remove-Item -Path $StagingRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $LinuxServicePublish | Out-Null
New-Item -ItemType Directory -Force -Path $LinuxTrayPublish | Out-Null
New-Item -ItemType Directory -Force -Path $WindowsServicePublish | Out-Null
New-Item -ItemType Directory -Force -Path $WindowsTrayPublish | Out-Null

Write-Host "`n[1/6] Publishing Linux self-contained binaries..." -ForegroundColor Yellow

dotnet publish $SyncServiceProject `
    --configuration $Configuration `
    --runtime linux-x64 `
    --self-contained true `
    --output $LinuxServicePublish

dotnet publish $SyncTrayProject `
    --configuration $Configuration `
    --runtime linux-x64 `
    --self-contained true `
    --output $LinuxTrayPublish

Write-Host "[2/6] Publishing Windows self-contained binaries..." -ForegroundColor Yellow

dotnet publish $SyncServiceProject `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output $WindowsServicePublish

dotnet publish $SyncTrayProject `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output $WindowsTrayPublish

Write-Host "[3/6] Writing Linux installer scripts..." -ForegroundColor Yellow

$LinuxInstallScript = @(
        '#!/usr/bin/env bash',
        'set -euo pipefail',
        '',
        'if [[ "$EUID" -ne 0 ]]; then',
        '  echo "Please run as root (sudo ./install.sh)."',
        '  exit 1',
        'fi',
        '',
        'SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")"; pwd)"',
        'INSTALL_DIR="/opt/dotnetcloud-desktop-client"',
        'SERVICE_NAME="dotnetcloud-sync"',
        'SERVICE_FILE="/etc/systemd/system/${SERVICE_NAME}.service"',
        'LAUNCHER="/usr/local/bin/dotnetcloud-sync-tray"',
        '',
        'mkdir -p "$INSTALL_DIR"',
        '',
        '# Upgrade-safe replacement: stop running service before replacing binaries.',
        'if systemctl list-unit-files | grep -q "^${SERVICE_NAME}\.service"; then',
        '    systemctl stop "$SERVICE_NAME" || true',
        'fi',
        '',
        'cp -a "$SCRIPT_DIR/payload/." "$INSTALL_DIR/"',
        '',
        'install -m 0755 "$INSTALL_DIR/SyncService/dotnetcloud-sync-service" "$INSTALL_DIR/SyncService/dotnetcloud-sync-service"',
        'install -m 0755 "$INSTALL_DIR/SyncTray/dotnetcloud-sync-tray" "$INSTALL_DIR/SyncTray/dotnetcloud-sync-tray"',
        '',
        'cat > "$SERVICE_FILE" <<EOF',
        '[Unit]',
        'Description=DotNetCloud Sync Service',
        'After=network.target',
        '',
        '[Service]',
        'Type=notify',
        'ExecStart=$INSTALL_DIR/SyncService/dotnetcloud-sync-service',
        'Restart=on-failure',
        'RestartSec=10',
        '',
        '[Install]',
        'WantedBy=multi-user.target',
        'EOF',
        '',
        'cat > "$LAUNCHER" <<EOF',
        '#!/usr/bin/env bash',
        'exec "$INSTALL_DIR/SyncTray/dotnetcloud-sync-tray" "\$@"',
        'EOF',
        'chmod 0755 "$LAUNCHER"',
        '',
        'systemctl daemon-reload',
        'systemctl enable --now "$SERVICE_NAME"',
        '',
        'echo',
        'echo "Install complete."',
        'echo "Service status: systemctl status ${SERVICE_NAME} --no-pager"',
        'echo "Run tray as desktop user: dotnetcloud-sync-tray"'
) -join [Environment]::NewLine
Set-Content -Path (Join-Path $LinuxRoot "install.sh") -Value $LinuxInstallScript

$LinuxUninstallScript = @(
        '#!/usr/bin/env bash',
        'set -euo pipefail',
        '',
        'if [[ "$EUID" -ne 0 ]]; then',
        '  echo "Please run as root (sudo ./uninstall.sh)."',
        '  exit 1',
        'fi',
        '',
        'INSTALL_DIR="/opt/dotnetcloud-desktop-client"',
        'SERVICE_NAME="dotnetcloud-sync"',
        'SERVICE_FILE="/etc/systemd/system/${SERVICE_NAME}.service"',
        'LAUNCHER="/usr/local/bin/dotnetcloud-sync-tray"',
        '',
        'systemctl stop "$SERVICE_NAME" || true',
        'systemctl disable "$SERVICE_NAME" || true',
        'rm -f "$SERVICE_FILE"',
        'rm -f "$LAUNCHER"',
        'rm -rf "$INSTALL_DIR"',
        '',
        'systemctl daemon-reload',
        '',
        'echo "Uninstall complete."'
) -join [Environment]::NewLine
Set-Content -Path (Join-Path $LinuxRoot "uninstall.sh") -Value $LinuxUninstallScript

Write-Host "[4/6] Writing Windows installer scripts..." -ForegroundColor Yellow

$WindowsInstallScript = @(
    'param(',
    '    [string]$InstallPath = "$env:ProgramFiles\DotNetCloud\DesktopClient"',
    ')',
    '',
    '$ErrorActionPreference = "Stop"',
    '',
    '$identity = [Security.Principal.WindowsIdentity]::GetCurrent()',
    '$principal = New-Object Security.Principal.WindowsPrincipal($identity)',
    'if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {',
    '    throw "Run this installer from an elevated PowerShell window."',
    '}',
    '',
    '$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path',
    '$payloadPath = Join-Path $scriptDir "payload"',
    '',
    'if (-not (Test-Path $payloadPath)) {',
    '    throw "Payload folder not found: $payloadPath"',
    '}',
    '',
    'New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null',
    '',
    '$existingService = Get-Service -Name "DotNetCloudSync" -ErrorAction SilentlyContinue',
    'if ($null -ne $existingService -and $existingService.Status -ne ''Stopped'') {',
    '    sc.exe stop DotNetCloudSync | Out-Null',
    '    Start-Sleep -Seconds 2',
    '}',
    '',
    'Copy-Item -Path (Join-Path $payloadPath "*") -Destination $InstallPath -Recurse -Force',
    '',
    '$serviceExe = Join-Path $InstallPath "SyncService\dotnetcloud-sync-service.exe"',
    'if (-not (Test-Path $serviceExe)) {',
    '    throw "Sync service executable not found: $serviceExe"',
    '}',
    '',
    '$service = Get-Service -Name "DotNetCloudSync" -ErrorAction SilentlyContinue',
    'if ($null -eq $service) {',
    '    sc.exe create DotNetCloudSync binPath= "`"$serviceExe`"" start= auto | Out-Null',
    '}',
    'else {',
    '    sc.exe config DotNetCloudSync binPath= "`"$serviceExe`"" start= auto | Out-Null',
    '}',
    '',
    'sc.exe start DotNetCloudSync | Out-Null',
    '',
    '$startup = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup"',
    'New-Item -ItemType Directory -Force -Path $startup | Out-Null',
    '$shortcutPath = Join-Path $startup "DotNetCloud SyncTray.lnk"',
    '$trayExe = Join-Path $InstallPath "SyncTray\dotnetcloud-sync-tray.exe"',
    '$wsh = New-Object -ComObject WScript.Shell',
    '$shortcut = $wsh.CreateShortcut($shortcutPath)',
    '$shortcut.TargetPath = $trayExe',
    '$shortcut.WorkingDirectory = Split-Path $trayExe -Parent',
    '$shortcut.Save()',
    '',
    'Write-Host "Install complete."',
    'Write-Host "Service status:" -ForegroundColor Cyan',
    'Get-Service DotNetCloudSync',
    'Write-Host "Launch tray: $trayExe"'
) -join [Environment]::NewLine
Set-Content -Path (Join-Path $WindowsRoot "Install-DesktopClient.ps1") -Value $WindowsInstallScript

$WindowsUninstallScript = @(
    'param(',
    '    [string]$InstallPath = "$env:ProgramFiles\DotNetCloud\DesktopClient"',
    ')',
    '',
    '$ErrorActionPreference = "Stop"',
    '',
    '$identity = [Security.Principal.WindowsIdentity]::GetCurrent()',
    '$principal = New-Object Security.Principal.WindowsPrincipal($identity)',
    'if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {',
    '    throw "Run this uninstaller from an elevated PowerShell window."',
    '}',
    '',
    'sc.exe stop DotNetCloudSync | Out-Null',
    'sc.exe delete DotNetCloudSync | Out-Null',
    '',
    '$startupShortcut = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup\DotNetCloud SyncTray.lnk"',
    'if (Test-Path $startupShortcut) {',
    '    Remove-Item $startupShortcut -Force',
    '}',
    '',
    'if (Test-Path $InstallPath) {',
    '    Remove-Item $InstallPath -Recurse -Force',
    '}',
    '',
    'Write-Host "Uninstall complete."'
) -join [Environment]::NewLine
Set-Content -Path (Join-Path $WindowsRoot "Uninstall-DesktopClient.ps1") -Value $WindowsUninstallScript

$WindowsInstallCmd = @(
    '@echo off',
    'setlocal enabledelayedexpansion',
    '',
    'net session >nul 2>&1',
    'if not "%errorlevel%"=="0" (',
    '  echo This installer must run from an elevated Command Prompt.',
    '  echo Right-click cmd.exe and choose "Run as administrator".',
    '  exit /b 1',
    ')',
    '',
    'set "SCRIPT_DIR=%~dp0"',
    'set "PAYLOAD_DIR=%SCRIPT_DIR%payload"',
    'set "INSTALL_PATH=%ProgramFiles%\DotNetCloud\DesktopClient"',
    'set "SERVICE_NAME=DotNetCloudSync"',
    'set "SERVICE_EXE=%INSTALL_PATH%\SyncService\dotnetcloud-sync-service.exe"',
    'set "TRAY_EXE=%INSTALL_PATH%\SyncTray\dotnetcloud-sync-tray.exe"',
    '',
    'if not exist "%PAYLOAD_DIR%" (',
    '  echo Payload folder not found: "%PAYLOAD_DIR%"',
    '  exit /b 1',
    ')',
    '',
    'mkdir "%INSTALL_PATH%" 2>nul',
    'sc query "%SERVICE_NAME%" >nul 2>&1 && sc stop "%SERVICE_NAME%" >nul 2>&1',
    '',
    'robocopy "%PAYLOAD_DIR%" "%INSTALL_PATH%" /E /R:1 /W:1 /NFL /NDL /NJH /NJS >nul',
    'if %errorlevel% GEQ 8 (',
    '  echo Failed to copy payload to "%INSTALL_PATH%".',
    '  exit /b 1',
    ')',
    '',
    'if not exist "%SERVICE_EXE%" (',
    '  echo Sync service executable not found: "%SERVICE_EXE%"',
    '  exit /b 1',
    ')',
    '',
    'sc query "%SERVICE_NAME%" >nul 2>&1',
    'if %errorlevel%==0 (',
    '  sc config "%SERVICE_NAME%" binPath= "\"%SERVICE_EXE%\"" start= auto >nul',
    ') else (',
    '  sc create "%SERVICE_NAME%" binPath= "\"%SERVICE_EXE%\"" start= auto >nul',
    ')',
    '',
    'sc start "%SERVICE_NAME%" >nul',
    '',
    'if exist "%TRAY_EXE%" (',
    '  reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v DotNetCloudSyncTray /t REG_SZ /d "\"%TRAY_EXE%\"" /f >nul',
    ')',
    '',
    'echo Install complete.',
    'sc query "%SERVICE_NAME%"',
    'if exist "%TRAY_EXE%" (',
    '  start "" "%TRAY_EXE%"',
    ')',
    'endlocal'
) -join [Environment]::NewLine
Set-Content -Path (Join-Path $WindowsRoot "install.cmd") -Value $WindowsInstallCmd

$WindowsUninstallCmd = @(
    '@echo off',
    'setlocal',
    '',
    'net session >nul 2>&1',
    'if not "%errorlevel%"=="0" (',
    '  echo This uninstaller must run from an elevated Command Prompt.',
    '  echo Right-click cmd.exe and choose "Run as administrator".',
    '  exit /b 1',
    ')',
    '',
    'set "INSTALL_PATH=%ProgramFiles%\DotNetCloud\DesktopClient"',
    'set "SERVICE_NAME=DotNetCloudSync"',
    '',
    'sc stop "%SERVICE_NAME%" >nul 2>&1',
    'sc delete "%SERVICE_NAME%" >nul 2>&1',
    'reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v DotNetCloudSyncTray /f >nul 2>&1',
    '',
    'if exist "%INSTALL_PATH%" (',
    '  rmdir /s /q "%INSTALL_PATH%"',
    ')',
    '',
    'echo Uninstall complete.',
    'endlocal'
) -join [Environment]::NewLine
Set-Content -Path (Join-Path $WindowsRoot "uninstall.cmd") -Value $WindowsUninstallCmd

Write-Host "[5/6] Marking executable scripts and creating README files..." -ForegroundColor Yellow

if ($IsLinux -or $IsMacOS) {
    & chmod +x (Join-Path $LinuxRoot "install.sh")
    & chmod +x (Join-Path $LinuxRoot "uninstall.sh")
}

Set-Content -Path (Join-Path $LinuxRoot "README.txt") -Value ((@(
        'DotNetCloud Desktop Client (Linux x64)',
        '',
        'Install:',
        '  sudo ./install.sh',
        '',
        'Uninstall:',
        '  sudo ./uninstall.sh',
        '',
        'After install, run tray in user session:',
        '  dotnetcloud-sync-tray'
) -join [Environment]::NewLine))

Set-Content -Path (Join-Path $WindowsRoot "README.txt") -Value ((@(
        'DotNetCloud Desktop Client (Windows x64)',
        '',
    'Install (elevated Command Prompt):',
        '  install.cmd',
        '',
    'PowerShell option (if policy allows scripts):',
    '  .\Install-DesktopClient.ps1',
    '',
    'Uninstall (elevated Command Prompt):',
    '  uninstall.cmd',
    '',
        'Uninstall (elevated PowerShell):',
        '  .\Uninstall-DesktopClient.ps1'
) -join [Environment]::NewLine))

Write-Host "[6/6] Creating distributable archives..." -ForegroundColor Yellow

if (Test-Path $LinuxArchive) { Remove-Item $LinuxArchive -Force }
if (Test-Path $WindowsArchive) { Remove-Item $WindowsArchive -Force }

if (Get-Command tar -ErrorAction SilentlyContinue) {
    Push-Location $StagingRoot
    & tar -czf $LinuxArchive "linux-x64"
    Pop-Location
}
else {
    throw "The 'tar' command is required to produce the Linux installer archive."
}

Compress-Archive -Path (Join-Path $StagingRoot "win-x64/*") -DestinationPath $WindowsArchive

if ($BuildMsix)
{
    if (-not $IsWindows)
    {
        throw "-BuildMsix can only run on Windows hosts."
    }

    Write-Host "[Optional] Building SyncTray MSIX..." -ForegroundColor Yellow
    $msixScript = Join-Path $PSScriptRoot "build-desktop-client-msix.ps1"

    & $msixScript -Version $Version -Configuration $Configuration -OutputDir $OutputRoot
}

# Generate release-style checksum files for local distribution/testing.
$LinuxArchiveName = Split-Path $LinuxArchive -Leaf
$WindowsArchiveName = Split-Path $WindowsArchive -Leaf
$LinuxHash = (Get-FileHash -Path $LinuxArchive -Algorithm SHA256).Hash.ToLowerInvariant()
$WindowsHash = (Get-FileHash -Path $WindowsArchive -Algorithm SHA256).Hash.ToLowerInvariant()

Set-Content -Path "$LinuxArchive.sha256" -Value "$LinuxHash  $LinuxArchiveName"
Set-Content -Path "$WindowsArchive.sha256" -Value "$WindowsHash  $WindowsArchiveName"

$msixPath = Join-Path $OutputRoot "dotnetcloud-sync-tray-win-x64-$Version.msix"
if ($BuildMsix -and (Test-Path $msixPath))
{
    $msixName = Split-Path $msixPath -Leaf
    $msixHash = (Get-FileHash -Path $msixPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -Path "$msixPath.sha256" -Value "$msixHash  $msixName"
}

Write-Host "`nDesktop client bundles created:" -ForegroundColor Green
Write-Host "  $LinuxArchive"
Write-Host "  $WindowsArchive"
Write-Host "  $LinuxArchive.sha256"
Write-Host "  $WindowsArchive.sha256"

if ($BuildMsix -and (Test-Path $msixPath))
{
    Write-Host "  $msixPath"
    Write-Host "  $msixPath.sha256"
}

Write-Host "`nEnd users can install without .NET SDK/runtime prerequisites." -ForegroundColor Green
