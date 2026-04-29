# Tracks Module — Hierarchy Expansion Plan

> **Status:** Implemented — source code clean, UI + tests pending
> **Scope:** Rewrite Tracks module data model, services, API, and UI for multi-level project hierarchy
> **Phase 0 (Foundation)** — breaking changes acceptable

---

## 1. Summary

Replace the current flat **Board → Swimlane → Card** hierarchy with a six-level project management hierarchy:

```
Organization → Product → Epic → Feature → Item → Sub Item
```

Each level from Product through Feature has its own kanban board (swimlanes) containing the next level's work items as draggable cards. Sprints, planning poker, and review sessions move from Board scope to Epic scope. Each Product chooses whether its Items support child Sub Items or the current Checklist system.

The card rendering and card detail editor (KanbanBoard + CardDetailPanel) are preserved and adapted — these are the only pieces of the current UI that carry forward.

---

## 2. Architectural Decision: Unified WorkItem Model

After evaluating two options, the plan uses a **unified `WorkItem` entity with a type discriminator** rather than separate entity classes per hierarchy level.

### Option A (Chosen): Unified WorkItem

| Pros | Cons |
|------|------|
| One DTO and one component path for all levels | Lose DB-level FK enforcement for "Epic must be in a Product swimlane" |
| ~60% fewer entity/config/service files | App-level validation required |
| Hierarchy via simple `ParentWorkItemId` | Single table may grow large |
| Adding/removing levels in future is trivial |  |

### Option B (Rejected): Separate Entities per Level

Separate `Epic`, `Feature`, `Item`, `SubItem` classes, each with their own swimlane tables (`ProductSwimlane`, `EpicSwimlane`, `FeatureSwimlane`). Rejected because: ~30 entity tables vs ~15, triple the configuration code, three detail panel components instead of one, and no material benefit for a pre-release product.

### What Stays Separate

**Product** remains a distinct entity — it has unique properties (OrganizationId, SubItemsEnabled, owner membership, Label ownership) that don't fit a generic work item. **Swimlane** is a single unified table with a polymorphic container reference.

---

## 3. Target Data Model

### 3.1 New Entity Relationship Diagram

```
Core.Organization
    │
    └── Product (1:N)
            ├── ProductMember (1:N)
            ├── Label (1:N)
            ├── Activity (1:N)
            ├── Swimlane (1:N, ContainerType=Product)
            │       │
            │       └── WorkItem (Type=Epic) [1:N]
            │               ├── Swimlane (1:N, ContainerType=WorkItem)
            │               │       │
            │               │       └── WorkItem (Type=Feature) [1:N]
            │               │               ├── Swimlane (1:N, ContainerType=WorkItem)
            │               │               │       │
            │               │               │       └── WorkItem (Type=Item) [1:N]
            │               │               │               ├── WorkItem (Type=SubItem) [1:N, if Product.SubItemsEnabled]
            │               │               │               ├── Checklist (1:N, if !Product.SubItemsEnabled)
            │               │               │               ├── TimeEntry (1:N)
            │               │               │               ├── SprintItem (N:M → Sprint)
            │               │               │               └── PokerSession (1:N)
            │               │               └── (Feature shared sub-entities below)
            │               ├── Sprint (1:N)
            │               ├── ReviewSession (1:N)
            │               └── (Epic shared sub-entities below)
            └── (Product shared sub-entities)
```

### 3.2 Entity Definitions

#### Product
Replaces `Board` as the top-level Tracks container. Belongs to a Core Organization.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `OrganizationId` | `Guid` | FK to Core `Organization`. No DB-level FK (cross-module). |
| `Name` | `string` | Required, max 200 |
| `Description` | `string?` | Markdown, nullable |
| `Color` | `string?` | Hex color, max 20 |
| `OwnerId` | `Guid` | Creator user ID |
| `SubItemsEnabled` | `bool` | Default `false`. `true` = Items use SubItems; `false` = Items use Checklists |
| `IsArchived` | `bool` | |
| `IsDeleted` | `bool` | Soft-delete |
| `DeletedAt` | `DateTime?` | |
| `ETag` | `string` | Optimistic concurrency, max 64 |
| `CreatedAt` | `DateTime` | |
| `UpdatedAt` | `DateTime` | |

Navigation properties: `Swimlanes`, `Members`, `Labels`, `WorkItems` (all items in the product tree), `Activities`

#### ProductMember
Replaces `BoardMember`. Membership with role.

| Column | Type | Notes |
|--------|------|-------|
| `ProductId` | `Guid` | Composite PK part 1, FK → Product (cascade) |
| `UserId` | `Guid` | Composite PK part 2. Cross-module ref, no FK. |
| `Role` | `ProductMemberRole` | Enum stored as string: Viewer, Member, Admin, Owner |
| `JoinedAt` | `DateTime` | |

#### Swimlane
Unified kanban column. Replaces `BoardSwimlane`.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `ContainerType` | `SwimlaneContainerType` | Enum stored as string: `Product`, `WorkItem` |
| `ContainerId` | `Guid` | Product.Id for Product-level; WorkItem.Id for Epic/Feature-level |
| `Title` | `string` | Required, max 200 |
| `Color` | `string?` | Hex color, max 20 |
| `Position` | `double` | Gap-based ordering |
| `CardLimit` | `int?` | WIP limit |
| `IsDone` | `bool` | Items in this swimlane count as "done" for sprint tracking |
| `IsArchived` | `bool` | |
| `CreatedAt` | `DateTime` | |
| `UpdatedAt` | `DateTime` | |

Navigation: `WorkItems` (items currently in this swimlane)

**App-level constraint:** When `ContainerType=Product`, `ContainerId` has FK to `Product.Id`. When `ContainerType=WorkItem`, the WorkItem must have `Type ∈ {Epic, Feature}`.

#### WorkItem
Unified work item. Replaces `Card` and adds Epic/Feature/SubItem levels.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `ProductId` | `Guid` | FK → Product (cascade). Root product — always set. |
| `ParentWorkItemId` | `Guid?` | Self-referencing FK (restrict). `null` for Epics, set for Features→Epic, Items→Feature, SubItems→Item. |
| `Type` | `WorkItemType` | Enum stored as string: `Epic`, `Feature`, `Item`, `SubItem` |
| `SwimlaneId` | `Guid?` | FK → Swimlane (set null on swimlane delete). Current kanban column. |
| `ItemNumber` | `int` | Sequential number, scoped to Product (unique per product, not per-type) |
| `Title` | `string` | Required, max 500 |
| `Description` | `string?` | Markdown |
| `Position` | `double` | Gap-based ordering within swimlane |
| `Priority` | `Priority` | Enum stored as string: None, Low, Medium, High, Urgent |
| `DueDate` | `DateTime?` | UTC |
| `StoryPoints` | `int?` | Fibonacci estimate |
| `IsArchived` | `bool` | |
| `IsDeleted` | `bool` | Soft-delete |
| `DeletedAt` | `DateTime?` | |
| `CreatedByUserId` | `Guid` | Creator |
| `ETag` | `string` | Optimistic concurrency, max 64 |
| `CreatedAt` | `DateTime` | |
| `UpdatedAt` | `DateTime` | |

Navigation: `Product`, `ParentWorkItem`, `ChildWorkItems`, `Swimlane`, `Assignments`, `WorkItemLabels`, `Comments`, `Attachments`, `Dependencies`, `Dependents`, `TimeEntries`, `SprintItems`, `Checklists`, `PokerSessions`

**Constraints:**
- `Type=Epic` ⇒ `ParentWorkItemId` is null (direct child of Product), lives in Product-level Swimlane
- `Type=Feature` ⇒ `ParentWorkItemId` points to an Epic, lives in an Epic-level Swimlane
- `Type=Item` ⇒ `ParentWorkItemId` points to a Feature, lives in a Feature-level Swimlane
- `Type=SubItem` ⇒ `ParentWorkItemId` points to an Item, no SwimlaneId
- `Type=SubItem` only allowed when `Product.SubItemsEnabled = true`

#### WorkItemAssignment
Unified assignment table. Replaces `CardAssignment`.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `WorkItemId` | `Guid` | FK → WorkItem (cascade) |
| `UserId` | `Guid` | Cross-module ref |
| `AssignedAt` | `DateTime` | |

Unique index on `(WorkItemId, UserId)`.

#### WorkItemLabel
Unified many-to-many join. Replaces `CardLabel`.

| Column | Type | Notes |
|--------|------|-------|
| `WorkItemId` | `Guid` | Composite PK part 1, FK → WorkItem (cascade) |
| `LabelId` | `Guid` | Composite PK part 2, FK → Label (cascade) |
| `AppliedAt` | `DateTime` | |

#### WorkItemComment
Unified comments. Replaces `CardComment`.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `WorkItemId` | `Guid` | FK → WorkItem (cascade) |
| `UserId` | `Guid` | Cross-module ref |
| `Content` | `string` | Markdown |
| `IsEdited` | `bool` | |
| `IsDeleted` | `bool` | Soft-delete |
| `DeletedAt` | `DateTime?` | |
| `CreatedAt` | `DateTime` | |
| `UpdatedAt` | `DateTime` | |

#### WorkItemAttachment
Unified attachments. Replaces `CardAttachment`.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `WorkItemId` | `Guid` | FK → WorkItem (cascade) |
| `FileNodeId` | `Guid?` | Cross-module ref to Files module |
| `Url` | `string?` | External URL, max 2000 |
| `FileName` | `string` | Max 500 |
| `FileSize` | `long?` | Bytes |
| `MimeType` | `string?` | Max 255 |
| `UploadedByUserId` | `Guid` | |
| `CreatedAt` | `DateTime` | |

#### WorkItemDependency
Unified dependencies. Replaces `CardDependency`.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `WorkItemId` | `Guid` | FK → WorkItem (cascade) — the dependent item |
| `DependsOnWorkItemId` | `Guid` | FK → WorkItem (restrict) — the blocker |
| `Type` | `DependencyType` | Enum stored as string: BlockedBy, RelatesTo |
| `CreatedAt` | `DateTime` | |

Unique index on `(WorkItemId, DependsOnWorkItemId, Type)`.

#### Label
Updated FK from Board → Product.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `ProductId` | `Guid` | FK → Product (cascade) |
| `Title` | `string` | Max 100 |
| `Color` | `string` | Max 20 |
| `CreatedAt` | `DateTime` | |

Unique index on `(ProductId, Title)`.

#### Checklist
Updated FK from Card → WorkItem (Item type only). Only used when `Product.SubItemsEnabled = false`.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `ItemId` | `Guid` | FK → WorkItem (cascade) |
| `Title` | `string` | Max 200 |
| `Position` | `double` | |
| `CreatedAt` | `DateTime` | |

#### ChecklistItem
Unchanged structurally.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `ChecklistId` | `Guid` | FK → Checklist (cascade) |
| `Title` | `string` | Max 500 |
| `IsCompleted` | `bool` | |
| `Position` | `double` | |
| `AssignedToUserId` | `Guid?` | |
| `CreatedAt` | `DateTime` | |
| `UpdatedAt` | `DateTime` | |

#### Sprint
Reparented from Board → Epic (WorkItem with Type=Epic).

| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `EpicId` | `Guid` | FK → WorkItem (cascade) |
| `Title` | `string` | Max 200 |
| `Goal` | `string?` | Markdown |
| `StartDate` | `DateTime?` | |
| `EndDate` | `DateTime?` | |
| `Status` | `SprintStatus` | Enum: Planning, Active, Completed |
| `TargetStoryPoints` | `int?` | |
| `DurationWeeks` | `int?` | |
| `PlannedOrder` | `int?` | |
| `CreatedAt` | `DateTime` | |
| `UpdatedAt` | `DateTime` | |

#### SprintItem
Replaces `SprintCard`. Only Items (leaf WorkItems) can be sprint members.

| Column | Type | Notes |
|--------|------|-------|
| `SprintId` | `Guid` | Composite PK part 1, FK → Sprint (cascade) |
| `ItemId` | `Guid` | Composite PK part 2, FK → WorkItem (cascade) |
| `AddedAt` | `DateTime` | |

#### PokerSession
Reparented from Board → Epic.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `EpicId` | `Guid` | FK → WorkItem (restrict) — the owning Epic |
| `ItemId` | `Guid` | FK → WorkItem (cascade) — the Item being estimated |
| `CreatedByUserId` | `Guid` | |
| `Scale` | `PokerScale` | Fibonacci, TShirt, PowersOfTwo, Custom |
| `CustomScaleValues` | `string?` | |
| `Status` | `PokerStatus` | |
| `AcceptedEstimate` | `string?` | |
| `Round` | `int` | Default 1 |
| `ReviewSessionId` | `Guid?` | FK → ReviewSession (set null) |
| `CreatedAt` | `DateTime` | |
| `UpdatedAt` | `DateTime` | |

#### PokerVote
Unchanged.

#### ReviewSession
Reparented from Board → Epic.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `EpicId` | `Guid` | FK → WorkItem (cascade) |
| `HostUserId` | `Guid` | |
| `CurrentItemId` | `Guid?` | FK → WorkItem (set null) |
| `Status` | `ReviewStatus` | Active, Paused, Ended |
| `CreatedAt` | `DateTime` | |
| `EndedAt` | `DateTime?` | |

#### ReviewSessionParticipant
Unchanged.

#### Activity
Reparented from Board → Product.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `ProductId` | `Guid` | FK → Product (cascade) |
| `UserId` | `Guid` | |
| `Action` | `string` | e.g. "workitem.created", "sprint.started" |
| `EntityType` | `string` | e.g. "WorkItem", "Sprint", "Swimlane" |
| `EntityId` | `Guid` | |
| `Details` | `string?` | JSON |
| `CreatedAt` | `DateTime` | |

#### ProductTemplate
Replaces `BoardTemplate`. FK shifted from Board → Product.

#### ItemTemplate
Replaces `CardTemplate`. FK shifted.

#### TeamRole
Minor rename: `CoreTeamId` → `TeamId`. Clarifies it references a Core Team.

### 3.3 Entities to Delete

All existing entities except infrastructure (DbContext, design-time factory): `Board`, `BoardMember`, `BoardSwimlane`, `Card`, `CardAssignment`, `CardLabel`, `CardComment`, `CardAttachment`, `CardDependency`, `CardChecklist`, `ChecklistItem` (replaced), `SprintCard` (replaced by SprintItem), `BoardActivity` (replaced by Activity).

### 3.4 Enums

```csharp
public enum WorkItemType { Epic, Feature, Item, SubItem }
public enum SwimlaneContainerType { Product, WorkItem }
public enum ProductMemberRole { Viewer, Member, Admin, Owner }
// Retained: CardPriority → Priority, DependencyType → DependencyType
// Retained: SprintStatus, PokerScale, PokerStatus, ReviewStatus
// Deleted: BoardMode (Personal/Team — Products are always team-scoped to an Org)
// Deleted: BoardMemberRole (replaced by ProductMemberRole)
```

---

## 4. Migration Strategy

### 4.1 Approach

Since this is Phase 0 (pre-release, no production data), use a **single breaking migration**:

1. Delete all files in `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Migrations/`
2. Delete `TracksDbContextModelSnapshot.cs`
3. Rewrite `TracksDbContext.cs` with new `DbSet<T>` properties
4. Write all new EF Core entity configuration classes
5. Run `dotnet ef migrations add InitialCreate` to generate a fresh migration
6. Update `TracksDbInitializer.cs` to seed:
   - Default swimlanes for new Products: "To Do" (position 1000), "In Progress" (position 2000), "Done" (position 3000, IsDone=true)

### 4.2 Non-Breaking

`CoreDbContext` is unaffected — Organization, Team, and User entities remain as-is. The Tracks module references Core Organization by Guid with no FK enforcement.

---

## 5. DTOs and Events

### 5.1 Key DTOs

All DTOs in `src/Core/DotNetCloud.Core/DTOs/TracksDtos.cs`.

**ProductDto:**
```csharp
public sealed record ProductDto(
    Guid Id, Guid OrganizationId, string Name, string? Description, string? Color,
    Guid OwnerId, bool SubItemsEnabled, bool IsArchived,
    int SwimlaneCount, int EpicCount, int MemberCount, int LabelCount,
    string ETag, DateTime CreatedAt, DateTime UpdatedAt,
    List<LabelDto> Labels, List<ProductMemberDto> Members
);
```

**WorkItemDto** (serves Epic, Feature, Item, SubItem):
```csharp
public sealed record WorkItemDto(
    Guid Id, Guid ProductId, Guid? ParentWorkItemId, WorkItemType Type,
    Guid? SwimlaneId, string? SwimlaneTitle,
    int ItemNumber, string Title, string? Description, double Position,
    Priority Priority, DateTime? DueDate, int? StoryPoints,
    bool IsArchived, int CommentCount, int AttachmentCount,
    List<WorkItemAssignmentDto> Assignments, List<LabelDto> Labels,
    // Conditional — populated based on Type and Product.SubItemsEnabled
    List<WorkItemDto>? ChildWorkItems,      // Features (for Epic), Items (for Feature), SubItems (for Item when SubItemsEnabled)
    List<ChecklistDto>? Checklists,          // Only for Items when !SubItemsEnabled
    int? SprintId, string? SprintTitle,      // Only for Items
    int? TotalTrackedMinutes,                // Only for Items
    string ETag, DateTime CreatedAt, DateTime UpdatedAt
);
```

**SwimlaneDto:**
```csharp
public sealed record SwimlaneDto(
    Guid Id, SwimlaneContainerType ContainerType, Guid ContainerId,
    string Title, string? Color, double Position, int? CardLimit,
    bool IsDone, bool IsArchived, int CardCount,
    DateTime CreatedAt, DateTime UpdatedAt
);
```

### 5.2 Events

New events in `src/Core/DotNetCloud.Core/Events/TracksEvents.cs`:

| Event | Payload |
|-------|---------|
| `ProductCreatedEvent` | ProductId, OrganizationId, OwnerId |
| `ProductDeletedEvent` | ProductId |
| `WorkItemCreatedEvent` | WorkItemId, ProductId, Type, ParentWorkItemId? |
| `WorkItemUpdatedEvent` | WorkItemId, Type |
| `WorkItemMovedEvent` | WorkItemId, Type, FromSwimlaneId, ToSwimlaneId |
| `WorkItemDeletedEvent` | WorkItemId, Type |
| `WorkItemAssignedEvent` | WorkItemId, UserId |
| `WorkItemCommentAddedEvent` | WorkItemId, CommentId, UserId |
| `SprintStartedEvent` | SprintId, EpicId |
| `SprintCompletedEvent` | SprintId, EpicId |
| `PokerSessionStartedEvent` | SessionId, EpicId, ItemId |
| `PokerSessionRevealedEvent` | SessionId, EpicId |
| `PokerSessionCompletedEvent` | SessionId, EpicId |

Delete all old `Board`/`Card`-prefixed events.

---

## 6. Service Layer

All services in `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/`.

| Service | Replaces | Key Methods |
|---------|----------|-------------|
| `ProductService` | BoardService, LabelService, BoardMemberService | Create, Get, Update, Delete, ListByOrganization, AddMember, RemoveMember, UpdateMemberRole, CreateLabel, DeleteLabel |
| `SwimlaneService` | SwimlaneService (old) | Create, Get, Update, Delete, Reorder — parameterized by ContainerType + ContainerId |
| `WorkItemService` | CardService, BulkOperationService | Create, Get, GetByNumber, Update, Delete, Move, Assign, AddLabel, RemoveLabel, ListBySwimlane, ListByParent |
| `CommentService` | CommentService (old) | Create, Update, Delete, ListByWorkItem |
| `AttachmentService` | AttachmentService (old) | Add, Remove, ListByWorkItem |
| `DependencyService` | DependencyService (old) | Add, Remove, ListByWorkItem |
| `ChecklistService` | ChecklistService (old) | Create, Delete, AddItem, ToggleItem, DeleteItem, ListByItem |
| `SprintService` | SprintService, SprintPlanningService, SprintReportService | Create, Update, Delete, Start, Complete, AddItem, RemoveItem, ListByEpic, GetBacklog, GetVelocity |
| `PokerService` | PokerService (old) | StartSession, SubmitVote, Reveal, AcceptEstimate, NewRound |
| `ReviewSessionService` | ReviewSessionService (old) | Start, Join, Leave, SetCurrentItem, End |
| `TimeTrackingService` | TimeTrackingService (old) | StartTimer, StopTimer, AddEntry, DeleteEntry, GetTotalForItem |
| `ActivityService` | ActivityService (old) | ListByProduct, ListByWorkItem |
| `AnalyticsService` | AnalyticsService (old) | GetProductAnalytics, GetSprintReport, GetBurndown |

### App-Level Validation in WorkItemService

- Creating `Type=Epic`: set `ParentWorkItemId=null`, validate SwimlaneId belongs to a Product-level swimlane
- Creating `Type=Feature`: validate ParentWorkItem exists and has Type=Epic, validate SwimlaneId belongs to that Epic
- Creating `Type=Item`: validate ParentWorkItem exists and has Type=Feature, validate SwimlaneId belongs to that Feature
- Creating `Type=SubItem`: validate ParentWorkItem exists and has Type=Item, validate Product.SubItemsEnabled=true
- Move: validate target swimlane matches the item's level

### Item Number Generation

Item numbers are unique per Product (shared across all WorkItem types under a Product). A `NextItemNumber` counter on Product is incremented atomically on creation. Display format: `#N` (e.g., `#42`). The Type is shown via UI context, not in the number.

---

## 7. API Layer

All controllers in `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/`.

### 7.1 REST Endpoints

```
# Products
GET    /api/v1/organizations/{orgId}/products          # List products for org
POST   /api/v1/organizations/{orgId}/products          # Create product
GET    /api/v1/products/{productId}                    # Get product
PUT    /api/v1/products/{productId}                    # Update product
DELETE /api/v1/products/{productId}                    # Soft-delete product

# Product Members
GET    /api/v1/products/{productId}/members            # List members
POST   /api/v1/products/{productId}/members            # Add member
DELETE /api/v1/products/{productId}/members/{userId}   # Remove member
PUT    /api/v1/products/{productId}/members/{userId}/role  # Update role

# Labels
GET    /api/v1/products/{productId}/labels             # List labels
POST   /api/v1/products/{productId}/labels             # Create label
PUT    /api/v1/products/{productId}/labels/{labelId}   # Update label
DELETE /api/v1/products/{productId}/labels/{labelId}   # Delete label

# Swimlanes (unified — ContainerType + ContainerId in route or body)
GET    /api/v1/products/{productId}/swimlanes          # Product-level swimlanes
POST   /api/v1/products/{productId}/swimlanes          # Create product swimlane
GET    /api/v1/workitems/{workItemId}/swimlanes         # Epic/Feature-level swimlanes
POST   /api/v1/workitems/{workItemId}/swimlanes         # Create Epic/Feature swimlane
PUT    /api/v1/swimlanes/{swimlaneId}                  # Update swimlane
DELETE /api/v1/swimlanes/{swimlaneId}                  # Delete swimlane
PUT    /api/v1/swimlanes/reorder                       # Reorder swimlanes

# Work Items
GET    /api/v1/swimlanes/{swimlaneId}/items            # List items in swimlane
POST   /api/v1/swimlanes/{swimlaneId}/items            # Create item in swimlane
GET    /api/v1/workitems/{workItemId}                  # Get work item
PUT    /api/v1/workitems/{workItemId}                  # Update work item
DELETE /api/v1/workitems/{workItemId}                  # Soft-delete
GET    /api/v1/workitems/by-number/{productId}/{itemNumber}  # Get by number
PUT    /api/v1/workitems/{workItemId}/move             # Move between swimlanes

# Work Item — Sub-resources
GET    /api/v1/workitems/{workItemId}/assignments      # List assignments
POST   /api/v1/workitems/{workItemId}/assignments      # Assign user
DELETE /api/v1/workitems/{workItemId}/assignments/{userId}  # Unassign
POST   /api/v1/workitems/{workItemId}/labels/{labelId}      # Add label
DELETE /api/v1/workitems/{workItemId}/labels/{labelId}      # Remove label
GET    /api/v1/workitems/{workItemId}/comments          # List comments
POST   /api/v1/workitems/{workItemId}/comments          # Add comment
PUT    /api/v1/workitems/{workItemId}/comments/{id}     # Edit comment
DELETE /api/v1/workitems/{workItemId}/comments/{id}     # Delete comment
GET    /api/v1/workitems/{workItemId}/attachments       # List attachments
POST   /api/v1/workitems/{workItemId}/attachments       # Upload attachment
DELETE /api/v1/workitems/{workItemId}/attachments/{id}  # Remove attachment
GET    /api/v1/workitems/{workItemId}/dependencies      # List dependencies
POST   /api/v1/workitems/{workItemId}/dependencies      # Add dependency
DELETE /api/v1/workitems/{workItemId}/dependencies/{depId}  # Remove dependency

# Checklists (only for Items where Product.SubItemsEnabled=false)
GET    /api/v1/workitems/{itemId}/checklists            # List checklists
POST   /api/v1/workitems/{itemId}/checklists            # Create checklist
DELETE /api/v1/workitems/{itemId}/checklists/{id}       # Delete checklist
POST   /api/v1/workitems/{itemId}/checklists/{id}/items # Add item
PUT    /api/v1/workitems/{itemId}/checklists/{id}/items/{itemId}/toggle  # Toggle
DELETE /api/v1/workitems/{itemId}/checklists/{id}/items/{itemId}  # Delete item

# Time Tracking (Items only)
GET    /api/v1/workitems/{itemId}/time-entries          # List entries
POST   /api/v1/workitems/{itemId}/time-entries          # Add manual entry
DELETE /api/v1/workitems/{itemId}/time-entries/{id}     # Delete entry
POST   /api/v1/workitems/{itemId}/timer/start           # Start timer
POST   /api/v1/workitems/{itemId}/timer/stop            # Stop timer

# Sprints (under Epic)
GET    /api/v1/workitems/{epicId}/sprints               # List sprints
POST   /api/v1/workitems/{epicId}/sprints               # Create sprint
GET    /api/v1/sprints/{sprintId}                       # Get sprint
PUT    /api/v1/sprints/{sprintId}                       # Update sprint
DELETE /api/v1/sprints/{sprintId}                       # Delete sprint
POST   /api/v1/sprints/{sprintId}/start                 # Start sprint
POST   /api/v1/sprints/{sprintId}/complete              # Complete sprint
POST   /api/v1/sprints/{sprintId}/items/{itemId}        # Add item to sprint
DELETE /api/v1/sprints/{sprintId}/items/{itemId}        # Remove item from sprint
GET    /api/v1/workitems/{epicId}/backlog               # Get backlog (items not in any sprint)

# Poker (under Epic)
POST   /api/v1/workitems/{epicId}/poker                 # Start poker session
GET    /api/v1/poker/{sessionId}                        # Get session
POST   /api/v1/poker/{sessionId}/vote                   # Submit vote
POST   /api/v1/poker/{sessionId}/reveal                 # Reveal votes
POST   /api/v1/poker/{sessionId}/accept                 # Accept estimate

# Review Sessions (under Epic)
POST   /api/v1/workitems/{epicId}/reviews               # Start review
GET    /api/v1/reviews/{sessionId}                      # Get session
POST   /api/v1/reviews/{sessionId}/join                 # Join
POST   /api/v1/reviews/{sessionId}/leave                # Leave
PUT    /api/v1/reviews/{sessionId}/current-item         # Navigate
POST   /api/v1/reviews/{sessionId}/end                  # End

# Activity
GET    /api/v1/products/{productId}/activity            # Product activity feed
GET    /api/v1/workitems/{workItemId}/activity          # Work item activity

# Analytics
GET    /api/v1/products/{productId}/analytics           # Product analytics
GET    /api/v1/products/{productId}/velocity            # Velocity chart data

# Teams
GET    /api/v1/teams                                    # List teams
POST   /api/v1/teams                                    # Create team
GET    /api/v1/teams/{teamId}                           # Get team
PUT    /api/v1/teams/{teamId}                           # Update team
DELETE /api/v1/teams/{teamId}                           # Delete team
GET    /api/v1/teams/{teamId}/members                   # List members
POST   /api/v1/teams/{teamId}/members                   # Add member
DELETE /api/v1/teams/{teamId}/members/{userId}          # Remove member
PUT    /api/v1/teams/{teamId}/members/{userId}/role     # Update role
```

### 7.2 Controllers to Create

`ProductsController`, `SwimlanesController`, `WorkItemsController`, `CommentsController`, `AttachmentsController`, `DependenciesController`, `ChecklistsController`, `TimeEntriesController`, `SprintsController`, `PokerController`, `ReviewSessionsController`, `ActivityController`, `AnalyticsController`, `TeamsController`

### 7.3 Controllers to Delete

`BoardsController`, `CardsController`, `BoardTemplatesController`, `CardTemplatesController`, `BulkOperationsController`, `BoardSwimlanesController` (replaced by `SwimlanesController`)

---

## 8. UI Adaptation

All components in `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/`.

### 8.1 Preserved Templates

The card template from `KanbanBoard.razor` (lines 112–218) and the detail panel layout from `CardDetailPanel.razor` are preserved. These are the visual elements the user wants to keep.

### 8.2 Components to Adapt

**KanbanBoard.razor** — Parameterized to work at any level:
- Receives: `ContainerType` + `ContainerId`, `Swimlanes` (List<SwimlaneDto>), `WorkItemsBySwimlane` (Dictionary<Guid, List<WorkItemDto>>), `OnItemSelected`, `OnItemCreated`, `OnItemMoved` callbacks
- Card rendering is identical — `WorkItemDto` carries the same display fields as the old `CardDto`
- Swimlane header shows swimlane title, card count, WIP limit

**WorkItemDetailPanel.razor** (adapted from `CardDetailPanel.razor`):
- **Always shown** (all levels): Inline-editable title with item number, Labels (color pills), Description (Markdown editor), Comments, Activity log, Priority selector, Due Date, Story Points, Assignees, Labels picker, Attachments, Dependencies, Archive/Delete
- **Epic only** (Type=Epic): Sprint list + create, Poker sessions, Review sessions, "Open Kanban" button (navigates to Epic's Feature kanban), list of child Features
- **Feature only** (Type=Feature): "Open Kanban" button (navigates to Feature's Item kanban), list of child Items
- **Item only** (Type=Item): Checklists (when SubItemsEnabled=false) OR SubItems list (when SubItemsEnabled=true), Time Tracking (timer + logged time), Sprint assignment dropdown

### 8.3 Components to Rewrite

**TracksPage.razor** — New navigation architecture:
1. **Header**: Organization selector dropdown (loads from Core `IOrganizationDirectory`) + breadcrumb trail
2. **Breadcrumb**: `Org Name > Product Name > Epic #N > Feature #N > Item #N`
3. **Views**: Product List (grid of products) → Product Kanban (Epics in swimlanes) → Epic Kanban (Features in swimlanes) → Feature Kanban (Items in swimlanes) → Item Detail (slide-out panel)
4. **Epic-level sub-views**: Sprint Planning, Backlog, Timeline, Review Session (these only appear when viewing an Epic)
5. Navigation is drill-down: clicking an Epic card opens the Epic kanban, clicking a Feature card opens the Feature kanban, clicking an Item card opens the detail panel

**ProductListView.razor** (replaces BoardListView) — Grid of product cards. Search, create product dialog, org context.

**SprintPanel.razor** (rewritten for Epic scope) — Sprint list, create/edit, start/complete, backlog drag-to-sprint

**SprintPlanningView.razor**, **SprintPlanningWizard.razor**, **SprintBurndownChart.razor**, **SprintCompletionDialog.razor** — Updated to accept EpicId instead of BoardId

**TimelineView.razor** — Updated for Epic-level sprint timeline

**ReviewSessionHost.razor**, **ReviewSessionParticipant.razor** — Updated for Epic scope

**VelocityChart.razor** — Updated

**WorkItemFullscreenPage.razor** (replaces CardFullscreenPage) — Route `/apps/tracks/item/{productId}/{itemNumber}`. Same pattern: load item, render WorkItemDetailPanel.

### 8.4 Components to Delete

`BoardListView.razor`, `BoardSettingsDialog.razor`, `BacklogView.razor` (logic merged into SprintPanel), `TeamManagement.razor` (replaced)

### 8.5 Shell Pages

- `src/UI/DotNetCloud.UI.Web/Components/Pages/Modules/Tracks.razor` — update `ModuleUiRegistry` key if needed (likely unchanged)
- `src/UI/DotNetCloud.UI.Web/Components/Pages/Modules/TracksCard.razor` — update route to `/apps/tracks/item/{ProductId:guid}/{ItemNumber:int}`

---

## 9. gRPC

Update `Protos/tracks_service.proto`:
- Replace `BoardMessage` with `ProductMessage`
- Replace `CardMessage` with `WorkItemMessage` (with `type` field)
- Add `SwimlaneMessage` (with `container_type`, `container_id`)
- Update RPC signatures accordingly

Regenerate `TracksGrpcService.cs`.

---

## 10. Cross-Module Updates

### 10.1 ITracksDirectory

Add methods for new entity lookups:
```csharp
Task<string?> GetProductTitleAsync(Guid productId, CancellationToken ct);
Task<string?> GetWorkItemTitleAsync(Guid workItemId, CancellationToken ct);
```

### 10.2 Event Handlers

In `TracksModule.cs`:
- Subscribe to new event names
- `FileDeletedEventHandler` — resolve to ItemId (was CardId)
- `ChatMessageTracksHandler` — update entity references

### 10.3 Manifest

Update `TracksModuleManifest.cs`:
- Published events: replace old names with new ones
- Subscribed events: unchanged (FileDeleted, MessageSent, ChannelCreated, ChannelDeleted)

### 10.4 Service Registration

Update `TracksServiceRegistration.cs`:
- Replace old service registrations with new ones
- SignalR service updated for new event types
- Null-object patterns updated for new interfaces

---

## 11. Implementation Order

| Step | Area | Description | Files Affected |
|------|------|-------------|----------------|
| 1 | Models | Delete all old models, create new ones | `Models/` — ~18 files deleted, ~20 files created |
| 2 | Config | Delete old EF configs, create new ones | `Data/Configuration/` — ~22 files deleted, ~20 files created |
| 3 | DbContext | Rewrite TracksDbContext, delete old migrations, generate InitialCreate | `TracksDbContext.cs`, `Migrations/`, `TracksDbInitializer.cs` |
| 4 | DTOs | Rewrite TracksDtos.cs | `src/Core/DotNetCloud.Core/DTOs/TracksDtos.cs` |
| 5 | Events | Rewrite TracksEvents.cs | `src/Core/DotNetCloud.Core/Events/TracksEvents.cs` |
| 6 | Services | Implement all new services | `Data/Services/` — ~16 files |
| 7 | Controllers | Implement all new controllers | `Host/Controllers/` — ~14 files |
| 8 | API Client | Rewrite ITracksApiClient + TracksApiClient | `Services/ITracksApiClient.cs`, `Services/TracksApiClient.cs` |
| 9 | UI — Core | Adapt KanbanBoard + create WorkItemDetailPanel | `UI/KanbanBoard.razor*`, `UI/WorkItemDetailPanel.razor*` |
| 10 | UI — Pages | Rewrite TracksPage + ProductListView + WorkItemFullscreenPage | `UI/TracksPage.razor*`, `UI/ProductListView.razor*`, `UI/WorkItemFullscreenPage.razor*` |
| 11 | UI — Epic | Rewrite sprint/planning/review components for Epic scope | `UI/SprintPanel.razor*`, `UI/SprintPlanningView.razor*`, etc. |
| 12 | Cross-module | Update TracksModule, manifest, SignalR, gRPC, service registration | Multiple files |
| 13 | Docs | Update IMPLEMENTATION_CHECKLIST.md + MASTER_PROJECT_PLAN.md | `docs/` — 2 files |
| 14 | Build | `dotnet build -c Release` with CI solution filter | Verify no compilation errors |

Steps 1–3 (data layer) must be sequential. Steps 4–5 can run in parallel. Steps 6–8 depend on 1–5. Steps 9–11 depend on 8. Step 12 depends on 6–8. Steps 13–14 final.

---

## 12. Open Decisions

The following decisions are deferred until implementation reaches the relevant step. They don't block the plan:

1. **Card numbering scheme**: Sequential per Product (all types share one counter) vs. per-Product per-Type (EPIC-1, FEAT-1, ITEM-1 separately). Current plan uses shared counter for simplicity; can revisit during Step 2.
2. **Organization selector in UI**: Whether to show all orgs the user belongs to, or persist a "last selected" org. Can be decided during UI implementation.
3. **Cross-type dependencies**: Whether an Item can be blocked by an Epic. Current plan supports same-type only (matching existing behavior). The unified `WorkItemDependency` table can support cross-type if `DependsOnWorkItemId` FK is loose — just change app-level validation later.
4. **Default swimlane creation**: Whether Epic/Feature containers auto-create default swimlanes on creation, or start empty. Current plan starts empty; user creates swimlanes manually.

---

## 13. Verification

### Build
```bash
dotnet build -c Release
```
Must succeed with zero warnings (TreatWarningsAsErrors).

### Database
```bash
dotnet ef migrations add InitialCreate --project src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data --context TracksDbContext
```
Verify the generated migration SQL is valid and creates all expected tables and indexes.

### Manual UI Smoke Test
1. Create an Organization (via Core admin endpoint or seed data)
2. Create a Product under that Organization → verify it appears in product list
3. Add swimlanes to Product → verify they render as columns
4. Create an Epic in a Product swimlane → verify card appears with correct number, priority, labels
5. Click Epic → verify detail panel opens with all sections
6. Drag Epic between swimlanes → verify position updates
7. Open Epic kanban → add swimlanes → create Feature → verify Feature card
8. Open Feature kanban → add swimlanes → create Item → verify Item card
9. Open Item detail → verify: checklists (SubItemsEnabled=false), sub-items (SubItemsEnabled=true), time tracking, sprint assignment
10. Create Sprint on Epic → add Items → start/complete sprint → verify burndown
11. Planning Poker on an Epic Item → start, vote, reveal, accept
12. Review Session on an Epic → start, join, navigate cards
