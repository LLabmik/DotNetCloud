#!/usr/bin/env pwsh
<#
.SYNOPSIS
Deploys the static dotnetcloud.net websites (`www` and `modules`) onto the IIS host.

.DESCRIPTION
Both sites are plain static sites served by IIS (no managed code, no app pool beyond the file-system
read identity). This script is the deploy contract for them and runs ON the IIS host.

Each site uses a release + junction layout so a rollback is a pointer swap, never a rebuild:

    <SiteRoot>\releases\<yyyyMMdd-HHmmss>\     immutable content snapshot
    <SiteRoot>\current -> releases\<...>       directory junction, this is the IIS physical path

The IIS site's physical path must be `<SiteRoot>\current`. The first deploy onto an existing
hand-copied site therefore requires a one-off change of that physical path to the `current`
junction - see docs/admin/SITES.md.

Site content is composed from the two content repos the core repo does not own:

    www      DotNetCloud-WebSite\                 -> release root
    modules  DotNetCloud.ModuleStore\site\         -> release root
             DotNetCloud.ModuleStore\index\        -> release root\index
             DotNetCloud.ModuleStore\packages\     -> release root\packages
             DotNetCloud.ModuleStore\publishers.json, yanked.json -> release root

`web.config` is NOT taken from the content repos: the file in this repo
(`deploy/sites/<site>/web.config`) is copied into the release last, so redirects, MIME maps and
cache headers are versioned here and cannot drift per checkout.

.PARAMETER Site
Which site to deploy. Default: all.

.PARAMETER WwwSource
Content root of the marketing site checkout (`DotNetCloud-WebSite`).
Default: C:\Src\DotNetCloud-WebSite

.PARAMETER ModulesCatalogueSource
Store repository root holding `index\`, `packages\`, `publishers.json` and `yanked.json`
(`DotNetCloud.ModuleStore`). Default: C:\Src\DotNetCloud.ModuleStore

.PARAMETER ModulesFrontendSource
Store frontend directory (`index.html`, `package.html`, `publisher.html`, `store.css`, `store.js`).
Default: <ModulesCatalogueSource>\site

.PARAMETER WwwSiteRoot
Deploy root of the www site. Default: C:\inetpub\Apps\DotNetCloud-WebSite

.PARAMETER ModulesSiteRoot
Deploy root of the modules site. Default: C:\inetpub\Apps\DotNetCloud-Modules

.PARAMETER SiteConfigRoot
Directory holding the per-site `web.config` files. Default: <repo>\deploy\sites

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
Lists the releases on disk for the selected site(s) and exits.

.PARAMETER Verify
Runs the post-deploy smoke checks against the live HTTPS site: every path returns 200, package
files are served as `application/octet-stream`, and the http->https / apex->www redirects are in
place. Any failure makes the script exit non-zero.

.PARAMETER VerifyOnly
Runs the smoke checks and exits without deploying anything. Use it to check a running site, or to
re-run the smoke list after a manual change.

.PARAMETER WwwBaseUrl
Base URL used by -Verify for the www site. Default: https://www.dotnetcloud.net

.PARAMETER ModulesBaseUrl
Base URL used by -Verify for the modules site. Default: https://modules.dotnetcloud.net

.EXAMPLE
PS> .\scripts\deploy-sites.ps1 -Site modules -Verify

Builds a new modules release, swaps `current`, runs the store smoke checks.

.EXAMPLE
PS> .\scripts\deploy-sites.ps1 -Site all -WhatIf

Prints the robocopy, junction swap and retention actions without changing anything.

.EXAMPLE
PS> .\scripts\deploy-sites.ps1 -Site all -VerifyOnly

Runs the smoke checks against both live sites without deploying.

.EXAMPLE
PS> .\scripts\deploy-sites.ps1 -Site modules -ListReleases

Lists the releases and marks the one `current` points at.

.EXAMPLE
PS> .\scripts\deploy-sites.ps1 -Site modules -Rollback

.EXAMPLE
PS> .\scripts\deploy-sites.ps1 -Site www -RollbackTo 20261108-120000
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [ValidateSet('www', 'modules', 'all')]
    [string]$Site = 'all',

    [string]$WwwSource,
    [string]$ModulesCatalogueSource,
    [string]$ModulesFrontendSource,

    [string]$WwwSiteRoot = 'C:\inetpub\Apps\DotNetCloud-WebSite',
    [string]$ModulesSiteRoot = 'C:\inetpub\Apps\DotNetCloud-Modules',

    [string]$SiteConfigRoot,

    [string]$ReleaseName,
    [int]$KeepReleases = 5,

    [switch]$Rollback,
    [string]$RollbackTo,
    [switch]$ListReleases,

    [switch]$Verify,
    [switch]$VerifyOnly,

    [string]$WwwBaseUrl = 'https://www.dotnetcloud.net',
    [string]$ModulesBaseUrl = 'https://modules.dotnetcloud.net'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------------------------
# Defaults that depend on other parameters
# ---------------------------------------------------------------------------------------------

if (-not $WwwSource) { $WwwSource = 'C:\Src\DotNetCloud-WebSite' }
if (-not $ModulesCatalogueSource) { $ModulesCatalogueSource = 'C:\Src\DotNetCloud.ModuleStore' }
if (-not $ModulesFrontendSource) {
    # Path::Combine, not Join-Path: the default catalogue root is a drive-qualified Windows path and
    # Join-Path rejects a drive that does not exist on the current host.
    $ModulesFrontendSource = [System.IO.Path]::Combine($ModulesCatalogueSource, 'site')
}
if (-not $SiteConfigRoot) {
    $SiteConfigRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../deploy/sites'))
}
if ($RollbackTo) { $Rollback = $true }
if ($Rollback -and $Verify) {
    throw '-Rollback and -Verify cannot be combined: verify after the rollback has been reviewed.'
}
if ($Rollback -and $ReleaseName) {
    throw '-Rollback and -ReleaseName cannot be combined.'
}
if ($Rollback -and $VerifyOnly) {
    throw '-Rollback and -VerifyOnly cannot be combined.'
}
if ($ListReleases -and $VerifyOnly) {
    throw '-ListReleases and -VerifyOnly cannot be combined.'
}

# ---------------------------------------------------------------------------------------------
# Site definitions
# ---------------------------------------------------------------------------------------------

$siteDefinitions = @{
    www     = @{
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
    modules = @{
        DisplayName = 'modules.dotnetcloud.net'
        Root        = $ModulesSiteRoot
        BaseUrl     = $ModulesBaseUrl
        ConfigDir   = Join-Path $SiteConfigRoot 'modules'
        Sources     = @(
            @{ Kind = 'dir'; Path = $ModulesFrontendSource; Dest = '.'; Required = $true; Label = 'store frontend (site)' },
            @{ Kind = 'dir'; Path = [System.IO.Path]::Combine($ModulesCatalogueSource, 'index'); Dest = 'index'; Required = $true; Label = 'catalogue index' },
            @{ Kind = 'dir'; Path = [System.IO.Path]::Combine($ModulesCatalogueSource, 'packages'); Dest = 'packages'; Required = $true; Label = 'catalogue packages' },
            @{ Kind = 'file'; Path = [System.IO.Path]::Combine($ModulesCatalogueSource, 'publishers.json'); Dest = 'publishers.json'; Required = $true; Label = 'publishers.json' },
            @{ Kind = 'file'; Path = [System.IO.Path]::Combine($ModulesCatalogueSource, 'yanked.json'); Dest = 'yanked.json'; Required = $false; Label = 'yanked.json' }
        )
        SmokePaths  = @(
            '/',
            '/index.html',
            '/package.html',
            '/publisher.html',
            '/index/v1/index.json',
            '/index/v1/index.json.sig',
            '/publishers.json',
            '/yanked.json'
        )
        Redirects   = @(
            @{ From = 'http://modules.dotnetcloud.net/'; To = 'https://modules.dotnetcloud.net/' }
        )
    }
}

$selectedSites = switch ($Site) {
    'all' { @('www', 'modules') }
    default { @($Site) }
}

# ---------------------------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------------------------

function Write-Head {
    param([string]$Text)
    Write-Host ''
    Write-Host "== $Text" -ForegroundColor Cyan
}

function Write-Step {
    param([string]$Text)
    Write-Host "   $Text"
}

function Write-Warn {
    param([string]$Text)
    Write-Host "   WARNING: $Text" -ForegroundColor Yellow
}

function Write-Fail {
    param([string]$Text)
    Write-Host "   FAILED: $Text" -ForegroundColor Red
}

# Returns $true when the action should be carried out, $false (after printing) under -WhatIf.
function Confirm-Action {
    param([string]$Description)

    if ($WhatIfPreference) {
        Write-Host "   [whatif] $Description" -ForegroundColor DarkGray
        return $false
    }
    Write-Step $Description
    return $true
}

function Get-ReleasesRoot {
    param([string]$SiteRoot)
    return (Join-Path $SiteRoot 'releases')
}

function Get-CurrentPath {
    param([string]$SiteRoot)
    return (Join-Path $SiteRoot 'current')
}

# Returns the release directory names present on disk, oldest first.
function Get-ReleaseNames {
    param([string]$SiteRoot)

    $releasesRoot = Get-ReleasesRoot -SiteRoot $SiteRoot
    if (-not (Test-Path -LiteralPath $releasesRoot)) { return @() }

    return @(
        Get-ChildItem -LiteralPath $releasesRoot -Directory |
            Where-Object { $_.Name -match '^\d{8}-\d{6}' } |
            Sort-Object -Property Name |
            ForEach-Object { $_.Name }
    )
}

# Returns the release name `current` points at, or $null. Tolerates a broken junction.
function Get-CurrentReleaseName {
    param([string]$SiteRoot)

    $currentPath = Get-CurrentPath -SiteRoot $SiteRoot
    try {
        $item = Get-Item -LiteralPath $currentPath -Force -ErrorAction Stop
    }
    catch {
        return $null
    }
    if ($null -eq $item -or $null -eq $item.LinkType) { return $null }

    $target = @($item.Target)
    if ($target.Count -eq 0 -or -not $target[0]) { return $null }
    return (Split-Path -Leaf $target[0])
}

# Removes a directory junction/symlink without touching its target. Never deletes a real directory.
function Remove-DirectoryLink {
    param([string]$Path)

    if (Test-Path -LiteralPath $Path) {
        $item = Get-Item -LiteralPath $Path -Force
        if ($null -eq $item.LinkType) {
            throw "Refusing to remove '$Path': it exists and is not a junction or symbolic link."
        }
        [System.IO.Directory]::Delete($Path, $false)
        return
    }

    # Test-Path follows the link, so a junction whose target was deleted looks absent here.
    # Directory::Delete on that path removes the reparse point itself.
    try {
        [System.IO.Directory]::Delete($Path, $false)
    }
    catch [System.IO.IOException] {
        # Nothing there either - that is the desired end state.
    }
}

function Set-CurrentRelease {
    param(
        [string]$SiteRoot,
        [string]$ReleaseDirectoryName
    )

    $releasesRoot = Get-ReleasesRoot -SiteRoot $SiteRoot
    $targetPath = Join-Path $releasesRoot $ReleaseDirectoryName
    if (-not (Test-Path -LiteralPath $targetPath)) {
        throw "Release '$ReleaseDirectoryName' does not exist under '$releasesRoot'."
    }

    $currentPath = Get-CurrentPath -SiteRoot $SiteRoot
    if (-not (Confirm-Action "Point '$currentPath' at '$targetPath'")) { return }

    Remove-DirectoryLink -Path $currentPath
    New-Item -ItemType Junction -Path $currentPath -Target $targetPath | Out-Null

    # New-Item can report success while creating nothing (e.g. on a platform without junction
    # support), which would leave IIS pointing at a directory that no longer exists.
    $created = Get-Item -LiteralPath $currentPath -Force -ErrorAction SilentlyContinue
    if ($null -eq $created -or $null -eq $created.LinkType) {
        throw "Could not create the 'current' junction at '$currentPath'."
    }
    Write-Step "current -> $ReleaseDirectoryName"
}

# Runs robocopy /MIR and throws when it reports a real failure (exit code >= 8).
function Invoke-Robocopy {
    param(
        [string]$Source,
        [string]$Destination
    )

    $arguments = @(
        $Source,
        $Destination,
        '/MIR',
        '/XJ',
        '/R:2',
        '/W:2',
        '/NP',
        '/NFL',
        '/NDL',
        '/NJH',
        '/NJS',
        '/XF', 'thumbs.db', 'Thumbs.db', 'desktop.ini', '.DS_Store'
    )

    & robocopy.exe @arguments | Out-Null
    $exitCode = $LASTEXITCODE

    # robocopy: 0 = nothing to do, 1-7 = copied/mirrored fine, >= 8 = failure.
    if ($exitCode -ge 8) {
        throw "robocopy failed with exit code $exitCode ($Source -> $Destination)."
    }
}

function New-SiteRelease {
    param(
        [hashtable]$Definition,
        [string]$ReleaseNameToUse,
        [string]$ReleasesRoot
    )

    $releasePath = Join-Path $ReleasesRoot $ReleaseNameToUse
    if (Test-Path -LiteralPath $releasePath) {
        throw "Release '$ReleaseNameToUse' already exists for $($Definition.DisplayName). Pass -ReleaseName to pick another."
    }

    Write-Step "release directory: $releasePath"

    # Compose the release from the content sources, then lay our own web.config over the top.
    foreach ($source in $Definition.Sources) {
        if (-not (Test-Path -LiteralPath $source.Path)) {
            if ($source.Required) {
                throw "Missing $($source.Label): '$($source.Path)'."
            }
            Write-Warn "skipping optional $($source.Label) ('$($source.Path)' not found)"
            continue
        }

        if ($source.Kind -eq 'dir') {
            $destination = if ($source.Dest -eq '.') { $releasePath } else { Join-Path $releasePath $source.Dest }
            Write-Step "robocopy /MIR $($source.Path) -> $destination"
            Invoke-Robocopy -Source $source.Path -Destination $destination
        }
        else {
            $destinationFile = Join-Path $releasePath $source.Dest
            $destinationDirectory = Split-Path -Parent $destinationFile
            if (-not (Test-Path -LiteralPath $destinationDirectory)) {
                New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
            }
            Write-Step "copy $($source.Path) -> $destinationFile"
            Copy-Item -LiteralPath $source.Path -Destination $destinationFile -Force
        }
    }

    $webConfigSource = Join-Path $Definition.ConfigDir 'web.config'
    if (-not (Test-Path -LiteralPath $webConfigSource)) {
        throw "Missing site configuration '$webConfigSource'."
    }
    Write-Step "copy $webConfigSource -> $releasePath\web.config"
    Copy-Item -LiteralPath $webConfigSource -Destination (Join-Path $releasePath 'web.config') -Force

    # The ACME HTTP-01 webroot lives inside the site root, so it must exist in every release:
    # after the swap, <root>\current\ is what IIS serves and what an ACME client writes to. Without
    # this, certificate renewal would 404 in ~90 days, long after anyone remembers the swap.
    $acmeChallenge = Join-Path $releasePath '.well-known\acme-challenge'
    if (-not (Test-Path -LiteralPath $acmeChallenge)) {
        New-Item -ItemType Directory -Path $acmeChallenge -Force | Out-Null
        Write-Step 'created .well-known\acme-challenge (ACME webroot)'
    }

    $payload = @(Get-ChildItem -LiteralPath $releasePath -Force)
    if ($payload.Count -eq 0) {
        throw "Release '$ReleaseNameToUse' ended up empty - refusing to activate it."
    }
    Write-Step "release contains $(($payload).Count) top-level entries"

    return $releasePath
}

function Remove-OldReleases {
    param(
        [string]$SiteRoot,
        [string]$KeepReleaseName,
        [int]$KeepCount
    )

    if ($KeepCount -le 0) { return }

    $releasesRoot = Get-ReleasesRoot -SiteRoot $SiteRoot
    $names = @(Get-ReleaseNames -SiteRoot $SiteRoot)
    if ($names.Count -le $KeepCount) { return }

    $protected = @($KeepReleaseName)
    $currentName = Get-CurrentReleaseName -SiteRoot $SiteRoot
    if ($currentName) { $protected += $currentName }

    $excess = $names.Count - $KeepCount
    $delete = @($names | Where-Object { $protected -notcontains $_ } | Select-Object -First $excess)
    Assert-ReleaseDeletable -ReleaseNames $delete

    foreach ($name in $delete) {
        $path = Join-Path $releasesRoot $name
        if (Confirm-Action "delete old release '$name'") {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }
}

# Guard for every recursive delete: only touch real directories that live under releases\.
function Assert-ReleaseDeletable {
    param([string[]]$ReleaseNames)

    if (-not $ReleaseNames) { return }
    foreach ($name in $ReleaseNames) {
        if ($name -notmatch '^\d{8}-\d{6}') {
            throw "Refusing to delete '$name': it is not a release directory name."
        }
    }
}

# ---------------------------------------------------------------------------------------------
# Smoke checks
# ---------------------------------------------------------------------------------------------

function Test-HttpRedirect {
    param(
        [string]$Url,
        [string]$ExpectedLocation
    )

    $request = [System.Net.HttpWebRequest]::Create($Url)
    $request.Method = 'GET'
    $request.AllowAutoRedirect = $false
    $request.Timeout = 20000
    $request.UserAgent = 'dotnetcloud-deploy-sites-smoke'

    $response = $null
    try {
        $response = $request.GetResponse()
    }
    catch [System.Net.WebException] {
        if ($_.Exception.Response) {
            $response = $_.Exception.Response
        }
        else {
            return "request failed: $($_.Exception.Message)"
        }
    }

    try {
        $status = [int]$response.StatusCode
        if ($status -lt 300 -or $status -ge 400) {
            return "expected a 301/302/307/308 redirect, got HTTP $status"
        }

        $location = [string]$response.Headers['Location']
        if (-not $location) { return 'redirect had no Location header' }

        $normalised = $location.TrimEnd('/')
        $expected = $ExpectedLocation.TrimEnd('/')
        if ($normalised -ne $expected) {
            return "redirected to '$location' instead of '$ExpectedLocation'"
        }
        return $null
    }
    finally {
        if ($response) { $response.Close() }
    }
}

function Invoke-SmokeTest {
    param(
        [hashtable]$Definition
    )

    $failures = New-Object 'System.Collections.Generic.List[string]'
    $baseUrl = $Definition.BaseUrl.TrimEnd('/')

    Write-Head "Smoke checks: $($Definition.DisplayName)"

    foreach ($path in $Definition.SmokePaths) {
        $uri = "$baseUrl$path"
        try {
            $response = Invoke-WebRequest -Uri $uri -Method Head -TimeoutSec 30 -UseBasicParsing -ErrorAction Stop
            $status = [int]$response.StatusCode
            if ($status -ne 200) {
                $failures.Add("$uri returned HTTP $status")
                Write-Fail "$uri -> HTTP $status"
                continue
            }

            $contentType = [string]($response.Headers['Content-Type'])
            if ($path -like '*.dncpkg' -or $path -like '*.sig') {
                if (-not $contentType.StartsWith('application/octet-stream')) {
                    $failures.Add("$uri served as '$contentType' (expected application/octet-stream)")
                    Write-Fail "$uri -> $contentType (MIME map missing?)"
                    continue
                }
            }

            Write-Step "OK  $uri ($contentType)"
        }
        catch {
            $failures.Add("$uri failed: $($_.Exception.Message)")
            Write-Fail "$uri -> $($_.Exception.Message)"
        }
    }

    # The published package itself is the check that proves the .dncpkg MIME map is live.
    if ($Definition.ContainsKey('SmokePackageProbe') -and $Definition.SmokePackageProbe) {
        try {
            $index = Invoke-RestMethod -Uri "$baseUrl/index/v1/index.json" -TimeoutSec 30 -UseBasicParsing
            $package = @($index.packages)[0]
            $version = @($package.versions)[0]
            if ($package -and $version -and $version.downloadUrl) {
                $packageUri = "$baseUrl/" + ([string]$version.downloadUrl).TrimStart('/')
                $probe = Invoke-WebRequest -Uri $packageUri -Method Head -TimeoutSec 30 -UseBasicParsing -ErrorAction Stop
                $probeType = [string]($probe.Headers['Content-Type'])
                if (-not $probeType.StartsWith('application/octet-stream')) {
                    $failures.Add("$packageUri served as '$probeType' (expected application/octet-stream)")
                    Write-Fail "package probe $packageUri -> $probeType"
                }
                else {
                    Write-Step "OK  package probe $packageUri ($probeType, $($probe.Headers['Content-Length']) bytes)"
                }
            }
            else {
                Write-Warn 'no package in the catalogue to probe'
            }
        }
        catch {
            $failures.Add("package probe failed: $($_.Exception.Message)")
            Write-Fail "package probe failed: $($_.Exception.Message)"
        }
    }

    foreach ($redirect in $Definition.Redirects) {
        $problem = Test-HttpRedirect -Url $redirect.From -ExpectedLocation $redirect.To
        if ($problem) {
            $failures.Add("$($redirect.From) -> $($redirect.To): $problem")
            Write-Fail "$($redirect.From) -> $problem"
        }
        else {
            Write-Step "OK  $($redirect.From) -> $($redirect.To)"
        }
    }

    return $failures
}

# ---------------------------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------------------------

$definitionsToUse = @{}
foreach ($key in $selectedSites) {
    $definition = $siteDefinitions[$key]
    if ($key -eq 'modules') {
        $definition.SmokePackageProbe = $true
    }
    $definitionsToUse[$key] = $definition
}

try {
    # ---------------------------------------------------------------- list
    if ($ListReleases) {
        foreach ($key in $selectedSites) {
            $definition = $definitionsToUse[$key]
            Write-Head "$($definition.DisplayName) releases ($(Get-ReleasesRoot -SiteRoot $definition.Root))"
            $currentName = Get-CurrentReleaseName -SiteRoot $definition.Root
            $names = @(Get-ReleaseNames -SiteRoot $definition.Root)
            if ($names.Count -eq 0) {
                Write-Step '(none)'
                continue
            }
            foreach ($name in $names) {
                $marker = if ($name -eq $currentName) { '  <- current' } else { '' }
                Write-Step "$name$marker"
            }
        }
        exit 0
    }

    # ---------------------------------------------------------------- rollback
    if ($Rollback) {
        foreach ($key in $selectedSites) {
            $definition = $definitionsToUse[$key]
            Write-Head "Rollback $($definition.DisplayName)"

            $names = @(Get-ReleaseNames -SiteRoot $definition.Root)
            if ($names.Count -eq 0) {
                throw "No releases found for $($definition.DisplayName) under '$(Get-ReleasesRoot -SiteRoot $definition.Root)'."
            }

            if ($RollbackTo) {
                if ($names -notcontains $RollbackTo) {
                    throw "Release '$RollbackTo' does not exist for $($definition.DisplayName). Available: $($names -join ', ')"
                }
                $target = $RollbackTo
            }
            else {
                $currentName = Get-CurrentReleaseName -SiteRoot $definition.Root
                if (-not $currentName) {
                    throw "No 'current' junction for $($definition.DisplayName); pass -RollbackTo."
                }
                $previous = @($names | Where-Object { $_ -lt $currentName })
                if ($previous.Count -eq 0) {
                    throw "No release older than the current one ('$currentName'); pass -RollbackTo."
                }
                $target = $previous[-1]
            }

            Write-Step "current release: $(Get-CurrentReleaseName -SiteRoot $definition.Root)"
            Set-CurrentRelease -SiteRoot $definition.Root -ReleaseDirectoryName $target
        }
        exit 0
    }

    # ---------------------------------------------------------------- smoke only
    if ($VerifyOnly) {
        $failures = New-Object 'System.Collections.Generic.List[string]'
        foreach ($key in $selectedSites) {
            foreach ($failure in (Invoke-SmokeTest -Definition $definitionsToUse[$key])) {
                $failures.Add($failure)
            }
        }
        if ($failures.Count -gt 0) {
            Write-Head 'Smoke check failures'
            foreach ($failure in $failures) { Write-Fail $failure }
            throw "$($failures.Count) smoke check(s) failed."
        }
        Write-Host ''
        Write-Host 'All smoke checks passed.' -ForegroundColor Green
        exit 0
    }

    # ---------------------------------------------------------------- pre-flight
    Write-Head 'Pre-flight'
    foreach ($key in $selectedSites) {
        $definition = $definitionsToUse[$key]
        foreach ($source in $definition.Sources) {
            if (-not (Test-Path -LiteralPath $source.Path)) {
                if ($source.Required) {
                    throw "Missing $($source.Label) for $($definition.DisplayName): '$($source.Path)'. Pass the matching -Source parameter."
                }
                Write-Warn "optional $($source.Label) not found at '$($source.Path)'"
            }
        }
        $webConfigSource = Join-Path $definition.ConfigDir 'web.config'
        if (-not (Test-Path -LiteralPath $webConfigSource)) {
            throw "Missing site configuration '$webConfigSource'."
        }
        Write-Step "OK  $($definition.DisplayName) <- $($definition.ConfigDir)"
    }

    # ---------------------------------------------------------------- deploy
    $releaseNameToUse = if ($ReleaseName) { $ReleaseName } else { (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss') }
    if ($releaseNameToUse -notmatch '^\d{8}-\d{6}') {
        throw "ReleaseName '$releaseNameToUse' must look like yyyyMMdd-HHmmss."
    }

    $smokeFailures = New-Object 'System.Collections.Generic.List[string]'

    foreach ($key in $selectedSites) {
        $definition = $definitionsToUse[$key]
        Write-Head "Deploy $($definition.DisplayName) (release $releaseNameToUse)"

        $siteRoot = $definition.Root
        $releasesRoot = Get-ReleasesRoot -SiteRoot $siteRoot

        if (-not (Test-Path -LiteralPath $releasesRoot)) {
            if (Confirm-Action "create deploy root '$releasesRoot'") {
                New-Item -ItemType Directory -Path $releasesRoot -Force | Out-Null
            }
        }

        $swapped = $false
        $releaseBuilt = $false
        try {
            if ($WhatIfPreference) {
                Write-Host "   [whatif] build release '$releaseNameToUse' from $($definition.Sources.Count) source(s)" -ForegroundColor DarkGray
                foreach ($source in $definition.Sources) {
                    Write-Host "   [whatif] $($source.Kind): $($source.Path) -> $($source.Dest)" -ForegroundColor DarkGray
                }
                Write-Host "   [whatif] copy $(Join-Path $definition.ConfigDir 'web.config') -> release root" -ForegroundColor DarkGray
                Write-Host "   [whatif] point '$(Get-CurrentPath -SiteRoot $siteRoot)' at '$releaseNameToUse'" -ForegroundColor DarkGray
                continue
            }

            New-SiteRelease -Definition $definition -ReleaseNameToUse $releaseNameToUse -ReleasesRoot $releasesRoot
            $releaseBuilt = $true
            Set-CurrentRelease -SiteRoot $siteRoot -ReleaseDirectoryName $releaseNameToUse
            $swapped = $true

            if ($Verify) {
                foreach ($failure in (Invoke-SmokeTest -Definition $definition)) {
                    $smokeFailures.Add("[$($definition.DisplayName)] $failure")
                }
            }

            Remove-OldReleases -SiteRoot $siteRoot -KeepReleaseName $releaseNameToUse -KeepCount $KeepReleases
        }
        catch {
            Write-Fail $_.Exception.Message

            if (-not $swapped -and $releaseBuilt) {
                # The snapshot is good, only the swap failed: leave it on disk so it can be
                # activated once the junction problem is fixed.
                Write-Warn "release '$releaseNameToUse' was built but is NOT live: '$(Get-CurrentPath -SiteRoot $siteRoot)' could not be pointed at it."
            }
            elseif (-not $swapped) {
                # The release was never completed: discard the partial snapshot, leave `current` alone.
                $partialPath = Join-Path $releasesRoot $releaseNameToUse
                if (Test-Path -LiteralPath $partialPath) {
                    try {
                        Remove-Item -LiteralPath $partialPath -Recurse -Force
                        Write-Step "discarded partial release '$releaseNameToUse'"
                    }
                    catch {
                        Write-Warn "could not discard partial release '$partialPath': $($_.Exception.Message)"
                    }
                }
            }
            else {
                Write-Warn "release '$releaseNameToUse' is live; run -Rollback to return to the previous release."
            }

            throw
        }
    }

    Write-Head 'Summary'
    foreach ($key in $selectedSites) {
        $definition = $definitionsToUse[$key]
        $currentName = Get-CurrentReleaseName -SiteRoot $definition.Root
        if (-not $currentName) { $currentName = "$releaseNameToUse (not swapped)" }
        Write-Step "$($definition.DisplayName): current -> $currentName"
    }

    if ($smokeFailures.Count -gt 0) {
        Write-Head 'Smoke check failures'
        foreach ($failure in $smokeFailures) { Write-Fail $failure }
        throw "$($smokeFailures.Count) smoke check(s) failed."
    }

    Write-Host ''
    if ($WhatIfPreference) {
        Write-Host 'Dry run complete - nothing was changed.' -ForegroundColor Green
    }
    else {
        Write-Host 'Deploy complete.' -ForegroundColor Green
    }
    exit 0
}
catch {
    Write-Host ''
    Write-Host "Failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
