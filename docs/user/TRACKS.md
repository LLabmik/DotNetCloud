# Tracks — User Guide

> **Last Updated:** 2026-08-27

---

## Welcome

DotNetCloud Tracks is a project management tool with Kanban boards, sprints, and team collaboration. Use it to organize work visually, track progress, and plan iterations.

---

## Getting Started

### Creating Your First Board

1. Open **Tracks** from the sidebar
2. Click **New Board**
3. Enter a board name and optional description
4. Choose visibility: **Public** (all organization members can see) or **Private** (invite only)
5. Click **Create**

Your board starts with no columns. Add columns like "To Do", "In Progress", and "Done" to define your workflow.

---

## Boards

### Managing Swimlanes (Columns)

- **Add a swimlane:** click **+ Add Swimlane** at the right edge of the board
- **Rename a swimlane:** click the swimlane title and edit inline
- **Reorder swimlanes:** drag a swimlane header to a new position
- **Delete a swimlane:** open the swimlane menu → Delete (cards are not deleted, they become unassigned)

### Board Members

Access **Board Settings → Members** to manage who can see and edit the board.

| Role       | Can View | Can Edit Cards | Can Manage Swimlanes | Can Manage Members | Can Delete Board |
| ---------- | -------- | -------------- | -------------------- | ------------------ | ---------------- |
| **Owner**  | ✓        | ✓              | ✓                    | ✓                  | ✓                |
| **Admin**  | ✓        | ✓              | ✓                    | ✓                  | ✗                |
| **Member** | ✓        | ✓              | ✗                    | ✗                  | ✗                |
| **Viewer** | ✓        | ✗              | ✗                    | ✗                  | ✗                |

### Board Labels

Create color-coded labels for categorizing cards:

1. Go to **Board Settings → Labels**
2. Click **Create Label**
3. Choose a color and name (e.g., "Bug", "Feature", "Urgent")
4. Apply labels to cards from the card detail view

### Transfer Ownership

Board owners can transfer ownership to another member via **Board Settings → Transfer Ownership**.

### Import / Export

- **Export:** download a board as JSON, including all swimlanes, cards, labels, and members
- **Import:** upload a JSON file to create a new board from exported data

---

## Cards

### Creating Cards

1. Click **+ Add card** at the bottom of any swimlane
2. Enter a title and press **Enter**
3. Click the card to open the detail view for adding description, assignees, labels, due date, and more

### Card Detail View

| Field             | Description                                            |
| ----------------- | ------------------------------------------------------ |
| **Title**         | Card name, displayed on the board                      |
| **Description**   | Rich text (Markdown) description                       |
| **Assignees**     | One or more users responsible for the card             |
| **Labels**        | Color tags for categorization                          |
| **Due Date**      | Deadline with optional reminder notification           |
| **Priority**      | None, Low, Medium, High, or Urgent                     |
| **Checklists**    | Task lists with progress tracking                      |
| **Comments**      | Discussion thread on the card                          |
| **Attachments**   | Files linked from the Files module                     |
| **Dependencies**  | Links to other cards this card blocks or is blocked by |
| **Time Tracking** | Logged time entries and live timer                     |
| **Activity**      | History of all changes to the card                     |

### Moving Cards

- **Drag and drop** a card between swimlanes
- **Card menu → Move** to select a target swimlane and position

### Archiving Cards

Archived cards are hidden from the board but not deleted. Use **Card menu → Archive** or the bulk archive operation.

---

## Checklists

Add task lists to any card to track sub-items:

1. Open a card → click **Add Checklist**
2. Name the checklist (e.g., "Acceptance Criteria")
3. Add items one at a time
4. Check items off as they are completed
5. Progress is shown as a percentage bar on the card

You can have multiple checklists per card.

---

## Comments

Discuss cards with your team:

- Comments support **Markdown** formatting
- Edit your own comments after posting
- Delete comments you own (admins can delete any comment)
- The activity feed records when comments are added

---

## Labels

Labels help categorize and filter cards across the board:

- Each board has its own label set
- Labels have a **color** and optional **name**
- A card can have multiple labels
- Use labels to highlight priority, type, category, or team

---

## Dependencies

Link cards to show relationships:

| Type           | Meaning                                                         |
| -------------- | --------------------------------------------------------------- |
| **Blocked By** | This card cannot start until the dependency is complete         |
| **Blocks**     | This card must be completed before the dependent card can start |
| **Related To** | Informational link between related cards                        |

Tracks automatically detects and prevents circular dependencies (A blocks B blocks C blocks A).

---

## Sprints

Sprints are time-boxed iterations for boards using agile methodology.

### Sprint Lifecycle

1. **Create Sprint:** set a name, start date, and end date
2. **Add Cards:** assign cards from the backlog to the sprint
3. **Start Sprint:** activates the sprint (only one sprint can be active per board)
4. **Work:** move cards through swimlanes as work progresses
5. **Complete Sprint:** ends the sprint; unfinished cards can be moved to the next sprint

### Sprint Reports

After completing a sprint, view the sprint report:

- **Completed cards** — cards that reached the "Done" state
- **Incomplete cards** — cards that were not finished
- **Velocity** — story points completed per sprint
- **Burndown** — remaining work over time

---

## Time Tracking

### Manual Entries

1. Open a card → **Time Tracking** section
2. Click **Add Entry**
3. Enter hours/minutes and an optional description

### Timer

1. Click **Start Timer** on a card
2. Work on the task
3. Click **Stop Timer** — the elapsed time is automatically logged

Only one timer can be active per user at a time.

---

## Planning Poker

Estimate card complexity collaboratively:

1. Open a card → click **Start Poker Session**
2. All board members see the estimation session
3. Each member submits their estimate (story points)
4. When ready, click **Reveal Votes** to show all estimates
5. Discuss outliers
6. **Accept** the agreed estimate, or **Start New Round** to re-vote

The accepted estimate is stored on the card and used in sprint velocity calculations.

---

## Teams

Teams group users for shared board access:

1. **Create a Team** from the Teams page
2. **Add Members** with roles: Owner, Manager, Member, or Guest
3. **Assign Boards** to the team — members get access based on their team role

### Team Roles

| Role        | Team Management         | Create Boards | Edit Cards | View Only |
| ----------- | ----------------------- | ------------- | ---------- | --------- |
| **Owner**   | ✓                       | ✓             | ✓          | ✓         |
| **Manager** | Partial (below manager) | ✓             | ✓          | ✓         |
| **Member**  | ✗                       | ✗             | ✓          | ✓         |
| **Guest**   | ✗                       | ✗             | ✗          | ✓         |

A user's direct board role (if any) takes precedence over their team role.

---

## Bulk Operations

Perform batch actions on multiple cards after selecting them:

| Operation        | Description                              |
| ---------------- | ---------------------------------------- |
| **Bulk Move**    | Move selected cards to a target swimlane |
| **Bulk Assign**  | Assign a user to multiple cards          |
| **Bulk Label**   | Apply a label to multiple cards          |
| **Bulk Archive** | Archive multiple cards at once           |

---

## Templates

### Board Templates

1. Open the board menu → **Save as Template**
2. The template captures swimlanes, labels, and board settings (not cards)
3. Create new boards from the template via **New Board → From Template**

### Card Templates

1. Open a card menu → **Save as Template**
2. The template captures title, description, checklists, and labels
3. When creating a new card, choose **From Template** to pre-fill fields

---

## Activity Feed

Every board and card maintains an activity log showing:

- Who performed the action
- What changed (created, updated, moved, assigned, commented, etc.)
- When it happened

Access the activity feed from the board sidebar or the card detail view.

---

## Analytics

### Board Analytics

View aggregate metrics for a board:

- Total cards, cards per swimlane, cards per member
- Average time in each swimlane (cycle time)
- Cards created vs. completed over time

### Sprint Velocity

Track team productivity across sprints:

- Story points completed per sprint
- Velocity trend line
- Sprint-over-sprint comparison

### Team Analytics

Organization-level metrics across all team boards:

- Active boards and members
- Total cards in progress
- Completion rates by team

---

## Notifications

Tracks sends notifications for key events:

| Event                    | Notified Users              |
| ------------------------ | --------------------------- |
| Assigned to card         | The assigned user           |
| Comment on your card     | Card assignees and watchers |
| Due date approaching     | Card assignees              |
| Sprint started/completed | All board members           |
| Added to board/team      | The added user              |

Notifications appear in the DotNetCloud notification panel.

---

## Keyboard Shortcuts

| Shortcut | Action                       |
| -------- | ---------------------------- |
| `N`      | New card in focused list     |
| `E`      | Edit focused card            |
| `M`      | Move focused card            |
| `L`      | Add label to focused card    |
| `D`      | Set due date on focused card |
| `Esc`    | Close card detail view       |

---

## Related Guides

- [Search](SEARCH.md) — find cards and content across modules
- [Getting Started](GETTING_STARTED.md) — general platform basics
