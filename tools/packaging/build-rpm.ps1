# =============================================================================
# DotNetCloud — RPM Package Build Script (Skeleton)
# =============================================================================
# Usage: .\build-rpm.ps1 [-Version "0.1.0"] [-Configuration "Release"]
#
# Prerequisites:
#   - .NET SDK installed
#   - rpmbuild available (Linux or WSL)
#
# Output: ./artifacts/dotnetcloud-<version>.x86_64.rpm
# =============================================================================

param(
    [string]$Version = "0.1.0",
    [string]$Configuration = "Release",
    [string]$OutputDir = "./artifacts"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " DotNetCloud — Build RPM Package"        -ForegroundColor Cyan
Write-Host " Version: $Version"                      -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$SolutionRoot = Resolve-Path "$PSScriptRoot\..\.."
$PublishDir = Join-Path $SolutionRoot "artifacts\publish\linux-x64"
$RpmBuildRoot = Join-Path $SolutionRoot "artifacts\rpm-staging"
$SpecFile = Join-Path $RpmBuildRoot "SPECS\dotnetcloud.spec"

# Step 1: Publish for linux-x64
Write-Host "`n[1/4] Publishing for linux-x64..." -ForegroundColor Yellow
dotnet publish "$SolutionRoot\src\Core\DotNetCloud.Core.Server\DotNetCloud.Core.Server.csproj" `
    --configuration $Configuration `
    --runtime linux-x64 `
    --self-contained true `
    --output $PublishDir

# Step 2: Create RPM directory structure
Write-Host "[2/4] Creating RPM build structure..." -ForegroundColor Yellow
$RpmDirs = @("BUILD", "RPMS", "SOURCES", "SPECS", "SRPMS", "BUILDROOT")
foreach ($dir in $RpmDirs) {
    New-Item -ItemType Directory -Force -Path (Join-Path $RpmBuildRoot $dir) | Out-Null
}

# Step 3: Create .spec file
Write-Host "[3/4] Writing spec file..." -ForegroundColor Yellow
$SpecContent = @"
Name:           dotnetcloud
Version:        $Version
Release:        1%{?dist}
Summary:        A modular, multi-tenant cloud platform built with .NET
License:        AGPL-3.0-or-later
URL:            https://github.com/LLabmik/DotNetCloud

%description
DotNetCloud is a self-hosted cloud platform providing file sync, chat,
calendar, contacts, project management, and more. Built with .NET 10.

%install
mkdir -p %{buildroot}/opt/dotnetcloud
cp -r %{_sourcedir}/* %{buildroot}/opt/dotnetcloud/

%files
/opt/dotnetcloud/

%changelog
* $(Get-Date -Format "ddd MMM dd yyyy") DotNetCloud Contributors <noreply@dotnetcloud.dev> - $Version-1
- Initial package build
"@
Set-Content -Path $SpecFile -Value $SpecContent

# Step 4: Copy source files
Write-Host "[4/4] Copying published output to SOURCES..." -ForegroundColor Yellow
$SourcesDir = Join-Path $RpmBuildRoot "SOURCES"
Copy-Item -Path "$PublishDir\*" -Destination $SourcesDir -Recurse -Force

# TODO: Run rpmbuild -bb $SpecFile --define "_topdir $RpmBuildRoot"
# TODO: Copy resulting RPM to $OutputDir

Write-Host "`n[SKELETON] RPM package build script structure ready." -ForegroundColor Green
Write-Host "NOTE: rpmbuild execution step is not yet implemented." -ForegroundColor DarkYellow
Write-Host "Output would be: $OutputDir\dotnetcloud-${Version}.x86_64.rpm" -ForegroundColor Gray
