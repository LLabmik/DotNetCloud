# Remaining Work Plan — Phases 0–3

> **Scope:** All pending items across Phases 0–3, excluding MariaDB/Pomelo (blocked on .NET 10 support from Pomelo EF Core provider).
>
> **Created:** 2026-03-23
>
> **Rule:** Nothing in this plan may be deferred without explicit user approval per item.

---

## Summary

| Workstream | Items | Type | Status |
|---|---|---|---|
| WS-1: Phase 3 Deferred Verification | 7 exit criteria | Verify code exists, update tracking | ✓ Complete |
| WS-2: Phase 0 Pending Items | 4 items | Code + docs | ✓ Complete |
| WS-3: Phase 1 Code Gaps | 8 items | Code work | ✓ Complete |
| WS-4: Phase 1 Live Verification | 48+ items | Manual testing against running server | Requires deployment |
| **Total** | **~67 items** | | **WS-1–3 done; WS-4 pending** |

---

## WS-1: Phase 3 Deferred Items — ✓ COMPLETE

**All D1–D7 verified in code. Tracking docs updated.**

---

## WS-2: Phase 0 Pending Items — ✓ COMPLETE

| # | Item | Status | Notes |
|---|------|--------|-------|
| 2.1 | Create status badge documentation | ✓ Done | `docs/development/STATUS_BADGES.md` created |
| 2.2 | Docker containers run and pass health checks | ✓ Done | Dockerfile updated with all modules, health check fixed (wget), `DotNetCloud.CI.slnf` updated |
| 2.3 | Backup/restore settings (admin UI) | ✓ Done | `BackupSettings.razor` admin page created |
| 2.4 | Kubernetes Helm chart for module deployment | ✓ Done | Full chart scaffold at `deploy/helm/dotnetcloud/` |

---

## WS-3: Phase 1 Code Gaps — ✓ COMPLETE

### 3A: Share & Quota Notifications — ✓ Already Implemented

All 4 notification handlers existed in code (`FileSharedNotificationHandler`, `PublicLinkAccessedNotificationHandler`, `QuotaNotificationHandler`, `ShareExpiringNotificationHandler`). Tracking updated.

### 3B: FCM / Push Notifications — ✓ Complete

| # | Item | Status | Notes |
|---|------|--------|-------|
| 3.5 | Configure Firebase Admin SDK credentials | ✓ Already existed | `FcmPushOptions` with credential loading |
| 3.6 | Implement batch sending for efficiency | ✓ Done | `FcmHttpTransport` with batch API, semaphore concurrency |
| 3.7 | Add admin UI for FCM credential management | ✓ Done | `PushNotificationSettings.razor` admin page |

### 3C: Other Code Items — ✓ Complete

| # | Item | Status | Notes |
|---|------|--------|-------|
| 3.8 | Admin can configure trash retention per organization | ✓ Done | `TrashRetentionOptions.OrganizationOverrides` + `IUserOrganizationResolver` + `TrashCleanupService` updated |

---

## WS-4: Phase 1 Live Verification

These all require a running server instance. They are `[verification-only]` — the code exists, but no one has manually tested the flows against a live deployment.

### Sprint 1.1–1.2: File & Folder Operations (11 items)
- ☐ Upload a file via web UI → confirm it appears in file browser
- ☐ Download the uploaded file → verify content matches
- ☐ Rename a file → confirm name updates
- ☐ Move a file to subfolder → confirm new location
- ☐ Copy a file → confirm both copies exist
- ☐ Delete a file → confirm it moves to trash
- ☐ Create a new folder via web UI
- ☐ Navigate into folder and back out
- ☐ Rename a folder
- ☐ Move a folder into another folder
- ☐ Delete a folder → confirm children also trash

### Sprint 1.3: Chunked Upload & Dedup (3 items)
- ☐ Upload a file >4MB → confirm chunked upload completes
- ☐ Upload same file again → confirm dedup (no new chunks)
- ☐ Interrupt upload mid-stream → re-open → verify resume from last chunk

### Sprint 1.4: Versioning (4 items)
- ☐ Upload new version of existing file
- ☐ Open version history panel → confirm both versions listed
- ☐ Download a previous version
- ☐ Restore previous version → verify content reverts

### Sprint 1.5: Sharing (4 items)
- ☐ Share a file with another user (Read) → confirm they can view
- ☐ Create public link → access in incognito browser
- ☐ Set password on public link → confirm required to access
- ☐ Set download limit on public link → confirm blocks after limit

### Sprint 1.6: Quotas (2 items)
- ☐ Set low quota for test user via admin dashboard
- ☐ Upload until quota exceeded → confirm rejection error

### Sprint 1.7: Collabora / WOPI (3 items)
- ☐ Open .docx in Collabora editor from file browser
- ☐ Edit document, save → confirm new version created
- ☐ Verify WOPI CheckFileInfo returns correct metadata (curl test)

### Sprint 1.8: File Preview (6 items)
- ☐ Preview image (JPEG/PNG)
- ☐ Preview video
- ☐ Preview PDF
- ☐ Preview text/code file
- ☐ Preview Markdown
- ☐ Confirm unsupported formats show "Download File" fallback

### Sprint 1.9: Tags & Comments (6 items)
- ☐ Add a tag to a file via UI
- ☐ Filter files by tag in sidebar
- ☐ Remove a tag
- ☐ Add a comment to a file
- ☐ Reply to a comment (threaded)
- ☐ Edit and delete a comment

### Sprint 1.10: Sync Endpoints (3 items)
- ☐ `GET /api/v1/files/sync/changes?since=<timestamp>` → verify correct data
- ☐ `POST /api/v1/files/sync/reconcile` with local state → verify diff
- ☐ `GET /api/v1/files/sync/tree` → verify complete folder tree with hashes

### Sprint 1.11: SQL Server Integration (1 item)
- ☐ Run integration tests against SQL Server — all pass

### Sprint 3: Range Requests (2 items)
- ☐ Test with large video files seeking in browser player
- ☐ Test with download resumption (`curl --range`)

### Sprint 4.1: FSW Debounce (1 item)
- ☐ Rapid-save file 10 times → verify ≤2 sync cycles

### Sprint 4.2: End-to-End Sync (11 items)
- ☐ Install SyncService on Windows as a Windows Service
- ☐ Install SyncService on Linux as a systemd unit
- ☐ Add account via SyncTray OAuth2 flow
- ☐ Create file on server → appears in local sync folder
- ☐ Create file in local sync folder → appears on server
- ☐ Modify file on both sides → conflict copy created
- ☐ Disconnect network → queue changes → reconnect → sync completes
- ☐ Upload 100MB+ file → chunked transfer works
- ☐ SyncTray displays correct status (idle, syncing, error, offline)
- ☐ SyncTray selective sync (exclude folder, verify not synced)
- ☐ Multi-account support (two servers, both sync independently)

### Sprint 5: Module & Observability (3 items)
- ☐ Verify gRPC between core and Files host process
- ☐ Verify module start/stop cleanly
- ☐ Verify i18n works for Files UI strings (switch locale)

### Sprint 5: OpenTelemetry (1 item)
- ☐ Verify traces include Files operations (check Jaeger/OTLP output)

### Sprint 6: Security (5 items)
- ☐ Path traversal: create file named `../../etc/passwd` → rejected
- ☐ Path traversal: rename file to `../../../tmp/evil` → rejected
- ☐ Upload file exceeding quota → clear error, no crash
- ☐ Verify rate limiting applies to upload endpoints (if configured)
- ☐ Verify rate limiting returns 429 with `Retry-After` header

---

## Recommended Execution Order

```
WS-1 (Verify Phase 3 deferred)     — ✓ DONE
  ↓
WS-2 (Phase 0 gaps)                — ✓ DONE
WS-3A (Share/quota notifications)  — ✓ DONE (already implemented)
WS-3B (FCM push)                   — ✓ DONE
WS-3C (Trash retention admin)      — ✓ DONE
  ↓
WS-4 (Live verification)           — PENDING: requires deployed server
```

---

## Items Explicitly NOT In Scope

| Item | Reason |
|---|---|
| MariaDB/Pomelo migration | Blocked on third-party .NET 10 support — revisit when Pomelo ships |
| Phase 4–9 (future phases) | Not started, not part of Phases 0–3 |
| Desktop/mobile client features | Separate workstream, not Phase 0–3 gaps |
