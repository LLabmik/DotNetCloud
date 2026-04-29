# Tracks Hierarchy Clarity & Creation Wizards Plan

## Problem
The Tracks hierarchy (Product > Epic > Feature > Item > SubItem) is invisible to users. All kanban boards look identical at every level — just swimlanes with cards. There's no guided creation flow. Even administrators can't tell what level they're viewing.

## Solution Overview

### 1. Visual Hierarchy Indicators (KanbanBoard.razor)
- **Level banner** at the top of each kanban showing "Epics", "Features", "Items", or "SubItems"
- **Depth styling** — deeper levels get progressive indentation and subtle border color shifts
- **Type badges** on cards showing the work item type (Epic/Feature/Item/SubItem)
- **Enhanced breadcrumb** with hierarchy depth indicator

### 2. Product Creation Wizard (replaces modal)
- **Step 1:** Name & Description
- **Step 2:** Color & Default Swimlanes (pre-populate with To Do/In Progress/Done)
- **Step 3:** Team Members (invite members by user search)
- **Step 4:** Review & Create

### 3. WorkItem Creation Wizard
- **Step 1:** Type Selection & Title
- **Step 2:** Details (Description, Priority, Story Points, Due Date, Swimlane)
- **Step 3:** Assignments & Labels
- **Step 4:** Review & Create

Context-aware: opens with the correct type pre-selected based on current view level.
Product Kanban → Epic, Epic Kanban → Feature, Feature Kanban → Item, Item Kanban → SubItem.

### 4. Files to Create
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/ProductCreationWizard.razor` — New
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/ProductCreationWizard.razor.cs` — New
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/WorkItemCreationWizard.razor` — New
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/WorkItemCreationWizard.razor.cs` — New

### 5. Files to Modify
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/KanbanBoard.razor` — Level indicator, depth styling, type badges, wizard integration
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/ProductListView.razor` — Use ProductCreationWizard instead of modal
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/TracksPage.razor` — Pass hierarchy context to kanban
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/TracksPage.razor.cs` — Handle wizard events

## Visual Hierarchy Design

### Level Banner
```
┌──────────────────────────────────────────┐
│ 🏗️ Epics · Product Name                 │  ← Level indicator banner
│ ─────────────────────────────────────── │
│ [Filter bar]                    [Refresh]│
│ ─────────────────────────────────────── │
│ [Swimlane 1]  [Swimlane 2]  [Swimlane 3]│
│ [Card]        [Card]        [Card]       │
└──────────────────────────────────────────┘
```

### Card Type Badge
```
┌──────────────────┐
│ #42  Epic  🔴     │  ← Type badge on card header
│ Card Title        │
│ 📅 Apr 30  3 SP   │
└──────────────────┘
```

### Depth Styling
- Product level: normal white cards
- Epic level: subtle left colored border
- Feature level: slightly indented, lighter border
- Item/SubItem level: further indented, muted border
