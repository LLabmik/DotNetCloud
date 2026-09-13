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
| Fields            | Severity (Info / Warning / Critical) + title + Markdown message + optional expiry  |
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
- ✓ Message body is **Markdown** (2026-09-13): the composer uses the shared `MarkdownEditor` (toolbar + live
  preview, 2,000-char cap) and the user modal renders the message with a preview-only `MarkdownEditor`,
  sanitized through `IMarkdownRenderer` — the same approach as the Notes module's read-only view
- ✓ The viewer's message region is height-capped (`55vh`) and scrolls vertically on overflow, so long
  broadcasts keep the title, severity banner and Dismiss button visible
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
- Attachments or embedded media in the message body (Markdown text — including links — is supported as of 2026-09-13)

## Enhancement — Markdown message body (2026-09-13)

**Branch:** `fix/markdown-in-admin-broadcast`

The message body is now Markdown instead of plain text, following the Notes module's implementation.

- Compose (`/admin/broadcast`): the plain `InputTextArea` was replaced by the shared `MarkdownEditor`
  (`Content`/`ContentChanged`, `Renderer="MarkdownRenderer"`, `MaxLength=2000`) — toolbar buttons plus a live
  split preview. The stored value is still the raw Markdown string; no schema change.
- View (`AdminBroadcastModal`): the message is rendered through `IMarkdownRenderer` using a preview-only
  `MarkdownEditor`, matching the Notes read-only view. Content is sanitized by `MarkdownRenderer`
  (Markdig + HtmlSanitizer), so links are allowed but scripts/iframes are stripped.
- Overflow: `.broadcast-message` is capped at `55vh` with `overflow-y: auto` so a long broadcast scrolls instead
  of pushing the dialog past the viewport; the severity banner, title and Dismiss button stay visible.
  Scoped `AdminBroadcastModal.razor.css` removes the editor's border/background/min-height so the rendered
  Markdown reads as dialog content.
- Severity is now explicit: the banner shows a text label (`Information` / `Warning` / `Critical`), and Info
  maps to a new `.alert-info` style (the shared `app.css` only defines danger/warning/success).
- DI: `IMarkdownRenderer` is registered in the WASM client (`DotNetCloud.UI.Web.Client/Program.cs`) because
  `/admin/broadcast` is `InteractiveAuto`; the Blazor Server path already resolved it via `AddNotesUiServices`.

### Verification

- ✓ `dotnet build src/UI/DotNetCloud.UI.Web/DotNetCloud.UI.Web.csproj -c Release` — 0 warnings / 0 errors
- ✓ Scoped CSS bundle contains the new `.broadcast-*` selectors (`AdminBroadcastModal.razor.rz.scp.css`)
- ✓ Core.Server broadcast tests 76/76 passed
- ✓ Deployed `sudo ./scripts/deploy.sh --force --verify` on mint22 — 15/15 targets succeeded (168 s), all assembly
  hashes verified, migrations applied, version 0.6.05, deploy commit `f10ac4bafbfc`
- ✓ `/health/ready` → **Healthy** (14/14 modules Running; the first probe right after the restart is transiently
  `503` while module hosts start)
- ✓ Shipped markup confirmed: `broadcast-viewer` / `broadcast-severity` / `broadcast-message` in the deployed
  `DotNetCloud.UI.Web.dll`; "Markdown is supported" in the deployed `DotNetCloud.UI.Web.Client.dll`; the scoped
  CSS bundle (`DotNetCloud.UI.Web.rg0vnqw6uo.bundle.scp.css`) is present in `wwwroot/_content/` and imported by
  `DotNetCloud.Core.Server.styles.css`
- ✓ Browser E2E (mint22 dev, 2026-09-13): composed a Markdown broadcast (H2, bold, bulleted list, link, ordered
  list) — the composer preview and the user modal both rendered it formatted, under the severity banner — and the
  modal message region scrolled vertically on overflow (`max-height: 466px`, `scrollHeight 641 > clientHeight 466`),
  with the editor chrome removed (0px border, transparent background)

## Fix — Sent timestamp persistence & dismissal reliability (2026-09-13)

**Branch:** `fix/admin-broadcast`

Four reported defects, of which two shared a single root cause.

**Root cause (defects 2 and 3).** `CoreDbContext` sets `QueryTrackingBehavior.NoTracking` globally. Both
`PublishPendingAsync` and `SendNowAsync` did "load entity → mutate → `SaveChangesAsync`" **without**
`.AsTracking()`, so the write was a silent no-op: `SentAtUtc` was never persisted. The row therefore never left the
"pending" set and `AdminBroadcastSchedulerHostedService` re-delivered it on **every 30-second tick, forever**.
That is both why the admin `Sent` column stayed empty and why a dismissed message kept reappearing for users
(`OnAdminBroadcastReceived` only suppresses re-delivery within one circuit, so a reload or a second tab saw it
again).

Evidence captured before the fix:

- Server log: the same broadcast id delivered at `16:25:34 → 16:26:04 → 16:26:34 → 16:27:04 → 16:27:34 …`
- Database: two users had dismissal rows (21:22:11 / 21:22:13) while `SentAtUtc` was still `NULL`

**Fixes.**

- `.AsTracking()` on the `AdminBroadcasts` load in `SendNowAsync` and `PublishPendingAsync` (11th occurrence of
  this repo-wide NoTracking pattern — see `/memories/repo/notracking-persistence-fix.md`).
- History Status badge renders `Scheduled <local time>` when the row is still scheduled, instead of a bare
  `Scheduled`.
- The history list auto-refreshes every 30 seconds (the scheduler's cadence) so Status / Sent / Dismissed fill in
  without pressing Refresh; the loop is skipped while an action is in flight.
- The action cell now uses the codebase-standard `class="actions"` (`display: flex`) so `Send now` and `Delete` are
  equal height and line up.

### Verification

- ✓ `AdminBroadcastServiceTests` 32/32 (18 pre-existing + 2 new regression + others); Core.Server 780 passed / 0 failed
- ✓ The two new regression tests use a context configured with `QueryTrackingBehavior.NoTracking` and read the row
  back through a **fresh** context: both **fail** when `.AsTracking()` is reverted and pass with it
- ✓ Deployed `sudo ./scripts/deploy.sh --force --verify` on production (cloud) — 15/15 targets succeeded (452 s),
  0 pending migrations, all assembly hashes verified
- ✓ `/health/ready` → **Healthy**, 14/14 modules Running, `database` Healthy; `_framework/blazor.web.js` 200
- ✓ Shipped-artifact check: `StatusLabel` + `AutoRefreshAsync` present in the deployed
  `wwwroot/_framework/DotNetCloud.UI.Web.Client.*.wasm`; `AsTracking` referenced in the deployed
  `DotNetCloud.Core.Server.dll`
- ✓ Re-delivery loop confirmed stopped (last delivery `16:42:04`, before the data repair) and absent after the
  service restart
- ✓ Live-verified by the user on the deployed build

### Data repair

One row (`Testin scheduled broadcast`) had been delivered — its dismissal rows proved it — but still had
`SentAtUtc = NULL`. It was backfilled to its scheduled send time (21:22:00 UTC), which is the correct historical
value and immediately stopped the in-flight 30-second re-delivery loop without waiting for the code deploy.
