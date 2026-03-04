# =============================================================================
# DotNetCloud — Docker Image Build Script
# =============================================================================
# Usage: .\build-docker.ps1 [-Version "0.1.0"] [-Push] [-Registry "ghcr.io/benk"]
#
# Prerequisites:
#   - Docker Engine installed and running
#
# Output: Docker image tagged as dotnetcloud:<version>
# =============================================================================

param(
    [string]$Version = "0.1.0",
    [string]$Registry = "",
    [switch]$Push,
    [switch]$Latest
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " DotNetCloud — Build Docker Image"       -ForegroundColor Cyan
Write-Host " Version: $Version"                      -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$SolutionRoot = Resolve-Path "$PSScriptRoot\..\.."
$ImageName = "dotnetcloud"

if ($Registry) {
    $ImageName = "$Registry/$ImageName"
}

$Tags = @("${ImageName}:${Version}")
if ($Latest) {
    $Tags += "${ImageName}:latest"
}

# Build tag arguments
$TagArgs = $Tags | ForEach-Object { "-t", $_ }

# Step 1: Build Docker image
Write-Host "`n[1/2] Building Docker image..." -ForegroundColor Yellow
docker build $TagArgs `
    --build-arg VERSION=$Version `
    --file "$SolutionRoot\Dockerfile" `
    $SolutionRoot

if ($LASTEXITCODE -ne 0) {
    Write-Host "Docker build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Docker image built successfully." -ForegroundColor Green
foreach ($tag in $Tags) {
    Write-Host "  Tagged: $tag" -ForegroundColor Gray
}

# Step 2: Push (optional)
if ($Push) {
    Write-Host "`n[2/2] Pushing Docker image..." -ForegroundColor Yellow
    foreach ($tag in $Tags) {
        Write-Host "  Pushing $tag..." -ForegroundColor Gray
        docker push $tag
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Push failed for $tag!" -ForegroundColor Red
            exit 1
        }
    }
    Write-Host "Docker image pushed successfully." -ForegroundColor Green
}
else {
    Write-Host "`n[2/2] Push skipped (use -Push to push)." -ForegroundColor DarkYellow
}
