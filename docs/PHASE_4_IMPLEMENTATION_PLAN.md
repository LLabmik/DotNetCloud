# Phase 4: Project Management (Tracks) — Implementation Plan

> **Goal:** Kanban boards + Jira-like project tracking as a process-isolated module.
> **Module ID:** `dotnetcloud.tracks`
> **Namespace:** `DotNetCloud.Modules.Tracks`

---

## Overview

Tracks is a project management module providing kanban boards with lists, cards, labels, due dates, assignments, sprints, time tracking, and dependencies. It follows the exact same 3-tier module pattern as Files, Chat, Contacts, Calendar, and Notes (module library → data layer → gRPC host).

**Key differentiator from NextCloud Deck:** Tracks adds sprint management, time tracking, card dependencies, and board templates — closer to Jira/Linear than a basic kanban tool, while keeping the simplicity of board-based workflows.

---

## Phase Breakdown

### Phase 4.1 — Architecture & Contracts

Core DTOs, events, and capability interfaces added to `DotNetCloud.Core`.

**Deliverables:**
- ☐ `TracksDto.cs` — DTOs for Board, BoardList, Card, Label, CardAssignment, CardComment, CardAttachment, Sprint, TimeEntry, CardDependency
- ☐ `TracksEvents.cs` — Domain events: BoardCreatedEvent, BoardDeletedEvent, CardCreatedEvent, CardMovedEvent, CardUpdatedEvent, CardDeletedEvent, CardAssignedEvent, CardCommentAddedEvent, SprintStartedEvent, SprintCompletedEvent
- ☐ `ITracksDirectory` capability interface (Public tier) — board/card lookup for cross-module integration
- ☐ Error codes: `TRACKS_` domain codes in `ErrorCodes.cs`
- ☐ Unit tests for all new DTOs and events

---

### Phase 4.2 — Data Model & Module Scaffold

Module projects + EF Core data layer.

**Deliverables:**
- ☐ `DotNetCloud.Modules.Tracks/` — Module library (TracksModule.cs, TracksModuleManifest.cs)
- ☐ `DotNetCloud.Modules.Tracks.Data/` — TracksDbContext, entity models, EF configurations, migrations
- ☐ `DotNetCloud.Modules.Tracks.Host/` — gRPC host + REST controllers
- ☐ Solution integration (add to DotNetCloud.sln)

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

---

### Phase 4.3 — Core Services & Business Logic

Service implementations for all domain operations.

**Deliverables:**
- ☐ `BoardService` — CRUD boards, manage members/roles, archive/unarchive
- ☐ `ListService` — CRUD lists, reorder (position management), WIP limit enforcement
- ☐ `CardService` — CRUD cards, move between lists, assign/unassign users, update priority/due date, archive
- ☐ `LabelService` — CRUD labels per board, assign/remove from cards
- ☐ `CommentService` — CRUD comments with Markdown rendering + sanitization
- ☐ `ChecklistService` — CRUD checklists and items, toggle completion
- ☐ `AttachmentService` — Link files (from Files module or external URL), remove
- ☐ `DependencyService` — Add/remove card dependencies, cycle detection
- ☐ `SprintService` — CRUD sprints, start/complete sprints, move cards in/out of sprints
- ☐ `TimeTrackingService` — Start/stop timer, manual entry, duration rollup per card/user/sprint
- ☐ `ActivityService` — Log all mutations, query activity feed per board/card
- ☐ Authorization logic — Board role checks, card-level permissions via board membership
- ☐ Unit tests (target: ~80 tests covering all services)

---

### Phase 4.4 — REST API & gRPC Service

API endpoints and inter-process communication.

**Deliverables:**

**REST API (~40 endpoints):**
- ☐ `BoardsController` — GET/POST/PUT/DELETE boards, GET /boards/{id}/activity
- ☐ Board members — GET/POST/DELETE /boards/{id}/members, PUT /boards/{id}/members/{userId}/role
- ☐ `ListsController` — GET/POST/PUT/DELETE lists, PUT /lists/reorder
- ☐ `CardsController` — GET/POST/PUT/DELETE cards, PUT /cards/{id}/move, PUT /cards/reorder
- ☐ Card assignments — POST/DELETE /cards/{id}/assign
- ☐ Card labels — POST/DELETE /cards/{id}/labels
- ☐ `CommentsController` — GET/POST/PUT/DELETE /cards/{id}/comments
- ☐ `ChecklistsController` — CRUD checklists + items, PUT /items/{id}/toggle
- ☐ `AttachmentsController` — GET/POST/DELETE /cards/{id}/attachments
- ☐ `DependenciesController` — GET/POST/DELETE /cards/{id}/dependencies
- ☐ `SprintsController` — GET/POST/PUT/DELETE sprints, POST /sprints/{id}/start, POST /sprints/{id}/complete
- ☐ `TimeEntriesController` — GET/POST/PUT/DELETE time entries, POST /cards/{id}/timer/start, POST /cards/{id}/timer/stop
- ☐ Board export/import (JSON format)

**gRPC Service:**
- ☐ `tracks.proto` — Proto definition for board/card CRUD, queries
- ☐ `TracksGrpcService` — gRPC server implementation
- ☐ `TracksLifecycleService` — Module lifecycle (InitializeAsync/StartAsync/StopAsync)

**Cross-Module Integration:**
- ☐ File attachment links (reference FileNode from Files module via event subscription)
- ☐ Chat integration — Card activity events published to event bus, Chat can subscribe to show card updates in channels

---

### Phase 4.5 — Web UI (Blazor)

Board and card management interface.

**Deliverables:**
- ☐ **Board list page** — Grid/list view of all boards the user is a member of, create board dialog
- ☐ **Board view** — Full kanban board with drag-and-drop cards between lists, add list, list settings
- ☐ **Card detail panel** — Slide-out panel showing card details, description (Markdown editor), assignments, labels, checklists, comments, attachments, time entries, dependencies, activity log
- ☐ **Sprint management** — Sprint planning view, backlog → sprint drag, sprint burndown/progress
- ☐ **Board settings** — Members, labels, archive management, board delete
- ☐ **Filters & search** — Filter cards by label, assignee, due date, priority; search across boards
- ☐ **Real-time updates** — SignalR integration for live board state (card moves, new cards, comments)
- ☐ **Responsive layout** — Works on desktop and tablet; mobile-friendly card detail
- ☐ CSS styling consistent with existing DotNetCloud UI theme

---

### Phase 4.6 — Real-time & Notifications

Live updates and push notifications for board activity.

**Deliverables:**
- ☐ **SignalR Hub** — `TracksHub` for real-time board state sync (card moved, created, updated, deleted)
- ☐ **Notification integration** — Card assigned, due date approaching, mentioned in comment, sprint started/completed
- ☐ **Activity feed** — Per-board activity stream with real-time additions
- ☐ **@mention support** — Parse @username in card descriptions and comments, send notifications

---

### Phase 4.7 — Advanced Features

Board templates, automation, and analytics.

**Deliverables:**
- ☐ **Board templates** — Pre-built templates (Kanban, Scrum, Bug Tracking, Personal TODO), create board from template
- ☐ **Card templates** — Save card as template, create card from template
- ☐ **Due date reminders** — Background service dispatching reminders (like Calendar's ReminderDispatchService)
- ☐ **Board analytics** — Cards completed over time, average cycle time (list → list), time in each list, per-user workload
- ☐ **Sprint reports** — Velocity chart, burndown chart data (API endpoints returning chart-ready data)
- ☐ **Bulk operations** — Multi-select cards for move, label, assign, archive

---

### Phase 4.8 — Testing, Documentation & Release

Comprehensive testing and documentation.

**Deliverables:**
- ☐ Unit tests — Full coverage of services, authorization, dependency cycle detection
- ☐ Integration tests — REST API endpoint tests, gRPC service tests
- ☐ Security tests — Board role authorization, tenant isolation, Markdown XSS prevention
- ☐ Performance tests — Large board (1000+ cards) rendering, drag-and-drop reorder
- ☐ Admin documentation — Module configuration, storage, permissions
- ☐ User guide — Board management, card workflows, sprints, time tracking
- ☐ API documentation — All REST endpoints documented
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
