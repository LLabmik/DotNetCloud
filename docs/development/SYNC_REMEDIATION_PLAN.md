# Sync Remediation Plan

**Created:** 2026-03-09  
**Source:** [SYNC_VERIFICATION_PLAN.md](SYNC_VERIFICATION_PLAN.md) verification results  
**Handoff:** [CLIENT_SERVER_MEDIATION_HANDOFF.md](CLIENT_SERVER_MEDIATION_HANDOFF.md) (continuing from Issue #47)  

---

## Summary

Verification found **4 missing** and **10 partial** items across the 5 sync improvement batches.
This plan organizes them into prioritized remediation batches with clear SERVER/CLIENT ownership.

| Priority | Issues | Description |
|----------|--------|-------------|
| **P1 — Must Fix** | #48–#51 | Missing functionality specified in the implementation guide |
| **P2 — Should Fix** | #52–#58 | Partial implementations with functional gaps |
| **P3 — Nice to Have** | #59–#61 | Minor polish items, low impact |

---

## Ownership Summary

| Owner | P1 | P2 | P3 | Total |
|-------|:---:|:---:|:---:|:---:|
| **CLIENT only** | 3 | 5 | 2 | 10 |
| **SERVER only** | 0 | 2 | 1 | 3 |
| **BOTH** | 1 | 0 | 0 | 1 |

---

## P1 — Must Fix (Missing Functionality)

### Issue #48: Three-Pane Merge Editor (Task 3.5e) — CLIENT

**What's missing:** The entire merge editor UI. No `MergeEditorWindow`, `MergeEditorView`, or merge-related command in `ConflictViewModel`. DiffPlex is used only for headless auto-merge. `Microsoft.XmlDiffPatch` is not installed.

**Deliverables:**
- ☐ `MergeEditorWindow.axaml` + `MergeEditorWindow.axaml.cs` — separate Avalonia window
- ☐ `MergeEditorViewModel.cs` — 4-pane model: Left (local, read-only), Center (base, read-only), Right (server, read-only), Bottom (merged result, editable)
- ☐ DiffPlex integration: line-level diff visualization with colors (green=added, red=removed, yellow=conflict)
- ☐ Auto-merge non-conflicting hunks; conflict markers for overlapping changes
- ☐ Commands: click hunk to apply, "Accept all local", "Accept all server", "Reset merge", "Save & resolve", "Cancel"
- ☐ `MergeCommand` added to `ConflictViewModel` (opens merge editor for text files, disabled for binary)
- ☐ Binary file detection: show only Keep/Both buttons (no merge editor)
- ☐ Text file types: `.txt`, `.md`, `.json`, `.yaml`, `.cs`, `.py`, `.js`, `.ts`, `.html`, `.css`, `.xml`, etc.
- ☐ Install `Microsoft.XmlDiffPatch` NuGet package
- ☐ XML merge: tree-based diffing for `.xml`, `.csproj`, `.fsproj`, `.props`, `.targets`, `.xaml`, `.svg`, `.xslt`
- ☐ XML post-merge validation via `XDocument.Parse()`
- ☐ In-editor help panel for first XML merge
- ☐ Unit tests for `MergeEditorViewModel`

**Complexity:** HIGH — largest single deliverable. Consider splitting into sub-issues:
- #48a: Text merge editor (DiffPlex UI)
- #48b: XML merge editor (XmlDiffPatch)

**Reference:** [SYNC_IMPLEMENTATION_GUIDE.md](SYNC_IMPLEMENTATION_GUIDE.md) Task 3.5e

---

### Issue #49: Client ETag/If-None-Match (Task 2.6) — BOTH ✅

**What's missing:** Client never sends conditional GET requests. `DownloadChunkByHashAsync()` is a plain GET with no `If-None-Match` header and no 304 handling.

**Server status:** ✓ Already returns `ETag` header and handles `If-None-Match` → 304.

**Deliverables:**
- ✓ `DotNetCloudApiClient.DownloadChunkByHashAsync()`: add `If-None-Match: "{chunkHash}"` header
- ✓ Handle `HttpStatusCode.NotModified` (304) — return cached chunk from local filesystem
- ✓ Unit tests for conditional GET logic (304 path, 200 path, cache miss path)

**Complexity:** LOW — ~20 lines of code change in one method. **Resolved:** `158ebdc`

**Reference:** [SYNC_IMPLEMENTATION_GUIDE.md](SYNC_IMPLEMENTATION_GUIDE.md) Task 2.6

---

### Issue #50: Compression Skip for Pre-Compressed Types (Task 2.3) — CLIENT ✅

**What's missing:** `UploadChunkAsync()` wraps ALL chunks in `GZipStream` unconditionally. No MIME type or extension parameter exists, so it cannot skip compression for already-compressed files.

**Deliverables:**
- ✓ Add `string? mimeType` or `string? fileExtension` parameter to `UploadChunkAsync()`
- ✓ Define skip list: `.jpg`, `.jpeg`, `.png`, `.gif`, `.webp`, `.zip`, `.gz`, `.bz2`, `.xz`, `.7z`, `.rar`, `.mp4`, `.mp3`, `.mkv`, `.avi`, `.webm`, `.flac`, `.ogg`, `.woff2`
- ✓ Skip GZip wrapping when file extension matches skip list
- ✓ Caller (`ChunkedTransferClient`) passes file extension from upload context
- ✓ Unit tests for skip logic

**Complexity:** LOW — ~30 lines across 2 files. **Resolved:** `158ebdc`

**Reference:** [SYNC_IMPLEMENTATION_GUIDE.md](SYNC_IMPLEMENTATION_GUIDE.md) Task 2.3

---

### Issue #51: Client Case-Sensitivity Handling (Task 4.1) — CLIENT

**What's missing:** Server-side case checks exist, but client has no explicit case-conflict handling. No `StringComparer.OrdinalIgnoreCase` in `SyncEngine`, no `(case conflict)` suffix renaming.

**Deliverables:**
- ☐ `SyncEngine`: use `StringComparer.OrdinalIgnoreCase` for path comparisons on Windows/macOS
- ☐ Before applying remote file: check if file with different casing already exists locally
- ☐ If conflict on case-insensitive FS: rename to `filename (case conflict).ext`
- ☐ Log warning with both path variants
- ☐ Unit tests for case-conflict detection and renaming

**Complexity:** MEDIUM — needs careful platform-conditional logic.

**Reference:** [SYNC_IMPLEMENTATION_GUIDE.md](SYNC_IMPLEMENTATION_GUIDE.md) Task 4.1

---

## P2 — Should Fix (Partial Implementations)

### Issue #52: RequestId in Serilog LogContext (Task 1.2) — SERVER ✅

**What's missing:** `RequestCorrelationMiddleware` sets `TraceIdentifier` but does NOT call `LogContext.PushProperty("RequestId", requestId)`. Structured logs don't include `RequestId` field.

**Deliverables:**
- ✓ In `RequestCorrelationMiddleware`: add `using (LogContext.PushProperty("RequestId", requestId))` around `await _next(context)`
- ✓ Verify `UseSerilogRequestLogging()` picks up the property
- ✓ Unit test: verify log entries include `RequestId` property

**Complexity:** LOW — 3-line change. **Resolved:** `0a0ab19`

---

### Issue #53: Sync-Specific Rate Limit Policy Names (Task 1.3) — SERVER

**What's partial:** Rate limiting works but uses generic `module-{name}` config-driven policies instead of the spec's named `sync-standard`, `sync-heavy`, `sync-tree` policies.

**Decision needed:** Is renaming worth the churn? Current approach is functionally equivalent and more flexible (config-driven limits per endpoint). **Recommend: SKIP — current approach is better than spec.**

**Deliverables (if proceeding):**
- ☐ Rename policies or add aliases: `sync-standard`, `sync-heavy`, `sync-tree`
- ☐ Update controller attributes

**Complexity:** LOW but disruptive — touches many endpoint files.

---

### Issue #54: Content-Disposition on Versioned Downloads (Task 1.9) — SERVER ✅

**What's missing:** Current-version downloads include `Content-Disposition: attachment` via `File()` helper with filename. Versioned download path calls `File(stream, mime)` without filename parameter.

**Deliverables:**
- ✓ In versioned download endpoint: add filename to `File()` call or set `Content-Disposition` header explicitly
- ✓ Unit test: versioned download response includes `Content-Disposition: attachment; filename="..."`

**Complexity:** LOW — 1-line fix. **Resolved:** `0a0ab19`

---

### Issue #55: Conflict Resolution Settings in sync-settings.json (Task 3.5b) — CLIENT

**What's missing:** `sync-settings.json` has no `conflictResolution` section. Strategy 4 (newer-wins) threshold is hardcoded at 5 minutes.

**Deliverables:**
- ☐ Add `conflictResolution` section to `sync-settings.json` schema and defaults:
  ```json
  "conflictResolution": {
    "autoResolveEnabled": true,
    "newerWinsThresholdMinutes": 5,
    "enabledStrategies": ["identical", "fast-forward", "clean-merge", "newer-wins", "append-only"]
  }
  ```
- ☐ `ConflictResolver` reads settings from config instead of hardcoded values
- ☐ Settings UI: checkboxes for each strategy, threshold slider

**Complexity:** MEDIUM — config plumbing + UI.

---

### Issue #56: Conflict Notification Polish (Task 3.5c) — CLIENT

**What's partial:** Tray icon changes color but no badge count overlay. No 24-hour recurring reminder. No first-conflict educational notification.

**Deliverables:**
- ☐ Badge count overlay on tray icon (platform-dependent — may need native interop)
- ☐ 24-hour recurring re-notification timer for unresolved conflicts
- ☐ First-conflict educational notification (different message on first-ever conflict)

**Complexity:** MEDIUM — badge overlay is platform-specific (may be limited by Avalonia tray API).

---

### Issue #57: FSW.Error Event + Symlink Config (Tasks 4.3, 4.4) — CLIENT

**What's partial:** `FileSystemWatcher.Error` event not subscribed (only Created/Changed/Deleted/Renamed). Symlink mode not configurable in `sync-settings.json`.

**Deliverables:**
- ☐ Subscribe to `FileSystemWatcher.Error` → log error + set `_pollingFallback = true` + notify user
- ☐ Add `symlinks` section to `sync-settings.json`: `{ "mode": "ignore" | "sync-as-link" }`
- ☐ Settings UI: dropdown for symlink mode

**Complexity:** LOW — 2 small changes.

---

### Issue #58: Selective Sync Cleanup + Lazy Load (Task 5.2) — CLIENT

**What's partial:** Lazy-load children has a TODO comment. When selections change, excluded files are not actively deleted locally.

**Deliverables:**
- ☐ Implement lazy-load children on expand in `FolderBrowserViewModel`
- ☐ When folder is unchecked: delete local files for that folder (with confirmation dialog)
- ☐ Unit tests for lazy-load and cleanup behavior

**Complexity:** MEDIUM — deletion needs careful UX.

---

## P3 — Nice to Have (Minor Polish)

### Issue #59: TaskCanceledException Retry (Task 1.5) — CLIENT ✅

**What's partial:** Per-chunk retry catches `HttpRequestException` and 5xx but not `TaskCanceledException` (HTTP timeouts).

**Deliverables:**
- ✓ Add `TaskCanceledException` (when not user-cancelled) to retry conditions
- ✓ Unit test for timeout retry

**Complexity:** LOW — 3-line change. **Resolved:** `158ebdc`

---

### Issue #60: Windows Long Path Support (Task 4.5) — CLIENT

**What's partial:** No `longPathAware` app manifest, no `LongPathsEnabled` registry check.

**Deliverables:**
- ☐ Add `<longPathAware>true</longPathAware>` to app manifest (`.manifest` file)
- ☐ On first Windows run: check `HKLM\SYSTEM\CurrentControlSet\Control\FileSystem\LongPathsEnabled`
- ☐ If 0: show notification suggesting to enable
- ☐ Add `SyncStateTag.PathTooLong` value
- ☐ Linux: filename byte-length check + truncation with `~{hash}.ext`

**Complexity:** MEDIUM — Windows-specific, needs manifest + registry.

---

### Issue #61: Session Resume Window Alignment (Task 3.2) — CLIENT ✅

**What's partial:** Session resume window is 18 hours but cleanup is 48 hours. Spec said 48h for both.

**Deliverables:**
- ✓ Change `SessionResumeWindow` from 18h to 48h (or make configurable)

**Complexity:** LOW — 1-line change. **Resolved:** `158ebdc`

---

## Recommended Execution Order

### Remediation Batch A — Quick Wins (Issues #49, #50, #52, #54, #59, #61)

All LOW complexity. Can be done in one session per side.

**Server (#52, #54):** LogContext.PushProperty + Content-Disposition fix  
**Client (#49, #50, #59, #61):** ETag headers + compression skip + timeout retry + session window

### Remediation Batch B — Medium Items (Issues #51, #55, #56, #57, #58)

MEDIUM complexity. One session per side.

**Client (#51, #55, #57, #58):** Case sensitivity + conflict config + FSW.Error + selective sync  
**Server:** None in this batch.

### Remediation Batch C — Merge Editor (Issue #48)

HIGH complexity. Likely needs multiple sessions.

**Client (#48a):** Text merge editor with DiffPlex UI  
**Client (#48b):** XML merge editor with XmlDiffPatch  

### Remediation Batch D — Platform Polish (Issues #53, #56, #60)

**Consider skipping #53** (rate limit renaming — current approach is better).  
**Client (#56, #60):** Badge overlay + long path support  

---

## Mediation Handoff Format

Each issue follows the established handoff process:

```markdown
### Issue #NN: [Title]
**Owner:** SERVER | CLIENT | BOTH
**Status:** ☐ Not started

**Send to [Server|Client] Agent:**
[Description of what to implement]

**Acceptance criteria:**
- [ ] Code compiles
- [ ] Tests pass
- [ ] Specific verification checks

**Request Back:**
- commit hash
- files changed
- test results
```

When ready to start, open the issue in [CLIENT_SERVER_MEDIATION_HANDOFF.md](CLIENT_SERVER_MEDIATION_HANDOFF.md).
