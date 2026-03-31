# Sprint Planning & Workflow Enhancement Plan

**Status:** Proposed  
**Priority:** Critical — current sprint implementation has backend plumbing but no usable UX  
**Created:** 2026-03-31  

---

## Problem Statement

The Tracks module has a fully-functional sprint lifecycle backend (CRUD, state transitions, card association APIs, events, activity logging) but the **user experience is broken**:

1. **No way to add cards to a sprint** — The API supports `AddCardToSprintAsync`/`RemoveCardFromSprintAsync` but there is zero UI to invoke it. Not in the card detail panel, not on the kanban board, not in the sprint panel.
2. **No sprint backlog view** — You create a sprint, but can't see what's in it. No card list, no table, nothing.
3. **No sprint-scoped board** — The kanban board shows all cards regardless of sprint. No way to focus on "just this sprint's work."
4. **No integration between planning poker and sprints** — Poker and sprints are completely separate features. You can't estimate cards in sprint context.
5. **No sprint planning ceremony support** — No review mode, no capacity planning, no commitment workflow.
6. **Cards don't know about sprints** — No sprint badge on kanban cards, no sprint field in card detail sidebar.

**Result:** Sprints exist as metadata-only shells. The entire point of sprints — selecting work, estimating, committing, focusing — is impossible through the UI.

---

## Competitive Research Summary

### How other tools handle sprint planning:

| Feature | Jira | Azure DevOps | Linear (Cycles) | GitHub Projects |
|---------|------|-------------|-----------------|-----------------|
| **Add cards to sprint** | Drag from backlog to sprint in planning view | Drag from product backlog to sprint panel | Assign iteration field on issue, or drag into cycle | Set iteration field on issue |
| **Sprint backlog view** | Sprint section on Backlog page shows card list | Sprint backlog with task breakdown | Cycle page shows issues in list/board | Filter project view by iteration |
| **Sprint board** | Board auto-filters to active sprint | Sprint-specific taskboard | Cycle board view | Board view filtered by iteration |
| **Planning poker** | Marketplace plugins (Scrum Poker for Jira) | Extensions marketplace | Not built-in | Not built-in |
| **Sprint card badge** | Sprint name shown on backlog | Iteration path visible everywhere | Cycle badge on issues | Iteration column in table view |
| **Capacity planning** | Team velocity + story point sum | Capacity bars per member per sprint | Scope target per cycle | Number field sums |
| **Sprint scope on card** | Drag card to sprint, or edit Sprint field | Edit Iteration Path field on work item | Set Cycle dropdown on issue | Set Iteration field |
| **Backlog refinement** | Backlog grooming view with estimates | Planning pane side-by-side | Triage view for incoming issues | Draft issues in table view |
| **Sprint reports** | Burndown, velocity, sprint report | Burndown, velocity, cumulative flow | Cycle progress, scope history | Insights charts |

### Key patterns (universal across tools):

1. **Product Backlog ↔ Sprint Backlog split** — All tools have a clear separation between "all work" and "sprint work"
2. **Drag-and-drop assignment** — The primary interaction for adding cards to sprints
3. **Sprint-scoped board view** — Kanban board filters to show only the active sprint
4. **Sprint selector on card detail** — Dropdown/field on every card to set/change its sprint
5. **Running totals during planning** — Story point sum updates live as you add/remove cards
6. **Two-phase planning** — Part 1: What to commit? (select cards) → Part 2: How to do it? (estimate/break down)

---

## Implementation Plan

### Phase 1: Sprint Assignment UI (Core Functionality)

**Goal:** Users can assign cards to sprints and see what's in them.

#### 1.1 Sprint Selector in Card Detail Sidebar

Add a "Sprint" section to `CardDetailPanel.razor` sidebar, between Priority and Due Date:

```
┌─────────────────────────────┐
│ Sprint                       │
│ ┌─────────────────────────┐ │
│ │ Sprint 3 (Active)    ▼ │ │
│ └─────────────────────────┘ │
│                              │
│ [Remove from sprint]         │
└─────────────────────────────┘
```

- Dropdown lists all sprints for the board (Planning + Active)
- Shows sprint status badge
- "None" option to remove from sprint
- Calls existing `AddCardToSprintAsync` / `RemoveCardFromSprintAsync`

**Changes required:**
- `CardDto` — Add `SprintId?` and `SprintTitle?` properties
- `CardDetailPanel.razor` — Add sprint selector section in sidebar
- `CardDetailPanel.razor.cs` — Load sprints, handle sprint change
- `CardService` or `SprintService` — Ensure card→sprint lookup is efficient

#### 1.2 Sprint Backlog View in Sprint Panel

Expand `SprintPanel.razor` — clicking a sprint shows its card list:

```
┌──────────────────────────────────────────┐
│ 🏃 Sprint 3 (Active)                     │
│ Goal: Ship file upload improvements       │
│ 📅 Mar 17 – Mar 31  🎴 8 cards  📊 34 SP │
│ ████████████░░░░░░░░ 62%                 │
│                                           │
│ ┌─ Sprint Backlog ──────────────────────┐ │
│ │ ☐ Upload progress indicator    5 SP   │ │
│ │ ☐ Drag-and-drop upload        8 SP   │ │
│ │ ☐ File versioning             13 SP  │ │
│ │ ✓ Thumbnail generation        3 SP   │ │
│ │ ✓ Upload error handling       2 SP   │ │
│ │ ☐ Bulk download               3 SP   │ │
│ │                                       │ │
│ │ Total: 34 SP | Done: 5 SP (15%)      │ │
│ └───────────────────────────────────────┘ │
│                                           │
│ [+ Add cards...]                          │
└──────────────────────────────────────────┘
```

**Changes required:**
- `SprintDto` — Add `Cards` list (or a new `SprintDetailDto`)
- `ITracksApiClient` — Add `GetSprintCardsAsync(boardId, sprintId)`
- `SprintService` — Add method to list sprint cards with details
- `SprintsController` — Add `GET /api/v1/boards/{boardId}/sprints/{sprintId}/cards`
- `SprintPanel.razor` — Expandable card list per sprint with click-to-open card detail

#### 1.3 Quick-Add Cards to Sprint

From the expanded sprint backlog, an "+ Add cards" button opens a picker showing unassigned board cards:

```
┌─ Add Cards to Sprint 3 ─────────────────┐
│ 🔍 Search cards...                       │
│                                          │
│ Unassigned Cards (12):                   │
│ ☐ Fix login redirect          — 2 SP    │
│ ☐ Admin user search           — 5 SP    │
│ ☐ Dashboard widget API        — ?       │
│ ☐ Email notification template — 3 SP    │
│ ...                                      │
│                                          │
│ Selected: 3 cards (10 SP)                │
│ Sprint total after: 44 SP                │
│                                          │
│ [Add Selected]  [Cancel]                 │
└──────────────────────────────────────────┘
```

**Changes required:**
- `SprintPanel.razor` — Card picker dialog with multi-select
- `SprintPanel.razor.cs` — Load unassigned cards, batch add
- `SprintService` — `GetUnassignedCardsAsync(boardId)` helper
- `ITracksApiClient` — Add batch add method or call individual adds

---

### Phase 2: Sprint-Scoped Board View

**Goal:** Focus the kanban board on just the active sprint's cards.

#### 2.1 Sprint Filter on Kanban Board

Add a sprint filter dropdown alongside the existing text/priority/label filters:

```
┌─ Filters ──────────────────────────────────────────┐
│ 🔍 Search  │ Priority ▼ │ Label ▼ │ Sprint ▼      │
│            │            │         │ ○ All cards    │
│            │            │         │ ● Sprint 3 ✓  │
│            │            │         │ ○ Sprint 2    │
│            │            │         │ ○ No sprint   │
└────────────────────────────────────────────────────┘
```

- Default: "All cards" (current behavior)
- Selecting a sprint filters kanban to only show cards in that sprint
- "No sprint" shows unassigned cards (useful for backlog grooming)
- Active sprint gets a checkmark badge

**Changes required:**
- `KanbanBoard.razor` — Add sprint filter dropdown
- `KanbanBoard.razor.cs` — Add `_sprintFilter` state, filter cards by sprint
- `CardDto` — Must include `SprintId` (from Phase 1.1)
- `KanbanBoard` parameters — Pass sprints list from parent

#### 2.2 Sprint Badge on Kanban Cards

Show a subtle sprint indicator on cards in the kanban view:

```
┌─────────────────────────┐
│ Fix login redirect       │
│ 🟡 Medium               │
│ 📅 Mar 28  │ 2 SP       │
│ 🏃 Sprint 3             │  ← New badge
└─────────────────────────┘
```

Only shown when viewing "All cards" (not when already filtered to a sprint).

**Changes required:**
- `KanbanBoard.razor` — Add sprint name badge in card template
- CSS — `.tracks-card-sprint-badge` styling

---

### Phase 3: Sprint Planning Mode

**Goal:** Dedicated planning experience for sprint planning ceremonies.

#### 3.1 Sprint Planning View

A side-by-side view for sprint planning meetings. Left panel = product backlog (unassigned cards). Right panel = sprint backlog. Drag cards between them.

```
┌─── Product Backlog ──────────┬─── Sprint 4 Planning ─────────┐
│                              │                                │
│ Sort: Priority ▼             │ Capacity: 40 SP                │
│                              │ Committed: 34 SP (85%)         │
│ ─── High Priority ───        │ ████████████████░░░░           │
│ ▸ Auth token refresh   5 SP  │                                │
│ ▸ File sync conflict   8 SP  │ ─── Committed Cards ───        │
│ ▸ Calendar sharing     3 SP  │ ▸ Upload progress      5 SP   │
│                              │ ▸ Drag-and-drop        8 SP   │
│ ─── Medium Priority ──       │ ▸ File versioning     13 SP   │
│ ▸ Dashboard widgets    5 SP  │ ▸ Thumbnail gen        3 SP   │
│ ▸ Notification center  3 SP  │ ▸ Upload errors        2 SP   │
│ ▸ User profile avatar  2 SP  │ ▸ Bulk download        3 SP   │
│                              │                                │
│ ─── Low Priority ───         │ ─── Needs Estimate ───         │
│ ▸ Dark mode           ? SP   │ ▸ Mobile responsive    ? SP   │
│ ▸ Export to CSV        1 SP  │                                │
│                              │                                │
│     [→ Add to Sprint]        │   [← Remove from Sprint]      │
└──────────────────────────────┴────────────────────────────────┘
```

Features:
- **Product backlog** (left): All cards NOT in any active/planning sprint, sorted by priority
- **Sprint backlog** (right): Cards committed to this sprint
- **Capacity bar**: Team's target SP vs committed SP
- **"Needs Estimate" section**: Cards with no story points — highlights gaps
- **Drag-and-drop** between panels (or button-based move)
- **Running totals** update live as cards are added/removed
- **Click card** to see details without leaving planning view

**Changes required:**
- New component: `SprintPlanningView.razor` / `.razor.cs`
- CSS: `.tracks-sprint-planning` layout (flexbox two-panel)
- `TracksPage.razor` — New view mode alongside Boards/Board/Teams
- `SprintService` — `GetBacklogCardsAsync(boardId)` returns cards not in any planning/active sprint
- Sprint capacity: Add `TargetStoryPoints` field to `Sprint` model and `CreateSprintDto`/`UpdateSprintDto`

#### 3.2 Planning Poker Integration

Connect planning poker to sprint planning. From the sprint planning view, you can start a poker session for any unestimated card:

```
┌─── Sprint 4 Planning ────────────────────┐
│                                          │
│ ▸ Mobile responsive    ? SP  [🃏 Estimate] │  ← Start poker
│ ▸ File versioning     13 SP              │
│                                          │
└──────────────────────────────────────────┘
```

Clicking "🃏 Estimate" opens a poker session inline or as a modal:

```
┌─ Estimating: Mobile Responsive Layout ───┐
│                                          │
│ Scale: Fibonacci                         │
│                                          │
│ Your vote:                               │
│ [1] [2] [3] [5] [8] [13] [21]           │
│                                          │
│ Votes: 3/4 submitted                    │
│                                          │
│ ● Alice: voted                           │
│ ● Bob: voted                            │
│ ● Charlie: voted                         │
│ ○ Dana: waiting...                       │
│                                          │
│ [Reveal Votes]                           │
│                                          │
│ Results:                                 │
│ Alice: 5  Bob: 8  Charlie: 5  Dana: 5   │
│ Avg: 5.75 → Suggested: 5                │
│                                          │
│ [Accept 5 SP] [New Round] [Cancel]       │
└──────────────────────────────────────────┘
```

When estimate is accepted:
- Story points are set on the card
- Sprint totals update immediately
- Card moves from "Needs Estimate" to "Committed" section

**Changes required:**
- `SprintPlanningView.razor` — Inline poker session UI
- Wire existing `PokerService` / `PokerController` APIs
- Real-time updates via SignalR (already exists for poker)
- Auto-refresh sprint totals when estimate is accepted

#### 3.3 Sprint Capacity Planning

Add team capacity awareness to sprint planning:

```
┌─ Sprint 4 Capacity ─────────────────────┐
│                                          │
│ Team Target: 40 SP/sprint                │
│ Historical Velocity: 38 SP avg           │
│                                          │
│ ┌─ Member Workload ──────────────────── │
│ │ Alice    ████████░░  12 SP            │ │
│ │ Bob      █████████░  15 SP            │ │
│ │ Charlie  ████░░░░░░   7 SP            │ │
│ │ Dana     ░░░░░░░░░░   0 SP  ⚠        │ │
│ └─────────────────────────────────────── │
│                                          │
│ ⚠ Dana has no cards assigned             │
└──────────────────────────────────────────┘
```

**Changes required:**
- `Sprint` model — Add `TargetStoryPoints` field
- `SprintService` — Compute member workload (cards assigned + story points per assignee)
- `SprintVelocityDto` — Already exists, surface it in UI
- `SprintPlanningView.razor` — Capacity sidebar panel

---

### Phase 4: Sprint Reports & Analytics

**Goal:** Visibility into sprint progress and team performance.

#### 4.1 Burndown Chart

Already have `BurndownPointDto` and `SprintReportService`. Need UI:

```
Remaining SP
     34 │╲
        │ ╲___
     20 │     ╲___
        │         ╲
     10 │          ╲___
        │              ╲
      0 │───────────────╲──
        Day1  Day3  Day5  Day7  Day10
        
        ── Ideal   ── Actual
```

**Changes required:**
- New component: `SprintBurndownChart.razor`
- Chart rendering: SVG-based or lightweight JS chart library
- `SprintReportService` already computes burndown data
- Add chart to sprint detail view and planning view

#### 4.2 Velocity Chart

Show team velocity across past sprints:

```
SP Completed
     45 │     ██
     40 │  ██ ██ ██
     35 │  ██ ██ ██ ██
     30 │  ██ ██ ██ ██
        └──S1──S2──S3──S4──
        
        Avg: 38 SP/sprint
```

**Changes required:**
- New component: `VelocityChart.razor`
- `SprintReportService` — Already has velocity calculation
- Surface in sprint panel and team dashboard

#### 4.3 Sprint Retrospective Summary

When completing a sprint, show a summary:

```
┌─ Sprint 3 Complete ──────────────────────┐
│                                          │
│ ✓ Completed: 6/8 cards (75%)            │
│ ✓ Story Points: 26/34 SP (76%)          │
│                                          │
│ Incomplete cards:                        │
│ ▸ File versioning (13 SP)               │
│ ▸ Bulk download (3 SP)                  │
│                                          │
│ Move incomplete to:                      │
│ ○ Next Sprint (Sprint 4)                │
│ ○ Product Backlog                        │
│ ● Select sprint...  [Sprint 4 ▼]       │
│                                          │
│ [Complete Sprint]                        │
└──────────────────────────────────────────┘
```

**Changes required:**
- `SprintCompletionDialog.razor` — Summary + incomplete card handling
- `SprintService` — `CompleteSprintAsync` gains `moveIncompleteToSprintId` parameter
- Batch move incomplete cards to next sprint or unassign them

---

## Implementation Priority & Dependencies

```
Phase 1 (MUST HAVE — Sprint is unusable without these)
├── 1.1 Sprint Selector in Card Detail (prerequisite: CardDto gains SprintId)
├── 1.2 Sprint Backlog View (list cards in sprint)
└── 1.3 Quick-Add Cards to Sprint

Phase 2 (HIGH — Makes daily sprint work productive)
├── 2.1 Sprint Filter on Kanban Board (depends on 1.1 CardDto.SprintId)
└── 2.2 Sprint Badge on Kanban Cards

Phase 3 (MEDIUM — Sprint planning ceremony support)
├── 3.1 Sprint Planning View (depends on Phase 1 + 2)
├── 3.2 Planning Poker Integration (depends on 3.1)
└── 3.3 Sprint Capacity Planning (depends on 3.1)

Phase 4 (NICE-TO-HAVE — Analytics & reporting)
├── 4.1 Burndown Chart (backend already exists)
├── 4.2 Velocity Chart (backend already exists)
└── 4.3 Sprint Retrospective Summary
```

---

## Data Model Changes

### CardDto additions
```csharp
/// Current sprint assignment (null if unassigned)
public Guid? SprintId { get; init; }
public string? SprintTitle { get; init; }
```

### Sprint model additions
```csharp
/// Target story points for capacity planning
public int? TargetStoryPoints { get; set; }
```

### New API endpoints needed
```
GET  /api/v1/boards/{boardId}/sprints/{sprintId}/cards    — List cards in sprint
GET  /api/v1/boards/{boardId}/backlog                      — Unassigned cards (not in any planning/active sprint)
POST /api/v1/boards/{boardId}/sprints/{sprintId}/cards/batch — Batch add multiple cards
```

### DTO additions
```csharp
/// Request to batch-add cards to a sprint
public sealed record BatchAddSprintCardsDto
{
    public required List<Guid> CardIds { get; init; }
}
```

---

## Effort Estimates (Rough)

| Phase | Components | Estimated Effort |
|-------|-----------|-----------------|
| Phase 1 | CardDto changes, Sprint selector, Backlog view, Quick-add | Medium |
| Phase 2 | Sprint filter, Card badges | Small |
| Phase 3 | Planning view, Poker integration, Capacity | Large |
| Phase 4 | Charts, Completion dialog | Medium |

---

## Key Principles

1. **Backend first** — The API layer for card↔sprint assignment already exists. Most work is UI.
2. **Progressive enhancement** — Phase 1 makes sprints usable. Phase 2+ makes them great.
3. **Don't reinvent** — Use existing poker service, velocity DTOs, burndown calculations.
4. **Real-time** — All sprint changes should propagate via existing SignalR infrastructure.
5. **Mobile-friendly** — Sprint planning views should work reasonably on tablets (common in planning meetings).
