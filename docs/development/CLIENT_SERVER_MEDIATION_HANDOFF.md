# Client/Server Mediation Handoff

Last updated: 20260325 (WS-4 API verification bootstrap active for monolith)

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
- **Active cycle (20260325):** WS-4 live verification bootstrap in progress. Monolith is collecting auth/runtime artifacts for TC-1.27, TC-1.40, TC-1.41, TC-1.42, and TC-1.45 against `mint22`.

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

**Target machine:** mint22
**Status:** BLOCKER - AWAITING SERVER-SIDE INVESTIGATION (20260325)

### WS-4 API Verification Bootstrap For mint22 Deployment — FINDINGS

**Goal:** Collect auth/runtime inputs for WS-4 Copilot-capable checks

**Bootstrap attempt:** 20260325 ~07:20 UTC from monolith (Windows 11)

#### Results Summary

| Test | Status | Finding |
|------|--------|---------|
| Server connectivity | PASS | `https://mint22:5443/` responds; HTTP 302 redirect to `/auth/login` |
| TC-1: Login | PARTIAL | Success: HTTP 200, `userId` received; **BLOCKER: `accessToken` returned empty** |
| TC-1.40 | BLOCKED | Cannot proceed without bearer token |
| TC-1.41 | BLOCKED | Cannot proceed without bearer token |
| TC-1.42 | BLOCKED | Cannot proceed without bearer token |
| TC-1.45 | BLOCKED | Cannot proceed without bearer token |
| TC-1.27 | BLOCKED | Cannot proceed without bearer token |

#### Detailed Findings

**Login endpoint test:**
- Endpoint: `POST https://mint22:5443/api/v1/core/auth/login`
- Request: `{"email":"testdude@llabmik.net","password":"<PASSWORD>"}`
- **Response (successful):**
  ```json
  {
    "success": true,
    "data": {
      "accessToken": "",
      "refreshToken": "",
      "expiresIn": 0,
      "tokenType": "Bearer",
      "userId": "019d1fd0-7277-7724-8ddd-d1a74b9fc32a",
      "displayName": "Test Dude"
    }
  }
  ```
- **Root cause:** `accessToken` and `refreshToken` fields are empty strings despite HTTP 200 success response

#### Client-side observations

1. **SSL/TLS:** Server uses self-signed certificate; curl works with `-k` flag; PowerShell `Invoke-RestMethod` requires certificate policy override
2. **API response:** Server correctly authenticates the user (returns `userId` and `displayName`), but token fields are null/empty
3. **Timestamp:** Request made 2026-03-25T07:20:42Z
4. **NetworkReachability:** mint22:5443 is reachable from monolith via HTTPS

#### Blocking Issue

**The server is not returning bearer tokens** even though the login is successful. This prevents all downstream WS-4 API checks (sync, reconcile, WOPI) from executing.

**Hypothesis:** 
- Token generation/serialization issue on mint22
- Possible causes: missing configuration, expired signing key, JWT generation disabled, or response DTO field mapping error

#### Next Steps Required (Server-side: mint22)

1. **Verify token generation pipeline**
   - Check OpenIddict configuration: is token generation enabled?
   - Verify signing certificate/key is valid and loaded
   - Check `IdentityController` or `AuthController` implementation: is `accessToken` being populated before response?

2. **Investigate response DTO**
   - Confirm login response DTO correctly maps generated token to `data.accessToken`
   - Check for serialization filters that might null out token fields

3. **Review logs**
   - Mint22 app logs: any errors during token generation?
   - Check if user authentication succeeded (it did) but token generation failed silently

4. **Test directly on server**
   - Attempt login via Swagger/OpenAPI UI on mint22 console
   - Verify token is generated when called locally

**Handoff ready when:**
- Server-side team confirms token generation is now working and returns non-empty `accessToken`
- A test login from monolith successfully returns a bearer token
- Monolith re-runs WS-4 API bootstrap and completes all 6 tests
