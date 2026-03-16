# Phase 1 Completion Plan

**Goal:** Close all remaining Phase 1 items and mark the phase as complete.

**Scope:** Files module (server + web UI + desktop sync client + documentation)

---

## Summary

| Category | Items | Effort |
|---|---|---|
| Sprint 1: End-to-End Verification | 24 items | Manual testing against live deployment |
| Sprint 2: Deferred UI Features | 6 items | JS interop + Blazor component work |
| Sprint 3: Deferred Backend Features | 4 items | Service-layer additions |
| Sprint 4: Desktop Sync Hardening | 2 items | SyncEngine + SyncTray improvements |
| Sprint 5: Module Integration Verification | 7 items | Live deployment checks |
| Sprint 6: Security Verification | 7 items | Security audit + live testing |
| External Blockers (skip for now) | 1 item | MariaDB migration (Pomelo .NET 10) |

---

## Sprint 1: End-to-End Verification

**What:** All core functionality is coded and unit-tested. These items need manual verification against the live deployment at `https://mint22:15443`.

**Prerequisite:** Server deployed, at least one test user account, Collabora CODE running.

### 1.1 File Operations (web UI)
- ☐ Upload a file via the web UI and confirm it appears in the file browser
- ☐ Download the uploaded file and verify content matches
- ☐ Rename a file and confirm the name updates in the listing
- ☐ Move a file to a subfolder and confirm it appears in the new location
- ☐ Copy a file and confirm both copies exist
- ☐ Delete a file and confirm it moves to trash

### 1.2 Folder Operations (web UI)
- ☐ Create a new folder via the web UI
- ☐ Navigate into the folder and back out
- ☐ Rename a folder
- ☐ Move a folder into another folder
- ☐ Delete a folder (confirm children also trash)

### 1.3 Chunked Upload & Dedup
- ☐ Upload a file >4MB and confirm chunked upload completes
- ☐ Upload the same file again and confirm dedup (no new chunks stored)
- ☐ Interrupt an upload (close browser mid-upload), re-open, and verify resume picks up missing chunks

### 1.4 Versioning
- ☐ Upload a new version of an existing file
- ☐ Open version history panel and confirm both versions appear
- ☐ Download a previous version
- ☐ Restore a previous version and verify content reverts

### 1.5 Sharing
- ☐ Share a file with another user (Read permission) and confirm they can view it
- ☐ Create a public link and access it in an incognito browser
- ☐ Set a password on a public link and confirm it's required to access
- ☐ Set download limit on a public link and confirm it blocks after limit reached

### 1.6 Quotas
- ☐ Set a low quota for a test user via admin dashboard
- ☐ Upload files until quota is exceeded and confirm rejection error

### 1.7 Collabora / WOPI
- ☐ Open a .docx file in the Collabora editor from the file browser
- ☐ Edit the document, save, and confirm a new version is created
- ☐ Verify WOPI CheckFileInfo returns correct metadata (curl test)

### 1.8 File Preview
- ☐ Preview an image file (JPEG/PNG)
- ☐ Preview a video file
- ☐ Preview a PDF file
- ☐ Preview a text/code file
- ☐ Preview a Markdown file
- ☐ Confirm unsupported formats show "Download File" fallback

### 1.9 Tags & Comments
- ☐ Add a tag to a file via the UI
- ☐ Filter files by tag in the sidebar
- ☐ Remove a tag
- ☐ Add a comment to a file
- ☐ Reply to a comment (threaded)
- ☐ Edit and delete a comment

### 1.10 Sync Endpoints
- ☐ Call `GET /api/v1/files/sync/changes?since=<timestamp>` and verify correct change data
- ☐ Call `POST /api/v1/files/sync/reconcile` with local state and verify diff
- ☐ Call `GET /api/v1/files/sync/tree` and verify complete folder tree with hashes

### 1.11 Integration Tests
- ☐ Run `dotnet test tests/DotNetCloud.Integration.Tests/` against PostgreSQL — all pass
- ☐ Run integration tests against SQL Server — all pass

---

## Sprint 2: Deferred UI Features

**What:** UI enhancements that were deferred during initial implementation because they require JS interop wiring.

### 2.1 Right-Click Context Menu
- ☐ Create JS interop for floating context menu positioning (`contextmenu` event)
- ☐ Create `FileContextMenu.razor` component with actions: Rename, Move, Copy, Share, Delete, Download
- ☐ Wire context menu to file items in both grid and list views
- ☐ Dismiss menu on click-outside or Escape

### 2.2 Drag-and-Drop Move to Folder
- ☐ Create JS interop bridge for `dragstart`, `dragover`, `drop` events
- ☐ Add drag handles to file items
- ☐ Highlight drop-target folders during drag
- ☐ Call `MoveAsync` on drop
- ☐ Show error toast if move fails (permission, name conflict)

### 2.3 Upload Queue Management
- ☐ Add chunk-level cancellation tokens to `ChunkedUploadService`
- ☐ Create JS interop to abort in-flight `fetch` requests
- ☐ Wire Pause/Resume/Cancel per-file in `UploadProgressPanel` to actual chunk-level control

### 2.4 Paste Image Upload
- ☐ Create JS interop for `window.paste` event to capture clipboard images
- ☐ Auto-generate filename from timestamp (e.g., `paste-2026-03-16-143022.png`)
- ☐ Trigger upload flow with pasted image data

### 2.5 Upload Size Validation
- ☐ Expose `MaxUploadSize` from server config to the UI layer (API endpoint or Blazor cascade)
- ☐ Validate file size before starting upload
- ☐ Show clear error message for oversized files

### 2.6 Share Dialog — Existing Shares List
- ☐ Wire `GET /api/v1/files/{nodeId}/shares` to ShareDialog on open
- ☐ Display existing shares with recipient, permission level, and creation date
- ☐ Add inline "Remove" action per existing share
- ☐ Refresh share list after adding/removing

---

## Sprint 3: Deferred Backend Features

**What:** Backend enhancements that were deliberately deferred.

### 3.1 Range Requests for Partial Downloads
- ☐ Verify `ConcatenatedStream` seekability works correctly with HTTP range requests
- ☐ Test with large video files seeking in browser player
- ☐ Test with download resumption (e.g., `curl --range`)

### 3.2 Configurable Version Retention Limits
- ☐ Add `MaxVersionsPerFile` and `VersionRetentionDays` to `FilesOptions` (may already exist in `FilesAdminSettings`)
- ☐ Enforce limits in `VersionService` — auto-delete oldest unlabeled versions when exceeded
- ☐ Wire admin settings to the backend config

### 3.3 Share Notifications (Notification Integration)
- ☐ Send notification to share recipients when a file is shared with them
- ☐ Notify share creator on first access of public link
- ☐ Send notification when share is about to expire (e.g., 24h before)

### 3.4 Quota Warning Notifications
- ☐ Send notification at 80% quota usage
- ☐ Send notification at 95% quota usage
- ☐ These depend on the notification system (Phase 2 — already complete), so wire them up

---

## Sprint 4: Desktop Sync Client Hardening

**What:** Remaining sync client improvements and end-to-end verification.

### 4.1 FileSystemWatcher Debounce
- ☐ Add coalescing timer to `SyncEngine` (e.g., 2-second debounce after last FSW event)
- ☐ Batch rapid file changes into a single sync cycle
- ☐ Test: rapid-save a file 10 times → should produce ≤2 sync cycles, not 10

### 4.2 End-to-End Sync Verification
- ☐ Install SyncService on Windows as a Windows Service
- ☐ Install SyncService on Linux as a systemd unit
- ☐ Add account via SyncTray OAuth2 flow
- ☐ Create a file on server → verify it appears in local sync folder
- ☐ Create a file in local sync folder → verify it appears on server
- ☐ Modify a file on both sides simultaneously → verify conflict copy is created
- ☐ Disconnect network → queue changes → reconnect → verify sync completes
- ☐ Upload a 100MB+ file → verify chunked transfer works
- ☐ Verify SyncTray displays correct status (idle, syncing, error, offline)
- ☐ Verify SyncTray selective sync (exclude a folder, verify it doesn't sync)
- ☐ Verify multi-account support (add two server accounts, both sync independently)

---

## Sprint 5: Module Integration Verification

**What:** Verify the Files module integrates correctly with the core platform.

### 5.1 Module System
- ☐ Verify Files module loads via module system and responds to health checks (`/health`)
- ☐ Verify gRPC communication works between core and Files host process
- ☐ Verify module can start and stop cleanly

### 5.2 Observability
- ☐ Verify Files module logs are enriched with module context (check Serilog output)
- ☐ Verify Files module errors are handled gracefully (trigger a 500 and check error boundary)
- ☐ Verify OpenTelemetry traces include Files operations (check Jaeger/OTLP output if configured)

### 5.3 Documentation & API
- ☐ Verify OpenAPI/Swagger documentation is generated for Files API endpoints
- ☐ Verify internationalization works for Files UI strings (switch locale, check UI labels)

---

## Sprint 6: Security Verification

**What:** Security audit and penetration testing of the Files module.

### 6.1 Authentication & Authorization
- ☐ Verify all `/api/v1/files/*` endpoints return 401 without auth token
- ☐ Verify public link access works WITHOUT authentication (`/api/v1/files/public/{linkToken}`)
- ☐ Verify public link passwords are stored hashed (check DB — `LinkPasswordHash` column)
- ☐ Verify WOPI tokens are scoped, signed, and time-limited (try expired/tampered token)

### 6.2 Input Validation
- ☐ Attempt path traversal: create file named `../../etc/passwd` → should be rejected
- ☐ Attempt path traversal: rename file to `../../../tmp/evil` → should be rejected
- ☐ Upload a file exceeding quota → should return clear error, not crash

### 6.3 Rate Limiting
- ☐ Verify rate limiting applies to upload endpoints (if configured)
- ☐ Verify rate limiting returns 429 with `Retry-After` header

---

## External Blockers (Not Actionable Now)

| Item | Blocker | Action |
|---|---|---|
| MariaDB initial migration | Pomelo EF Core provider doesn't support .NET 10 yet | Wait for Pomelo release, then generate migration |

---

## Execution Order

Recommended sequence for maximum efficiency:

1. **Sprint 1** (Verification) — Do first. Many items may already work. Fast to check, and any failures guide what to fix.
2. **Sprint 6** (Security) — Do alongside Sprint 1. Quick manual tests.
3. **Sprint 5** (Module Integration) — Quick verification, do alongside Sprint 1.
4. **Sprint 3** (Backend) — Notifications wiring + version retention. Moderate effort.
5. **Sprint 4** (Sync) — End-to-end sync testing requires Windows + Linux clients.
6. **Sprint 2** (UI) — Deferred UI features. Most effort. Can be deprioritized if Phase 1 launch doesn't require context menus or paste upload.

---

## What Can Be Safely Deferred Past Phase 1 Launch?

These items are nice-to-have and won't block a usable Phase 1 release:

| Item | Reason to Defer |
|---|---|
| Right-click context menu | Actions already available via buttons and selection mode |
| Drag-and-drop move to folder | Move available via selection mode + folder picker |
| Paste image upload | Standard file upload works fine |
| Upload queue management | Basic pause/cancel exists; chunk-level control is polish |
| Upload size validation | Server already rejects oversized files; client-side is UX polish |
| Share expiration notifications | Not critical for launch |
| Share creator first-access notification | Not critical for launch |
| Admin retention per organization | Single-instance config sufficient for launch |
| MariaDB migration | PostgreSQL and SQL Server are the primary targets |

**If deferring these, Phase 1 can close with ~4 sprints instead of 6.**
