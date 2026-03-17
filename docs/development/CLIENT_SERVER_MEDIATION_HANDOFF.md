# Client/Server Mediation Handoff

Last updated: 2026-03-16 (Deletion propagation chain CLOSED — all 3 steps verified)

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
- **Active cycle:** Server redeploy requested — Chat.Host + Files.Host references added to Core.Server.

## Environment

| Role | Machine | Detail |
|---|---|---|
| Server | `mint22` | `https://mint22:15443/` |
| Client | `Windows11-TestDNC` | Sync dir: `C:\Users\benk\Documents\synctray` |
| Client | `mint-dnc-client` | Linux Mint 22 validation host for desktop sync client implementation + E2E testing |
| Android Client | `monolith` | Android MAUI app development + emulator testing (Windows 11) |

## Key Carry-Forward Contracts

- Auth: OpenIddict bearer on files/sync endpoints via `FilesControllerBase` `[Authorize]`.
- API envelope: middleware wraps responses; clients should unwrap via envelope helpers.
- Sync flow: changes -> tree -> reconcile -> chunk manifest -> chunk download -> file assembly.
- Desktop OAuth constant: `OAuthConstants.ClientId = "dotnetcloud-desktop"`.

## Active Handoff

### Server Redeploy — Chat.Host + Files.Host Endpoint Registration

**Target machine:** `mint22`
**Priority:** Normal
**Requested by:** `Windows11-TestDNC` (client agent)

#### What Changed

`DotNetCloud.Core.Server.csproj` now references two additional Host projects:

- `DotNetCloud.Modules.Chat.Host` — registers Chat API endpoints (ChatController)
- `DotNetCloud.Modules.Files.Host` — registers Files API endpoints

Without these references, the server binary does not include the Chat/Files controller assemblies, so their API endpoints are not discovered or mapped at startup.

#### Action Required

1. `git pull origin main` to get the latest changes.
2. Run the bare-metal redeploy script:
   ```bash
   cd /path/to/dotnetcloud   # wherever the repo is cloned on mint22
   ./tools/redeploy-baremetal.sh
   ```
   This will:
   - `dotnet publish` the server project in Release mode
   - Restart `dotnetcloud.service` via systemd
   - Probe the health endpoint to confirm the service is live
3. After redeploy, verify the Chat endpoints are registered by hitting:
   ```bash
   curl -kfsS https://localhost:15443/api/chat/health
   ```
   Expected: HTTP 200 with a health response (not 404).

#### Verification Checklist

- [ ] `dotnet publish` succeeds with no errors
- [ ] `dotnetcloud.service` restarts without failure
- [ ] Health probe passes (`/health/live` returns 200)
- [ ] Chat API endpoint reachable (`/api/chat/health` returns 200, not 404)
- [ ] No ERR entries in `journalctl -u dotnetcloud.service --since "5 min ago"`

#### Hand Off To

**Next target:** `monolith` (Android client agent)

After redeploy is verified, update this handoff with results and mark target as `monolith` so the Android client can rebuild the APK and test channel loading against the newly-registered Chat API endpoints.

#### Request Back

- Commit hash on mint22 after pull
- Output of `curl -kfsS https://localhost:15443/api/chat/health`
- Output of `systemctl status dotnetcloud.service --no-pager` (first 10 lines)
- Any errors from `journalctl -u dotnetcloud.service --since "5 min ago" | grep -i err`

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
