# Phase 4: Project Management (Tracks) — Implementation Plan

> **Goal:** Kanban boards + Jira-like project tracking as a process-isolated module.
> **Module ID:** `dotnetcloud.tracks`
> **Namespace:** `DotNetCloud.Modules.Tracks`
> **Optional:** Yes — Tracks is not a required module. Installations can operate without it. The module is listed in `SetupCommand.cs` optional modules and can be enabled/disabled from the admin UI.

---

## Design Constraints

- **Optional module:** Core platform and other modules must function without Tracks installed. No hard dependencies from Core → Tracks. Cross-module references (e.g., file attachments on cards) use the event bus and capability system — if Tracks isn't installed, those events simply have no subscribers.
- **Process-isolated:** Runs as a separate process communicating via gRPC, following the standard module pattern.
- **Self-contained data:** Tracks has its own `TracksDbContext` and database schema. No foreign keys to other module tables.

---

## Overview

Tracks is an **optional** project management module providing kanban boards with lists, cards, labels, due dates, assignments, sprints, time tracking, and dependencies. Not all installations will have Tracks enabled — the core platform and all other modules operate independently of it. It follows the exact same 3-tier module pattern as Files, Chat, Contacts, Calendar, and Notes (module library → data layer → gRPC host).

**Key differentiator from NextCloud Deck:** Tracks adds sprint management, time tracking, card dependencies, and board templates — closer to Jira/Linear than a basic kanban tool, while keeping the simplicity of board-based workflows.

---

## Phase Breakdown

### Phase 4.1 — Architecture & Contracts ✅

Core DTOs, events, and capability interfaces added to `DotNetCloud.Core`.

**Deliverables:**
- ✓ `TracksDtos.cs` — 21 DTO records: BoardDto, BoardMemberDto, BoardListDto, CardDto, CardAssignmentDto, LabelDto, CardCommentDto, CardAttachmentDto, CardChecklistDto, ChecklistItemDto, CardDependencyDto, SprintDto, TimeEntryDto, BoardActivityDto + 7 request DTOs + 4 enums
- ✓ `TracksEvents.cs` — 10 domain events: BoardCreatedEvent, BoardDeletedEvent, CardCreatedEvent, CardMovedEvent, CardUpdatedEvent, CardDeletedEvent, CardAssignedEvent, CardCommentAddedEvent, SprintStartedEvent, SprintCompletedEvent
- ✓ `ITracksDirectory` capability interface (Public tier) — board/card lookup for cross-module integration + CardSummary record
- ✓ Error codes: 15 `TRACKS_` domain codes in `ErrorCodes.cs`
- ✓ Unit tests — 49 tests: 34 DTO, 10 event, 5 capability (all passing)
- ✓ `ITeamDirectory` capability interface (Restricted tier) — cross-module read-only team/membership access with `TeamInfo` and `TeamMemberInfo` records (5 methods: GetTeam, GetTeamsForUser, IsTeamMember, GetTeamMember, GetTeamMembers)
- ✓ `ITeamManager` capability interface (Restricted tier) — cross-module team CRUD and member management (5 methods: CreateTeam, UpdateTeam, DeleteTeam, AddMember, RemoveMember)
- ✓ Tracks team DTOs: `TracksTeamDto`, `TracksTeamMemberDto`, `CreateTracksTeamDto`, `UpdateTracksTeamDto`, `TransferBoardDto`, `TracksTeamMemberRole` enum (Member/Manager/Owner)
- ✓ Tracks team events: `TeamCreatedEvent`, `TeamDeletedEvent`
- ✓ Tracks team error codes: `TracksTeamNotFound`, `TracksNotTeamMember`, `TracksInsufficientTeamRole`, `TracksTeamHasBoards`, `TracksAlreadyTeamMember`

**Teams Architecture — Option C (Core teams = identity, Tracks extends with roles):**
Core platform owns team identity and membership via `ITeamDirectory` (read) and `ITeamManager` (write). Tracks stores module-specific role assignments in a `TeamRole` entity that maps Core team members to Tracks-specific roles (Member/Manager/Owner). This allows each module to define its own role semantics while sharing a single team identity across the platform. No cross-DB foreign keys — application-level validation only.

---

### Phase 4.2 — Data Model & Module Scaffold ✅

Module projects + EF Core data layer.

**Deliverables:**
- ✓ `DotNetCloud.Modules.Tracks/` — Module library (TracksModule.cs, TracksModuleManifest.cs, manifest.json, 16 entity models + PokerSession + PokerVote)
- ✓ `DotNetCloud.Modules.Tracks.Data/` — TracksDbContext (18 DbSets), 18 EF configurations, design-time factory, db initializer, service registration
- ✓ `DotNetCloud.Modules.Tracks.Host/` — gRPC host scaffold (Program.cs, TracksGrpcService with 11 RPCs incl. 4 poker RPCs, TracksLifecycleService, TracksHealthCheck, InProcessEventBus, TracksControllerBase, tracks_service.proto)
- ✓ Solution integration (all 3 projects in DotNetCloud.sln + DotNetCloud.CI.slnf)
- ✓ Integrated planning poker: PokerSession/PokerVote entities, PokerSessionStatus/PokerScale enums, 6 DTOs, 3 events, 4 error codes, 14 new unit tests

**Data Model (Entities):**

| Entity | Description | Key Fields |
|--------|-------------|------------|
| **Board** | Top-level container | Title, Description, OwnerId, Color, IsArchived, CreatedAt |
| **BoardMember** | Board membership + role | BoardId, UserId, Role (Owner/Admin/Member/Viewer) |
| **BoardList** | Column in a board | BoardId, Title, Position, Color, CardLimit (WIP limit) |
| **Card** | Work item / task | ListId, Title, Description (Markdown), Position, DueDate, Priority, StoryPoints, IsArchived |
| **CardAssignment** | Card ↔ User | CardId, UserId, AssignedAt |
| **Label** | Reusable tag per board | BoardId, Title, Color |
| **CardLabel** | Card ↔ Label join | CardId, LabelId |
| **CardComment** | Discussion on card | CardId, UserId, Content (Markdown), CreatedAt |
| **CardAttachment** | File link on card | CardId, FileNodeId (FK to Files module, nullable), FileName, Url |
| **CardChecklist** | Subtask checklist | CardId, Title, Position |
| **ChecklistItem** | Single checklist item | ChecklistId, Title, IsCompleted, Position |
| **CardDependency** | Card ↔ Card relation | CardId, DependsOnCardId, Type (BlockedBy/RelatesTo) |
| **Sprint** | Time-boxed iteration | BoardId, Title, StartDate, EndDate, Goal, Status (Planning/Active/Completed) |
| **SprintCard** | Card ↔ Sprint | SprintId, CardId |
| **TimeEntry** | Time tracking | CardId, UserId, StartTime, EndTime, Duration, Description |
| **BoardActivity** | Audit log per board | BoardId, UserId, Action, EntityType, EntityId, Details (JSON), CreatedAt |
| **TeamRole** | Tracks role overlay for Core teams (Option C) | CoreTeamId, UserId, Role (Member/Manager/Owner), AssignedAt |

`Board.TeamId` (nullable Guid) is a cross-DB reference to a Core team. No FK enforcement — app-level validation only.

---

### Phase 4.3 — Core Services & Business Logic ✅

Service implementations for all domain operations.

**Status:** Completed

**Deliverables:**
- ✓ `BoardService` — CRUD boards, manage members/roles, archive/unarchive
- ✓ `ListService` — CRUD lists, reorder (gap-based positioning), WIP limit enforcement
- ✓ `CardService` — CRUD cards, move between lists, assign/unassign users, update priority/due date, archive
- ✓ `LabelService` — CRUD labels per board, assign/remove from cards
- ✓ `CommentService` — CRUD comments with Markdown content (stored as-is)
- ✓ `ChecklistService` — CRUD checklists and items, toggle completion
- ✓ `AttachmentService` — Link files (from Files module or external URL), remove
- ✓ `DependencyService` — Add/remove card dependencies, BFS cycle detection for BlockedBy
- ✓ `SprintService` — CRUD sprints, start/complete lifecycle, move cards in/out
- ✓ `TimeTrackingService` — Start/stop timer, manual entry, duration rollup
- ✓ `ActivityService` — Log all mutations, query activity feed per board/card
- ✓ Authorization logic — Board role checks via EnsureBoardRoleAsync (Owner/Admin/Member/Viewer)
- ✓ Unit tests (112 tests covering all 11 services — exceeded ~80 target)
- ✓ `TeamService` — Option C implementation: Core teams via ITeamDirectory (read) + ITeamManager (write), Tracks TeamRoles overlay
  - ✓ Team CRUD (create → Core team + Tracks Owner role, update via ITeamManager, delete with block/cascade)
  - ✓ Member add/remove/update role (ITeamManager + TeamRoles). Owner protected from removal; last-owner demotion blocked.
  - ✓ Board transfer (personal ↔ team). Requires board Owner role + team Manager role on target.
  - ✓ `GetEffectiveBoardRoleAsync` — merges direct board membership + team-derived role (higher wins). Team role mapping: Owner→BoardOwner, Manager→BoardAdmin, Member→BoardMember. Core team members without a Tracks role get default BoardMember access.
  - ✓ Graceful degradation — ITeamDirectory/ITeamManager injected as nullable; operations fail cleanly when capabilities unavailable.
- ✓ `TeamDirectoryService` — ITeamDirectory implementation in Core.Auth (reads from CoreDbContext)
- ✓ `TeamManagerService` — ITeamManager implementation in Core.Auth (writes to CoreDbContext)
- ✓ DI registration for ITeamDirectory + ITeamManager as scoped services in AuthServiceExtensions
- ✓ 29 TeamServiceTests (team CRUD, member management, board transfer, effective role resolution, error cases)

**Notes:** 12 services total (11 original + TeamService). Option C team architecture: Core owns team identity/membership, Tracks stores module-specific role assignments in TeamRole entity. `GetEffectiveBoardRoleAsync` resolves the effective board role by checking both direct BoardMember entries and team-derived roles, returning whichever is higher. All services follow established DI patterns. 141 total service-level tests (112 original + 29 team).

---

### Phase 4.4 — REST API & gRPC Service ✅

API endpoints and inter-process communication.

**Status:** Completed

**Deliverables:**

**REST API (50+ endpoints — 10 controllers):**
- ✓ `BoardsController` — GET/POST/PUT/DELETE boards, GET /boards/{id}/activity, GET /boards/{id}/export, POST /boards/import
- ✓ Board members — GET/POST/DELETE /boards/{id}/members, PUT /boards/{id}/members/{userId}/role
- ✓ Board labels — GET/POST/PUT/DELETE /boards/{id}/labels
- ✓ `ListsController` — GET/POST/PUT/DELETE /boards/{boardId}/lists, PUT /lists/reorder
- ✓ `CardsController` — GET/POST/PUT/DELETE cards, PUT /cards/{id}/move, POST/DELETE assign, POST/DELETE labels, GET activity
- ✓ `CommentsController` — GET/POST/PUT/DELETE /cards/{id}/comments
- ✓ `ChecklistsController` — CRUD checklists + items, PUT /items/{id}/toggle, DELETE items
- ✓ `AttachmentsController` — GET/POST/DELETE /cards/{id}/attachments
- ✓ `DependenciesController` — GET/POST/DELETE /cards/{id}/dependencies (cycle detection → 409 Conflict)
- ✓ `SprintsController` — GET/POST/PUT/DELETE sprints, POST /sprints/{id}/start, POST /sprints/{id}/complete, POST/DELETE cards
- ✓ `TimeEntriesController` — GET/POST/DELETE time entries, POST /cards/{id}/timer/start, POST /cards/{id}/timer/stop
- ✓ `TeamsController` — 10 endpoints: POST /teams (create), GET /teams (list), GET /teams/{id} (get), PUT /teams/{id} (update), DELETE /teams/{id} (delete, ?cascade=true), POST /teams/{id}/members (add), DELETE /teams/{id}/members/{userId} (remove), PUT /teams/{id}/members/{userId}/role (update role), POST /teams/{id}/transfer-board (transfer), GET /teams/{id}/boards (list team boards)

**gRPC Service:**
- ✓ `TracksGrpcService` — Full implementation of 7 RPCs (CreateBoard, GetBoard, ListBoards, CreateList, CreateCard, GetCard, MoveCard) calling actual service layer; 4 poker RPCs remain stubs (deferred to Phase 4.7)
- ✓ `TracksLifecycleService` — Module lifecycle (existing from Phase 4.2)

**Base Controller Infrastructure:**
- ✓ `TracksControllerBase` — Auth helpers (GetAuthenticatedCaller), response envelopes (Envelope/ErrorEnvelope), IsBoardNotFound() helper for consistent 404 mapping

**Cross-Module Integration:**
- ☐ File attachment links (reference FileNode from Files module via event subscription) — deferred to Phase 4.6
- ☐ Chat integration — deferred to Phase 4.6

**Unit Tests (58 new controller + gRPC tests):**
- ✓ `BoardsControllerTests` — 10 tests: CRUD, activity, members, labels, export/import
- ✓ `CardsControllerTests` — 7 tests: CRUD, move, assign, activity
- ✓ `ListsControllerTests` — 5 tests: list CRUD, board-not-found handling
- ✓ `SprintsControllerTests` — 7 tests: CRUD, start/complete lifecycle
- ✓ `SubresourceControllerTests` — 19 tests: comments, checklists, attachments, dependencies, time entries
- ✓ `TracksGrpcServiceTests` — 10 tests: board/list/card gRPC RPCs

**Notes:** All 199 tests pass (141 service/team + 58 controller/gRPC). Controllers use consistent error handling: IsBoardNotFound() maps both BoardNotFound and NotBoardMember to 404. TeamsController follows same pattern with TracksTeamNotFound/TracksNotTeamMember/TracksInsufficientTeamRole error codes. Response envelope pattern: `{ success, data }` for success, `{ success, error: { code, message } }` for errors. Poker gRPC RPCs left as stubs — full implementation in Phase 4.7 with board templates and analytics.

---

### Phase 4.5 — Web UI (Blazor) ✅

Board and card management interface.

**Status:** completed

**Deliverables:**
- ✓ **Board list page** — Grid/list view of all boards the user is a member of, create board dialog
- ✓ **Board view** — Full kanban board with drag-and-drop cards between lists, add list, list settings
- ✓ **Card detail panel** — Slide-out panel showing card details, description (Markdown editor), assignments, labels, checklists, comments, attachments, time entries, dependencies, activity log
- ✓ **Sprint management** — Sprint planning view, backlog → sprint drag, sprint burndown/progress
- ✓ **Board settings** — Members, labels, archive management, board delete
- ✓ **Team management page** — Create/edit teams, manage team members and Tracks-specific roles (Owner/Manager/Member), transfer boards to teams
- ✓ **Team board view** — Team-scoped board list, team member role badges, team-owned board indicators
- ✓ **Filters & search** — Filter cards by label, assignee, due date, priority; search across boards; filter boards by team
- ☐ **Real-time updates** — SignalR integration for live board state (deferred to Phase 4.6)
- ✓ **Responsive layout** — Works on desktop and tablet; mobile-friendly card detail
- ✓ CSS styling consistent with existing DotNetCloud UI theme

**Notes:** All Blazor UI components implemented: TracksPage (main layout with sidebar), BoardListView, KanbanBoard (HTML5 drag-drop), CardDetailPanel (slide-out), SprintPanel, BoardSettingsDialog, TeamManagement. Full HTTP API client (ITracksApiClient/TracksApiClient). Module registered in ModuleUiRegistrationHostedService. SignalR real-time deferred to Phase 4.6 as planned.

---

### Phase 4.6 — Real-time & Notifications

Live updates and push notifications for board activity.

**Deliverables:**
- ☐ **SignalR Hub** — `TracksHub` for real-time board state sync (card moved, created, updated, deleted)
- ☐ **Team event notifications** — Real-time updates for team membership changes, role updates, board transfers
- ☐ **Notification integration** — Card assigned, due date approaching, mentioned in comment, sprint started/completed, team member added/removed
- ☐ **Activity feed** — Per-board and per-team activity stream with real-time additions
- ☐ **@mention support** — Parse @username in card descriptions and comments, send notifications

---

### Phase 4.7 — Advanced Features

Board templates, automation, and analytics.

**Deliverables:**
- ☐ **Board templates** — Pre-built templates (Kanban, Scrum, Bug Tracking, Personal TODO), create board from template
- ☐ **Card templates** — Save card as template, create card from template
- ☐ **Due date reminders** — Background service dispatching reminders (like Calendar's ReminderDispatchService)
- ☐ **Board analytics** — Cards completed over time, average cycle time (list → list), time in each list, per-user workload
- ☐ **Team analytics** — Team productivity metrics, boards per team, member activity, cross-team workload comparison
- ☐ **Sprint reports** — Velocity chart, burndown chart data (API endpoints returning chart-ready data)
- ☐ **Bulk operations** — Multi-select cards for move, label, assign, archive
- ☐ **Bulk team operations** — Assign team to multiple boards, batch role updates across team members

---

### Phase 4.8 — Testing, Documentation & Release

Comprehensive testing and documentation.

**Deliverables:**
- ☐ Unit tests — Full coverage of services, authorization, dependency cycle detection, team role resolution
- ☐ Integration tests — REST API endpoint tests, gRPC service tests, cross-module team capability tests
- ☐ Security tests — Board role authorization, team role escalation prevention, tenant isolation, Markdown XSS prevention
- ☐ Performance tests — Large board (1000+ cards) rendering, drag-and-drop reorder, team with many members
- ☐ Admin documentation — Module configuration, storage, permissions, team management
- ☐ User guide — Board management, card workflows, sprints, time tracking, team workflows
- ☐ API documentation — All REST endpoints documented (including Teams endpoints)
- ☐ Update README roadmap status

---

## Technical Decisions

### Drag-and-Drop Reordering
Cards and lists use a **position** integer field. On reorder, use gap-based positioning (intervals of 1000) to minimize database writes. Re-normalize positions when gaps get too small.

### Markdown Rendering
Reuse the existing Markdig + HtmlSanitizer pipeline from the Notes module for card descriptions and comments.

### File Attachments
Cards can reference files from the Files module (via `FileNodeId`) or external URLs. Cross-module file references use the event bus (subscribe to `FileDeletedEvent` to clean up broken references).

### Authorization Model
- **Board-level roles:** Owner, Admin, Member, Viewer
- **Team-level roles (Tracks-specific):** Owner, Manager, Member — mapped to board roles via `GetEffectiveBoardRoleAsync`
- Team Owner → BoardOwner, Team Manager → BoardAdmin, Team Member → BoardMember
- Core team members without a Tracks role get default BoardMember access to team-owned boards
- Owner/Admin: full board management, member management
- Member: create/edit/move cards, comment
- Viewer: read-only access
- No card-level permissions (keeps it simple; board membership controls access)

### Sprint Model
Optional — boards can be pure kanban (no sprints) or sprint-based. Sprint status lifecycle: Planning → Active → Completed. Only one active sprint per board at a time.

### Time Tracking
Optional per board. Timer-based (start/stop) or manual entry. Duration stored in minutes. Rollup queries by card, user, sprint, or date range.

---

## Dependencies

- `DotNetCloud.Core` — Capabilities, events, DTOs, CallerContext
- `DotNetCloud.UI.Shared` — Shared Blazor components, theme
- `DotNetCloud.Modules.Files` (optional) — File attachment cross-references via event bus
- `DotNetCloud.Modules.Chat` (optional) — Activity events for chat integration

---

## Module Manifest (Planned)

```csharp
public sealed class TracksModuleManifest : IModuleManifest
{
    public string Id => "dotnetcloud.tracks";
    public string Name => "Tracks";
    public string Version => "1.0.0";

    public IReadOnlyCollection<string> RequiredCapabilities => new[]
    {
        nameof(Core.Capabilities.INotificationService),
        nameof(Core.Capabilities.IUserDirectory),
        nameof(Core.Capabilities.ICurrentUserContext),
        nameof(Core.Capabilities.IRealtimeBroadcaster)
    };

    public IReadOnlyCollection<string> PublishedEvents => new[]
    {
        nameof(BoardCreatedEvent),
        nameof(BoardDeletedEvent),
        nameof(CardCreatedEvent),
        nameof(CardMovedEvent),
        nameof(CardUpdatedEvent),
        nameof(CardDeletedEvent),
        nameof(CardAssignedEvent),
        nameof(CardCommentAddedEvent),
        nameof(SprintStartedEvent),
        nameof(SprintCompletedEvent)
    };

    public IReadOnlyCollection<string> SubscribedEvents => new[]
    {
        "FileDeletedEvent"   // Clean up broken file attachment references
    };
}
```

---

## Estimated Phase Order

| Sub-Phase | Description | Est. Effort |
|-----------|-------------|-------------|
| 4.1 | Architecture & Contracts | 1 day |
| 4.2 | Data Model & Module Scaffold | 1-2 days |
| 4.3 | Core Services & Business Logic | 3-4 days |
| 4.4 | REST API & gRPC Service | 2-3 days |
| 4.5 | Web UI (Blazor) | 3-4 days |
| 4.6 | Real-time & Notifications | 1-2 days |
| 4.7 | Advanced Features | 2-3 days |
| 4.8 | Testing, Docs & Release | 2-3 days |

---

## Success Criteria (Phase 4 Milestone)

> **Milestone:** Teams manage projects with boards.

- ✓ Users can create boards with lists and cards
- ✓ Drag-and-drop card reordering and moving between lists
- ✓ Card assignments, labels, due dates, checklists, comments, attachments
- ✓ Sprint planning and tracking (optional per board)
- ✓ Time tracking (optional per board)
- ✓ Card dependencies with cycle detection
- ✓ Real-time board updates via SignalR
- ✓ Notifications for assignments, mentions, due dates
- ✓ Board templates for quick start
- ✓ Analytics: cycle time, velocity, burndown data
- ✓ Module follows established 3-tier pattern (module → data → host)
- ✓ All tests pass, security verified
