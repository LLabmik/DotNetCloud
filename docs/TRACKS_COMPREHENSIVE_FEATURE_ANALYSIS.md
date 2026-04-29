# Tracks Module — Comprehensive Professional Feature Analysis

> Research: April 29, 2026 — Jira, Linear, Asana, Monday.com, Azure DevOps
> Goal: Make Tracks "impressively good in all aspects"

---

## Feature Matrix: What Every Professional PM Tool Has

| Category | Jira | Linear | Asana | Azure DevOps | **DNC Tracks** |
|----------|------|--------|-------|-------------|----------------|
| **Kanban Board** | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Table/List View** | ✅ | ✅ | ✅ | ✅ | ❌ |
| **Calendar View** | ✅ | ❌ | ✅ | ❌ | ❌ |
| **Timeline/Gantt** | ✅ | ✅ | ✅ | ❌ | ✅ (basic) |
| **Scrum/Sprints** | ✅ | ✅ | ❌ | ✅ | ✅ |
| **Backlog** | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Roadmaps** | ✅ | ✅ | ✅ | ❌ | ❌ |
| **Custom Views/Filters** | ✅ | ✅ | ✅ | ✅ | ❌ |
| **Dashboards** | ✅ | ❌ | ✅ | ✅ | ❌ |
| **Reports/Analytics** | ✅ | ✅ | ✅ | ✅ | ✅ (burndown) |
| **Goals/OKRs** | ✅ | ❌ | ✅ | ❌ | ❌ |
| **Portfolios** | ✅ | ❌ | ✅ | ❌ | ❌ |
| **Forms/Intake** | ✅ | ✅ | ✅ | ❌ | ❌ |
| **Custom Fields** | ✅ | ✅ | ✅ | ✅ | ❌ |
| **Workflows/Automation** | ✅ | ✅ | ✅ | ❌ | ❌ |
| **Templates** | ✅ | ✅ | ✅ | ❌ | ✅ (data) |
| **Dependencies** | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Time Tracking** | ✅ | ❌ | ✅ | ❌ | ✅ |
| **@mentions** | ✅ | ✅ | ✅ | ✅ | ❌ (partial) |
| **Watchers/Subscribers** | ✅ | ✅ | ✅ | ✅ | ❌ |
| **Comments & Activity** | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Attachments** | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Multi-Team Projects** | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Role-Based Access** | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Trash/Recycle Bin** | ✅ (60d) | ✅ (30d) | ✅ (30d) | ✅ (28d) | ✅ (30d) |
| **Archive (read-only)** | ✅ | ✅ (retire) | ✅ | ❌ | ❌ |
| **Bulk Operations** | ✅ | ✅ | ✅ | ✅ | ✅ (backlog) |
| **Import/Export (CSV)** | ✅ | ✅ | ✅ | ✅ | ❌ |
| **Keyboard Shortcuts** | ✅ | ✅ | ✅ | ❌ | ❌ |
| **Command Palette** | ✅ (Cmd-K) | ✅ (Cmd-K) | ❌ | ❌ | ❌ |
| **Search** | ✅ (JQL) | ✅ | ✅ | ✅ (WIQL) | ❌ |
| **API/REST** | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Webhooks/Integrations** | ✅ | ✅ | ✅ | ✅ | ❌ |
| **Mobile App** | ✅ | ✅ | ✅ | ❌ | ❌ |
| **Dark Mode** | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Onboarding Tour** | ❌ | ❌ | ✅ | ❌ | ❌ |
| **Undo/Undo Toast** | ❌ | ❌ | ❌ | ❌ | ❌ |

---

## Detailed Gap Analysis by Category

### 1. VIEWS & VISUALIZATION (🔴 High Priority)

#### 1.1 Table / List View — MISSING
Every professional PM tool has a sortable, filterable data table.
- **Jira:** "Issues" view — full table with column chooser, sort, filter
- **Linear:** List view with customizable columns, grouping
- **Asana:** List view with sections, custom fields as columns
- **Azure DevOps:** Queries — SQL-like filtered lists, saveable

**What we need:** A `WorkItemListView.razor` with:
- Resizable, sortable columns (Title, Type, Priority, Status, Assignee, Due Date, Story Points, Sprint)
- Column chooser (show/hide columns)
- Multi-select with bulk actions toolbar
- Export to CSV
- Save filters as named views

#### 1.2 Calendar View — MISSING
- **Jira:** Calendar view showing issues by due date
- **Asana:** Calendar view with drag-to-reschedule
- **Monday.com:** Calendar view with color coding

**What we need:** A `WorkItemCalendarView.razor` with month/week/day views, due date items, drag to reschedule.

#### 1.3 Roadmap / Initiative View — MISSING
- **Linear:** Initiatives contain projects, visual roadmap timeline
- **Jira:** Advanced Roadmaps (formerly Portfolio), timeline with dependency lines
- **Asana:** Timeline view with dependencies, Portfolios grouping projects
- **Azure DevOps:** Delivery Plans — cross-team calendar view

**What we need:** A `ProductRoadmapView.razor` showing epics/features on timeline, color-coded by status, dependency arrows.

#### 1.4 Dashboards — MISSING
- **Jira:** Configurable dashboards with 30+ widget types
- **Asana:** Dashboard with real-time chart widgets
- **Azure DevOps:** Team dashboards with Markdown, charts, query tiles

**What we need:** A `ProductDashboardView.razor` with:
- Work item count by status (donut chart)
- Burndown/burnup charts
- Velocity chart (already have)
- Cycle time chart
- Workload by assignee
- Recently updated items

#### 1.5 Custom Views / Saved Filters — MISSING
- **Linear:** Custom views with filter, sort, group, layout; saved per team/user
- **Jira:** Saved filters with JQL; shared across team
- **Azure DevOps:** Queries saved to "My Queries" or "Shared Queries"

**What we need:** Save current filter state as a named view. Show saved views in sidebar.
```json
{ "name": "My Bugs", "filters": {"type":"Item","label":"bug"}, "sort":"priority", "group":"assignee" }
```

---

### 2. WORK ITEMS (🟡 High Priority)

#### 2.1 Custom Fields — MISSING
- **Jira:** Field configurations — text, number, date, select, user picker, etc.
- **Linear:** Properties (Estimate, Priority, Labels, custom statuses)
- **Asana:** Custom fields — text, number, date, people, dropdown
- **Azure DevOps:** Process customization — add fields to work item types

**What we need:**
- Product-level custom field definitions (`CustomField` entity: ProductId, Name, Type, Options, IsRequired, Position)
- Field types: Text, Number, Date, SingleSelect, MultiSelect, User
- `WorkItemFieldValue` entity: WorkItemId, FieldId, Value
- Show custom fields in detail panel and table view

#### 2.2 Work Item Templates — HAS DATA, MISSING UI
- **Jira:** Issue templates with pre-filled fields
- **Linear:** Issue templates per team with auto-assign, auto-label
- **Asana:** Task templates with subtasks, custom fields, dependencies

**What we need:** Template picker in creation wizard. "Create from template" dropdown.
We have `ItemTemplate` entity already.

#### 2.3 Rich Description / Documents — PARTIAL
- **Linear:** Real-time collaborative documents (PRDs, specs) with inline comments
- **Jira:** Description with rich text, Confluence integration
- **Asana:** Rich text description, ability to create docs

**What we have:** Markdown editor (good). Missing: collaborative editing, document linking.

#### 2.4 Checklists — HAS
✅ We have checklists with items. Good.

#### 2.5 Dependencies — HAS
✅ We have blocking/blocked-by with cycle detection. Good.

#### 2.6 Recurring Work Items — MISSING
- **Linear:** Recurring issues with cron-like scheduling
- **Jira:** Recurring issues via automation
- **Asana:** Recurring tasks with repeat rules

**What we need:** `CreateRecurringWorkItem` — schedule (daily/weekly/monthly/cron), next creation date.

---

### 3. WORKFLOWS & AUTOMATION (🟡 High Priority)

#### 3.1 Automation Rules — MISSING
- **Jira:** Automation — "When X happens, do Y" (200+ triggers/actions)
- **Linear:** Auto-close, auto-archive, git automations
- **Asana:** Rules — triggers + actions, bundles for reusable workflows
- **Monday.com:** Automations — "When status changes, notify assignee"

**What we need (simple version):**
```json
{
  "trigger": "work_item_moved",  // work_item_created, status_changed, due_date_approaching, assigned
  "conditions": { "to_swimlane": "done" },
  "actions": [
    { "type": "set_field", "field": "is_archived", "value": true },
    { "type": "add_label", "label_id": "..." },
    { "type": "notify", "users": ["assignee"] }
  ]
}
```

#### 3.2 Swimlane / Status Name Customization — PARTIAL
- **Linear:** Custom workflows per team — add/edit/remove statuses, set "Done" status
- **Jira:** Workflow designer — states, transitions, conditions, validators, post-functions

**What we have:** Swimlane CRUD. Missing: custom transition rules, enforce that items can only move right.

---

### 4. COLLABORATION (🟡 High Priority)

#### 4.1 @Mentions — PARTIAL
We have `MentionParser` in the codebase but it's not fully wired into the comment UI.
- **All tools:** Type @ to mention user → they get notified → click to see item

**What we need:** Full @mention UX: typeahead dropdown, notification on mention, mention highlighting in comments.

#### 4.2 Watchers / Subscribers — MISSING
- **Jira:** "Watchers" — users who get notified of all changes
- **Linear:** Subscribe to issue for updates
- **Asana:** "Followers" — get inbox notifications
- **Azure DevOps:** "Follow" work item

**What we need:** `WorkItemWatcher` entity: WorkItemId, UserId. Subscribe/Unsubscribe button on detail panel. Auto-subscribe creator and assignee.

#### 4.3 Activity Feed — HAS ENTITY, MISSING UI
We have `Activity` entity. Missing: chronological activity feed on product detail page and work item detail.

#### 4.4 Comment Reactions — MISSING
- **All modern tools** support emoji reactions on comments

**What we need:** `CommentReaction` entity: CommentId, UserId, Emoji. Simple 👍👎❤️😄🎉🚀 picker.

#### 4.5 Share / Guest Access — MISSING
- **Linear:** Share issue with guests via magic link
- **Asana:** Guest users with limited permissions
- **Jira:** Share issue via link (requires access)

**What we need:** Generate shareable link with optional permissions (view/comment).

---

### 5. SEARCH & NAVIGATION (🟢 Medium Priority)

#### 5.1 Full-Text Search — MISSING
- **Jira:** JQL (Jira Query Language) — `project = "MOBILE" AND status = "In Progress" AND assignee = currentUser()`
- **Linear:** Command-K with fuzzy search for issues, projects, docs
- **Azure DevOps:** WIQL + full-text search

**What we need:** Search bar that searches across titles, descriptions, comments. Auto-suggest results.

#### 5.2 Command Palette (Cmd-K) — MISSING
- **Linear:** Cmd-K → "Go to PRO-123", "My issues", "Create issue", "Toggle dark mode"
- **Jira:** `.` key opens command palette

**What we need:** A `TracksCommandPalette.razor` with:
- Quick navigation: "go to #42", "go to product Mobile"
- Quick actions: "create epic", "my items", "assigned to me"
- Global keyboard shortcut: Ctrl+K or /

#### 5.3 Keyboard Shortcuts — MISSING
- **Jira:** C = create, . = command palette, J/K = navigate, E = edit, A = assign
- **Linear:** Full shortcut reference page

**What we need:** Documented shortcut system. At minimum: N = new item, / = search, Esc = close panel, ← → = navigate hierarchy.

---

### 6. PLANNING & ROADMAPPING (🟢 Medium Priority)

#### 6.1 Milestones — MISSING
- **Linear:** Project milestones with dates, status (upcoming/completed)
- **GitHub:** Milestones with progress bar, due date

**What we need:** `Milestone` entity: ProductId, Title, Description, DueDate, Status. Show on timeline.

#### 6.2 Capacity Planning — MISSING
- **Asana:** Workload view — see how busy each team member is
- **Jira:** Capacity planning in Advanced Roadmaps
- **Linear:** Cycle planning with team capacity

**What we need:** Workload chart showing story points per assignee per sprint.

#### 6.3 Goals / OKRs — MISSING
- **Asana:** Goals linked to projects/tasks, progress tracking
- **Jira:** Goals in Jira Product Discovery
- **Monday.com:** OKR tracking

**What we could add (future):** `Goal` entity linked to products/epics.

---

### 7. IMPORTS, EXPORTS & INTEGRATIONS (🟢 Medium Priority)

#### 7.1 CSV Import/Export — MISSING
- **All tools** support CSV import/export for work items

**What we need:** Export button on any filtered view → downloads CSV. Import from CSV with field mapping.

#### 7.2 Product / Board Templates — HAS DATA, MISSING UI
We have `ProductTemplate` and `ItemTemplate` entities. Missing: UI to browse, preview, and apply templates when creating a product.

#### 7.3 Webhooks / API — HAS REST API
✅ REST API exists. Missing: webhook configuration UI, API docs portal.

---

### 8. POLISH & UX (🟢 Medium Priority)

#### 8.1 Undo Toast — MISSING
"Product deleted" → "Undo" button that appears for 10 seconds. Gmail-style.

#### 8.2 Loading Skeletons — PARTIAL
Replace spinners with animated skeleton placeholders matching the layout.

#### 8.3 Empty States — PARTIAL
Every empty view should have a helpful illustration, description, and CTA button. We have some.

#### 8.4 Drag-and-Drop Polish
Our kanban drag works. Could improve: ghost card follows cursor, column highlights on hover, smooth animation on drop.

#### 8.5 Bulk Select Polish
- Checkbox on hover (not always visible)
- "Select All" → shows count → bulk action toolbar slides down
- Keyboard: Shift+click for range select, Ctrl+click for multi-select

#### 8.6 Onboarding Tour — MISSING
First-time user sees guided tour: "This is a Product", "This is the Kanban", "Click + to create an Epic", etc.

---

### 9. PERMISSIONS & ADMIN (🟡 High Priority)

#### 9.1 Product Settings Page — MISSING
A dedicated settings page for each product (currently only a creation wizard):
- Product name, description, color
- Default swimlanes management
- Custom fields configuration
- Automation rules
- Danger zone: Archive, Delete, Transfer ownership

#### 9.2 Audit Log — PARTIAL
We have `Activity` entity. Missing: filterable audit log UI showing who did what and when.

#### 9.3 Transfer Ownership — MISSING
Product owner can transfer ownership to another admin.

---

## Priority Implementation Roadmap

### 🔴 MVP Critical (These make Tracks feel professional)
1. **Table/List view** — sortable, filterable work item table
2. **Product Settings page** — dedicated settings view
3. **@Mentions in comments** — full typeahead + notification
4. **CSV Export** — export filtered items
5. **Saved filters / Custom views** — named, shareable views
6. **Empty trash permanently** — force permanent delete from trash
7. **Archive product** — read-only mode as alternative to delete
8. **Audit trail in trash** — show who deleted + when (implemented!)
9. **Watchers/Subscribers** — subscribe to work item changes
10. **Keyboard shortcuts** — at minimum: N = new, / = search, Esc = close

### 🟡 Next Phase (Competitive advantage)
11. **Calendar view** — due date calendar
12. **Custom fields** — product-defined fields on work items
13. **Automation rules** — simple triggers + actions
14. **Roadmap / Timeline** — initiative-level planning
15. **Command palette (Cmd-K)** — fast navigation
16. **Dashboards** — product overview with charts
17. **Bulk operations polish** — toolbar slide-down, shift+click select
18. **Work item templates UI** — template picker in wizard

### 🟢 Future (Delight)
19. **Recurring work items** — scheduled creation
20. **Comment reactions** — emoji on comments
21. **Undo toast** — Gmail-style undo for deletes
22. **Milestones** — project milestones on timeline
23. **Capacity planning** — workload visualization
24. **Import from CSV** — field mapping wizard
25. **Onboarding tour** — guided first-time experience
26. **Goals/OKRs** — objective tracking
27. **Share / guest access** — magic links
