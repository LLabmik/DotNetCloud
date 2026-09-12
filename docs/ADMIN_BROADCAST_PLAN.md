# Admin Broadcast — Plan & Implementation Record

**Status:** ✓ Implemented, deployed and verified (server checks + browser E2E confirmed by the user)
**Date:** 2026-09-12
**Branch:** `feature/admin-broadcast`

## Goal

Give an administrator a way to push a short message to **every logged-in web (Blazor) user**, surfaced as a
**dismissible modal dialog** — primarily to warn about upcoming disruptive events such as a server reboot.

Android has its own queueing system for short server outages and is deliberately **out of scope**: native clients
never join the broadcast group, so they receive nothing.

## Decisions

| Decision          | Choice                                                                             |
| ----------------- | ---------------------------------------------------------------------------------- |
| Lifetime          | Persisted in the database; visible until it **expires** or an admin **deletes** it |
| Dismissal         | Remembered **per user** in a dedicated table; never reappears                      |
| Admin actions     | **Delete only** (hard delete) — no retract/soft-delete                             |
| Cleanup on delete | Dismissal rows are deleted with the broadcast (FK cascade + explicit cleanup)      |
| Scheduling        | Optional send time; a 30-second background poller delivers it                      |
| Fields            | Severity (Info / Warning / Critical) + title + message + optional expiry           |
| Delivery scope    | Blazor only, via a dedicated SignalR group                                         |
| Dismissability    | Always dismissible (a warning, not an enforcement)                                 |
| Sender visibility | The admin who sends it sees the modal too                                          |
| Notification bell | Not used — no push/email/bell entries                                              |

## Architecture

```mermaid
flowchart LR
    A[Admin UI /admin/broadcast] -->|POST api/v1/core/admin/broadcasts| B[AdminBroadcastsController]
    B --> C[AdminBroadcastService]
    D[AdminBroadcastSchedulerHostedService<br/>30s poll] --> C
    C -->|persist, incl. scheduled| E[(AdminBroadcasts<br/>AdminBroadcastDismissals)]
    C -->|IRealtimeBroadcaster.BroadcastAsync<br/>group admin-broadcast| F[CoreHub]
    F -->|only relays that joined the group| G[Blazor circuit relay<br/>RealtimeNotificationClient]
    G --> H[AdminBroadcastModal<br/>dismissible modal]
    H -->|GET api/v1/core/broadcasts/active| B2[BroadcastsController]
    H -->|POST {id}/dismiss| B2
```

**Why a dedicated group rather than `Clients.All`:** only the server-side Blazor circuit relay invokes
`JoinAdminBroadcastGroupAsync`, so Android/desktop hub clients are provably excluded. The Blazor relay re-joins
automatically on reconnect (`WithAutomaticReconnect` → `Reconnected`).

**Why the client also fetches `/active`:** realtime is best-effort. The one-shot fetch on component start means a
user who logs in after the message was sent — or reconnects after the reboot it warns about — still sees it.

## Implementation record

### Data (Phase A)

- ✓ `AdminBroadcast` entity (`Entities/Admin/AdminBroadcast.cs`) — Id, Title, Message, Severity, CreatedByUserId,
  CreatedAtUtc, ScheduledForUtc?, SentAtUtc?, ExpiresAtUtc?
- ✓ `AdminBroadcastDismissal` entity — BroadcastId (FK, cascade), UserId, DismissedAtUtc
- ✓ `AdminBroadcastSeverity` / `AdminBroadcastStatus` enums + DTOs in `DotNetCloud.Core/DTOs/AdminBroadcastDtos.cs`
  (enums live with the DTOs, following the `NotificationType` precedent)
- ✓ EF configurations in `Configuration/Admin/` (max lengths, snake_case index names, unique `(BroadcastId, UserId)`)
- ✓ `CoreDbContext`: 2 DbSets + `ConfigureAdminBroadcastModels(...)`
- ✓ Migrations generated for **both** providers: `20260912111356_AddAdminBroadcast` (PostgreSQL) and
  `20260912111409_AddAdminBroadcast_SqlServer`

### Service (Phase A)

- ✓ `IAdminBroadcastService` (`DotNetCloud.Core/Services/`) — CreateAsync, ListAsync, SendNowAsync, DeleteAsync,
  GetActiveForUserAsync, DismissAsync, PublishPendingAsync
- ✓ `AdminBroadcastService` (`Core.Server/Services/`) — dispatch to group `admin-broadcast`, event `admin.broadcast`;
  delete emits `admin.broadcast.removed` so an open modal closes; dismissal is idempotent and guarded by the unique index
- ✓ Input validation: blank title/message and expiry-before-send both rejected with `ArgumentException`
- ✓ Delete removes dismissal rows explicitly (`ExecuteDeleteAsync` on relational, tracked remove otherwise) so the
  behaviour is identical on PostgreSQL, SQL Server and the in-memory test provider

### Realtime (Phase B)

- ✓ `CoreHub.JoinAdminBroadcastGroupAsync()` — parameterless, fixed group (never accepts an arbitrary group name)
- ✓ `RealtimeNotificationClient` — joins the group after start and on reconnect; handles `admin.broadcast` and
  `admin.broadcast.removed`
- ✓ `IRealtimeNotificationClient` — new `AdminBroadcastReceived` / `AdminBroadcastRemoved` events
- ✓ No change needed to `IRealtimeBroadcaster` (existing `BroadcastAsync(group, event, payload)` is sufficient)

### API (Phase C)

- ✓ `AdminBroadcastsController` — `api/v1/core/admin/broadcasts`, `RequireAdmin`: GET history, POST create,
  POST `{id}/send-now`, DELETE `{id}`; every mutation audited via `IAuditLogger`
- ✓ `BroadcastsController` — `api/v1/core/broadcasts`, `RequireAuthenticated`: GET active, POST `{id}/dismiss`

### Scheduler (Phase D)

- ✓ `AdminBroadcastSchedulerHostedService` — 30 s `PeriodicTimer`, tracked by `IBackgroundServiceTracker`,
  failures logged and retried on the next tick

### UI (Phase E)

- ✓ `AdminBroadcastModal.razor` + code-behind (`UI.Web/Components/Shared/`) — rendered `InteractiveServer` inside
  `MainLayout`'s `AuthorizeView` next to `DemoBanner`, so anonymous users never see it
- ✓ Built on the shared `DncModal` (`StartVisible`, overlay-click disabled); severity drives icon + alert class
- ✓ `Pages/Admin/Broadcast.razor` — `/admin/broadcast`, `RequireAdmin`: compose card (title/message/severity/schedule/expiry),
  `Send now` / `Schedule`, history table (created, severity, status, sent, expires, dismissed count) with
  `Send now` for scheduled rows and `Delete` behind `DncConfirmDialog`
- ✓ `DotNetCloudApiClient` — 6 new methods (active/dismiss + 4 admin)
- ✓ `NavMenu.razor` — admin "Broadcast" entry using `<MaterialIcon Icon="campaign" />`
- ✓ `campaign` SVG path added to `MaterialSvgIcons.cs` (the icon did not exist; a missing icon silently renders as text)

### Tests

- ✓ `AdminBroadcastServiceTests` — 18 tests: immediate vs scheduled delivery, due/not-due/already-sent polling,
  active lookup (sent / expired / pending / dismissed-by-another-user), idempotent dismissal, delete cascading
  dismissal rows + removal event, send-now, history ordering and dismissal counts
- ✓ `AdminBroadcastsControllerTests` — create/validation/unauthorized/send-now/delete + audit assertions
- ✓ `BroadcastsControllerTests` — active (+ null), dismiss, unauthorized
- ✓ `CoreHubTests.JoinAdminBroadcastGroupAsync_AddsConnectionToFixedBroadcastGroup` — pins the wire contract
- ✓ `MaterialSvgIconsTests` — new row-set covering `campaign`, `info`, `warning`, `error`, `send`, `schedule`
- ✓ Results: Core.Server 760 passed / 0 failed; UI.Shared 91 passed; Core.Data 177 passed

### Verification

- ✓ `dotnet build DotNetCloud.CI.slnf -c Release` — 0 warnings, 0 errors
- ✓ `sudo ./scripts/deploy.sh --force --verify` on mint22 — 15/15 targets, 1 pending migration applied,
  assembly hashes verified, version 0.6.05
- ✓ `/health/ready` → HTTP 200
- ✓ Unauthenticated `GET /api/v1/core/admin/broadcasts` and `GET /api/v1/core/broadcasts/active` → HTTP 401
  (routes live and auth policies applied)
- ✓ `AdminBroadcasts` + `AdminBroadcastDismissals` tables present in the database
- ✓ `Admin Broadcast Scheduler started.` in the service log
- ✓ `/admin/broadcast` page present in the deployed `DotNetCloud.UI.Web.Client.dll`; hub method present in
  `DotNetCloud.Core.Server.dll`
- ✓ Browser E2E (two sessions): modal appears on Send; Dismiss survives reload/re-login; Delete closes an open
  modal; scheduled broadcast fires within 30 s; expired broadcasts stop appearing; one user's dismissal does not
  affect another user (confirmed by the user on the deployed build)

## Behaviour notes

- The message is delivered to **all** connected Blazor clients, including the admin who sent it (immediate confirmation).
- Expiry is evaluated at read time — no writes are needed to expire a broadcast.
- A broadcast whose scheduled time is already in the past is delivered immediately on create.
- Deleting a broadcast is permanent: the message and all dismissal rows are removed, and open modals close.

## Out of scope (possible follow-ups)

- Push/email delivery and notification-bell entries for users who never open the app
- Per-organization or per-user targeting
- Acknowledgement tracking / reporting ("who has seen it") — the dismissal table is the natural place to build this
- Android/desktop UI presentation
- Rich text, links, or attachments in the message body
