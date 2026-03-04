# =============================================================================
# DotNetCloud — Debian Package Build Script (Skeleton)
# =============================================================================
# Usage: .\build-deb.ps1 [-Version "0.1.0"] [-Configuration "Release"]
#
# Prerequisites:
#   - .NET SDK installed
#   - dpkg-deb available (Linux or WSL)
#
# Output: ./artifacts/dotnetcloud_<version>_amd64.deb
# =============================================================================

param(
    [string]$Version = "0.1.0",
    [string]$Configuration = "Release",
    [string]$OutputDir = "./artifacts"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " DotNetCloud — Build Debian Package"     -ForegroundColor Cyan
Write-Host " Version: $Version"                      -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$SolutionRoot = Resolve-Path "$PSScriptRoot\..\.."
$PublishDir = Join-Path $SolutionRoot "artifacts\publish\linux-x64"
$DebRoot = Join-Path $SolutionRoot "artifacts\deb-staging"
$DebOutput = Join-Path $OutputDir "dotnetcloud_${Version}_amd64.deb"

# Step 1: Publish for linux-x64
Write-Host "`n[1/4] Publishing for linux-x64..." -ForegroundColor Yellow
dotnet publish "$SolutionRoot\src\Core\DotNetCloud.Core.Server\DotNetCloud.Core.Server.csproj" `
    --configuration $Configuration `
    --runtime linux-x64 `
    --self-contained true `
    --output $PublishDir

# Step 2: Create .deb directory structure
Write-Host "[2/4] Creating Debian package structure..." -ForegroundColor Yellow
$InstallDir = Join-Path $DebRoot "opt\dotnetcloud"
$SystemdDir = Join-Path $DebRoot "etc\systemd\system"
$DebianDir  = Join-Path $DebRoot "DEBIAN"

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
New-Item -ItemType Directory -Force -Path $SystemdDir | Out-Null
New-Item -ItemType Directory -Force -Path $DebianDir  | Out-Null

# Step 3: Create DEBIAN/control
Write-Host "[3/4] Writing control file..." -ForegroundColor Yellow
$ControlContent = @"
Package: dotnetcloud
Version: $Version
Section: web
Priority: optional
Architecture: amd64
Maintainer: DotNetCloud Contributors <noreply@dotnetcloud.dev>
Description: DotNetCloud — A modular, multi-tenant cloud platform built with .NET
 Self-hosted cloud platform providing file sync, chat, calendar,
 contacts, project management, and more.
Depends: libc6 (>= 2.31), libssl3 | libssl1.1
Homepage: https://git.kimball.home/benk/dotnetcloud
"@
Set-Content -Path (Join-Path $DebianDir "control") -Value $ControlContent

# Step 4: Copy published files
Write-Host "[4/4] Copying published output..." -ForegroundColor Yellow
Copy-Item -Path "$PublishDir\*" -Destination $InstallDir -Recurse -Force

# TODO: Create systemd service file at $SystemdDir\dotnetcloud.service
# TODO: Create postinst script for user creation and directory permissions
# TODO: Run dpkg-deb --build $DebRoot $DebOutput

Write-Host "`n[SKELETON] Debian package build script structure ready." -ForegroundColor Green
Write-Host "NOTE: dpkg-deb build step and systemd service file are not yet implemented." -ForegroundColor DarkYellow
Write-Host "Output would be: $DebOutput" -ForegroundColor Gray
