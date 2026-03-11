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

### Server Follow-up - Live E2E Preconditions Verified (Phase 2.10)

**Date:** 2026-03-11  
**Owner:** Server (`mint22`)  
**Status:** Server endpoints verified; client can execute live run now

**Test is now executable:**

✓ Updated `SignalRChatClientE2eTests` with full setup instructions
✓ Bearer token handled via environment variable `DOTNETCLOUD_E2E_BEARER_TOKEN`
✓ Event logging shows real-time connection + event receipt
✓ Trigger protocol embedded in test documentation
✓ Unit tests (deserialization, mapping) runnable immediately

**Server checks completed (mint22):**

✓ `GET https://mint22:15443/health` -> `200` (Healthy)  
✓ `GET https://mint22:15443/.well-known/openid-configuration` -> `200`  
✓ OIDC metadata confirms:
- `authorization_endpoint`: `https://mint22:15443/connect/authorize`
- `token_endpoint`: `https://mint22:15443/connect/token`

**Token constraint confirmed:**
- `client_credentials` probe returns `invalid_request` requiring `client_secret`.
- For this E2E, use a real **user** bearer token from auth-code + PKCE flow (desktop/mobile sign-in). 
- Do not use client-credentials token for hub test.

**Execute on Windows machine:**

1. Obtain a valid **user** bearer token from your server's OAuth OIDC flow (test user must be chat channel member)
2. Set environment variable: `$env:DOTNETCLOUD_E2E_BEARER_TOKEN = "your-bearer-token"`
3. Run unit tests first (server not needed):
   ```
   dotnet test tests/DotNetCloud.Client.Android.Tests/DotNetCloud.Client.Android.Tests.csproj --filter "not Live"
   ```
4. For live E2E test:
   - Remove `[Fact(Skip = "...")]` from `ConnectAsync_SubscribesAndReceivesEvents_Live()` method
   - Replace with `[Fact]`
   - From another authenticated client, trigger protocol (POST message to fire events)
   - Run: `dotnet test tests/DotNetCloud.Client.Android.Tests/DotNetCloud.Client.Android.Tests.csproj --filter "Live"`

**Expected output on success:**
- `✓ Hub connection established`
- `✓ UnreadCountUpdated received: ChannelId=..., Count=...`
- `✓ NewMessage received: ChannelId=..., Preview=...`
- `✓ TEST PASSED: Received X unread updates and Y messages`

**Trigger command template (sender side):**
```bash
curl -k -X POST "https://mint22:15443/api/v1/chat/channels/{channelId}/messages?userId={senderUserId}" \
  -H "Authorization: Bearer {senderAccessToken}" \
  -H "Content-Type: application/json" \
  -d '{"content":"e2e-signalr-probe"}'
```

**Request back:**
- commit hash
- sanitized logs from `ConnectAsync_SubscribesAndReceivesEvents_Live`
- exact auth error text if token acquisition still blocked

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
