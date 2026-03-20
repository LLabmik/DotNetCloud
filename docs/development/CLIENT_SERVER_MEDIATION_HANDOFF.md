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

### Windows IIS + Service Validation (Option 2) (for `Windows11-TestDNC`)

**Target:** `Windows11-TestDNC`
**Status:** READY FOR EXECUTION
**Priority:** P1

#### Goal

Validate the new Windows Option 2 deployment path end-to-end:

- IIS is public edge.
- DotNetCloud core server runs as a native Windows Service.
- IIS reverse proxies to `http://localhost:5080`.

#### Relevant Code/Docs to Pull

- `tools/install-windows.ps1`
- `docs/admin/server/WINDOWS_IIS_INSTALL_GUIDE.md`
- `docs/admin/server/WINDOWS_SERVICE_ARCHITECTURE_NOTES.md`
- `src/Core/DotNetCloud.Core.Server/Program.cs`
- `src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj`

#### Required Execution Steps on `Windows11-TestDNC`

1. Pull latest `main`.
2. Open elevated PowerShell.
3. Run installer (beginner path):

  ```powershell
  PowerShell -ExecutionPolicy Bypass -File .\tools\install-windows.ps1 -SourcePath .\artifacts\publish -Beginner
  ```

4. Confirm installer behavior for reverse-proxy prerequisites:
  - It should auto-attempt URL Rewrite + ARR install via `winget` when missing.
  - If auto-install fails, manually install URL Rewrite + ARR and rerun installer.
5. Confirm service and IIS runtime state:

  ```powershell
  Get-Service DotNetCloud
  Import-Module WebAdministration
  Get-WebGlobalModule | Where-Object { $_.Name -in @('RewriteModule','ApplicationRequestRouting') }
  Get-Website -Name DotNetCloud
  Get-WebAppPoolState -Name DotNetCloud
  Invoke-WebRequest http://localhost:5080/health/live -UseBasicParsing
  Invoke-WebRequest http://localhost/health/live -UseBasicParsing
  ```

6. Reboot Windows host once, then re-check `Get-Service DotNetCloud` and both health endpoints.

#### Pass Criteria

- DotNetCloud service exists, is `Running`, and survives reboot.
- IIS site/app pool exist and are started.
- Both modules present: `RewriteModule`, `ApplicationRequestRouting`.
- Local app health works: `http://localhost:5080/health/live`.
- IIS-proxied health works: `http://localhost/health/live` (or configured host header URL).

#### If Blocked

Capture and return exact errors for:

- installer output lines around failure
- `Get-WinEvent` snippets from Application/System logs
- `sc.exe qc DotNetCloud`
- resulting `web.config` under installed server path

#### Request Back (MANDATORY)

- commit hash used
- exact command run
- raw command outputs for all verification commands above
- whether IIS features were auto-installed or manually installed
- whether URL Rewrite/ARR were auto-installed or manually installed
- service status before and after reboot
- final verdict: PASS/FAIL with first failing step

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
