# Calendar Module — Recurrence UI + Organization Support + Shelter-Style UX

**Status:** Draft (pending review)  
**Created:** 2026-04-27  
**Plan file:** This document

---

## Summary

The DotNetCloud Calendar module has a fully built, tested backend with RFC 5545 recurrence (`RecurrenceEngine`, `OccurrenceExpansionService`), all-day support, color/URL fields, attendees, and reminders — but the Blazor UI (`CalendarPage.razor`) never wired any of these up. The `EventEditorModel` only exposes Title, Description, Location, Start, and End.

This plan addresses three layers:

1. **Phase 1** — Wire existing backend recurrence + missing fields into the UI (the immediate user-facing gap).
2. **Phase 2** — Redesign the calendar UI in Shelter/Guild-style: modal-based editing, professional month grid, org picker dropdown.
3. **Phase 3–6** — Add organization-owned calendars (coexisting with user-owned), org membership authorization, API updates, and tests.

---

## Investigation Findings

### What the backend already supports (but UI ignores)

| Feature | Backend | DTO | UI (`EventEditorModel`) |
|---------|---------|-----|--------------------------|
| **Recurrence (RFC 5545)** | ✅ `RecurrenceEngine` + `OccurrenceExpansionService` | ✅ `CreateCalendarEventDto.RecurrenceRule` | ❌ No field |
| **All-day events** | ✅ `CalendarEvent.IsAllDay` | ✅ `CreateCalendarEventDto.IsAllDay` | ❌ No field |
| **Event color** | ✅ `CalendarEvent.Color` | ✅ `CreateCalendarEventDto.Color` | ❌ No field |
| **Event URL** | ✅ `CalendarEvent.Url` | ✅ `CreateCalendarEventDto.Url` | ❌ No field |
| **Attendees** | ✅ `EventAttendee` entity + RSVP | ✅ `CreateCalendarEventDto.Attendees` | ❌ No field |
| **Reminders** | ✅ `EventReminder` + `ReminderDispatchService` | ✅ `CreateCalendarEventDto.Reminders` | ❌ No field |

### WildFrontiers.Shelter comparison

| Feature | Shelter | DotNetCloud (backend) | Gap |
|---------|---------|----------------------|-----|
| Recurrence | Simple strings (Daily/Weekly/Biweekly/Monthly/Yearly) | Full RFC 5545 RRULE | Backend is better; UI missing |
| Guild/Org scope | `GuildCalendarEvent.GuildId` | N/A | Need `OrganizationId` on `Calendar` |
| Modal-based UI | 4 modals (add, edit, day details, delete) | Inline form | UI needs redesign |
| Month grid | 120px cells, "+X more", today highlight | Basic grid, no overflow | UI polish needed |
| Role-based access | Manager+ can edit | N/A for orgs | Need org membership checks |
| Reminders | ❌ None | ✅ Full reminder engine | Already ahead |

---

## Phase 1: Wire Missing Fields into Event Editor

### 1.1 Update `EventEditorModel`

**File:** `src/Modules/Calendar/DotNetCloud.Modules.Calendar/UI/CalendarPage.razor`  
**Location:** Nested `EventEditorModel` class (currently ~line 768)

Add these properties:

```csharp
public string? RecurrenceRule { get; set; }      // RFC 5545 RRULE string
public DateTime? RecurrenceEndDate { get; set; }   // UI convenience → appended to RRULE as UNTIL
public string? RecurrenceType { get; set; }        // UI convenience: "None","Daily","Weekly","Biweekly","Monthly","Yearly"
public bool IsAllDay { get; set; }
public string? Color { get; set; }
public string? Url { get; set; }
```

Update `From(CalendarEventDto)` to populate new fields from `dto.RecurrenceRule`, `dto.IsAllDay`, `dto.Color`, `dto.Url`.

Update `ToCreateDto()`:
```csharp
RecurrenceRule = BuildRrule(),  // combine RecurrenceType + RecurrenceEndDate
IsAllDay = IsAllDay,
Color = Color,
Url = Url
```

Update `ToUpdateDto()` similarly.

**Helper:** `BuildRrule()` method:
- If `RecurrenceType` is null/empty/"None" → return `null`.
- Build: `"FREQ={DAILY|WEEKLY|MONTHLY|YEARLY}"` + optional `";INTERVAL=2"` for Biweekly + optional `";UNTIL={yyyyMMddTHHmmssZ}"`.
- For advanced users, allow pasting raw RRULE directly.

### 1.2 Update Event Editor Form Markup

**File:** `src/Modules/Calendar/DotNetCloud.Modules.Calendar/UI/CalendarPage.razor`  
**Location:** `@if (_eventEditor is not null)` block (~line 156)

Add form fields:

```
All-day checkbox (toggles time inputs off when checked)
Start date picker (always visible)
End date picker (hidden when all-day)
Time inputs for start/end (hidden when all-day)
Recurrence dropdown: None | Daily | Weekly | Biweekly | Monthly | Yearly
Recurrence end date (shown when recurrence != None)
Color input with preset swatches: 🔴🟠🟡🟢🔵🟣⚫ + custom hex
URL input (video call link, etc.)
```

### 1.3 Verify API Client

**File:** `src/Modules/Calendar/DotNetCloud.Modules.Calendar/Services/CalendarApiClient.cs`

The `CreateEventAsync(CreateCalendarEventDto)` and `UpdateEventAsync(Guid, UpdateCalendarEventDto)` already serialize these fields via JSON. No changes expected, but verify.

### Phase 1 Verification

- [ ] Create an event with recurrence "Weekly" → events appear in subsequent weeks in month view.
- [ ] Create an all-day event → displayed without time in month grid.
- [ ] Set event color → badge renders in that color.
- [ ] Edit existing event → recurrence/all-day/color/URL fields load with current values.
- [ ] Existing non-recurring events still work (no regression).

---

## Phase 2: Shelter-Style UX Redesign

### 2.1 Modal-Based Editing

Replace the inline `EditForm` blocks with Bootstrap-style modal dialogs.

**Four modals:**

| Modal | Trigger | Content |
|-------|---------|---------|
| **Add/Edit Event** | "New Event" button or click existing event | Full event form (title, dates, all-day, recurrence, location, description, color, URL) |
| **Add/Edit Calendar** | "New Calendar" button or edit calendar | Calendar form (name, description, color, timezone, org selector) |
| **Day Details** | Click day cell number | List of events for that day + "Add Event" button |
| **Delete Confirmation** | Delete button in edit modal | "Are you sure?" with event title |

**Modal implementation:**
- Use Bootstrap 5 modal classes (`modal`, `modal-dialog`, `modal-content`, `modal-header`, `modal-body`, `modal-footer`).
- Backdrop click closes (with unsaved-changes warning for edit modal).
- Escape key closes.
- Modal state: `_showEventModal`, `_showCalendarModal`, `_showDayModal`, `_showDeleteModal` booleans.

### 2.2 Month Grid Polish

```
┌──────┬──────┬──────┬──────┬──────┬──────┬──────┐
│ Sun  │ Mon  │ Tue  │ Wed  │ Thu  │ Fri  │ Sat  │
├──────┼──────┼──────┼──────┼──────┼──────┼──────┤
│  1   │  2   │  3   │  4   │  5   │  6   │  7   │
│      │      │ █ 9am│ █ 2pm│      │      │      │
│      │      │ Team│ Dent│      │      │      │
├──────┼──────┼──────┼──────┼──────┼──────┼──────┤
│  8   │  9   │ 10   │ 11   │ 12   │ 13   │ 14   │
│      │      │      │█ All │      │      │      │
│      │      │      │ Lunch│      │      │      │
│      │      │      │+2more│      │      │      │
├──────┴──────┴──────┴──────┴──────┴──────┴──────┤
│             ← April 2026 →    [Today]           │
└────────────────────────────────────────────────┘
```

**Specifications:**
- 7-column CSS Grid (`grid-template-columns: repeat(7, 1fr)`).
- Day headers: Sun–Sat (configurable to Mon–Sun via setting).
- Day cells: 120px min-height, `overflow-y: auto`.
- Today's cell: `border: 2px solid #3b82f6` (blue), subtle blue background.
- Adjacent month dates: `color: #9ca3af` (gray), `background: #f9fafb`.
- Events: rounded badges, `font-size: 0.75rem`, background from event color.
- Event badge format: `{HH:mm} {Title}` (or just `{Title}` for all-day).
- "+X more" link when >3 events: opens Day Details modal.
- Click day number → Day Details modal.
- Click event badge → Edit Event modal.
- Click empty area of day cell → Add Event modal (pre-filled date).

### 2.3 Org Picker Dropdown

**Location:** Toolbar (top of calendar main area).

```
[←] April 2026 [→]  [Today]    [My Calendars ▾]
```

Dropdown options:
- "My Calendars" (default — shows user-owned calendars).
- Divider.
- Organization names (resolved via `ITeamDirectory` / org membership).

**Behavior:**
- Selecting an org filters the sidebar calendar list to that org's calendars.
- Month grid shows events from selected org's calendars.
- "New Calendar" button creates org calendar when org is selected.
- Requires `_selectedOrganizationId` state variable.

### 2.4 CSS Redesign

**File:** `src/Modules/Calendar/DotNetCloud.Modules.Calendar/UI/CalendarPage.razor.css`

**Design principles:**
- Professional, clean, modern.
- Consistent spacing (8px grid).
- Proper typography (system font stack).
- Responsive: sidebar collapses on small screens, modals full-width on mobile.
- Color palette anchored to Bootstrap 5 variables where possible.

**Key styles:**
```css
.calendar-page-layout { display: flex; height: 100%; }
.calendar-sidebar { width: 260px; flex-shrink: 0; border-right: 1px solid #e5e7eb; }
.calendar-sidebar.collapsed { width: 48px; }
.calendar-main { flex: 1; display: flex; flex-direction: column; overflow: hidden; }
.calendar-month-grid { display: grid; grid-template-columns: repeat(7, 1fr); flex: 1; }
.calendar-day-cell { min-height: 120px; border: 1px solid #e5e7eb; padding: 4px; overflow-y: auto; }
.calendar-day-cell.today { border-color: #3b82f6; background: #eff6ff; }
.calendar-day-cell.other-month { background: #f9fafb; }
.calendar-day-number { font-size: 0.8125rem; color: #6b7280; margin-bottom: 2px; }
.calendar-day-cell.today .calendar-day-number { color: #3b82f6; font-weight: 700; }
.calendar-day-event { 
    font-size: 0.75rem; padding: 1px 4px; border-radius: 4px; 
    margin-bottom: 2px; cursor: pointer; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; 
}
```

### Phase 2 Verification

- [ ] All four modals open/close correctly with proper transitions.
- [ ] Month grid renders correct dates for any month/year.
- [ ] Today cell is visually distinct.
- [ ] Adjacent month dates are grayed out.
- [ ] "+X more" appears when >3 events, clickable.
- [ ] Org picker shows user's organizations, switching works.
- [ ] Responsive: sidebar collapses, modals adapt to narrow viewports.

---

## Phase 3: Data Model — Organization Calendar Support

### 3.1 Add `OrganizationId` to Calendar Entity

**File:** `src/Modules/Calendar/DotNetCloud.Modules.Calendar/Models/Calendar.cs`

```csharp
/// <summary>
/// When set, this calendar belongs to an organization rather than an individual user.
/// Organization members have implicit access based on their role.
/// </summary>
public Guid? OrganizationId { get; set; }
```

**Rules:**
- `OrganizationId` nullable — `null` means user-owned calendar (existing behavior).
- When `OrganizationId` is set, `OwnerId` tracks the creator but does not grant ownership.
- A user-owned calendar cannot be converted to org-owned (and vice versa) to avoid permission confusion.

### 3.2 Update Entity Configuration

**File:** `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Data/Configuration/CalendarConfiguration.cs`

```csharp
builder.Property(c => c.OrganizationId).IsRequired(false);
builder.HasIndex(c => c.OrganizationId).HasDatabaseName("IX_Calendars_OrganizationId");
```

### 3.3 EF Migrations

```bash
# PostgreSQL
dotnet ef migrations add AddCalendarOrganizationId \
  --project src/Modules/Calendar/DotNetCloud.Modules.Calendar.Data \
  --context CalendarDbContext

# SQL Server
dotnet ef migrations add AddCalendarOrganizationId_SqlServer \
  --project src/Modules/Calendar/DotNetCloud.Modules.Calendar.Data \
  --context CalendarDbContext \
  --output-dir Migrations/SqlServer
```

### 3.4 Update DTOs

**File:** `src/Core/DotNetCloud.Core/DTOs/CalendarDtos.cs`

- `CreateCalendarDto` — add `public Guid? OrganizationId { get; init; }`.
- `CalendarDto` — add `public Guid? OrganizationId { get; init; }`.
- `UpdateCalendarDto` — add `public Guid? OrganizationId { get; init; }` (allows setting org on update? Decision: no — org ownership is immutable after creation; exclude from update).

### Phase 3 Verification

- [ ] Migration creates `OrganizationId` column with nullable type and index.
- [ ] `dotnet build` succeeds.
- [ ] Can create user-owned calendar (OrganizationId = null) — no regression.
- [ ] Can create org-owned calendar (OrganizationId set) via API.

---

## Phase 4: Authorization — Organization Membership Checks

### 4.1 Organization Directory Capability

Check if `IOrganizationDirectory` already exists. If not:

**File:** `src/Core/DotNetCloud.Core/Capabilities/IOrganizationDirectory.cs`

```csharp
public interface IOrganizationDirectory : ICapabilityInterface
{
    Task<bool> IsOrganizationMemberAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
    Task<OrganizationMemberInfo?> GetMemberAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
}
```

**File:** `src/Core/DotNetCloud.Core.Auth/Capabilities/OrganizationDirectoryService.cs`

Implementation using existing `OrganizationMember` / `Organization` entities from Core Data.

### 4.2 Authorization Logic in Calendar Services

**File:** `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Data/Services/CalendarService.cs`

For org-owned calendars:
- **Read access:** User must be an organization member.
- **Write access:** User must be an organization member with Manager+ role (or Admin).
- Helper method:
  ```csharp
  private async Task<bool> CanAccessCalendarAsync(Calendar calendar, Guid userId, bool requireWrite)
  {
      if (calendar.OrganizationId is null)
      {
          // User-owned: owner has full access; shares checked separately
          return calendar.OwnerId == userId;
      }
      
      var isMember = await _orgDirectory.IsOrganizationMemberAsync(calendar.OrganizationId.Value, userId);
      if (!isMember) return false;
      if (!requireWrite) return true;
      
      var member = await _orgDirectory.GetMemberAsync(calendar.OrganizationId.Value, userId);
      return member?.RoleIds.Any(r => OrgRoles.IsManagerOrAbove(r)) == true;
  }
  ```

**File:** `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Data/Services/CalendarEventService.cs`

Same pattern for event CRUD — resolve calendar first, then check auth.

### 4.3 CalendarShare Behavior for Org Calendars

**Decision:** Org calendars do not use `CalendarShare`. Membership IS the share.  
If a user is an org member, they see org calendars automatically.  
If they leave the org, they lose access.  
No additional per-user/team sharing on org calendars.

**Implementation:** In `CalendarShareService`, reject share creation if `calendar.OrganizationId != null`.

### Phase 4 Verification

- [ ] Org member can list/view org calendars and events.
- [ ] Non-org-member cannot see org calendars.
- [ ] Org member without Manager role cannot create/edit/delete events (read-only).
- [ ] Org Manager can create/edit/delete events.
- [ ] Sharing on org calendar returns error.
- [ ] User-owned calendars still work with existing share logic.

---

## Phase 5: API Layer Updates

### 5.1 REST Controller

**File:** `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/Controllers/CalendarController.cs`

- `GET /api/v1/calendars` — add optional query param `?organizationId={guid}` to filter.
- `POST /api/v1/calendars` — validate that if `OrganizationId` is set, the caller is an org member with Manager+ role.
- All org-scoped endpoints resolve `CallerContext` → validate org membership → proceed.

### 5.2 gRPC Proto

**File:** `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/Protos/calendar_service.proto`

```protobuf
message CalendarMessage {
    // ...existing fields...
    string organization_id = XY;  // nullable GUID as string
}

message CreateCalendarRequest {
    // ...existing fields...
    string organization_id = XY;  // optional
}

message ListCalendarsRequest {
    // ...existing fields...
    string organization_id = XY;  // optional filter
}
```

Regenerate C# stubs after proto change.

### 5.3 CalendarGrpcService

**File:** `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/Services/CalendarGrpcService.cs`

Map new proto fields to DTOs and delegate to business services (already handled by auth in Phase 4).

### Phase 5 Verification

- [ ] REST: `POST /api/v1/calendars` with `organizationId` creates org calendar.
- [ ] REST: `GET /api/v1/calendars?organizationId={guid}` returns org calendars.
- [ ] gRPC: `CreateCalendar` with `organization_id` works via gRPC client.
- [ ] Auth errors return 403 with clear message.

---

## Phase 6: Routing, Registration, and Tests

### 6.1 Route Registration

**File:** `src/Modules/Calendar/DotNetCloud.Modules.Calendar/CalendarModule.cs`

Calendar page already registered at `/calendar`. Add org-scoped route variant if needed: `/calendar/organization/{orgId}` (maps to same page with org pre-selected).

### 6.2 Navigation Entry

Ensure Calendar is accessible from the main app navigation (should already be the case).

### 6.3 Tests

**New unit tests** in `tests/DotNetCloud.Modules.Calendar.Tests/`:
- `OrganizationCalendarAuthorizationTests.cs` — org member read, manager write, non-member denied, ownership coexistence.
- `RecurrenceUITests.cs` — bUnit tests for recurrence picker, RRULE generation, all-day toggle.

**Existing tests:** Run full suite to verify no regressions.

### Phase 6 Verification

- [ ] `dotnet build DotNetCloud.CI.slnf` — clean build.
- [ ] `dotnet test DotNetCloud.CI.slnf` — all tests pass.
- [ ] Manual smoke test: create org calendar → add recurring event → view in month grid → edit → delete.

---

## File Manifest

| # | File | Phase | Change |
|---|------|-------|--------|
| 1 | `src/Modules/Calendar/DotNetCloud.Modules.Calendar/UI/CalendarPage.razor` | 1, 2 | Major rework: `EventEditorModel` fields, recurrence UI, modals, org picker, month grid |
| 2 | `src/Modules/Calendar/DotNetCloud.Modules.Calendar/UI/CalendarPage.razor.css` | 2 | Full CSS redesign |
| 3 | `src/Modules/Calendar/DotNetCloud.Modules.Calendar/Services/CalendarApiClient.cs` | 1 | Verify (likely no changes needed) |
| 4 | `src/Modules/Calendar/DotNetCloud.Modules.Calendar/Models/Calendar.cs` | 3 | Add `OrganizationId` |
| 5 | `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Data/Configuration/CalendarConfiguration.cs` | 3 | Add index for `OrganizationId` |
| 6 | `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Data/CalendarDbContext.cs` | 3 | May need `DbSet<Organization>` include (verify) |
| 7 | `src/Core/DotNetCloud.Core/DTOs/CalendarDtos.cs` | 3 | Add `OrganizationId` to DTOs |
| 8 | `src/Core/DotNetCloud.Core/Capabilities/IOrganizationDirectory.cs` | 4 | New capability interface (if not existing) |
| 9 | `src/Core/DotNetCloud.Core.Auth/Capabilities/OrganizationDirectoryService.cs` | 4 | New implementation (if not existing) |
| 10 | `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Data/Services/CalendarService.cs` | 4 | Org auth logic |
| 11 | `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Data/Services/CalendarEventService.cs` | 4 | Org auth logic |
| 12 | `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Data/Services/CalendarShareService.cs` | 4 | Reject shares on org calendars |
| 13 | `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/Controllers/CalendarController.cs` | 5 | Org filter + create |
| 14 | `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/Protos/calendar_service.proto` | 5 | Add `organization_id` |
| 15 | `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/Services/CalendarGrpcService.cs` | 5 | Map org fields |
| 16 | `tests/DotNetCloud.Modules.Calendar.Tests/OrganizationCalendarAuthorizationTests.cs` | 6 | New tests |
| 17 | `tests/DotNetCloud.Modules.Calendar.Tests/RecurrenceUITests.cs` | 6 | New bUnit tests |

---

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| **Keep RFC 5545 recurrence engine** | Backend is solid and tested. UI generates simple RRULE strings from dropdown; advanced users can paste raw RRULE or import .ics. |
| **Coexistence model** | `OrganizationId` nullable — `null` = user-owned, non-null = org-owned. No migration of existing data needed. |
| **Org auth: membership-based** | No `CalendarShare` for org calendars. Membership in organization = implicit access. Manager+ role = write access. |
| **Org ownership immutable** | Once a calendar is org-owned, it stays org-owned. Prevents permission confusion. |
| **Standalone page with picker** | Calendar page has org dropdown; not embedded in org admin pages. Simpler, more flexible. |
| **Shelter-style modals** | Better UX than inline forms for a complex form with recurrence, color, etc. |

---

## Execution Order & Dependencies

```
Phase 1 (UI fields) ──┐
                       ├── Phase 2 (UX redesign)
                       │
Phase 3 (data model) ──┼── Phase 4 (auth) ── Phase 5 (API) ── Phase 6 (tests)
```

- Phases 1 and 3 have **no dependencies** and can start immediately in parallel.
- Phase 2 depends on Phase 1 (needs the `EventEditorModel` fields).
- Phase 4 depends on Phase 3 (needs `OrganizationId` on Calendar).
- Phase 5 depends on Phase 4 (needs auth logic).
- Phase 6 depends on Phase 5.

**Recommended order:** 1 → 2 → 3 → 4 → 5 → 6  
(Get recurrence visible to users ASAP, then layer on org support.)

---

## Verification Checklist (Pre-Commit)

- [ ] `dotnet build DotNetCloud.CI.slnf` succeeds with no warnings.
- [ ] `dotnet test DotNetCloud.CI.slnf` — all tests pass, no regressions.
- [ ] Recurring events are visible and expand correctly in month/week/day views.
- [ ] All-day events display without time in grid.
- [ ] Event color renders on badges.
- [ ] Org calendars are only visible to org members.
- [ ] Org managers can create/edit/delete org events.
- [ ] Non-members get 403 on org calendar endpoints.
- [ ] User-owned calendars work exactly as before.
- [ ] Both `IMPLEMENTATION_CHECKLIST.md` and `MASTER_PROJECT_PLAN.md` updated with `✓` marks.

---

## Excluded (Future Work)

- CalDAV support for organization calendars
- Embedding calendar in organization admin pages
- Default auto-created org calendar on org creation
- Inheriting org branding color as calendar default
- Multi-day event spanning (backend supports StartUtc/EndUtc across days; UI just needs visual spanning)
- Attendee management UI
- Reminder configuration UI
- Complex recurrence builder (BYDAY, BYMONTHDAY, etc.) — power users can paste raw RRULE or import .ics
