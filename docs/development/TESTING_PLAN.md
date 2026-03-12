# Client Testing Plan — Windows Machine

**Created:** 2026-03-12
**Machine:** `Windows11-TestDNC`
**Server:** `https://mint22:15443/`
**Status:** Not Started

---

## Prerequisites

- ☐ Server running at `https://mint22:15443/`
- ☐ Test account exists (or register one)
- ☐ Sync folder configured at `C:\Users\benk\Documents\synctray`
- ☐ Full test suite passing (baseline)

---

## Testing Priority Order

1. **Run all unit tests** — validates nothing is broken at code level
2. **Web UI** — fastest to test, just needs a browser
3. **Sync App on Windows** — build and run locally, full integration test
4. **Android** — depends on device/emulator availability
5. **Sync App on Linux** — separate VM (deferred to next phase)

---

## Phase 1: Unit Test Baseline

**Status:** ✓ COMPLETE (2026-03-12)

| Step | Command | Status |
|------|---------|--------|
| Run full test suite | `dotnet test` | ✓ |
| Verify 2,095+ pass / 0 fail | Check output | ✓ |
| Note any new failures | None | ✓ |

**Baseline Results:**
- Passed: 2,095
- Failed: 0
- Skipped: 13 (environment-gated)
- Notes: All 12 test projects passed. Breakdown below.

| Test Project | Passed | Skipped | Duration |
|-------------|--------|---------|----------|
| DotNetCloud.Core.Tests | 138 | 0 | 1s |
| DotNetCloud.Core.Data.Tests | 176 | 0 | 2s |
| DotNetCloud.Modules.Example.Tests | 51 | 0 | <1s |
| DotNetCloud.CLI.Tests | 118 | 0 | <1s |
| DotNetCloud.Client.SyncService.Tests | 27 | 0 | <1s |
| DotNetCloud.Client.Core.Tests | 148 | 0 | 20s |
| DotNetCloud.Core.Auth.Tests | 85 | 0 | 3s |
| DotNetCloud.Client.SyncTray.Tests | 71 | 0 | 23s |
| DotNetCloud.Modules.Files.Tests | 570 | 0 | 7s |
| DotNetCloud.Modules.Chat.Tests | 263 | 0 | 10s |
| DotNetCloud.Core.Server.Tests | 328 | 1 | 1s |
| DotNetCloud.Integration.Tests | 120 | 12 | 65s |
| **TOTAL** | **2,095** | **13** | **~2m** |

---

## Phase 2: Web UI Testing

**Status:** BLOCKED — server DB error (see Issues Log #1)
**URL:** `https://mint22:15443/`

### 2.1 Authentication

| Test | Status | Notes |
|------|--------|-------|
| Register new test account | ☐ | |
| Log in with existing account | ☐ | |
| Log out | ☐ | |
| Log back in | ☐ | |
| Token refresh (leave tab open, return later) | ☐ | |

### 2.2 Files Module

| Test | Status | Notes |
|------|--------|-------|
| Upload a small file (button) | ☐ | |
| Upload via drag-and-drop | ☐ | |
| Create a new folder | ☐ | |
| Navigate into folder | ☐ | |
| Upload file into subfolder | ☐ | |
| Rename a file | ☐ | |
| Move a file between folders | ☐ | |
| Copy a file | ☐ | |
| Download a file | ☐ | |
| Delete a file (trash) | ☐ | |
| Toggle grid/list view | ☐ | |
| Sort by name | ☐ | |
| Sort by size | ☐ | |
| Sort by date | ☐ | |
| Sort by type | ☐ | |
| Search for a file by name | ☐ | |

### 2.3 Chat Module

| Test | Status | Notes |
|------|--------|-------|
| Create a new channel | ☐ | |
| Send a message | ☐ | |
| Reply in a thread | ☐ | |
| Add an emoji reaction | ☐ | |
| @mention another user (needs 2nd account) | ☐ | |
| Direct message (needs 2nd account) | ☐ | |

### 2.4 General UI

| Test | Status | Notes |
|------|--------|-------|
| Navigate Files → Chat → Home | ☐ | |
| Responsive layout (resize window) | ☐ | |
| No browser console errors (F12) | ☐ | |

---

## Phase 3: Sync App Testing (Windows)

**Status:** ☐ Not Started

### 3.1 Build & Launch

| Step | Command | Status |
|------|---------|--------|
| Build sync service | `dotnet build src\Clients\DotNetCloud.Client.SyncService\` | ☐ |
| Build tray app | `dotnet build src\Clients\DotNetCloud.Client.SyncTray\` | ☐ |
| Run sync service (foreground) | `dotnet run --project src\Clients\DotNetCloud.Client.SyncService\` | ☐ |
| Run tray app | `dotnet run --project src\Clients\DotNetCloud.Client.SyncTray\` | ☐ |

### 3.2 Account Setup

| Test | Status | Notes |
|------|--------|-------|
| Add account via tray UI | ☐ | Server: `https://mint22:15443/` |
| Complete OAuth login in browser | ☐ | |
| Verify sync folder path | ☐ | Expected: `C:\Users\benk\Documents\synctray` |

### 3.3 Sync — Server → Client (Download)

| Test | Status | Notes |
|------|--------|-------|
| Upload file via Web UI | ☐ | |
| File appears in local sync folder | ☐ | Wait up to 5 min |
| Upload nested folder via Web UI | ☐ | |
| Nested structure syncs down correctly | ☐ | |

### 3.4 Sync — Client → Server (Upload)

| Test | Status | Notes |
|------|--------|-------|
| Drop new file into sync folder | ☐ | |
| File appears in Web UI | ☐ | Wait up to 5 min |
| Create subfolder locally + add file | ☐ | |
| Subfolder + file appear in Web UI | ☐ | |

### 3.5 Conflict Resolution

| Test | Status | Notes |
|------|--------|-------|
| Edit file on both Web UI and locally | ☐ | Before sync fires |
| Observe conflict resolution behavior | ☐ | Strategy: newer-wins (default) |
| Check tray UI for conflict notification | ☐ | |

### 3.6 Selective Sync

| Test | Status | Notes |
|------|--------|-------|
| Open folder browser in tray settings | ☐ | |
| Deselect a folder → removed locally | ☐ | |
| Re-select folder → re-downloads | ☐ | |

### 3.7 Tray UI

| Test | Status | Notes |
|------|--------|-------|
| Tray icon visible in system tray | ☐ | |
| Right-click menu works | ☐ | |
| Sync status display (idle/syncing/error) | ☐ | |
| Settings → bandwidth limits | ☐ | |
| Active transfers view during sync | ☐ | |

### 3.8 Edge Cases

| Test | Status | Notes |
|------|--------|-------|
| Large file upload (100MB+) | ☐ | Chunked transfer |
| Special chars in filename | ☐ | `file (1).txt`, `résumé.pdf` |
| Rename file locally → propagates to server | ☐ | |
| Delete file locally → goes to server trash | ☐ | |

### 3.9 Logs

| Check | Status | Notes |
|-------|--------|-------|
| Sync service logs — no errors | ☐ | |
| Tray app logs — no errors | ☐ | |

---

## Phase 4: Android App Testing

**Status:** ☐ Not Started

### 4.0 Prerequisites

| Requirement | Status | Notes |
|-------------|--------|-------|
| Android SDK installed | ☐ | |
| Emulator OR physical device available | ☐ | |
| USB debugging enabled (physical device) | ☐ | |
| `google-services.json` present (Google Play build) | ☐ | Only for FCM testing |

### 4.1 Build & Deploy

| Step | Command | Status |
|------|---------|--------|
| Build debug APK | `dotnet build src\Clients\DotNetCloud.Client.Android\ -f net10.0-android` | ☐ |
| Install on device/emulator | `dotnet build -f net10.0-android -t:Install` | ☐ |

**Alternative:** Run Android unit tests only (no device needed):
```
dotnet test tests\DotNetCloud.Client.Android.Tests\
```

### 4.2 Authentication

| Test | Status | Notes |
|------|--------|-------|
| App opens to login page | ☐ | |
| Tap login → system browser opens | ☐ | |
| Complete OAuth flow → returns to app | ☐ | |
| Session persists after app restart | ☐ | |

### 4.3 Chat

| Test | Status | Notes |
|------|--------|-------|
| View channel list | ☐ | |
| Open channel → see messages | ☐ | |
| Send message → appears in Web UI | ☐ | |
| Receive message from Web UI → real-time | ☐ | |
| Thread reply | ☐ | |
| Emoji reaction | ☐ | |
| Offline → send → online → sends | ☐ | |

### 4.4 Push Notifications (Google Play build)

| Test | Status | Notes |
|------|--------|-------|
| Background app → receive push | ☐ | Requires FCM setup |
| Tap notification → opens channel | ☐ | |
| Badge count shows on app icon | ☐ | |

### 4.5 Photo Auto-Upload

| Test | Status | Notes |
|------|--------|-------|
| Enable in settings | ☐ | |
| Take photo → uploads to server | ☐ | |

### 4.6 Settings & Account

| Test | Status | Notes |
|------|--------|-------|
| Change server URL | ☐ | |
| Log out → log back in | ☐ | |
| Token survives app restart | ☐ | |

---

## Phase 5: Sync App on Linux (Deferred)

**Status:** ☐ Deferred — separate Linux VM
**Notes:** After Windows testing is complete, spin up a Linux VM and repeat Phase 3 tests.

---

## Issues Log

Track any bugs or server-side issues discovered during testing.

| # | Phase | Test | Issue Description | Root Cause | Server Handoff Needed? | Status |
|---|-------|------|-------------------|------------|----------------------|--------|
| 1 | 2 | Page load | `42703: column f.PosixMode does not exist POSITION: 271` — Web UI shows "Something went wrong" on load. Missing column in PostgreSQL `files` table. Likely a migration not applied on `mint22`. | Missing DB migration | YES | Open |

---

## Server Handoff Protocol

If testing reveals a server-side bug:
1. Document the exact error in the Issues Log above
2. Include: endpoint, request details, response/error, steps to reproduce
3. Update the Active Handoff in `CLIENT_SERVER_MEDIATION_HANDOFF.md`
4. Commit and push
5. Relay to server agent
