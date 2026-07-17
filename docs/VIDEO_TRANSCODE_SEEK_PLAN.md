# Video Transcode Seek — Full-Duration Slider with Priority Seek

**Date:** 2026-07-16  
**Branch:** `fix/blazor-video-module`  
**Status:** ☐ In progress — JS implemented, DurationTicks fix applied, ready for deploy & test

---

## 🔴 CRITICAL: Session Findings (2026-07-16)

### What's Deployed & Working

- ✅ Backend `POST /stream/seek` endpoint (cancels old transcode, starts new from seek position)
- ✅ `IVideoTranscodingService.TranscodeHlsAsync` now accepts `seekStart: TimeSpan?` parameter
- ✅ `seekStart` flows through to `BuildHlsArgs` → ffmpeg `-ss` flag (already supported)
- ✅ `SeekTranscodeDto` DTO class
- ✅ Razor renders seek bar DOM when `_streamStrategy != "direct"`:
  - `#transcode-seek-bar` (with `data-max-duration` attribute)
  - `#transcode-seek-track`, `#transcode-seek-fill`, `#transcode-seek-thumb`
  - `#transcode-seek-hint` text
- ✅ CSS for seek bar (track/fill/thumb/hint)
- ✅ C# fields: `_seekBarPosition`, `_seekInProgress`
- ✅ `OnStreamStrategy` resets `_seekBarPosition = 0` AND calls `initSeekSlider`
- ✅ `_playerVideo` declared as `VideoDto?` (nullable)

### 🆕 Changes (2026-07-16 Session)

- ✅ JS: `initSeekSlider()` — custom div-based drag slider with retry loop for Blazor render
- ✅ JS: `seekTranscode()` — buffered-range check → API call + HLS re-init
- ✅ JS: `formatTime()` helper
- ✅ JS: `data-stream-url` attribute stored on video element for seek re-init
- ✅ JS: Spinner animation uses correct `dnc-spin` keyframe name
- ✅ C#: `OnStreamStrategy` fires `initSeekSlider` after strategy is known
- ✅ `VideoThumbnailService.ExtractMetadataAsync` now populates `DurationTicks` from ffprobe `format.duration`

### 🔴 Root Cause: Database `DurationTicks = 0`

```sql
-- Rick.and.Morty.S09E06:
SELECT DurationTicks FROM video.canonical_videos WHERE Title LIKE '%Rick%Morty%S09E06%';
-- Result: 0
```

The video scan is NOT populating `DurationTicks` from ffprobe metadata. This means:

- `_playerVideo.Duration.TotalSeconds` = 0
- `data-max-duration="0"` in rendered HTML
- `maxDuration` in JS = 0 → seek doesn't work (pos = (pct/100) \* 0 = 0)

**Fix needed:** The video import/scan pipeline must extract duration from ffprobe and store it in `canonical_videos.DurationTicks`.

### 🔴 JS File Corruption

The `video-player.js` file was corrupted by repeated sed/replace operations and restored from git (`git checkout`). The JS functions `initSeekSlider`, `seekTranscode`, and `formatTime` need to be re-implemented cleanly.

### 🔴 Razor Null Reference (CS8602)

`_playerVideo` is `VideoDto?` but Razor doesn't do null-state analysis through `is not null` patterns. Several `_playerVideo.XXX` references need `!` operator. This was partially fixed but may need more instances.

### Lessons Learned

1. **Don't use `<input type="range">`** — browser native range input has cross-browser styling/event issues. Use custom `<div>`-based slider (track + fill + thumb) with JS drag handling.
2. **Don't create DOM in JS** — let Blazor/Razor render the DOM elements. JS should only attach event listeners.
3. **Always verify DB data** — the `data-max-duration` attribute was correct in the DLL but the underlying data was 0.
4. **JS must be copied to 3 locations after deploy** — the deploy script doesn't always refresh static web assets: `/opt/dotnetcloud/server/modules/dotnetcloud.video/wwwroot/_content/DotNetCloud.Modules.Video/video-player.js`, `/opt/dotnetcloud/server/wwwroot/_content/DotNetCloud.Modules.Video/video-player.js`, `/opt/dotnetcloud/server/wwwroot/video-player.js`
5. **`video.duration` for progressive streams is unreliable** — initially `Infinity`/`NaN`, updates when moov atom arrives. Use `durationchange` event to capture the real value.
6. **`Restart=on-failure` in systemd** — after deploy, service may stop cleanly (exit 0) and not auto-restart. Must `sudo systemctl start dotnetcloud` manually.

---

## 📋 Next Steps (Priority Order)

### P1: Fix DurationTicks in Database (Video Scan Pipeline) ✅

**Fixed:** `VideoThumbnailService.ExtractMetadataAsync` now parses `format.duration` from ffprobe JSON and stores it in `CanonicalVideo.DurationTicks`.

- **File:** `src/Modules/Video/DotNetCloud.Modules.Video.Data/Services/VideoThumbnailService.cs`
- **Note:** Existing videos need a library re-scan to populate `DurationTicks`. New scans will have it automatically.

### P2: Re-implement video-player.js Cleanly ✅

Three functions added to `video-player.js`:

1. **`initSeekSlider(dotNetRef, videoId, fullDuration)`** — attaches drag events to Blazor-rendered div elements, retries up to 30 times (3s) for DOM to appear, prevents double-init via `_seekInit` guard, reads `data-max-duration` with fallback to `video.duration` via `durationchange`, updates fill/thumb on drag, calls `seekTranscode` on release, updates fill on `timeupdate` when not dragging.

2. **`seekTranscode(elementId, targetSeconds, videoId, dotNetRef)`** — checks buffered range, normal seek if within, otherwise POST `/stream/seek` + destroy/re-init HLS + show/hide overlay.

3. **`formatTime(seconds)`** — formats as H:MM:SS or M:SS.

**Call flow:** `OnStreamStrategy` (C#) → `initSeekSlider` (JS) via `InvokeAsync` fire-and-forget.

### P3: Fix Remaining Razor Null Warnings ✅

Build passes with 0 warnings. `_playerVideo!` null-forgiveness already applied where needed.

### P4: Deployed ✅

- Build: 0 warnings, 0 errors
- Tests: 147 passed, 0 failed
- Deploy: All 14 modules healthy
- JS copied to all 3 locations
- DLL hashes verified

## Overview

When a video requires transcoding (HLS), the native browser position slider only shows the duration of segments generated so far. This plan adds a **custom seek bar** that spans the **full video duration** from the moment playback starts. When the user seeks beyond what's been transcoded, the backend restarts ffmpeg from the selected position so playback can resume immediately.

---

## Files to Modify

| File                                                                                   | What Changes                                        |
| -------------------------------------------------------------------------------------- | --------------------------------------------------- |
| `src/Modules/Video/DotNetCloud.Modules.Video.Host/Controllers/VideoController.cs`      | Add `POST /stream/seek` endpoint                    |
| `src/Modules/Video/DotNetCloud.Modules.Video/Services/IVideoTranscodingService.cs`     | Add `TranscodeHlsAsync` overload with `seekStart`   |
| `src/Modules/Video/DotNetCloud.Modules.Video.Host/Services/VideoTranscodingService.cs` | Implement seek-aware HLS transcode restart          |
| `src/Modules/Video/DotNetCloud.Modules.Video/UI/VideoPage.razor`                       | Add custom seek slider markup (transcode mode only) |
| `src/Modules/Video/DotNetCloud.Modules.Video/UI/VideoPage.razor.cs`                    | Wire slider → JS interop + new fields               |
| `src/Modules/Video/DotNetCloud.Modules.Video/UI/VideoPage.razor.css`                   | Style custom seek bar                               |
| `src/Modules/Video/DotNetCloud.Modules.Video/wwwroot/video-player.js`                  | Add `seekTranscode()` function                      |

---

## Phase 1: Backend — Seek-Transcode API

### Step 1.1: Add endpoint to `VideoController.cs`

**File:** `src/Modules/Video/DotNetCloud.Modules.Video.Host/Controllers/VideoController.cs`

Add a new endpoint after the `GetStreamProgress` method (around line ~460). The exact insertion point is after the closing brace of `GetStreamProgress` and before `ProbeStream`.

```csharp
/// <summary>
/// Seeks an active HLS transcode to a new position.
/// Cancels the current transcode, cleans up old segments, and starts
/// a new transcode from the requested position. The client should
/// reload the HLS stream after this returns successfully.
/// </summary>
[HttpPost("{videoId:guid}/stream/seek")]
public async Task<IActionResult> SeekTranscode(
    Guid videoId,
    [FromBody] SeekTranscodeDto dto)
{
    var caller = GetAuthenticatedCaller();

    // Validate position
    if (dto.PositionSeconds < 0)
        return BadRequest(ErrorEnvelope("invalid_position", "Position must be non-negative."));

    var seekStart = TimeSpan.FromSeconds(dto.PositionSeconds);

    // Look up the video to get the file path
    var video = await _videoService.GetVideoAsync(videoId, caller);
    if (video is null)
        return NotFound(ErrorEnvelope(ErrorCodes.VideoNotFound, "Video not found."));

    // Cancel any existing transcode for this video+user
    _transcodingService.CancelTranscode(videoId, caller.UserId);

    // Clean up old HLS output directory if it exists
    var hlsRootDir = Path.Combine(Path.GetTempPath(), "dotnetcloud-hls");
    var oldDirPattern = $"hls-{videoId:N}-{caller.UserId:N}";
    if (Directory.Exists(hlsRootDir))
    {
        foreach (var dir in Directory.GetDirectories(hlsRootDir, oldDirPattern + "*"))
        {
            try { Directory.Delete(dir, recursive: true); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean old HLS dir for seek: {Dir}", dir);
            }
        }
    }

    // Reconstruct file path (same pattern as StreamVideo)
    var (filePath, _) = await SaveVideoToTempFile(video, caller);
    if (filePath is null)
        return NotFound(ErrorEnvelope("file_not_found", "Video file not found in storage."));

    try
    {
        // Start new HLS transcode from the seek position
        var (jobId, outputDir, playlistPath) = await _transcodingService.TranscodeHlsAsync(
            videoId,
            caller.UserId,
            filePath,
            video.MimeType,
            seekStart: seekStart,
            ct: HttpContext.RequestAborted);

        _logger.LogInformation(
            "SeekTranscode: Started new transcode job {JobId} for video {VideoId} at position {Position}s",
            jobId, videoId, dto.PositionSeconds);

        // Wait for the playlist + at least 2 segments to be ready
        // (same pattern as StreamVideo — uses FileSystemWatcher)
        var waitResult = await WaitForHlsReadyAsync(
            playlistPath, outputDir, jobId, HttpContext.RequestAborted);

        if (waitResult == HlsWaitResult.Ready)
        {
            return Ok(Envelope(new { ready = true, jobId }));
        }

        return StatusCode(504, ErrorEnvelope("TRANSCODE_TIMEOUT",
            "HLS transcode did not produce segments within 30 seconds."));
    }
    finally
    {
        TryDeleteTempFile(filePath);
    }
}
```

**DTO class to add** (in the same file, at the bottom inside the namespace, or in a new file `Controllers/SeekTranscodeDto.cs`):

```csharp
/// <summary>
/// Request DTO for seeking an active HLS transcode to a new position.
/// </summary>
public sealed class SeekTranscodeDto
{
    /// <summary>The target position in seconds (may have decimal precision).</summary>
    public double PositionSeconds { get; set; }
}
```

### Step 1.2: Add `seekStart` parameter to `IVideoTranscodingService.TranscodeHlsAsync`

**File:** `src/Modules/Video/DotNetCloud.Modules.Video/Services/IVideoTranscodingService.cs`

The existing `TranscodeHlsAsync` signature is (around line ~115):

```csharp
Task<(string JobId, string OutputDir, string PlaylistPath)> TranscodeHlsAsync(
    Guid videoId,
    Guid userId,
    string sourceFilePath,
    string mimeType,
    string? sourceVideoCodec = null,
    string? sourceAudioCodec = null,
    CancellationToken ct = default);
```

Add `seekStart` parameter:

```csharp
Task<(string JobId, string OutputDir, string PlaylistPath)> TranscodeHlsAsync(
    Guid videoId,
    Guid userId,
    string sourceFilePath,
    string mimeType,
    string? sourceVideoCodec = null,
    string? sourceAudioCodec = null,
    TimeSpan? seekStart = null,
    CancellationToken ct = default);
```

### Step 1.3: Update `VideoTranscodingService.TranscodeHlsAsync` implementation

**File:** `src/Modules/Video/DotNetCloud.Modules.Video.Host/Services/VideoTranscodingService.cs`

Update the `TranscodeHlsAsync` method signature to match the interface (add `TimeSpan? seekStart = null`).

Then find where `LaunchFfmpegAsync` is called inside `TranscodeHlsAsync` (around line ~440) and pass `seekStart` through. Look for:

```csharp
await LaunchFfmpegAsync(activeJob, sourceFilePath, actualOutputDir, actualPlaylistPath, sourceVideoCodec, sourceAudioCodec, ct);
```

Also in `CreateHlsJobUnlocked`:

```csharp
await LaunchFfmpegAsync(job, sourceFilePath, actualOutputDir, actualPlaylistPath, sourceVideoCodec, sourceAudioCodec, ct);
```

Now find the `LaunchFfmpegAsync` private method and add `seekStart`:

**Current signature** (search for `private async Task LaunchFfmpegAsync`):

```csharp
private async Task LaunchFfmpegAsync(
    TranscodingJob job,
    string sourceFilePath,
    string outputDir,
    string playlistPath,
    string? sourceVideoCodec,
    string? sourceAudioCodec,
    CancellationToken ct)
```

**New signature:**

```csharp
private async Task LaunchFfmpegAsync(
    TranscodingJob job,
    string sourceFilePath,
    string outputDir,
    string playlistPath,
    string? sourceVideoCodec,
    string? sourceAudioCodec,
    TimeSpan? seekStart = null,
    CancellationToken ct = default)
```

Inside `LaunchFfmpegAsync`, find where `_argBuilder.BuildHlsArgs` is called and pass `seekStart`:

```csharp
var args = _argBuilder.BuildHlsArgs(
    sourceFilePath,
    outputDir,
    _options,
    sourceVideoCodec,
    sourceAudioCodec,
    seekStart: seekStart);  // <-- ADD THIS
```

**Note:** `BuildHlsArgs` already supports `seekStart` and `seekDuration`. The `-copyts` flag preserves original timestamps, so seeking via `-ss` will produce segments whose PTS starts at the seeked position. This means the time display will be correct without any special handling.

### Step 1.4: Ensure `SaveVideoToTempFile` is accessible

The `SeekTranscode` endpoint needs to reconstruct the temp file from chunks. Check that `SaveVideoToTempFile` (used in `StreamVideo`) is a private method in `VideoController`. If so, you may need to refactor it to be usable from `SeekTranscode`:

- **Option A:** Copy the relevant code from `StreamVideo` (lines ~560-700 that handle file reconstruction)
- **Option B:** Extract the file reconstruction into a shared private method `SaveVideoToTempFile(Guid videoId, CallerContext caller)` that both `StreamVideo` and `SeekTranscode` can call

The key pieces needed:

1. `_downloadService.DownloadCurrentAsync(video.FileNodeId, caller)` to get the stream
2. Writing it to a temp file at `Path.Combine(Path.GetTempPath(), "dotnetcloud-stream-source", $"source-{videoId:N}")`
3. Returning the path

Also need `TryDeleteTempFile(string path)` to clean up. If these don't exist as shared methods, extract them.

---

## Phase 2: Frontend — Custom Seek Bar

### Step 2.1: Add markup to `VideoPage.razor`

**File:** `src/Modules/Video/DotNetCloud.Modules.Video/UI/VideoPage.razor`

In the player section (after `@if (_playerOpen && _playerVideo is not null)`), add a custom seek bar INSIDE `#player-container`, BEFORE the `<video>` element.

Find this section (around line ~1262):

```razor
@if (_playerOpen && _playerVideo is not null)
{
    <div class="player-view">
        <div id="player-container" class="player-container">
            <video id="video-player"
```

Add the custom seek bar between `<div id="player-container" class="player-container">` and `<video id="video-player"`:

```razor
<div id="player-container" class="player-container">
    @if (_streamStrategy == "transcode" && _playerVideo is not null)
    {
        <div class="transcode-seek-bar">
            <input type="range"
                   class="transcode-seek-slider"
                   min="0"
                   max="@_playerVideo.Duration.TotalSeconds"
                   step="0.1"
                   value="@_seekBarPosition"
                   @oninput="OnSeekBarInput"
                   @onchange="OnSeekBarChanged"
                   title="Seek to position (transcoding in progress)" />
            <div class="transcode-seek-hint">Transcoding in progress — drag to seek anywhere</div>
        </div>
    }
    <video id="video-player"
```

### Step 2.2: Add C# fields and handlers to `VideoPage.razor.cs`

**File:** `src/Modules/Video/DotNetCloud.Modules.Video/UI/VideoPage.razor.cs`

**Add new fields** near the other player state fields (around line ~90, near `_streamStrategy`):

```csharp
// ── Transcode seek bar ──
private double _seekBarPosition;
private bool _seekInProgress;
```

**Add handler methods** (add after the `OnNoAudio` method, around line ~340):

```csharp
/// <summary>
/// Called on every input event while the user drags the transcode seek slider.
/// Updates the displayed position without triggering a seek-transcode.
/// </summary>
private void OnSeekBarInput(ChangeEventArgs e)
{
    if (e.Value is string s && double.TryParse(s, out var pos))
    {
        _seekBarPosition = pos;
    }
}

/// <summary>
/// Called when the user releases the transcode seek slider (onchange).
/// Triggers the seek-transcode flow if the target is beyond buffered range.
/// </summary>
private async Task OnSeekBarChanged(ChangeEventArgs e)
{
    if (_seekInProgress) return;
    if (e.Value is not string s || !double.TryParse(s, out var targetSeconds)) return;

    _seekBarPosition = targetSeconds;
    _seekInProgress = true;
    StateHasChanged();

    try
    {
        await Js.InvokeVoidAsync("DotNetCloudVideo.seekTranscode",
            "video-player",
            targetSeconds,
            _playerVideo!.Id.ToString(),
            _dotNetRef);
    }
    catch (Exception ex)
    {
        Logger.LogWarning(ex, "Seek-transcode failed for video {VideoId}", _playerVideo?.Id);
    }
    finally
    {
        _seekInProgress = false;
    }
}

/// <summary>
/// Called from JS when the transcode seek completes and the stream is ready.
/// Updates the seek bar position to match the new playback position.
/// </summary>
[JSInvokable]
public void OnTranscodeSeekComplete(double positionSeconds)
{
    _seekBarPosition = positionSeconds;
    InvokeAsync(StateHasChanged);
}
```

**Update `OpenVideoDetailAsync`** to initialize `_seekBarPosition`:

Find the method (around line ~660) and add after `_streamStrategy = null;`:

```csharp
_seekBarPosition = 0;
```

**Update `ClosePlayer`** to reset the seek bar state:

Find the method and add with the other resets:

```csharp
_seekBarPosition = 0;
_seekInProgress = false;
```

**Update the `OnStreamStrategy` JSInvokable** to reset seek bar when strategy changes:

```csharp
[JSInvokable]
public void OnStreamStrategy(string strategy)
{
    _streamStrategy = strategy;
    _seekBarPosition = 0;
    InvokeAsync(StateHasChanged);
}
```

### Step 2.3: Add CSS for custom seek bar

**File:** `src/Modules/Video/DotNetCloud.Modules.Video/UI/VideoPage.razor.css`

Add at the end of the file:

```css
/* ── Transcode Seek Bar (shown above video during HLS transcode) ── */
.transcode-seek-bar {
  position: absolute;
  bottom: 40px; /* sits above native controls */
  left: 0;
  right: 0;
  z-index: 10;
  padding: 0 12px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
  pointer-events: auto;
}

.transcode-seek-slider {
  width: 100%;
  height: 6px;
  -webkit-appearance: none;
  appearance: none;
  background: rgba(255, 255, 255, 0.15);
  border-radius: 3px;
  outline: none;
  cursor: pointer;
}

.transcode-seek-slider::-webkit-slider-thumb {
  -webkit-appearance: none;
  appearance: none;
  width: 14px;
  height: 14px;
  border-radius: 50%;
  background: #3b82f6;
  cursor: pointer;
  border: 2px solid #fff;
}

.transcode-seek-slider::-moz-range-thumb {
  width: 14px;
  height: 14px;
  border-radius: 50%;
  background: #3b82f6;
  cursor: pointer;
  border: 2px solid #fff;
}

.transcode-seek-hint {
  font-size: 11px;
  color: rgba(255, 255, 255, 0.5);
  pointer-events: none;
  white-space: nowrap;
}
```

### Step 2.4: Add `seekTranscode` function to `video-player.js`

**File:** `src/Modules/Video/DotNetCloud.Modules.Video/wwwroot/video-player.js`

Add the following function before the final `})();` at the end of the file (before line ~660 where `disposeProgressTracking` ends). Insert it after the `disposeProgressTracking` function:

```javascript
/**
 * Seeks the transcode to a new position. If the target is within the
 * already-transcoded (buffered) range, performs a normal seek. If the
 * target is beyond buffered range, calls the server to restart the
 * transcode from that position, then reloads the HLS stream.
 *
 * @param {string} elementId - The video element ID.
 * @param {number} targetSeconds - The target position in seconds.
 * @param {string} videoId - The video GUID.
 * @param {object} dotNetRef - .NET reference for callbacks.
 */
videoPlayer.seekTranscode = function (
  elementId,
  targetSeconds,
  videoId,
  dotNetRef,
) {
  var video = document.getElementById(elementId);
  if (!video) return;

  // Check if target is within already-buffered range
  var bufferedEnd = 0;
  if (video.buffered && video.buffered.length > 0) {
    bufferedEnd = video.buffered.end(video.buffered.length - 1);
  }

  if (targetSeconds <= bufferedEnd + 1) {
    // Within (or very near) buffered range — normal seek
    video.currentTime = targetSeconds;
    if (dotNetRef) {
      dotNetRef
        .invokeMethodAsync("OnTranscodeSeekComplete", targetSeconds)
        .catch(function () {});
    }
    return;
  }

  // Beyond buffered range — need to restart transcode from target position.
  // Show a brief overlay message.
  var overlay = document.createElement("div");
  overlay.id = "dnc-seek-overlay";
  overlay.innerHTML =
    '<div style="position:absolute;top:0;left:0;right:0;bottom:0;display:flex;align-items:center;justify-content:center;background:rgba(0,0,0,0.7);z-index:20;">' +
    '<div style="text-align:center;color:#fff;">' +
    '<div class="dnc-spinner" style="width:24px;height:24px;border:3px solid rgba(255,255,255,0.2);border-top-color:#3b82f6;border-radius:50%;margin:0 auto 12px;animation:dncSpin 0.8s linear infinite;"></div>' +
    '<p style="margin:0;font-size:14px;">Jumping to ' +
    formatTime(targetSeconds) +
    "&hellip;</p>" +
    "</div></div>";
  var container = document.getElementById("player-container");
  if (container) container.appendChild(overlay);

  // Call the seek-transcode API
  fetch("/api/v1/videos/" + videoId + "/stream/seek", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    credentials: "include",
    body: JSON.stringify({ positionSeconds: targetSeconds }),
  })
    .then(function (resp) {
      if (!resp.ok) {
        return resp.json().then(function (err) {
          throw new Error(
            err.message || "Seek failed (HTTP " + resp.status + ")",
          );
        });
      }
      return resp.json();
    })
    .then(function () {
      // Destroy old HLS instance
      if (video._hls) {
        video._hls.destroy();
        delete video._hls;
      }
      if (videoPlayer._hls) {
        delete videoPlayer._hls;
      }

      // Remove overlay
      if (overlay.parentNode) overlay.parentNode.removeChild(overlay);

      // Re-initialize HLS with the same stream URL.
      // The server has restarted the transcode from the seeked position,
      // so new segments start at that timestamp (thanks to -copyts).
      var streamUrl = video.src || video.querySelector("source")?.src;
      if (!streamUrl) {
        // Reconstruct URL from the current page
        streamUrl = "/api/v1/videos/" + videoId + "/stream?forceTranscode=true";
      }

      // Clear existing src to force a fresh load
      video.removeAttribute("src");
      video.load();

      // Set flag to prevent error listener from firing during HLS re-init
      videoPlayer._expectingHlsResponse = true;

      // Re-initialize HLS
      if (typeof Hls !== "undefined" && Hls.isSupported()) {
        if (!Hls.DefaultConfig._dncConfigured) {
          Hls.DefaultConfig.lowLatencyMode = false;
          Hls.DefaultConfig.backBufferLength = Infinity;
          Hls.DefaultConfig._dncConfigured = true;
        }
        var hls = new Hls({ manifestLoadingTimeOut: 20000 });
        hls.loadSource(streamUrl);
        hls.attachMedia(video);
        hls.on(Hls.Events.MANIFEST_PARSED, function () {
          video.play().catch(function () {});
          // The new segments have -copyts timestamps, so the video position
          // will naturally reflect the correct time. No need to seek.
        });
        hls.on(Hls.Events.ERROR, function (event, data) {
          if (data.fatal) {
            switch (data.type) {
              case Hls.ErrorTypes.NETWORK_ERROR:
                hls.startLoad();
                break;
              case Hls.ErrorTypes.MEDIA_ERROR:
                hls.recoverMediaError();
                break;
              default:
                hls.destroy();
                break;
            }
          }
        });
        video._hls = hls;
        videoPlayer._hls = hls;
      }

      if (dotNetRef) {
        dotNetRef
          .invokeMethodAsync("OnTranscodeSeekComplete", targetSeconds)
          .catch(function () {});
      }
    })
    .catch(function (err) {
      console.error("DNC: seek-transcode failed", err);
      if (overlay.parentNode) overlay.parentNode.removeChild(overlay);

      // Show error overlay
      var errOverlay = document.createElement("div");
      errOverlay.id = "dnc-seek-error";
      errOverlay.innerHTML =
        '<div style="position:absolute;top:0;left:0;right:0;bottom:0;display:flex;align-items:center;justify-content:center;background:rgba(0,0,0,0.8);z-index:20;">' +
        '<div style="text-align:center;color:#fff;max-width:400px;padding:24px;">' +
        '<p style="font-size:18px;margin:0 0 8px;">&#9888; Seek Failed</p>' +
        '<p style="font-size:13px;color:rgba(255,255,255,0.7);margin:0 0 16px;">' +
        (err.message || "Could not jump to the selected position.") +
        "</p>" +
        '<button onclick="document.getElementById(\'dnc-seek-error\').remove()" style="background:#3b82f6;color:#fff;border:none;padding:8px 20px;border-radius:6px;cursor:pointer;font-size:13px;">Dismiss</button>' +
        "</div></div>";
      if (container) container.appendChild(errOverlay);
    });
};

/**
 * Formats seconds as HH:MM:SS or MM:SS.
 * @param {number} seconds
 * @returns {string}
 */
function formatTime(seconds) {
  var h = Math.floor(seconds / 3600);
  var m = Math.floor((seconds % 3600) / 60);
  var s = Math.floor(seconds % 60);
  if (h > 0) {
    return (
      h + ":" + String(m).padStart(2, "0") + ":" + String(s).padStart(2, "0")
    );
  }
  return m + ":" + String(s).padStart(2, "0");
}
```

---

## Phase 3: Unit Tests

### Step 3.1: Test `SeekTranscode` endpoint

**File:** Create or update test file, likely at `tests/DotNetCloud.Modules.Video.Tests/Controllers/VideoControllerTests.cs`

```csharp
[Fact]
public async Task SeekTranscode_ValidPosition_CancelsOldJobAndStartsNew()
{
    // Arrange
    var videoId = Guid.NewGuid();
    var userId = Guid.NewGuid();
    var dto = new SeekTranscodeDto { PositionSeconds = 120.5 };

    // Mock: video exists, file can be reconstructed
    // Mock: WaitForHlsReadyAsync returns Ready

    // Act
    var result = await _controller.SeekTranscode(videoId, dto);

    // Assert
    var okResult = Assert.IsType<OkObjectResult>(result);
    // Verify CancelTranscode was called with (videoId, userId)
    // Verify TranscodeHlsAsync was called with seekStart = TimeSpan.FromSeconds(120.5)
}

[Fact]
public async Task SeekTranscode_NegativePosition_ReturnsBadRequest()
{
    // Arrange
    var dto = new SeekTranscodeDto { PositionSeconds = -5 };

    // Act
    var result = await _controller.SeekTranscode(Guid.NewGuid(), dto);

    // Assert
    Assert.IsType<BadRequestObjectResult>(result);
}

[Fact]
public async Task SeekTranscode_VideoNotFound_ReturnsNotFound()
{
    // Arrange — mock video service to return null
    // Act & Assert — NotFoundObjectResult
}
```

### Step 3.2: Test `BuildHlsArgs` with `seekStart`

Find existing tests for `FfmpegArgumentBuilder` and add:

```csharp
[Fact]
public void BuildHlsArgs_WithSeekStart_IncludesSsFlag()
{
    var builder = new FfmpegArgumentBuilder();
    var options = new VideoTranscodingOptions();

    var args = builder.BuildHlsArgs(
        "/path/to/video.mkv",
        "/tmp/output",
        options,
        seekStart: TimeSpan.FromSeconds(120));

    Assert.Contains("-ss 120.000", args);
}

[Fact]
public void BuildHlsArgs_WithoutSeekStart_NoSsFlag()
{
    var builder = new FfmpegArgumentBuilder();
    var options = new VideoTranscodingOptions();

    var args = builder.BuildHlsArgs(
        "/path/to/video.mkv",
        "/tmp/output",
        options);

    Assert.DoesNotContain("-ss ", args);
}
```

---

## Verification Checklist

- [ ] `dotnet build DotNetCloud.CI.slnf -c Release` succeeds
- [ ] `dotnet test tests/` — all existing tests pass, new tests pass
- [ ] No compiler warnings introduced
- [ ] Custom seek bar appears only when `_streamStrategy == "transcode"`
- [ ] Custom seek bar has correct `max` = video duration in seconds
- [ ] Dragging slider within buffered range: normal seek, no API call
- [ ] Dragging slider beyond buffered range: API call, overlay shown, HLS reloaded
- [ ] CSS styles look clean (slider above native controls, blue thumb, subtle hint text)
- [ ] Destroy/recreate hls.js on seek-transcode reinit (no memory leaks)
- [ ] `-copyts` ensures time display is correct after seek (shows actual position, not 0:00)

## Notes

- The `-copyts` flag in `BuildHlsArgs` is critical: without it, after seeking to 2:00, the time display would show 0:00. With `-copyts`, the first segment's PTS equals the seeked position.
- The custom seek bar is only for transcode mode. Direct-play and remux modes use the native browser controls which already show the full duration.
- The seek-transcode API is synchronous from the client's perspective — it waits for the new transcode to produce its first few segments before returning. This means the user sees a brief "Jumping…" overlay rather than an immediate response.
- `TryDeleteTempFile` and `SaveVideoToTempFile` may need to be extracted from `StreamVideo` into shared private methods if they don't already exist as such.
