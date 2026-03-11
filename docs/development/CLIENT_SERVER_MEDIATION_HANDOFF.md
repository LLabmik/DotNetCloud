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
- Keep this file lean: only active sprint kickoff plus 1-2 latest updates in **Active Handoff**.
- Move completed historical blocks to `CLIENT_SERVER_MEDIATION_ARCHIVE.md` during each new handoff cycle.

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

### Server Follow-up Result - Android Contract Alignment (Phase 2.10)

**Date:** 2026-03-11  
**Owner:** Server (`Linux workspace`)  
**Status:** server follow-up completed; client action required

**Server findings:**

1. OIDC mobile client seeding is now present in server initialization.
- File: `src/Core/DotNetCloud.Core.Server/Initialization/OidcClientSeeder.cs`
- Registered client IDs and redirects:
  - `dotnetcloud-desktop` -> `http://localhost:52701/oauth/callback`
  - `dotnetcloud-mobile` -> `net.dotnetcloud.client://oauth2redirect`

2. SignalR contract is currently core-hub based and does not match current Android SignalR assumptions.
- Server hub path default: `/hubs/core`
  - `src/Core/DotNetCloud.Core.Server/Configuration/SignalRConfiguration.cs`
  - `src/Core/DotNetCloud.Core.Server/appsettings.json`
- Server unread event shape:
  - Event name: `UnreadCountUpdated`
  - Payload shape: object `{ channelId, count }`
  - Source: `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/ChatRealtimeService.cs`
- Server new-message event shape:
  - Event name: `NewMessage`
  - Payload shape: object `{ channelId, message }`
  - Source: `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/ChatRealtimeService.cs`
- Android current expectations (mismatch):
  - Connects to `/hubs/chat`
  - Expects positional args for `UnreadCountUpdated` and `NewMessage`
  - Source: `src/Clients/DotNetCloud.Client.Android/Chat/SignalRChatClient.cs`

3. Caller identity contract for chat REST remains query-based today.
- Chat endpoints still require `[FromQuery] Guid userId`.
- Source: `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Controllers/ChatController.cs`
- Announcement endpoints also use `[FromQuery] Guid userId`.
- Source: `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Controllers/AnnouncementController.cs`

**Required next step (client):**
- Align Android SignalR client to server contract (`/hubs/core`, object payloads).
- Keep chat REST calls sending `userId` query parameter until server publishes bearer-derived caller refactor for chat endpoints.

### Send to Client Agent
Server follow-up is recorded in the handoff doc. Pull latest `main` and align Android SignalR to `/hubs/core` with object payload handlers for `UnreadCountUpdated` and `NewMessage`.

### Request Back
- commit hash
- Android SignalR handler mapping implemented (hub path + payload parser details)
- validation output (build/tests) from Android workspace
- any remaining contract blockers

---

## Completed Handoff

### Server Validation Complete - Android SignalR Contract (Phase 2.10)

**Date:** 2026-03-11 (completed)  
**Owner:** Server (`mint22`)  
**Status:** Server contract validated; client implementation accepted

**Server validation:**

✓ Chat module builds (0 errors, 0 warnings)  
✓ Hub path `/hubs/core` confirmed in `SignalRConfiguration.cs` and `appsettings.json`  
✓ Event payload shapes match client expectations:
  - `UnreadCountUpdated`: `{ channelId, count }`
  - `NewMessage`: `{ channelId, message }`
  - Source: `ChatRealtimeService.cs` broadcast calls

✓ Client payload parsing (DTOs + `[JsonPropertyName]`) will deserialize correctly  
✓ Android handler mapping (object → EventArgs) is sound

**Acceptances:**
- Client hub connection to `/hubs/core` ✓
- UnreadCountUpdated + NewMessage object payloads ✓
- Chat REST userId query parameter maintained ✓

**Integration status:** Ready for cross-machine end-to-end test.

**Commit:** Server validation result  
**Previous:** `c855eef` (client implementation)

---

## Active Handoff

### Send to Client Agent - E2E SignalR Testing (Phase 2.10)

**Date:** 2026-03-11  
**Owner:** Client (`Windows11-TestDNC`)  
**Status:** Ready for implementation

**Test scenario:**

Server running at `https://mint22:15443/`. Chat module live with SignalR hub at `/hubs/core`.

**Client task:**
1. Implement Android SignalR connection (MockSignalRChatClient for testing)
2. Connect to `https://mint22:15443/hubs/core` with bearer token from OAuth
3. Subscribe to `UnreadCountUpdated` handler → log `{ channelId, count }`
4. Subscribe to `NewMessage` handler → log `{ channelId, message }`
5. Create test by sending chat message from desktop client → verify Android receives both events

**Success criteria:**
- Android app connects to `/hubs/core` (no SSL cert errors)
- Receives `UnreadCountUpdated` + `NewMessage` event messages (log output or UI update)
- Commit hash + test output + any blockers

**Server ready:** Hub live, broadcast working, awaiting client connection.

### Request Back
- commit hash (SignalR implementation)
- test output (connection established, events received)
- any network/SSL blockers

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
