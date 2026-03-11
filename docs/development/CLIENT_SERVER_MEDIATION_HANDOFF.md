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
- Runtime verification gate (MANDATORY): before declaring a server-side blocker fixed, verify the running service is on current binaries (not stale publish output) and document the verification command/output in handoff notes.
- OAuth contract check (MANDATORY when auth is involved): verify `client_id`, `redirect_uri`, and requested scopes exactly match server-registered OpenIddict client permissions before requesting cross-machine retries.
- Secret handling rule (MANDATORY): never commit raw bearer tokens/refresh tokens; share token acquisition steps and sanitized outputs only.
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

### Client Follow-up - OAuth Browser Launch Fixed, Ready for Live Token Run (Phase 2.10)

**Date:** 2026-03-11  
**Owner:** Client (`Windows11-TestDNC`)  
**Status:** Executable tests passing; environment-gated live E2E pending token input

**Client implementation completed:**

✓ Fixed OAuth mobile auth-code flow launch step in Android client:
- File: `src/Clients/DotNetCloud.Client.Android/Auth/MauiOAuth2Service.cs`
- Change: added system browser launch before callback wait:
  - `await Browser.OpenAsync(authUrl, BrowserLaunchMode.SystemPreferred)`
- Result: auth flow now actually opens the authorize URL instead of waiting indefinitely.

**Readiness gate (mandatory) status:**

✓ Android client build passed:
- `dotnet build src/Clients/DotNetCloud.Client.Android/DotNetCloud.Client.Android.csproj -f net10.0-android`

✓ Android client test project passed:
- `dotnet test tests/DotNetCloud.Client.Android.Tests/DotNetCloud.Client.Android.Tests.csproj`
- Result: `total: 9, failed: 0, succeeded: 8, skipped: 1`

✓ Full executable suite passed:
- `dotnet test --nologo --logger "trx;LogFileName=full-suite.trx"`
- Result: `total: 2041, failed: 0, succeeded: 2028, skipped: 13`

**Environment-gated test (expected skip):**
- `ConnectAsync_SubscribesAndReceivesEvents_Live` remains `[Ignore]` until a real mobile user bearer token is provided at runtime.
- Prerequisites remain documented in test and this handoff.

**Next action to complete live E2E:**
1. Obtain fresh mobile user access token from Android OAuth login (`dotnetcloud-mobile`, auth-code + PKCE).
2. Set PowerShell env var on client machine:
   - `$env:DOTNETCLOUD_E2E_BEARER_TOKEN = "<mobile-user-access-token>"`
3. Remove `[Ignore]` from `ConnectAsync_SubscribesAndReceivesEvents_Live`.
4. Run:
   - `dotnet test tests/DotNetCloud.Client.Android.Tests/DotNetCloud.Client.Android.Tests.csproj --filter "Live"`
5. Trigger sender-side event:
   - `POST /api/v1/chat/channels/{channelId}/messages?userId={senderUserId}` with bearer token.

**Request back (server/moderator relay):**
- confirm channel ID + sender user context for trigger message
- if token acquisition still fails: exact callback/error text and endpoint params used
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
