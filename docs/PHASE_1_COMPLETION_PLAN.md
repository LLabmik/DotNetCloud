# Phase 1 Completion Plan

**Goal:** Close all remaining Phase 1 items and mark the phase as complete.

**Scope:** Files module (server + web UI + desktop sync client + documentation)

---

## Summary

**Audit Status Labels (codebase-aligned):**
- `[implemented]` = implemented in code
- `[verification-only]` = code exists; still requires live/manual/env validation
- `[missing]` = not found in current code
- `[deferred]` = explicitly deferred for post-launch
- `[blocked]` = external blocker

| Category | Items | Effort | Status |
|---|---|---|---|
| Sprint 1: End-to-End Verification | 24 items | Manual testing against live deployment | Mostly `[verification-only]` (code present; live validation pending) |
| Sprint 2: Deferred UI Features | 6 items | JS interop + Blazor component work | 2.1-2.6 `[implemented]` |
| Sprint 3: Deferred Backend Features | 4 items | Service-layer additions | Mostly `[implemented]`; range tests remain `[verification-only]` |
| Sprint 4: Desktop Sync Hardening | 2 items | SyncEngine + SyncTray improvements | Debounce `[implemented]`; E2E checklist `[verification-only]` |
| Sprint 5: Module Integration Verification | 7 items | Live deployment checks | Health/logging mostly `[implemented]`; Files-host OpenAPI `[implemented]` |
| Sprint 6: Security Verification | 7 items | Security audit + live testing | Core controls mostly `[implemented]`; attack/rate-limit validation pending |
| External Blockers (skip for now) | 1 item | MariaDB migration (Pomelo .NET 10) | Blocked — waiting for Pomelo release |

---

## Sprint 1: End-to-End Verification

**What:** All core functionality is coded and unit-tested. These items need manual verification against the live deployment at `https://mint22:15443`.

**Prerequisite:** Server deployed, at least one test user account, Collabora CODE running.

### 1.1 File Operations (web UI)
- ☐ Upload a file via the web UI and confirm it appears in the file browser `[verification-only]`
- ☐ Download the uploaded file and verify content matches `[verification-only]`
- ☐ Rename a file and confirm the name updates in the listing `[verification-only]`
- ☐ Move a file to a subfolder and confirm it appears in the new location `[verification-only]`
- ☐ Copy a file and confirm both copies exist `[verification-only]`
- ☐ Delete a file and confirm it moves to trash `[verification-only]`

### 1.2 Folder Operations (web UI)
- ☐ Create a new folder via the web UI `[verification-only]`
- ☐ Navigate into the folder and back out `[verification-only]`
- ☐ Rename a folder `[verification-only]`
- ☐ Move a folder into another folder `[verification-only]`
- ☐ Delete a folder (confirm children also trash) `[verification-only]`

### 1.3 Chunked Upload & Dedup
- ☐ Upload a file >4MB and confirm chunked upload completes `[verification-only]`
- ☐ Upload the same file again and confirm dedup (no new chunks stored) `[verification-only]`
- ☐ Interrupt an upload (close browser mid-upload), re-open, and verify resume picks up missing chunks `[verification-only]`

### 1.4 Versioning
- ☐ Upload a new version of an existing file `[verification-only]`
- ☐ Open version history panel and confirm both versions appear `[verification-only]`
- ☐ Download a previous version `[verification-only]`
- ☐ Restore a previous version and verify content reverts `[verification-only]`

### 1.5 Sharing
- ☐ Share a file with another user (Read permission) and confirm they can view it `[verification-only]`
- ☐ Create a public link and access it in an incognito browser `[verification-only]`
- ☐ Set a password on a public link and confirm it's required to access `[verification-only]`
- ☐ Set download limit on a public link and confirm it blocks after limit reached `[verification-only]`

### 1.6 Quotas
- ☐ Set a low quota for a test user via admin dashboard `[verification-only]`
- ☐ Upload files until quota is exceeded and confirm rejection error `[verification-only]`

### 1.7 Collabora / WOPI
- ☐ Open a .docx file in the Collabora editor from the file browser `[verification-only]`
- ☐ Edit the document, save, and confirm a new version is created `[verification-only]`
- ☐ Verify WOPI CheckFileInfo returns correct metadata (curl test) `[verification-only]`

### 1.8 File Preview
- ☐ Preview an image file (JPEG/PNG) `[verification-only]`
- ☐ Preview a video file `[verification-only]`
- ☐ Preview a PDF file `[verification-only]`
- ☐ Preview a text/code file `[verification-only]`
- ☐ Preview a Markdown file `[verification-only]`
- ☐ Confirm unsupported formats show "Download File" fallback `[verification-only]`

### 1.9 Tags & Comments
- ☐ Add a tag to a file via the UI `[verification-only]`
- ☐ Filter files by tag in the sidebar `[verification-only]`
- ☐ Remove a tag `[verification-only]`
- ☐ Add a comment to a file `[verification-only]`
- ☐ Reply to a comment (threaded) `[verification-only]`
- ☐ Edit and delete a comment `[verification-only]`

### 1.10 Sync Endpoints
- ☐ Call `GET /api/v1/files/sync/changes?since=<timestamp>` and verify correct change data `[verification-only]`
- ☐ Call `POST /api/v1/files/sync/reconcile` with local state and verify diff `[verification-only]`
- ☐ Call `GET /api/v1/files/sync/tree` and verify complete folder tree with hashes `[verification-only]`

### 1.11 Integration Tests
- ✓ Run `dotnet test tests/DotNetCloud.Integration.Tests/` against PostgreSQL — all 133 pass `[implemented]`
- ☐ Run integration tests against SQL Server — all pass `[verification-only]`

---

## Sprint 2: Deferred UI Features

**What:** UI enhancements that were deferred during initial implementation because they require JS interop wiring.

### 2.1 Right-Click Context Menu
- ✓ Create JS interop for floating context menu positioning (`contextmenu` event) `[implemented]`
- ✓ Create `FileContextMenu.razor` component with actions: Open, Rename, Move, Copy, Share, Download, Delete `[implemented]`
- ✓ Wire context menu to file items in both grid and list views `[implemented]`
- ✓ Dismiss menu on click-outside or Escape `[implemented]`

### 2.2 Drag-and-Drop Move to Folder
- ✓ Create JS interop bridge for `dragstart`, `dragover`, `drop` events (`file-drag-move.js`) `[implemented]`
- ✓ All file items marked draggable automatically `[implemented]`
- ✓ Highlight drop-target folders during drag (`.drag-over-target` CSS) `[implemented]`
- ✓ Call `MoveAsync` on drop via `OnDragMoveNode` JSInvokable `[implemented]`
- ✓ Reload folder on error to show current state `[implemented]`

### 2.3 Upload Queue Management
- ✓ Per-file `AbortController` in `file-upload.js` with `_uploadState` Map `[implemented]`
- ✓ Chunk-level pause/resume/cancel with `AbortError` handling `[implemented]`
- ✓ Wire Pause/Resume/Cancel buttons in `FileUploadComponent.razor` to JS interop `[implemented]`

### 2.4 Paste Image Upload
- ✓ Create JS interop (`file-paste.js`) for `paste` event to capture clipboard images `[implemented]`
- ✓ Auto-generate filename from timestamp (e.g., `paste-2026-03-16-143022.png`) `[implemented]`
- ✓ Trigger upload flow with pasted image data via `addExternalFiles` `[implemented]`

### 2.5 Upload Size Validation
- ✓ Expose `MaxUploadSize` via `GET /api/v1/files/config` endpoint `[implemented]`
- ✓ Validate file size before starting upload (client-side check in `file-upload.js`) `[implemented]`
- ✓ Show clear error message for oversized files with formatted size `[implemented]`

### 2.6 Share Dialog — Existing Shares List
- ✓ `ShareDialog.razor` already loads and displays `ExistingShares` on open `[implemented]`
- ✓ Displays recipient name, share type, expiry status `[implemented]`
- ✓ Inline permission editing (Read/ReadWrite/Full) with `UpdateSharePermissionAsync` `[implemented]`
- ✓ Remove action per share via `RemoveShareAsync` `[implemented]`
- ✓ Refreshes share list after add/remove `[implemented]`

---

## Sprint 3: Deferred Backend Features

**What:** Backend enhancements that were deliberately deferred.

### 3.1 Range Requests for Partial Downloads
- ✓ Enable `enableRangeProcessing: true` on download endpoints (both core server and module host) `[implemented]`
- ✓ `ConcatenatedStream` already supports seeking (canSeek + proper seek implementation) `[implemented]`
- ☐ Test with large video files seeking in browser player `[verification-only]`
- ☐ Test with download resumption (e.g., `curl --range`) `[verification-only]`

### 3.2 Configurable Version Retention Limits
- ✓ `VersionRetentionOptions` already exists: `MaxVersionCount` (50), `RetentionDays` (0/disabled), `CleanupInterval` (24h) `[implemented]`
- ✓ `VersionCleanupService` background job enforces limits — prunes oldest unlabeled versions, labeled versions preserved `[implemented]`
- ✓ Admin settings wired via `FilesAdminSettings` component `[implemented]`

### 3.3 Share Notifications (Notification Integration)
- ✓ `FileSharedNotificationHandler` sends push notification to user recipients on share creation `[implemented]`
- ☐ Notify share creator on first access of public link (deferred — not critical for launch) `[deferred]`
- ☐ Send notification when share is about to expire (deferred — not critical for launch) `[deferred]`

### 3.4 Quota Warning Notifications
- ✓ `QuotaNotificationHandler` wired to `QuotaWarningEvent` — sends push notification at 80% usage `[implemented]`
- ✓ `QuotaNotificationHandler` wired to `QuotaCriticalEvent` — sends push notification at 95% usage `[implemented]`
- ✓ `NotificationEventSubscriber` hosted service registers handlers on startup `[implemented]`

---

## Sprint 4: Desktop Sync Client Hardening

**What:** Remaining sync client improvements and end-to-end verification.

### 4.1 FileSystemWatcher Debounce
- ✓ Semaphore + trailing pass coalescing already implemented in `SyncEngine.SyncAsync()` `[implemented]`
- ✓ N rapid FSW events → at most 2 sync passes (main + 1 trailing) `[implemented]`
- ☐ Test: rapid-save a file 10 times → verify ≤2 sync cycles (E2E verification) `[verification-only]`

### 4.2 End-to-End Sync Verification
- ☐ Install SyncService on Windows as a Windows Service `[verification-only]`
- ☐ Install SyncService on Linux as a systemd unit `[verification-only]`
- ☐ Add account via SyncTray OAuth2 flow `[verification-only]`
- ☐ Create a file on server → verify it appears in local sync folder `[verification-only]`
- ☐ Create a file in local sync folder → verify it appears on server `[verification-only]`
- ☐ Modify a file on both sides simultaneously → verify conflict copy is created `[verification-only]`
- ☐ Disconnect network → queue changes → reconnect → verify sync completes `[verification-only]`
- ☐ Upload a 100MB+ file → verify chunked transfer works `[verification-only]`
- ☐ Verify SyncTray displays correct status (idle, syncing, error, offline) `[verification-only]`
- ☐ Verify SyncTray selective sync (exclude a folder, verify it doesn't sync) `[verification-only]`
- ☐ Verify multi-account support (add two server accounts, both sync independently) `[verification-only]`

---

## Sprint 5: Module Integration Verification

**What:** Verify the Files module integrates correctly with the core platform.

### 5.1 Module System
- ✓ Health checks verified: `/health` (200), `/health/ready` (200), `/health/live` (200), Collabora Online health check included `[implemented]`
- ☐ Verify gRPC communication works between core and Files host process `[verification-only]`
- ☐ Verify module can start and stop cleanly `[verification-only]`

### 5.2 Observability
- ✓ Structured JSON logs verified with SourceContext, RequestId, MachineName, ProcessId, ThreadId `[implemented]`
- ☐ Verify Files module errors are handled gracefully (trigger a 500 and check error boundary) `[verification-only]`
- ☐ Verify OpenTelemetry traces include Files operations (check Jaeger/OTLP output if configured) `[verification-only]`

### 5.3 Documentation & API
- ✓ OpenAPI documentation generated for Files API endpoints (Scalar UI at `/scalar/v1`) `[implemented]`
- ☐ Verify internationalization works for Files UI strings (switch locale, check UI labels) `[verification-only]`

---

## Sprint 6: Security Verification

**What:** Security audit and penetration testing of the Files module.

### 6.1 Authentication & Authorization
- ✓ All `/api/v1/files/*` endpoints return 401 without auth token (15 endpoints verified) `[implemented]`
- ✓ Public link endpoint returns 404 for invalid tokens without requiring auth `[implemented]`
- ✓ Public link passwords stored hashed — `LinkPasswordHash` column (character varying) in `FileShares` table `[implemented]`
- ✓ WOPI endpoints reject fake/expired tokens (404) `[implemented]`
- ✓ Security headers verified: CSP, X-Frame-Options: DENY, X-Content-Type-Options: nosniff, HSTS, Referrer-Policy, Permissions-Policy `[implemented]`

### 6.2 Input Validation
- ☐ Attempt path traversal: create file named `../../etc/passwd` → should be rejected `[verification-only]`
- ☐ Attempt path traversal: rename file to `../../../tmp/evil` → should be rejected `[verification-only]`
- ☐ Upload a file exceeding quota → should return clear error, not crash `[verification-only]`

### 6.3 Rate Limiting
- ☐ Verify rate limiting applies to upload endpoints (if configured) `[missing]`
- ☐ Verify rate limiting returns 429 with `Retry-After` header `[verification-only]`

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
6. **Sprint 2** (UI) — All deferred UI features now `[implemented]`: context menu, drag-drop move, upload queue management, paste upload, size validation.

---

## What Can Be Safely Deferred Past Phase 1 Launch?

These items are nice-to-have and won't block a usable Phase 1 release:

| Item | Reason to Defer |
|---|---|
| Right-click context menu | `[implemented]` — context-menu.js + FileContextMenu.razor |
| Drag-and-drop move to folder | Move available via selection mode + folder picker (`[deferred]`) |
| Paste image upload | `[implemented]` — file-paste.js with timestamp filenames |
| Upload queue management | `[implemented]` — AbortController per-file, chunk-level pause/resume/cancel |
| Upload size validation | `[implemented]` — client-side check via /api/v1/files/config |
| Share expiration notifications | Not critical for launch (`[deferred]`) |
| Share creator first-access notification | Not critical for launch (`[deferred]`) |
| Admin retention per organization | Single-instance config sufficient for launch |
| MariaDB migration | PostgreSQL and SQL Server are the primary targets (`[blocked]`) |

**If deferring these, Phase 1 can close with ~4 sprints instead of 6.**
