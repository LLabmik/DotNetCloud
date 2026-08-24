# Client/Server Mediation Handoff

Last updated: 2026-08-24 (DB Outage Resilience server verification archived ✅; new Active Handoff → SyncTray test machine §11.4)

Purpose: shared handoff between client-side and server-side agents, mediated by user.

Archived context:

- Historical completed updates are in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Additional history remains available in git.

## Process Rules

**Agent autonomy (CRITICAL):**

- Both client and server agents work autonomously — they do NOT ask the moderator for context or permission.
- Agents pull the branch specified in the relay message, read the **Active Handoff** section, and execute the work described there independently.
- All actionable items, blockers, and technical details go directly in this document.
- **Current active branch:** `fix/database-offline-recovery`

## Active Handoff — SyncTray test machine: DB Outage SyncTray Simulation (plan §11.4)

**Target:** `Windows11-TestDNC` (client; sync dir `C:\Users\benk\synctray`) — alt: `mint-OptiPlex-7010` (production client → `https://cloud.dotnetcloud.net/`)
**Branch:** `fix/database-offline-recovery`
**Canonical plan:** `docs/DB_OUTAGE_RESILIENCE_PLAN.md` §11.4 (SyncTray simulation)
**Prerequisite (DONE — server agent):** Server deploy verified on `cloud.kimball.home` (`https://cloud.dotnetcloud.net/`): resilience code live, `/health/ready` Healthy, `database` entry Healthy, 14/14 modules. §11.2/§11.3 + integration tests passed (see `CLIENT_SERVER_MEDIATION_ARCHIVE.md`). The client tests the **already-deployed** server — no client code changes expected for this simulation.

**Task:** Run the SyncTray outage simulation (§11.4) against the live production server:

1. Run SyncTray with an active account and the server up → tray is **green/idle**.
2. Server outage — ask the moderator to **stop the DotNetCloud service** on `cloud` (`sudo systemctl stop dotnetcloud`). The client agent cannot stop production itself; the moderator executes it on request.
3. Assert:
   - Within one backoff interval (≤ ~30 s) the tray turns **gray** with tooltip "server unreachable, retrying automatically".
   - "Sync now" returns quickly with a toast (no multi-minute hang).
4. Server recovery — ask the moderator to **start the service** (`sudo systemctl start dotnetcloud`; wait until `/health/ready` is Healthy again — 14/14 modules, `database` Healthy).
5. Assert: the tray returns to **idle/syncing automatically** within the backoff interval (no manual restart).

**Report back:** pass/fail + evidence (tray screenshots with timestamps, backoff timings) here.

**Notes for the client agent:**
- Production server is `https://cloud.dotnetcloud.net/`. SyncTray must be connected to an active account before starting the sim.
- During the outage the server returns HTTP 503 `DATABASE_UNAVAILABLE` and `/health/ready` → Unhealthy; SyncTray's offline classification should trigger the gray state. Do NOT close the handoff until the recovery assertion (step 5) passes.
- If the tray does NOT go gray or "Sync now" hangs, report it as a client regression with logs.

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
