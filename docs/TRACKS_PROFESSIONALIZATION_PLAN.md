# Tracks Professionalization — Implementation Plan

> Based on comprehensive competitive research against Jira, Linear, Asana, and Azure DevOps
> Reference: `docs/TRACKS_COMPREHENSIVE_FEATURE_ANALYSIS.md`
> Date: April 29, 2026

---

## Executive Summary

DotNetCloud Tracks has a solid foundation: kanban boards, sprints, burndown charts, work item hierarchy, creation wizards, and a professional trash system with 30-day retention. But compared to industry leaders, we're missing 10 critical features that every professional PM tool has. This plan addresses all of them.

---

## Phase A: Quick Wins (Effort: Small, Impact: High)

### A-1: CSV Export
**Effort:** ~1 hour  
**What:** Export any filtered view to CSV.  
**Files:**
- `WorkItemCsvExporter.cs` — builds CSV from `List<WorkItemDto>`, writes to `HttpResponse`
- Button in `KanbanBoard.razor` and backlog toolbar: "Export CSV"
- Controller: `GET /api/v1/products/{productId}/work-items/export?swimlaneId=&labelId=&priority=`

**Deliverables:**
- ☐ Export button on kanban and backlog views
- ☐ CSV includes: Number, Title, Type, Priority, Status (swimlane), Assignee, Story Points, Due Date, Labels, Sprint
- ☐ Respects current filters

---

### A-2: Keyboard Shortcuts
**Effort:** ~1 hour  
**What:** Documented keyboard shortcut system.  
**Files:**
- `TracksKeyboardHandler.razor.cs` — JavaScript interop for global keydown listener
- Shortcut reference modal (`TracksShortcutsModal.razor`)

**Deliverables:**
- ☐ `N` = New work item (opens wizard)
- ☐ `/` = Focus search/filter input
- ☐ `Esc` = Close detail panel / cancel
- ☐ `←` `→` = Navigate hierarchy levels (browser back/forward doesn't break)
- ☐ `?` = Show shortcuts reference modal
- ☐ `Ctrl+Enter` = Submit current form/dialog

---

### A-3: Undo Toast for Deletes
**Effort:** ~30 minutes  
**What:** Gmail-style "Product deleted. Undo?" snackbar that appears for 10 seconds.  
**Files:**
- `UndoToast.razor` — reusable toast component
- Wire into `ProductListView.razor.cs` delete flow

**Deliverables:**
- ☐ After product delete: toast appears at bottom with "Undo" button
- ☐ Auto-dismisses after 10 seconds
- ☐ Clicking "Undo" calls restore API
- ☐ Reusable component for future use (work item delete, etc.)

---

### A-4: Watchers / Subscribers
**Effort:** ~2 hours  
**What:** Users can subscribe to work items to get notified of changes.  
**Files:**
- `WorkItemWatcher` entity (WorkItemId, UserId, SubscribedAt) — new migration
- `WorkItemService.SubscribeAsync` / `UnsubscribeAsync` / `GetWatchersAsync`
- `WorkItemsController` endpoints: `POST/DELETE /api/v1/work-items/{id}/watch`
- Bell icon button in `WorkItemDetailPanel.razor`
- Auto-subscribe creator and assignee on work item creation

**Deliverables:**
- ☐ Entity + migration + EF config
- ☐ Subscribe/unsubscribe API endpoints
- ☐ Bell icon in detail panel (filled when subscribed)
- ☐ Watcher count shown on card
- ☐ Auto-subscribe creator + assignee
- ☐ API client methods

---

## Phase B: Medium Effort, High Impact

### B-1: @Mentions in Comments
**Effort:** ~3 hours  
**What:** Type @ to mention users in comments; they get notified.  
**Files:**
- `MentionTypeahead.razor` — dropdown that appears when typing @
- Wire into comment input in `WorkItemDetailPanel.razor`
- `INotificationService` call on mention
- Mention highlighting in rendered comments

**Deliverables:**
- ✓ `@` triggers user search typeahead (max 8 results)
- ✓ Mentioned user gets notification with link to work item
- ✓ @username rendered as clickable link in comments
- ✓ Debounced search as user types (300ms)

---

### B-2: Product Settings Page
**Effort:** ~3 hours  
**What:** Dedicated settings page for product configuration.  
**Files:**
- `ProductSettingsPage.razor` — new page component
- Route in `TracksPage.razor`: `TracksView.Settings`
- Settings gear icon in sidebar

**Sections:**
- ✓ **General:** Name, description, color picker
- ✓ **Swimlanes:** Manage default swimlanes (add/remove/reorder/rename, set Done)
- ✓ **Members:** List members, change roles, remove
- ✓ **Labels:** Manage product labels (create/edit/delete)
- ✓ **Danger Zone:** Archive product, Transfer ownership, Delete product

---

### B-3: Saved Filters / Custom Views
**Effort:** ~3 hours  
**What:** Save current filter state as a named view. Show in sidebar.  
**Files:**
- `CustomView` entity (ProductId, UserId, Name, FilterJson, SortJson, GroupBy, Layout) — new migration
- `CustomViewService` — CRUD
- `CustomViewsController` — REST endpoints
- `CustomViewsSidebar.razor` — list saved views in sidebar
- Save/load in `KanbanBoard.razor` and `BacklogView.razor`

**Deliverables:**
- ✓ Entity + migration + EF config
- ✓ "Save current view" button in toolbar → name prompt
- ✓ Saved views listed in sidebar under product
- ✓ Click saved view to apply filters/sort/group
- ✓ Delete/rename saved views
- ✓ Share with team option (Shared boolean flag)

---

### B-4: Calendar View
**Effort:** ~3 hours  
**What:** Calendar showing work items by due date.  
**Files:**
- `WorkItemCalendarView.razor` — month/week/day views
- `TracksCalendar.razor.cs` — calendar logic (day grid, navigation)
- `TracksView.Calendar` enum addition
- Calendar icon in sidebar navigation

**Deliverables:**
- ✓ Month view (default): grid of days, items shown as colored bars
- ✓ Week view: 7-column horizontal layout
- ✓ Click item → opens detail panel
- ✓ Drag item to different date → change due date
- ✓ Color-coded by priority or swimlane
- ✓ Previous/Next month navigation
- ✓ "Today" button

---

## Phase C: Large Effort, Game-Changers

### C-1: Table / List View
**Effort:** ~5 hours  
**What:** Sortable, filterable data table of all work items.  
**Files:**
- `WorkItemListView.razor` + `.razor.cs`
- `TracksView.List` enum addition
- List icon in sidebar navigation
- Column chooser dropdown

**Deliverables:**
- ☐ Columns: Number, Title (clickable), Type, Priority, Swimlane, Assignee, Story Points, Due Date, Labels, Sprint
- ☐ Click column header to sort (asc/desc toggle)
- ☐ Resizable columns (drag column border)
- ☐ Column chooser: show/hide columns via dropdown
- ☐ Multi-select checkboxes with bulk action toolbar (assign, label, move, archive, delete)
- ☐ Inline edit: click cell to edit (title, priority, assignee, due date)
- ☐ Row click → opens detail panel
- ☐ Respects current kanban/backlog filters when switching views
- ☐ "Group by" dropdown: None, Assignee, Priority, Swimlane, Sprint, Type
- ☐ Export to CSV from table view

---

### C-2: Product Dashboard
**Effort:** ~5 hours  
**What:** Product-level overview page with charts and metrics.  
**Files:**
- `ProductDashboardView.razor` + `.razor.cs`
- `TracksView.Dashboard` enum addition
- Dashboard icon in sidebar (or default when selecting product)
- `AnalyticsService` additions for dashboard metrics

**Widgets:**
- ☐ **Status breakdown** — donut chart: work items by swimlane
- ☐ **Priority breakdown** — bar chart: Urgent/High/Medium/Low counts
- ☐ **Sprint progress** — current sprint burndown (reuse existing)
- ☐ **Velocity** — last 6 sprints velocity chart (reuse existing)
- ☐ **Cycle time** — average days from "To Do" to "Done"
- ☐ **Workload** — story points per assignee bar chart
- ☐ **Recently updated** — list of last 10 changed items
- ☐ **Upcoming due dates** — items due this week
- ☐ **Unassigned items** — count + link to filtered kanban

---

## Implementation Order

```
A-1 (CSV Export)        →  ~1h   Quick win, immediately useful
A-2 (Shortcuts)         →  ~1h   Power-user delight
A-3 (Undo Toast)        →  ~0.5h Tiny, huge UX improvement
A-4 (Watchers)          →  ~2h   Expected collaboration feature
B-1 (@Mentions)         →  ~3h   Expected collaboration feature
B-2 (Settings Page)     →  ~3h   Expected admin feature
B-3 (Saved Views)       →  ~3h   Power-user feature
B-4 (Calendar View)     →  ~3h   Expected view type
C-1 (Table View)        →  ~5h   Expected view type
C-2 (Dashboard)         →  ~5h   Game-changer overview

Total: ~26.5 hours
```

---

## Build & Deploy Strategy

Each phase group should be built, tested, deployed, and user-tested before moving to the next:

1. **Phase A** → Build → `dotnet build DotNetCloud.CI.slnf` → Deploy → Test
2. **Phase B** → Build → Deploy → Test
3. **Phase C** → Build → Deploy → Test

---

## Post-Implementation: Remaining Gaps

After these 10 items, the analysis identifies 17 more features for future phases:
- Custom fields, Automation rules, Roadmap timeline, Command palette
- Recurring work items, Comment reactions, Milestones, Capacity planning
- Dark mode enhancements, Onboarding tour, Product templates UI
- Import from CSV, Share/guest access, Goals/OKRs, Mobile notifications
- Webhooks, Custom swimlane transition rules, Column constraints (WIP limits enforcement)
