# Client/Server Mediation Handoff

Last updated: 2026-03-11 (compacted active-only handoff)

Purpose: shared handoff between client-side and server-side agents, mediated by user.

Archived context:
- Historical completed updates are in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Additional history remains available in git.

## Process Rules

- Put all technical findings and debugging conclusions in this document, committed to `main`.
- Mediator role is relay-only: short commit/update notifications and request forwarding.
- Keep this file lean: only active sprint kickoff plus latest 1-2 updates.
- Move completed historical blocks to `CLIENT_SERVER_MEDIATION_ARCHIVE.md` during each new handoff cycle.
- Default relay text: `New commit on main with handoff updates. Pull and resume from the current checklist.`
- Assistant pushes commits; mediator relays short notifications.

## Moderator Short-Ping Templates

- `New handoff update is in docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md. Pull latest main and continue.`
- `Please read the latest active section in the handoff doc and post results back there.`
- `New commit on main with handoff updates. Pull and resume from the current checklist.`

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
