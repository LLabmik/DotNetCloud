<#
DotNetCloud - Windows + IIS Install Script

This script installs DotNetCloud on Windows with IIS acting as a reverse proxy
to the internal Kestrel listener on http://localhost:5080.

It intentionally does not modify tools/install.sh. This is the dedicated
Windows installation path for self-hosting on Windows Server / Windows 10+.
#>

[CmdletBinding()]
param(
    [string]$SourcePath,
    [string]$InstallRoot = "$env:ProgramFiles\DotNetCloud",
    [string]$DataRoot = "$env:ProgramData\DotNetCloud",
    [string]$SiteName = "DotNetCloud",
    [string]$AppPoolName = "DotNetCloud",
    [string]$HostName = "",
    [int]$PublicHttpPort = 80,
    [int]$KestrelHttpPort = 5080,
    [switch]$ConfigureFirewall,
    [switch]$SkipFirewall,
    [switch]$Beginner,
    [switch]$Advanced,
    [switch]$SkipFeatureInstall,
    [switch]$SkipHostingBundleInstall,
    [switch]$SkipIisConfiguration,
    [switch]$SkipServiceInstall,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$Script:HostingBundleDownloadUrl = "https://dotnet.microsoft.com/en-us/download/dotnet/10.0"
$Script:RewriteDownloadUrl = "https://www.iis.net/downloads/microsoft/url-rewrite"
$Script:ArrDownloadUrl = "https://www.iis.net/downloads/microsoft/application-request-routing"
$Script:InstallRoot = [System.IO.Path]::GetFullPath($InstallRoot)
$Script:DataRoot = [System.IO.Path]::GetFullPath($DataRoot)
$Script:ServerRoot = Join-Path $Script:InstallRoot "server"
$Script:CliRoot = Join-Path $Script:InstallRoot "cli"
$Script:ConfigRoot = Join-Path $Script:DataRoot "config"
$Script:LogsRoot = Join-Path $Script:DataRoot "logs"
$Script:StorageRoot = Join-Path $Script:DataRoot "storage"
$Script:BackupsRoot = Join-Path $Script:DataRoot "backups"
$Script:CliExe = Join-Path $Script:CliRoot "dotnetcloud.exe"
$Script:ServerExe = Join-Path $Script:ServerRoot "DotNetCloud.Core.Server.exe"
$Script:ServerDll = Join-Path $Script:ServerRoot "DotNetCloud.Core.Server.dll"
$Script:ServiceName = "DotNetCloud"
$Script:ServiceDisplayName = "DotNetCloud Core Server"
$Script:ServiceDescription = "DotNetCloud self-hosted cloud platform"

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message)
    Write-Host "[OK]   $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Write-Fail {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Assert-Administrator {
    if (-not (Test-Administrator)) {
        throw "This script must be run from an elevated PowerShell session (Run as Administrator)."
    }
}

function Get-DefaultSourcePath {
    $scriptDirectory = Split-Path -Parent $PSCommandPath
    $repoRoot = Split-Path -Parent $scriptDirectory
    $candidates = @(
        (Join-Path $repoRoot "artifacts\publish"),
        (Join-Path $repoRoot "artifacts\staging")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    return $null
}

function Resolve-PublishLayout {
    param([string]$Root)

    $resolvedRoot = [System.IO.Path]::GetFullPath($Root)
    $layouts = @(
        @{
            Server = Join-Path $resolvedRoot "server"
            Cli = Join-Path $resolvedRoot "cli"
        },
        @{
            Server = Join-Path $resolvedRoot "publish\server"
            Cli = Join-Path $resolvedRoot "publish\cli"
        }
    )

    foreach ($layout in $layouts) {
        $serverDll = Join-Path $layout.Server "DotNetCloud.Core.Server.dll"
        $cliExe = Join-Path $layout.Cli "dotnetcloud.exe"
        if ((Test-Path $serverDll) -and (Test-Path $cliExe)) {
            return $layout
        }
    }

    throw "Could not find a published DotNetCloud server + CLI layout under '$resolvedRoot'. Provide -SourcePath pointing at artifacts\publish or another published output folder."
}

function Ensure-Directory {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Ensure-WindowsFeatures {
    if ($SkipFeatureInstall) {
        Write-Warn "Skipping IIS feature installation because -SkipFeatureInstall was specified."
        return
    }

    $featureNames = @(
        "IIS-WebServerRole",
        "IIS-WebServer",
        "IIS-CommonHttpFeatures",
        "IIS-StaticContent",
        "IIS-DefaultDocument",
        "IIS-HttpErrors",
        "IIS-HttpRedirect",
        "IIS-ApplicationDevelopment",
        "IIS-ISAPIExtensions",
        "IIS-ISAPIFilter",
        "IIS-ASPNET45",
        "IIS-NetFxExtensibility45",
        "IIS-ManagementConsole",
        "IIS-RequestFiltering",
        "IIS-WebSockets"
    )

    $missing = @()
    foreach ($featureName in $featureNames) {
        $feature = Get-WindowsOptionalFeature -Online -FeatureName $featureName -ErrorAction SilentlyContinue
        if ($null -eq $feature -or $feature.State -ne "Enabled") {
            $missing += $featureName
        }
    }

    if ($missing.Count -eq 0) {
        Write-Ok "Required IIS Windows features are already enabled."
        return
    }

    Write-Info "Enabling required IIS Windows features..."
    Enable-WindowsOptionalFeature -Online -FeatureName $missing -All -NoRestart | Out-Null
    Write-Ok "Enabled IIS features: $($missing -join ', ')"
}

function Test-HostingBundleInstalled {
    $paths = @(
        "$env:ProgramFiles\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll",
        "$env:ProgramFiles(x86)\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
    )

    foreach ($path in $paths) {
        if (Test-Path $path) {
            return $true
        }
    }

    return $false
}

function Ensure-HostingBundle {
    if ($SkipHostingBundleInstall) {
        Write-Warn "Skipping ASP.NET Core Hosting Bundle installation because -SkipHostingBundleInstall was specified."
        return
    }

    if (Test-HostingBundleInstalled) {
        Write-Ok "ASP.NET Core Hosting Bundle / ANCM appears to be installed already."
        return
    }

    Write-Warn "ASP.NET Core Hosting Bundle was not detected."
    Write-Host "Install it from:" -ForegroundColor Yellow
    Write-Host "  $Script:HostingBundleDownloadUrl" -ForegroundColor Yellow
    throw "Install the .NET 10 ASP.NET Core Hosting Bundle, then re-run this script."
}

function Test-IisModuleInstalled {
    param([string]$ModuleName)

    Import-Module WebAdministration -ErrorAction Stop
    $globalModule = Get-WebGlobalModule | Where-Object { $_.Name -eq $ModuleName }
    return $null -ne $globalModule
}

function Test-WingetAvailable {
    $winget = Get-Command winget -ErrorAction SilentlyContinue
    return $null -ne $winget
}

function Install-ProxyModuleWithWinget {
    param(
        [string]$PackageName
    )

    if (-not (Test-WingetAvailable)) {
        Write-Warn "winget is not available. Cannot automatically install '$PackageName'."
        return $false
    }

    Write-Info "Attempting to install '$PackageName' via winget..."

    try {
        & winget install --name $PackageName --accept-source-agreements --accept-package-agreements --silent --disable-interactivity | Out-Null
        return $true
    }
    catch {
        Write-Warn "Automatic install attempt failed for '$PackageName'. $_"
        return $false
    }
}

function Ensure-IisModules {
    $rewriteInstalled = Test-IisModuleInstalled -ModuleName "RewriteModule"
    $arrInstalled = Test-IisModuleInstalled -ModuleName "ApplicationRequestRouting"

    if ($rewriteInstalled -and $arrInstalled) {
        Write-Ok "IIS URL Rewrite and ARR are already installed."
        return
    }

    if (-not $rewriteInstalled) {
        Install-ProxyModuleWithWinget -PackageName "IIS URL Rewrite Module 2" | Out-Null
    }

    if (-not $arrInstalled) {
        Install-ProxyModuleWithWinget -PackageName "IIS Application Request Routing 3.0" | Out-Null
    }

    # Re-check after auto-install attempts.
    $rewriteInstalled = Test-IisModuleInstalled -ModuleName "RewriteModule"
    $arrInstalled = Test-IisModuleInstalled -ModuleName "ApplicationRequestRouting"

    if ($rewriteInstalled -and $arrInstalled) {
        Write-Ok "IIS URL Rewrite and ARR are installed and ready for reverse proxy configuration."
        return
    }

    $missing = @()
    if (-not $rewriteInstalled) {
        $missing += "URL Rewrite"
    }
    if (-not $arrInstalled) {
        $missing += "Application Request Routing"
    }

    Write-Warn "Missing IIS modules: $($missing -join ', ')"
    if ($missing -contains "URL Rewrite") {
        Write-Host "  URL Rewrite: $Script:RewriteDownloadUrl" -ForegroundColor Yellow
    }
    if ($missing -contains "Application Request Routing") {
        Write-Host "  ARR:         $Script:ArrDownloadUrl" -ForegroundColor Yellow
    }

    throw "Install the missing IIS modules above, then re-run this script."
}

function Copy-Binaries {
    param(
        [string]$ServerSource,
        [string]$CliSource
    )

    Write-Info "Copying DotNetCloud binaries into the Windows install location..."
    Ensure-Directory $Script:InstallRoot
    Ensure-Directory $Script:DataRoot

    foreach ($target in @($Script:ServerRoot, $Script:CliRoot, $Script:ConfigRoot, $Script:LogsRoot, $Script:StorageRoot, $Script:BackupsRoot)) {
        Ensure-Directory $target
    }

    Copy-Item -Path (Join-Path $ServerSource "*") -Destination $Script:ServerRoot -Recurse -Force
    Copy-Item -Path (Join-Path $CliSource "*") -Destination $Script:CliRoot -Recurse -Force

    Write-Ok "Binaries copied to '$Script:InstallRoot'."
}

function Write-EnvironmentFile {
    $envFilePath = Join-Path $Script:ConfigRoot "dotnetcloud.env"
    $contents = @(
        "DOTNETCLOUD_CONFIG_DIR=$Script:ConfigRoot",
        "DOTNETCLOUD_DATA_DIR=$Script:DataRoot",
        "ASPNETCORE_ENVIRONMENT=Production",
        "DOTNET_ENVIRONMENT=Production",
        "Kestrel__HttpPort=$KestrelHttpPort",
        "Kestrel__EnableHttps=false"
    )

    Set-Content -Path $envFilePath -Value $contents -Encoding UTF8
    Write-Ok "Wrote environment file to '$envFilePath'."
}

function Invoke-SetupWizard {
    if (-not (Test-Path $Script:CliExe)) {
        throw "CLI executable not found at '$Script:CliExe'."
    }

    $mode = if ($Advanced.IsPresent) { "advanced" } else { "beginner" }
    Write-Info "Running dotnetcloud setup in $mode mode..."

    $setupArgs = @("setup")
    if (-not $Advanced.IsPresent) {
        $setupArgs += "--beginner"
    }

    $originalConfigDir = $env:DOTNETCLOUD_CONFIG_DIR
    $originalDataDir = $env:DOTNETCLOUD_DATA_DIR
    $originalAspNetEnv = $env:ASPNETCORE_ENVIRONMENT
    $originalDotNetEnv = $env:DOTNET_ENVIRONMENT

    try {
        $env:DOTNETCLOUD_CONFIG_DIR = $Script:ConfigRoot
        $env:DOTNETCLOUD_DATA_DIR = $Script:DataRoot
        $env:ASPNETCORE_ENVIRONMENT = "Production"
        $env:DOTNET_ENVIRONMENT = "Production"

        $process = Start-Process -FilePath $Script:CliExe `
            -ArgumentList $setupArgs `
            -WorkingDirectory $Script:CliRoot `
            -Wait `
            -PassThru `
            -NoNewWindow
    }
    finally {
        $env:DOTNETCLOUD_CONFIG_DIR = $originalConfigDir
        $env:DOTNETCLOUD_DATA_DIR = $originalDataDir
        $env:ASPNETCORE_ENVIRONMENT = $originalAspNetEnv
        $env:DOTNET_ENVIRONMENT = $originalDotNetEnv
    }

    if ($process.ExitCode -ne 0) {
        throw "dotnetcloud setup exited with code $($process.ExitCode)."
    }

    Write-Ok "DotNetCloud setup completed."
}

function Get-ServiceExecutablePath {
    if (Test-Path $Script:ServerExe) {
        return "`"$Script:ServerExe`""
    }

    if (Test-Path $Script:ServerDll) {
        return "`"dotnet.exe`" `"$Script:ServerDll`""
    }

    throw "Neither '$Script:ServerExe' nor '$Script:ServerDll' was found. The server publish output is incomplete."
}

function Install-WindowsService {
    if ($SkipServiceInstall) {
        Write-Warn "Skipping Windows Service installation because -SkipServiceInstall was specified."
        return
    }

    $service = Get-Service -Name $Script:ServiceName -ErrorAction SilentlyContinue
    $binaryPath = Get-ServiceExecutablePath

    if ($null -eq $service) {
        Write-Info "Creating the Windows Service '$Script:ServiceName'..."
        & sc.exe create $Script:ServiceName binPath= $binaryPath start= auto DisplayName= $Script:ServiceDisplayName | Out-Null
    }
    else {
        Write-Info "Updating the Windows Service '$Script:ServiceName'..."
        & sc.exe config $Script:ServiceName binPath= $binaryPath start= auto DisplayName= $Script:ServiceDisplayName | Out-Null
    }

    & sc.exe description $Script:ServiceName $Script:ServiceDescription | Out-Null

    $regPath = "HKLM:\SYSTEM\CurrentControlSet\Services\$Script:ServiceName"
    New-ItemProperty -Path $regPath -Name "AppDirectory" -Value $Script:ServerRoot -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $regPath -Name "Environment" -Value @(
        "DOTNETCLOUD_CONFIG_DIR=$Script:ConfigRoot",
        "DOTNETCLOUD_DATA_DIR=$Script:DataRoot",
        "ASPNETCORE_ENVIRONMENT=Production",
        "DOTNET_ENVIRONMENT=Production",
        "Kestrel__HttpPort=$KestrelHttpPort",
        "Kestrel__EnableHttps=false"
    ) -PropertyType MultiString -Force | Out-Null

    & sc.exe failure $Script:ServiceName reset= 86400 actions= restart/5000/restart/5000/restart/5000 | Out-Null

    Write-Ok "Windows Service is configured."
}

function Start-WindowsService {
    if ($SkipServiceInstall) {
        return
    }

    Write-Info "Starting DotNetCloud Windows Service..."
    Start-Service -Name $Script:ServiceName
    Start-Sleep -Seconds 3
    Write-Ok "DotNetCloud service start requested."
}

function Test-HealthEndpoint {
    param([string]$Url)

    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
        return ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400)
    }
    catch {
        return $false
    }
}

function Wait-ForHealth {
    $healthUrl = "http://localhost:$KestrelHttpPort/health/live"
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        if (Test-HealthEndpoint -Url $healthUrl) {
            Write-Ok "DotNetCloud responded on $healthUrl"
            return
        }

        Start-Sleep -Seconds 2
    }

    Write-Warn "DotNetCloud did not respond on $healthUrl yet. Continue with IIS setup, then check the Windows Service and logs if needed."
}

function Ensure-IisReverseProxyEnabled {
    Import-Module WebAdministration -ErrorAction Stop
    Set-WebConfigurationProperty -PSPath "MACHINE/WEBROOT/APPHOST" -Filter "system.webServer/proxy" -Name enabled -Value True
    Write-Ok "Enabled IIS proxy support for ARR."
}

function Remove-DefaultSiteBindingIfNeeded {
    Import-Module WebAdministration -ErrorAction Stop
    $defaultSite = Get-Website -Name "Default Web Site" -ErrorAction SilentlyContinue
    if ($null -eq $defaultSite) {
        return
    }

    if (($defaultSite.Bindings.Collection | Where-Object { $_.bindingInformation -like "*:$PublicHttpPort:*" }).Count -gt 0) {
        Write-Warn "The Default Web Site is bound to port $PublicHttpPort."
        throw "Free port $PublicHttpPort in IIS or re-run this script with a different -PublicHttpPort."
    }
}

function Ensure-AppPool {
    Import-Module WebAdministration -ErrorAction Stop
    if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
        New-WebAppPool -Name $AppPoolName | Out-Null
    }

    Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedRuntimeVersion -Value ""
    Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedPipelineMode -Value "Integrated"
    Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name processModel.identityType -Value "ApplicationPoolIdentity"
    Write-Ok "IIS application pool '$AppPoolName' is configured."
}

function Ensure-IisSite {
    if ($SkipIisConfiguration) {
        Write-Warn "Skipping IIS site configuration because -SkipIisConfiguration was specified."
        return
    }

    Import-Module WebAdministration -ErrorAction Stop
    Ensure-IisReverseProxyEnabled
    Remove-DefaultSiteBindingIfNeeded
    Ensure-AppPool

    $physicalPath = $Script:ServerRoot
    $bindingInformation = if ([string]::IsNullOrWhiteSpace($HostName)) {
        "*:$PublicHttpPort:"
    }
    else {
        "*:$PublicHttpPort:$HostName"
    }

    $site = Get-Website -Name $SiteName -ErrorAction SilentlyContinue
    if ($null -eq $site) {
        New-Website -Name $SiteName -Port $PublicHttpPort -HostHeader $HostName -PhysicalPath $physicalPath -ApplicationPool $AppPoolName | Out-Null
    }
    else {
        Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $physicalPath
        Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $AppPoolName
        Get-WebBinding -Name $SiteName | Remove-WebBinding
        New-WebBinding -Name $SiteName -Protocol http -Port $PublicHttpPort -HostHeader $HostName | Out-Null
    }

    $rewriteRules = @'
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <clear />
        <rule name="DotNetCloudReverseProxy" stopProcessing="true">
          <match url="(.*)" />
          <conditions logicalGrouping="MatchAll">
            <add input="{CACHE_URL}" pattern="^http(s)?://" />
          </conditions>
          <serverVariables>
            <set name="HTTP_X_FORWARDED_PROTO" value="http" />
            <set name="HTTP_X_FORWARDED_HOST" value="{HTTP_HOST}" />
            <set name="HTTP_X_FORWARDED_PORT" value="$PublicHttpPort" />
          </serverVariables>
          <action type="Rewrite" url="http://localhost:$KestrelHttpPort/{R:1}" logRewrittenUrl="true" />
        </rule>
      </rules>
    </rewrite>
    <webSocket enabled="true" />
    <proxy enabled="true" preserveHostHeader="true" reverseRewriteHostInResponseHeaders="false" />
  </system.webServer>
</configuration>
'@

    Set-Content -Path (Join-Path $Script:ServerRoot "web.config") -Value $rewriteRules -Encoding UTF8
    Write-Ok "IIS site '$SiteName' now reverse proxies to http://localhost:$KestrelHttpPort."
}

function Ensure-FirewallRules {
    if ($SkipFirewall) {
        Write-Warn "Skipping Windows Firewall configuration because -SkipFirewall was specified."
        return
    }

    if (-not $ConfigureFirewall.IsPresent) {
        $answer = Read-Host "Open Windows Firewall for inbound HTTP/HTTPS traffic (80/443) now? [Y/n]"
        if ($answer -and $answer.ToLowerInvariant().StartsWith("n")) {
            Write-Warn "Firewall rules were not changed."
            return
        }
    }

    $rules = @(
        @{ Name = "DotNetCloud IIS HTTP"; Port = 80 },
        @{ Name = "DotNetCloud IIS HTTPS"; Port = 443 }
    )

    foreach ($rule in $rules) {
        $existing = Get-NetFirewallRule -DisplayName $rule.Name -ErrorAction SilentlyContinue
        if ($null -eq $existing) {
            New-NetFirewallRule -DisplayName $rule.Name -Direction Inbound -Action Allow -Protocol TCP -LocalPort $rule.Port | Out-Null
        }
    }

    Write-Ok "Windows Firewall rules for ports 80 and 443 are configured."
}

function Get-AccessUrl {
    if ([string]::IsNullOrWhiteSpace($HostName)) {
        return "http://localhost:$PublicHttpPort"
    }

    return "http://$HostName"
}

function Print-Summary {
    $accessUrl = Get-AccessUrl
    $internalUrl = "http://localhost:$KestrelHttpPort"
    $serviceState = "Unknown"

    $service = Get-Service -Name $Script:ServiceName -ErrorAction SilentlyContinue
    if ($null -ne $service) {
        $serviceState = $service.Status.ToString()
    }

    Write-Host ""
    Write-Host "DotNetCloud for Windows + IIS is set up." -ForegroundColor Green
    Write-Host ""
    Write-Host "Access URLs:" -ForegroundColor Cyan
    Write-Host "  Public IIS URL:    $accessUrl"
    Write-Host "  Internal app URL:  $internalUrl"
    Write-Host "  Health check URL:  $internalUrl/health/live"
    Write-Host ""
    Write-Host "What was configured:" -ForegroundColor Cyan
    Write-Host "  IIS site:          $SiteName"
    Write-Host "  IIS app pool:      $AppPoolName"
    Write-Host "  Windows service:   $Script:ServiceName ($serviceState)"
    Write-Host "  Install path:      $Script:InstallRoot"
    Write-Host "  Data path:         $Script:DataRoot"
    Write-Host "  Config file:       $(Join-Path $Script:ConfigRoot 'config.json')"
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Open the public IIS URL above in your browser."
    Write-Host "  2. If you want HTTPS, either add an IIS self-signed certificate or use win-acme for a real certificate."
    Write-Host "  3. If the site does not load, check the Windows Service, Event Viewer, and the DotNetCloud logs under '$Script:LogsRoot'."
    Write-Host ""
}

Assert-Administrator

if ([string]::IsNullOrWhiteSpace($SourcePath)) {
    $SourcePath = Get-DefaultSourcePath
}

if ([string]::IsNullOrWhiteSpace($SourcePath)) {
    throw "No published binaries were found automatically. Use -SourcePath to point at artifacts\publish or another published DotNetCloud output folder."
}

if ($Beginner.IsPresent -and $Advanced.IsPresent) {
    throw "Use either -Beginner or -Advanced, not both."
}

if (-not $Beginner.IsPresent -and -not $Advanced.IsPresent) {
    $Beginner = $true
}

$layout = Resolve-PublishLayout -Root $SourcePath

Write-Info "Windows installation source: $SourcePath"
Write-Info "Install root: $Script:InstallRoot"
Write-Info "Data root: $Script:DataRoot"

Ensure-WindowsFeatures
Ensure-HostingBundle
Ensure-IisModules
Copy-Binaries -ServerSource $layout.Server -CliSource $layout.Cli
Write-EnvironmentFile
Invoke-SetupWizard
Install-WindowsService
Start-WindowsService
Wait-ForHealth
Ensure-IisSite
Ensure-FirewallRules
Print-Summary