# DotNetCloud Windows + IIS Install Guide

This guide is the Windows equivalent of the Linux reverse-proxy beginner path.

The goal is simple:

- DotNetCloud runs locally on Kestrel at `http://localhost:5080`
- IIS handles the public-facing site on ports `80` and `443`
- The Windows installer script does the heavy lifting for a beginner-friendly setup

If you are new to self-hosting on Windows, this is the recommended path.

For the architectural rationale behind this service-backed model, see [WINDOWS_SERVICE_ARCHITECTURE_NOTES.md](WINDOWS_SERVICE_ARCHITECTURE_NOTES.md).

## Who This Is For

Use this guide if:

- you want to host DotNetCloud on Windows Server 2022, Windows Server 2019, Windows 11, or Windows 10
- you are comfortable running one elevated PowerShell command
- you want IIS to reverse proxy to DotNetCloud, similar to the Apache/Caddy pattern on Linux

This guide assumes DotNetCloud binaries are already available on the machine, for example from a published release ZIP or a local publish output.

## How The Windows IIS Setup Works

The request flow looks like this:

```text
Browser -> IIS on ports 80/443 -> http://localhost:5080 -> DotNetCloud on Kestrel
```

This means:

- IIS is the public web server
- DotNetCloud itself stays on the local machine
- WebSockets and normal HTTP traffic both go through IIS
- TLS certificates are managed at the IIS layer

This mirrors the reverse-proxy recommendation on Linux.

## Prerequisites

Before you run the script, make sure these are true:

1. You are on Windows Server 2022, Windows Server 2019, Windows 11, or Windows 10.
2. You can run PowerShell as Administrator.
3. DotNetCloud server and CLI binaries are already present on disk.
4. It is okay if IIS is not installed yet. The script can enable the required Windows features.
5. You have internet access to install missing Windows dependencies if needed.

### Required IIS Components

The script checks for these pieces:

- IIS Web Server
- IIS WebSocket Protocol
- URL Rewrite
- Application Request Routing (ARR) for reverse proxy support
- ASP.NET Core Hosting Bundle / ASP.NET Core Module (ANCM)

If URL Rewrite, ARR, or the ASP.NET Core Hosting Bundle are missing, the script tells you exactly what to install and where to get it.
For URL Rewrite and ARR specifically, the script now attempts an automatic install first (via `winget`) and falls back to manual install instructions if needed.

## Beginner One-Command Install

Open an elevated PowerShell window in the repository root or in the folder where the published binaries are available.

Run:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\tools\install-windows.ps1 -SourcePath .\artifacts\publish -Beginner
```

That script will:

1. check that you are running as Administrator
2. enable IIS and the required Windows features
3. verify URL Rewrite, ARR, and the .NET 10 Hosting Bundle
4. copy the DotNetCloud binaries into a machine-level install location
5. run `dotnetcloud setup --beginner`
6. create the DotNetCloud Windows Service
7. create an IIS site and app pool that reverse proxy to `http://localhost:5080`
8. optionally open Windows Firewall ports `80` and `443`
9. print a plain-language summary with access URLs and next steps

## Advanced Install

If you want to control the site name, host name, install paths, or similar settings, run:

```powershell
pwsh -ExecutionPolicy Bypass -File .\tools\install-windows.ps1 -SourcePath .\artifacts\publish -Advanced -SiteName "DotNetCloud" -AppPoolName "DotNetCloud" -HostName "cloud.example.com"
```

Advanced mode still uses the same Windows install path, but it avoids forcing the beginner defaults inside the DotNetCloud setup wizard.

## Where Things Go On Disk

By default, the script uses machine-level paths:

- Install root: `C:\Program Files\DotNetCloud`
- Server binaries: `C:\Program Files\DotNetCloud\server`
- CLI binaries: `C:\Program Files\DotNetCloud\cli`
- Data root: `C:\ProgramData\DotNetCloud`
- Config: `C:\ProgramData\DotNetCloud\config\config.json`
- Logs: `C:\ProgramData\DotNetCloud\logs`
- Storage: `C:\ProgramData\DotNetCloud\storage`

This is intentional. A machine-level Windows Service should not depend on an individual administrator's user profile.

## IIS Reverse Proxy Details

The Windows installer configures IIS to forward requests to:

```text
http://localhost:5080
```

That is DotNetCloud's internal Kestrel HTTP listener.

This matches the Linux guidance:

- public traffic terminates at the reverse proxy
- the app itself stays on a local-only port
- HTTPS, host bindings, and public access are handled by the reverse proxy layer

## TLS Options

There are two beginner-friendly TLS approaches on Windows.

### Option 1: IIS Self-Signed Certificate

This is the simplest local or private-network option.

In IIS Manager:

1. Open `Server Certificates`.
2. Choose `Create Self-Signed Certificate...`.
3. Give it a name like `DotNetCloud Local`.
4. Open your DotNetCloud site.
5. Add an `https` binding on port `443` and select that certificate.

This works well for:

- local testing
- home lab installs
- private LAN access

Your browser will likely warn that the certificate is not publicly trusted unless you install the certificate into the trusted root store on your client devices.

### Option 2: Real Certificate With win-acme

If you have a public domain name pointing to your Windows server, use `win-acme`.

Typical flow:

1. Point `cloud.example.com` to your server.
2. Make sure ports `80` and `443` are reachable.
3. Install `win-acme`.
4. Request a certificate for your IIS site.
5. Let `win-acme` create or update the HTTPS binding.

This is the recommended public-internet path for beginners on Windows because it keeps certificate issuance and renewal inside the normal IIS workflow.

## After The Script Finishes

The script prints a summary similar to this:

- public IIS URL
- internal Kestrel URL
- health endpoint URL
- IIS site name
- application pool name
- Windows Service state
- config path
- next steps

If you used a host name, open the public site in your browser.

If you did not use a host name, the local test URL is usually:

```text
http://localhost
```

The internal health check is usually:

```text
http://localhost:5080/health/live
```

## Troubleshooting

If the site does not load, check the pieces in this order.

### 1. Check The DotNetCloud Windows Service

In PowerShell:

```powershell
Get-Service DotNetCloud
```

You want to see it in the `Running` state.

If needed:

```powershell
Start-Service DotNetCloud
Restart-Service DotNetCloud
```

### 2. Check The Local Health Endpoint First

If DotNetCloud is healthy locally, IIS is usually the remaining problem.

```powershell
Invoke-WebRequest http://localhost:5080/health/live -UseBasicParsing
```

If this fails, focus on the Windows Service or the DotNetCloud configuration first.

### 3. Check IIS Site State

In PowerShell:

```powershell
Import-Module WebAdministration
Get-Website
Get-WebAppPoolState -Name DotNetCloud
```

Make sure the site is started and the app pool is healthy.

### 4. Check IIS Logs

IIS logs are typically under:

```text
C:\inetpub\logs\LogFiles
```

Look for:

- `502` style reverse proxy errors
- binding mismatches
- request-routing failures

### 5. Check Windows Event Viewer

Open Event Viewer and check:

- `Windows Logs -> Application`
- `Windows Logs -> System`

Common things you may see:

- ASP.NET Core Module startup failures
- service start errors
- missing hosting bundle issues
- certificate or binding problems

### 6. Check DotNetCloud Logs

Look under:

```text
C:\ProgramData\DotNetCloud\logs
```

If the service starts and then exits, the app logs often explain whether the issue is:

- database connection failure
- invalid `config.json`
- missing files or permissions
- startup exception inside the server

### 7. Common Errors

#### The Script Says URL Rewrite Or ARR Is Missing

Install:

- URL Rewrite
- Application Request Routing

Then re-run the script.

IIS reverse proxying on Windows needs both.

#### The Script Says The Hosting Bundle Is Missing

Install the .NET 10 ASP.NET Core Hosting Bundle, then run the script again.

That bundle includes:

- ASP.NET Core runtime
- IIS support
- ASP.NET Core Module (ANCM)

#### Port 80 Is Already In Use

Another IIS site, web server, or local service is already bound to port `80`.

Fix one of these:

- stop or rebind the conflicting site
- remove the old binding
- run the installer with a different `-PublicHttpPort`

#### IIS Loads But DotNetCloud Does Not

That usually means IIS is up but the backend app is not healthy.

Check:

1. `Get-Service DotNetCloud`
2. `Invoke-WebRequest http://localhost:5080/health/live -UseBasicParsing`
3. Event Viewer
4. `C:\ProgramData\DotNetCloud\logs`

#### Local Health Works But IIS Returns Errors

That usually points to an IIS binding, ARR, URL Rewrite, or site configuration issue.

Check:

1. site bindings
2. host header settings
3. whether ARR proxy mode is enabled
4. whether the rewrite rule still points to `http://localhost:5080`

## Recommended Next Steps

After the site is live:

1. sign in with the admin account created during setup
2. confirm the app loads correctly through IIS
3. add HTTPS on port `443`
4. verify the reverse-proxy URL is the one you will use long-term
5. back up `config.json` and the data directory

For public deployments, prefer:

- IIS on `80/443`
- DotNetCloud on `localhost:5080`
- certificate management at the IIS layer

That keeps the Windows setup aligned with the Linux reverse-proxy architecture.