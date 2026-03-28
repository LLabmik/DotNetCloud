# WS-4 Live Verification Execution Plan

**Environment:** Server on mint22 · Web client on monolith · User: testdude@llabmik.net  
**Tracking docs to update on completion:** `docs/REMAINING_WORK_PLAN.md`, `docs/MASTER_PROJECT_PLAN.md`, `docs/IMPLEMENTATION_CHECKLIST.md`

---

## Status (2026-03-25)

**Completed pre-work:**
- 36 automated integration tests added (commit `8f5c37d`) — CI only, not live-server tests
- Broken ROPC (password grant) code removed; server is clean
- mint22: `dotnetcloud.service` active, `coolwsd` (Collabora) active (deployed Mar 25 03:28)

**Live verification: 23 of 66 items passed ✅**

**Known blockers:**
- ~~**Comments UI** (TC-1.37–1.39, 3 items) — UNBLOCKED: CommentsPanel implemented~~
- **SQL Server** (TC-1.43, 1 item) — no SQL Server instance available
- **Sync clients** (TC-1.46–1.57, 12 items) — Windows11-TestDNC and mint-dnc-client not set up

---

## Phase A — Browser Tests (33 items, User-driven)

Log in as testdude@llabmik.net at `https://mint22:5443`. Work through the sprints below.

After completing Phase A, open DevTools (F12) > Network and grab:
- `Authorization: Bearer <token>` from any authenticated API call → `DNC_BEARER_TOKEN`
- A file GUID from any file detail call → `DNC_FILE_ID`
- `access_token` query param from the WOPI request URL when Collabora is open → `DNC_WOPI_TOKEN`

### Sprint 1.1–1.2: File and Folder Operations (11)

#### TC-1.1 Upload file via web UI — ✅ Pass
- Setup: Sign in as testdude@llabmik.net in Files UI.
- Steps:
	1. Click Upload.
	2. Select a small test file (for example 50 KB text file).
	3. Wait for upload completion.
- Pass criteria: File appears in current folder with expected name and size.
- **Result:** Pass (2026-03-26)

#### TC-1.2 Download uploaded file — ✅ Pass
- Setup: Existing file from TC-1.1.
- Steps:
	1. Click file actions.
	2. Choose Download.
	3. Open downloaded file locally.
- Pass criteria: Download succeeds and content matches original.
- **Result:** Pass (2026-03-26)

#### TC-1.3 Rename file — ✅ Pass
- Setup: Existing test file.
- Steps:
	1. Open file context menu.
	2. Select Rename and enter new name.
	3. Confirm.
- Pass criteria: List updates to new name and file is still accessible.
- **Result:** Pass (2026-03-26)

#### TC-1.4 Move file to subfolder — ✅ Pass
- Setup: File plus target subfolder.
- Steps:
	1. Create subfolder if needed.
	2. Move file using drag/drop or Move action.
	3. Open target folder.
- Pass criteria: File no longer in source folder and appears in target folder.
- **Result:** Pass (2026-03-26)

#### TC-1.5 Copy file — ✅ Pass
- Setup: Existing file.
- Steps:
	1. Use Copy action.
	2. Paste in same folder or a target folder.
	3. Refresh file list.
- Pass criteria: Original and copied file both exist with distinct names.
- **Result:** Pass (2026-03-26)
- **Fix applied:** Root-level copy/move required null-target support across FileService, UI picker guards, and controllers.

#### TC-1.6 Delete file to trash — ✅ Pass
- Setup: Existing file.
- Steps:
	1. Use Delete action.
	2. Open Trash view.
- Pass criteria: File removed from active view and present in Trash.
- **Result:** Pass (2026-03-26)
- **Fix applied:** TrashService query filter and OriginalParentId assignment on descendants fixed.

#### TC-1.7 Create new folder — ✅ Pass
- Setup: In Files root or selected parent folder.
- Steps:
	1. Click New Folder.
	2. Enter folder name.
	3. Confirm.
- Pass criteria: Folder appears immediately in file list.
- **Result:** Pass (2026-03-26)

#### TC-1.8 Navigate into folder and back — ✅ Pass
- Setup: Existing folder.
- Steps:
	1. Open folder.
	2. Verify breadcrumb/path changes.
	3. Navigate back to previous level.
- Pass criteria: Navigation works both directions and listing is correct.
- **Result:** Pass (2026-03-26)

#### TC-1.9 Rename folder — ✅ Pass
- Setup: Existing folder.
- Steps:
	1. Open folder context menu.
	2. Rename folder.
	3. Confirm.
- Pass criteria: Folder name updates and folder opens normally.
- **Result:** Pass (2026-03-26)

#### TC-1.10 Move folder into another folder — ✅ Pass
- Setup: Source folder and destination folder.
- Steps:
	1. Move source folder to destination.
	2. Open destination folder.
- Pass criteria: Source folder appears under destination and path resolves correctly.
- **Result:** Pass (2026-03-26)

#### TC-1.11 Delete folder and verify children trashed — ✅ Pass
- Setup: Folder containing at least one file.
- Steps:
	1. Delete the folder.
	2. Open Trash.
- Pass criteria: Deleted folder and child items are present in Trash.
- **Result:** Pass (2026-03-26)

### Sprint 1.3: Chunked Upload and Dedup (3)

#### TC-1.12 Upload file larger than 4 MB — ✅ Pass
- Setup: Prepare file larger than 4 MB.
- Steps:
	1. Upload large file.
	2. Monitor progress until complete.
- Pass criteria: Upload completes successfully without timeout.
- **Result:** Pass (2026-03-26)

#### TC-1.13 Upload same file again for dedup — ✅ Pass
- Setup: File from TC-1.12 already uploaded.
- Steps:
	1. Upload identical file again.
	2. Compare completion time/log indicators if available.
- Pass criteria: Second upload completes and backend indicates dedup behavior (no duplicate chunk storage behavior).
- **Result:** Pass (2026-03-26)

#### TC-1.14 Interrupt and resume upload — ✅ Pass
- Setup: Large file upload in progress.
- Steps:
	1. Start upload.
	2. Interrupt network/browser.
	3. Reopen session and retry upload.
- Pass criteria: Upload resumes from last chunk boundary and completes.
- **Result:** Pass (2026-03-26)
- **Fix applied:** Upload dialog now stays in progress view while files are paused; resume button correctly triggers chunk resumption.

### Sprint 1.4: Versioning (4)

#### TC-1.15 Upload new version of existing file
- Setup: Existing file in folder.
- Steps:
	1. Upload replacement content with same logical file.
	2. Confirm version update flow is triggered.
- Pass criteria: File now has version history count greater than 1.
- **Result:** Pass (2026-03-26)

#### TC-1.16 Open version history panel
- Setup: File with at least two versions.
- Steps:
	1. Open file details/history.
	2. Inspect versions list.
- Pass criteria: Both versions are listed with expected metadata.
- **Result:** Pass (2026-03-26)

#### TC-1.17 Download previous version
- Setup: File with multiple versions.
- Steps:
	1. Choose older version in history.
	2. Download selected version.
	3. Open file locally.
- Pass criteria: Downloaded content matches the older revision.
- **Result:** Pass (2026-03-26)

#### TC-1.18 Restore previous version
- Setup: File with multiple versions.
- Steps:
	1. Select older version.
	2. Click Restore.
	3. Reopen current file.
- Pass criteria: Current file content reverts to selected older version.
- **Result:** Pass (2026-03-26)

### Sprint 1.5: Sharing (4)

#### TC-1.19 Share file with another user (read) — ✅ Pass
- Setup: Second account available for verification.
- Steps:
	1. Share file with read permission.
	2. Sign into second account and open share.
- Pass criteria: Second user can view/download but not modify.
- **Result:** Pass (2026-03-26)

#### TC-1.20 Create public link and open incognito — ✅ Pass
- Setup: Existing shareable file.
- Steps:
	1. Generate public link.
	2. Open link in incognito/private browser.
- Pass criteria: Anonymous access works according to default share permissions.
- **Result:** Pass (2026-03-27)

#### TC-1.21 Public link password protection — ✅ Pass
- Setup: Existing public link.
- Steps:
	1. Set password on link.
	2. Reopen link without password.
	3. Enter password and continue.
- Pass criteria: Access blocked until correct password is provided.
- **Result:** Pass (2026-03-27)

#### TC-1.22 Public link download limit — ✅ Pass
- Setup: Public link with configurable limit.
- Steps:
	1. Set small download limit (for example 1).
	2. Download once.
	3. Attempt second download.
- Pass criteria: Further downloads are blocked after limit reached.
- **Result:** Pass (2026-03-27)

### Sprint 1.6: Quotas (2)

#### TC-1.23 Set low quota for test user ✅ PASS
- Setup: Admin access available.
- Steps:
	1. Open admin quota settings.
	2. Assign low quota to testdude@llabmik.net.
	3. Save settings.
- Pass criteria: New quota value is persisted and visible.
- Result: PASS — Built admin quota UI (UserEdit quota field, UserDetail quota display, UserList quota bars), fixed QuotaController security (added RequireAdmin policy), fixed Blazor dispatcher threading bug (ToastService subscribers using InvokeAsync), changed ToastService from singleton to scoped, added Files sidebar storage summary with quota bar. Quota persists and displays correctly in both admin and user views.

#### TC-1.24 Upload until quota exceeded ✅ PASS
- Setup: Low quota configured.
- Steps:
	1. Upload files until threshold exceeded.
	2. Observe behavior at boundary.
- Pass criteria: Upload is rejected with clear quota error and no crash.
- Result: PASS — Added client-side quota pre-check (GET /api/v1/files/quota before chunking), friendly error message parsing from initiate response, context menu viewport repositioning fix, scrollable context menu. Upload is rejected instantly with clear message showing file size vs remaining quota.

### Sprint 1.7: Collabora (2)

#### TC-1.25 Open ODT in Collabora — ✅ Pass
- Setup: Collabora integration configured.
- Steps:
	1. Upload ODT file.
	2. Open in editor.
- Pass criteria: Document opens in Collabora editor successfully.
- **Result:** Pass (2026-03-27)

#### TC-1.26 Edit and save ODT — ✅ Pass
- Setup:ODT open in editor.
- Steps:
	1. Make small text edit.
	2. Save/auto-save.
	3. Return to file metadata/history.
- Pass criteria: New version is created after save.
- **Result:** Pass (2026-03-27)

### Sprint 1.8: File Preview (6)

#### TC-1.28 Preview image (JPEG/PNG) — ✅ Pass
- Setup: JPEG and PNG sample files.
- Steps:
	1. Open each image in preview.
- Pass criteria: Inline image preview renders correctly.
- **Result:** Pass (2026-03-27)

#### TC-1.29 Preview video — ✅ Pass
- Setup: Small MP4 test video.
- Steps:
	1. Open video in preview.
	2. Play and pause.
- Pass criteria: Video preview loads and playback works.
- **Result:** Pass (2026-03-27)

#### TC-1.30 Preview PDF — ✅ Pass
- Setup: Sample PDF.
- Steps:
	1. Open PDF in preview.
	2. Navigate at least one page.
- Pass criteria: PDF renders in preview without forced download.
- **Result:** Pass (2026-03-27) — Required fix: changed frame-ancestors to 'self' and X-Frame-Options to SAMEORIGIN (5b37316)

#### TC-1.31 Preview text/code file — ✅ Pass
- Setup: TXT or source code file.
- Steps:
	1. Open in preview.
	2. Scroll content.
- Pass criteria: Text content is readable in preview viewer.
- **Result:** Pass (2026-03-27) — Added highlight.js syntax highlighting for code files

#### TC-1.32 Preview Markdown — ✅ Pass
- Setup: Markdown file with headings and list.
- Steps:
	1. Open markdown file in preview.
- Pass criteria: Markdown displays correctly (rendered view or readable source per product design).
- **Result:** Pass (2026-03-27) — Shared markdown viewer/editor extracted from Notes and integrated into Files preview/edit flow; preview renders markdown correctly.

#### TC-1.33 Unsupported format fallback — ✅ Pass
- Setup: Unknown/unsupported file type.
- Steps:
	1. Open file in preview.
- Pass criteria: Preview offers Download File fallback.
- **Result:** Pass (2026-03-27) — `test.zip` showed unsupported preview state with **Download File** action.

### Sprint 1.9: Tags and Comments (6)

#### TC-1.34 Add tag to file — ✅ Pass
- Setup: Existing file.
- Steps:
	1. Add a new tag from file details.
- Pass criteria: Tag appears on file and persists after refresh.
- **Result:** Pass (2026-03-28)

#### TC-1.35 Filter files by tag — ✅ Pass
- Setup: At least two tagged files.
- Steps:
	1. Select tag filter.
- Pass criteria: List shows only files with selected tag.
- **Result:** Pass (2026-03-28)

#### TC-1.36 Remove tag — ✅ Pass
- Setup: Tagged file.
- Steps:
	1. Remove one tag.
	2. Refresh.
- Pass criteria: Tag is no longer associated.
- **Result:** Pass (2026-03-28)

#### TC-1.37 Add comment to file — ✅ Pass
- Setup: Select any file, open Comments panel (via context menu or preview header).
- Steps:
	1. Type a comment in the compose area.
	2. Click "Post" (or Ctrl+Enter).
- Pass criteria: Comment appears with author and timestamp.
- **Result:** Pass (2026-03-28)

#### TC-1.38 Reply to comment (threaded) — ✅ Pass
- Setup: File with at least one existing comment.
- Steps:
	1. Click "Reply" on an existing comment.
	2. Type reply text and submit.
- Pass criteria: Reply is nested under root comment.
- **Result:** Pass (2026-03-28)

#### TC-1.39 Edit and delete comment — ✅ Pass
- Setup: File with own comment.
- Steps:
	1. Click "Edit" on own comment, change text, save.
	2. Click "Delete" on own comment.
- Pass criteria: Edit persists, then deletion removes or marks comment per design.
- **Result:** Pass (2026-03-28)

---

## Phase B — API & Protocol Tests (6 items, Copilot-runs with your token)

Provide `DNC_BEARER_TOKEN`, `DNC_FILE_ID`, and `DNC_WOPI_TOKEN` captured at the end of Phase A. Set these env vars once and Copilot runs the command-based tests below.

```bash
export DNC_BASE_URL="https://mint22:5443"
export DNC_BEARER_TOKEN="<paste-access-token>"
export DNC_FILE_ID="<target-file-id>"
export DNC_WOPI_TOKEN="<wopi-token>"
export DNC_SINCE="2026-03-25T00:00:00Z"
```

### TC-1.44 Browser video seek on large file (User-driven)
- Setup: Large video uploaded.
- Steps:
	1. Start playback.
	2. Seek to different timestamps.
- Pass criteria: Seeking works without full re-download behavior.

### TC-1.45 Curl range resume (Copilot-capable)
- Setup: Downloadable large file.
- Steps:
	1. Start partial download.
	2. Resume with curl --range.
- Pass criteria: Server returns correct partial content and resume completes.

```bash
curl -sS -D /tmp/range-head-1.txt \
	-H "Authorization: Bearer $DNC_BEARER_TOKEN" \
	-H "Range: bytes=0-1048575" \
	"$DNC_BASE_URL/api/v1/files/$DNC_FILE_ID/download" \
	-o /tmp/part1.bin

curl -sS -D /tmp/range-head-2.txt \
	-H "Authorization: Bearer $DNC_BEARER_TOKEN" \
	-H "Range: bytes=1048576-" \
	"$DNC_BASE_URL/api/v1/files/$DNC_FILE_ID/download" \
	-o /tmp/part2.bin

cat /tmp/part1.bin /tmp/part2.bin > /tmp/reconstructed.bin
```

Pass checks: both responses HTTP 206, Content-Range header present, reconstructed file hash/size matches source.

### Sprint 1.7: WOPI CheckFileInfo (1, Copilot-capable)

#### TC-1.27 Verify WOPI CheckFileInfo metadata
- Setup: Valid WOPI token and file id (captured from DevTools during TC-1.25/1.26).
- Steps:
	1. Call CheckFileInfo endpoint.
	2. Inspect JSON fields.
- Pass criteria: Metadata is complete and values match target file.

```bash
curl -sS -D - \
	"$DNC_BASE_URL/api/v1/wopi/files/$DNC_FILE_ID?access_token=$DNC_WOPI_TOKEN"
```

Pass checks: HTTP 200, JSON includes BaseFileName, Size, UserId, Version.

### Sprint 1.10: Sync Endpoints (3, Copilot-capable)

#### TC-1.40 GET sync changes

```bash
curl -sS -D - \
	-H "Authorization: Bearer $DNC_BEARER_TOKEN" \
	"$DNC_BASE_URL/api/v1/files/sync/changes?since=$DNC_SINCE"
```

Pass checks: HTTP 200, response includes only changes newer than DNC_SINCE.

#### TC-1.41 POST sync reconcile

```bash
cat > /tmp/reconcile-payload.json << 'JSON'
{
	"items": [
		{
			"path": "/Documents/example.txt",
			"hash": "abc123",
			"lastModifiedUtc": "2026-03-25T00:00:00Z",
			"size": 128
		}
	]
}
JSON

curl -sS -D - \
	-H "Authorization: Bearer $DNC_BEARER_TOKEN" \
	-H "Content-Type: application/json" \
	-X POST \
	"$DNC_BASE_URL/api/v1/files/sync/reconcile" \
	--data @/tmp/reconcile-payload.json
```

Pass checks: HTTP 200, response contains expected server diff actions.

#### TC-1.42 GET sync tree

```bash
curl -sS -D - \
	-H "Authorization: Bearer $DNC_BEARER_TOKEN" \
	"$DNC_BASE_URL/api/v1/files/sync/tree"
```

Pass checks: HTTP 200, tree entries include paths and hashes.

---

## Phase C — Sync Client End-to-End (12 items, Deferred)

Requires Windows11-TestDNC and mint-dnc-client to be set up with sync clients. Do in a separate session.

### Sprint 4.1: FSW Debounce (1)

#### TC-1.46 Rapid-save debounce behavior
- Setup: Synced file in local folder.
- Steps:
	1. Save same file rapidly 10 times.
	2. Observe sync cycles/logs.
- Pass criteria: At most 2 sync cycles are triggered.

### Sprint 4.2: End-to-End Sync (11)

#### TC-1.47 Install SyncService on Windows service
- Setup: Windows11-TestDNC access.
- Steps:
	1. Install SyncService as Windows Service.
	2. Start service.
- Pass criteria: Service installed and running.

#### TC-1.48 Install SyncService on Linux systemd
- Setup: mint-dnc-client access.
- Steps:
	1. Install service unit.
	2. Enable and start service.
- Pass criteria: Service active under systemd.

#### TC-1.49 Add account via SyncTray OAuth2
- Setup: SyncTray running.
- Steps:
	1. Add account.
	2. Complete OAuth2 login flow.
- Pass criteria: Account appears connected in tray UI.

#### TC-1.50 Server to local file sync
- Setup: Connected sync client.
- Steps:
	1. Create file in web UI.
	2. Wait for sync.
- Pass criteria: File appears in local sync folder.

#### TC-1.51 Local to server file sync
- Setup: Connected sync client.
- Steps:
	1. Create file in local sync folder.
	2. Wait for sync.
- Pass criteria: File appears in server web UI.

#### TC-1.52 Conflict copy on concurrent edits
- Setup: Same file present both sides.
- Steps:
	1. Edit on server and local before sync settles.
	2. Allow sync.
- Pass criteria: Conflict copy is created and data preserved.

#### TC-1.53 Offline queue and reconnect
- Setup: Connected client.
- Steps:
	1. Disable network.
	2. Make local changes.
	3. Re-enable network.
- Pass criteria: Queued changes sync after reconnect.

#### TC-1.54 Upload 100 MB plus file through sync
- Setup: Large local file ready.
- Steps:
	1. Place file in synced folder.
	2. Wait for upload completion.
- Pass criteria: Large file uploads successfully with chunked transfer behavior.

#### TC-1.55 SyncTray status indicators
- Setup: SyncTray running.
- Steps:
	1. Observe idle state.
	2. Trigger sync for syncing state.
	3. Simulate fault or disconnect for error/offline state.
- Pass criteria: Status indicator reflects idle, syncing, error, and offline correctly.

#### TC-1.56 Selective sync exclusion
- Setup: Folder exclusion feature enabled.
- Steps:
	1. Exclude one folder.
	2. Add file under excluded folder on server.
	3. Sync.
- Pass criteria: Excluded folder content is not synced locally.

#### TC-1.57 Multi-account independent sync
- Setup: Two server accounts configured.
- Steps:
	1. Add both accounts.
	2. Make changes in each scope.
- Pass criteria: Both accounts sync independently with no cross-over.

---

## Phase D — Observability & Security (10 items, Hybrid)

After Phase A/B are complete and bearer token is available.

### Sprint 5: Module and Observability (3)

#### TC-1.58 Verify gRPC between core and Files host (Hybrid — Copilot checks mint22 logs)
- Setup: mint22 server access.
- Steps:
	1. Perform file operations in browser.
	2. Copilot inspects logs/health endpoints for module communication evidence.
- Pass criteria: gRPC calls between core and Files host are visible and successful.

#### TC-1.59 Verify module start and stop (Hybrid)
- Setup: Service control access on mint22.
- Steps:
	1. Restart module process.
	2. Confirm clean startup and graceful stop behavior.
- Pass criteria: Module lifecycle completes without crash or orphaned state.

#### TC-1.60 Verify i18n strings for Files UI (User-driven)
- Setup: Alternate locale available.
- Steps:
	1. Switch locale in browser.
	2. Reload Files UI.
- Pass criteria: Files UI strings are localized and no missing keys appear.

### Sprint 5: OpenTelemetry (1)

#### TC-1.61 Verify Files traces in telemetry backend — **BLOCKED** if no collector configured
- Setup: Jaeger or OTLP collector reachable.
- Steps:
	1. Trigger file operations.
	2. Query telemetry backend.
- Pass criteria: Traces contain Files operation spans with expected metadata.

### Sprint 6: Security (5, Copilot-capable via API)

#### TC-1.62 Path traversal create rejected

```bash
curl -sS -D - \
	-H "Authorization: Bearer $DNC_BEARER_TOKEN" \
	-H "Content-Type: application/json" \
	-X POST \
	"$DNC_BASE_URL/api/v1/files" \
	-d '{"name":"../../etc/passwd","parentId":null}'
```

Pass checks: 400 or 422 response, operation rejected with safe validation error.

#### TC-1.63 Path traversal rename rejected

```bash
curl -sS -D - \
	-H "Authorization: Bearer $DNC_BEARER_TOKEN" \
	-H "Content-Type: application/json" \
	-X PATCH \
	"$DNC_BASE_URL/api/v1/files/$DNC_FILE_ID" \
	-d '{"name":"../../../tmp/evil"}'
```

Pass checks: 400 or 422 response, file remains intact.

#### TC-1.64 Quota exceed does not crash
- Setup: Low quota configuration from TC-1.23/1.24.
- Steps:
	1. Upload file that exceeds quota via API or browser.
- Pass criteria: Clear error shown, no service crash, server still responds.

#### TC-1.65 Rate limiting applied to upload endpoints / TC-1.66 429 includes Retry-After

```bash
for i in $(seq 1 40); do
	curl -sS -o /tmp/rate-$i.out -D /tmp/rate-$i.hdr \
		-H "Authorization: Bearer $DNC_BEARER_TOKEN" \
		-F "file=@/etc/hosts" \
		"$DNC_BASE_URL/api/v1/files/upload" &
done
wait

grep -H "HTTP/" /tmp/rate-*.hdr | tail -n 20
grep -H "Retry-After" /tmp/rate-*.hdr
```

Pass checks: at least one HTTP 429 response; 429 responses include Retry-After header.

---

## Phase E — SQL Server & Closeout (1 item + docs)

TC-1.43 is **Blocked** until a SQL Server instance is available. All other items must be resolved first, then update the three tracking docs.

### TC-1.43 SQL Server Integration Tests — **BLOCKED** (no SQL Server instance)

```bash
export DOTNETCLOUD_TEST_SQLSERVER_CONNECTION_STRING="Server=<sql-host>;Database=<db>;User Id=<user>;Password=<pass>;TrustServerCertificate=true"

dotnet test tests/DotNetCloud.Integration.Tests/ \
	-p:DatabaseProvider=SqlServer
```

Pass checks: test run completes, no failed tests.

**Note:** When `DOTNETCLOUD_TEST_SQLSERVER_CONNECTION_STRING` is set, integration tests use that SQL Server. This enables testing against a network host (e.g. Hyperdrive) without requiring local Windows SQL Server or Docker.

---

## Cosmetic Issues Log

- **Selected row text invisible:** When a file or folder row is selected, white text on white background makes the name unreadable. Needs CSS fix for selected-row text color or background contrast.

---

## Evidence Standard

Each checklist item must include:
1. Status: Pass, Fail, or Blocked
2. One artifact: screenshot, command output summary, or log/trace reference
3. Repro details for failed/blocked items, including suspected layer (UI/API/Sync/Infra)
4. No checkbox marked complete without evidence

## Per-Item Result Template

Use one copy of this template for every WS-4 checklist item.

```markdown
### Item: <sprint and checklist text>
- Status: Pass | Fail | Blocked
- Date: YYYY-MM-DD
- Tester: <name>
- Environment: monolith | mint22 | Windows11-TestDNC | mint-dnc-client
- User: testdude@llabmik.net
- Preconditions:
	- <required setup>
- Steps:
	1. <step 1>
	2. <step 2>
- Expected Result:
	- <expected behavior>
- Actual Result:
	- <what happened>
- Evidence:
	- Screenshot: <path or description>
	- Command Output: <command summary>
	- Log/Trace: <service + key line or trace id>
- Suspected Layer (if Fail/Blocked): UI | API | Sync | Infra
- Issue Link (if created): <issue id or url>
- Notes:
	- <extra context>
```

### Filled Example (Sprint 1.8, Item 1)

```markdown
### Item: Sprint 1.8 - Preview image (JPEG/PNG)
- Status: Pass
- Date: 2026-03-25
- Tester: benk
- Environment: monolith
- User: testdude@llabmik.net
- Preconditions:
	- Mint22 deployment is reachable from monolith
	- User is logged into web client
	- Test image file available (`preview-test-image.jpg`)
- Steps:
	1. Open Files in web client
	2. Upload `preview-test-image.jpg`
	3. Click file row to open preview panel/viewer
- Expected Result:
	- Image preview renders inline without download requirement
	- Viewer shows file name and correct dimensions/thumbnail
- Actual Result:
	- Image rendered inline in preview viewer
	- No forced download prompt shown
- Evidence:
	- Screenshot: `artifacts/ws4-evidence/sprint-1.8/image-preview-pass.png`
	- Command Output: n/a (UI flow)
	- Log/Trace: `files-ui` request completed with HTTP 200 for preview endpoint
- Suspected Layer (if Fail/Blocked): n/a
- Issue Link (if created): n/a
- Notes:
	- Repeated with PNG file `preview-test-image.png` and got same result
```

## Sprint Progress Tracker Template

Use this table to keep a live rollup while testing.

| Sprint | Total | Pass | Fail | Blocked | Remaining | Owner | Notes |
|---|---:|---:|---:|---:|---:|---|---|
| 1.1-1.2 File & Folder Ops | 11 | 11 | 0 | 0 | 0 | | All pass |
| 1.3 Chunked Upload & Dedup | 3 | 3 | 0 | 0 | 0 | | All pass |
| 1.4 Versioning | 4 | 4 | 0 | 0 | 0 | | All pass |
| 1.5 Sharing | 4 | 0 | 0 | 0 | 4 | | |
| 1.6 Quotas | 2 | 0 | 0 | 0 | 2 | | |
| 1.7 Collabora / WOPI | 3 | 0 | 0 | 0 | 3 | | |
| 1.8 File Preview | 6 | 0 | 0 | 0 | 6 | | |
| 1.9 Tags & Comments | 6 | 0 | 0 | 0 | 6 | | |
| 1.10 Sync Endpoints | 3 | 0 | 0 | 0 | 3 | | |
| 1.11 SQL Server Integration | 1 | 0 | 0 | 0 | 1 | | |
| 3 Range Requests | 2 | 0 | 0 | 0 | 2 | | |
| 4.1 FSW Debounce | 1 | 0 | 0 | 0 | 1 | | |
| 4.2 End-to-End Sync | 11 | 0 | 0 | 0 | 11 | | |
| 5 Module & Observability | 3 | 0 | 0 | 0 | 3 | | |
| 5 OpenTelemetry | 1 | 0 | 0 | 0 | 1 | | |
| 6 Security | 5 | 0 | 0 | 0 | 5 | | |
| **Total** | **66** | **0** | **0** | **0** | **66** | | |
