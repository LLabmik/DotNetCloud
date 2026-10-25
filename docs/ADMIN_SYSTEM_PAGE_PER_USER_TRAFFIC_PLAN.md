# Admin System Page — Per-User Live Traffic

**Status:** implemented
**Target:** .NET 10 (`Directory.Build.props`), ASP.NET Core
**Depends on:** `docs/ADMIN_SYSTEM_PAGE_PLAN.md` (implemented 2026-10-24, PR #192)
**Branch:** _to be created_

---

## 1. Goal

Add a **per-user** traffic breakdown to the existing `/admin/system` page that answers one question:

> What is happening on this machine **right now**, and who is moving the bytes?

It is deliberately **not** a history, a quota ledger, or a billing report. Entries appear when a user
transfers bytes, grey out when they stop, and are forgotten when they go quiet. Nothing is persisted.

### Relationship to the shipped global counters

| Metric | Scope | Lifetime |
| ------ | ----- | -------- |
| In / Out / Total (existing) | whole core process | since process start |
| **Per user (new)** | one user | **since their entry became active** |

The existing global counters are unchanged. The per-user numbers are **not** "today's usage" — a user who
goes idle and comes back starts again at zero. The UI must label this (*"transferred (this session)"*) so it
can never be mistaken for a cumulative total.

**There is no "unattributed" bucket.** `TrafficAccountingMiddleware` counts only requests where
`User.Identity.IsAuthenticated` is true, skips `application/grpc`, and background services never traverse
Kestrel — so every counted byte already belongs to an authenticated user. Conversely, because entries are
forgotten after the idle grace, the sum of the per-user rows legitimately **drifts below** the global total.
That is the design working, so the page must **not** present a reconciliation figure implying an error.

---

## 2. Locked design decisions

1. **Keyed by user id**, not by login/device session. This matches the existing `PresenceService` model
   (`_lastSeen` keyed by user); a user on two devices is one row.
2. **Liveness comes from counted I/O**, not from a SignalR connection. Presence only sees hub clients; a
   desktop/mobile sync client with a bearer token never appears there, yet it is exactly the client that
   saturates the link. See §7a — this divergence **must be explained on the page**.
3. **Same idle lifecycle pattern as chat presence** — activity timestamps, idle threshold, sweep, auto-drop,
   automatic re-materialisation on the next byte.
4. **Activity = every 1 KB of attributed bytes**, not every counted chunk. SignalR and Blazor circuits send
   keep-alive pings every ~15 s and `CountingWebSocketFeature` counts every non-Close frame, so refreshing on
   each chunk would keep any open tab permanently "active" and destroy the point of the feature. Bytes are
   still fully counted; only the activity timestamp is quantised.
5. **Nothing is persisted.** No database writes, no new table, no new NuGet dependency.
6. **Bytes are still counted exactly once**, at the client-facing layer. Per-user attribution only changes
   *which bucket* a byte lands in, never *how many times* it is counted.
7. **No unattributed bucket and no reconciliation figure** — see §1. The numbers cannot be made to add up.
8. **One batched DB read per poll** for display names. This relaxes the original page's "no database access on
   the polling path" rule, deliberately and minimally: a single `GetDisplayNamesAsync` for the handful of
   users in the snapshot, 5 s apart, per watching admin. Caching the name map for the entry's lifetime is the
   fallback if it ever shows up in profiling.
9. **Presence is display-only.** It is rendered as a column on each traffic row but never drives row existence,
   counting or expiry. A row that greys out while bytes are flowing (because a hub reconnected) would be a
   worse defect than the timing differences it would "fix".
10. **The System page's own polling endpoint is excluded** (`/api/v1/core/admin/system`), so a watching admin
    does not accumulate self-inflicted traffic on their own row.

---

## 3. Lifecycle

Activity = the user's attributed bytes crossed another **1 KB** boundary (§2.4). A brand-new entry is stamped
active immediately, so a slow trickle still shows up as active traffic.

| Phase | Condition | Display |
| ----- | --------- | ------- |
| **Active** | idle < threshold | normal row, counters climbing |
| **Idle** | threshold ≤ idle < threshold + 5 min | row greyed, counters frozen |
| **Forgotten** | idle ≥ threshold + 5 min | row removed, counters discarded |
| **Return** | any counted byte for that user | new row, counters restart at 0 |

- **Threshold** — reuse the existing admin setting `PresenceIdleTimeoutMinutes` (default **3**, clamped
  1–60). Rationale: both features answer "is this person still here?", and two near-identical knobs with
  different values would be confusing.
  - ⚠️ Known coupling: tuning the chat presence away-timer also changes when traffic rows grey out. If that
    proves annoying, add `SystemSettingKeys.TrafficIdleTimeoutMinutes` later — a one-line resolver change.
- **Grace** — fixed 5 minutes (a constant, not a setting).
- **Sweep** — reuse the 1 s `SystemMetricsSampler` tick; pruning a small dictionary once a second is noise.
  No new timers.

---

## 4. Files

| Action | File |
| ------ | ---- |
| edit | `src/Core/DotNetCloud.Core.Server/Diagnostics/TrafficAccounting.cs` |
| new | `src/Core/DotNetCloud.Core.Server/Diagnostics/UserTrafficEntry.cs` |
| edit | `src/Core/DotNetCloud.Core.Server/Middleware/TrafficAccountingMiddleware.cs` |
| edit | `src/Core/DotNetCloud.Core.Server/Diagnostics/SystemMetricsSampler.cs` |
| edit | `src/Core/DotNetCloud.Core.Server/Controllers/AdminSystemController.cs` |
| edit | `src/UI/DotNetCloud.UI.Web.Client/Services/AdminSystemDtos.cs` |
| edit | `src/UI/DotNetCloud.UI.Web.Client/Pages/Admin/SystemPage.razor` (+ `.razor.css`) |
| edit | `tests/…/Diagnostics/TrafficAccountingTests.cs`, `AdminSystemControllerTests.cs` |

---

## 5. Accounting shape

`TrafficAccounting` gains a bounded map beside the existing two `long`s:

```
ConcurrentDictionary<Guid, UserTrafficEntry> _perUser

UserTrafficEntry { long BytesIn; long BytesOut; long LastActivityTicks; long UnmarkedBytes; }
```

- `AddIn(Guid userId, long count)` / `AddOut(Guid userId, long count)` — the middleware only calls these for
  authenticated requests, so `userId` is always a real user.
- `UnmarkedBytes` accumulates attributed bytes; crossing `ActivityQuantumBytes` (1024) stamps
  `LastActivityTicks` and resets the accumulator. Keep-alive pings never reach the quantum.
- **Hard cap** (512 entries) with oldest-`LastActivity` eviction as a safety valve; unreachable in practice
  with the 8-minute idle horizon.
- **Prune** (`PruneIdle()`, called from the sampler tick): drop entries idle past threshold + 5 min grace.
  The next byte re-creates the entry with zeroed counters.
- **Snapshot** (`GetPerUserSnapshot()`): one pass producing `(userId, in, out, lastActivity, isIdle)` —
  `isIdle` uses the same threshold the pruner uses, so grey-out and forgetting always agree.

### Hot-path cost

Today: two `Interlocked.Add` calls per counted chunk. After: one `TryGetValue` (allocate-free) plus the same
two adds on the entry. Roughly one hash lookup per ~64 KB copy — negligible, and no change to the streaming
or `SendFileAsync` paths.

Deliberately **not** caching the entry reference across the request: a long-open but silent WebSocket could
have its entry pruned, and further frames would increment an orphaned object. A per-chunk lookup keeps the
map authoritative.

---

## 6. Middleware

- Resolve the user id **once per request** into `HttpContext.Items` (cheap: `HttpContext.User` is already
  materialised because the middleware runs after `UseAuthentication`); parse the claim only on cache miss.
- Skip counting entirely for `/api/v1/core/admin/system` — the page's own polling must not inflate the
  watching admin's row.
- The idle threshold is pushed into `TrafficAccounting` by the existing `PresenceActivityMonitor` sweep, which
  already reads `PresenceIdleTimeoutMinutes` every 30 s — no new settings read, no new timer.
- Cookie sessions and bearer-token clients both produce a `ClaimsPrincipal`, so one code path covers the web
  UI and the sync clients.
- Anonymous / pre-auth → `null` → unattributed bucket.
- WebSocket frames: resolved from the accepted connection's `HttpContext` and counted against that user, which
  matches "this session received X bytes" semantics (bytes fanned out to five users are counted five times —
  once per recipient — and that is correct here).

---

## 7. API and UI

`GET /api/v1/core/admin/system` gains one field:

```jsonc
"usersTraffic": {
  "total": 4,
  "entries": [
    { "userId": "…", "displayName": "Ben", "bytesIn": 0, "bytesOut": 0,
      "bytesTotal": 0, "lastActivityUtc": "…", "isIdle": false,
      "presenceState": "Online" }   // Online | Away | DoNotDisturb | NotConnected
  ]
}
```

- Entries ordered by `bytesTotal` descending — the biggest talker first, capped at **25** rows with a
  `showing top 25 of N` caption when the list is truncated (`total` carries the untruncated count).
- Display names resolved with one bulk `IUserDirectory.GetDisplayNamesAsync(ids)` call per poll (the
  controller gains that dependency; it is already registered scoped). Fall back to the id when a lookup
  misses. This is the only DB touch on the polling path and it is a single indexed read.
- `presenceState` is `NotConnected` for users who have traffic but no hub connection — this is the common and
  expected case for sync clients, not an error.
- **UI** — a second table under the existing process table:
  *User · Presence · State · Last activity · In · Out · Total*.
  Idle rows greyed via a CSS class (`.traffic-row--idle`), no inline styles.
- Empty state: "No user traffic yet".

---

## 7a. Explaining the two different sources on the page (required)

The "Users online" tile and the per-user traffic table are fed by **different mechanisms** and will
legitimately disagree. The page must state this, in three places:

1. **The tile itself names its source.** Label it `Users online` with the sub-caption `SignalR sessions` (and a
   `title` tooltip: *"Distinct users with an active SignalR (chat/Blazor) connection. Does not include sync
   clients."*).
2. **The traffic table carries a caption:**
   > *Transferred (this session) — counts every client, including ones that are not in the online-user count
   > above, such as the desktop sync client (which streams over SSE rather than a SignalR hub). Total rows can
   > therefore exceed the online-user count.*
3. **Each row shows its presence state**, so the difference is visible rather than inferred:

| Presence | Meaning |
| -------- | ------- |
| `Online` | hub connected and recently active |
| `Away` | hub connected, idle past the threshold |
| `DoNotDisturb` | hub connected, DND enabled |
| `Not connected` | **transferring without a hub connection** — sync client or token/REST client |

`Not connected` is the diagnostically interesting row: it tells an admin "this user is moving bytes but is not
browsing or chatting" — i.e. a background sync, not a person clicking around.

The copy must avoid implying a bug. Suggested tooltip on the footer of the traffic table:
*"These figures come from network counters. The online-user count comes from SignalR connections. They measure
different things."*

---

## 8. Tests

| Test | Coverage |
| ---- | -------- |
| `TrafficAccountingTests` | per-user adds accumulate; 1 KB quantum stamps activity while keep-alive-sized writes do not; concurrent increments from many users; prune drops only past-grace entries; entry re-appears with zeroed counters; hard-cap eviction; `isIdle` agrees with the pruner |
| `TrafficAccountingMiddlewareTests` (new) | authenticated request attributed to the user; anonymous request not counted at all; `/api/v1/core/admin/system` excluded; WebSocket frames attributed; no double counting |
| `AdminSystemControllerTests` | payload shape; `isIdle` reflects the threshold; ordering by total; unattributed surfaced; `presenceState` is `NotConnected` for a user with traffic but no hub connection, and mirrors the tracker state when present |
| `SystemMetricsSamplerTests` | prune invoked on tick; idle threshold honoured |

Naming: `MethodName_Condition_ExpectedResult`.

---

## 9. Risks

| Risk | Mitigation |
| ---- | ---------- |
| Per-session totals misread as daily usage | Column label "Transferred (this session)" + no persistence anywhere |
| Sum of rows is below the global total and looks like a bug | Deliberate: forgotten entries drop their counters while the global keeps counting. §7a copy explains the per-row "this session" scope and no reconciliation figure is shown. |
| Open tab never goes idle because of SignalR keep-alives | Activity quantised to 1 KB of attributed bytes; pings never reach it |
| Memory growth from pathological churn | 8-minute horizon plus 512-entry hard cap |
| Hot-path regression on large transfers | One hash lookup per chunk; benchmark upload/download throughput before/after (expect < 2 %) |
| Coupling to the chat presence setting | Documented; a dedicated `SystemSettingKeys` entry is a one-line change if it bites |
| Long-lived silent WebSocket pruned mid-connection | Per-chunk lookup (never a cached entry reference); next frame re-creates the entry |
| **Admin reads the online-user tile and the traffic table as contradicting each other** (tile says 1, table lists 3) and reports it as a bug | Mandatory on-page explanation per §7a: the tile names its source (`SignalR sessions`), the table carries a caption saying it includes clients outside that count, and every row shows its presence state (`Not connected` for sync clients) |
| Presence column misread as "should be online but isn't" | `Not connected` is documented as the normal state for sync/token clients, not an error |

---

## 10. Effort

| Work | Estimate |
| ---- | -------- |
| Accounting map + quantum + prune + snapshot | 1.5 h |
| Middleware attribution | 0.5 h |
| Controller + DTO | 0.5 h |
| UI table + idle styling + presence column + source captions (§7a) | 1.5 h |
| Tests | 1.5 h |
| **Total** | **~0.5 day** |

---

## 11. Post-implementation (repo rules)

- Update `docs/IMPLEMENTATION_CHECKLIST.md` and `docs/MASTER_PROJECT_PLAN.md` with targeted edits.
- Build + full test suite green before any commit.
- Deploy (`sudo ./scripts/deploy.sh --force --verify`) and verify against a live admin session before commit
  approval: counters climb for the browsing user, an idle tab greys out, a second account shows as its own
  row, and unattributed stays flat while browsing.
