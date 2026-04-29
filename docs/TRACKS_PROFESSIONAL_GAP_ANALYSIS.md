# Tracks Module — Professional Gap Analysis

> Research conducted April 29, 2026 against Jira, Linear, Monday.com, Azure DevOps.

---

## 1. Deletion & Lifecycle (Current: Partial)

### Industry Standard Pattern

All major tools use a **two-stage deletion lifecycle**:

```
Active → Soft-Deleted (Trash/Recycle Bin) → Permanently Deleted
         ↑___________ Grace Period ___________↑
         (28-60 days, 30 is most common)
```

| Tool | Stage 1 | Stage 2 | Retention | Who Can |
|------|---------|---------|-----------|---------|
| **Jira** | Move to Trash | Permanent Delete | **60 days** | Admins only |
| **Jira** | Archive (read-only) | N/A | Forever | Admins |
| **Linear** | Delete → "Recently deleted" | Permanent | **30 days** | Team owners |
| **Linear** | Retire (read-only) | N/A | Forever | Admins |
| **Monday.com** | Recycle Bin | Permanent | 30 days | Admins |
| **Azure DevOps** | Soft-delete | Permanent | **28 days** | Org admins |

### What DotNetCloud Has (Current)
- ✅ Soft-delete (IsDeleted=true, DeletedAt set)
- ✅ 30-day retention before hard delete
- ✅ Background cleanup service (ProductCleanupBackgroundService)
- ✅ Admin/Owner role check on delete
- ✅ Restore (undelete) endpoint
- ✅ Deleted products list endpoint
- ✅ Deleted products UI with restore button and days-remaining badge

### What DotNetCloud Is Still Missing
- ☐ **"Trash" / "Recycle Bin" branding** — call it what the industry calls it
- ☐ **Archive as alternative** — read-only mode without deletion (Jira's archive, Linear's retire)
- ☐ **Permanent delete button** in trash — let admins force immediate permanent deletion
- ☐ **Delete audit metadata** — show who deleted the product (currently not stored)
- ☐ **Work items become read-only** during grace period (currently they stay editable via direct link)
- ☐ **Cascade soft-delete** — when product is soft-deleted, all work items should also be soft-deleted
- ☐ **Bulk delete/restore** — multi-select in trash view
- ☐ **Empty trash** — one-click "permanently delete all expired"
- ☐ **Restore audit trail** — log when and who restored

---

## 2. Hierarchy Navigation & Breadcrumb (Current: Basic)

### Industry Standard
- **Jira**: Project → Board → Issue. Breadcrumbs at top. Project sidebar shows all boards.
- **Linear**: Workspace → Team → Project → Issue. Left sidebar with team/project tree. Command-K global search.
- **Monday.com**: Workspace → Board → Group → Item. Left sidebar with collapsible workspace tree.

### What DotNetCloud Has
- ✅ Breadcrumb navigation (Org → Product → Epic → Feature)
- ✅ Sidebar product list
- ✅ Level indicator banner on kanban
- ✅ Card type badges (Epic/Feature/Item/SubItem)

### What DotNetCloud Is Still Missing
- ☐ **Hierarchy tree view** — collapsible tree in sidebar showing Products → Epics → Features → Items
- ☐ **"Where am I?" depth indicator** — more prominent visual stack showing full path
- ☐ **Command palette / quick search** (Cmd-K style) — fast navigation between products/items
- ☐ **Drill-down state persistence** — remember last-viewed hierarchy level per product
- ☐ **Parent link on every item** — clickable "Parent: Epic #42" on every card/detail
- ☐ **Breadcrumb dropdowns** — click any breadcrumb level to see siblings (not just go up)

---

## 3. Work Item Views & Organization (Current: Kanban-only)

### Industry Standard
- **Jira**: Board (Kanban/Scrum), Timeline, List, Calendar, Issues (table)
- **Linear**: Board, List, Timeline (Gantt-like), custom Views
- **Monday.com**: Kanban, Timeline, Gantt, Calendar, Chart, Table, Map, Files

### What DotNetCloud Has
- ✅ Kanban board (drag-and-drop)
- ✅ Backlog view
- ✅ Timeline view (basic)
- ✅ Sprint planning view
- ✅ Work item detail panel

### What DotNetCloud Is Still Missing
- ☐ **Table/List view** — sortable, filterable table of all work items (Jira's "Issues" view)
- ☐ **Calendar view** — work items with due dates on a calendar
- ☐ **Gantt chart** — dependency visualization with timeline
- ☐ **Dashboard/Overview** — product-level summary with charts (burndown, velocity, cycle time)
- ☐ **Saved filters / Custom views** — let users create and save filtered views (Linear's custom views)
- ☐ **Group by** — group cards by assignee, label, priority, sprint (not just swimlane)
- ☐ **Quick filters** — preset filter chips (My Items, Unassigned, Due This Week, etc.)

---

## 4. Product Settings & Configuration (Current: Minimal)

### Industry Standard
- **Jira**: Project settings → Issue types, Workflows, Screens, Fields, Permissions, Notifications, Automation
- **Linear**: Team settings → Workflows, Labels, Templates, Cycles, Triage, Estimates, Auto-close/archive
- **Monday.com**: Board settings → Columns, Automations, Permissions, Notifications, Integrations

### What DotNetCloud Has
- ✅ Product creation wizard (Name, Color, Settings, Members)
- ✅ Swimlane management (add/remove/reorder on kanban)
- ✅ Label management
- ✅ Product members (add/remove/role)

### What DotNetCloud Is Still Missing
- ☐ **Product settings page** — dedicated settings view for the product
- ☐ **Default swimlane templates** — "Kanban", "Scrum", "Bug Tracking" presets (we have templates but no UI)
- ☐ **Work item type configuration** — enable/disable types per product (e.g., skip Feature level)
- ☐ **Custom fields** — let products define custom fields on work items
- ☐ **Automation rules** — "When X happens, do Y" (e.g., auto-assign, auto-label)
- ☐ **Notification settings** — per-product notification preferences
- ☐ **Product export/import** — CSV/JSON export of all work items
- ☐ **Product duplication** — clone an existing product with its structure
- ☐ **Product transfer** — transfer ownership to another user

---

## 5. Teams & Collaboration (Current: Basic)

### Industry Standard
- **Linear**: Teams with private/public, sub-teams, team retirement, cross-team projects
- **Jira**: Project roles (Admin, Member, Viewer), groups, shared permissions
- **Monday.com**: Board owners, subscribers, guest users

### What DotNetCloud Has
- ✅ Team entity (separate from product members)
- ✅ Product member roles (Viewer, Member, Admin, Owner)
- ✅ Team management page

### What DotNetCloud Is Still Missing
- ☐ **@mentions in comments** — notify specific users (mention parser exists but not fully wired)
- ☐ **Watchers/subscribers** — users who get notified of changes on specific items
- ☐ **Activity feed per product** — chronological log of all changes (activity entity exists)
- ☐ **Comment reactions** — emoji reactions on comments
- ☐ **Share work item** — generate shareable link with optional permissions

---

## 6. Polish & Professional Touches (Current: Needs Work)

### What DotNetCloud Is Still Missing
- ☐ **Empty states** — helpful illustrations and guidance when a view is empty (partially done)
- ☐ **Loading skeletons** — animated placeholders instead of spinners
- ☐ **Keyboard shortcuts** — documented shortcut system (Jira/Linear style)
- ☐ **Undo toast** — "Product deleted. Undo?" snackbar (like Gmail)
- ☐ **Confirmation dialogs** — consistent styled confirm for destructive actions (partially done)
- ☐ **Error handling UX** — graceful error states with retry buttons
- ☐ **Offline indicator** — show when real-time connection is lost
- ☐ **Onboarding tour** — first-time user walkthrough for Tracks
- ☐ **Help/info tooltips** — `?` icons with contextual help

---

## Priority Action Items

### 🔴 Critical (Missing basics that every professional tool has)
1. **Rename "Deleted Products" to "Trash"** — match industry terminology
2. **Add "Permanently Delete" button in trash** — let admins force immediate deletion
3. **Record DeletedByUserId** — store who deleted the product for audit
4. **Add Table/List view** — sortable, filterable work item table
5. **Add Product Settings page** — dedicated settings (not just the wizard)
6. **Add parent link on work item cards/details** — navigable hierarchy

### 🟡 High Priority (Expected for professional polish)
7. **Archive as alternative to delete** — read-only mode
8. **Cascade soft-delete** — soft-delete all work items when product is soft-deleted
9. **Command palette (Cmd-K)** — fast navigation
10. **Custom views / saved filters** — user-defined filtered views
11. **Undo toast for deletes** — "Product deleted. Undo?" with 10s timeout
12. **Keyboard shortcuts** — documented shortcut reference

### 🟢 Medium Priority (Competitive advantage)
13. **Automation rules** — simple if-this-then-that
14. **Calendar view** — due date calendar
15. **Gantt/dependency chart** — visual dependency graph
16. **Watchers/subscribers** — notification on specific items
17. **Dashboard with charts** — product-level overview
18. **Onboarding tour** — guided first-time experience
