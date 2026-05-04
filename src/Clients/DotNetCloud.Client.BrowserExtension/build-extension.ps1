# ─── DotNetCloud Browser Extension — Build Script (PowerShell) ─────────────
# Builds both Chrome and Firefox extensions and packages them as ZIP archives.
# Usage: .\build-extension.ps1

$ErrorActionPreference = 'Stop'

# Resolve project root
$ProjectRoot = Split-Path -Parent $PSScriptRoot
if (-not $ProjectRoot) {
    $ProjectRoot = Get-Location
}

Write-Host "Building DotNetCloud Browser Extension..." -ForegroundColor Cyan

# Install dependencies if needed
if (-not (Test-Path "$ProjectRoot\node_modules")) {
    Write-Host "Installing dependencies..." -ForegroundColor Yellow
    Push-Location $ProjectRoot
    try {
        npm install
    }
    finally {
        Pop-Location
    }
}

# Build Chrome
Write-Host "`nBuilding Chrome extension (MV3)..." -ForegroundColor Cyan
Push-Location $ProjectRoot
try {
    npm run build:chrome
}
finally {
    Pop-Location
}

# Build Firefox
Write-Host "`nBuilding Firefox extension (MV3)..." -ForegroundColor Cyan
Push-Location $ProjectRoot
try {
    npm run build:firefox
}
finally {
    Pop-Location
}

# Package
Write-Host "`nPackaging extensions..." -ForegroundColor Cyan

$DistDir = "$ProjectRoot\dist"
if (Test-Path "$DistDir\chrome") {
    Compress-Archive -Path "$DistDir\chrome\*" -DestinationPath "$DistDir\dotnetcloud-bookmarks-chrome.zip" -Force
    Write-Host "  ✓ Created: dist\dotnetcloud-bookmarks-chrome.zip" -ForegroundColor Green
}

if (Test-Path "$DistDir\firefox") {
    Compress-Archive -Path "$DistDir\firefox\*" -DestinationPath "$DistDir\dotnetcloud-bookmarks-firefox.zip" -Force
    Write-Host "  ✓ Created: dist\dotnetcloud-bookmarks-firefox.zip" -ForegroundColor Green
}

Write-Host "`nDone!" -ForegroundColor Cyan
