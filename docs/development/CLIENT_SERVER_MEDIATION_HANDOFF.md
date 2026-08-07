# Client/Server Mediation Handoff

Last updated: 2026-08-07 (Android: DM display names fixed client-side, two server issues found)

Purpose: shared handoff between client-side and server-side agents, mediated by user.

Archived context:

- Historical completed updates are in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Additional history remains available in git.

## Process Rules

**Agent autonomy (CRITICAL):**

- Both client and server agents work autonomously — they do NOT ask the moderator for context or permission.
- Agents pull the branch specified in the relay message, read the **Active Handoff** section, and execute the work described there independently.
- All actionable items, blockers, and technical details go directly in this document.
- **Current active branch:** `fix/chat-dm-notification`

## Active Handoff — Cloud (`cloud.kimball.home`): Fix two server-side issues found during Android DM verification

**Summary:** Android DM verification revealed two server-side issues on `cloud.dotnetcloud.net` that block DM functionality. Android client-side DM name resolution is now fixed (commit `d618e2b2`) using the `id_token` instead of the encrypted access token.

### Issue 1: `IsDmAccepted` column missing — 500 on channel members endpoint

**Symptom:** `GET /api/v1/chat/channels/{id}/members` returns **500 Internal Server Error**.

**Root cause:** SQL error `Invalid column name 'IsDmAccepted'` — a DB migration adding this column has not been applied to the production database.

**Fix:** Apply the pending EF Core migration that adds the `IsDmAccepted` column.

**Impact:** Android cannot load DM channel members, which blocks the members list UI and DM channel detail screens.

### Issue 2: Access token switched to JWE encryption — client-side token parsing broken

**Symptom:** The access token from `/connect/token` is now a 5-part JWE token (header: `{"alg":"RSA-OAEP","enc":"A256CBC-HS512"}`) instead of a 3-part signed JWT. Client-side base64-decode of the payload yields ciphertext, not JSON.

**Impact:** Any code that decodes the access token client-side (extracting `sub`, `email`, `name` claims) fails silently. The Android `LoginViewModel` and `AccessTokenUserIdExtractor` were both affected.

**Android fix applied (commit `d618e2b2`):** The Android client now captures the `id_token` from the OIDC `/connect/token` response (it's a standard signed JWT, not encrypted) and uses it for user ID and claim extraction. Changes:
- `OAuth2Result` / `TokenResponse`: capture `id_token`
- `ISecureTokenStore` / `AndroidKeyStoreTokenStore`: store/retrieve `id_token`
- `LoginViewModel`: extract claims from `id_token` instead of access token
- `ResolveDmChannelNamesAsync`: use `id_token`'s `sub` claim to identify current user, then resolve OTHER participant's display name

**⚠️ Users must log out and back in** to capture the `id_token` from a fresh login.

**Server-side question:** Was the switch to JWE intentional? If so, the `id_token` workaround is sufficient for Android. Other clients (desktop, web) may also be affected.

### Verification status

| Step | Status |
|------|--------|
| DM channels show display names | ✅ Fixed client-side (requires re-login for id_token capture) |
| Push notification with correct display name | ⏳ Blocked by Issue 1 (500 on members endpoint) |
| 3 inline notification actions | ⏳ Blocked by Issue 1 |
| SignalR connection | ✅ Connected successfully after transient 401 |

## Moderator Communication (Minimal)

**Moderator relays ONLY ONE OF THESE messages — nothing more:**

- `New handoff update for <target-machine>. Pull main and resume from 'Active Handoff' section.`
- `<Commit hash> — New handoff update for <target-machine>. Pull and check docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md Active Handoff.`

**No moderator task:** Moderator provides zero context, zero explanation. The handoff document has everything the receiving agent needs.

## Environment

| Role           | Machine              | Detail                                                                             |
| -------------- | -------------------- | ---------------------------------------------------------------------------------- |
| Server         | `cloud.kimball.home` | `https://cloud.dotnetcloud.net/` (production)                                      |
| Server         | `mint22`             | `https://mint22:5443/` (dev)                                                       |
| Client         | `Windows11-TestDNC`  | Sync dir: `C:\Users\benk\synctray`                                       |
| Client         | `mint-dnc-client`    | Linux Mint 22 validation host for desktop sync client implementation + E2E testing |
| Client         | `mint-OptiPlex-7010` | production client connected to `cloud.dotnetcloud.net`              |
| Android Client | `monolith`           | Android MAUI app development + emulator testing (Windows 11)                       |

## Key Carry-Forward Contracts

- Auth: Files module host uses a policy scheme (`DotNetCloud.Module`) that auto-selects between `OpenIddict.Validation.AspNetCore` (JWT Bearer) and `Identity.Application` (cookie) based on the `Authorization` header. Controllers use plain `[Authorize]`. All module hosts must follow this pattern.
- API envelope: middleware wraps responses; clients should unwrap via envelope helpers.
- Sync flow: changes -> tree -> reconcile -> chunk manifest -> chunk download -> file assembly.
- Desktop OAuth constant: `OAuthConstants.ClientId = "dotnetcloud-desktop"`.
- ✅ **SignalR channel group naming:** `chat-channel-{channelId}` (used by `ChatHub.ChannelGroup()`, `CoreHub.JoinGroupAsync()`, and Android `SignalRChatClient`).
- **Controller discovery:** Core.Server references Files.Host and Chat.Host via `ProjectReference`. ASP.NET Core auto-discovers controllers from referenced assemblies. Do NOT create duplicate controllers in Core.Server for routes already served by module Host assemblies.
- ✅ **Calendar event broadcasting pattern:** Follow `CalendarReminderEventHandler` (`CalendarReminderEventSubscriber` + `CalendarEventBroadcastHandler`) as the reference implementation. It calls `CoreCapabilitiesClient.BroadcastRealtimeEventAsync` for SignalR and `SendNotificationAsync` for FCM push.
- ✅ **DM notification flow:** `DmChannelCreatedEventHandler` subscribes to `ChannelCreatedEvent`. For `DirectMessage` channels only, it sends push via `IPushNotificationService` and raises `IChatMessageNotifier.DmChannelCreated` for in-process Blazor. `GlobalChatNotificationState` handles the Blazor-side toast. Android handles the push-side with 3 inline notification actions.

<!-- carry-forward contracts and old Android changes archived to CLIENT_SERVER_MEDIATION_ARCHIVE.md -->
