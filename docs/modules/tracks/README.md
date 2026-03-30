# DotNetCloud Tracks Module

> **Module ID:** `dotnetcloud.tracks`
> **Version:** 1.0.0
> **Status:** Implemented (Phase 4)
> **License:** AGPL-3.0

---

## Overview

The Tracks module provides project management and kanban boards for DotNetCloud organizations. It supports multi-board workspaces, drag-and-drop card management, sprint planning with velocity tracking, time tracking with timers, planning poker estimation, card dependencies, checklists, labels, comments, file attachments, bulk operations, board/card templates, team-based access control, analytics dashboards, and real-time collaboration via SignalR.

## Key Features

| Feature | Description |
|---|---|
| **Kanban Boards** | Multi-list boards with customizable columns, backgrounds, and visibility (public/private) |
| **Cards** | Rich cards with descriptions (Markdown), due dates, priority, assignees, and position tracking |
| **Lists** | Ordered columns within boards, reorderable, with card containment |
| **Sprints** | Time-boxed iterations with start/complete lifecycle, card assignment, and velocity tracking |
| **Comments** | Threaded comments on cards with Markdown support |
| **Checklists** | Multi-checklist support per card with toggleable items and progress tracking |
| **Labels** | Color-coded labels per board, assignable to cards for categorization |
| **Dependencies** | Card-to-card dependency tracking with cycle detection |
| **Time Tracking** | Manual time entries and start/stop timer per card |
| **Planning Poker** | Multi-round estimation sessions with vote submission, reveal, and acceptance |
| **Attachments** | File attachments on cards integrated with the Files module |
| **Bulk Operations** | Batch move, assign, label, and archive cards across lists |
| **Board Templates** | Create reusable board structures; instantiate boards from templates |
| **Card Templates** | Create reusable card structures from existing cards |
| **Teams** | Team-based board ownership with role hierarchy (Owner > Manager > Member > Guest) |
| **Analytics** | Board analytics, team analytics, sprint reports, and velocity charts |
| **Activity Feed** | Per-board and per-card activity logs with actor/action/target tracking |
| **Real-Time** | SignalR hub for live card moves, board updates, and poker sessions |
| **Notifications** | Event-driven notifications for assignments, comments, due dates, and mentions |
| **Import/Export** | Board export to JSON and import from external sources |

## Architecture

The Tracks module follows the DotNetCloud module architecture pattern with three projects:

```
src/Modules/Tracks/
├── DotNetCloud.Modules.Tracks/          # Core domain models, DTOs, interfaces, events
├── DotNetCloud.Modules.Tracks.Data/     # EF Core context, entity configs, service implementations
└── DotNetCloud.Modules.Tracks.Host/     # ASP.NET Core host: REST controllers, gRPC services, SignalR hub
```

### Module Manifest

The `TracksModuleManifest` declares:

- **Required Capabilities:** `INotificationService`, `IUserDirectory`, `ICurrentUserContext`, `IAuditLogger`, `IRealtimeBroadcaster`
- **Published Events:** `BoardCreatedEvent`, `BoardDeletedEvent`, `CardCreatedEvent`, `CardMovedEvent`, `CardUpdatedEvent`, `CardDeletedEvent`, `CardAssignedEvent`, `CardCommentAddedEvent`, `SprintStartedEvent`, `SprintCompletedEvent`, `PokerSessionStartedEvent`, `PokerSessionRevealedEvent`, `PokerSessionCompletedEvent`
- **Subscribed Events:** `FileDeletedEvent` (from Files module — for attachment cleanup)

### Data Flow

```
Client → REST API / gRPC → Service Layer → EF Core → Database
                                         → ITracksRealtimeService → SignalR (IRealtimeBroadcaster)
                                         → ITracksNotificationService → Core Notification System
                                         → IEventBus → Other Modules
```

## Project Structure

### DotNetCloud.Modules.Tracks (Core)

| Directory | Contents |
|---|---|
| `Models/` | Entity models (`Board`, `BoardList`, `Card`, `Sprint`, `Comment`, `Checklist`, `Label`, `Dependency`, `TimeEntry`, `PokerSession`, `Attachment`, `TeamRole`, etc.) |
| `DTOs/` | Data transfer objects for all API requests/responses |
| `Events/` | Domain events implementing `IEvent` (board, card, sprint, poker events) |
| `Services/` | Service interfaces (`IBoardService`, `ICardService`, `IListService`, etc.) |

### DotNetCloud.Modules.Tracks.Data (Data Access)

| Directory | Contents |
|---|---|
| `Configuration/` | EF Core `IEntityTypeConfiguration` for all 20+ entities |
| `Services/` | Service implementations (21 services: `BoardService`, `CardService`, `ListService`, `SprintService`, `CommentService`, `ChecklistService`, `LabelService`, `DependencyService`, `TimeTrackingService`, `PokerService`, `AttachmentService`, `ActivityService`, `TeamService`, `AnalyticsService`, `SprintReportService`, `BulkOperationService`, `BoardTemplateService`, `CardTemplateService`, `DueDateReminderService`, `TracksRealtimeService`, `TracksNotificationService`) |
| `Migrations/` | PostgreSQL and SQL Server migrations |

### DotNetCloud.Modules.Tracks.Host (Web Host)

| Directory | Contents |
|---|---|
| `Controllers/` | 15 REST API controllers (see API section below) |
| `Services/` | gRPC services (`TracksGrpcService`, `TracksLifecycleService`) |
| `Protos/` | Protobuf service definitions (11 RPCs) |

## Controllers

| Controller | Route | Endpoints | Description |
|---|---|---|---|
| `BoardsController` | `api/v1/boards` | 18 | Board CRUD, members, labels, activity, import/export |
| `ListsController` | `api/v1/boards/{boardId}/lists` | 5 | List CRUD and reorder |
| `CardsController` | `api/v1` | 11 | Card CRUD, move, assign, labels, activity |
| `SprintsController` | `api/v1/boards/{boardId}/sprints` | 9 | Sprint lifecycle, card assignment |
| `CommentsController` | `api/v1/cards/{cardId}/comments` | 4 | Comment CRUD |
| `ChecklistsController` | `api/v1/cards/{cardId}/checklists` | 6 | Checklist CRUD and item management |
| `DependenciesController` | `api/v1/cards/{cardId}/dependencies` | 3 | Dependency add/remove/list |
| `TimeEntriesController` | `api/v1/cards/{cardId}` | 5 | Time entries and start/stop timer |
| `AttachmentsController` | `api/v1/cards/{cardId}/attachments` | 3 | Attachment list/add/delete |
| `PokerController` | `api/v1` | 7 | Planning poker sessions, voting, reveal |
| `TeamsController` | `api/v1/teams` | 10 | Team CRUD, member management, team boards |
| `AnalyticsController` | `api/v1` | 4 | Board/team analytics, sprint reports, velocity |
| `BulkOperationsController` | `api/v1/boards/{boardId}/bulk` | 4 | Bulk move/assign/label/archive |
| `BoardTemplatesController` | `api/v1/tracks/board-templates` | 5 | Board template management |
| `CardTemplatesController` | `api/v1/boards/{boardId}/card-templates` | 4 | Card template management |
| | | **88 total** | |

## Authorization Model

Tracks uses a two-tier authorization model:

### Board Roles

| Role | Permissions |
|---|---|
| **Owner** | Full control: delete board, transfer ownership, manage all members |
| **Admin** | Manage members, lists, labels, sprints; edit all cards |
| **Member** | Create/edit own cards, add comments, manage checklists |
| **Viewer** | Read-only access to board, lists, and cards |

### Team Roles

| Role | Permissions |
|---|---|
| **Owner** | Full team control, manage all boards, promote managers |
| **Manager** | Create boards, manage team members (below manager level) |
| **Member** | Access team boards, create cards on assigned boards |
| **Guest** | Read-only access to team boards |

Board membership takes precedence over team membership. A user's effective board role is the higher of their direct board role and their mapped team role.

## Database Support

| Provider | Status |
|---|---|
| PostgreSQL | Supported (schema: `tracks.*`) |
| SQL Server | Supported (schema: `tracks.*`) |
| MariaDB | Pending (awaiting Pomelo .NET 10 support) |

## Module Lifecycle

The `TracksModule` class implements `IModuleLifecycle`:

| Phase | Action |
|---|---|
| **Initialize** | Resolve `IEventBus`, subscribe event handlers for board/card/sprint events |
| **Start** | Set running state, start due date reminder background service |
| **Stop** | Unsubscribe event handlers, stop background services |
| **Dispose** | Release resources |

## Enums

| Enum | Values | Description |
|---|---|---|
| `BoardMemberRole` | `Owner`, `Admin`, `Member`, `Viewer` | Board-level permission |
| `TracksTeamMemberRole` | `Owner`, `Manager`, `Member`, `Guest` | Team-level permission |
| `CardPriority` | `None`, `Low`, `Medium`, `High`, `Urgent` | Card urgency level |
| `SprintStatus` | `Planning`, `Active`, `Completed` | Sprint lifecycle state |
| `PokerSessionStatus` | `Voting`, `Revealed`, `Accepted` | Poker session state |
| `ActivityType` | `Created`, `Updated`, `Deleted`, `Moved`, `Assigned`, `Commented`, etc. | Activity log classification |
| `DependencyType` | `BlockedBy`, `Blocks`, `RelatedTo` | Card relationship type |

## gRPC Services

The Tracks module exposes 11 gRPC RPCs for inter-module communication:

| RPC | Description |
|---|---|
| `GetBoard` | Retrieve board by ID |
| `ListBoards` | List boards for the current user |
| `CreateBoard` | Create a new board |
| `CreateCard` | Create a card in a list |
| `MoveCard` | Move a card to a different list/position |
| `GetSprint` | Retrieve sprint by ID |
| `StartSprint` | Begin a sprint |
| `CompleteSprint` | Complete a sprint |
| `SubmitPokerVote` | Submit a planning poker vote |
| `CreateTimeEntry` | Log a time entry |
| `GetBoardAnalytics` | Retrieve board analytics |

## Related Documentation

- [REST API Reference](API.md)
- [User Guide](USER_GUIDE.md)

## Test Coverage

| Test Project | Tests | Description |
|---|---|---|
| `DotNetCloud.Modules.Tracks.Tests` | 344 | Unit, integration, security, and performance tests for all services, models, events, and controllers |
