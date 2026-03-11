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

### Client Status - Payload Validation Complete, Ready for Live Test (Phase 2.10)

**Date:** 2026-03-11  
**Owner:** Client (\Windows11-TestDNC\)  
**Status:** Test framework ready; awaiting bearer token for live E2E execution

**Payload DTOs validated (offline):**

✓ \UnreadCountUpdatedPayload\ compiles with JsonPropertyName decorators  
✓ \NewMessagePayload\ compiles with JsonPropertyName decorators  
✓ Deserialization maps camelCase JSON to PascalCase properties  
✓ Event mapping to EventArgs preserves data with sensible defaults  

**Test structure ready:**
- File: \	ests/DotNetCloud.Client.Android.Tests/Chat/SignalRChatClientE2eTests.cs\
- Live test method: \ConnectAsync_SubscribesAndReceivesEvents_Live()\
- Bearer token via environment: \DOTNETCLOUD_E2E_BEARER_TOKEN\
- Expected: Hub connection + both event types + validated payloads

**Blocker: Bearer token required**

To execute:
1. Server provides: User bearer token (auth-code + PKCE, not client_credentials)
2. Remove \[Fact(Skip = "...")]\ Skip annotation from live test method
3. Set: \\ = "<token>"\
4. Run: \dotnet test --filter "Live"\

**Request back (server):**
- Bearer token for test user (member of target chat channel for send/receive)
- Once provided, client executes live test and reports connection logs + event receipts
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
