# Client/Server Mediation Handoff

Last updated: 2026-03-24 (Phase 3.6 Migration Foundation complete)

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
- Windows IIS + Service Validation: **COMPLETE** (2026-03-21). Three startup blockers resolved. IIS reverse proxy configured and verified (URL Rewrite + ARR). HTTP (port 80) and HTTPS (port 443) both proxy to Kestrel :5080. Self-signed localhost cert bound.
- File browser child count fix: **DEPLOYED** (2026-03-21). `mint22` redeployed; service stable.
- `mint22` connectivity diagnosis: **COMPLETE** (2026-03-22). Current deployment listens directly on HTTPS `:5443`; no listener exists on `:15443`.
- Security audit desktop client validation on `Windows11-TestDNC`: **COMPLETE** (2026-03-23).
- Security audit closeout + merge validation on `mint22`: **COMPLETE** (2026-03-23).
- Post-closeout Windows runtime smoke: **COMPLETE** (2026-03-23). 4/4 targeted tests passed; login launch path verified reachable.
- **Active cycle:** Phase 3.8 Documentation And Release Readiness complete. Milestone D (Import + Hardening + Docs) fully closed.

## Environment

| Role | Machine | Detail |
|---|---|---|
| Server | `mint22` | `https://mint22:5443/` |
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

**Target machine:** monolith
**Status:** READY

### WS-4 API Verification Bootstrap For mint22 Deployment

Goal: unblock WS-4 Copilot-capable checks by collecting auth/runtime inputs and executing API verification from a Windows admin session on monolith.

Server under test:
- `https://mint22:5443`

Use test account:
- Email: `testdude@llabmik.net`
- Password: provided out-of-band by moderator

#### Required work

1. Acquire bearer token via API login
   - Endpoint: `POST https://mint22:5443/api/v1/core/auth/login`
   - Capture `data.accessToken` and `data.refreshToken` from response
   - Do NOT commit raw tokens to git; report only masked values in handoff notes

2. Execute WS-4 API checks (PowerShell)
   - TC-1.40: `GET /api/v1/files/sync/changes?since=<timestamp>`
   - TC-1.42: `GET /api/v1/files/sync/tree`
   - TC-1.41: `POST /api/v1/files/sync/reconcile`

3. Determine one valid file id
   - Use sync tree response or file API responses to pick one accessible file id
   - Record the file id in handoff notes

4. Execute range request check with the discovered file id
   - TC-1.45: `GET /api/v1/files/{fileId}/download` with `Range` headers
   - Verify `206 Partial Content` and `Content-Range`

5. Attempt WOPI CheckFileInfo two ways
   - First try bearer auth against `GET /api/v1/wopi/files/{fileId}`
   - If bearer fails, capture WOPI `access_token` from Collabora launch flow and call:
     - `GET /api/v1/wopi/files/{fileId}?access_token=<token>`

6. Return artifacts for relay
   - HTTP status lines and key response fields for each test case
   - Sanitized token evidence (first 12 chars + length only)
   - Discovered `fileId`
   - Whether WOPI required query token or accepted bearer auth

#### PowerShell command template (run on monolith)

```powershell
$BaseUrl = "https://mint22:5443"
$Since = "2026-03-25T00:00:00Z"

# 1) Login
$loginBody = @{ email = "testdude@llabmik.net"; password = "<PASSWORD>" } | ConvertTo-Json
$login = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/v1/core/auth/login" -Body $loginBody -ContentType "application/json" -SkipCertificateCheck
$token = $login.data.accessToken

$headers = @{ Authorization = "Bearer $token" }

# 2) Sync changes (TC-1.40)
$changes = Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/v1/files/sync/changes?since=$Since" -Headers $headers -SkipCertificateCheck

# 3) Sync tree (TC-1.42)
$tree = Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/v1/files/sync/tree" -Headers $headers -SkipCertificateCheck

# 4) Pick a fileId from tree payload (adjust path to match actual schema)
# Example placeholder:
# $fileId = $tree.data.items | Where-Object { $_.type -eq "file" } | Select-Object -First 1 -ExpandProperty id

# 5) Reconcile (TC-1.41)
$reconcileBody = @{
  items = @(
    @{
      path = "/Documents/example.txt"
      hash = "abc123"
      lastModifiedUtc = "2026-03-25T00:00:00Z"
      size = 128
    }
  )
} | ConvertTo-Json -Depth 5
$reconcile = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/v1/files/sync/reconcile" -Headers $headers -Body $reconcileBody -ContentType "application/json" -SkipCertificateCheck

# 6) Range test (TC-1.45)
# Replace <FILE_ID> with discovered id if not set in script.
$fileId = "<FILE_ID>"
$rangeHeaders1 = @{ Authorization = "Bearer $token"; Range = "bytes=0-1048575" }
$rangeHeaders2 = @{ Authorization = "Bearer $token"; Range = "bytes=1048576-" }
Invoke-WebRequest -Uri "$BaseUrl/api/v1/files/$fileId/download" -Headers $rangeHeaders1 -OutFile "$env:TEMP\part1.bin" -SkipCertificateCheck
Invoke-WebRequest -Uri "$BaseUrl/api/v1/files/$fileId/download" -Headers $rangeHeaders2 -OutFile "$env:TEMP\part2.bin" -SkipCertificateCheck

# 7) WOPI bearer attempt (TC-1.27)
# If this fails, capture access_token from Collabora request and retry with query token.
try {
  $wopi = Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/v1/wopi/files/$fileId" -Headers $headers -SkipCertificateCheck
  "WOPI bearer: success"
}
catch {
  "WOPI bearer: failed"
}
```

#### Return format (for relay back)

- Commit hash
- For each test (TC-1.40, TC-1.41, TC-1.42, TC-1.45, TC-1.27): HTTP status + brief result
- `fileId` used
- Token evidence: masked access token prefix + total length
- WOPI auth mode used: bearer or query `access_token`

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
