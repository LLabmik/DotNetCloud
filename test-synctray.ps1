#!/usr/bin/env pwsh
# Test script for SyncTray - starts both services and shows logs

Write-Host "=== DotNetCloud SyncTray Test ===" -ForegroundColor Cyan
Write-Host ""

# Stop any running instances
Write-Host "Stopping existing processes..." -ForegroundColor Yellow
Stop-Process -Name "dotnetcloud-sync-*" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# Clean old logs
$logDir = "$env:LOCALAPPDATA\DotNetCloud\logs"
if (Test-Path $logDir) {
    Write-Host "Cleaning old logs..." -ForegroundColor Yellow
    Remove-Item "$logDir\sync-tray*.log" -Force -ErrorAction SilentlyContinue
}

# Start SyncService in background
Write-Host "Starting SyncService..." -ForegroundColor Green
$serviceJob = Start-Job -ScriptBlock {
    Set-Location "C:\Repos\dotnetcloud\src\Clients\DotNetCloud.Client.SyncService"
    dotnet run 2>&1
}

Start-Sleep -Seconds 4

# Verify IPC pipe exists
$pipe = Get-ChildItem "\\.\pipe\dotnetcloud-sync" -ErrorAction SilentlyContinue
if ($pipe) {
    Write-Host "✓ IPC pipe ready" -ForegroundColor Green
} else {
    Write-Host "✗ IPC pipe not found - SyncService may have failed to start" -ForegroundColor Red
    Write-Host "  Check SyncService output:"
    Receive-Job $serviceJob
    exit 1
}

# Start SyncTray
Write-Host "Starting SyncTray..." -ForegroundColor Green
Write-Host "  (SyncTray window will open - check system tray for icon)" -ForegroundColor Gray
Write-Host ""

cd "C:\Repos\dotnetcloud\src\Clients\DotNetCloud.Client.SyncTray"
Start-Process powershell -ArgumentList @(
    "-NoExit",
    "-Command",
    "cd '$PWD'; Write-Host 'SyncTray Console - Keep this window open' -ForegroundColor Cyan; Write-Host ''; dotnet run"
)

Start-Sleep -Seconds 5

# Check if processes are running
$trayProc = Get-Process -Name "dotnetcloud-sync-tray" -ErrorAction SilentlyContinue
$serviceProc = Get-Process -Name "dotnetcloud-sync-service" -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "=== Status ===" -ForegroundColor Cyan
if ($serviceProc) {
    Write-Host "✓ SyncService running (PID: $($serviceProc.Id))" -ForegroundColor Green
} else {
    Write-Host "✗ SyncService NOT running" -ForegroundColor Red
}

if ($trayProc) {
    Write-Host "✓ SyncTray running (PID: $($trayProc.Id))" -ForegroundColor Green
} else {
    Write-Host "✗ SyncTray NOT running" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== What to check in the system tray ===" -ForegroundColor Cyan
Write-Host "1. Look for a colored circle icon (gray = offline, green = connected)"
Write-Host "2. Hover over the icon - tooltip should show sync status"
Write-Host "3. LEFT-CLICK (not right-click) on the icon to show the menu"
Write-Host "4. If no menu appears, check the log file below"
Write-Host ""

# Show SyncTray log
Start-Sleep -Seconds 3
$logFile = Get-ChildItem "$logDir\sync-tray*.log" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($logFile) {
    Write-Host "=== SyncTray Log ($($logFile.Name)) ===" -ForegroundColor Cyan
    Get-Content $logFile.FullName
} else {
    Write-Host "⚠ No log file found at: $logDir" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Press Ctrl+C to stop monitoring, then run 'Stop-Process -Name dotnetcloud-sync-* -Force' to stop all services"
