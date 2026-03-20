# Client/Server Mediation Handoff

Last updated: 2026-03-18 (File browser fixes — child count, breadcrumbs; server redeploy needed)

Purpose: shared handoff between client-side and server-side agents, mediated by user.

Archived context:
- Historical completed updates are in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Additional history remains available in git.

## Process Rules

**Agent autonomy (CRITICAL):**
- Both client and server agents work autonomously — they do NOT ask the moderator for context or permission.
- Agents pull the latest `main`, read the **Active Handoff** section, and execute the work described there independently.
- All actionable items, blockers, and technical details go directly in this document (committed to `main`).
- No moderator involvement in technical decisions, code reviews, or work coordination.

**Handoff management:**
- Put all technical findings, debugging conclusions, and next-step details in this document.
- Assistant (current agent) commits their findings/work and updates the **Active Handoff** section with actionable next steps for the other client.
- Assistant pushes commits to `main`.
- Unexpected untracked content rule (MANDATORY): remove unexpected untracked files/directories before commit; only keep intentional tracked changes for the handoff update.
- Handoff readiness gate (MANDATORY): all executable tests must pass before marking a handoff as ready.
- Environment-gated tests are allowed to be skipped, but must be explicitly identified as gated with the required environment/runtime prerequisites documented in the handoff.
- Runtime verification gate (MANDATORY): before declaring a server-side blocker fixed, verify the running service is on current binaries (not stale publish output) and document the verification command/output in handoff notes.
- OAuth contract check (MANDATORY when auth is involved): verify `client_id`, `redirect_uri`, and requested scopes exactly match server-registered OpenIddict client permissions before requesting cross-machine retries.
- Secret handling rule (MANDATORY): never commit raw bearer tokens/refresh tokens; share token acquisition steps and sanitized outputs only.
- Moderator relays a short "check for updates" message to the other machine.
- Moderator handoff prompt rule (MANDATORY): every ready-to-relay message must explicitly state the target machine name (for example: `mint22`, `mint-dnc-client`, `Windows11-TestDNC`).
- Other agent pulls latest, reads the handoff, and takes action without asking questions.

**Document maintenance:**
- Pre-commit archive rule (MANDATORY): before committing this file, move all completed/older handoff tasks to `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Keep only the single current task in **Active Handoff** (one active block only).
- If a task is completed, archive it first, then replace **Active Handoff** with the next task.

## Moderator Communication (Minimal)

**Moderator relays ONLY ONE OF THESE messages — nothing more:**

- `New handoff update for <target-machine>. Pull main and resume from 'Active Handoff' section.`
- `<Commit hash> — New handoff update for <target-machine>. Pull and check docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md Active Handoff.`

**No moderator task:** Moderator provides zero context, zero explanation. The handoff document has everything the receiving agent needs.

## Current Status

- All prior Phase 2, chat, and pre-Linux sync remediation work is complete and archived.
- P0 server-side sync hardening deployed and verified on `mint22`.
- Upload hardening story: CLOSED (2026-03-15). All machines verified.
- Deletion propagation story: **CLOSED** (2026-03-16). All three machines verified.
  - Linux client (`mint-dnc-client`): verified 2026-03-16 ~03:00Z
  - Windows client (`Windows11-TestDNC`): verified 2026-03-16 ~08:16Z. Bug fixed: `RemoveFileRecordsUnderPathAsync` path separator on Windows.
  - Server (`mint22`): confirmed stable 2026-03-16. Zero ERR entries, both nodes soft-deleted, no 5xx.
- Duplicate controller fix: CLOSED (2026-03-18). Deployed and verified on `mint22`. Files endpoint returns 401, service healthy.
- **Active cycle:** File browser fixes — folder child count fix (server-side `FileService.cs`), breadcrumb navigation (Android client). Server redeploy needed on `mint22` before child counts appear correctly.

## Environment

| Role | Machine | Detail |
|---|---|---|
| Server | `mint22` | `https://mint22:15443/` |
| Client | `Windows11-TestDNC` | Sync dir: `C:\Users\benk\Documents\synctray` |
| Client | `mint-dnc-client` | Linux Mint 22 validation host for desktop sync client implementation + E2E testing |
| Android Client | `monolith` | Android MAUI app development + emulator testing (Windows 11) |

## Key Carry-Forward Contracts

- Auth: OpenIddict bearer on files/sync endpoints via `FilesControllerBase` `[Authorize(AuthenticationSchemes = "OpenIddict.Validation.AspNetCore")]`.
- API envelope: middleware wraps responses; clients should unwrap via envelope helpers.
- Sync flow: changes -> tree -> reconcile -> chunk manifest -> chunk download -> file assembly.
- Desktop OAuth constant: `OAuthConstants.ClientId = "dotnetcloud-desktop"`.
- **SignalR channel group naming:** `chat-channel-{channelId}` (used by `ChatRealtimeService.ChannelGroup()` and Android `SignalRChatClient`).
- **Controller discovery:** Core.Server references Files.Host and Chat.Host via `ProjectReference`. ASP.NET Core auto-discovers controllers from referenced assemblies. Do NOT create duplicate controllers in Core.Server for routes already served by module Host assemblies.

## Active Handoff

### Windows IIS + Service Validation Option 2 — PostgreSQL + URL Rewrite Prereqs (for `Windows11-TestDNC`)

**Target:** `Windows11-TestDNC`  
**Status:** REQUIRES USER ACTION (prerequisites missing)  
**Priority:** P1

#### Summary

The Windows11-TestDNC validation cannot proceed because **two prerequisites are missing and require elevation**:

1. **PostgreSQL not installed/running** (CRITICAL BLOCKER) — prevents app startup entirely
2. **IIS URL Rewrite module not installed** (blocks reverse proxy setup)

Both require escalated privileges that the agent cannot use.

#### Required User Actions (Moderator/User on Windows11-TestDNC)

**Option A: Use Chocolatey/Winget (Recommended)**

Open **elevated PowerShell** and run:

```powershell
# Option A1: Chocolatey (if installed)
choco install postgresql -y
choco install urlrewrite -y

# Option A2: Winget (Windows 11 built-in)
winget install "PostgreSQL" --accept-source-agreements
winget install "IIS URL Rewrite Module" --accept-source-agreements
```

**Option B: Manual MSI Install**

1. **PostgreSQL:**  
   - Download: https://www.postgresql.org/download/windows/
   - During install, set password for `postgres` user to `postgres` (matches default connection string)
   - Accept default port `5432`
   - Let it auto-start as a Windows service

2. **IIS URL Rewrite:**  
   - Download: https://www.iis.net/downloads/microsoft/url-rewrite
   - Run the MSI installer
   - Accept default installation location

**Option C: Check If PostgreSQL Is Already Running**

If PostgreSQL was installed previously, just start the service:

```powershell
# Elevated PowerShell:
Start-Service PostgreSQL-x64-16  # (or appropriate version number)
Get-Service PostgreSQL*  # Find the exact name if unsure
```

#### After Prerequisites Are Installed

1. PostgreSQL should be running and listening on `localhost:5432`
2. URL Rewrite module should be loaded in IIS
3. **Re-run the installer with elevated PowerShell:**

```powershell
pwsh -ExecutionPolicy Bypass -File .\tools\install-windows.ps1 -SourcePath .\artifacts\publish -Beginner
```

4. The setup wizard will create the `dotnetcloud` database and configure everything
5. Report back in handoff with:
   - Installation success/failure
   - Service status: `Get-Service DotNetCloud`
   - Health check: `Invoke-WebRequest http://localhost:5080/health/live -UseBasicParsing`
   - IIS site status: `Get-Website DotNetCloud`

#### Request Back (MANDATORY after user completes prerequisites)

- PostgreSQL version installed and port `5432` confirmed open: `Test-NetConnection -ComputerName localhost -Port 5432`
- URL Rewrite installed confirmation: `Get-WebGlobalModule | Where-Object { $_.Name -eq 'RewriteModule' }`
- Installer re-ran successfully (output from installer)
- Service status after re-install: `Get-Service DotNetCloud`
- Health endpoint result: `Invoke-WebRequest http://localhost:5080/health/live -UseBasicParsing`
- IIS site and app pool status

#### Validation Run Result (2026-03-20, `Windows11-TestDNC`)

**Commit used:** `8e5d61f`

**Commands run (elevated):**

```powershell
pwsh -ExecutionPolicy Bypass -File .\tools\install-windows.ps1 -SourcePath .\artifacts\publish -Beginner -SkipFirewall -SkipFeatureInstall
```

```powershell
Start-Service DotNetCloud
Get-Service DotNetCloud
sc.exe qc DotNetCloud
Get-WinEvent -LogName System -MaxEvents 100 | Where-Object { $_.ProviderName -eq 'Service Control Manager' -and $_.Message -match 'DotNetCloud' } | Select-Object -First 5 | Format-List TimeCreated, Id, Message
```

**Raw output excerpts:**

- Installer precheck:
  - `[WARN] Missing IIS modules: URL Rewrite, Application Request Routing`
  - `Install the missing IIS modules above, then re-run this script.`
- Service start:
  - `Start-Service : Service 'DotNetCloud Core Server (DotNetCloud)' cannot be started...`
  - `Get-Service DotNetCloud` -> `Stopped`
- Service config:
  - `BINARY_PATH_NAME   : C:\Program Files\DotNetCloud\server\DotNetCloud.Core.Server.exe`
  - `SERVICE_START_NAME : LocalSystem`
- SCM errors:
  - `Id 7009: A timeout was reached (30000 milliseconds) while waiting for the DotNetCloud Core Server service to connect.`
  - `Id 7000: The DotNetCloud Core Server service failed to start ... The service did not respond to the start or control request in a timely fashion.`

**Interactive server diagnostics from installed binary:**

- Initial run failure:
  - `System.UnauthorizedAccessException: Access to the path 'C:\Program Files\DotNetCloud\server\oidc-keys' is denied.`
- Run with expected service env vars set:
  - Server proceeds past OIDC key path issue.
  - Then fails on DB connect:
  - `Npgsql.NpgsqlException: Failed to connect to 127.0.0.1:5432`
  - `SocketException (10061): No connection could be made because the target machine actively refused it.`

**First failing step:**

- Step 5 (service/runtime state verification). Service never reaches Running due to startup blockers.

**Root cause analysis (2026-03-20, diagnostics by agent):**

1. **PostgreSQL NOT INSTALLED** (CRITICAL): Port 5432 is unreachable. The default connection string expects `Host=localhost;Database=dotnetcloud;Username=postgres;Password=postgres`. The setup wizard (`dotnetcloud setup --beginner`) will detect this and stop with instructions to install PostgreSQL first. This is the PRIMARY blocker preventing the service from starting.
2. **URL Rewrite module missing** (ISSUE): File scan shows only ARR installed; URL Rewrite not found. Requires manual MSI download + elevated install. Blocks IIS reverse proxy configuration but NOT the service itself.
3. **OIDC keys directory** (SECONDARY): Directory `C:\Program Files\DotNetCloud\server\oidc-keys` doesn't exist yet; service needs to create it on first run. Requires service startup to succeed first (blocked by PostgreSQL).

**Verdict:** **FAIL** — PostgreSQL prerequisite missing. Windows11-TestDNC cannot complete startup sequence until database is available.

## Relay Template

```markdown
### Send to [Server|Client] Agent on <target-machine>
<message text including target machine>

### Request Back
- commit hash
- raw endpoint/URL used
- raw error/query params
- raw log lines around the event (with timestamp)
```
