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

### Client Complete - E2E SignalR Test (Phase 2.10)

**Date:** 2026-03-11  
**Owner:** Client (`Windows11-TestDNC`)  
**Status:** E2E test implementation complete; ready for server integration testing

**Client implementation:**

✓ Created `SignalRChatClientE2eTests` with comprehensive test structure
  - **Live E2E test:** Connects to `https://mint22:15443/hubs/core` with bearer token
  - **Payload deserialization unit tests:** Validates UnreadCountUpdatedPayload and NewMessagePayload JSON parsing
  - **Event mapping tests:** Confirms payload → EventArgs mapping preserves data correctly
  
✓ Test validates:
  - Hub path `/hubs/core` connection
  - Bearer token authentication
  - UnreadCountUpdated event subscription and handler firing
  - NewMessage event subscription and handler firing
  - Proper JSON deserialization (camelCase → PascalCase mapping via `[JsonPropertyName]`)

✓ File: `tests/DotNetCloud.Client.Android.Tests/Chat/SignalRChatClientE2eTests.cs`
  - Live test: `[Fact(Skip = "E2E test — requires live server")]` (manual execution: update bearer token + remove Skip)
  - Unit tests: Payment deserialization + event arg mapping (runnable immediately)
  - Prerequisite documented: Valid OAuth bearer token from server OIDC flow

**Commit:** `0cddd41`  
**Message:** "Add E2E SignalR test scenario for manual validation against live server"

**Server response (integration instructions):**

- Hub auth is required (`CoreHub` has `[Authorize]`), so the test must use a real user bearer token.
- Use any valid user token from the desktop/mobile OIDC auth-code flow. `client_credentials` tokens are not suitable (subject is client ID, not user GUID).
- Server cannot safely commit/share a raw bearer token in git handoff.

**Trigger protocol (fires both events):**

1. Start Android test client and connect to: `https://mint22:15443/hubs/core`.
2. Ensure Android test user is a member of target channel.
3. From another authenticated user in same channel, send:
  - `POST /api/v1/chat/channels/{channelId}/messages?userId={senderUserId}`
  - Header: `Authorization: Bearer {senderAccessToken}`
  - Body: `{ "content": "e2e-signalr-probe" }`
4. Expected events on Android client:
  - `NewMessage` with `{ channelId, message }`
  - `UnreadCountUpdated` with `{ channelId, count }`

**Server-side verification status:**
- Contract, hub path, and payload shape already verified on server.
- Client test project exists, but server machine cannot execute Android test project due missing `maui-android` workload (`NETSDK1147`).

**Request back (client):**
- commit hash for live test execution updates
- sanitized connection + event logs (timestamps)
- any SSL/auth/deserialize errors with exact text

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
