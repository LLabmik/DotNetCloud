# Windows Installer Improvement Plan

> **Created:** 2026-03-21
> **Goal:** Make the Windows server install nearly effortless — matching the Linux one-liner experience.
> **Target experience:** User opens elevated PowerShell, runs one command, answers 3 prompts, gets a working HTTPS server.
> **File to modify:** `tools/install-windows.ps1`
> **Docs to update:** `docs/admin/server/WINDOWS_IIS_INSTALL_GUIDE.md`, `docs/admin/server/INSTALLATION.md`

---

## Background & Pain Points Discovered

During a hands-on SyncTray-to-localhost testing session on 2026-03-21, these blockers were discovered — every one of them would stop a beginner cold:

| # | Pain Point | Severity | How We Fixed It Manually |
|---|-----------|----------|--------------------------|
| 1 | **No .NET runtime check** — service fails silently if .NET 10 missing | Moderate | Was already installed on test machine |
| 2 | **PostgreSQL not installed** — no auto-install on Windows (Linux script does it) | Blocker | Was already installed on test machine |
| 3 | **DB user/password not created** — CLI wizard prompts but doesn't create the PostgreSQL user on Windows | Blocker | Manually ran SQL to create user |
| 4 | **Admin user not created during install** — seeder only works if CLI setup runs first, and user may not know the credentials | Blocker | Manually created user via API + SQL UPDATE |
| 5 | **IIS `allowedServerVariables` locked** — web.config sets `HTTP_X_FORWARDED_PROTO` but the server variables are never registered at machine level via `appcmd.exe` | Blocker | Ran `appcmd.exe set config -section:system.webServer/rewrite/allowedServerVariables /+"[name='HTTP_X_FORWARDED_PROTO']" /commit:apphost` |
| 6 | **ARR rewrites Location headers** — `reverseRewriteHostInResponseHeaders` not disabled at server level, breaking OAuth callbacks to non-standard ports | Blocker | Ran `appcmd.exe set config -section:system.webServer/proxy /reverseRewriteHostInResponseHeaders:false /commit:apphost` |
| 7 | **No HTTPS by default** — OpenIddict requires HTTPS in production; IIS only gets an HTTP binding | Major | Manually created self-signed cert + HTTPS binding in IIS |
| 8 | **web.config sets X-Forwarded-Proto to `http`** — should be dynamic or `https` when HTTPS is configured | Major | Manually updated web.config |

### What Already Works Well

- `install-windows.ps1` handles IIS features, ARR/URLRewrite via winget, service creation, firewall, health check polling
- `AdminSeeder` + `DbInitializer` + `OidcClientSeeder` auto-run at startup (idempotent)
- Linux `install.sh` is the gold standard — auto-installs PostgreSQL, creates DB user, writes systemd units
- The web.config template already has `reverseRewriteHostInResponseHeaders="false"` in the `<proxy>` element — the bug is that IIS server-level config also needs it set via `appcmd.exe`

---

## Implementation Tasks

### Task 1: Assert-DotNetRuntime — Early .NET Runtime Check
**Status:** ✓ Completed
**File:** `tools/install-windows.ps1`
**Insert:** New function, called FIRST in the main flow (before `Assert-Administrator` or right after it)

**What it does:**
- Checks `dotnet --list-runtimes` for `Microsoft.AspNetCore.App 10.x`
- If missing, throws a clear error with download URL
- Runs before any IIS/service work so the user doesn't get halfway through and fail

**Implementation:**
```powershell
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
```

---

### Task 2: Ensure-PostgreSQL — Auto-Install PostgreSQL
**Status:** ✓ Completed
**File:** `tools/install-windows.ps1`
**Insert:** New function, called after `Ensure-IisModules` (before binary copy)
**Skip flag:** Add `-SkipDatabaseInstall` parameter

**What it does:**
- Checks for running `postgresql*` service
- If missing, attempts install via `winget install --id PostgreSQL.PostgreSQL.17`
- Waits for the service to start
- Falls back to clear manual instructions if winget fails
- Adds `psql.exe` parent directory to PATH for the current session

**Key decisions:**
- Use PostgreSQL 17 (latest stable as of 2026)
- winget package ID: `PostgreSQL.PostgreSQL.17`
- Default PostgreSQL superuser: `postgres` with password set during winget install (may be blank on Windows)

---

### Task 3: Ensure-Database — Auto-Create DB User + Database
**Status:** ✓ Completed
**File:** `tools/install-windows.ps1`
**Insert:** New function, called after `Ensure-PostgreSQL`

**What it does:**
1. Generates a cryptographically random 32-character password for the `dotnetcloud` DB user
2. Locates `psql.exe` (searches `$env:ProgramFiles\PostgreSQL\*\bin\`)
3. Connects as `postgres` superuser
4. Creates the `dotnetcloud` user if it doesn't exist
5. Creates the `dotnetcloud` database if it doesn't exist
6. Grants all privileges
7. Writes the connection string to config so the server can find it
8. Stores the generated password in the config file (not displayed to user — they don't need it)

**Connection string format:**
```
Host=localhost;Database=dotnetcloud;Username=dotnetcloud;Password=<generated>
```

**Security:** The password is random and stored only in the server's config file (`C:\ProgramData\DotNetCloud\config\config.json`), which is only readable by admins. The user never needs to type it.

---

### Task 4: Prompt for Admin User Credentials
**Status:** ✓ Completed
**File:** `tools/install-windows.ps1`
**Insert:** New function `Read-AdminCredentials`, called after `Ensure-Database` and before `Install-WindowsService`

**What it does:**
1. Prompts the user for an admin **email address** (with validation: must contain `@`)
2. Prompts for a **password** (with `Read-Host -AsSecureString` so it's not displayed)
3. Confirms the password (second prompt)
4. Validates password strength (minimum 12 chars, at least one uppercase, one lowercase, one digit)
5. Writes the admin email to config and the password to the `.admin-seed` one-time file
6. The server's `AdminSeeder` picks these up automatically on first start

**Why not rely on the CLI wizard:** The CLI wizard (`dotnetcloud setup`) is interactive and complex. For the beginner path, we want exactly 3 prompts total (email, password, confirm password) — not a 9-step wizard.

**Implementation sketch:**
```powershell
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
        if ($pass.Length -lt 8 -or $pass -notmatch "[A-Z]" -or $pass -notmatch "[a-z]" -or $pass -notmatch "\d") {
            Write-Warn "Password must be at least 8 characters with uppercase, lowercase, and a digit."
            continue
        }
        break
    } while ($true)

    # Write to config for AdminSeeder
    $configPath = Join-Path $Script:ConfigRoot "config.json"
    # ... merge into existing config JSON ...
    # Write .admin-seed one-time file
    $seedPath = Join-Path $Script:ConfigRoot ".admin-seed"
    Set-Content -Path $seedPath -Value $pass -Encoding UTF8

    Write-Ok "Admin account will be created on first server start."
}
```

**Config file entry:**
```json
{
  "DotNetCloud": {
    "AdminEmail": "user@example.com"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=dotnetcloud;Username=dotnetcloud;Password=<generated>"
  }
}
```

---

### Task 5: Register allowedServerVariables at IIS Machine Level
**Status:** ✓ Completed
**File:** `tools/install-windows.ps1`
**Insert:** Inside `Ensure-IisSite`, BEFORE writing web.config

**What it does:**
- Uses `appcmd.exe` to register `HTTP_X_FORWARDED_PROTO`, `HTTP_X_FORWARDED_HOST`, and `HTTP_X_FORWARDED_PORT` as allowed server variables at the machine (apphost) level
- Without this, the web.config `<serverVariables>` section causes a 500.52 error because the section is locked at the parent level
- Idempotent — checks if each variable already exists before adding

**Implementation:**
```powershell
$appcmd = "$env:windir\system32\inetsrv\appcmd.exe"
foreach ($varName in @("HTTP_X_FORWARDED_PROTO", "HTTP_X_FORWARDED_HOST", "HTTP_X_FORWARDED_PORT")) {
    $existing = & $appcmd list config -section:system.webServer/rewrite/allowedServerVariables 2>&1
    if ("$existing" -notmatch [regex]::Escape($varName)) {
        & $appcmd set config -section:system.webServer/rewrite/allowedServerVariables /+"[name='$varName']" /commit:apphost 2>&1 | Out-Null
    }
}
Write-Ok "IIS allowed server variables registered for X-Forwarded headers."
```

---

### Task 6: Disable ARR reverseRewriteHostInResponseHeaders at Server Level
**Status:** ✓ Completed
**File:** `tools/install-windows.ps1`
**Insert:** Inside `Ensure-IisReverseProxyEnabled`, after enabling the proxy

**What it does:**
- Runs `appcmd.exe set config -section:system.webServer/proxy /reverseRewriteHostInResponseHeaders:false /commit:apphost`
- This prevents ARR from rewriting `Location` headers in responses, which was breaking OAuth callback redirects (redirect to `http://localhost:52701/oauth/callback` was being rewritten to `https://localhost/oauth/callback`)
- The web.config template already has this attribute, but IIS server-level config also needs it

**Implementation:**
```powershell
function Ensure-IisReverseProxyEnabled {
    Import-Module WebAdministration -ErrorAction Stop
    Set-WebConfigurationProperty -PSPath "MACHINE/WEBROOT/APPHOST" -Filter "system.webServer/proxy" -Name enabled -Value True
    
    # Prevent ARR from rewriting Location response headers (breaks OAuth, WebSocket upgrades, etc.)
    $appcmd = "$env:windir\system32\inetsrv\appcmd.exe"
    & $appcmd set config -section:system.webServer/proxy /reverseRewriteHostInResponseHeaders:false /commit:apphost 2>&1 | Out-Null
    
    Write-Ok "Enabled IIS proxy support for ARR (response header rewriting disabled)."
}
```

---

### Task 7: Auto-Generate Self-Signed Certificate + HTTPS Binding
**Status:** ✓ Completed
**File:** `tools/install-windows.ps1`
**Insert:** New function `Ensure-HttpsBinding`, called after `Ensure-IisSite`
**New param:** Add `-SkipHttps` switch parameter

**What it does:**
1. Determines the certificate subject (hostname or `localhost`)
2. Checks if a matching cert already exists in `Cert:\LocalMachine\My`
3. If not, generates a new self-signed cert with `New-SelfSignedCertificate` (5-year validity, DNS SANs for hostname + localhost)
4. Adds an HTTPS binding on port 443 to the IIS site
5. Associates the cert with the binding
6. Updates `X-Forwarded-Proto` in web.config to `https`

**Implementation sketch:**
```powershell
function Ensure-HttpsBinding {
    if ($SkipHttps) {
        Write-Warn "Skipping HTTPS configuration because -SkipHttps was specified."
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
        Write-Ok "Using existing certificate (thumbprint: $($cert.Thumbprint), expires: $($cert.NotAfter))."
    }

    # Add HTTPS binding
    $existingHttps = Get-WebBinding -Name $SiteName -Protocol https -ErrorAction SilentlyContinue
    if (-not $existingHttps) {
        New-WebBinding -Name $SiteName -Protocol https -Port 443 -HostHeader $HostName -SslFlags 0
    }

    # Bind cert — use netsh for SNI-less binding
    $bindingHash = $cert.Thumbprint
    & netsh http delete sslcert ipport=0.0.0.0:443 2>&1 | Out-Null
    & netsh http add sslcert ipport=0.0.0.0:443 certhash=$bindingHash appid='{4dc3e181-e14b-4a21-b022-59fc669b0914}' certstore=MY 2>&1 | Out-Null

    Write-Ok "HTTPS binding configured on port 443 with certificate '$certSubject'."
    
    # For self-signed: inform user about browser trust
    Write-Warn "This is a self-signed certificate. Browsers will show a security warning."
    Write-Warn "For a production domain, use win-acme to get a real certificate from Let's Encrypt."
}
```

---

### Task 8: Update web.config X-Forwarded-Proto to Match HTTPS State
**Status:** ✓ Completed
**File:** `tools/install-windows.ps1`
**Modify:** The `$rewriteRules` template in `Ensure-IisSite`

**What it does:**
- When HTTPS is configured (the default), sets `HTTP_X_FORWARDED_PROTO` to `https` in the web.config
- When `-SkipHttps` is used, keeps it as `http`
- This ensures OpenIddict and the ForwardedHeaders middleware see the correct protocol

**Implementation:**
Change the template line:
```xml
<set name="HTTP_X_FORWARDED_PROTO" value="http" />
```
To use a variable:
```powershell
$forwardedProto = if ($SkipHttps) { "http" } else { "https" }
# ... in template ...
<set name="HTTP_X_FORWARDED_PROTO" value="__PROTO__" />
# ... replace ...
-replace '__PROTO__', $forwardedProto
```

---

### Task 9: Skip CLI Setup Wizard in Beginner Mode
**Status:** ✓ Completed
**File:** `tools/install-windows.ps1`
**Modify:** `Invoke-SetupWizard` function or the main flow

**What it does:**
- In beginner mode, instead of running the full CLI setup wizard, the install script handles everything directly:
  - Database: auto-created (Task 3)
  - Admin credentials: prompted directly (Task 4)
  - Config: written by the install script
- The CLI wizard is still used in advanced mode (`-Advanced` flag) for users who want full control
- This eliminates the 9-step interactive CLI wizard from the beginner path

**Rationale:** The CLI wizard was designed for the Linux manual install path. On Windows, the installer script already knows everything it needs. The beginner path should be: run script → answer 3 questions → done.

---

### Task 10: Update Print-Summary for HTTPS
**Status:** ✓ Completed
**File:** `tools/install-windows.ps1`
**Modify:** `Print-Summary` function and `Get-AccessUrl`

**What it does:**
- Changes the access URL to `https://` when HTTPS is configured
- Removes the "If you want HTTPS…" next step (it's already done)
- Adds note about self-signed cert browser warning
- Shows the admin email they used

---

### Task 11: Update Windows Install Documentation
**Status:** ✓ Completed
**Files:**
- `docs/admin/server/WINDOWS_IIS_INSTALL_GUIDE.md`
- `docs/admin/server/INSTALLATION.md`

**What changes:**
- Update the "Beginner One-Command Install" section to show the simplified flow
- Document that PostgreSQL is auto-installed (or how to pre-install)
- Document that HTTPS is configured automatically with a self-signed cert
- Document the admin credential prompt
- Update the "What the script does" numbered list
- Remove/reduce the HTTPS manual steps (now automated)
- Update troubleshooting for new scenarios (PostgreSQL install failures, cert issues)
- Add a "What you'll need before starting" section (just: Windows 10/11/Server, internet, admin rights)
- Add a "What the installer does automatically" section listing everything

---

### Task 12: Script Syntax Validation
**Status:** ✓ Completed
**File:** `tools/install-windows.ps1`

**What it does:**
- Run `powershell -NoProfile -Command "& { $null = [System.Management.Automation.PSParser]::Tokenize((Get-Content 'tools\install-windows.ps1' -Raw), [ref]$null) }"` to validate syntax
- Check for any parse errors before committing

---

## New Parameter Summary

After all changes, `install-windows.ps1` will accept:

| Parameter | Default | Description |
|-----------|---------|-------------|
| `-SourcePath` | auto-detect | Path to published binaries |
| `-InstallRoot` | `C:\Program Files\DotNetCloud` | Where binaries go |
| `-DataRoot` | `C:\ProgramData\DotNetCloud` | Where data/config/logs go |
| `-SiteName` | `DotNetCloud` | IIS site name |
| `-AppPoolName` | `DotNetCloud` | IIS app pool name |
| `-HostName` | `""` (localhost) | Public hostname |
| `-PublicHttpPort` | `80` | IIS HTTP port |
| `-KestrelHttpPort` | `5080` | Internal Kestrel port |
| `-ConfigureFirewall` | prompt | Auto-open firewall |
| `-SkipFirewall` | `$false` | Skip firewall setup |
| `-Beginner` | `$true` | Simplified beginner flow |
| `-Advanced` | `$false` | Full CLI wizard |
| `-SkipFeatureInstall` | `$false` | Skip IIS feature install |
| `-SkipHostingBundleInstall` | `$false` | Skip hosting bundle check |
| `-SkipIisConfiguration` | `$false` | Skip IIS site setup |
| `-SkipServiceInstall` | `$false` | Skip Windows Service creation |
| **`-SkipDatabaseInstall`** | `$false` | **NEW** — skip PostgreSQL install |
| **`-SkipHttps`** | `$false` | **NEW** — skip HTTPS cert + binding |
| `-Force` | `$false` | Overwrite without prompts |

## New Main Flow (Beginner)

```
Assert-Administrator
Assert-DotNetRuntime           ← NEW
Ensure-WindowsFeatures
Ensure-HostingBundle
Ensure-IisModules
Ensure-PostgreSQL              ← NEW
Ensure-Database                ← NEW (creates DB user + database + connection string)
Read-AdminCredentials          ← NEW (prompts for email + password)
Copy-Binaries
Write-EnvironmentFile          ← UPDATED (includes connection string)
Write-ConfigFile               ← NEW (writes config.json with DB + admin email)
# Invoke-SetupWizard           ← SKIPPED in beginner mode
Install-WindowsService
Start-WindowsService
Wait-ForHealth
Ensure-IisSite                 ← UPDATED (registers allowedServerVariables first)
Ensure-HttpsBinding            ← NEW (self-signed cert + port 443)
Ensure-FirewallRules
Print-Summary                  ← UPDATED (shows https:// URL, admin email)
```

## Target User Experience

```
PS C:\> powershell -ExecutionPolicy Bypass -File .\tools\install-windows.ps1 -SourcePath .\artifacts\publish

[INFO] Windows installation source: C:\artifacts\publish
[INFO] Install root: C:\Program Files\DotNetCloud
[INFO] Data root: C:\ProgramData\DotNetCloud
[OK]   ASP.NET Core 10.0 runtime detected.
[OK]   Required IIS Windows features are already enabled.
[OK]   ASP.NET Core Hosting Bundle / ANCM appears to be installed already.
[OK]   IIS URL Rewrite and ARR are already installed.
[INFO] Installing PostgreSQL 17 via winget...
[OK]   PostgreSQL 17 is running.
[INFO] Creating database user and database...
[OK]   Database 'dotnetcloud' created with user 'dotnetcloud'.

Create your administrator account
You will use these credentials to log in to DotNetCloud.

Admin email address: admin@example.com
Admin password (min 12 chars, mixed case + digit): ********
Confirm password: ********
[OK]   Admin account will be created on first server start.

[INFO] Copying DotNetCloud binaries...
[OK]   Binaries copied.
[INFO] Creating the Windows Service 'DotNetCloud'...
[OK]   Windows Service is configured.
[INFO] Starting DotNetCloud Windows Service...
[OK]   DotNetCloud responded on http://localhost:5080/health/live
[OK]   IIS allowed server variables registered for X-Forwarded headers.
[OK]   Enabled IIS proxy support for ARR (response header rewriting disabled).
[OK]   IIS site 'DotNetCloud' now reverse proxies to http://localhost:5080.
[INFO] Creating self-signed certificate for 'localhost'...
[OK]   HTTPS binding configured on port 443.
[WARN] This is a self-signed certificate. Browsers will show a security warning.
[OK]   Windows Firewall rules for ports 80 and 443 are configured.

DotNetCloud for Windows is set up.

Access URLs:
  Public URL:        https://localhost/
  Health check:      http://localhost:5080/health/live

Admin account:       admin@example.com

Next steps:
  1. Open https://localhost/ in your browser.
  2. Log in with the admin credentials you just created.
  3. For a production domain, use win-acme to replace the self-signed certificate.
```

---

## Implementation Order

1. Tasks 5 + 6 first (bug fixes — smallest, most impactful)
2. Task 1 (runtime check — simple, prevents confusing failures)
3. Tasks 2 + 3 (PostgreSQL auto-install + DB creation)
4. Task 4 (admin credential prompting)
5. Tasks 7 + 8 (HTTPS)
6. Tasks 9 + 10 (skip wizard in beginner mode, update summary)
7. Task 11 (documentation)
8. Task 12 (validation)
