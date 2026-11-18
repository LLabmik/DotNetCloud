#!/usr/bin/env pwsh
<#
.SYNOPSIS
Deploys the static `www.dotnetcloud.net` site onto the IIS host.

.DESCRIPTION
Runs ON the IIS host, like every deploy script here. The release/junction layout, retention,
rollback and smoke-check machinery is shared with the other dotnetcloud.net site and lives in
`scripts/lib/SiteDeployment.psm1`; this script only describes *this* site:

    www.dotnetcloud.net   content: DotNetCloud-WebSite checkout
                          config : deploy/sites/www/web.config

`modules.dotnetcloud.net` is deployed by the `DotNetCloud-ModuleStore` repository
(`scripts/deploy-site.ps1` there), which imports the same module - one implementation of the swap,
two entry points. Each site uses a release + junction layout so a rollback is a pointer swap:

    <SiteRoot>\releases\<yyyyMMdd-HHmmss>\     immutable content snapshot
    <SiteRoot>\current -> releases\<...>       directory junction, this is the IIS physical path

The IIS site's physical path must be `<SiteRoot>\current`; the first deploy onto a hand-copied site
needs a one-off change of that path - see docs/admin/SITES.md.

`web.config` is NOT taken from the content repository: `deploy/sites/www/web.config` is copied into
the release last, so redirects, MIME maps and cache headers are versioned here and cannot drift per
checkout.

.PARAMETER WwwSource
Content root of the marketing site checkout (`DotNetCloud-WebSite`).
Default: C:\Src\DotNetCloud-WebSite

.PARAMETER WwwSiteRoot
Deploy root of the www site. Default: C:\inetpub\Apps\DotNetCloud-WebSite

.PARAMETER SiteConfigRoot
Directory holding the per-site `web.config`. Default: <repo>\deploy\sites

.PARAMETER ReleaseName
Release directory name to create, `yyyyMMdd-HHmmss` format. Default: the current UTC time.
Fails if that release already exists - re-deploying always creates a new release.

.PARAMETER KeepReleases
How many newest releases to keep after a successful deploy. 0 keeps every release. Default: 5.
The current release and the one before it are never deleted.

.PARAMETER Rollback
Re-points `current` at the previous release instead of deploying. Use -RollbackTo to pick a
specific one.

.PARAMETER RollbackTo
Release directory name to roll back to (implies -Rollback).

.PARAMETER ListReleases
Lists the releases on disk and exits.

.PARAMETER Verify
Runs the post-deploy smoke checks against the live HTTPS site: every path returns 200 and the
http->https / apex->www redirects are in place. Any failure makes the script exit non-zero.

.PARAMETER VerifyOnly
Runs the smoke checks and exits without deploying anything.

.PARAMETER WwwBaseUrl
Base URL used by -Verify. Default: https://www.dotnetcloud.net

.EXAMPLE
PS> .\scripts\deploy-sites.ps1 -Verify

Builds a new www release, swaps `current`, runs the smoke checks.

.EXAMPLE
PS> .\scripts\deploy-sites.ps1 -WhatIf

Prints the robocopy, junction swap and retention actions without changing anything.

.EXAMPLE
PS> .\scripts\deploy-sites.ps1 -Rollback

Re-points `current` at the previous release.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$WwwSource,

    [string]$WwwSiteRoot = 'C:\inetpub\Apps\DotNetCloud-WebSite',
    [string]$SiteConfigRoot,

    [string]$ReleaseName,
    [int]$KeepReleases = 5,

    [switch]$Rollback,
    [string]$RollbackTo,
    [switch]$ListReleases,

    [switch]$Verify,
    [switch]$VerifyOnly,

    [string]$WwwBaseUrl = 'https://www.dotnetcloud.net'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $WwwSource) { $WwwSource = 'C:\Src\DotNetCloud-WebSite' }
if (-not $SiteConfigRoot) {
    $SiteConfigRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../deploy/sites'))
}

$sites = [ordered]@{
    www = @{
        DisplayName = 'www.dotnetcloud.net'
        Root        = $WwwSiteRoot
        BaseUrl     = $WwwBaseUrl
        ConfigDir   = Join-Path $SiteConfigRoot 'www'
        Sources     = @(
            @{ Kind = 'dir'; Path = $WwwSource; Dest = '.'; Required = $true; Label = 'DotNetCloud-WebSite' }
        )
        SmokePaths  = @('/', '/index.html', '/modules.html')
        Redirects   = @(
            @{ From = 'http://www.dotnetcloud.net/'; To = 'https://www.dotnetcloud.net/' },
            @{ From = 'http://dotnetcloud.net/'; To = 'https://www.dotnetcloud.net/' },
            @{ From = 'https://dotnetcloud.net/'; To = 'https://www.dotnetcloud.net/' }
        )
    }
}

Import-Module (Join-Path $PSScriptRoot 'lib/SiteDeployment.psm1') -Force

try {
    Invoke-SiteDeployment -Definitions $sites `
        -ReleaseName $ReleaseName `
        -KeepReleases $KeepReleases `
        -Rollback:$Rollback `
        -RollbackTo $RollbackTo `
        -ListReleases:$ListReleases `
        -Verify:$Verify `
        -VerifyOnly:$VerifyOnly `
        -WhatIf:$WhatIfPreference
}
catch {
    Write-Host ''
    Write-Host "Failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
