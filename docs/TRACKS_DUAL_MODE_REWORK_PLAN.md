# Tracks Module Dual-Mode Rework Plan

**Created:** 2026-04-02
**Status:** In Progress — Phases A, B, C Complete
**Scope:** Full rework of Tracks module into Personal and Team paradigms

---

## Summary

Rework the Tracks module from a generic Kanban system into two distinct paradigms:

- **Personal Mode** — Simple multi-board kanban without sprints, teams, or planning overhead. Users can create multiple personal boards, each a clean drag-and-drop kanban.
- **Team Mode** — Full project management with year-long sprint planning, backlog management, Gantt timeline, and live review sessions with integrated planning poker. Sprints are views/filters on one project board (not separate boards). A planning wizard lets PMs define all sprints upfront with adjustable durations (1–16 weeks). A new "Review Session" feature synchronizes the PM's current card view to all team members in real-time with integrated planning poker.

---

## Decisions

| Decision | Resolution |
|----------|-----------|
| Sprint model | Sprints are **views/filters**, not separate boards. One Board entity per project; `SprintCard` join table determines which sprint a card belongs to. Sprint "view" = filtered kanban showing only that sprint's cards in the board's swimlanes. |
| Swimlane ownership | Defined at **board level**. PM defines swimlanes during wizard step 2; these ARE the board's swimlanes used across all sprint views. No separate "swimlane template" entity needed. |
| Personal mode boards | **Multiple boards allowed**. Users can have many personal boards, each a simple kanban with no sprint/team features. |
| Review session model | **New entity pair** — `ReviewSession` + `ReviewSessionParticipant` for live meeting mode. One active session per board at a time. |
| Poker + review integration | **Linked, not replaced**. Existing `PokerSession` gets optional `ReviewSessionId` FK. Poker can still be used standalone outside review sessions. |
| Sprint duration range | **1–16 weeks**. Stored as `DurationWeeks` on Sprint entity. `EndDate` computed from `StartDate + (DurationWeeks × 7 days)`. |
| Card carryover | **Yes**. When a sprint completes with incomplete cards, `SprintCompletionDialog` shows them and lets PM bulk-move to next sprint or back to backlog. |
| Review session persistence | **Persistent**. Stored in DB, allow reconnect after disconnect. End explicitly by host. |
| Concurrent poker in review | **No**. One active poker per review session at a time. Previous must be accepted or cancelled first. |
| Data migration | **Pre-launch** — no production data to migrate. Can restructure entities freely. |

---

## Phase A: Data Model & Mode System

### Step 1 — Add `BoardMode` enum
- Add `BoardMode` enum (`Personal`, `Team`) in `TracksDtos.cs` alongside existing enums
- No dependencies

### Step 2 — Add `Mode` property to Board entity
- Modify `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/Board.cs` — add `Mode` property, default `Personal`
- Update `BoardConfiguration` in `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Configuration/`
- Update `CreateBoardDto`, `BoardDto` in `TracksDtos.cs`

### Step 3 — Add sprint planning fields to Sprint entity
- Modify `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/Sprint.cs`
- Add `DurationWeeks` (int, 1–16) and `PlannedOrder` (int)
- Sprint already has `StartDate`/`EndDate`; `EndDate` becomes computed from `StartDate + DurationWeeks`
- Update `CreateSprintDto`, `SprintDto`

### Step 4 — Create ReviewSession entity
- New file: `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/ReviewSession.cs`
- Fields: `Id`, `BoardId`, `HostUserId`, `CurrentCardId` (nullable), `Status` (Active/Paused/Ended), `CreatedAt`, `EndedAt`
- Navigation properties: `Board`, `Participants` collection

### Step 5 — Create ReviewSessionParticipant entity
- New file: `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/ReviewSessionParticipant.cs`
- Fields: `Id`, `ReviewSessionId`, `UserId`, `JoinedAt`, `IsConnected`

### Step 6 — Link PokerSession to ReviewSession
- Modify `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/PokerSession.cs`
- Add optional `ReviewSessionId` FK — enables poker sessions as part of a live review flow

### Step 7 — Add ReviewSessionStatus enum
- Add `ReviewSessionStatus` enum (`Active`, `Paused`, `Ended`) in `TracksDtos.cs`

### Step 8 — EF Configuration & Migration
- Modify `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/TracksDbContext.cs` — add DbSets for `ReviewSession`, `ReviewSessionParticipant`
- New EF configurations for `ReviewSession`, `ReviewSessionParticipant`
- Generate migration covering all Phase A changes
- **Depends on:** Steps 1–7

---

## Phase B: Service Layer — Mode & Sprint Planning

### Step 9 — Mode-aware BoardService changes
- Modify `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/` — `BoardService`
- Add mode checks to `CreateBoard`, `UpdateBoard`
- Personal boards: skip sprint validation, block team assignment
- Team boards: require team, enable sprint features
- `ListBoards` supports filtering by mode
- **Guard:** Team-only operations (sprint create, team assign, review session) return validation error on Personal boards

### Step 10 — Sprint Planning Wizard Service
- New `ISprintPlanningService` / `SprintPlanningService`
- Methods:
  - `CreateYearPlanAsync(boardId, startDate, sprintCount, durationWeeks, callerId)` — bulk creates N sprints with sequential dates
  - `AdjustSprintAsync(sprintId, newDurationWeeks, newStartDate)` — adjust individual sprint, auto-cascade subsequent sprint dates
  - `GetPlanOverviewAsync(boardId)` — returns all sprints with status, dates, card counts for timeline view
- Validation: only on Team-mode boards, only Admin+ can create plans, durations 1–16 weeks
- **Depends on:** Steps 2, 3

### Step 11 — Backlog Service additions
- Modify `CardService`:
  - Add `GetBacklogCardsAsync(boardId)` — cards where no `SprintCard` record exists
  - Add optional `sprintId` filter parameter to `ListCards` — show only cards in a specific sprint
- **Depends on:** Step 2 (mode-awareness)

### Step 12 — Review Session Service
- New `IReviewSessionService` / `ReviewSessionService`
- Methods:
  - `StartSessionAsync(boardId, hostUserId)` — creates `ReviewSession`, host auto-joins as participant
  - `JoinSessionAsync(sessionId, userId)` — adds participant
  - `LeaveSessionAsync(sessionId, userId)` — marks participant disconnected
  - `SetCurrentCardAsync(sessionId, cardId, hostUserId)` — PM navigates to card, triggers broadcast
  - `StartPokerForCurrentCardAsync(sessionId, hostUserId, scale)` — starts poker session linked to review session + current card
  - `GetSessionStateAsync(sessionId)` — returns current card, participants, active poker session
  - `EndSessionAsync(sessionId, hostUserId)` — ends session
- Validation: one active session per board, only Team-mode boards, only Admin+ can host
- **Depends on:** Steps 4, 5, 6, 9

### Step 13 — Poker Service modifications
- Modify `PokerService` — add `GetVoteStatusAsync(sessionId)` returning list of `{userId, hasVoted}` (no vote values)
- When poker is linked to a review session, broadcast vote-status (voted/not-voted) per participant
- Votes hidden until `RevealPokerSession` — existing reveal/accept logic works as-is
- **Depends on:** Step 6

---

## Phase C: API Layer Changes

### Step 14 — Board mode endpoints
- Modify `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/BoardsController.cs`
- Add `mode` parameter to `POST /api/v1/boards` and return in `GET`
- **Depends on:** Step 9

### Step 15 — Sprint wizard endpoints
- Add to `SprintsController` or new controller:
  - `POST /api/v1/boards/{boardId}/sprint-plan` — create year plan (bulk sprints)
  - `PUT /api/v1/sprints/{sprintId}/adjust` — adjust sprint duration/dates with cascade
  - `GET /api/v1/boards/{boardId}/sprint-plan` — get plan overview for timeline
- **Depends on:** Step 10

### Step 16 — Backlog endpoints
- Modify `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/CardsController.cs`
- `GET /api/v1/boards/{boardId}/backlog` — cards not in any sprint
- Add sprint filter query param: `GET /api/v1/boards/{boardId}/cards?sprintId={id}`
- **Depends on:** Step 11

### Step 17 — Review session endpoints
- New `ReviewSessionController`:
  - `POST /api/v1/boards/{boardId}/review-session` — start session
  - `POST /api/v1/review-sessions/{id}/join` — join session
  - `POST /api/v1/review-sessions/{id}/leave` — leave session
  - `PUT /api/v1/review-sessions/{id}/current-card` — set current card (host only)
  - `POST /api/v1/review-sessions/{id}/poker` — start poker for current card
  - `GET /api/v1/review-sessions/{id}` — get session state
  - `POST /api/v1/review-sessions/{id}/end` — end session
- **Depends on:** Step 12

### Step 18 — Poker vote status endpoint
- Modify `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/PokerController.cs`
- `GET /api/v1/poker/{sessionId}/vote-status` — returns `{userId, hasVoted}[]` (no actual vote values)
- **Depends on:** Step 13

### Step 19 — gRPC proto updates
- Modify `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Protos/tracks_service.proto`
- Add review session RPCs, sprint plan RPCs, board mode to existing messages
- **Depends on:** Steps 14–18

---

## Phase D: Real-Time / SignalR

### Step 20 — Review session SignalR broadcasts
- Modify `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Services/TracksRealtimeService.cs`
- Extend `ITracksRealtimeService` with:
  - `BroadcastReviewCardChangedAsync(sessionId, boardId, cardId)`
  - `BroadcastReviewSessionStateAsync(sessionId, action)` — "started", "ended", "paused"
  - `BroadcastPokerVoteStatusAsync(sessionId, pokerId, userId, hasVoted)` — per-vote notification
- SignalR group: `tracks-review-{sessionId}`
- **Depends on:** Step 12

### Step 21 — Client-side SignalR events for review
- Modify `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Services/ITracksSignalRService.cs` and implementation
- New events: `ReviewCardChanged`, `ReviewSessionStateChanged`, `PokerVoteStatusChanged`
- **Depends on:** Step 20

---

## Phase E: UI — Personal Mode Simplification

### Step 22 — Board creation dialog with mode selection
- Modify `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/BoardListView.razor` create board form
- When creating a board, user picks "Personal" or "Team"
- Personal: just name, color, optional description
- Team: name, color, description, team selection
- **Depends on:** Step 14

### Step 23 — Conditional UI in TracksPage
- Modify `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/TracksPage.razor` sidebar + view switcher
- Hide sprint panel, team nav, planning view for Personal boards
- Personal board: Boards list + Board (kanban) views only
- Team board: All views available
- **Depends on:** Step 22

### Step 24 — Clean personal kanban view
- Modify `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/KanbanBoard.razor`
- Strip sprint-related controls (sprint labels, sprint assignment on cards) when `Mode == Personal`
- **Parallel with:** Step 23

---

## Phase F: UI — Sprint Planning Wizard

### Step 25 — Sprint Planning Wizard component
- New file: `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/SprintPlanningWizard.razor`
- Multi-step wizard:
  - **Step 1: Plan basics** — start date, number of sprints, default duration (1–16 weeks dropdown)
  - **Step 2: Swimlane definition** — PM defines swimlanes (columns) that will apply to all sprint views (these are the board's swimlanes)
  - **Step 3: Sprint schedule** — shows all generated sprints in a list/table, allows individual duration adjustments (1–16 weeks per sprint), auto-cascades dates
  - **Step 4: Review & Create** — summary view, confirm button creates board + swimlanes + all sprints in one operation
- **Depends on:** Steps 15, 22

### Step 26 — New TracksView: Wizard
- Add to the view switcher in TracksPage
- Accessible from team board creation flow or from board settings (to add a year plan to an existing team board)
- **Depends on:** Step 25

---

## Phase G: UI — Backlog & Sprint Views

### Step 27 — Backlog View component
- New file: `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/BacklogView.razor`
- Shows cards not assigned to any sprint
- Drag-and-drop to assign cards to sprints (or bulk assign)
- Priority/label filtering
- Card count + total story points
- **Depends on:** Step 16

### Step 28 — Sprint-filtered Kanban view
- Modify `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/KanbanBoard.razor`
- Accept optional `sprintId` parameter and filter cards
- Sprint selector dropdown/tabs at top of board view:
  - "Backlog" tab — unsprinted cards on the board
  - "Sprint N" tabs — cards in that sprint
  - "All" tab — everything (current behavior)
- **Depends on:** Step 16

### Step 29 — New TracksView: Backlog
- Add to view switcher in TracksPage, visible only for Team boards
- **Depends on:** Steps 27, 23

---

## Phase H: UI — Year Timeline / Gantt View

### Step 30 — Timeline View component
- New file: `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/TimelineView.razor`
- Gantt-style horizontal timeline showing sprints as blocks across months
- Each sprint block: colored by status (Planning=gray, Active=blue, Completed=green)
- Sprint blocks show: name, duration, card count, story points
- Click sprint block → navigate to sprint-filtered kanban view
- Drag sprint edges → adjust duration (calls adjust endpoint with cascade)
- Today marker line
- Month/quarter labels on X-axis
- **Depends on:** Step 15

### Step 31 — New TracksView: Timeline
- Add to view switcher, visible only for Team boards with a sprint plan
- **Depends on:** Step 30

---

## Phase I: UI — Live Review Mode

### Step 32 — Review Session Host Controls
- New file: `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/ReviewSessionHost.razor`
- Start/end session button (visible to Admin+ on Team boards)
- Card navigator: prev/next card within current sprint or backlog
- Participant list with online status
- "Start Poker" button on current card → launches poker for that card
- Poker control panel: see vote status (who voted, who hasn't), "Reveal" button, "Accept Estimate" button
- One active poker per review session — previous must be accepted or cancelled first
- **Depends on:** Steps 17, 18, 21

### Step 33 — Review Session Participant View
- New file: `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/ReviewSessionParticipant.razor`
- Auto-follows host's current card (card detail panel updates via SignalR)
- Shows poker input (point selector) when poker is active on current card
- Shows "Waiting for reveal" after voting
- Shows revealed votes + accepted estimate when host reveals
- Indicator showing which team members have/haven't voted
- **Depends on:** Steps 21, 32

### Step 34 — Review Session entry in TracksPage
- New `TracksView.Review` state in view switcher with join/host flow
- If user is Admin+ → "Start Review" button in sidebar
- If active session exists → "Join Review" button for other members
- Review view replaces main content area with host or participant component
- **Depends on:** Steps 32, 33

---

## Phase J: Tests

### Step 35 — Data model & migration tests
- Verify new entities, updated fields, EF configurations
- **Parallel** — can start once Phase A is done

### Step 36 — Mode-aware service tests
- Personal board blocks sprint/team/review operations; Team board allows all
- Test `BoardService` mode guards
- **Depends on:** Step 9

### Step 37 — Sprint planning wizard tests
- Bulk creation, date cascading, duration validation (1–16 weeks), plan overview
- Test `SprintPlanningService`
- **Depends on:** Step 10

### Step 38 — Review session service tests
- Start/join/leave/set card/start poker/end lifecycle
- Security: only host can set card, only Admin+ can host, one active session per board
- Test `ReviewSessionService`
- **Depends on:** Step 12

### Step 39 — Poker vote status tests
- Vote status visibility without revealing values
- **Depends on:** Step 13

### Step 40 — Controller tests
- New endpoints: sprint plan, backlog, review session, vote status
- **Depends on:** Phase C

### Step 41 — Security tests
- Mode enforcement (personal blocks team ops), review session auth, poker during review auth
- Extend `tests/DotNetCloud.Modules.Tracks.Tests/TracksSecurityTests.cs`
- **Depends on:** Phases B + C

### Step 42 — Performance tests
- Year plan with 52 sprints, review session with 20 participants
- Extend `tests/DotNetCloud.Modules.Tracks.Tests/TracksPerformanceTests.cs`
- **Depends on:** Phases B + C

---

## Files Summary

### Modify

| File | Changes |
|------|---------|
| `src/Core/DotNetCloud.Core/DTOs/TracksDtos.cs` | Add `BoardMode`, `ReviewSessionStatus` enums; update `CreateBoardDto`, `BoardDto`, `CreateSprintDto`, `SprintDto` |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/Board.cs` | Add `Mode` property |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/Sprint.cs` | Add `DurationWeeks`, `PlannedOrder` |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/PokerSession.cs` | Add optional `ReviewSessionId` FK |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/TracksDbContext.cs` | Add DbSets for new entities |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/BoardService` | Mode-aware guards |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/CardService` | Backlog + sprint filter |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/PokerService` | Vote status method |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Services/TracksRealtimeService.cs` | Review session broadcasts |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Services/ITracksSignalRService.cs` | Review session client events |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/BoardsController.cs` | Mode parameter |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/CardsController.cs` | Backlog/sprint filter |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/SprintsController.cs` | Plan endpoints |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/PokerController.cs` | Vote status endpoint |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Protos/tracks_service.proto` | Review session RPCs, sprint plan RPCs |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/TracksPage.razor` | View switcher, mode-aware sidebar |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/BoardListView.razor` | Mode selection in create dialog |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/KanbanBoard.razor` | Sprint filter tabs, personal mode cleanup |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/SprintCompletionDialog.razor` | Card carryover flow on sprint complete |

### Create

| File | Purpose |
|------|---------|
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/ReviewSession.cs` | Review session entity |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/ReviewSessionParticipant.cs` | Review session participant entity |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/SprintPlanningService.cs` | Sprint wizard business logic |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/ReviewSessionService.cs` | Review session business logic |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/ReviewSessionController.cs` | Review session REST endpoints |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/SprintPlanningWizard.razor` | Multi-step year planning wizard |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/BacklogView.razor` | Unsprinted cards pool |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/TimelineView.razor` | Gantt-style year view |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/ReviewSessionHost.razor` | PM/Scrum Master review controls |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/ReviewSessionParticipant.razor` | Team member review view |

---

## Dependency Graph

```
Phase A (Data Model) ──┬──→ Phase B (Services) ──→ Phase C (API) ──→ Phase D (SignalR)
                       │                                              │
                       │     Phase E (Personal UI) ←── Phase C ───────┤
                       │     Phase F (Wizard UI) ←── Phase C ─────────┤
                       │     Phase G (Backlog UI) ←── Phase C ────────┤
                       │     Phase H (Timeline UI) ←── Phase C ───────┤
                       │     Phase I (Review UI) ←── Phase D ─────────┘
                       │
                       └──→ Phase J (Tests) — parallel with B through I
```

---

## Verification Checklist

- ☐ `dotnet build` — solution compiles with all new entities, services, controllers
- ☐ `dotnet test tests/DotNetCloud.Modules.Tracks.Tests/` — all existing tests still pass (backward compat); new tests pass
- ☐ **Personal mode** — create personal board, verify no sprint/team UI appears, kanban works normally
- ☐ **Team mode** — create team board via wizard, define sprints, verify swimlanes appear in all sprint views
- ☐ **Year timeline** — open timeline view, see all sprints as Gantt blocks, drag to adjust duration, verify cascade
- ☐ **Backlog** — cards without sprint show in backlog, drag to sprint assigns them
- ☐ **Review session** — PM starts session, team member joins, PM navigates cards → member's view follows. Start poker → member votes → PM sees "voted" status → PM reveals → all see votes
- ☐ **Sprint filter** — on kanban board, switch between sprint tabs, verify only that sprint's cards show
- ☐ **Sprint completion carryover** — complete sprint with incomplete cards, verify PM can bulk-move to next sprint or backlog
- ☐ **Security audit** — personal board rejects sprint/team/review operations; non-host cannot set current card; non-Admin cannot start review
