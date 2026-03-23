# Phase 3 Deferred Items — Implementation Plan

> Scope: All ☐ items remaining in `docs/PHASE_3_IMPLEMENTATION_PLAN.md` after Phase 3.1–3.8 completion.
>
> Prerequisite: Phase 3 backend is fully implemented — REST APIs, gRPC services, CardDAV/CalDAV, data models, import pipeline, and 2,700+ tests all pass.

---

## Deferred Items Summary

| # | Item | Source Section | Complexity |
|---|------|---------------|------------|
| D1 | Contacts UI — full CRUD wiring | 3.2 Exit Criteria | High |
| D2 | Calendar UI — views + event management | (implied by 3.5) | High |
| D3 | Notes UI — editor, folders, tags, versions | (implied by 3.5) | High |
| D4 | Audit trail columns on PIM entities + migration | 3.2 Exit Criteria | Medium |
| D5 | Shared notification patterns for PIM modules | 3.5 Deliverables | Medium |
| D6 | Contacts reverse cross-links (surface related notes/events) | 3.5 Deliverables | Medium |
| D7 | Cross-module link rendering in UI | 3.5 Exit Criteria | Low |

---

## Dependency Graph

```
D4 (Audit Columns)         — no dependencies, can start immediately
D5 (Notifications)         — no dependencies, can start immediately
D6 (Reverse Cross-Links)   — no dependencies, can start immediately
D1 (Contacts UI)           — benefits from D4, D6, D7 but not blocked
D2 (Calendar UI)           — benefits from D4, D7 but not blocked
D3 (Notes UI)              — benefits from D4, D7 but not blocked
D7 (Link Rendering in UI)  — depends on D6 (reverse links must resolve)
```

**Recommended order:** D4 → D5 → D6 → D7 → D1 → D2 → D3

---

## D1: Contacts UI — Full CRUD Wiring

**Goal:** Replace the ContactsPage shell with a working UI that calls the Contacts REST API.

**Current state:** `ContactsPage.razor` has sidebar nav (All/Groups/Recently Added), search input, and a "New Contact" button — all with placeholder handlers and no data binding.

**REST endpoints available (ContactsController):**
- `GET /api/v1/contacts` — paginated list with search
- `GET /api/v1/contacts/{id}` — single contact detail
- `POST /api/v1/contacts` — create
- `PUT /api/v1/contacts/{id}` — update
- `DELETE /api/v1/contacts/{id}` — soft-delete
- `GET /api/v1/contacts/groups` — list groups
- `GET /api/v1/contacts/groups/{id}/members` — group members
- `GET /api/v1/contacts/{id}/avatar` — avatar image
- `PUT /api/v1/contacts/{id}/avatar` — upload avatar
- `GET /api/v1/contacts/{id}/shares` — list shares
- `POST /api/v1/contacts/{id}/shares` — create share

### Tasks

- ✓ **D1.1** Create `IContactsApiClient` HTTP service in `DotNetCloud.Modules.Contacts` (typed HttpClient calling REST endpoints).
- ✓ **D1.2** Contact list component — paginated table/card view with search, loading states, empty states.
- ✓ **D1.3** Contact detail panel — display all fields (name, emails, phones, addresses, custom fields, avatar).
- ✓ **D1.4** Create/Edit contact form — validated form with field sections matching the DTO structure.
- ✓ **D1.5** Delete contact — confirmation dialog, soft-delete call, list refresh.
- ✓ **D1.6** Contact groups panel — list groups, show members, add/remove members.
- ✓ **D1.7** Contact sharing dialog — share with user, permission level (ReadOnly/ReadWrite), revoke.
- ✓ **D1.8** Avatar display + upload — show avatar in detail view, upload via file input.
- ✓ **D1.9** Wire sidebar sections (All Contacts, Groups, Recently Added) to filtered API calls.
- ✓ **D1.10** Tests — component tests or integration tests for the UI service layer.

### Exit Criteria
- User can list, search, create, edit, delete, and share contacts entirely from the Blazor UI.
- Groups and avatars are functional.
- No placeholder text remains in ContactsPage.

---

## D2: Calendar UI — Views + Event Management

**Goal:** Replace the CalendarPage shell with month/week/day/agenda views and event CRUD.

**Current state:** `CalendarPage.razor` has view mode toggles (Month/Week/Day/Agenda), "My Calendars" stub, and a "New Event" button — all placeholders.

**REST endpoints available (CalendarController):**
- `GET /api/v1/calendars` — list calendars
- `POST /api/v1/calendars` — create calendar
- `PUT /api/v1/calendars/{id}` — update calendar
- `DELETE /api/v1/calendars/{id}` — soft-delete
- `GET /api/v1/calendars/{calendarId}/events` — events with date range filter
- `GET /api/v1/calendars/events/{id}` — single event
- `POST /api/v1/calendars/events` — create event
- `PUT /api/v1/calendars/events/{id}` — update event
- `DELETE /api/v1/calendars/events/{id}` — soft-delete
- `POST /api/v1/calendars/events/{id}/rsvp` — RSVP
- `GET /api/v1/calendars/events/search` — search
- `GET /api/v1/calendars/{id}/shares` — shares
- `POST /api/v1/calendars/{calendarId}/export` — iCal export
- `POST /api/v1/calendars/{id}/import` — iCal import

### Tasks

- ✓ **D2.1** Create `ICalendarApiClient` HTTP service (typed HttpClient).
- ✓ **D2.2** Calendar sidebar — list user's calendars with color indicators, toggle visibility, create/edit/delete calendars.
- ✓ **D2.3** Month view component — list-based, events with date navigation (prev/next month).
- ✓ **D2.4** Week view component — list-based, events filtered by 7-day range.
- ✓ **D2.5** Day view component — list-based, events filtered by single day.
- ✓ **D2.6** Agenda view component — chronological event list for 30-day range.
- ✓ **D2.7** Event detail panel — display title, time, location, attendees, description.
- ✓ **D2.8** Create/Edit event form — date pickers, location, description fields.
- ✓ **D2.9** RSVP flow — accept/decline/tentative buttons on event rows.
- ✓ **D2.10** Calendar sharing dialog — share with user/team, permission level, revoke.
- ✓ **D2.11** Import/Export — iCal text import panel, export button for calendar.
- ✓ **D2.12** Tests for the calendar UI service layer.

### Exit Criteria
- All four view modes render events from the API.
- User can create, edit, delete, RSVP, share, import, and export entirely from the Blazor UI.
- Recurring events display expanded occurrences in all views.

---

## D3: Notes UI — Editor, Folders, Tags, Versions

**Goal:** Replace the NotesPage shell with a working note editor, folder tree, tag management, and version history.

**Current state:** `NotesPage.razor` has sidebar nav (All Notes, Folders, Tags, Favorites, Shared with Me), search input, and "New Note" button — all placeholders. `MarkdownEditor.razor` component (with CSS) exists separately.

**REST endpoints available (NotesController):**
- `GET /api/v1/notes` — list with folder filter
- `GET /api/v1/notes/{id}` — single note
- `POST /api/v1/notes` — create
- `PUT /api/v1/notes/{id}` — update
- `DELETE /api/v1/notes/{id}` — soft-delete
- `GET /api/v1/notes/search` — search by title/content/tags
- `GET /api/v1/notes/{id}/versions` — version history
- `POST /api/v1/notes/{id}/versions/{versionId}/restore` — restore version
- `GET /api/v1/notes/folders` — list folders
- `POST /api/v1/notes/folders` — create folder
- `PUT /api/v1/notes/folders/{id}` — update folder
- `DELETE /api/v1/notes/folders/{id}` — delete folder
- `GET /api/v1/notes/{id}/shares` — list shares
- `POST /api/v1/notes/{id}/shares` — share note
- `GET /api/v1/notes/{id}/preview` — rendered HTML preview
- `POST /api/v1/notes/render` — live Markdown rendering

### Tasks

- ✓ **D3.1** Create `INotesApiClient` HTTP service (typed HttpClient).
- ✓ **D3.2** Note list component — sortable list with title, folder, tags, last-modified, loading/empty states.
- ✓ **D3.3** Note editor panel — integrate `MarkdownEditor.razor` component, bind to note content, explicit save.
- ✓ **D3.4** Create/Edit note metadata — title, folder assignment, tag management (CSV input).
- ✓ **D3.5** Folder tree sidebar — list folders, create/rename/delete with inline form.
- ✓ **D3.6** Tag management — tag CSV in editor, tag display in detail view.
- ✓ **D3.7** Version history panel — list versions with timestamps, restore button.
- ✓ **D3.8** Note sharing dialog — share with user, permission level, revoke.
- ✓ **D3.9** "Shared with Me" view — nav section for shared notes.
- ✓ **D3.10** "Favorites" view — toggle favorite on notes, filtered list.
- ✓ **D3.11** Live preview — MarkdownEditor component with split-pane preview.
- ✓ **D3.12** Cross-module links in editor — render resolved link chips via CrossModuleLinkList component.
- ✓ **D3.13** Tests for the notes UI service layer.

### Exit Criteria
- Full Markdown editing with live preview works end-to-end.
- Folder and tag organization functional.
- Version history with restore works.
- Sharing and favorites functional.

---

## D4: Audit Trail Columns on PIM Entities

**Goal:** Add `CreatedByUserId` and `UpdatedByUserId` audit columns to PIM entities and generate EF migrations.

**Current state:**
- `IAuditLogger` capability and `AuditEntry` record exist in Core.
- PIM entities have `OwnerId` and timestamp columns (`CreatedAt`, `UpdatedAt`) but **no user-tracking audit columns** for who last modified a record.
- `CalendarEvent` has `CreatedByUserId` but not `UpdatedByUserId`.

### Tasks

- ✓ **D4.1** Add `CreatedByUserId` (Guid?) and `UpdatedByUserId` (Guid?) properties to:
  - `Contact`
  - `Note`
  - `EventAttendee`
  - `EventReminder`
  - `NoteTag`
  - `ContactShare`, `CalendarShare`, `NoteShare` (if missing)
- ✓ **D4.2** Add `UpdatedByUserId` to `CalendarEvent` (already has `CreatedByUserId`).
- ✓ **D4.3** Update EF configurations in each module's `Configuration/` folder to map the new columns.
- ✓ **D4.4** Wire audit column population in service methods — set `CreatedByUserId` on create, `UpdatedByUserId` on update, using `CallerContext`.
- ✓ **D4.5** Generate EF migrations for PostgreSQL and SQL Server.
- ✓ **D4.6** Update existing tests to verify audit columns are populated.
- ✓ **D4.7** Update API docs and release notes to document the new columns.

### Exit Criteria
- All PIM entities have audit columns.
- Migrations apply cleanly on fresh and existing databases.
- Services populate audit columns from `CallerContext` on every write operation.

---

## D5: Shared Notification Patterns for PIM Modules

**Goal:** Wire PIM modules into the Core notification infrastructure so that shares, invites, reminders, and mentions produce user-visible notifications.

**Current state:**
- `INotificationService` exists in Core with `SendAsync`, `GetUnreadAsync`, `MarkReadAsync`, etc.
- `NotificationDto` has full schema (type, priority, action URL, related entity).
- `NotificationCategory` enum includes `CalendarInvitation`, `Reminder`, `ResourceShared`, `Mention`.
- Chat module has its own push notification subsystem (`IPushNotificationService`) — separate from Core.
- **No PIM module currently calls `INotificationService.SendAsync()`.**

### Tasks

- ✓ **D5.1** Implement `INotificationService` backing store — `Notification` entity, EF configuration, `CoreDbContext` registration, migration.
- ✓ **D5.2** Implement `NotificationService` in `DotNetCloud.Core.Server` — persist to DB, query by user, mark read.
- ✓ **D5.3** Create notification event handlers in Contacts module:
  - `ContactSharedEvent` → notify target user ("X shared a contact with you").
- ✓ **D5.4** Create notification event handlers in Calendar module:
  - `CalendarEventCreatedEvent` → notify attendees (invitation).
  - `CalendarEventUpdatedEvent` → notify attendees (event changed).
  - `ReminderTriggeredEvent` → notify event owner/attendees.
  - `CalendarSharedEvent` → notify target user.
- ✓ **D5.5** Create notification event handlers in Notes module:
  - `NoteSharedEvent` → notify target user ("X shared a note with you").
  - `NoteMentionEvent` → notify mentioned user (if @mention support added).
- ✓ **D5.6** Add notification bell/dropdown component in Blazor UI shell — unread count badge, dropdown list, mark-read, click-through navigation.
- ✓ **D5.7** Optional: bridge Core notifications to Chat push pipeline for mobile/desktop push delivery.
- ✓ **D5.8** Tests — notification persistence, handler dispatch, UI component.

### Exit Criteria
- Sharing a contact/calendar/note produces an in-app notification for the target user.
- Calendar invitations and reminders produce notifications.
- Notifications are visible in the Blazor UI with unread count and mark-read.

---

## D6: Contacts Reverse Cross-Links

**Goal:** Enable the Contacts module to surface related Calendar events and Notes for a given contact.

**Current state:**
- `ICrossModuleLinkResolver` can resolve Contact → display name, but not Contact → related entities.
- `EventAttendee` has `UserId` (nullable) but no `ContactId` — attendees are system users, not contact records.
- Notes have `NoteLinkType.Contact` links, but no reverse query from Contacts side.
- No event handlers for `ContactCreatedEvent`/`ContactDeletedEvent` in Calendar or Notes modules.

### Tasks

- ✓ **D6.1** Define `IContactRelatedEntitiesService` interface in Core:
  - `GetRelatedEventsAsync(contactId)` → list of calendar events where attendee email matches contact email.
  - `GetRelatedNotesAsync(contactId)` → list of notes with `NoteLinkType.Contact` targeting this contact ID.
- ✓ **D6.2** Implement reverse query in Calendar module — match `EventAttendee.Email` against `Contact.Emails` to find events involving a contact.
- ✓ **D6.3** Implement reverse query in Notes module — query `NoteLink` table for `LinkType = Contact AND TargetId = contactId`.
- ✓ **D6.4** Add REST endpoint `GET /api/v1/contacts/{id}/related` returning related events and notes.
- ✓ **D6.5** Register capabilities in module manifests.
- ✓ **D6.6** Tests — reverse link resolution for contact → events and contact → notes.

### Exit Criteria
- Viewing a contact shows related calendar events and linked notes.
- Reverse queries return correct results across module boundaries.

---

## D7: Cross-Module Link Rendering in UI

**Goal:** When the UI displays a note with cross-module links (contacts, events, files), render them as clickable resolved chips instead of raw IDs.

**Current state:**
- `ICrossModuleLinkResolver` exists server-side with `ResolveAsync` / `ResolveBatchAsync`.
- `CrossModuleLinkDto` has `Label`, `Href`, `LinkType`, `IsResolved`.
- No UI component consumes this yet — the Razor pages are shells.

### Tasks

- ✓ **D7.1** Create `CrossModuleLink.razor` shared component — displays a resolved link as a styled chip/badge with icon (👤 Contact, 📅 Event, 📝 Note, 📁 File) and clickable navigation.
- ✓ **D7.2** Create `CrossModuleLinkList.razor` — takes a list of `NoteLinkDto` or `CrossModuleLinkDto`, calls resolver, renders chips.
- ✓ **D7.3** Integrate into Notes detail/editor view — render resolved links in note metadata panel.
- ✓ **D7.4** Integrate into Contact detail view — render related events/notes as link chips (depends on D6).
- ✓ **D7.5** Integrate into Calendar event detail view — render attendee contacts as link chips (where contact match exists).
- ✓ **D7.6** Tests — component renders resolved links, handles unresolved/deleted entities gracefully.

### Exit Criteria
- Cross-module links render as resolved, clickable elements in all three PIM module UIs.
- Deleted/missing entities show graceful fallback (`[Deleted Contact]`).

---

## Delivery Phases

### Phase A: Data Layer Hardening (D4)
**Estimated scope:** ~15 files changed, 1 migration per provider.
- Audit columns on all PIM entities.
- EF migrations generated and verified.
- Can be done independently with no UI impact.

### Phase B: Notification Infrastructure (D5)
**Estimated scope:** ~20 files, new Notification entity + service + event handlers.
- Notification persistence layer.
- PIM module event handlers dispatching notifications.
- Notification UI component in Blazor shell.

### Phase C: Cross-Link Completion (D6 + D7)
**Estimated scope:** ~12 files, new service + REST endpoint + Razor components.
- Reverse cross-links from Contacts.
- Shared UI components for link rendering.
- Integration into module detail views.

### Phase D: Module UI Build-Out (D1 + D2 + D3)
**Estimated scope:** ~40+ files, the largest phase.
- Contacts, Calendar, Notes full UI.
- Depends on Phases A–C for complete features (audit display, notifications, link rendering).
- Can start in parallel on the API client layer while A–C finish.

---

## Completion Criteria

When all D1–D7 tasks are ☐ → ✓:

- ✓ Contacts UI can fully manage records (3.2 Exit Criteria)
- ✓ Audit trail entries are recorded for sensitive operations (3.2 Exit Criteria)
- ✓ Shared notification patterns for invites, reminders, mentions, and shares (3.5)
- ✓ Contacts can surface related notes/events (3.5)
- ✓ Cross-module links resolve correctly in UI (3.5 Exit Criteria)
- ✓ phase-3.5 complete (Milestone C)
- ✓ All phase-3.x items marked complete (Definition of Done)
