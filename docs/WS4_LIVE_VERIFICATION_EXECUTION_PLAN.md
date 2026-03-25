# WS-4 Live Verification Execution Plan

## Scope
Complete all WS-4 live verification items from `docs/REMAINING_WORK_PLAN.md` using the current environment:
- Server deployment running on mint22
- Web client running on monolith
- Active user: testdude@llabmik.net

## Goal
Finish all WS-4 checks with pass/fail evidence, then update tracking docs:
- `docs/REMAINING_WORK_PLAN.md`
- `docs/MASTER_PROJECT_PLAN.md`
- `docs/IMPLEMENTATION_CHECKLIST.md`

## Execution Phases

### Phase A: Web-Only Verification (monolith)
Run all browser-driven checks first.

1. Sprint 1.1-1.2: File and Folder Operations (11 items)
2. Sprint 1.3: Chunked Upload and Dedup (3 items)
3. Sprint 1.4: Versioning (4 items)
4. Sprint 1.5: Sharing (4 items)
5. Sprint 1.6: Quotas (2 items)
6. Sprint 1.8: File Preview (6 items)
7. Sprint 1.9: Tags and Comments (6 items)

Deliverable: 36 completed checks with evidence for each.

### Phase B: API and Protocol Verification (monolith -> mint22)
Run endpoint and protocol validations against the live deployment.

1. Sprint 1.10: Sync Endpoints (3 items)
2. Sprint 1.7 protocol check: WOPI CheckFileInfo metadata validation
3. Sprint 3: Range Requests (2 items)

Deliverable: Request/response evidence and expected behavior notes.

### Phase C: Sync End-to-End (Windows11-TestDNC and mint-dnc-client)
Validate full sync lifecycle across client machines.

1. Sprint 4.2: End-to-End Sync (11 items)
2. Sprint 4.1: FSW Debounce (1 item)

Deliverable: Per-platform and cross-platform sync evidence, including conflict handling.

### Phase D: Observability and Security (mint22)
Validate runtime behavior and hardening checks.

1. Sprint 5: Module and Observability (3 items)
2. Sprint 5: OpenTelemetry traces (1 item)
3. Sprint 6: Security checks (5 items)

Deliverable: Logs, trace IDs, and security test outcomes mapped to checklist items.

### Phase E: SQL Server and Documentation Closeout
Finalize remaining integration and tracking updates.

1. Sprint 1.11: SQL Server Integration (1 item)
2. Update `docs/REMAINING_WORK_PLAN.md` WS-4 checkboxes
3. Update `docs/MASTER_PROJECT_PLAN.md` status summary + step details
4. Update `docs/IMPLEMENTATION_CHECKLIST.md` corresponding items

Deliverable: WS-4 fully closed or explicitly marked blocked with reasons.

## Detailed Test Catalog (Solo Execution)

Use these as direct, runnable test cases. Each case is one checklist item from WS-4.

### Sprint 1.1-1.2: File and Folder Operations (11)

#### TC-1.1 Upload file via web UI
- Setup: Sign in as testdude@llabmik.net in Files UI.
- Steps:
	1. Click Upload.
	2. Select a small test file (for example 50 KB text file).
	3. Wait for upload completion.
- Pass criteria: File appears in current folder with expected name and size.

#### TC-1.2 Download uploaded file
- Setup: Existing file from TC-1.1.
- Steps:
	1. Click file actions.
	2. Choose Download.
	3. Open downloaded file locally.
- Pass criteria: Download succeeds and content matches original.

#### TC-1.3 Rename file
- Setup: Existing test file.
- Steps:
	1. Open file context menu.
	2. Select Rename and enter new name.
	3. Confirm.
- Pass criteria: List updates to new name and file is still accessible.

#### TC-1.4 Move file to subfolder
- Setup: File plus target subfolder.
- Steps:
	1. Create subfolder if needed.
	2. Move file using drag/drop or Move action.
	3. Open target folder.
- Pass criteria: File no longer in source folder and appears in target folder.

#### TC-1.5 Copy file
- Setup: Existing file.
- Steps:
	1. Use Copy action.
	2. Paste in same folder or a target folder.
	3. Refresh file list.
- Pass criteria: Original and copied file both exist with distinct names.

#### TC-1.6 Delete file to trash
- Setup: Existing file.
- Steps:
	1. Use Delete action.
	2. Open Trash view.
- Pass criteria: File removed from active view and present in Trash.

#### TC-1.7 Create new folder
- Setup: In Files root or selected parent folder.
- Steps:
	1. Click New Folder.
	2. Enter folder name.
	3. Confirm.
- Pass criteria: Folder appears immediately in file list.

#### TC-1.8 Navigate into folder and back
- Setup: Existing folder.
- Steps:
	1. Open folder.
	2. Verify breadcrumb/path changes.
	3. Navigate back to previous level.
- Pass criteria: Navigation works both directions and listing is correct.

#### TC-1.9 Rename folder
- Setup: Existing folder.
- Steps:
	1. Open folder context menu.
	2. Rename folder.
	3. Confirm.
- Pass criteria: Folder name updates and folder opens normally.

#### TC-1.10 Move folder into another folder
- Setup: Source folder and destination folder.
- Steps:
	1. Move source folder to destination.
	2. Open destination folder.
- Pass criteria: Source folder appears under destination and path resolves correctly.

#### TC-1.11 Delete folder and verify children trashed
- Setup: Folder containing at least one file.
- Steps:
	1. Delete the folder.
	2. Open Trash.
- Pass criteria: Deleted folder and child items are present in Trash.

### Sprint 1.3: Chunked Upload and Dedup (3)

#### TC-1.12 Upload file larger than 4 MB
- Setup: Prepare file larger than 4 MB.
- Steps:
	1. Upload large file.
	2. Monitor progress until complete.
- Pass criteria: Upload completes successfully without timeout.

#### TC-1.13 Upload same file again for dedup
- Setup: File from TC-1.12 already uploaded.
- Steps:
	1. Upload identical file again.
	2. Compare completion time/log indicators if available.
- Pass criteria: Second upload completes and backend indicates dedup behavior (no duplicate chunk storage behavior).

#### TC-1.14 Interrupt and resume upload
- Setup: Large file upload in progress.
- Steps:
	1. Start upload.
	2. Interrupt network/browser.
	3. Reopen session and retry upload.
- Pass criteria: Upload resumes from last chunk boundary and completes.

### Sprint 1.4: Versioning (4)

#### TC-1.15 Upload new version of existing file
- Setup: Existing file in folder.
- Steps:
	1. Upload replacement content with same logical file.
	2. Confirm version update flow is triggered.
- Pass criteria: File now has version history count greater than 1.

#### TC-1.16 Open version history panel
- Setup: File with at least two versions.
- Steps:
	1. Open file details/history.
	2. Inspect versions list.
- Pass criteria: Both versions are listed with expected metadata.

#### TC-1.17 Download previous version
- Setup: File with multiple versions.
- Steps:
	1. Choose older version in history.
	2. Download selected version.
	3. Open file locally.
- Pass criteria: Downloaded content matches the older revision.

#### TC-1.18 Restore previous version
- Setup: File with multiple versions.
- Steps:
	1. Select older version.
	2. Click Restore.
	3. Reopen current file.
- Pass criteria: Current file content reverts to selected older version.

### Sprint 1.5: Sharing (4)

#### TC-1.19 Share file with another user (read)
- Setup: Second account available for verification.
- Steps:
	1. Share file with read permission.
	2. Sign into second account and open share.
- Pass criteria: Second user can view/download but not modify.

#### TC-1.20 Create public link and open incognito
- Setup: Existing shareable file.
- Steps:
	1. Generate public link.
	2. Open link in incognito/private browser.
- Pass criteria: Anonymous access works according to default share permissions.

#### TC-1.21 Public link password protection
- Setup: Existing public link.
- Steps:
	1. Set password on link.
	2. Reopen link without password.
	3. Enter password and continue.
- Pass criteria: Access blocked until correct password is provided.

#### TC-1.22 Public link download limit
- Setup: Public link with configurable limit.
- Steps:
	1. Set small download limit (for example 1).
	2. Download once.
	3. Attempt second download.
- Pass criteria: Further downloads are blocked after limit reached.

### Sprint 1.6: Quotas (2)

#### TC-1.23 Set low quota for test user
- Setup: Admin access available.
- Steps:
	1. Open admin quota settings.
	2. Assign low quota to testdude@llabmik.net.
	3. Save settings.
- Pass criteria: New quota value is persisted and visible.

#### TC-1.24 Upload until quota exceeded
- Setup: Low quota configured.
- Steps:
	1. Upload files until threshold exceeded.
	2. Observe behavior at boundary.
- Pass criteria: Upload is rejected with clear quota error and no crash.

### Sprint 1.7: Collabora and WOPI (3)

#### TC-1.25 Open DOCX in Collabora
- Setup: Collabora integration configured.
- Steps:
	1. Upload DOCX file.
	2. Open in editor.
- Pass criteria: Document opens in Collabora editor successfully.

#### TC-1.26 Edit and save DOCX
- Setup: DOCX open in editor.
- Steps:
	1. Make small text edit.
	2. Save/auto-save.
	3. Return to file metadata/history.
- Pass criteria: New version is created after save.

#### TC-1.27 Verify WOPI CheckFileInfo metadata
- Setup: Valid WOPI token and file id.
- Steps:
	1. Call CheckFileInfo endpoint.
	2. Inspect JSON fields.
- Pass criteria: Metadata is complete and values match target file.

### Sprint 1.8: File Preview (6)

#### TC-1.28 Preview image (JPEG/PNG)
- Setup: JPEG and PNG sample files.
- Steps:
	1. Open each image in preview.
- Pass criteria: Inline image preview renders correctly.

#### TC-1.29 Preview video
- Setup: Small MP4 test video.
- Steps:
	1. Open video in preview.
	2. Play and pause.
- Pass criteria: Video preview loads and playback works.

#### TC-1.30 Preview PDF
- Setup: Sample PDF.
- Steps:
	1. Open PDF in preview.
	2. Navigate at least one page.
- Pass criteria: PDF renders in preview without forced download.

#### TC-1.31 Preview text/code file
- Setup: TXT or source code file.
- Steps:
	1. Open in preview.
	2. Scroll content.
- Pass criteria: Text content is readable in preview viewer.

#### TC-1.32 Preview Markdown
- Setup: Markdown file with headings and list.
- Steps:
	1. Open markdown file in preview.
- Pass criteria: Markdown displays correctly (rendered view or readable source per product design).

#### TC-1.33 Unsupported format fallback
- Setup: Unknown/unsupported file type.
- Steps:
	1. Open file in preview.
- Pass criteria: Preview offers Download File fallback.

### Sprint 1.9: Tags and Comments (6)

#### TC-1.34 Add tag to file
- Setup: Existing file.
- Steps:
	1. Add a new tag from file details.
- Pass criteria: Tag appears on file and persists after refresh.

#### TC-1.35 Filter files by tag
- Setup: At least two tagged files.
- Steps:
	1. Select tag filter.
- Pass criteria: List shows only files with selected tag.

#### TC-1.36 Remove tag
- Setup: Tagged file.
- Steps:
	1. Remove one tag.
	2. Refresh.
- Pass criteria: Tag is no longer associated.

#### TC-1.37 Add comment to file
- Setup: Existing file.
- Steps:
	1. Post comment from comments panel.
- Pass criteria: Comment appears with author and timestamp.

#### TC-1.38 Reply to comment (threaded)
- Setup: Existing root comment.
- Steps:
	1. Add reply.
- Pass criteria: Reply is nested under root comment.

#### TC-1.39 Edit and delete comment
- Setup: Existing comment by current user.
- Steps:
	1. Edit comment text.
	2. Delete comment.
- Pass criteria: Edit persists, then deletion removes or marks comment per design.

### Sprint 1.10: Sync Endpoints (3)

#### TC-1.40 GET sync changes
- Setup: Known baseline timestamp.
- Steps:
	1. Call GET /api/v1/files/sync/changes?since=<timestamp>.
- Pass criteria: Response returns expected changed items only.

#### TC-1.41 POST sync reconcile
- Setup: Prepare local state payload.
- Steps:
	1. Call POST /api/v1/files/sync/reconcile.
	2. Inspect diff response.
- Pass criteria: Server returns correct reconcile actions.

#### TC-1.42 GET sync tree
- Setup: Folder tree with known hashes.
- Steps:
	1. Call GET /api/v1/files/sync/tree.
- Pass criteria: Response includes complete tree and hash data.

### Sprint 1.11: SQL Server Integration (1)

#### TC-1.43 Run integration tests against SQL Server
- Setup: SQL Server test environment configured.
- Steps:
	1. Execute integration test suite for SQL Server target.
- Pass criteria: All designated integration tests pass.

### Sprint 3: Range Requests (2)

#### TC-1.44 Browser video seek on large file
- Setup: Large video uploaded.
- Steps:
	1. Start playback.
	2. Seek to different timestamps.
- Pass criteria: Seeking works without full re-download behavior.

#### TC-1.45 Curl range resume
- Setup: Downloadable large file.
- Steps:
	1. Start partial download.
	2. Resume with curl --range.
- Pass criteria: Server returns correct partial content behavior and resume completes.

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

### Sprint 5: Module and Observability (3)

#### TC-1.58 Verify gRPC between core and Files host
- Setup: mint22 server access.
- Steps:
	1. Perform file operations.
	2. Inspect logs/health endpoints for module communication.
- Pass criteria: gRPC calls between core and Files host are visible and successful.

#### TC-1.59 Verify module start and stop
- Setup: Service control access on mint22.
- Steps:
	1. Restart module process.
	2. Confirm clean startup and graceful stop behavior.
- Pass criteria: Module lifecycle completes without crash or orphaned state.

#### TC-1.60 Verify i18n strings for Files UI
- Setup: Alternate locale available.
- Steps:
	1. Switch locale.
	2. Reload Files UI.
- Pass criteria: Files UI strings are localized and no missing keys appear.

### Sprint 5: OpenTelemetry (1)

#### TC-1.61 Verify Files traces in telemetry backend
- Setup: Jaeger or OTLP collector reachable.
- Steps:
	1. Trigger file operations.
	2. Query telemetry backend.
- Pass criteria: Traces contain Files operation spans with expected metadata.

### Sprint 6: Security (5)

#### TC-1.62 Path traversal create rejected
- Setup: File create dialog/API access.
- Steps:
	1. Attempt create with ../../etc/passwd style name.
- Pass criteria: Operation rejected with safe validation error.

#### TC-1.63 Path traversal rename rejected
- Setup: Existing file.
- Steps:
	1. Attempt rename to ../../../tmp/evil style path.
- Pass criteria: Rename is rejected and file remains intact.

#### TC-1.64 Quota exceed does not crash
- Setup: Low quota configuration.
- Steps:
	1. Upload file that exceeds quota.
	2. Observe client and server behavior.
- Pass criteria: Clear error shown, no service crash.

#### TC-1.65 Rate limiting applied to upload endpoints
- Setup: Rate limiting configured.
- Steps:
	1. Send burst upload requests.
- Pass criteria: Requests are throttled according to policy.

#### TC-1.66 429 response includes Retry-After
- Setup: Triggered rate limit condition.
- Steps:
	1. Capture throttled upload response.
- Pass criteria: Response status is 429 and Retry-After header is present.

## Who Runs What (Solo + Copilot Split)

This section marks each test as:
- User-only: Requires manual UI interaction, external machine access, or credentials only you can provide in browser/session.
- Copilot-capable: I can run from terminal here if required inputs (URL/token/test data) are available.
- Hybrid: You do the UI step and I can validate backend/log/API evidence.

### Ownership by Test Range

| Test Range | Area | Owner |
|---|---|---|
| TC-1.1 to TC-1.11 | File and folder UI operations | User-only |
| TC-1.12 to TC-1.14 | Chunked upload/dedup/resume | Hybrid |
| TC-1.15 to TC-1.18 | Versioning UI workflows | User-only |
| TC-1.19 to TC-1.22 | Sharing flows | User-only |
| TC-1.23 to TC-1.24 | Admin quota + UI boundary behavior | User-only |
| TC-1.25 to TC-1.26 | Collabora editing flow | User-only |
| TC-1.27 | WOPI CheckFileInfo API | Copilot-capable |
| TC-1.28 to TC-1.33 | File preview UI | User-only |
| TC-1.34 to TC-1.39 | Tags/comments UI | User-only |
| TC-1.40 to TC-1.42 | Sync endpoints API | Copilot-capable |
| TC-1.43 | SQL Server integration tests | Copilot-capable |
| TC-1.44 | Browser seek test | User-only |
| TC-1.45 | Curl range resume | Copilot-capable |
| TC-1.46 | FSW debounce (local editor/save behavior) | Hybrid |
| TC-1.47 to TC-1.49 | Sync service install and OAuth tray setup | User-only |
| TC-1.50 to TC-1.57 | Cross-machine sync scenarios | User-only |
| TC-1.58 to TC-1.61 | Module/gRPC/OTel log and trace verification | Hybrid |
| TC-1.62 to TC-1.66 | Security/rate-limit behavior | Hybrid |

### Fast Summary

- User-only: 48 tests
- Copilot-capable: 7 tests (TC-1.27, TC-1.40 to TC-1.43, TC-1.45)
- Hybrid: 11 tests (TC-1.12 to TC-1.14, TC-1.46, TC-1.58 to TC-1.66)

## Command Playbook (Copilot-Capable + Hybrid Backend Checks)

Use these command blocks for tests I can execute from terminal.

### Common Environment Variables

```bash
export DNC_BASE_URL="https://<your-server-host>"
export DNC_BEARER_TOKEN="<paste-access-token>"
export DNC_FILE_ID="<target-file-id>"
export DNC_WOPI_TOKEN="<wopi-token>"
export DNC_SINCE="2026-03-25T00:00:00Z"
```

### TC-1.40 GET Sync Changes

```bash
curl -sS -D - \
	-H "Authorization: Bearer $DNC_BEARER_TOKEN" \
	"$DNC_BASE_URL/api/v1/files/sync/changes?since=$DNC_SINCE"
```

Pass checks:
- HTTP 200
- Response includes only changes newer than DNC_SINCE

### TC-1.41 POST Sync Reconcile

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

Pass checks:
- HTTP 200
- Response contains expected server diff actions

### TC-1.42 GET Sync Tree

```bash
curl -sS -D - \
	-H "Authorization: Bearer $DNC_BEARER_TOKEN" \
	"$DNC_BASE_URL/api/v1/files/sync/tree"
```

Pass checks:
- HTTP 200
- Tree entries include paths and hashes

### TC-1.27 WOPI CheckFileInfo

```bash
curl -sS -D - \
	"$DNC_BASE_URL/api/v1/wopi/files/$DNC_FILE_ID?access_token=$DNC_WOPI_TOKEN"
```

Pass checks:
- HTTP 200
- JSON includes key metadata fields (BaseFileName, Size, UserId, Version)

### TC-1.45 Range Resume

```bash
# First partial chunk
curl -sS -D /tmp/range-head-1.txt \
	-H "Authorization: Bearer $DNC_BEARER_TOKEN" \
	-H "Range: bytes=0-1048575" \
	"$DNC_BASE_URL/api/v1/files/$DNC_FILE_ID/download" \
	-o /tmp/part1.bin

# Resume from next range
curl -sS -D /tmp/range-head-2.txt \
	-H "Authorization: Bearer $DNC_BEARER_TOKEN" \
	-H "Range: bytes=1048576-" \
	"$DNC_BASE_URL/api/v1/files/$DNC_FILE_ID/download" \
	-o /tmp/part2.bin

cat /tmp/part1.bin /tmp/part2.bin > /tmp/reconstructed.bin
```

Pass checks:
- Responses are HTTP 206 Partial Content
- Content-Range header exists
- Reconstructed file hash/size matches source file

### TC-1.65 and TC-1.66 Upload Rate Limit and Retry-After

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

Pass checks:
- At least one response is HTTP 429
- 429 responses include Retry-After header

### TC-1.43 SQL Server Integration Tests

```bash
export DOTNETCLOUD_TEST_SQLSERVER_CONNECTION_STRING="Server=<sql-host>;Database=<db>;User Id=<user>;Password=<pass>;TrustServerCertificate=true"

dotnet test tests/DotNetCloud.Integration.Tests/ \
	-p:DatabaseProvider=SqlServer
```

Pass checks:
- Test run completes
- No failed tests in SQL Server target run

Notes:
- When DOTNETCLOUD_TEST_SQLSERVER_CONNECTION_STRING is set, integration tests use that SQL Server first.
- This enables testing against network SQL Server hosts (for example Hyperdrive) without requiring local Windows SQL Server or Docker SQL Server.

## Inputs Needed Before I Run Copilot-Capable Tests

Provide these once and I can run the command-based tests directly:
1. Base URL for running deployment
2. Bearer token with file API access
3. One file id for download/range/WOPI checks
4. WOPI token (if different from bearer auth model)
5. Confirmation SQL Server integration environment is available

## How To Get Missing Inputs

### Bearer token

Option A (API login):
- Call POST /api/v1/core/auth/login with test user email/password.
- Use data.accessToken from the response.

Option B (browser capture):
- Open browser DevTools while logged in.
- Check Network for authenticated API calls and copy Authorization Bearer token.
- Or inspect local/session storage if token is stored there by the client.

### File id

- A file id is the server GUID/identifier for an existing file.
- Get it from sync tree API response, file details API response, or file-specific network calls in DevTools.
- Any accessible existing file id is fine.

### WOPI token

- Open a DOCX in Collabora flow.
- Capture access_token from the WOPI request URL in DevTools.
- If endpoint accepts bearer auth in your deployment, test that first; otherwise use access_token query parameter.

### SQL Server integration environment

- Existing integration tests prefer local SQL Server on Windows and otherwise attempt Docker SQL Server.
- Your external SQL Server can still be used, but that requires test wiring changes.

## Solo Execution Checklist

Use this mini-flow for each test case:
1. Run test case steps.
2. Mark Pass, Fail, or Blocked.
3. Capture one artifact.
4. Fill one Per-Item Result entry.
5. Update Sprint Progress Tracker counts.
6. Move to next test case.

## Recommended Run Order

1. Day 1: Phase A core file/folder, sharing, quota checks
2. Day 2: Phase A preview + comments, then Phase B endpoints/range
3. Day 3: Phase C client setup and baseline bidirectional sync
4. Day 4: Phase C advanced sync scenarios (conflicts, offline queue, selective sync, multi-account)
5. Day 5: Phase D observability and security, plus SQL Server integration
6. Day 6: Reruns for failures, complete documentation updates, final signoff

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
| 1.1-1.2 File & Folder Ops | 11 | 0 | 0 | 0 | 11 | | |
| 1.3 Chunked Upload & Dedup | 3 | 0 | 0 | 0 | 3 | | |
| 1.4 Versioning | 4 | 0 | 0 | 0 | 4 | | |
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

## Immediate Start Point
Start with Sprint 1.8 (File Preview, 6 items), then continue through the rest of Phase A in the same session.
