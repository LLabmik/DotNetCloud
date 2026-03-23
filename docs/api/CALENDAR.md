# Calendar API Reference

> **Base URL:** `/api/v1/calendars`  
> **Authentication:** Bearer token (OpenIddict)  
> **Response Format:** Standard envelope (see [RESPONSE_FORMAT.md](RESPONSE_FORMAT.md))

---

## REST Endpoints — Calendars

### List Calendars

```
GET /api/v1/calendars
```

**Response:** Array of `CalendarDto` (owned and shared calendars).

---

### Get Calendar

```
GET /api/v1/calendars/{calendarId}
```

**Response:** `CalendarDto`

**Errors:** `404` CALENDAR_NOT_FOUND

---

### Create Calendar

```
POST /api/v1/calendars
```

**Request Body:** `CreateCalendarDto`

```json
{
  "name": "Work",
  "description": "Work meetings and deadlines",
  "color": "#4285F4",
  "timezone": "America/New_York"
}
```

**Required Fields:** `name`

**Response:** `201` with created `CalendarDto`

---

### Update Calendar

```
PUT /api/v1/calendars/{calendarId}
```

**Request Body:** `UpdateCalendarDto` — all fields optional (patch semantics).

```json
{
  "name": "Work Calendar",
  "color": "#0F9D58",
  "isVisible": true
}
```

**Response:** Updated `CalendarDto`

**Errors:** `404` CALENDAR_NOT_FOUND, `403` insufficient permissions

---

### Delete Calendar

```
DELETE /api/v1/calendars/{calendarId}
```

Soft-deletes the calendar and all its events. Only the owner can delete.

**Response:** `204` No Content

---

## REST Endpoints — Events

### List Events

```
GET /api/v1/calendars/{calendarId}/events?from={date}&to={date}&skip={n}&take={n}
```

| Parameter | Type | Default | Description |
|---|---|---|---|
| `from` | DateTime | — | Start of date range filter (UTC) |
| `to` | DateTime | — | End of date range filter (UTC) |
| `skip` | int | 0 | Pagination offset |
| `take` | int | 200 | Page size |

**Response:** Paginated array of `CalendarEventDto`

**Recurrence Expansion:** When `from` and `to` are provided, recurring events within the window are automatically expanded into individual occurrence instances. Each expanded occurrence is returned as a `CalendarEventDto` where `recurringEventId` is set to the master event's ID and `originalStartUtc` is set to that occurrence's computed start time. Exception overrides (single-occurrence edits) replace the generated occurrence they modify.

---

### Get Event

```
GET /api/v1/calendars/events/{eventId}
```

**Response:** `CalendarEventDto`

**Errors:** `404` CALENDAR_EVENT_NOT_FOUND

---

### Create Event

```
POST /api/v1/calendars/events
```

**Request Body:** `CreateCalendarEventDto`

```json
{
  "calendarId": "...",
  "title": "Team Standup",
  "description": "Daily sync with the engineering team",
  "location": "Conference Room B",
  "startUtc": "2026-03-24T14:00:00Z",
  "endUtc": "2026-03-24T14:30:00Z",
  "isAllDay": false,
  "recurrenceRule": "FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR",
  "color": "#DB4437",
  "url": "https://meet.example.com/standup",
  "attendees": [
    {
      "email": "alice@example.com",
      "displayName": "Alice",
      "role": "Required"
    },
    {
      "userId": "...",
      "email": "bob@example.com",
      "role": "Optional"
    }
  ],
  "reminders": [
    { "method": "Notification", "minutesBefore": 15 },
    { "method": "Email", "minutesBefore": 60 }
  ]
}
```

**Required Fields:** `calendarId`, `title`, `startUtc`, `endUtc`

**Response:** `201` with created `CalendarEventDto`

---

### Update Event

```
PUT /api/v1/calendars/events/{eventId}
```

**Request Body:** `UpdateCalendarEventDto` — all fields optional (patch semantics).

**Response:** Updated `CalendarEventDto`

**Errors:** `404` CALENDAR_EVENT_NOT_FOUND, `403` insufficient permissions, `409` CALENDAR_EVENT_CONFLICT

---

### Delete Event

```
DELETE /api/v1/calendars/events/{eventId}
```

**Response:** `204` No Content

---

### RSVP to Event

```
POST /api/v1/calendars/events/{eventId}/rsvp
```

**Request Body:** `EventRsvpDto`

```json
{
  "status": "Accepted",
  "comment": "I'll be there!"
}
```

**Status Values:** `Accepted`, `Declined`, `Tentative`

**Response:** Updated `CalendarEventDto`

---

### Search Events

```
GET /api/v1/calendars/events/search?q={query}&from={date}&to={date}&skip={n}&take={n}
```

| Parameter | Type | Default | Description |
|---|---|---|---|
| `q` | string | — | Search query (title, description, location) |
| `from` | DateTime | — | Start of date range |
| `to` | DateTime | — | End of date range |
| `skip` | int | 0 | Pagination offset |
| `take` | int | 200 | Page size |

**Response:** Paginated array of `CalendarEventDto`

**Recurrence Expansion:** Same expansion behavior as List Events — recurring events within the date range are expanded into individual occurrences and included in search results. Searches across all of the authenticated user's calendars (owned and shared).

---

## Sharing

### List Calendar Shares

```
GET /api/v1/calendars/{calendarId}/shares
```

**Response:** Array of `CalendarShare`

---

### Share Calendar

```
POST /api/v1/calendars/{calendarId}/shares
```

**Request Body:**

```json
{
  "userId": "...",
  "teamId": null,
  "permission": "ReadWrite"
}
```

Provide either `userId` or `teamId`, not both. Sharing applies to the entire calendar and all its events.

**Permissions:** `ReadOnly`, `ReadWrite`

**Response:** `201` with created share

---

### Remove Share

```
DELETE /api/v1/calendars/shares/{shareId}
```

**Response:** `204` No Content

---

## iCalendar Import / Export

### Export Event as iCalendar

```
GET /api/v1/calendars/events/{eventId}/ical
```

**Response:** `text/calendar` content (RFC 5545)

```
BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//DotNetCloud//Calendar//EN
BEGIN:VEVENT
DTSTART:20260324T140000Z
DTEND:20260324T143000Z
SUMMARY:Team Standup
DESCRIPTION:Daily sync with the engineering team
LOCATION:Conference Room B
END:VEVENT
END:VCALENDAR
```

---

### Export Calendar as iCalendar

```
GET /api/v1/calendars/{calendarId}/export
```

**Response:** `text/calendar` with all events in the calendar

---

### Import Events from iCalendar

```
POST /api/v1/calendars/events/import
```

**Request Body:** Raw iCalendar text (`text/plain` or `text/calendar`)

**Response:** Array of created `CalendarEventDto`

---

## CalendarDto Schema

```json
{
  "id": "...",
  "ownerId": "...",
  "name": "Work",
  "description": "Work meetings and deadlines",
  "color": "#4285F4",
  "timezone": "America/New_York",
  "isDefault": true,
  "isVisible": true,
  "isDeleted": false,
  "syncToken": "sync-12345",
  "createdAt": "2026-01-15T10:00:00Z",
  "updatedAt": "2026-03-15T14:30:00Z"
}
```

## CalendarEventDto Schema

```json
{
  "id": "...",
  "calendarId": "...",
  "createdByUserId": "...",
  "title": "Team Standup",
  "description": "Daily sync with the engineering team",
  "location": "Conference Room B",
  "startUtc": "2026-03-24T14:00:00Z",
  "endUtc": "2026-03-24T14:30:00Z",
  "isAllDay": false,
  "status": "Confirmed",
  "recurrenceRule": "FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR",
  "recurringEventId": null,
  "originalStartUtc": null,
  "color": "#DB4437",
  "url": "https://meet.example.com/standup",
  "etag": "\"x1y2z3\"",
  "isDeleted": false,
  "createdAt": "2026-03-01T10:00:00Z",
  "updatedAt": "2026-03-20T09:15:00Z",
  "attendees": [
    {
      "userId": null,
      "email": "alice@example.com",
      "displayName": "Alice",
      "role": "Required",
      "status": "Accepted",
      "comment": null,
      "respondedAt": "2026-03-02T08:00:00Z"
    }
  ],
  "reminders": [
    { "method": "Notification", "minutesBefore": 15 }
  ]
}
```

---

## Recurrence

### Supported RRULE Components

The recurrence engine implements RFC 5545 RRULE parsing. Supported components:

| Component | Values | Example |
|---|---|---|
| `FREQ` | `DAILY`, `WEEKLY`, `MONTHLY`, `YEARLY` | `FREQ=WEEKLY` |
| `INTERVAL` | Positive integer | `INTERVAL=2` (every 2 weeks) |
| `COUNT` | Max occurrences | `COUNT=10` |
| `UNTIL` | End date (UTC) | `UNTIL=20260630T000000Z` |
| `BYDAY` | Day list, optional ordinal | `BYDAY=MO,WE,FR` or `BYDAY=2MO` (2nd Monday) or `BYDAY=-1FR` (last Friday) |
| `BYMONTHDAY` | Day(s) of month | `BYMONTHDAY=15` |
| `BYMONTH` | Month(s) | `BYMONTH=1,6` |
| `BYSETPOS` | Position within set | `BYSETPOS=-1` (last occurrence) |
| `WKST` | Week start day | `WKST=MO` |

### Occurrence Expansion

Occurrence expansion is capped at **1,000 instances** per recurring event. If a rule would produce more occurrences within the requested window, only the first 1,000 are returned.

Each expanded occurrence is a virtual `CalendarEventDto` with:
- `id` — same as the master event (use `originalStartUtc` to distinguish occurrences)
- `recurringEventId` — set to the master event's ID
- `originalStartUtc` — the computed start time of this occurrence
- `startUtc` / `endUtc` — adjusted to this occurrence's time, preserving the original event duration

### Recurrence Exceptions

Editing a single occurrence of a recurring event creates a **recurrence exception** — a standalone event linked to the master via `recurringEventId` and `originalStartUtc`. When expanding occurrences:

1. The generated occurrence at the exception's `originalStartUtc` is removed
2. The exception event replaces it with its own modified data
3. Deleting a single occurrence creates a cancelled exception that suppresses that date

---

## Reminders

### Reminder Dispatch

Reminders are dispatched automatically by a background service that scans every 30 seconds. When a reminder's trigger time (`event.startUtc - reminder.minutesBefore`) falls within the current look-ahead window (24 hours):

1. A `CalendarReminderTriggeredEvent` is published on the event bus
2. A cross-module `ReminderTriggeredEvent` is published (consumed by the notification service)

**Recurring events:** Reminders fire independently for each expanded occurrence. A reminder on a weekly meeting fires every week.

**Duplicate prevention:** Each reminder fires at most once per occurrence. Dispatch is tracked in the `ReminderLogs` table with a unique constraint on `(ReminderId, OccurrenceStartUtc)`.

---

## Enums

### Event Status

| Value | Description |
|---|---|
| `Tentative` | Provisionally scheduled |
| `Confirmed` | Definitely happening |
| `Cancelled` | Event cancelled |

### Attendee Role

| Value | Description |
|---|---|
| `Required` | Essential participant |
| `Optional` | Invited but not critical |
| `Informational` | FYI only |

### Attendee Status

| Value | Description |
|---|---|
| `NeedsAction` | No response yet |
| `Accepted` | Confirmed attendance |
| `Declined` | Won't attend |
| `Tentative` | May attend |

### Reminder Method

| Value | Description |
|---|---|
| `Notification` | In-app notification |
| `Email` | Email reminder |

---

## Error Codes

| Code | HTTP | Description |
|---|---|---|
| `CALENDAR_NOT_FOUND` | 404 | Calendar does not exist or is not accessible |
| `CALENDAR_EVENT_NOT_FOUND` | 404 | Event does not exist or is not accessible |
| `CALENDAR_ALREADY_EXISTS` | 409 | Duplicate calendar |
| `CALENDAR_EVENT_CONFLICT` | 409 | Event version conflict (concurrent edit) |
| `CALENDAR_INVALID_RECURRENCE` | 400 | Invalid RRULE format (unsupported FREQ, malformed components) |
| `ATTENDEE_NOT_FOUND` | 404 | Referenced attendee not found |
| `CALENDAR_SHARE_NOT_FOUND` | 404 | Share does not exist |

---

## CalDAV Endpoints

CalDAV (RFC 4791) endpoints for external client synchronization.

### Discovery

```
OPTIONS /.well-known/caldav
```

Returns discovery information for CalDAV clients.

### List Calendars (PROPFIND)

```
PROPFIND /caldav/{userId}/calendars
```

Returns XML with user's calendar collections and properties.

**Response Headers:**

```
DAV: 1, calendar-access
Allow: OPTIONS, PROPFIND, REPORT, GET, PUT, DELETE
```

### List Events (PROPFIND)

```
PROPFIND /caldav/{userId}/calendars/{calendarId}
```

Returns XML with event listing and ETags.

### Get Event (iCalendar)

```
GET /caldav/{userId}/calendars/{calendarId}/{eventId}.ics
```

**Response:** `text/calendar` with `ETag` header

### Create / Update Event

```
PUT /caldav/{userId}/calendars/{calendarId}/{eventId}.ics
```

**Request Body:** iCalendar (RFC 5545) text

**Headers:**
- `If-Match: "etag"` — update existing (412 on mismatch)
- Omit `If-Match` — create new

**Response:** `201` Created or `204` No Content with updated `ETag`

### Delete Event

```
DELETE /caldav/{userId}/calendars/{calendarId}/{eventId}.ics
```

**Response:** `204` No Content

### Sync Report (REPORT)

```
REPORT /caldav/{userId}/calendars/{calendarId}
```

Sync-token based change tracking for incremental synchronization.

**Request Body:** XML with `sync-token` from previous sync

**Response:** XML with added/modified/deleted events since the given sync token
