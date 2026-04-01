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
    [switch]$SkipDatabaseInstall,
    [switch]$SkipHttps,
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

function Assert-DotNetRuntime {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        throw ".NET 10 runtime is not installed. Download from https://dotnet.microsoft.com/download/dotnet/10.0"
    }
    $runtimes = & dotnet --list-runtimes 2>&1 | Where-Object { $_ -match "Microsoft\.AspNetCore\.App 10\." }
    if (-not $runtimes) {
        throw "ASP.NET Core 10.0 runtime is required but not found. Download from https://dotnet.microsoft.com/download/dotnet/10.0"
    }
    Write-Ok "ASP.NET Core 10.0 runtime detected."
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

    Write-Warn "ASP.NET Core Hosting Bundle (ANCM) was not detected."
    Write-Warn "For the Windows Service + IIS reverse proxy model, ANCM is not required."
    Write-Warn "IIS forwards requests to the DotNetCloud Kestrel service via ARR -- no in-process hosting."
    Write-Host "Optional: install from $Script:HostingBundleDownloadUrl" -ForegroundColor Yellow
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

function Find-PsqlExe {
    $pgDirs = Get-ChildItem -Path "$env:ProgramFiles\PostgreSQL" -Directory -ErrorAction SilentlyContinue
    foreach ($dir in ($pgDirs | Sort-Object Name -Descending)) {
        $candidate = Join-Path $dir.FullName "bin\psql.exe"
        if (Test-Path $candidate) {
            return $candidate
        }
    }
    return $null
}

function Ensure-PostgreSQL {
    if ($SkipDatabaseInstall) {
        Write-Warn "Skipping PostgreSQL installation because -SkipDatabaseInstall was specified."
        return
    }

    # Check for a running PostgreSQL service
    $pgService = Get-Service -Name "postgresql*" -ErrorAction SilentlyContinue | Where-Object { $_.Status -eq "Running" }
    if ($null -ne $pgService) {
        Write-Ok "PostgreSQL is running (service: $($pgService.Name))."
        return
    }

    # Check if PostgreSQL exists but is stopped
    $pgStopped = Get-Service -Name "postgresql*" -ErrorAction SilentlyContinue
    if ($null -ne $pgStopped) {
        Write-Info "PostgreSQL service found but not running. Starting..."
        Start-Service -Name $pgStopped.Name
        Start-Sleep -Seconds 3
        Write-Ok "PostgreSQL service started ($($pgStopped.Name))."
        return
    }

    # Attempt auto-install via winget
    if (Test-WingetAvailable) {
        Write-Info "Installing PostgreSQL 17 via winget..."
        try {
            & winget install --id PostgreSQL.PostgreSQL.17 --accept-source-agreements --accept-package-agreements --silent --disable-interactivity 2>&1 | Out-Null

            # Refresh PATH for the current session
            $pgBin = Find-PsqlExe
            if ($pgBin) {
                $pgBinDir = Split-Path $pgBin -Parent
                if ($env:PATH -notlike "*$pgBinDir*") {
                    $env:PATH = "$pgBinDir;$env:PATH"
                }
            }

            # Wait for the service to start
            $retries = 0
            while ($retries -lt 15) {
                $pgService = Get-Service -Name "postgresql*" -ErrorAction SilentlyContinue | Where-Object { $_.Status -eq "Running" }
                if ($null -ne $pgService) {
                    Write-Ok "PostgreSQL 17 is running."
                    return
                }
                Start-Sleep -Seconds 2
                $retries++
            }

            Write-Warn "PostgreSQL was installed but the service did not start automatically."
            Write-Warn "Start it manually: Start-Service postgresql-x64-17"
            throw "PostgreSQL service not running after install. Start the service and re-run."
        }
        catch {
            Write-Fail "Automatic PostgreSQL install failed: $_"
        }
    }

    Write-Fail "PostgreSQL is not installed and could not be installed automatically."
    Write-Host ""
    Write-Host "  Install PostgreSQL 17 manually:" -ForegroundColor Yellow
    Write-Host "    1. Download from https://www.postgresql.org/download/windows/" -ForegroundColor Yellow
    Write-Host "    2. Run the installer and complete the setup wizard" -ForegroundColor Yellow
    Write-Host "    3. Ensure the PostgreSQL service is running" -ForegroundColor Yellow
    Write-Host "    4. Re-run this install script" -ForegroundColor Yellow
    Write-Host ""
    throw "PostgreSQL is required. Install it and re-run this script."
}

function Ensure-Database {
    if ($SkipDatabaseInstall) {
        Write-Warn "Skipping database creation because -SkipDatabaseInstall was specified."
        return
    }

    # Find psql.exe
    $psqlExe = Find-PsqlExe
    if (-not $psqlExe) {
        # Also check PATH
        $psqlCmd = Get-Command psql -ErrorAction SilentlyContinue
        if ($psqlCmd) {
            $psqlExe = $psqlCmd.Source
        }
    }

    if (-not $psqlExe) {
        throw "psql.exe not found. Ensure PostgreSQL is installed and its bin directory is in PATH."
    }

    Write-Info "Creating database user and database..."

    # Generate a cryptographically random 32-character password
    $bytes = New-Object byte[] 24
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    $Script:DbPassword = [Convert]::ToBase64String($bytes)

    # Check if user exists
    $userCheck = & $psqlExe -U postgres -h localhost -tAc "SELECT 1 FROM pg_roles WHERE rolname='dotnetcloud'" 2>&1
    if ("$userCheck" -notmatch "^1") {
        & $psqlExe -U postgres -h localhost -c "CREATE USER dotnetcloud WITH PASSWORD '$($Script:DbPassword)';" 2>&1 | Out-Null
    }
    else {
        # User exists — update password
        & $psqlExe -U postgres -h localhost -c "ALTER USER dotnetcloud WITH PASSWORD '$($Script:DbPassword)';" 2>&1 | Out-Null
    }

    # Check if database exists
    $dbCheck = & $psqlExe -U postgres -h localhost -tAc "SELECT 1 FROM pg_database WHERE datname='dotnetcloud'" 2>&1
    if ("$dbCheck" -notmatch "^1") {
        & $psqlExe -U postgres -h localhost -c "CREATE DATABASE dotnetcloud OWNER dotnetcloud;" 2>&1 | Out-Null
    }

    # Grant privileges
    & $psqlExe -U postgres -h localhost -c "GRANT ALL PRIVILEGES ON DATABASE dotnetcloud TO dotnetcloud;" 2>&1 | Out-Null

    $Script:ConnectionString = "Host=localhost;Database=dotnetcloud;Username=dotnetcloud;Password=$($Script:DbPassword)"
    Write-Ok "Database 'dotnetcloud' created with user 'dotnetcloud'."
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

    if ($Script:ConnectionString) {
        $contents += "ConnectionStrings__DefaultConnection=$Script:ConnectionString"
    }

    Set-Content -Path $envFilePath -Value $contents -Encoding UTF8
    Write-Ok "Wrote environment file to '$envFilePath'."
}

function Read-AdminCredentials {
    Write-Host ""
    Write-Host "Create your administrator account" -ForegroundColor Cyan
    Write-Host "You will use these credentials to log in to DotNetCloud." -ForegroundColor Gray
    Write-Host ""

    # Email
    do {
        $email = Read-Host "Admin email address"
        if ($email -notmatch "^[^@]+@[^@]+\.[^@]+$") {
            Write-Warn "Please enter a valid email address."
        }
    } while ($email -notmatch "^[^@]+@[^@]+\.[^@]+$")

    # Password (masked)
    do {
        $securePass = Read-Host "Admin password (min 12 chars, mixed case + digit)" -AsSecureString
        $secureConfirm = Read-Host "Confirm password" -AsSecureString
        $pass = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
            [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePass))
        $confirm = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
            [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureConfirm))

        if ($pass -ne $confirm) {
            Write-Warn "Passwords do not match. Try again."
            continue
        }
        if ($pass.Length -lt 12 -or $pass -notmatch "[A-Z]" -or $pass -notmatch "[a-z]" -or $pass -notmatch "\d") {
            Write-Warn "Password must be at least 12 characters with uppercase, lowercase, and a digit."
            continue
        }
        break
    } while ($true)

    $Script:AdminEmail = $email
    $Script:AdminPassword = $pass

    Write-Ok "Admin account will be created on first server start."
}

function Write-ConfigFile {
    Ensure-Directory $Script:ConfigRoot

    $configPath = Join-Path $Script:ConfigRoot "config.json"

    $config = @{}
    if (Test-Path $configPath) {
        try {
            $config = Get-Content -Path $configPath -Raw | ConvertFrom-Json -AsHashtable
        }
        catch {
            $config = @{}
        }
    }

    # Set connection string
    if (-not $config.ContainsKey("ConnectionStrings")) {
        $config["ConnectionStrings"] = @{}
    }
    if ($Script:ConnectionString) {
        $config["ConnectionStrings"]["DefaultConnection"] = $Script:ConnectionString
    }

    # Set admin email
    if ($Script:AdminEmail) {
        if (-not $config.ContainsKey("DotNetCloud")) {
            $config["DotNetCloud"] = @{}
        }
        $config["DotNetCloud"]["AdminEmail"] = $Script:AdminEmail
    }

    $configJson = $config | ConvertTo-Json -Depth 10
    Set-Content -Path $configPath -Value $configJson -Encoding UTF8
    Write-Ok "Wrote config file to '$configPath'."

    # Write one-time admin seed file for the AdminSeeder
    if ($Script:AdminPassword) {
        $seedPath = Join-Path $Script:ConfigRoot ".admin-seed"
        Set-Content -Path $seedPath -Value $Script:AdminPassword -Encoding UTF8
    }
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
    $envVars = @(
        "DOTNETCLOUD_CONFIG_DIR=$Script:ConfigRoot",
        "DOTNETCLOUD_DATA_DIR=$Script:DataRoot",
        "ASPNETCORE_ENVIRONMENT=Production",
        "DOTNET_ENVIRONMENT=Production",
        "Kestrel__HttpPort=$KestrelHttpPort",
        "Kestrel__EnableHttps=false"
    )
    if ($Script:ConnectionString) {
        $envVars += "ConnectionStrings__DefaultConnection=$Script:ConnectionString"
    }
    New-ItemProperty -Path $regPath -Name "Environment" -Value $envVars -PropertyType MultiString -Force | Out-Null

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

    # Prevent ARR from rewriting Location response headers (breaks OAuth, WebSocket upgrades, etc.)
    $appcmd = "$env:windir\system32\inetsrv\appcmd.exe"
    & $appcmd set config -section:system.webServer/proxy /reverseRewriteHostInResponseHeaders:false /commit:apphost 2>&1 | Out-Null

    Write-Ok "Enabled IIS proxy support for ARR (response header rewriting disabled)."
}

function Remove-DefaultSiteBindingIfNeeded {
    Import-Module WebAdministration -ErrorAction Stop
    $defaultSite = Get-Website -Name "Default Web Site" -ErrorAction SilentlyContinue
    if ($null -eq $defaultSite) {
        return
    }

    if (($defaultSite.Bindings.Collection | Where-Object { $_.bindingInformation -like "*:${PublicHttpPort}:*" }).Count -gt 0) {
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

    # Register allowed server variables at the machine (apphost) level (Task 5)
    # Without this, the web.config <serverVariables> section causes a 500.52 error
    $appcmd = "$env:windir\system32\inetsrv\appcmd.exe"
    foreach ($varName in @("HTTP_X_FORWARDED_PROTO", "HTTP_X_FORWARDED_HOST", "HTTP_X_FORWARDED_PORT")) {
        $existing = & $appcmd list config -section:system.webServer/rewrite/allowedServerVariables 2>&1
        if ("$existing" -notmatch [regex]::Escape($varName)) {
            & $appcmd set config -section:system.webServer/rewrite/allowedServerVariables /+"[name='$varName']" /commit:apphost 2>&1 | Out-Null
        }
    }
    Write-Ok "IIS allowed server variables registered for X-Forwarded headers."

    Remove-DefaultSiteBindingIfNeeded
    Ensure-AppPool

    $physicalPath = $Script:ServerRoot
    $bindingInformation = if ([string]::IsNullOrWhiteSpace($HostName)) {
        "*:${PublicHttpPort}:"
    }
    else {
        "*:${PublicHttpPort}:$HostName"
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

    $forwardedProto = if ($SkipHttps) { "http" } else { "https" }

    $rewriteRules = (@'
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
            <set name="HTTP_X_FORWARDED_PROTO" value="__PROTO__" />
            <set name="HTTP_X_FORWARDED_HOST" value="{HTTP_HOST}" />
            <set name="HTTP_X_FORWARDED_PORT" value="__PUBLIC_PORT__" />
          </serverVariables>
          <action type="Rewrite" url="http://localhost:__KESTREL_PORT__/{R:1}" logRewrittenUrl="true" />
        </rule>
      </rules>
    </rewrite>
    <webSocket enabled="true" />
    <proxy enabled="true" preserveHostHeader="true" reverseRewriteHostInResponseHeaders="false" />
  </system.webServer>
</configuration>
'@) -replace '__PROTO__', $forwardedProto -replace '__PUBLIC_PORT__', $PublicHttpPort -replace '__KESTREL_PORT__', $KestrelHttpPort

    Set-Content -Path (Join-Path $Script:ServerRoot "web.config") -Value $rewriteRules -Encoding UTF8
    Write-Ok "IIS site '$SiteName' now reverse proxies to http://localhost:$KestrelHttpPort."
}

function Ensure-HttpsBinding {
    if ($SkipHttps) {
        Write-Warn "Skipping HTTPS configuration because -SkipHttps was specified."
        return
    }

    if ($SkipIisConfiguration) {
        return
    }

    Import-Module WebAdministration -ErrorAction Stop
    $certSubject = if ([string]::IsNullOrWhiteSpace($HostName)) { "localhost" } else { $HostName }
    $dnsNames = @($certSubject)
    if ($certSubject -ne "localhost") { $dnsNames += "localhost" }

    # Check for existing cert
    $cert = Get-ChildItem Cert:\LocalMachine\My |
        Where-Object { $_.Subject -eq "CN=$certSubject" -and $_.NotAfter -gt (Get-Date).AddDays(30) } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1

    if (-not $cert) {
        Write-Info "Creating self-signed certificate for '$certSubject'..."
        $cert = New-SelfSignedCertificate `
            -DnsName $dnsNames `
            -CertStoreLocation Cert:\LocalMachine\My `
            -NotAfter (Get-Date).AddYears(5) `
            -FriendlyName "DotNetCloud Self-Signed"
        Write-Ok "Created self-signed certificate (thumbprint: $($cert.Thumbprint))."
    }
    else {
        Write-Ok "Using existing certificate (thumbprint: $($cert.Thumbprint), expires: $($cert.NotAfter.ToString('yyyy-MM-dd')))."
    }

    # Add HTTPS binding
    $existingHttps = Get-WebBinding -Name $SiteName -Protocol https -ErrorAction SilentlyContinue
    if (-not $existingHttps) {
        New-WebBinding -Name $SiteName -Protocol https -Port 443 -HostHeader $HostName -SslFlags 0
    }

    # Bind cert via netsh for SNI-less binding
    $bindingHash = $cert.Thumbprint
    & netsh http delete sslcert ipport=0.0.0.0:443 2>&1 | Out-Null
    & netsh http add sslcert ipport=0.0.0.0:443 certhash=$bindingHash appid='{4dc3e181-e14b-4a21-b022-59fc669b0914}' certstore=MY 2>&1 | Out-Null

    Write-Ok "HTTPS binding configured on port 443 with certificate '$certSubject'."
    Write-Warn "This is a self-signed certificate. Browsers will show a security warning."
    Write-Warn "For a production domain, use win-acme to get a real certificate from Let's Encrypt."
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
    $protocol = if ($SkipHttps) { "http" } else { "https" }
    if ([string]::IsNullOrWhiteSpace($HostName)) {
        $portSuffix = ""
        if ($protocol -eq "http" -and $PublicHttpPort -ne 80) {
            $portSuffix = ":$PublicHttpPort"
        }
        return "${protocol}://localhost${portSuffix}"
    }

    return "${protocol}://$HostName"
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
    Write-Host "DotNetCloud for Windows is set up." -ForegroundColor Green
    Write-Host ""
    Write-Host "Access URLs:" -ForegroundColor Cyan
    Write-Host "  Public URL:        $accessUrl"
    Write-Host "  Health check:      $internalUrl/health/live"
    Write-Host ""

    if ($Script:AdminEmail) {
        Write-Host "Admin account:       $Script:AdminEmail" -ForegroundColor Cyan
        Write-Host ""
    }

    Write-Host "What was configured:" -ForegroundColor Cyan
    Write-Host "  IIS site:          $SiteName"
    Write-Host "  IIS app pool:      $AppPoolName"
    Write-Host "  Windows service:   $Script:ServiceName ($serviceState)"
    Write-Host "  Install path:      $Script:InstallRoot"
    Write-Host "  Data path:         $Script:DataRoot"
    Write-Host "  Config file:       $(Join-Path $Script:ConfigRoot 'config.json')"
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Open $accessUrl in your browser."

    if ($Script:AdminEmail) {
        Write-Host "  2. Log in with the admin credentials you just created."
    }
    else {
        Write-Host "  2. Log in with the admin account created during setup."
    }

    if (-not $SkipHttps) {
        Write-Host "  3. For a production domain, use win-acme to replace the self-signed certificate."
    }
    else {
        Write-Host "  3. If you want HTTPS, re-run without -SkipHttps or add a certificate in IIS Manager."
    }

    Write-Host "  4. If the site does not load, check the Windows Service, Event Viewer, and logs under '$Script:LogsRoot'."
    Write-Host ""

    if ($Beginner) {
        Write-Host "Optional -- Collabora CODE (browser document editing):" -ForegroundColor Cyan
        Write-Host "  Requires Docker Desktop for Windows."
        Write-Host "  Run once to start Collabora:"
        Write-Host '  docker run -d --name collabora --restart unless-stopped -p 9980:9980 `'
        Write-Host "    -e ""aliasgroup1=http://localhost"" collabora/code"
        Write-Host "  Then in config.json set collaboraMode to External with the server URL:"
        Write-Host '    "collaboraMode": "External",'
        Write-Host '    "collaboraUrl": "https://localhost:9980"'
        Write-Host "  The DotNetCloud CLI bridges this to the server configuration."
        Write-Host "  Collabora is reverse-proxied through DotNetCloud (single port)."
        Write-Host "  See docs/admin/COLLABORA.md for details."
        Write-Host ""
    }
}

Assert-Administrator
Assert-DotNetRuntime

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

# Script-level variables for data flow between functions
$Script:ConnectionString = $null
$Script:AdminEmail = $null
$Script:AdminPassword = $null
$Script:DbPassword = $null

$layout = Resolve-PublishLayout -Root $SourcePath

Write-Info "Windows installation source: $SourcePath"
Write-Info "Install root: $Script:InstallRoot"
Write-Info "Data root: $Script:DataRoot"

Ensure-WindowsFeatures
Ensure-HostingBundle
Ensure-IisModules
Ensure-PostgreSQL
Ensure-Database

if ($Beginner -and -not $Advanced) {
    # Beginner mode: prompt for admin credentials directly, skip CLI wizard
    Read-AdminCredentials
}

Copy-Binaries -ServerSource $layout.Server -CliSource $layout.Cli
Write-EnvironmentFile
Write-ConfigFile

if ($Advanced) {
    # Advanced mode: run the full CLI setup wizard
    Invoke-SetupWizard
}

Install-WindowsService
Start-WindowsService
Wait-ForHealth
Ensure-IisSite
Ensure-HttpsBinding
Ensure-FirewallRules
Print-Summary