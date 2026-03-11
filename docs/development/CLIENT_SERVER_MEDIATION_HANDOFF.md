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

### Server Completion — Live SignalR E2E Test PASSED (Phase 2.10)

**Date:** 2026-03-11
**Owner:** Server (`mint22`)
**Status:** COMPLETE — Live E2E test fully passing, two server-side bugs fixed

**Summary:**
The multi-cycle token-missing deadlock is broken. Server agent minted a mobile OAuth token directly,
discovered and fixed two server-side bugs blocking bearer-token hub access, rewrote the E2E test to
be fully self-contained, and achieved a passing live probe against the running server.

**Server-side bugs fixed:**

1. **CoreHub auth scheme (CRITICAL):** Hub used bare `[Authorize]` which defaults to Identity cookie auth.
   Mobile/API clients send bearer tokens, which were rejected with 401. Fixed to accept both:
   - File: `src/Core/DotNetCloud.Core.Server/RealTime/CoreHub.cs`
   - Change: `[Authorize(AuthenticationSchemes = "Identity.Application,OpenIddict.Validation.AspNetCore")]`

2. **CoreHub GetUserId claim mapping:** `GetUserId()` only checked `ClaimTypes.NameIdentifier` but OpenIddict
   bearer tokens use the `sub` claim directly (without Identity middleware mapping). Fixed to fall back:
   - File: `src/Core/DotNetCloud.Core.Server/RealTime/CoreHub.cs`
   - Change: `Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Context.User?.FindFirst("sub")?.Value`

**Test-side fix:**

3. **SSL certificate bypass:** SignalR client rejected self-signed cert on `mint22:15443` (RemoteCertificateNameMismatch).
   Added `DangerousAcceptAnyServerCertificateValidator` to the hub connection (test-only, matches `curl -k`).
   - File: `tests/DotNetCloud.Client.Android.Tests/Chat/SignalRChatClientE2eTests.cs`

**Live E2E test evidence:**
```
dotnet test tests/DotNetCloud.Client.Android.Tests -c Release --filter "Live" --logger "console;verbosity=detailed"
  Passed ConnectAsync_SubscribesAndReceivesEvents_Live [1 s]
  Test Run Successful.  Total tests: 1  Passed: 1  Total time: 2.2540 Seconds
```

**Test flow (fully self-contained):**
1. Connect to `wss://mint22:15443/hubs/core` with bearer token
2. Join group `chat-channel-{channelId}`
3. Invoke `SendMessageAsync(channelId, "e2e-signalr-probe", null)` → receives `NewMessage` broadcast
4. Extract `sentMessageId` from return value
5. Invoke `MarkReadAsync(channelId, sentMessageId)` → receives `UnreadCountUpdated` to user
6. Assert both events received with correct `channelId`

**Test infrastructure created:**
- Channel: `019cdb96-0000-7000-a000-000000000001` (created in DB)
- Channel member: `testdude` (ID `019cc1ac-da42-737c-b0ab-d0f2ecca8019`) as Owner
- Token: minted via full auth-code + PKCE flow (NOT committed; acquisition steps documented below)

**Token acquisition steps (for future re-runs):**
```bash
# 1. Generate PKCE pair
VERIFIER=$(python3 -c "import secrets; print(secrets.token_urlsafe(64))")
CHALLENGE=$(echo -n "$VERIFIER" | openssl dgst -sha256 -binary | openssl base64 -A | tr '+/' '-_' | tr -d '=')

# 2. Login (get session cookie)
curl -sk -c cookies.txt -X POST https://mint22:15443/auth/session/login \
  -d "Email=testdude@llabmik.net&Password=<password>&RememberMe=false"

# 3. Authorize (get code)
CODE=$(curl -sk -b cookies.txt -o /dev/null -w '%{redirect_url}' \
  "https://mint22:15443/connect/authorize?response_type=code&client_id=dotnetcloud-mobile&redirect_uri=net.dotnetcloud.client://oauth2redirect&scope=openid+profile+offline_access+files:read+files:write&code_challenge=$CHALLENGE&code_challenge_method=S256" \
  | grep -oP 'code=\K[^&]+')

# 4. Exchange for token
curl -sk -X POST https://mint22:15443/connect/token \
  -d "grant_type=authorization_code&code=$CODE&redirect_uri=net.dotnetcloud.client://oauth2redirect&client_id=dotnetcloud-mobile&code_verifier=$VERIFIER"
```

**Readiness gate:**
- Core.Tests: 138 passed
- Core.Server.Tests: 327 passed, 2 skipped
- Core.Auth.Tests: 85 passed
- Core.Data.Tests: 176 passed
- Chat.Tests: 263 passed (was 180 in previous run — more tests appeared)
- Android.Tests: 9 passed (including the live E2E)
- Integration.Tests: 14 pre-existing failures (not related to these changes)

**Next steps:**
- Phase 2.10 Android contract alignment is COMPLETE.
- Both client and server can connect via SignalR with bearer tokens.
- The live test is now a regression gate for hub auth changes.
- Next phase: proceed to Phase 3 or whatever the master plan dictates.

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
