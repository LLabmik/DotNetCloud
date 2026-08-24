# Client/Server Mediation Handoff

Last updated: 2026-08-24 (DB Outage Resilience — deploy + verification handoff → `cloud.kimball.home`; SyncTray multi-folder client testing archived ✅)

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

## Active Handoff — cloud.kimball.home: DB Outage Resilience — Deploy + Verification

**Target:** `cloud.kimball.home` (server; production `https://cloud.dotnetcloud.net/`; SQL Server DB on `hyperdrive`)
**Branch:** `fix/database-offline-recovery`
**Commit:** `e2eea604` — `feat(resilience): implement database/server outage recovery (DB_OUTAGE_RESILIENCE_PLAN)`
**Canonical plan:** `docs/DB_OUTAGE_RESILIENCE_PLAN.md` (implementation complete; **§11 Verification is the open work**)

**Task:** Pull `fix/database-offline-recovery`, deploy the latest code, then run the outage verifications + integration tests and report results back here. **Do NOT treat the handoff as closed until §11.2, §11.3, and the integration tests pass.**

### 1. Deploy (all targets)

```bash
git fetch origin && git checkout fix/database-offline-recovery && git pull
scripts/deploy.sh --force --verify   # all 15 targets + assembly hash verify, as usual
```

Confirm after deploy:
- `/health` + `/health/ready` → Healthy.
- `/health` now includes a `database` entry (tagged) reporting Healthy when the DB is reachable.
- All modules report running (14/14) — module hosts now register DB-aware health checks.

### 2. Server outage simulation (plan §11.2) — use a NON-production instance

⚠️ The production SQL Server on `hyperdrive` backs `cloud.dotnetcloud.net` — do NOT stop it. Run this against the **mint22 dev instance** (or a dedicated test DB). Steps:

1. With the dev DB running, start the server.
2. Stop the dev database.
3. Assert:
   - `GET /health/live` → `Healthy` (process stays alive).
   - `GET /health/ready` → `Unhealthy` with `entries.database.status == Unhealthy`.
   - An authenticated API call that hits the DB (e.g. `GET /api/v1/files/quota`) returns **HTTP 503** with body `{"success":false,"code":"DATABASE_UNAVAILABLE",...}` in **under ~2 s** (no hang).
4. Restart the dev DB.
5. Within ~10 s (one reconnect poll): `/health/ready` → `Healthy` again; the same API call → `200` — **without restarting the service**.

### 3. Module host simulation (plan §11.3)

With the DB down, module hosts (e.g. `dotnetcloud-module files`) must **keep running** (process stays up — no crash-loop) and report `Degraded`/`Unhealthy` via the supervisor aggregate; after DB recovery they return to healthy with no manual restart. If any host exits on DB-down, that's a regression in this branch — report it.

### 4. Integration tests (both providers — DB on hyperdrive)

```bash
# SQL Server (hyperdrive test DB, same pattern as WS4):
export DOTNETCLOUD_TEST_SQLSERVER_CONNECTION_STRING="Server=<hyperdrive-sql>;Database=DotNetCloud-Test;User Id=<user>;Password=<pass>;TrustServerCertificate=true"
dotnet test tests/DotNetCloud.Integration.Tests/ -p:DatabaseProvider=SqlServer

# PostgreSQL (hyperdrive or configured PG host):
dotnet test tests/DotNetCloud.Integration.Tests/ -p:DatabaseProvider=PostgreSql
```

Report pass/fail + evidence. This branch rewired **every** DbContext registration to the shared `DbResiliencePolicy` — integration tests confirm no provider regression.

### 5. Client-side verifications (separate handoffs — NOT this one)

- §11.4 SyncTray simulation → a client machine (`Windows11-TestDNC` / `mint-OptiPlex-7010`) once the server deploy above is verified.
- §11.5 Android simulation → `monolith` (Android MAUI).

### Report back

After deploy + server verifications, report results (pass/fail + evidence) here and either hand back for fixes or flag ready for the client-side verifications. **Do not close the branch out until §11.2 + §11.3 + integration tests pass.**

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
