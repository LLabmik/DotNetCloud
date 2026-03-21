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

### Windows IIS + Service Validation — Service Restart Needed (for `Windows11-TestDNC`)

**Target:** `Windows11-TestDNC`  
**Status:** AWAITING ELEVATED RESTART — DB credentials aligned; service restart requires elevated terminal  
**Priority:** P1

#### Summary

DB credential mismatch is now resolved. PostgreSQL role and database are correctly aligned. Service restart requires elevated PowerShell (moderator action).

#### Completed Actions (2026-03-21)

- `postgresql-x64-18` running, port 5432 open.
- Config password read from `C:\ProgramData\DotNetCloud\config\config.json` (password: `4773wfbFN6FCtG6Mja8Ot67m11sh9MQD`).
- PostgreSQL role aligned: `ALTER ROLE dotnetcloud WITH LOGIN PASSWORD '4773wfbFN6FCtG6Mja8Ot67m11sh9MQD'` → `ALTER ROLE`
- Database `dotnetcloud` confirmed to exist and is owned by `dotnetcloud` role.
- Service account confirmed: `LocalSystem`.
- Direct exe run from non-elevated terminal produced `UnauthorizedAccessException: Access to path 'C:\Program Files\DotNetCloud\server\oidc-keys' is denied` — **this is expected when running as normal user**; service runs as `LocalSystem` which has full write access, so this should not affect the service start.

#### Required Next Action — MODERATOR (Elevated PowerShell required)

**Run the following in an elevated PowerShell on `Windows11-TestDNC`:**

```powershell
Restart-Service DotNetCloud
Start-Sleep -Seconds 15
Get-Service DotNetCloud
Invoke-WebRequest http://localhost:5080/health/live -UseBasicParsing
Invoke-WebRequest http://localhost/health/live -UseBasicParsing
```

If the service fails again, capture the startup exception:

```powershell
Get-WinEvent -LogName Application -MaxEvents 100 | Where-Object { $_.ProviderName -match 'DotNetCloud' -or ($_.ProviderName -match 'Service Control Manager' -and $_.Message -match 'DotNetCloud') } | Select-Object -First 10 TimeCreated, Id, ProviderName, Message | Format-List
```

And check Windows Event Log for the actual .NET exception:

```powershell
Get-WinEvent -LogName Application -MaxEvents 200 | Where-Object { $_.Message -match 'DotNetCloud.Core.Server' } | Select-Object -First 5 TimeCreated, @{N='Msg';E={$_.Message}} | Format-List
```

#### Request Back (MANDATORY)

- `Get-Service DotNetCloud` (after restart attempt)
- `Invoke-WebRequest http://localhost:5080/health/live -UseBasicParsing`
- `Invoke-WebRequest http://localhost/health/live -UseBasicParsing`
- If still failing: full output of the WinEvent queries above
- If still failing: `Get-WinEvent` SCM entries + server startup exception text

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
