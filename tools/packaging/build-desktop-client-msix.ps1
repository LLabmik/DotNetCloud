# =============================================================================
# DotNetCloud - Desktop SyncTray MSIX Builder
# =============================================================================
# Usage:
#   .\build-desktop-client-msix.ps1 -Version "0.1.0-alpha" -Configuration "Release"
#
# Purpose:
#   Builds a Windows MSIX package for DotNetCloud desktop client binaries.
#   SyncTray is registered as the launchable app entry point.
#
# Notes:
#   - Must run on Windows with Windows 10/11 SDK installed (makeappx.exe required).
#   - Unsigned MSIX cannot be installed until signed with a trusted certificate.
# =============================================================================

param(
    [string]$Version = "0.1.0-alpha",
    [string]$Configuration = "Release",
    [string]$OutputDir = "./artifacts/installers",
    [string]$PackageIdentityName = "DotNetCloud.SyncTray",
    [string]$PackageDisplayName = "DotNetCloud SyncTray",
    [string]$Publisher = "CN=DotNetCloud Dev",
    [string]$PublisherDisplayName = "DotNetCloud",
    [switch]$Sign,
    [switch]$CreateTestCertificate,
    [string]$CertificatePath,
    [string]$CertificatePassword
)

$ErrorActionPreference = "Stop"

function Convert-ToMsixVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$InputVersion
    )

    $match = [regex]::Match($InputVersion, '^(\d+)\.(\d+)\.(\d+)')
    if (-not $match.Success) {
        throw "Version '$InputVersion' must start with semantic version components like 0.1.0."
    }

    return "{0}.{1}.{2}.0" -f $match.Groups[1].Value, $match.Groups[2].Value, $match.Groups[3].Value
}

function Get-WindowsSdkTool {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ToolName
    )

    $onPath = Get-Command $ToolName -ErrorAction SilentlyContinue
    if ($null -ne $onPath) {
        return $onPath.Source
    }

    $kitsRoot = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    if (-not (Test-Path $kitsRoot)) {
        throw "Windows SDK bin path not found at '$kitsRoot'. Install Windows 10/11 SDK."
    }

    $candidates = @(Get-ChildItem -Path $kitsRoot -Directory |
        Sort-Object -Property Name -Descending |
        ForEach-Object { Join-Path $_.FullName "x64\$ToolName" } |
        Where-Object { Test-Path $_ })

    if ($candidates.Count -eq 0) {
        throw "Unable to locate '$ToolName' in Windows SDK path '$kitsRoot'."
    }

    return $candidates[0]
}

function New-MsixAsset {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [int]$Width,
        [Parameter(Mandatory = $true)]
        [int]$Height,
        [Parameter(Mandatory = $true)]
        [string]$Text
    )

    Add-Type -AssemblyName System.Drawing

    $bitmap = New-Object System.Drawing.Bitmap($Width, $Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    try {
        $graphics.Clear([System.Drawing.Color]::FromArgb(24, 74, 168))
        $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

        $fontSize = [Math]::Max([double]($Height / 3.5), 10.0)
        $font = New-Object System.Drawing.Font("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold)
        $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)

        try {
            $format = New-Object System.Drawing.StringFormat
            $format.Alignment = [System.Drawing.StringAlignment]::Center
            $format.LineAlignment = [System.Drawing.StringAlignment]::Center
            $rect = New-Object System.Drawing.RectangleF(0, 0, $Width, $Height)
            $graphics.DrawString($Text, $font, $brush, $rect, $format)
        }
        finally {
            $font.Dispose()
            $brush.Dispose()
            if ($null -ne $format) {
                $format.Dispose()
            }
        }

        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function New-TestCertificate {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PublisherSubject,
        [Parameter(Mandatory = $true)]
        [string]$CertificatePfxPath,
        [Parameter(Mandatory = $true)]
        [string]$Password
    )

    $securePassword = ConvertTo-SecureString -String $Password -AsPlainText -Force

    $certificate = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $PublisherSubject `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -KeyUsage DigitalSignature `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3") `
        -FriendlyName "DotNetCloud SyncTray MSIX Test Certificate"

    Export-PfxCertificate -Cert $certificate -FilePath $CertificatePfxPath -Password $securePassword | Out-Null

    return $CertificatePfxPath
}

if (-not $IsWindows) {
    throw "MSIX packaging is only supported on Windows hosts. Run this script in Windows PowerShell or PowerShell 7 on Windows."
}

Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " DotNetCloud - Desktop SyncTray MSIX Builder" -ForegroundColor Cyan
Write-Host " Version: $Version" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan

$solutionRoot = (Resolve-Path "$PSScriptRoot/../..").Path
$outputRoot = (Resolve-Path (New-Item -ItemType Directory -Force -Path $OutputDir)).Path

$syncTrayProject = Join-Path $solutionRoot "src/Clients/DotNetCloud.Client.SyncTray/DotNetCloud.Client.SyncTray.csproj"
$syncServiceProject = Join-Path $solutionRoot "src/Clients/DotNetCloud.Client.SyncService/DotNetCloud.Client.SyncService.csproj"
$manifestTemplate = Join-Path $solutionRoot "tools/packaging/msix/AppxManifest.xml.template"

$msixVersion = Convert-ToMsixVersion -InputVersion $Version
$stagingRoot = Join-Path $solutionRoot "artifacts/desktop-client-msix/$Version"
$publishRoot = Join-Path $stagingRoot "SyncTray"
$servicePublishRoot = Join-Path $stagingRoot "SyncService"
$assetsRoot = Join-Path $stagingRoot "Assets"

$outputMsix = Join-Path $outputRoot "dotnetcloud-sync-tray-win-x64-$Version.msix"

if (Test-Path $stagingRoot) {
    Remove-Item -Path $stagingRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null
New-Item -ItemType Directory -Force -Path $servicePublishRoot | Out-Null
New-Item -ItemType Directory -Force -Path $assetsRoot | Out-Null

Write-Host "`n[1/4] Publishing desktop client binaries for win-x64..." -ForegroundColor Yellow

dotnet publish $syncTrayProject `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output $publishRoot

dotnet publish $syncServiceProject `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output $servicePublishRoot

Write-Host "[2/4] Generating MSIX assets and manifest..." -ForegroundColor Yellow

New-MsixAsset -Path (Join-Path $assetsRoot "StoreLogo.png") -Width 50 -Height 50 -Text "DC"
New-MsixAsset -Path (Join-Path $assetsRoot "Square44x44Logo.png") -Width 44 -Height 44 -Text "DC"
New-MsixAsset -Path (Join-Path $assetsRoot "Square150x150Logo.png") -Width 150 -Height 150 -Text "DC"
New-MsixAsset -Path (Join-Path $assetsRoot "Square310x310Logo.png") -Width 310 -Height 310 -Text "DC"
New-MsixAsset -Path (Join-Path $assetsRoot "Wide310x150Logo.png") -Width 310 -Height 150 -Text "DotNetCloud"

$manifestContent = Get-Content -Path $manifestTemplate -Raw
$manifestContent = $manifestContent.Replace("__IDENTITY_NAME__", $PackageIdentityName)
$manifestContent = $manifestContent.Replace("__PUBLISHER__", $Publisher)
$manifestContent = $manifestContent.Replace("__VERSION__", $msixVersion)
$manifestContent = $manifestContent.Replace("__DISPLAY_NAME__", $PackageDisplayName)
$manifestContent = $manifestContent.Replace("__PUBLISHER_DISPLAY_NAME__", $PublisherDisplayName)
$manifestContent = $manifestContent.Replace("__EXECUTABLE__", "SyncTray/dotnetcloud-sync-tray.exe")

Set-Content -Path (Join-Path $stagingRoot "AppxManifest.xml") -Value $manifestContent -Encoding UTF8

Write-Host "[3/4] Packing MSIX..." -ForegroundColor Yellow

$makeAppx = Get-WindowsSdkTool -ToolName "makeappx.exe"
if (Test-Path $outputMsix) {
    Remove-Item -Path $outputMsix -Force
}

& "$makeAppx" pack /d "$stagingRoot" /p "$outputMsix" /o

if ($LASTEXITCODE -ne 0) {
    throw "makeappx.exe failed with exit code $LASTEXITCODE."
}

$resolvedCertificatePath = $CertificatePath
if ($CreateTestCertificate -and [string]::IsNullOrWhiteSpace($resolvedCertificatePath)) {
    $resolvedCertificatePath = Join-Path $outputRoot "dotnetcloud-sync-tray-test-signing.pfx"
    if ([string]::IsNullOrWhiteSpace($CertificatePassword)) {
        $CertificatePassword = "dotnetcloud-dev"
    }

    Write-Host "[4/4] Creating test signing certificate..." -ForegroundColor Yellow
    New-TestCertificate -PublisherSubject $Publisher -CertificatePfxPath $resolvedCertificatePath -Password $CertificatePassword | Out-Null
}

if ($Sign) {
    if ([string]::IsNullOrWhiteSpace($resolvedCertificatePath)) {
        throw "Signing requested but no certificate provided. Use -CertificatePath or -CreateTestCertificate."
    }

    if (-not (Test-Path $resolvedCertificatePath)) {
        throw "Certificate file not found: $resolvedCertificatePath"
    }

    if ([string]::IsNullOrWhiteSpace($CertificatePassword)) {
        throw "Signing requested but -CertificatePassword was not provided."
    }

    Write-Host "[4/4] Signing MSIX..." -ForegroundColor Yellow
    $signtool = Get-WindowsSdkTool -ToolName "signtool.exe"
    & "$signtool" sign /fd SHA256 /f "$resolvedCertificatePath" /p "$CertificatePassword" "$outputMsix"

    if ($LASTEXITCODE -ne 0) {
        throw "signtool.exe failed with exit code $LASTEXITCODE."
    }
}
else {
    Write-Host "[4/4] Skipping signing (unsigned package)." -ForegroundColor Yellow
}

Write-Host "`nMSIX build complete." -ForegroundColor Green
Write-Host "Package: $outputMsix"

if ($Sign) {
    Write-Host "Status: signed" -ForegroundColor Green
}
else {
    Write-Host "Status: unsigned (must be signed with trusted certificate before install)" -ForegroundColor DarkYellow
}
