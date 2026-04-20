# Video Chat Background Blur Toggle — Implementation Plan

## Overview

Add a client-side background blur toggle to video calls using **MediaPipe Image Segmenter** (WASM+WebGL). The raw camera stream gets processed frame-by-frame on a canvas — person stays sharp, background gets Gaussian-blurred — and the *processed* stream is what gets sent to peers. Preference persists per-user via the existing `UserSettings` key-value store so any future call auto-enables blur.

**Requested by:** Wife wants background blur option in video chat.  
**Key requirements:**
- Toggle to enable/disable background blur
- Remembered per-user — any video chat they join will have blur if toggle is on
- Processing happens client-side before video data is sent (no server-side processing)

---

## Architecture

### Processing Pipeline (all client-side in JS)

1. Raw camera stream from `getUserMedia` → hidden `<video>` element
2. `requestVideoFrameCallback` loop draws each frame to an offscreen `<canvas>`
3. MediaPipe Image Segmenter produces per-pixel person/background mask
4. Compositing canvas: draw original frame, apply blur via `ctx.filter = 'blur(10px)'` to background region using mask
5. `canvas.captureStream(30)` → new `MediaStream` with processed video track
6. Replace raw camera track in all `RTCPeerConnection`s with processed track (using existing `replaceTrackInAllPeers()`)
7. On disable: swap back to raw camera track

### User Preference Storage

Stored via existing `IUserSettingsService`:
- **Module:** `"dotnetcloud.video"`
- **Key:** `"background-blur-enabled"`
- **Value:** `"true"` / `"false"`

---

## Implementation Steps

### Phase 1: MediaPipe Vendoring & JS Effects Engine

#### Step 1.1 — Vendor MediaPipe tasks-vision

- Download `@mediapipe/tasks-vision` WASM bundle + JS from npm
- Place in `src/UI/DotNetCloud.UI.Web/wwwroot/lib/mediapipe/`
  - `vision_bundle.mjs` (or `vision_bundle.js`)
  - `vision_wasm_internal.js`, `vision_wasm_internal.wasm`
  - `wasm/` subdirectory with WASM files
- Download the selfie segmentation model: `selfie_segmenter_landscape.tflite` (~300KB)
- Place model in `src/UI/DotNetCloud.UI.Web/wwwroot/lib/mediapipe/models/`
- Add `<script>` tag in `App.razor` (after existing scripts, before video-call.js)

#### Step 1.2 — Create `video-effects.js`

- New file: `src/UI/DotNetCloud.UI.Web/wwwroot/js/video-effects.js`
- Expose `window.dotnetcloudVideoEffects` namespace with:
  - `initialize()` — load MediaPipe Image Segmenter model, create offscreen canvases
  - `enableBackgroundBlur(rawStream)` — start processing loop:
    - Create hidden `<video>` element fed by raw camera stream
    - Set up `requestVideoFrameCallback` / `requestAnimationFrame` loop
    - Each frame: `segmenter.segment()` → get mask → composite with blur → output canvas
    - Return `canvas.captureStream(30)` track
  - `disableBackgroundBlur()` — stop processing loop, release canvases
  - `getProcessedTrack()` — return current processed video track (or null if not active)
  - `isSupported()` — check for WebGL2 + WASM support
  - `dispose()` — cleanup all resources
- Add `<script>` tag in `App.razor`

#### Step 1.3 — Integrate into `video-call.js`

- Add `enableBackgroundBlur()` function:
  - Store raw camera video track reference
  - Call `dotnetcloudVideoEffects.enableBackgroundBlur()`
  - Get processed track
  - Call existing `replaceTrackInAllPeers(rawTrack, processedTrack)` to swap in processed track
  - Update local video element to show processed stream (so user sees their own blur)
- Add `disableBackgroundBlur()` function:
  - Call `dotnetcloudVideoEffects.disableBackgroundBlur()`
  - Swap raw camera track back via `replaceTrackInAllPeers(processedTrack, rawTrack)`
  - Update local video element back to raw stream
- Modify `startLocalMedia()`: accept optional `enableBlur` parameter; if true, auto-enable blur after getting camera stream
- Modify `hangup()` / cleanup: ensure `dotnetcloudVideoEffects.dispose()` is called

---

### Phase 2: C# Interop & Preference Wiring

#### Step 2.1 — Extend `IWebRtcInteropService`

- Add method: `Task SetBackgroundBlurAsync(bool enabled)`
- Add method: `Task<bool> IsBackgroundBlurSupportedAsync()`

#### Step 2.2 — Extend `WebRtcInteropService`

- Implement both methods as JS interop calls to `dotnetcloudVideoCall.enableBackgroundBlur()` / `disableBackgroundBlur()` and `dotnetcloudVideoEffects.isSupported()`

#### Step 2.3 — Preference Loading

- In the component that initiates calls (wherever `StartLocalMediaAsync` is called), inject `IUserSettingsService`
- On call join: read setting `("dotnetcloud.video", "background-blur-enabled")`
- If `"true"`, pass `enableBlur: true` to `StartLocalMediaAsync` (or call `SetBackgroundBlurAsync(true)` right after)
- On toggle change: upsert setting via API

---

### Phase 3: UI Components

#### Step 3.1 — Add blur toggle to `CallControls.razor`

- Add new button between camera toggle and screen share button
- Icon: person silhouette with blur effect (e.g., `bi-person-bounding-box` or custom SVG)
- Visual state: highlighted when blur is active, dimmed when off
- Tooltip: "Background Blur"
- New parameters: `IsBackgroundBlurred` (bool), `IsBlurSupported` (bool), `OnToggleBackgroundBlur` (EventCallback)
- Hide/disable button if `IsBlurSupported` is false

#### Step 3.2 — Wire up in `VideoCallDialog`

- Add `IsBackgroundBlurred` and `IsBlurSupported` parameters
- Pass through to `CallControls`
- Add `OnToggleBackgroundBlur` EventCallback parameter, forwarded to parent

#### Step 3.3 — Wire up in parent call orchestration

- The component managing the call lifecycle (wherever `IWebRtcInteropService` is invoked) needs to:
  - Load blur preference on mount
  - Handle the toggle callback: call `SetBackgroundBlurAsync()`, save preference
  - Track `isBackgroundBlurred` state
  - Pass state down to `VideoCallDialog`

#### Step 3.4 — CSS for blur toggle button

- Add styles in `CallControls.razor.css` for the blur button
- Active state (blur on): highlighted/accent color
- Inactive state: same style as other control buttons
- Disabled state if not supported

---

### Phase 4: Testing

#### Step 4.1 — C# Unit Tests

- `WebRtcInteropService` tests for new methods (input validation, JS interop call verification)
- Test that preference is read on call join and written on toggle

#### Step 4.2 — Component Tests

- `CallControls` renders blur button when supported
- `CallControls` hides blur button when not supported
- Toggle callback fires correctly

#### Step 4.3 — JS Tests (if test infrastructure exists)

- `video-effects.js` initialize/dispose lifecycle
- `isSupported()` returns correct value based on browser capabilities

---

## Files to Modify

| File | Changes |
|------|---------|
| `src/UI/DotNetCloud.UI.Web/wwwroot/js/video-call.js` | Add `enableBackgroundBlur()` / `disableBackgroundBlur()`, modify `startLocalMedia()` and `hangup()` |
| `src/UI/DotNetCloud.UI.Web/Components/App.razor` | Add `<script>` tags for MediaPipe + video-effects.js |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/IWebRtcInteropService.cs` | Add `SetBackgroundBlurAsync`, `IsBackgroundBlurSupportedAsync` |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/WebRtcInteropService.cs` | Implement new interop methods |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/CallControls.razor` | Add blur toggle button |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/CallControls.razor.cs` | Add parameters and handler |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/CallControls.razor.css` | Blur button styles |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/VideoCallDialog.razor` | Pass blur props to CallControls |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/VideoCallDialog.razor.cs` | Add blur parameters/callbacks |

## Files to Create

| File | Purpose |
|------|---------|
| `src/UI/DotNetCloud.UI.Web/wwwroot/js/video-effects.js` | MediaPipe segmentation + canvas compositing engine |
| `src/UI/DotNetCloud.UI.Web/wwwroot/lib/mediapipe/` | Vendored MediaPipe WASM + model files |

## Files Referenced (no changes)

| File | Purpose |
|------|---------|
| `src/Core/DotNetCloud.Core/Services/IUserSettingsService.cs` | Existing settings interface (module/key pattern) |
| `src/Core/DotNetCloud.Core.Server/Controllers/UserSettingsController.cs` | Existing `PUT api/v1/core/user-settings/{module}/{key}` |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/WebRtcDtos.cs` | Existing DTOs for reference |

---

## Design Decisions

| Decision | Rationale |
|----------|-----------|
| **MediaPipe tasks-vision** over TensorFlow.js BodyPix | Smaller, faster, actively maintained, purpose-built for segmentation |
| **Self-hosted/vendored** (no CDN) | Project convention — all JS is self-hosted |
| **Canvas compositing** approach | Most browser-compatible; Insertable Streams API too new for broad support |
| **`requestVideoFrameCallback`** with `requestAnimationFrame` fallback | Frame-accurate processing; Firefox only recently added rVFC |
| **Per-user global preference** (not per-channel) | Requirement: "any video chat they join will have the background blurred" |
| **Processed stream sent to peers** | Requirement: "blurring on the user side before it even sends the video data" |
| **Fixed 10px Gaussian blur** | Simple toggle for v1; intensity slider can come later |
| Setting: `"dotnetcloud.video"` / `"background-blur-enabled"` | Consistent with existing `(module, key)` patterns like `"dotnetcloud.music"` / `"volume"` |

---

## Verification Checklist

- ☐ `dotnet build` succeeds with all new C# code
- ☐ `dotnet test` — all existing tests pass + new tests for blur interop and component rendering
- ☐ Manual browser test: blur toggle button appears in call controls
- ☐ Click toggle → local video preview shows blurred background
- ☐ Remote peer receives blurred video stream
- ☐ Preference persistence: enable blur → leave call → join new call → blur auto-enables
- ☐ Disable test: toggle blur off → preference saved → next call starts without blur
- ☐ Unsupported browser: blur button hidden/disabled, no errors
- ☐ Performance: blur active on mid-range machine → video still smooth at ~20+ fps
- ☐ Cleanup: end call with blur active → all canvases/streams properly disposed, no memory leaks

---

## Future Enhancements (Out of Scope)

1. **Blur intensity slider** — Allow user to adjust blur strength (5px–25px). Same pipeline, just a configurable parameter.
2. **Virtual backgrounds** — The same segmentation mask enables replacing the background with a custom image. Trivial addition once this ships.
3. **Background replacement presets** — Built-in background images (office, nature, solid colors).
4. **Model quality selector** — MediaPipe offers different model sizes (landscape vs. general). Could let users choose quality vs. performance tradeoff.
