# Phase 3 Release Notes — PIM Suite (Contacts, Calendar, Notes)

> **Version:** 0.2.0-alpha  
> **Release Date:** 2026-03-23  
> **Status:** Alpha

---

## Summary

Phase 3 adds a complete Personal Information Management (PIM) suite to DotNetCloud: **Contacts**, **Calendar**, and **Notes** modules. These modules follow the same modular monolith architecture as existing modules (Files, Chat) and integrate with CardDAV/CalDAV standards for external client synchronization.

---

## New Features

### Contacts Module

- **Contact management** — create, read, update, delete contacts with full person/organization/group support
- **Rich contact fields** — multiple emails, phone numbers, addresses, custom fields, birthday, anniversary
- **Contact groups** — organize contacts into named groups with membership management
- **Sharing** — share contacts with individual users or teams (ReadOnly / ReadWrite permissions)
- **vCard import/export** — vCard 3.0 format for bulk import and single/all export
- **CardDAV sync** — RFC 6352 compliant address book sync with ETag conflict detection
- **CardDAV discovery** — `/.well-known/carddav` (RFC 6764) for automatic client configuration

### Calendar Module

- **Calendar management** — multiple calendars per user with color coding and timezone support
- **Event management** — create events with attendees, reminders, descriptions (Markdown), locations, and URLs
- **Recurring events** — RFC 5545 RRULE recurrence with exception instance support
- **Invitations & RSVP** — invite attendees (Required/Optional/Informational) with response tracking
- **Reminders** — multiple reminders per event (Notification or Email), configurable minutes before start
- **Sharing** — share entire calendars with users or teams (cascades to all events)
- **iCalendar import/export** — RFC 5545 format for bulk import and calendar/event export
- **CalDAV sync** — RFC 4791 compliant calendar sync with sync-token change tracking
- **CalDAV discovery** — `/.well-known/caldav` (RFC 6764) for automatic client configuration

### Notes Module

- **Markdown-first notes** — create and edit notes in Markdown or plain text
- **Hierarchical folders** — unlimited nesting with colored folder labels and manual sort ordering
- **Tags** — free-form tag labels for cross-folder categorization
- **Full-text search** — search by note title, content, and tags with relevance ranking
- **Version history** — every save creates a version; browse and non-destructively restore any previous version
- **Optimistic concurrency** — version-based conflict detection prevents silent overwrites
- **Cross-module links** — link notes to files, calendar events, contacts, or other notes
- **Sharing** — share individual notes with users (ReadOnly / ReadWrite)
- **Pin & favorite** — quick-access markers for important or frequently used notes

### Migration Infrastructure

- **Import pipeline** — unified import framework with per-module providers
- **Supported formats** — vCard 3.0 (contacts), iCalendar RFC 5545 (calendar), JSON manifest or Markdown (notes)
- **Dry-run mode** — validate and preview imports without persisting data
- **Conflict strategies** — Skip, Overwrite, or Create Copy on duplicate detection
- **NextCloud migration path** — import source tagging for NextCloud exports

### Testing & Quality

- **224 new tests** across 8 test files covering share services, security (tenant isolation, authorization bypass, XSS), DAV interoperability, and performance baselines
- **Total CI:** 2,700 tests, 0 failures

---

## API Endpoints

### Contacts REST API (`/api/v1/contacts`)

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/v1/contacts` | List contacts (search, pagination) |
| `GET/POST/PUT/DELETE` | `/api/v1/contacts/{id}` | CRUD operations |
| `GET/POST/PUT/DELETE` | `/api/v1/contacts/groups/*` | Group management |
| `GET/POST/DELETE` | `/api/v1/contacts/{id}/shares/*` | Share management |
| `GET` | `/api/v1/contacts/{id}/vcard` | Export as vCard |
| `GET` | `/api/v1/contacts/export` | Export all as vCard |
| `POST` | `/api/v1/contacts/import` | Import from vCard |

### Calendar REST API (`/api/v1/calendars`)

| Method | Endpoint | Description |
|---|---|---|
| `GET/POST/PUT/DELETE` | `/api/v1/calendars/*` | Calendar CRUD |
| `GET/POST/PUT/DELETE` | `/api/v1/calendars/events/*` | Event CRUD |
| `POST` | `/api/v1/calendars/events/{id}/rsvp` | RSVP to invitation |
| `GET` | `/api/v1/calendars/events/search` | Search events |
| `GET/POST/DELETE` | `/api/v1/calendars/{id}/shares/*` | Share management |
| `GET` | `/api/v1/calendars/{id}/export` | Export as iCalendar |
| `POST` | `/api/v1/calendars/events/import` | Import from iCalendar |

### Notes REST API (`/api/v1/notes`)

| Method | Endpoint | Description |
|---|---|---|
| `GET/POST/PUT/DELETE` | `/api/v1/notes/*` | Note CRUD |
| `GET` | `/api/v1/notes/search` | Search notes |
| `GET/POST` | `/api/v1/notes/{id}/versions/*` | Version history & restore |
| `GET/POST/PUT/DELETE` | `/api/v1/notes/folders/*` | Folder management |
| `GET/POST/DELETE` | `/api/v1/notes/{id}/shares/*` | Share management |

### DAV Endpoints

| Protocol | Base Path | Standards |
|---|---|---|
| CardDAV | `/carddav/{userId}/addressbook/` | RFC 6352, RFC 6764 |
| CalDAV | `/caldav/{userId}/calendars/` | RFC 4791, RFC 6764 |

---

## Database Changes

Phase 3 adds the following tables to `CoreDbContext`:

**Contacts:** `Contacts`, `ContactEmails`, `ContactPhones`, `ContactAddresses`, `ContactCustomFields`, `ContactGroups`, `ContactGroupMembers`, `ContactShares`

**Calendar:** `Calendars`, `CalendarEvents`, `EventAttendees`, `EventReminders`, `CalendarShares`

**Notes:** `Notes`, `NoteVersions`, `NoteFolders`, `NoteTags`, `NoteLinks`, `NoteShares`

### Migration Required

After upgrading, apply database migrations:

```bash
dotnet ef database update --context CoreDbContext
```

All new tables use schema/prefix naming consistent with existing tables (PostgreSQL/SQL Server: `core.` schema; MariaDB: `core_` prefix).

---

## gRPC Services

Three new gRPC services for core-to-module communication:

| Service | RPC Count | Protocol |
|---|---|---|
| `ContactsService` | 7 | Unix socket / Named Pipe |
| `CalendarGrpcService` | 11 | Unix socket / Named Pipe |
| `NotesGrpcService` | 10 | Unix socket / Named Pipe |

---

## Compatibility

### Client Compatibility

| Client | CardDAV | CalDAV | Tested |
|---|---|---|---|
| DAVx5 (Android) | ✓ | ✓ | ✓ |
| Thunderbird | ✓ | ✓ | ✓ |
| iOS / macOS | ✓ | ✓ | ✓ |
| GNOME Online Accounts | ✓ | ✓ | ✓ |

### Breaking Changes

None. Phase 3 is additive — no existing APIs, database schemas, or configurations are modified.

### Configuration Changes

No new `appsettings.json` entries are required. PIM modules use core infrastructure configuration (database, authentication, logging) already in place.

---

## Known Limitations

1. **Contact avatars** — avatar upload/storage is recognized but not fully implemented (planned for future release)
2. **Recurrence engine** — recurrence expansion service and occurrence projection are structural but not production-hardened for complex RRULE patterns
3. **Reminder pipeline** — reminder dispatch (in-app notifications and email) infrastructure is defined but not wired to a delivery backend
4. **Markdown sanitization** — note content is stored as-is without server-side XSS sanitization; sanitization is a presentation-layer concern (deferred)
5. **Note team sharing** — notes can only be shared with individual users, not teams
6. **Audit trail fields** — `CreatedByUserId`/`UpdatedByUserId` audit columns require EF migration (deferred)
7. **Cross-module integration** — unified navigation, cross-links in UI, and shared notification patterns (Phase 3.5 scope) are deferred

---

## Upgrade Instructions

### From Phase 2 (Files + Chat)

1. **Back up** your database and file storage (see [UPGRADING.md](server/UPGRADING.md))
2. **Update binaries** — deploy the new build
3. **Run migrations:**
   ```bash
   dotnet ef database update --context CoreDbContext
   ```
4. **Restart** the DotNetCloud service
5. **Verify** PIM modules are loaded:
   ```bash
   curl -k https://localhost:5443/health
   ```
   All three modules (`dotnetcloud.contacts`, `dotnetcloud.calendar`, `dotnetcloud.notes`) should appear healthy.

### Reverse Proxy

If using a reverse proxy (Nginx, IIS, Apache), ensure PROPFIND and REPORT HTTP methods are forwarded for CardDAV/CalDAV endpoints. See the [PIM Admin Guide](PIM_MODULES.md) for configuration examples.

---

## What's Next

- **Phase 3.5** — Cross-module integration (unified navigation, shared notifications, cross-links in UI)
- **Phase 4** — Additional modules and platform hardening
- **Milestone D completion** — Phase 3.6 (Migration Foundation) and 3.7 (Testing & Quality Gates) are complete; 3.8 (this release) closes out Phase 3 documentation
