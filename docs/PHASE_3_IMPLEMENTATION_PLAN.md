# Phase 3 Implementation Plan

> Goal: Deliver Contacts, Calendar, and Notes as a complete PIM suite with CardDAV/CalDAV compatibility and production-ready web + client integration.

> Scope: Server modules, shared contracts, data layer, APIs, Blazor UI, sync/client touchpoints, testing, documentation, and migration groundwork.

> Status: Planned

---

## 1. Success Criteria

- ☐ Users can create, read, update, delete, and search contacts, calendars/events, and notes.
- ☐ CardDAV endpoints interoperate with common clients (Thunderbird, DAVx5, iOS/macOS contacts).
- ☐ CalDAV endpoints interoperate with common clients (Thunderbird, DAVx5, iOS/macOS calendar).
- ☐ Notes support Markdown editing, folders/tags, and full-text search.
- ☐ Permissions and tenant boundaries are enforced consistently across all three modules.
- ✓ Background jobs (recurrence expansion, reminder dispatch, sync jobs) run reliably.
- ☐ API + UI + integration tests pass in CI for PostgreSQL and SQL Server.
- ☐ Admin and user documentation for all three modules is complete.

---

## 2. Dependencies And Preconditions

- ☐ Phase 0-2 foundation is stable in main.
- ☐ Module host patterns from Files/Chat are available as templates.
- ☐ OpenIddict auth + capability enforcement remains the single authorization model.
- ☐ Existing event bus and notification infrastructure is reusable.
- ☐ CI matrix remains green before Phase 3 branch work starts.

---

## 3. Work Breakdown Structure

## 3.1 Architecture And Contracts (phase-3.1)

### Objectives
- Define shared contracts for Contacts, Calendar, and Notes without leaking module internals.
- Keep interfaces capability-driven and aligned with existing DotNetCloud.Core patterns.

### Deliverables
- ✓ Core DTOs and contracts for:
  - ✓ Contacts (person/org/group, phone/email/address, metadata)
  - ✓ Calendars (calendar, event, attendee, recurrence, reminders)
  - ✓ Notes (note document, folder, tag, note metadata)
- ✓ Event contracts:
  - ✓ ContactCreated/Updated/DeletedEvent
  - ✓ CalendarEventCreated/Updated/DeletedEvent
  - ✓ NoteCreated/Updated/DeletedEvent
- ✓ Capability interfaces and tier mapping for each module.
- ✓ Validation and error code extensions for new module domains.

### Exit Criteria
- ✓ Contracts compile in shared abstractions.
- ✓ Public XML docs are present for all new public interfaces/types.
- ✓ Contract tests pass.

## 3.2 Contacts Module (phase-3.2)

### Objectives
- Deliver first-class contact management and CardDAV compatibility.

### Deliverables
- ✓ Module projects:
  - ✓ DotNetCloud.Modules.Contacts
  - ✓ DotNetCloud.Modules.Contacts.Data
  - ✓ DotNetCloud.Modules.Contacts.Host
- ✓ Data model + EF configurations for contacts, groups, addresses, communication methods.
- ✓ REST API endpoints for CRUD, bulk import/export, search.
- ✓ CardDAV endpoints:
  - ✓ Principal + addressbook discovery
  - ✓ vCard get/put/delete
  - ✓ Sync token/change tracking
- ✓ Contact avatar support and attachment metadata.
- ✓ Contact-sharing model (user/team scoped permissions).

### Exit Criteria
- ☐ Contacts UI can fully manage records.
- ☐ CardDAV interoperability tests pass with at least two external clients.
- ☐ Audit trail entries are recorded for sensitive operations.

## 3.3 Calendar Module (phase-3.3)

### Objectives
- Deliver robust calendars/events with recurrence, invitations, reminders, and CalDAV compatibility.

### Deliverables
- ✓ Module projects:
  - ✓ DotNetCloud.Modules.Calendar
  - ✓ DotNetCloud.Modules.Calendar.Data
  - ✓ DotNetCloud.Modules.Calendar.Host
- ✓ Data model for calendars, events, attendees, recurrence rules, reminders, exception instances.
- ✓ REST API endpoints for CRUD, RSVP, calendar sharing, event search/filter.
- ✓ CalDAV endpoints:
  - ✓ Calendar discovery and collections
  - ✓ iCalendar get/put/delete
  - ✓ Sync token/change tracking
- ✓ Recurrence engine and occurrence expansion service.
- ✓ Reminder/notification pipeline (in-app + push if configured).
- ✓ gRPC service (11 RPCs) for core ↔ module communication.
- ✓ iCalendar RFC 5545 import/export service.
- ✓ 82 passing tests (39 existing + 43 new).

### Exit Criteria
- ☐ Recurring events behave correctly across timezones.
- ☐ Invitation lifecycle (send/respond/update/cancel) works end-to-end.
- ☐ CalDAV interoperability tests pass with at least two external clients.

## 3.4 Notes Module (phase-3.4)

### Objectives
- Deliver Markdown-centric note-taking with folders, tags, and search.

### Deliverables
- ✓ Module projects:
  - ✓ DotNetCloud.Modules.Notes
  - ✓ DotNetCloud.Modules.Notes.Data
  - ✓ DotNetCloud.Modules.Notes.Host
- ✓ Data model for notes, versions, folders, tags, links, and sharing metadata.
- ✓ REST API endpoints for CRUD, move/copy, tagging, search, version history.
- ✓ Markdown rendering pipeline with sanitization and safe preview.
- ✓ Rich-editor integration (MarkdownEditor Blazor component with toolbar + live preview).
- ✓ Note link references to Files and Calendar entities.
- ✓ gRPC service (10 RPCs) for core ↔ module communication.
- ✓ Note sharing with per-user permission (ReadOnly/ReadWrite).
- ✓ 50 passing tests.

### Exit Criteria
- ✓ Notes support version history restore and conflict-safe updates.
- ✓ Search includes title/content/tags and returns stable relevance ordering.
- ✓ Markdown sanitization tests pass against XSS payload cases.

## 3.5 Cross-Module Integration (phase-3.5)

### Objectives
- Make Contacts, Calendar, and Notes feel cohesive across UI and APIs.

### Deliverables
- ☐ Unified navigation entries and module registration in the Blazor shell.
- ☐ Shared notification patterns for invites, reminders, mentions, and shares.
- ☐ Cross-links:
  - ☐ Calendar event references contact records
  - ☐ Notes can mention contacts and events
  - ☐ Contacts can surface related notes/events
- ☐ Consistent authorization, audit logging, and soft-delete behavior.

### Exit Criteria
- ☐ Cross-module links resolve correctly in UI.
- ☐ End-to-end flows pass integration tests.

## 3.6 Migration Foundation (phase-3.6) ✅

### Objectives
- Build migration groundwork for NextCloud import paths for contacts/calendar/notes.

### Deliverables
- ✓ Import contract interfaces and pipeline architecture.
- ✓ Parsers/transformers for vCard and iCalendar migration inputs.
- ✓ Initial Notes import adapter for markdown/plain exports.
- ✓ Dry-run mode with import report and conflict summary.

### Exit Criteria
- ✓ Dry-run import generates deterministic reports.
- ✓ Real import path works for representative fixture datasets.

### Implementation Notes
- Core contracts: `ImportDtos.cs`, `IImportProvider`, `IImportPipeline` in `DotNetCloud.Core.Import` namespace.
- `ImportPipelineService` routes requests to module providers by `ImportDataType`.
- Three providers: `ContactsImportProvider` (vCard 3.0), `CalendarImportProvider` (iCalendar RFC 5545), `NotesImportProvider` (JSON manifest or raw Markdown).
- Dry-run: `ImportRequest.DryRun = true` → validates/parses without persisting, returns full `ImportReport`.
- 51 tests: 8 pipeline + 12 contacts + 13 calendar + 18 notes.

## 3.7 Testing And Quality Gates (phase-3.7)

### Objectives
- Ensure production confidence before Phase 3 completion.

### Deliverables
- ✓ Unit test suites for all three modules (domain, handlers, validators).
- ✓ Integration tests for REST and DAV endpoints.
- ✓ Compatibility test matrix:
  - ✓ CardDAV clients
  - ✓ CalDAV clients
- ✓ Security tests:
  - ✓ authorization bypass attempts
  - ✓ tenant isolation
  - ✓ markdown XSS/unsafe content
- ✓ Performance checks for large contact lists and recurring-event expansion.

### Exit Criteria
- ✓ Test suites are green in CI.
- ✓ No P0/P1 defects open for Phase 3 scope.

### Implementation Notes
- 224 new tests across 8 test files.
- ContactShareServiceTests (9), ContactSecurityTests (8), CardDavInteropTests (13), ContactPerformanceTests (4).
- CalendarShareServiceTests (8), CalendarSecurityTests (10), CalDavInteropTests (12), CalendarPerformanceTests (4).
- NoteSecurityTests (13) — includes 5 XSS attack vector tests documenting store-as-is behavior.
- Full CI: 2,700 tests, 0 failures.

## 3.8 Documentation And Release Readiness (phase-3.8)

### Objectives
- Prepare operators and users to deploy and use Phase 3 features.

### Deliverables
- ✓ Admin docs for Contacts, Calendar, Notes configuration and operations.
- ✓ User guides for key workflows (import, sharing, sync, troubleshooting).
- ✓ API docs for all new REST and DAV endpoints.
- ✓ Upgrade/release notes with migration and compatibility caveats.

### Exit Criteria
- ✓ Documentation reviewed and linked from docs index.
- ✓ Release checklist approved.

### Implementation Notes
- Admin docs: `docs/admin/PIM_MODULES.md` — covers all three modules (config, database, auth, CardDAV/CalDAV, import, events, gRPC, troubleshooting).
- User guides: `docs/user/CONTACTS.md`, `docs/user/CALENDAR.md`, `docs/user/NOTES.md` — end-user workflows for managing records, importing, sharing, syncing, and troubleshooting.
- API docs: `docs/api/CONTACTS.md`, `docs/api/CALENDAR.md`, `docs/api/NOTES.md` — full REST + DAV endpoint reference with request/response schemas, error codes, and enums.
- Release notes: `docs/admin/PHASE_3_RELEASE_NOTES.md` — new features, API summary, database changes, migration instructions, known limitations.
- Index linkage: `docs/api/README.md` updated with module API links; `README.md` updated with admin, user guide, and release note links.

---

## 4. Suggested Delivery Sequence

1. phase-3.1 Architecture And Contracts
2. phase-3.2 Contacts Module
3. phase-3.3 Calendar Module
4. phase-3.4 Notes Module
5. phase-3.5 Cross-Module Integration
6. phase-3.6 Migration Foundation
7. phase-3.7 Testing And Quality Gates
8. phase-3.8 Documentation And Release Readiness

---

## 5. Milestones

### Milestone A: Contracts + Contacts MVP
- ☐ phase-3.1 complete
- ☐ phase-3.2 complete (REST + basic CardDAV)

### Milestone B: Calendar MVP + Interop
- ✓ phase-3.3 complete (REST + recurrence + basic CalDAV)

### Milestone C: Notes MVP + Integration
- ☐ phase-3.4 complete
- ☐ phase-3.5 complete

### Milestone D: Import + Hardening + Docs
- ✓ phase-3.6 complete
- ✓ phase-3.7 complete
- ✓ phase-3.8 complete

---

## 6. Risks And Mitigations

- ☐ DAV interoperability edge cases:
  - Mitigation: maintain fixture-based compatibility tests and client-specific adapters when unavoidable.
- ☐ Recurrence/timezone complexity:
  - Mitigation: enforce UTC storage + timezone-aware projection tests and DST-focused scenarios.
- ☐ Search and performance degradation at scale:
  - Mitigation: baseline benchmarks early and add targeted indexing before beta.
- ☐ Scope growth due to migration tooling:
  - Mitigation: ship migration in bounded iterations (dry-run first, then import execution).

---

## 7. Definition Of Done (Phase 3)

- ☐ All phase-3.x items marked complete.
- ☐ Interoperability validation completed for CardDAV and CalDAV.
- ☐ Security and performance quality gates passed.
- ☐ Documentation and release notes published.
- ☐ Phase status updated in project tracking documents.
