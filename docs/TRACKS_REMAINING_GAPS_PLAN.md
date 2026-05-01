# Tracks Professionalization — Remaining Gaps Plan

> Based on: `docs/TRACKS_PROFESSIONALIZATION_PLAN.md` (Post-Implementation section)
> Research: `docs/TRACKS_COMPREHENSIVE_FEATURE_ANALYSIS.md`
> Date: April 30, 2026
> 
> **Phases A–F and I are completed.** This plan covers the 17 remaining gaps, organized into Phases D–I, with Phases D, E, F, and I now complete.
> Onboarding tour is saved for last (Phase I) and is now complete. Mobile notifications are deferred to `docs/PHASE_MOBILE_NOTIFICATIONS_PLAN.md`.

---

## Executive Summary

DotNetCloud Tracks has a mature foundation: kanban boards, sprints, burndown, work item hierarchy, time tracking, dependencies, review sessions, planning poker, @mentions, watchers, calendar view, table view, dashboard, custom views, CSV export, keyboard shortcuts, and undo toast. This plan addresses the 17 remaining gaps identified in the competitive analysis against Jira, Linear, Asana, and Azure DevOps.

**Total estimated effort: 67–79 hours across 6 phases (D–I). Phases A–F and I completed.**

---

## User Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **Mobile notifications** | Deferred | Multi-day effort; separate domain; needs dedicated plan |
| **Webhooks** | Production-grade | Full system: retry, HMAC signing, delivery logs |
| **Automation rules** | Simple model | Trigger+condition+action, text-based in product settings |
| **CSV import** | Full wizard | Multi-step with auto-detect, field mapping, validation, preview |
| **Share/guest access** | Full guest system | Invite-by-email, limited permissions, token-based access |
| **Roadmap** | New product-level view | Epics/features on timeline with dependency arrows |
| **Onboarding tour** | LAST | After all features exist, covers everything end-to-end |
| **Dark mode** | Enhancement | Audit pass — CSS variables exist but new components need verification |

---

## Phase D: Data Foundation ✅ COMPLETED

**Estimate:** 12–14 hours · **Depends on:** Nothing · **Status: COMPLETED**
**Purpose:** Foundation features that other phases build upon.

### Step D-1: Custom Fields (~5h)

Custom fields allow product admins to define additional fields on work items — the #1 missing feature in every PM tool comparison.

**Entities:**
- `CustomField` — ProductId, Name, Type (Text / Number / Date / SingleSelect / MultiSelect / User), Options JSON, IsRequired, Position, CreatedAt
- `WorkItemFieldValue` — WorkItemId, CustomFieldId, Value (string)

**Files:**
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/CustomField.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/WorkItemFieldValue.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Configuration/CustomFieldConfiguration.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Configuration/WorkItemFieldValueConfiguration.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/CustomFieldService.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/CustomFieldsController.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/CustomFieldEditor.razor` (in product settings)
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/CustomFieldValues.razor` (in work item detail panel)

**Deliverables:**
- ✓ CustomField + WorkItemFieldValue entities with EF config + migration
- ✓ CustomFieldService: CRUD field definitions, get/set field values on work items
- ✓ CustomFieldsController: `GET/POST/PUT/DELETE /api/v1/products/{id}/custom-fields`
- ✓ CustomFieldEditor.razor: add/edit/delete/reorder fields in product settings
- ✓ CustomFieldValues.razor: dynamic input widgets in work item detail panel (textbox, number, date picker, dropdown, multi-select, user picker)
- ✓ Optional columns in table/list view (dynamic based on product fields)
- ✓ Field validation: required fields enforced, type validation, select options validated

### Step D-2: Milestones (~3h)

Milestones mark key dates on the project timeline — ship dates, review checkpoints, phase completions.

**Entity:**
- `Milestone` — ProductId, Title, Description, DueDate, Status (Upcoming / Active / Completed), Color, CreatedAt

**Files:**
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/Milestone.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Configuration/MilestoneConfiguration.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/MilestoneService.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/MilestonesController.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/MilestoneList.razor`

**Deliverables:**
- ✓ Milestone entity with EF config + migration
- ✓ MilestoneService: CRUD
- ✓ MilestonesController: `GET/POST/PUT/DELETE /api/v1/products/{id}/milestones`
- ✓ MilestoneList.razor: list in product settings, reorderable, status toggle
- ✓ Milestone badge/chip on work items (assignable via dropdown in detail panel)
- ✓ Milestones shown as diamond markers on roadmap timeline (Phase G-1)
- ✓ Progress indicator: X of Y work items completed for this milestone

### Step D-3: Recurring Work Items (~3h)

Automatically create work items on a schedule — weekly standup notes, monthly reports, daily checklists.

**Entity:**
- `RecurringRule` — ProductId, SwimlaneId, WorkItemType, Template JSON (title, priority, labels, assignee, story points), CronExpression, NextRunAt, IsActive, CreatedByUserId

**Files:**
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/RecurringRule.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Configuration/RecurringRuleConfiguration.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/RecurringWorkItemService.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/RecurringWorkItemBackgroundService.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/RecurringRulesController.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/RecurringRuleEditor.razor`

**Deliverables:**
- ✓ RecurringRule entity with EF config + migration
- ✓ RecurringWorkItemService: CRUD rules + `ProcessDueRecurringItemsAsync()`
- ✓ RecurringWorkItemBackgroundService: runs every 15 minutes via `PeriodicTimer`
- ✓ RecurringRulesController: `GET/POST/PUT/DELETE /api/v1/products/{id}/recurring-rules`
- ✓ RecurringRuleEditor.razor: cron builder with human-readable preview ("Every Monday at 9 AM")
- ✓ Cron presets: Daily, Weekly, Biweekly, Monthly, Weekdays
- ✓ Last-run timestamp and next-run display on rule list
- ✓ Created work item links back to the recurring rule (source tracking)

### Phase D Verification

1. Create custom fields (text, number, dropdown) → see them in work item detail panel → filter by custom field in table view
2. Create milestones → assign to work items → see progress indicator → verify diamond markers on roadmap
3. Create recurring rule "Daily standup notes" → wait for next 15-min tick → verify item auto-created in correct swimlane

---

## Phase E: Collaboration & Sharing

**Estimate:** 12–14 hours · **Depends on:** Nothing
**Purpose:** User-facing collaboration features.

### Step E-1: Comment Reactions (~1.5h)

Emoji reactions on comments — lightweight, fun, expected in any modern collaboration tool.

**Entity:**
- `CommentReaction` — CommentId + UserId + Emoji (composite key), CreatedAt

**Files:**
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/CommentReaction.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Configuration/CommentReactionConfiguration.cs`
- (Extend) `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/CommentService.cs`
- (Extend) `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/CommentsController.cs`

**Deliverables:**
- ☐ CommentReaction entity with EF config + migration (composite key on CommentId + UserId + Emoji)
- ☐ CommentService extensions: AddReactionAsync, RemoveReactionAsync, GetReactionsAsync
- ☐ Controller endpoints: `POST/DELETE /api/v1/comments/{id}/reactions`
- ☐ Emoji picker below each comment (👍 ❤️ 😄 🎉 🚀 👀)
- ☐ Reaction counts displayed as chips below comment text
- ☐ Toggle behavior: click add → click same emoji again removes it

### Step E-2: Share / Guest Access (~7h)

Share work items with external stakeholders via magic links. Full guest user system with email invites and limited permissions.

**Entities:**
- `WorkItemShareLink` — WorkItemId, CreatedByUserId, Token (random base64), Permission (View / Comment), ExpiresAt?, IsActive, CreatedAt
- `GuestUser` — Email, DisplayName, InvitedByUserId, ProductId, Status (Pending / Active / Revoked), CreatedAt
- `GuestPermission` — GuestUserId, WorkItemId, Permission

**Files:**
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/WorkItemShareLink.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/GuestUser.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/GuestPermission.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/ShareLinkService.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/GuestAccessService.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/ShareLinksController.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/GuestAccessController.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/ShareLinkModal.razor`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/GuestManagementPage.razor`
- `src/UI/DotNetCloud.UI.Web/Pages/GuestLandingPage.razor`

**Deliverables:**
- ☐ WorkItemShareLink entity: generate token, configurable expiry, permission level
- ☐ GuestUser entity: email invite, status lifecycle (pending → active → revoked)
- ☐ GuestPermission entity: per-work-item permissions for guests
- ☐ ShareLinkService: generate/revoke share links, validate tokens
- ☐ GuestAccessService: invite, accept, revoke; resolve effective permissions
- ☐ ShareLinksController: `POST /api/v1/work-items/{id}/share-links` (generate), `DELETE` (revoke)
- ☐ GuestAccessController: invite, list, revoke guests per product
- ☐ Share button in work item detail panel → modal with permission selector and copy link
- ☐ Guest management in product settings: invite form, guest list with status badges, revoke button
- ☐ Guest landing page: token-based access, limited UI showing only shared items
- ☐ Auth: validate guest tokens in middleware, enforce view/comment-only restrictions
- ☐ Notification: guest receives email with magic link on invite

### Step E-3: Product Templates UI (~2.5h)

Entities and services already exist (`ProductTemplate`, `ItemTemplate`, `ProductTemplateService`, `ItemTemplateService`). This step adds the UI layer.

**Files:**
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/ProductTemplatesController.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/TemplatePickerModal.razor`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/ItemTemplatePicker.razor`

**Deliverables:**
- ☐ ProductTemplatesController: `GET /api/v1/product-templates` (list), `GET /api/v1/product-templates/{id}` (detail)
- ☐ TemplatePickerModal.razor: grid of template cards with name, description, category, preview
- ☐ "Create from template" button on product creation page (links to template picker)
- ☐ Template preview: shows swimlane layout, default labels, sample work items
- ☐ ItemTemplatePicker.razor: dropdown in work item creation wizard ("Create from template")
- ☐ Seed 5 built-in templates:
  - **Software Project** — swimlanes: Backlog, To Do, In Progress, Review, Done; labels: bug, feature, improvement, docs
  - **Bug Tracker** — swimlanes: Reported, Triaged, In Fix, Testing, Resolved; labels: critical, high, medium, low
  - **Content Calendar** — swimlanes: Ideas, Drafting, Review, Scheduled, Published; labels: blog, social, video, newsletter
  - **Simple Todo** — swimlanes: To Do, Doing, Done; labels: high, medium, low
  - **Hiring Pipeline** — swimlanes: Sourced, Phone Screen, Onsite, Offer, Hired; labels: engineering, design, product, ops

### Phase E Verification

1. Add reactions to comments → see counts update → remove own reaction
2. Create share link for a work item → open URL in incognito → see limited view → add comment as guest
3. Invite guest via email → accept invite → guest sees only shared items
4. Create product from "Software Project" template → verify swimlanes, labels, item templates are pre-populated

---

## Phase F: Power Tools ✅ COMPLETED

**Estimate:** 16–19 hours · **Depends on:** Phase D (custom fields needed for CSV import column mapping) · **Status: COMPLETED**
**Purpose:** Advanced features for power users and external integrations.

### Step F-1: Command Palette (~4h)

A Ctrl+K command palette like Linear/Jira/VSCode — fast keyboard-driven navigation and actions.

**Files:**
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/TracksCommandPalette.razor` + `.razor.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Services/CommandPaletteService.cs`

**Deliverables:**
- ✓ TracksCommandPalette.razor: modal overlay triggered by Ctrl+K (or Ctrl+P)
- ✓ Fuzzy search across: work items (by number/title), products, sprints, saved views, recent items
- ✓ Quick actions: "New epic in [product]", "Go to my items", "Toggle dark mode", "Open product settings", "Go to dashboard"
- ✓ Keyboard navigation: Ctrl+K open, Esc close, ↑↓ arrows navigate, Enter select
- ✓ Grouped results: Items, Products, Views, Actions (with section headers)
- ✓ Result preview: secondary text line (product name, swimlane, assignee)
- ✓ Recent items tracking: last 10 viewed work items stored in localStorage
- ✓ ICommandPaletteService + CommandPaletteService: aggregates searchable items from DbContext

### Step F-2: CSV Import Wizard (~5h)

Import work items from CSV with a multi-step wizard — field mapping, validation, and batch import.

**Files:**
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/CsvImportWizard.razor` + `.razor.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/CsvImportService.cs`
- (Extend) `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/WorkItemsController.cs`

**Deliverables:**
- ✓ CsvImportWizard.razor: 5-step modal
  - Step 1 — File upload: drag & drop zone, file picker button
  - Step 2 — Parse & preview: auto-detect delimiter (comma, tab, semicolon), show raw first 5 rows
  - Step 3 — Column mapping: dropdown per detected CSV column → map to Title, Description, Priority, Type, Story Points, Assignee (by email), Due Date, Labels, [custom fields from Phase D-1]
  - Step 4 — Validation: show row-level errors (missing required fields, invalid priorities, unknown users, bad dates)
  - Step 5 — Import: progress bar, X of Y items created, summary (success/fail counts)
- ✓ CsvImportService: parse CSV (handles BOM, quoted fields, empty rows), validate rows, batch create via transaction
- ✓ Controller: `POST /api/v1/products/{id}/work-items/import` (multipart form, returns import summary)
- ✓ Duplicate detection: option to skip or error on matching title within same product
- ✓ Dry-run mode: validate without creating (preview-only for user confidence)
- ✓ Chunked import: process in batches of 50 to avoid request timeouts

### Step F-3: Webhooks (~9h)

Production-grade webhook system: HTTP callbacks for external integrations with retry, HMAC signing, and delivery logs.

**Entities:**
- `WebhookSubscription` — ProductId, Url, Secret (HMAC), Events (JSON array), IsActive, CreatedByUserId, LastDeliveryAt?, FailedDeliveryCount
- `WebhookDelivery` — SubscriptionId, EventType, Payload (JSON), ResponseStatusCode?, ResponseBody?, DurationMs, DeliveredAt?, ErrorMessage?, RetryCount

**Files:**
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/WebhookSubscription.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/WebhookDelivery.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Configuration/WebhookSubscriptionConfiguration.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Configuration/WebhookDeliveryConfiguration.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/WebhookService.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/WebhookDeliveryService.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/WebhookRetryBackgroundService.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/WebhooksController.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/WebhookDeliveriesController.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/WebhookManagementPage.razor`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Events/WebhookEventHandler.cs`

**Deliverables:**
- ✓ WebhookSubscription entity: URL, secret, event type filter list, active flag
- ✓ WebhookDelivery entity: full delivery audit trail with timing and response data
- ✓ EF configurations + migrations for both entities
- ✓ WebhookService: CRUD subscriptions, dispatch events to matching subscribers
- ✓ WebhookDeliveryService: execute HTTP POST with HMAC-SHA256 signature in `X-DotNetCloud-Signature` header
- ✓ WebhookRetryBackgroundService: retry failed deliveries with exponential backoff:
  - Intervals: 1 min → 5 min → 15 min → 1 hour → 6 hours → 24 hours → 24 hours (max 7 retries)
  - Uses `PeriodicTimer` every 30 seconds to check for due retries
- ✓ HMAC: SHA-256 HMAC computed from request body + subscription secret; hex-encoded
- ✓ Event types supported:
  - `work_item.created`, `work_item.updated`, `work_item.deleted`
  - `work_item.moved` (swimlane change)
  - `comment.added`
  - `sprint.started`, `sprint.completed`
  - `milestone.reached` (Phase D-2)
- ✓ WebhookEventHandler: subscribes to IEventBus, dispatches to matching webhooks (via IWebhookDispatchService)
- ✓ WebhooksController: `GET/POST/PUT/DELETE /api/v1/products/{id}/webhooks` + test endpoint
- ✓ WebhookDeliveriesController: `GET /api/v1/webhooks/{id}/deliveries` (paginated)
- ✓ UI: Webhook management in product settings — list subscriptions, add/edit form, test button, delivery log
- ✓ IWebhookDispatchService + WebhookDispatchService: bridge between event handler and scoped data services
- ✓ ICsvImportUiService + CsvImportUiService: bridge between UI layer and data layer for CSV import
- ✓ ITracksApiClient extended with webhook methods
- ✓ TracksModule.cs updated to subscribe WebhookEventHandler + MilestoneReachedEvent

### Phase F Verification

1. Ctrl+K → type "my items" → Enter → navigates to kanban filtered to current user
2. Upload CSV with 50 work items → map columns → dry-run validate → fix 2 errors → import → all 50 appear on board
3. Create webhook → click "Test" → see 200 in delivery log → create a work item → see delivery appear with 200 status
4. Configure webhook to a dead URL → create item → see delivery fail → wait → see retry attempts with increasing intervals

---

## Phase G: Planning & Visualization

**Estimate:** 14–17 hours · **Depends on:** Phase D (milestones for roadmap, custom fields for automation)
**Purpose:** Project management views and automation.

### Step G-1: Product Roadmap (~5h)

A new product-level roadmap view showing epics and features on a timeline with dependency arrows. Distinct from the existing sprint-level TimelineView.

**Files:**
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/ProductRoadmapView.razor` + `.razor.cs`
- (Extend) `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/AnalyticsController.cs` (or new `RoadmapController`)
- (Extend) `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/TracksPage.razor` (add `TracksView.Roadmap`)

**Deliverables:**
- ☐ ProductRoadmapView.razor: horizontal timeline, epics/features as colored bars
- ☐ Group by: Epic (default), Sprint, Assignee
- ☐ Color coding: by swimlane color or priority
- ☐ Dependency arrows: curved SVG paths between dependent work items
- ☐ Today marker: vertical dashed line with "Today" label
- ☐ Click item → opens detail panel (reuse existing WorkItemDetailPanel)
- ☐ Drag item bar → changes start date or due date (API call on drop)
- ☐ Zoom toggle: Month / Quarter / Year view
- ☐ `TracksView.Roadmap` enum addition
- ☐ Roadmap icon (🗺️ or similar) in sidebar
- ☐ Controller: `GET /api/v1/products/{id}/roadmap` — returns work items with dates + dependencies
- ☐ Milestone integration: diamond markers on timeline from Phase D-2 milestones
- ☐ Empty state: "No roadmap items. Create epics with due dates to see them here."

### Step G-2: Automation Rules (~5h)

"When X happens, do Y" — simple trigger+condition+action rules evaluated in real-time via the event bus.

**Entity:**
- `AutomationRule` — ProductId, Name, Trigger (enum), Conditions JSON, Actions JSON, IsActive, CreatedByUserId, LastTriggeredAt?

**Files:**
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/AutomationRule.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Configuration/AutomationRuleConfiguration.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/AutomationRuleService.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Events/AutomationRuleEventHandler.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/AutomationRulesController.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/AutomationRuleEditor.razor`

**Trigger types:**
| Trigger | Fires when |
|---------|------------|
| `work_item_created` | New work item created |
| `work_item_moved` | Work item moves to a different swimlane |
| `status_changed` | Any field change triggers (swimlane, priority, assignee) |
| `due_date_approaching` | Due date is within 24 hours |
| `assigned` | Work item assigned to a user |

**Action types:**
| Action | Effect |
|--------|--------|
| `set_field` | Set a custom field value (Phase D-1) |
| `add_label` | Add a label to the work item |
| `remove_label` | Remove a label from the work item |
| `notify` | Send notification to specified users/roles |
| `move_to_swimlane` | Move work item to a specific swimlane |
| `assign` | Assign to a specific user |
| `set_priority` | Change priority level |
| `add_comment` | Post a system comment on the work item |

**Deliverables:**
- ☐ AutomationRule entity with EF config + migration
- ☐ AutomationRuleService: CRUD + `EvaluateRulesAsync(triggerType, workItem, context)` — returns list of actions to execute
- ☐ AutomationRuleEventHandler: subscribes to IEventBus, evaluates matching rules, executes actions
- ☐ AutomationRulesController: `GET/POST/PUT/DELETE /api/v1/products/{id}/automation-rules`
- ☐ AutomationRuleEditor.razor: rule builder in product settings
  - Trigger dropdown with human-readable labels
  - Condition builder: "When [field] [operator] [value]" — supports custom fields from D-1
  - Action builder: "Then [action] [parameters]" — dynamic parameters per action type
  - Rule preview: natural language summary ("When work item is moved to Done, add label ✓")
- ☐ 3 pre-built template rules:
  - "When moved to Done, mark as archived"
  - "When urgent item created, notify all product members"
  - "When due date is tomorrow, notify assignee"
- ☐ Rule execution logged in Activity audit trail
- ☐ Toggle to enable/disable individual rules

### Step G-3: Goals / OKRs (~3h)

Objectives and Key Results linked to work items — track progress toward outcomes.

**Entity:**
- `Goal` — ProductId, Title, Description, Type (Objective / KeyResult), ParentGoalId? (for KR nesting), TargetValue?, CurrentValue?, ProgressType (Manual / Automatic), Status (NotStarted / OnTrack / AtRisk / Behind / Completed), DueDate?, CreatedByUserId

**Files:**
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/Goal.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Configuration/GoalConfiguration.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/GoalService.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/GoalsController.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/GoalsList.razor`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/GoalDetail.razor`

**Deliverables:**
- ☐ Goal entity with EF config + migration
- ☐ Self-referencing hierarchy: Objective → Key Results (ParentGoalId)
- ☐ GoalService: CRUD + progress calculation (manual update or automatic from linked work items)
- ☐ GoalsController: `GET/POST/PUT/DELETE /api/v1/products/{id}/goals`
- ☐ GoalsList.razor: hierarchical list with expand/collapse, progress bars, status badges
- ☐ GoalDetail.razor: detail panel with title, description, KRs, progress updates, linked work items
- ☐ Link work items to key results: "This work item contributes to KR X"
- ☐ Automatic progress: when target value is set, progress = linked work items completed / total
- ☐ Goals widget on product dashboard (Phase C-2): compact list of objectives with progress
- ☐ Status auto-computation: OnTrack (≥80% on pace), AtRisk (50-79%), Behind (<50%)

### Step G-4: Capacity Planning (~3h)

See team member workload at a glance — who's overloaded, who has capacity.

**Files:**
- (Extend) `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/AnalyticsService.cs`
- (Extend) `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/AnalyticsController.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/CapacityWidget.razor`

**Deliverables:**
- ☐ Extend AnalyticsService:
  - `GetSprintCapacityAsync(sprintId)` — total story points assigned vs. sprint target
  - `GetMemberCapacityAsync(productId)` — story points per assignee across active sprints
  - `GetAvailableCapacityAsync(productId)` — members with <X story points assigned (configurable threshold)
- ☐ Extend AnalyticsController: `GET /api/v1/products/{id}/analytics/capacity`
- ☐ CapacityWidget.razor: horizontal bar chart — one bar per member, story points on x-axis
- ☐ Color coding: green (under 60% capacity), yellow (60–90%), red (90%+), dark red (>100%)
- ☐ Member name + avatar on y-axis, story points count on bar
- ☐ Tooltip on hover: assigned items list, sprint breakdown
- ☐ Widget placement: product dashboard grid
- ☐ Sprint selector: toggle between "All active sprints" and specific sprint view

### Phase G Verification

1. Open roadmap → see epics on timeline with dependency arrows → click → detail panel opens → drag bar → date updates
2. Create automation rule "When moved to Done, add label ✓" → move a work item to Done → see ✓ label appear automatically
3. Create objective "Launch v1" + 3 key results → link work items to KRs → complete items → see progress auto-update to 66%
4. Check capacity widget → assign 15 more story points to overloaded member → see bar turn deep red → shows 150% capacity

---

## Phase H: Polish & Constraints

**Estimate:** 8–10 hours · **Depends on:** All previous phases (audit covers new components)
**Purpose:** Quality-of-life improvements and workflow constraint enforcement.

### Step H-1: Dark Mode Enhancements (~3h)

CSS variables from `app.css` provide a dark mode foundation, but all 48 Tracks components need a thorough audit pass — especially new components from Phases D–G.

**Files to audit (all Tracks .razor.css files):**
- `TracksPage.razor.css` — sidebar, main layout
- `KanbanBoard.razor.css` — card colors, swimlane headers
- `WorkItemDetailPanel.razor.css` — comment area, fields
- `ProductDashboardView.razor.css` — chart colors
- `WorkItemListView.razor.css` — table rows, inline edit
- `WorkItemCalendarView.razor.css` — day grid
- `TimelineView.razor.css` — Gantt bars
- `ProductSettingsPage.razor.css` — forms
- `SprintPlanningView.razor.css`
- `SprintBurndownChart.razor.css`
- `VelocityChart.razor.css`
- `ReviewSessionHost.razor.css`
- Plus all new component CSS files from Phases D–G

**Known problem areas:**
- Kanban card colors may be too dark (add lighter border/tint in dark mode)
- Swimlane headers with colored backgrounds may clash with dark surface
- Mention highlighting (`@username`) contrast in dark mode
- Emoji picker background and hover states
- Label color chips: dark labels (navy, dark green, maroon) need lightened versions in dark mode
- Code blocks and markdown in comments: ensure `--color-code-bg` is set for dark
- CSV import wizard: step indicator, drop zone border
- Command palette: overlay background opacity, highlighted result row
- Webhook delivery log: status code badges

**Deliverables:**
- ☐ Audit every Tracks view in dark mode
- ☐ Add explicit `:global(.dark-mode)` overrides where CSS variables are insufficient
- ☐ Label color contrast: compute WCAG AA-compliant text colors for dark labels
- ☐ Code blocks in comments: ensure background and syntax colors work in dark mode
- ☐ Chart colors in dashboard: ensure donut/bar chart segments are visible on dark background
- ☐ Empty state illustrations: verify they work on dark backgrounds
- ☐ Verification: toggle dark mode, browse all views, document zero issues found

### Step H-2: Custom Swimlane Transition Rules (~3h)

Define which swimlanes can transition to which — enforce workflow rules like "Can't skip from Backlog to Done."

**Entity:**
- `SwimlaneTransitionRule` — ProductId, FromSwimlaneId, ToSwimlaneId, IsAllowed

**Files:**
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/SwimlaneTransitionRule.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Configuration/SwimlaneTransitionRuleConfiguration.cs`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/SwimlaneTransitionService.cs`
- (Extend) `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/WorkItemService.cs`
- (Extend) `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/SwimlanesController.cs`
- (Extend) `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/ProductSettingsPage.razor` (add transition matrix tab)

**Deliverables:**
- ☐ SwimlaneTransitionRule entity with EF config + migration
- ☐ SwimlaneTransitionService: validate move, CRUD rules, build transition matrix
- ☐ Controller: extend SwimlanesController with `GET/PUT /api/v1/products/{id}/swimlane-transitions`
- ☐ UI: Transition matrix in product settings
  - Grid layout: from-swimlane rows × to-swimlane columns
  - Checkbox in each cell to allow/disallow the transition
  - Default: all transitions allowed (backward compatible)
  - Quick presets: "Linear only" (each column can only move to the next), "Allow all", "Restrict to forward only"
- ☐ Enforcement: `WorkItemService.MoveWorkItemAsync` checks transition rules
- ☐ Error response: `409 Conflict` with message "Cannot move from '{from}' to '{to}'. Allowed transitions: {list}."
- ☐ Drag-and-drop: invalid drop targets dimmed in kanban when transition rules are active

### Step H-3: Column Constraints / WIP Limits (~2h)

`Swimlane.CardLimit` already exists as an informational field. This step adds enforcement.

**Files:**
- (Extend) `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/WorkItemService.cs`
- (Extend) `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/KanbanBoard.razor`
- (Extend) `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/ProductSettingsPage.razor`

**Deliverables:**
- ☐ Enforcement: when moving item to swimlane, check `CardLimit`
- ☐ Soft enforcement (default): warn with toast "This column is at its limit of {N} items. Add anyway?" with confirm/cancel
- ☐ Hard enforcement option: product setting "Enforce WIP limits strictly" — blocks moves that exceed limit
- ☐ Swimlane header: shows "3/5" count with color indicator
  - Green: under 70% of limit
  - Yellow: 70–99% of limit
  - Red: 100%+ of limit
- ☐ Warning toast: appears when limit is exactly at or exceeded, auto-dismisses after 5 seconds
- ☐ CardLimit editor: number input in swimlane settings (already exists, verify it works)
- ☐ "Unlimited" option: CardLimit = 0 means no limit enforced

### Phase H Verification

1. Toggle dark mode → browse kanban, backlog, list, calendar, roadmap, dashboard, settings, detail panel → no unreadable text, no invisible elements, no harsh contrast
2. Configure transition matrix: only allow To Do → In Progress → Review → Done → move item from To Do directly to Done → see 409 error
3. Set WIP limit to 3 on a swimlane → add 4th item → see warning toast → configure strict mode → blocked with error message

---

## Phase I: Onboarding Tour ✅ COMPLETED

**Estimate:** 5 hours · **Depends on:** ALL phases (covers everything) · **Status: COMPLETED**
**Purpose:** Guided first-time experience that shows users every feature — from basic kanban to advanced roadmap, automation, and webhooks.

### Step I-1: Onboarding Tour Framework (~2h)

**Files:**
- ✓ `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/OnboardingTour.razor` + `.razor.cs`
- ✓ `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/TourTooltip.razor` + `.razor.cs`
- ✓ `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Services/OnboardingStateService.cs`
- ✓ `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/OnboardingTour.razor.css` (styles)
- ✓ `src/UI/DotNetCloud.UI.Web/wwwroot/js/tracks-tour.js` (JS interop)
- ✓ (Extend) `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/TracksPage.razor` (mount tour)
- ✓ (Extend) `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/TracksPage.razor.cs` (tour integration)
- ✓ (Extend) `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/TracksPage.razor.css` (help menu styles)
- ✓ (Extend) `src/UI/DotNetCloud.UI.Web/Components/App.razor` (JS script reference)

**Deliverables:**
- ✓ OnboardingTour.razor: manages tour state machine, step progression, overlay rendering
- ✓ TourTooltip.razor: positioned tooltip with title, description, next/prev/skip buttons, step counter ("N of 10")
- ✓ Semi-transparent overlay: highlights the target element, dims everything else
- ✓ Tooltip positioning: auto-calculated (above/below/left/right of target) based on viewport space via JS interop
- ✓ Auto-scroll: if target element is not in viewport, smooth-scroll to it before showing tooltip
- ✓ OnboardingStateService: localStorage-based persistence
  - ✓ `IsCompletedAsync(userId, tourId)` → bool
  - ✓ `GetCurrentStepAsync(userId, tourId)` → int
  - ✓ `SetStepAsync(userId, tourId, step)` → void
  - ✓ `MarkCompletedAsync(userId, tourId)` → void
  - ✓ `ResetAsync(userId, tourId)` → void (for restart)
- ✓ TracksPage.razor: loads tour state on mount, triggers tour if not completed for this user
- ✓ Progress persistence: user can close mid-tour → next visit resumes from last step
- ✓ "Skip tour" button always visible during tour
- ✓ "Restart tour" option in help menu (? icon in breadcrumb bar)
- ✓ Help menu dropdown with keyboard shortcuts link

### Step I-2: Tour Content & Steps (~3h)

**10-step guided tour covering every feature:**

| Step | Title | Target Element | Content |
|------|-------|---------------|---------|
| ✓ 1 | Welcome | Center screen (no target) | "Welcome to DotNetCloud Tracks! Let's take a quick tour to get you familiar with everything. (3 minutes)" |
| ✓ 2 | Products | Product list in sidebar | "Products are your project containers. Each product has its own boards, sprints, and settings." |
| ✓ 3 | Kanban Board | Kanban board main area | "This is your Kanban board. Swimlanes represent workflow stages. Drag cards between columns to update their status." |
| ✓ 4 | Creating Work Items | + button in toolbar | "Click the + button to create work items. Tracks supports Epics, Features, Items, and Sub-Items in a hierarchy." |
| ✓ 5 | Work Item Details | Detail panel (auto-opens sample item) | "The detail panel shows description, comments, attachments, assignments, labels, custom fields, watchers, and dependencies." |
| ✓ 6 | Views | View switcher icons in sidebar | "Switch between Kanban, List, Calendar, Dashboard, Roadmap, and Settings. Each gives a different perspective." |
| ✓ 7 | Sprints | Sprint panel in sidebar | "Sprints are time-boxed iterations. Plan sprints from the backlog, track progress with burndown charts, and review velocity." |
| ✓ 8 | Filters & Search | Filter bar + Ctrl+K hint | "Filter by text, priority, label, or sprint. Save filters as Custom Views. Ctrl+K for command palette." |
| ✓ 9 | Product Settings | Settings gear icon | "Configure swimlanes, labels, members, custom fields, automation rules, webhooks, templates, and more." |
| ✓ 10 | Done | Center screen | "You're all set! Create your first work item or explore the dashboard. Replay this tour anytime from the help menu." |

**Deliverables:**
- ✓ Step 1: Welcome overlay (centered, no highlight), "Start Tour" button
- ✓ Step 2: Highlight product sidebar, tooltip positioned to right
- ✓ Step 3: Highlight kanban board, explain drag-and-drop
- ✓ Step 4: Highlight create button in toolbar
- ✓ Step 5: Programmatically open a sample work item (or first available), highlight detail panel
- ✓ Step 6: Highlight view switcher icons in sidebar
- ✓ Step 7: Highlight sprint panel in sidebar
- ✓ Step 8: Highlight filter/search bar, mention Ctrl+K shortcut
- ✓ Step 9: Highlight settings gear icon
- ✓ Step 10: Celebration overlay with emoji animation
- ✓ Each step: Prev / Next / Skip buttons, "N of 10" progress indicator

### Phase I Verification

1. Clear localStorage onboarding state → refresh Tracks → tour starts automatically
2. Click through all 10 steps → verify each targets the correct UI element
3. Close browser mid-tour (at step 4) → reopen → tour resumes at step 5
4. Complete tour → refresh → tour does NOT restart
5. Click help menu → "Restart tour" → tour starts from step 1
6. Verify tour works in both light and dark mode
7. Verify tour works on mobile viewport (320px wide)

---

## Summary

| Phase | Description | Steps | Est. Hours | Depends On |
|-------|-------------|-------|------------|------------|
| **D** | Data Foundation | Custom fields, Milestones, Recurring | 12–14 | — |
| **E** | Collaboration & Sharing | Reactions, Guest access, Templates UI | 12–14 | — |
| **F** | Power Tools | Command palette, CSV import, Webhooks | 16–19 | D (custom fields → CSV mapping) |
| **G** | Planning & Visualization | Roadmap, Automation, Goals/OKRs, Capacity | 14–17 | D (milestones, custom fields → automation) |
| **H** | Polish & Constraints | Dark mode audit, Transition rules, WIP limits | 8–10 | All previous (audit) |
| **I** | Onboarding Tour | Tour framework + 10-step content | 5 | ALL (covers everything) | ✅ COMPLETED |
| **—** | **Deferred** | Mobile notifications | — | → `docs/PHASE_MOBILE_NOTIFICATIONS_PLAN.md` |

| | |
|---|---|
| **Total** | **67–79 hours** |
| **Files created/modified** | ~60 files across 3 Tracks projects |
| **New entities** | 12 (CustomField, WorkItemFieldValue, Milestone, RecurringRule, CommentReaction, WorkItemShareLink, GuestUser, GuestPermission, WebhookSubscription, WebhookDelivery, AutomationRule, SwimlaneTransitionRule, Goal) |
| **New controllers** | 8 |
| **New UI components** | ~18 Razor components |
| **New background services** | 2 (RecurringWorkItem, WebhookRetry) |

---

## Build & Deploy Strategy

Each phase should be built, tested, deployed, and user-tested before moving to the next:

1. **Phase D** → Build → `dotnet build DotNetCloud.CI.slnf` → Deploy → Test
2. **Phase E** → Build → Deploy → Test
3. **Phase F** → Build → Deploy → Test
4. **Phase G** → Build → Deploy → Test
5. **Phase H** → Build → Deploy → Test
6. **Phase I** → Build → Deploy → Test

**Verification:** After each step within a phase: `dotnet test DotNetCloud.CI.slnf`

---

## Related Documents

- `docs/TRACKS_PROFESSIONALIZATION_PLAN.md` — Phases A–C (completed)
- `docs/TRACKS_COMPREHENSIVE_FEATURE_ANALYSIS.md` — Competitive research basis
- `docs/PHASE_MOBILE_NOTIFICATIONS_PLAN.md` — Deferred mobile push notifications plan (TBD)
