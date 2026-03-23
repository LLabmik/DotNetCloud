# PIM Modules — Admin Configuration Guide

> **Last Updated:** 2026-07-16  
> **Applies to:** Contacts, Calendar, Notes modules (Phase 3)

---

## Overview

The PIM (Personal Information Management) suite includes three modules:

| Module | Module ID | Description |
|---|---|---|
| **Contacts** | `dotnetcloud.contacts` | Contact management with CardDAV sync |
| **Calendar** | `dotnetcloud.calendar` | Calendars and events with CalDAV sync |
| **Notes** | `dotnetcloud.notes` | Markdown notes with folders, tags, and versioning |

All three modules follow the standard DotNetCloud module architecture (separate Core, Data, and Host projects) and use the shared `CoreDbContext` for persistence.

---

## Module Registration

PIM modules are discovered automatically via `ProjectReference` from `DotNetCloud.Core.Server`. No manual registration is required. Each module declares a manifest with its required capabilities and published/subscribed events.

### Required Core Capabilities

| Capability | Contacts | Calendar | Notes |
|---|---|---|---|
| `INotificationService` | ✓ | ✓ | ✓ |
| `IUserDirectory` | ✓ | ✓ | ✓ |
| `ICurrentUserContext` | ✓ | ✓ | ✓ |
| `IAuditLogger` | ✓ | ✓ | ✓ |
| `ICrossModuleLinkResolver` | ✓ | ✓ | ✓ |
| `IContactDirectory` | — | ✓ | ✓ |
| `ICalendarDirectory` | — | — | ✓ |

If any required capability is unavailable, the module will not start. Check logs for capability resolution errors if a module fails to initialize.

---

## Database

All three modules store data in the shared `CoreDbContext`. No separate databases or connection strings are needed.

### Supported Providers

| Provider | Schema/Prefix | Status |
|---|---|---|
| PostgreSQL | `core.` schema | Default, fully supported |
| SQL Server | `core.` schema | Fully supported |
| MariaDB | `core_` prefix | Pending Pomelo .NET 10 release |

### Migrations

PIM module entity configurations are included in the standard Core.Data migration path:

```bash
# Add migration (PostgreSQL)
dotnet ef migrations add AddPimModules \
  --project src/Core/DotNetCloud.Core.Data \
  --context CoreDbContext

# Apply
dotnet ef database update --context CoreDbContext
```

### Key Tables

**Contacts:**
`Contacts`, `ContactEmails`, `ContactPhones`, `ContactAddresses`, `ContactCustomFields`, `ContactGroups`, `ContactGroupMembers`, `ContactShares`

**Calendar:**
`Calendars`, `CalendarEvents`, `EventAttendees`, `EventReminders`, `CalendarShares`, `ReminderLogs`

**Notes:**
`Notes`, `NoteVersions`, `NoteFolders`, `NoteTags`, `NoteLinks`, `NoteShares`

All tables use soft-delete with automatic `DeletedAt` timestamps and query filters.

---

## Configuration

The PIM modules currently use sensible defaults and do not require module-specific `appsettings.json` entries. They rely on core infrastructure configuration (database, authentication, logging, etc.) already defined in your deployment.

### Standard Core Configuration Used

| Section | Purpose |
|---|---|
| `ConnectionStrings:CoreDb` | Database for all PIM data |
| `OpenIddict` | Authentication for API endpoints |
| `Serilog` | Structured logging |
| `OpenTelemetry` | Metrics and tracing |

### Future Configuration (Planned)

These options may be added in future releases to make current defaults configurable:

```json
{
  "Contacts": {
    "StoragePath": "/var/lib/dotnetcloud/contacts",
    "MaxImportBatchSize": 1000,
    "VCardExportThrottlePerMinute": 60
  },
  "Calendar": {
    "RecurrenceExpansionMaxInstances": 1000,
    "DefaultReminderMinutes": 15,
    "ReminderScanIntervalSeconds": 30,
    "ReminderLookAheadHours": 24
  },
  "Notes": {
    "MaxVersionsPerNote": 100,
    "FullTextSearchIndexRefreshSeconds": 30
  }
}
```

> **Note:** The Calendar recurrence engine and reminder dispatch service are active with the defaults shown above. These values are currently hardcoded; configurable overrides will be added in a future release.

### Contacts File Storage

Contact avatars and attachments are stored on disk. The storage location is resolved in the following order:

1. `Contacts:StoragePath` in `appsettings.json`
2. `DOTNETCLOUD_DATA_DIR` environment variable (files stored under `contacts/` subdirectory)
3. Default: `./data` (relative to the working directory)

**File size limits:**

| Type | Maximum Size |
|---|---|
| Avatar | 5 MB |
| Attachment | 25 MB |

**Allowed avatar formats:** JPEG, PNG, GIF, WebP, SVG

Ensure the configured storage directory has appropriate read/write permissions for the application process.

---

## Authentication & Authorization

### API Authentication

All PIM REST endpoints require Bearer authentication via OpenIddict:

```
Authorization: Bearer <access_token>
```

CardDAV and CalDAV endpoints also require Bearer authentication. Clients that support OAuth (DAVx5, Thunderbird with OAuth) should use the standard authorization flow. Clients that only support Basic auth are not currently supported.

### Tenant Isolation

Each user's data is isolated by `OwnerId`. Users can only access:

1. Records they own
2. Records explicitly shared with them (or their team)
3. Records they have appropriate permissions for (ReadOnly or ReadWrite)

There is no global admin override for PIM data. Administrators manage users and teams through the core admin API, not directly through PIM module data.

### Sharing Permissions

| Permission | Can View | Can Edit | Can Delete | Can Re-share |
|---|---|---|---|---|
| `ReadOnly` | ✓ | — | — | — |
| `ReadWrite` | ✓ | ✓ | — | — |

Only the record owner can delete records or manage shares.

---

## CardDAV / CalDAV Integration

### Discovery Endpoints

| Standard | Well-Known URL | Purpose |
|---|---|---|
| CardDAV | `/.well-known/carddav` | RFC 6764 address book discovery |
| CalDAV | `/.well-known/caldav` | RFC 6764 calendar discovery |

These endpoints redirect clients to the user's address book or calendar collection.

### Client Compatibility

Tested with these DAV clients:

| Client | CardDAV | CalDAV | Notes |
|---|---|---|---|
| **DAVx5** (Android) | ✓ | ✓ | OAuth Bearer recommended |
| **Thunderbird** | ✓ | ✓ | Use CardBook add-on for contacts |
| **iOS/macOS** | ✓ | ✓ | Accounts → Add CalDAV/CardDAV |
| **GNOME Online Accounts** | ✓ | ✓ | WebDAV accounts |

### Sync Token / ETag

- Each contact and event has an `ETag` field for conflict detection
- Calendar collections support `sync-token` for incremental change tracking
- Clients use `If-Match` headers for safe updates (412 Precondition Failed on mismatch)

### Reverse Proxy Considerations

If running behind a reverse proxy (Nginx, IIS, Apache), ensure these HTTP methods are forwarded:

```
GET, PUT, DELETE, OPTIONS, PROPFIND, REPORT
```

Example Nginx snippet:

```nginx
location /carddav {
    proxy_pass http://localhost:5080;
    proxy_set_header Host $host;
    proxy_pass_request_headers on;
    # Allow WebDAV methods
    proxy_method $request_method;
}

location /caldav {
    proxy_pass http://localhost:5080;
    proxy_set_header Host $host;
    proxy_pass_request_headers on;
}
```

For IIS, ensure URL Rewrite + ARR forwards all HTTP methods, not just GET/POST.

---

## Import / Migration

### Import Pipeline

The core import infrastructure supports bulk data import for all three PIM modules:

| Data Type | Format | Provider |
|---|---|---|
| Contacts | vCard 3.0 (`.vcf`) | `ContactsImportProvider` |
| Calendar Events | iCalendar (`.ics`) | `CalendarImportProvider` |
| Notes | JSON manifest or Markdown | `NotesImportProvider` |

### Import Sources

| Source | Description |
|---|---|
| `Generic` | Standard format import (default) |
| `Nextcloud` | NextCloud export format |
| `StandardFile` | Standard Notes export |

### Conflict Strategies

| Strategy | Behavior |
|---|---|
| `Skip` | Ignore records that match existing data (default) |
| `Overwrite` | Replace existing records with imported data |
| `CreateCopy` | Import alongside existing records |

### Dry-Run Mode

Set `DryRun = true` on import requests to validate data without persisting:

- Full parsing and validation runs
- Returns detailed `ImportReport` with per-item status
- No database writes occur
- Useful for pre-flight checks before large imports

### Duplicate Detection

| Module | Detection Method |
|---|---|
| Contacts | Primary email address match |
| Calendar | Start time + title combination |
| Notes | Title + owner match |

---

## Event Bus Integration

PIM modules publish and subscribe to events for cross-module coordination:

### Published Events

| Module | Events |
|---|---|
| Contacts | `ContactCreatedEvent`, `ContactUpdatedEvent`, `ContactDeletedEvent`, `ResourceSharedEvent` |
| Calendar | `CalendarEventCreatedEvent`, `CalendarEventUpdatedEvent`, `CalendarEventDeletedEvent`, `CalendarEventRsvpEvent`, `CalendarReminderTriggeredEvent`, `ReminderTriggeredEvent`, `ResourceSharedEvent` |
| Notes | `NoteCreatedEvent`, `NoteUpdatedEvent`, `NoteDeletedEvent`, `ResourceSharedEvent`, `UserMentionedEvent` |

### Subscribed Events

| Module | Subscribes To |
|---|---|
| Contacts | `CalendarEventCreatedEvent`, `NoteCreatedEvent` |
| Calendar | `ContactCreatedEvent`, `ContactDeletedEvent` |
| Notes | `ContactCreatedEvent`, `CalendarEventCreatedEvent` |

---

## gRPC Services

Each module exposes a gRPC service for core-to-module communication:

| Module | Service | RPCs |
|---|---|---|
| Contacts | `ContactsService` | 7 (Create, Get, List, Update, Delete, ExportVCard, ImportVCards) |
| Calendar | `CalendarGrpcService` | 11 (CRUD for calendars + events, RSVP, Export/Import iCal) |
| Notes | `NotesGrpcService` | 10 (CRUD for notes + folders, Search, Version History, Restore) |

gRPC communication uses Unix domain sockets (Linux) or Named Pipes (Windows) per the standard module isolation architecture.

---

## Monitoring & Health

PIM modules participate in the standard health check system:

- `/health` — overall system health (includes PIM module status)
- `/health/ready` — readiness probe
- `/health/live` — liveness probe

---

## Background Services

### Reminder Dispatch Service (Calendar)

The `ReminderDispatchService` is a background service that continuously scans for due reminders and dispatches notifications.

**Behavior:**

| Parameter | Default |
|---|---|
| Scan interval | 30 seconds |
| Look-ahead window | 24 hours |
| Max recurrence expansion | 1,000 occurrences per event |

**How it works:**

1. Every 30 seconds, the service queries all events with reminders whose trigger time falls within the current look-ahead window
2. For **single events**, it checks `event.StartUtc - reminder.MinutesBefore` against the current time
3. For **recurring events**, it expands occurrences using the `RecurrenceEngine` and checks each occurrence's reminders individually
4. Before dispatching, it checks the `ReminderLogs` table to prevent duplicate delivery
5. On successful dispatch, two events are published via the event bus:
   - `CalendarReminderTriggeredEvent` (Calendar-internal)
   - `ReminderTriggeredEvent` (cross-module, consumed by notification infrastructure)
6. Each dispatch is logged in the `ReminderLogs` table with success/failure status

**Duplicate Prevention:**

The `ReminderLogs` table uses a unique index on `(ReminderId, OccurrenceStartUtc)` to ensure each reminder fires exactly once per event occurrence, even across server restarts.

**Logging:**

```
[INF] Reminder dispatched for event {EventId}, occurrence {OccurrenceStartUtc}
[WRN] Reminder dispatch failed for event {EventId}: {ErrorMessage}
```

### Logging

PIM operations are logged via Serilog with structured properties:

```
[INF] Contact created {ContactId} by {UserId}
[INF] Calendar event updated {EventId} in {CalendarId}
[INF] Note version {Version} saved for {NoteId}
```

Sensitive data (contact emails, phone numbers) is masked by the `SensitiveDataMaskingEnricher` in production configurations.

### Metrics (OpenTelemetry)

Standard ASP.NET Core metrics apply to PIM API endpoints. Custom counters for operations like import batch size and share creation are planned for future releases.

---

## Backup & Restore

PIM data is stored in the same database as all other core data. The standard backup procedures documented in [BACKUP.md](BACKUP.md) apply:

- Database backups capture all PIM tables
- No separate file storage for PIM modules (unlike Files module)
- CardDAV/CalDAV sync tokens are included in database backups
- After restore, DAV clients will perform a full re-sync (expected behavior)

---

## Troubleshooting

### Module Not Starting

**Symptom:** PIM module fails to initialize, logs show capability resolution errors.

**Check:**
1. All required core capabilities are registered (see Required Core Capabilities table above)
2. Database migrations are up to date: `dotnet ef database update`
3. OpenIddict is configured and token validation is working

### CardDAV/CalDAV Clients Cannot Connect

**Check:**
1. Well-known URLs (`/.well-known/carddav`, `/.well-known/caldav`) return 301 redirects
2. Bearer authentication is working (test with `curl -H "Authorization: Bearer <token>"`)
3. Reverse proxy forwards PROPFIND and REPORT methods
4. TLS certificate is trusted by the client

### Import Errors

**Check:**
1. Use dry-run mode first to identify problematic records
2. Review the `ImportReport.Items` array for per-item error messages
3. Verify the import format matches the expected standard (vCard 3.0, iCalendar RFC 5545, or JSON manifest)
4. Check for encoding issues (UTF-8 required)

### Version Conflict on Note Update

**Symptom:** `NOTE_VERSION_CONFLICT` error when saving a note.

**Cause:** Another user or session modified the note since it was loaded.

**Resolution:** Reload the note to get the current version, merge changes, and retry with the updated `ExpectedVersion`.

### Reminders Not Firing

**Symptom:** Users report not receiving reminders for calendar events.

**Check:**
1. Verify the `ReminderDispatchService` is running (look for scan log entries every 30 seconds)
2. Check that the event has reminders configured (`EventReminders` table)
3. Verify the event is not soft-deleted (`DeletedAt IS NULL`)
4. For recurring events, ensure the RRULE is valid — invalid rules are skipped with a warning log
5. Check the `ReminderLogs` table for successful dispatches — if a log entry exists, the reminder already fired and won't fire again
6. If reminders fired but notifications weren't received, check downstream `INotificationService` configuration
