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
- Unexpected untracked content rule (MANDATORY): remove unexpected untracked files/directories before commit; only keep intentional tracked changes for the handoff update.
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

### Client Follow-up - Live Probe Executable, Awaiting Mobile Token Run (Phase 2.10)

**Date:** 2026-03-11  
**Owner:** Client (`Windows11-TestDNC`)  
**Status:** Executable tests passing; live probe now wired and awaiting token + sender trigger

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

✓ Live-filtered probe is discoverable and env-gated:
- `dotnet test tests/DotNetCloud.Client.Android.Tests/DotNetCloud.Client.Android.Tests.csproj -c Release --filter "Live"`
- Result: `total: 1, failed: 0, succeeded: 0, skipped: 1`
- Skip reason: `DOTNETCLOUD_E2E_BEARER_TOKEN` not set (expected until runtime token is provided).

✓ OAuth contract re-verified on server (`mint22`) with live endpoints + DB:
- Authorize probe (mobile contract params) returns expected login challenge:
   - `GET /connect/authorize?...client_id=dotnetcloud-mobile&redirect_uri=net.dotnetcloud.client://oauth2redirect&scope=openid profile offline_access files:read files:write...`
   - Result: `HTTP/2 302` -> `/auth/login` (no `invalid_client`)
- OpenIddict application registration confirms exact mobile contract:
   - `ClientId`: `dotnetcloud-mobile`
   - `RedirectUris`: `["net.dotnetcloud.client://oauth2redirect"]`
   - `Permissions`: includes `scp:openid`, `scp:profile`, `scp:offline_access`, `scp:files:read`, `scp:files:write`, plus auth-code/refresh grants and authorize/token/revocation endpoints.

✓ Full executable suite passed:
- `dotnet test --nologo --logger "trx;LogFileName=full-suite.trx"`
- Result: `total: 2041, failed: 0, succeeded: 2028, skipped: 13`

**Environment-gated test (expected skip until token exists):**
- `ConnectAsync_SubscribesAndReceivesEvents_Live` is now executable (no `[Ignore]`) and attempts real hub connection.
- It requires `DOTNETCLOUD_E2E_BEARER_TOKEN`; without token it exits as inconclusive by design.
- Optional env vars for controlled assertions:
   - `DOTNETCLOUD_E2E_BASE_URL` (default `https://mint22:15443`)
   - `DOTNETCLOUD_E2E_EXPECTED_CHANNEL_ID`

**Latest local execution evidence (2026-03-10, Windows11-TestDNC):**
- Verified token env var status:
   - PowerShell check returned `DOTNETCLOUD_E2E_BEARER_TOKEN=MISSING`.
- Re-ran live probe with detailed logs:
   - `dotnet test tests/DotNetCloud.Client.Android.Tests/DotNetCloud.Client.Android.Tests.csproj -c Release --filter "Live" --logger "console;verbosity=detailed"`
   - Result: `total: 1, failed: 0, succeeded: 0, skipped: 1`
   - Skip reason (explicit): `Assert.Inconclusive failed. DOTNETCLOUD_E2E_BEARER_TOKEN is not set.`

**Latest rerun after handoff pull (2026-03-10, Windows11-TestDNC):**
- Re-validated env in current shell:
   - `DOTNETCLOUD_E2E_BEARER_TOKEN=MISSING`
- Re-executed the same live probe command:
   - Result: `total: 1, failed: 0, succeeded: 0, skipped: 1`
   - Skip reason unchanged: `Assert.Inconclusive failed. DOTNETCLOUD_E2E_BEARER_TOKEN is not set.`

**Next action to complete live E2E:**
1. Obtain fresh mobile user access token from Android OAuth login (`dotnetcloud-mobile`, auth-code + PKCE).
2. Set PowerShell env var on client machine:
   - `$env:DOTNETCLOUD_E2E_BEARER_TOKEN = "<mobile-user-access-token>"`
3. Run:
   - `dotnet test tests/DotNetCloud.Client.Android.Tests/DotNetCloud.Client.Android.Tests.csproj -c Release --filter "Live"`
4. Trigger sender-side event:
   - `POST /api/v1/chat/channels/{channelId}/messages?userId={senderUserId}` with bearer token.

**One-pass runtime checklist (Windows11-TestDNC):**
- Verify token var is present before test:
   - `$env:DOTNETCLOUD_E2E_BEARER_TOKEN`
   - If blank, stop and reacquire token first.
- Optional strict channel assertion:
   - `$env:DOTNETCLOUD_E2E_EXPECTED_CHANNEL_ID = "<channelId>"`
- Run live probe with detailed console:
   - `dotnet test tests/DotNetCloud.Client.Android.Tests/DotNetCloud.Client.Android.Tests.csproj -c Release --filter "Live" --logger "console;verbosity=detailed"`
- While test is waiting, send one message event:
   - `POST /api/v1/chat/channels/{channelId}/messages?userId={senderUserId}`
- Success evidence to return:
   - test line indicating hub connection started/completed
   - test line(s) showing `UnreadCountUpdated`
   - test line(s) showing `NewMessage`
- Failure evidence to return:
   - exact test output line (auth or timeout)
   - exact authorize URL/query used by Android login flow
   - exact sender endpoint used for trigger

**Request back (server/moderator relay):**
- commit hash from runtime run machine
- live output lines showing connection + `UnreadCountUpdated` + `NewMessage`
- if token acquisition fails: exact callback/error text and endpoint params used
- if auth still fails unexpectedly: include the exact `/connect/authorize` query string used by the Android app
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
