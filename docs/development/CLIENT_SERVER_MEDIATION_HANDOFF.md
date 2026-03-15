# Client/Server Mediation Handoff

Last updated: 2026-03-15 (P1 sync hardening — device identity, echo suppression, per-device rate limiting deployed server-side)

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
- Client-side upload dedup + echo suppression (commit `4c575cc`) verified on all machines:
  - **Linux (`mint-dnc-client`):** runtime verified — single initiate per event, no conflict copies.
  - **Windows (`Windows11-TestDNC`):** runtime verified on MSIX `0.23.3.0` — single initiate per event, no conflict copies.
  - **Server (`mint22`):** zero 5xx errors since deployment; only normal token-refresh 401s observed.
- **Upload hardening story: CLOSED.** Full chain verification complete across all three machines.

## Environment

| Role | Machine | Detail |
|---|---|---|
| Server | `mint22` | `https://mint22:15443/` |
| Client | `Windows11-TestDNC` | Sync dir: `C:\Users\benk\Documents\synctray` |
| Client | `mint-dnc-client` | Linux Mint 22 validation host for desktop sync client implementation + E2E testing |

## Key Carry-Forward Contracts

- Auth: OpenIddict bearer on files/sync endpoints via `FilesControllerBase` `[Authorize]`.
- API envelope: middleware wraps responses; clients should unwrap via envelope helpers.
- Sync flow: changes -> tree -> reconcile -> chunk manifest -> chunk download -> file assembly.
- Desktop OAuth constant: `OAuthConstants.ClientId = "dotnetcloud-desktop"`.

## Active Handoff

### P1 Sync Hardening — Client-Side Device Identity Deployment

**Date:** 2026-03-15
**Owner:** `mint-dnc-client` (Linux), `Windows11-TestDNC` (Windows)
**Status:** READY FOR CLIENT DEPLOYMENT

#### What Changed (Server)

Server-side P1 sync hardening is complete and committed on `main`. Three features implemented:

1. **P1.1 — Device Identity Registration**
   - New `SyncDevice` table, auto-registered on first contact
   - `FileNode.OriginatingDeviceId` tracks which device created/modified each file
   - `ChunkedUploadSession.DeviceId` tracks upload source
   - `DeviceIdentityFilter` (global MVC filter) extracts `X-Device-Id`, `X-Device-Name`, `X-Device-Platform`, `X-Client-Version` headers
   - EF migration `SyncDeviceIdentity` generated (adds table, columns, indexes, FK)

2. **P1.2 — Echo Suppression**
   - `SyncChangeDto` now includes `OriginatingDeviceId`
   - Server populates it from `FileNode.OriginatingDeviceId` in all sync queries
   - Client-side: `SyncEngine.HandleRemoteUpdateAsync` skips download when `change.OriginatingDeviceId == DeviceId` and local hash matches remote hash

3. **P1.3 — Per-Device Rate Limiting**
   - Rate limit partition key changed from `userId` to `{module}:{userId}:{deviceId}` when module config has `PerDevice = true`
   - `X-RateLimit-Remaining: 0` header added on 429 rejections

#### Client-Side Changes (Already Committed)

The following client-side files were modified and are on `main`:

- **`src/Clients/DotNetCloud.Client.Core/Api/DeviceIdProvider.cs`** (NEW) — generates stable per-installation device GUID, persists to `device-id` file in data directory
- **`src/Clients/DotNetCloud.Client.Core/Api/DeviceIdentityHandler.cs`** (NEW) — `DelegatingHandler` that adds `X-Device-Id`, `X-Device-Name`, `X-Device-Platform`, `X-Client-Version` headers to every request
- **`src/Clients/DotNetCloud.Client.Core/Api/ApiModels.cs`** — `SyncChangeResponse` gained `OriginatingDeviceId` property
- **`src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs`** — added `DeviceId` property, device-aware echo suppression in `HandleRemoteUpdateAsync`
- **`src/Clients/DotNetCloud.Client.SyncService/ContextManager/SyncContextManager.cs`** — wires up `DeviceIdProvider` and `DeviceIdentityHandler` into HTTP pipeline, sets `DeviceId` on `SyncEngine`
- **`src/Clients/DotNetCloud.Client.SyncService/SyncServiceExtensions.cs`** — adds `DeviceIdentityHandler` to named HttpClient DI pipeline

#### Action Required on Client Machines

1. `git pull origin main`
2. Rebuild the sync client: `dotnet build src/Clients/DotNetCloud.Client.SyncService/`
3. Restart the sync service
4. Verify device ID persistence: check for `device-id` file in the sync data directory
5. Verify headers: observe sync requests include `X-Device-Id` header (check server logs or use curl)
6. Test echo suppression: upload a file, verify next sync pass doesn't re-download it

#### Verification Criteria

- `device-id` file created in data directory on first run
- All API requests include `X-Device-Id` header
- Server `SyncDevices` table shows registered device entry
- Echo suppression: uploading a file doesn't trigger re-download on the same device
- No regressions in normal sync flow

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
