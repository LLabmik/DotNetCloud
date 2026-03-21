# Client/Server Mediation Handoff

Last updated: 2026-03-21 (File browser child count fix — server redeploy needed on mint22)

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
- Windows IIS + Service Validation: **SERVICE RUNNING** (2026-03-21). Three blockers resolved. Kestrel healthy on :5080. IIS proxy deferred P2. Archived.
- **Active cycle:** File browser child count fix — server redeploy on `mint22` required. Code committed, awaiting deploy.

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

### File Browser Child Count Fix — Server Redeploy (for `mint22`)

**Target:** `mint22`  
**Status:** DEPLOYMENT BLOCKED ON TLS CERTIFICATE ERROR  
**Priority:** P1

#### Summary

Folder child count bug fix in `FileService.cs` was committed but deployment is blocked by TLS certificate issue. The fix ensures `ListChildrenAsync` and `ListRootAsync` return correct `childCount` values for folder nodes (previously always 0).

#### Server-side change (committed on `main`)

- `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/FileService.cs`
- Added `GetChildCountsAsync()` to batch-query child parent IDs and group counts in-memory.
- `ListChildrenAsync` and `ListRootAsync` now pass computed child counts to `ToDto(n, childCount)`.

#### Deployment Actions Completed

1. ✓ `git pull`
2. ✓ Publish succeeded: `dotnet publish` completed without errors to `/opt/dotnetcloud/server`
    - All binaries compiled and published correctly
    - No build-time errors

#### Blocker: TLS Certificate Issue

Service crashes immediately on startup with:
```
Interop+Crypto+OpenSslCryptographicException: error:10080002:BIO routines::system lib
  at System.Security.Cryptography.X509Certificates.OpenSslX509CertificateReader.FromFile()
  at KestrelConfiguration.ConfigureTls() line 162
```

**Diagnosis:**
- Certificate file exists: `/opt/dotnetcloud/server/certs/dotnetcloud-localhost.pfx`
- File permissions: `644` (readable by all)
- OpenSSL cannot parse it (likely corrupted during publish or binary/cert incompatibility)
- Tried: Copying working cert from `/opt/dotnetcloud/publish/certs/` — still crashes with same error
- All revisions of the certificate fail with identical OpenSSL error

**Next Steps (MANUAL INTERVENTION REQUIRED):**

Option A: Regenerate certificate on mint22
```bash
sudo systemctl stop dotnetcloud.service
# Run interactive setup (note: this is an interactive prompt):
sudo dotnetcloud setup
# Follow prompts, regenerate cert when asked
sudo systemctl start dotnetcloud.service
```

Option B: Revert to known-good binary (requires archival/versioning review — not yet implemented)

**AWAITING USER DECISION:** Choose Option A (regenerate cert interactively) or provide alternative resolution.

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
