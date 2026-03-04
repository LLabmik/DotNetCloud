# =============================================================================
# DotNetCloud — Windows MSI Installer Build Script (Skeleton)
# =============================================================================
# Usage: .\build-msi.ps1 [-Version "0.1.0"] [-Configuration "Release"]
#
# Prerequisites:
#   - .NET SDK installed
#   - WiX Toolset v4+ (dotnet tool install --global wix)
#
# Output: ./artifacts/DotNetCloud-<version>.msi
# =============================================================================

param(
    [string]$Version = "0.1.0",
    [string]$Configuration = "Release",
    [string]$OutputDir = "./artifacts"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " DotNetCloud — Build Windows MSI"        -ForegroundColor Cyan
Write-Host " Version: $Version"                      -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$SolutionRoot = Resolve-Path "$PSScriptRoot\..\.."
$PublishDir = Join-Path $SolutionRoot "artifacts\publish\win-x64"
$MsiOutput = Join-Path $OutputDir "DotNetCloud-${Version}.msi"

# Step 1: Publish for win-x64
Write-Host "`n[1/3] Publishing for win-x64..." -ForegroundColor Yellow
dotnet publish "$SolutionRoot\src\Core\DotNetCloud.Core.Server\DotNetCloud.Core.Server.csproj" `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output $PublishDir

# Step 2: Publish CLI
Write-Host "[2/3] Publishing CLI for win-x64..." -ForegroundColor Yellow
dotnet publish "$SolutionRoot\src\CLI\DotNetCloud.CLI\DotNetCloud.CLI.csproj" `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output "$PublishDir\cli"

# Step 3: Build MSI
Write-Host "[3/3] Building MSI installer..." -ForegroundColor Yellow

# TODO: Create WiX v4 .wxs source file with:
#   - Product/Package metadata (UpgradeCode, Version, Manufacturer)
#   - Directory layout (ProgramFiles\DotNetCloud\)
#   - Component groups for server and CLI binaries
#   - Windows Service registration (ServiceInstall + ServiceControl)
#   - PATH environment variable entry for CLI
#   - Start menu shortcuts
#   - Uninstall support
# TODO: Run: wix build -o $MsiOutput -d "PublishDir=$PublishDir" dotnetcloud.wxs

Write-Host "`n[SKELETON] Windows MSI build script structure ready." -ForegroundColor Green
Write-Host "NOTE: WiX source file (.wxs) and build step are not yet implemented." -ForegroundColor DarkYellow
Write-Host "Output would be: $MsiOutput" -ForegroundColor Gray
