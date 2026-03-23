# Phase 1 Remaining Work Plan

> **Status: ✓ ALL COMPLETE** — All items in both Option A and Option B have been implemented, tested, and merged.
>
> **Context:** Phase 1 can close by deferring Sprint 2, but two categories of work remain: one truly missing code item (OpenAPI) and the deferred Sprint 2 UI features.

---

## Option A: OpenAPI/Swagger for Files Module Host

**Problem:** The core server has OpenAPI via `Microsoft.AspNetCore.OpenApi` + Scalar, but the Files module host — which runs as a **separate process** — does not call `AddDotNetCloudOpenApi()` and has no OpenAPI generation. The 14 Files controllers (~80+ endpoints) are invisible to API documentation.

**Effort:** Small — 1–2 hours. Configuration-only; no new endpoints or logic needed.

### Tasks

| # | Task | Effort | Notes |
|---|------|--------|-------|
| A1 | Add `Microsoft.AspNetCore.OpenApi` + `Scalar.AspNetCore` package refs to `DotNetCloud.Modules.Files.Host.csproj` | 5 min | Match versions from Core.Server (10.0.3 / 2.0.30) |
| A2 | Call `AddDotNetCloudOpenApi()` in Files Host `Program.cs` (or inline `AddOpenApi("v1")` + Scalar config) | 15 min | Mirror `OpenApiConfiguration.cs` from Core.Server; gate behind `IsDevelopment()` |
| A3 | Verify all 14 controllers produce OpenAPI schemas | 15 min | Hit `/openapi/v1.json` on the module host port; confirm endpoints listed |
| A4 | Add XML doc comments to any controller actions missing `<summary>` tags | 30 min | Improves generated descriptions; check `ProducesResponseType` attributes exist |
| A5 | Verify Scalar UI renders at `/scalar/v1` on module host | 5 min | Interactive API explorer for dev/testing |
| A6 | Add `[ProducesResponseType]` attributes to any endpoints missing them | 30 min | Ensures accurate 200/400/401/404/409 response schemas |
| A7 | Test: build + run module host, confirm no regressions | 10 min | `dotnet build` + smoke test |

### Exit Criteria
- `GET <files-host>/openapi/v1.json` returns valid OpenAPI 3.x document listing all Files endpoints.
- Scalar interactive UI loads in browser and can send test requests.
- Sprint 5.3 checklist item `☐ Verify OpenAPI/Swagger documentation is generated for Files API endpoints` → `✓`.

### Risk
- Near zero. This is additive configuration with no behavioral changes to existing endpoints.

---

## Option B: Sprint 2 — JS Interop UI Features

**Problem:** Five UI features were deferred during Phase 1 because they require JavaScript interop. All file operations are already accessible via buttons and toolbar, so these are **UX polish** rather than functionality blockers.

**Effort:** Medium-High — 3–5 days total across all items.

### Existing Infrastructure (Already Built)

| Asset | What It Does | Location |
|-------|-------------|----------|
| `files-drop-bridge.js` | Full HTML5 drag-drop bridge — extracts files + folders from `DataTransfer`, invokes Blazor | `wwwroot/js/files-drop-bridge.js` |
| `composer-toolbar.js` | Clipboard paste handler (image only) for Chat composer | `wwwroot/js/composer-toolbar.js` |
| `file-upload.js` | Chunked upload with SHA-256 hashing, initiate/put-chunks/complete flow | `wwwroot/js/file-upload.js` |
| `ChunkedUploadService` | C# upload orchestration service (pairs with `file-upload.js`) | UI.Shared or UI.Web.Client |

### B1: Right-Click Context Menu — ~1 day

**Goal:** Right-click a file/folder in the browser → floating menu with Rename, Move, Copy, Share, Delete, Download.

| # | Task | Effort |
|---|------|--------|
| B1.1 | Create `context-menu.js` — JS module that listens for `contextmenu` event on file items, prevents default, calculates position (viewport-aware), invokes Blazor callback with `(nodeId, x, y)` | 2 hr |
| B1.2 | Create `FileContextMenu.razor` — Blazor component: absolutely-positioned menu at `(x, y)`, action buttons, renders conditionally | 2 hr |
| B1.3 | Wire menu actions to existing service methods (`RenameAsync`, `MoveAsync`, `CopyAsync`, `DeleteAsync`, `DownloadAsync`, open ShareDialog) | 1 hr |
| B1.4 | Dismiss on click-outside, Escape key, or scroll — JS `mousedown` + `keydown` listeners | 30 min |
| B1.5 | CSS: shadow, border-radius, hover states, separator lines, icon alignment. Match existing DotNetCloud design system | 1 hr |
| B1.6 | Accessibility: keyboard navigation within menu (arrow keys), `role="menu"` / `role="menuitem"`, focus trap | 1 hr |
| B1.7 | Tests: component tests for menu rendering, action invocation, dismiss behavior | 1 hr |

**Dependencies:** None. Purely additive UI.

### B2: Drag-and-Drop Move to Folder — ~1 day

**Goal:** Drag a file/folder onto another folder in the browser → move it there.

| # | Task | Effort |
|---|------|--------|
| B2.1 | Create `file-drag-move.js` — JS module: `dragstart` on file items (set `dataTransfer` with nodeId), `dragover` on folder items (add highlight class), `drop` on folder (invoke Blazor with source nodeId + target folderId) | 2 hr |
| B2.2 | Visual feedback: drag ghost image (file icon + name), folder highlight border/background on valid targets | 1 hr |
| B2.3 | Wire `drop` callback to existing `MoveAsync` API call | 30 min |
| B2.4 | Error handling: toast on permission error, name conflict dialog (rename/replace/skip) | 1 hr |
| B2.5 | Multi-select drag: if dragging a selected file and other files are also selected, move all selected items | 1 hr |
| B2.6 | Prevent invalid drops: can't drop folder into itself, can't drop into same parent (no-op) | 30 min |
| B2.7 | Tests: drag interaction tests, move API call verification, error states | 1 hr |

**Note:** `files-drop-bridge.js` handles file-upload drops (external files from OS). This new module handles **internal** file-to-folder move drags. They should coexist — distinguish by checking if `dataTransfer` contains node IDs (internal move) vs. `File` objects (external upload).

### B3: Upload Queue Management — ~0.5 day

**Goal:** Per-file Pause / Resume / Cancel in `UploadProgressPanel` with actual chunk-level control.

| # | Task | Effort |
|---|------|--------|
| B3.1 | Add `CancellationTokenSource` per upload in `ChunkedUploadService` (or its JS equivalent); pass to each chunk PUT request | 1 hr |
| B3.2 | Create `upload-abort.js` — JS interop to call `AbortController.abort()` on in-flight `fetch` requests | 1 hr |
| B3.3 | Wire Pause button → store `lastCompletedChunkIndex`, abort current fetch, hold state | 30 min |
| B3.4 | Wire Resume button → restart from `lastCompletedChunkIndex + 1` (server already supports partial sessions via `GET /upload/{sessionId}`) | 30 min |
| B3.5 | Wire Cancel button → abort fetch + call `DELETE /upload/{sessionId}` to clean up server-side session | 30 min |
| B3.6 | UI: update progress bar states (paused = yellow, cancelled = grey, error = red) | 30 min |

**Dependencies:** Existing upload infrastructure in `file-upload.js` + `ChunkedUploadService`.

### B4: Paste Image Upload — ~0.5 day

**Goal:** Ctrl+V in the file browser → auto-upload clipboard image as `paste-YYYY-MM-DD-HHmmss.png`.

| # | Task | Effort |
|---|------|--------|
| B4.1 | Create `file-paste.js` — JS module: listen for `paste` event on file browser container, extract image from `clipboardData.items`, read as `ArrayBuffer` | 1 hr |
| B4.2 | Generate filename from timestamp: `paste-2026-03-23-143022.png` | 15 min |
| B4.3 | Invoke Blazor callback with `(fileName, mimeType, base64Data)` | 15 min |
| B4.4 | Blazor handler: convert to upload flow (single-chunk upload, skip chunking for small clipboard images) | 1 hr |
| B4.5 | UX: show brief toast "Image pasted — uploading..." with progress | 15 min |
| B4.6 | Guard: only trigger on image MIME types; ignore text pastes | 15 min |

**Note:** `composer-toolbar.js` already does this for Chat. Can extract the paste-handling logic into a shared utility or simply replicate the pattern.

### B5: Upload Size Validation — ~2 hours

**Goal:** Reject oversized files on the client side before upload begins.

| # | Task | Effort |
|---|------|--------|
| B5.1 | Add `GET /api/v1/files/config` endpoint (or piggyback on existing module info) returning `{ maxUploadSizeBytes: N }` | 30 min |
| B5.2 | Cache the value in the Blazor app on first load (singleton service or cascading parameter) | 30 min |
| B5.3 | Validate in `file-upload.js` before `initiate` call; if file exceeds max, invoke Blazor error callback instead of starting upload | 30 min |
| B5.4 | UI: show error toast with file name + max size (human-readable, e.g., "500 MB") | 15 min |
| B5.5 | Also validate in paste-upload (B4) and drop-upload paths | 15 min |

---

## Comparison

| Factor | A: OpenAPI | B: Sprint 2 UI |
|--------|-----------|----------------|
| **Effort** | ~2 hours | ~3–5 days |
| **User-visible impact** | Dev/API consumers only | All web UI users |
| **Blocks Phase 1 close?** | Yes — it's `[missing]` in Sprint 5.3 | No — explicitly deferrable |
| **Risk** | Near zero | Low-medium (JS interop debugging) |
| **Dependencies** | None | Existing JS infrastructure |
| **Reversibility** | Fully additive | Fully additive |

---

## Recommended Approach

### If goal is "close Phase 1 fast":
Do **A only**. Takes ~2 hours. Clears the last `[missing]` code item. Phase 1 closes with Sprint 2 deferred (as the plan already authorizes).

### If goal is "ship polished Phase 1":
Do **A first** (2 hours), then **B1 + B4 + B5** (context menu + paste + size validation — ~2 days). These three give the highest UX lift per effort. Defer B2 (drag-move) and B3 (upload queue) to Phase 1.1 polish.

### If goal is "complete everything":
Do **A → B5 → B4 → B1 → B3 → B2** in that order (easiest/highest-value first). ~4–5 days total.
