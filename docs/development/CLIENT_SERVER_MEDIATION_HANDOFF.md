# Client/Server Mediation Handoff

Last updated: 2026-03-11 (compacted active-only handoff)

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
- Handoff readiness gate (MANDATORY): all executable tests must pass before marking a handoff as ready.
- Environment-gated tests are allowed to be skipped, but must be explicitly identified as gated with the required environment/runtime prerequisites documented in the handoff.
- Moderator relays a short "check for updates" message to the other machine.
- Other agent pulls latest, reads the handoff, and takes action without asking questions.

**Document maintenance:**
- Pre-commit archive rule (MANDATORY): before committing this file, move all completed/older handoff tasks to `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Keep only the single current task in **Active Handoff** (one active block only).
- If a task is completed, archive it first, then replace **Active Handoff** with the next task.

## Moderator Communication (Minimal)

**Moderator relays ONLY ONE OF THESE messages — nothing more:**

- `New handoff update. Pull main and resume from 'Active Handoff' section.`
- `<Commit hash> — New handoff update. Pull and check docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md Active Handoff.`

**No moderator task:** Moderator provides zero context, zero explanation. The handoff document has everything the receiving agent needs.

## Current Status

- Issues #1-#45 and previous sprint/batch closeout work: complete.
- Active cross-machine item: Android contract alignment follow-through (Phase 2.10).
- This handoff was compacted on 2026-03-11 to remove completed historical sections from active view.

## Environment

| Role | Machine | Detail |
|---|---|---|
| Server | `mint22` | `https://mint22:15443/` |
| Client | `Windows11-TestDNC` | Sync dir: `C:\Users\benk\Documents\synctray` |

## Key Carry-Forward Contracts

- Auth: OpenIddict bearer on files/sync endpoints via `FilesControllerBase` `[Authorize]`.
- API envelope: middleware wraps responses; clients should unwrap via envelope helpers.
- Sync flow: changes -> tree -> reconcile -> chunk manifest -> chunk download -> file assembly.
- Desktop OAuth constant: `OAuthConstants.ClientId = "dotnetcloud-desktop"`.

## Active Handoff

### Server Response - Mobile Token Blocker Fixed (Phase 2.10)

**Date:** 2026-03-11  
**Owner:** Server (`mint22`)  
**Status:** Blocker cleared; live test can proceed with mobile PKCE token

**Readiness gate (mandatory) status:**

✓ Executable tests passed on server repo:
- Command: `dotnet test tests/DotNetCloud.Client.Android.Tests/DotNetCloud.Client.Android.Tests.csproj -c Release`
- Result: `total: 9, failed: 0, succeeded: 8, skipped: 1`
- Gated test identified: live E2E method remains skipped until token is provided at runtime.

**Server token findings (live-validated):**

✓ Root cause found: running `dotnetcloud.service` was using stale published binaries (Mar 8) that did not include current mobile registration behavior.
✓ Server redeployed from current `main` and service restarted.
✓ Post-fix probe now succeeds for mobile client id:
- `GET /connect/authorize?...client_id=dotnetcloud-mobile...` -> `302` to `/auth/login` (expected challenge, no `invalid_client`).
✓ Android OAuth scope string fixed in code to supported scopes:
- `openid profile offline_access files:read files:write`
- File: `src/Clients/DotNetCloud.Client.Android/Auth/MauiOAuth2Service.cs`

**Use this now for live E2E run (mobile path):**

1. Obtain a fresh **mobile** user token via Android OAuth login (`dotnetcloud-mobile`, auth-code + PKCE).
2. Set env var on client machine:
	- PowerShell: `$env:DOTNETCLOUD_E2E_BEARER_TOKEN = "<mobile-user-access-token>"`
3. In `tests/DotNetCloud.Client.Android.Tests/Chat/SignalRChatClientE2eTests.cs`, remove `[Ignore(...)]` from `ConnectAsync_SubscribesAndReceivesEvents_Live`.
4. Run:
	- `dotnet test tests/DotNetCloud.Client.Android.Tests/DotNetCloud.Client.Android.Tests.csproj --filter "Live"`
5. Trigger event from sender side:
	- `POST /api/v1/chat/channels/{channelId}/messages?userId={senderUserId}` with bearer token.

**Security note:**
- Do not commit raw bearer tokens into git/handoff docs.

**Request back (client):**
- commit hash
- live test output lines for connection + `UnreadCountUpdated` + `NewMessage`
- if failure: exact auth error + raw endpoint/params used
## Relay Template

```markdown
### Send to [Server|Client] Agent
<message text>

### Request Back
- commit hash
- raw endpoint/URL used
- raw error/query params
- raw log lines around the event (with timestamp)
```
