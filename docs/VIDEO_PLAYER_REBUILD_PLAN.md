# Video Player Rebuild — Implementation Plan

> **Branch:** `fix/video-player`
> **Status:** Ready for implementation
> **Scope:** Replace the existing Blazor+JS hybrid video player with a single self-contained HTML5 player modeled on Jellyfin-web's `htmlVideoPlayer` plugin + `htmlMediaHelper` + video OSD. Adds playback-rate, next/previous-episode navigation, and alternate audio stream selection.

---

## 1. Goals

The rebuilt player must:

- Play video in Chromium and Firefox (and Safari where applicable) for direct-play MP4, remuxed MKV/AVI/etc., and HLS-transcoded files.
- Provide: play/pause, seek (buffered-aware), ±10s skip, fullscreen, subtitles (CC), volume/mute, playback rate, picture-in-picture, next/previous episode, and alternate audio track selection.
- Report watch progress (resume) and stream errors to the server.
- Be driven entirely by JavaScript (Blazor renders only a host container) to eliminate the Blazor re-render / `video.src` re-apply bug class documented in `docs/VIDEO_TRANSCODE_SEEK_PLAN.md`.

---

## 2. Reference material (already on disk — read before implementing)

### Jellyfin web player (`D:\Repos\jellyfin-web`)

| File                                                                 | What to take from it                                                                                                                                                                                                                       |
| -------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `src/plugins/htmlVideoPlayer/plugin.js`                              | Player class shape: `play`, `setCurrentSrc`, `setSrcWithHlsJs`, `createMediaElement`, subtitle/audio track selection, `seekable`, `pause/unpause`, `setVolume/setMute`, `setPlaybackRate/getSupportedPlaybackRates`, fullscreen/PiP hooks. |
| `src/components/htmlMediaHelper.js`                                  | Engine helpers to port: `enableHlsJsPlayer`, `handleHlsJsMediaError`, `seekOnPlaybackStart`, `playWithPromise`, `bindEventsToHlsPlayer`, `destroyHlsPlayer`, `getBufferedRanges`, `applySrc`/`resetSrc`.                                   |
| `src/apps/legacy/controllers/playback/video/index.html` + `index.js` | OSD layout and event wiring (title, start/end time, seek slider, play/pause, rewind/ff, skip, CC, audio, volume, fullscreen, PiP).                                                                                                         |
| `src/plugins/htmlVideoPlayer/style.scss`, `src/styles/videoosd.scss` | OSD CSS to adapt (scoped into the module's isolated CSS).                                                                                                                                                                                  |

### Jellyfin server (`D:\Repos\jellyfin`) — behavioral parity only (already mirrored in DotNetCloud)

| File                                                                     | Purpose                                           |
| ------------------------------------------------------------------------ | ------------------------------------------------- |
| `MediaBrowser.Controller/MediaEncoding/EncodingHelper.cs`                | Direct play / direct stream / transcode decision. |
| `src/Jellyfin.MediaEncoding.Hls/Playlist/DynamicHlsPlaylistGenerator.cs` | HLS playlist generation semantics.                |
| `MediaBrowser.MediaEncoding/Subtitles/SubtitleEncoder.cs`                | Subtitle → WebVTT conversion semantics.           |

---

## 3. Current code inventory (delete or replace)

| File                                                                  | Action                                               |
| --------------------------------------------------------------------- | ---------------------------------------------------- |
| `src/Modules/Video/DotNetCloud.Modules.Video/wwwroot/video-player.js` | **Delete entirely** and rewrite.                     |
| `src/Modules/Video/DotNetCloud.Modules.Video/UI/VideoPage.razor`      | Replace the player section (see §7.1).               |
| `src/Modules/Video/DotNetCloud.Modules.Video/UI/VideoPage.razor.cs`   | Replace the player wiring (see §7.2).                |
| `src/Modules/Video/DotNetCloud.Modules.Video/UI/VideoPage.razor.css`  | Remove old player styles, add OSD styles (see §7.3). |

Backend files touched (extensions only, no behavior change for existing single-audio flow):

| File                                                                                   | Change                                                                      |
| -------------------------------------------------------------------------------------- | --------------------------------------------------------------------------- |
| `src/Modules/Video/DotNetCloud.Modules.Video/Services/` (new type)                     | Add `AudioStreamInfo` record.                                               |
| `src/Modules/Video/DotNetCloud.Modules.Video/Services/FfmpegArgumentBuilder.cs`        | Audio index threading.                                                      |
| `src/Modules/Video/DotNetCloud.Modules.Video/Services/StreamCompatibilityMatrix.cs`    | Strategy decision with selected audio stream.                               |
| `src/Modules/Video/DotNetCloud.Modules.Video/Services/IVideoTranscodingService.cs`     | New `audioStreamIndex` params.                                              |
| `src/Modules/Video/DotNetCloud.Modules.Video.Host/Services/VideoTranscodingService.cs` | Enumerate audio streams; thread index.                                      |
| `src/Modules/Video/DotNetCloud.Modules.Video.Host/Controllers/VideoController.cs`      | `/stream`, `/stream/seek`, `/stream-probe` (+ optional `/streams`) changes. |
| `src/Core/DotNetCloud.Core/DTOs/VideoDtos.cs`                                          | Add `VideoAudioStreamDto`; optionally extend `VideoMetadataDto`.            |

---

## 4. API contract

### 4.1 Existing endpoints (unchanged behavior)

| Endpoint                                                                                           | Returns                                                                                                                                                                                                  |
| -------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `GET /api/v1/videos/{videoId}/stream?token=&forceTranscode=&startSeconds=`                         | Direct MP4 (Range supported, `X-Stream-Strategy: direct`), remux MP4 pipe (`X-Stream-Strategy: remux`, non-seekable), or HLS playlist (`application/vnd.apple.mpegurl`, `X-Stream-Strategy: transcode`). |
| `GET /api/v1/videos/{videoId}/stream-progress`                                                     | `{ stage, percent, message, strategy }`.                                                                                                                                                                 |
| `POST /api/v1/videos/{videoId}/stream/seek` body `{ positionSeconds }`                             | Restarts HLS transcode from position; returns when playlist + ≥2 segments ready.                                                                                                                         |
| `GET /api/v1/videos/{videoId}/stream/{segment.ts}` and `GET /api/v1/videos/{videoId}/{segment.ts}` | HLS segments (`.ts`, `.m4s`, `.mp4`, `.m3u8`).                                                                                                                                                           |
| `GET /api/v1/videos/{videoId}/subtitles/{subtitleId}/content`                                      | Subtitle text; Content-Type `text/vtt` for VTT, `text/plain` for SRT.                                                                                                                                    |
| `GET/PUT /api/v1/videos/{videoId}/progress` body `{ positionTicks }`                               | Resume position.                                                                                                                                                                                         |
| `GET /api/v1/videos/{videoId}/stream-probe`                                                        | `{ videoId, canDirectPlay, strategy, videoCodec, audioCodec, container, mimeType, streamUrl }`.                                                                                                          |

All endpoints use same-origin cookie auth (`Identity.Application`). The player must send `credentials: 'same-origin'` (default for fetch) on all requests.

### 4.2 New/changed endpoints

**`GET /api/v1/videos/{videoId}/streams` (new, recommended)** — returns audio streams for the audio menu without starting a pipeline:

```json
{
  "success": true,
  "data": {
    "videoId": "…",
    "audioStreams": [
      {
        "index": 0,
        "codec": "aac",
        "language": "eng",
        "title": "Stereo",
        "channels": 2,
        "isDefault": true
      },
      {
        "index": 1,
        "codec": "ac3",
        "language": "jpn",
        "title": null,
        "channels": 6,
        "isDefault": false
      }
    ]
  }
}
```

This is produced by running ffprobe on the (reconstructed) source file. If you prefer not to add a route, extend the existing `GET /api/v1/videos/{videoId}/stream-probe` to include `audioStreams` and call that instead. **Pick one and be consistent** — this plan uses the new `/streams` route.

**`GET /api/v1/videos/{videoId}/stream`** — add optional query param `audioStreamIndex` (int, default null → first stream). When set to a non-first stream, force the strategy to remux/transcode (never direct play) and select that audio stream.

**`POST /api/v1/videos/{videoId}/stream/seek`** — add optional body property `audioStreamIndex` (int, default null) so a seek after an audio switch preserves the selected stream.

---

## 5. Phase 1 — Backend: enumerate and select audio streams

### 5.1 New type `AudioStreamInfo`

File: `src/Modules/Video/DotNetCloud.Modules.Video/Services/AudioStreamInfo.cs` (new)

```csharp
namespace DotNetCloud.Modules.Video.Services;

/// <summary>An audio stream inside a video container, as reported by ffprobe.</summary>
public sealed record AudioStreamInfo
{
    public int Index { get; init; }
    public string? Codec { get; init; }
    public string? Language { get; init; }
    public string? Title { get; init; }
    public int? Channels { get; init; }
    public bool IsDefault { get; init; }
}
```

### 5.2 New DTO `VideoAudioStreamDto`

File: `src/Core/DotNetCloud.Core/DTOs/VideoDtos.cs` (add near `VideoMetadataDto`)

```csharp
/// <summary>An audio stream available for a video (for the audio-track selector).</summary>
public sealed record VideoAudioStreamDto
{
    public required int Index { get; init; }
    public string? Codec { get; init; }
    public string? Language { get; init; }
    public string? Title { get; init; }
    public int? Channels { get; init; }
    public bool IsDefault { get; init; }
}
```

Optionally add `IReadOnlyList<VideoAudioStreamDto> AudioStreams { get; init; } = [];` to `VideoMetadataDto` only if you choose the persist-at-index-time variant (see §9.3). The default variant does **not** persist and keeps `VideoMetadataDto` unchanged.

### 5.3 Enumerate streams in ffprobe parsing

File: `src/Modules/Video/DotNetCloud.Modules.Video.Host/Services/VideoTranscodingService.cs`

Replace `ParseCodecInfo(string json)` with a method that also returns audio streams:

```csharp
private static (string? VideoCodec, string? AudioCodec, string? Container, IReadOnlyList<AudioStreamInfo> AudioStreams)
    ParseCodecInfo(string json)
```

Parsing rules (inside the `streams` array loop):

- For each `codec_type == "audio"` stream, in order, build an `AudioStreamInfo` with:
  - `Index` = `stream["index"]` (int, fallback to positional counter).
  - `Codec` = `stream["codec_name"]` (string).
  - `Channels` = `stream["channels"]` (int, may be absent).
  - `Language` = `stream["tags"]["language"]` (string, may be absent).
  - `Title` = `stream["tags"]["title"]` (string, may be absent).
  - `IsDefault` = `stream["disposition"]["default"] == 1`.
- Keep the existing `videoCodec` (first video stream) and `audioCodec` (first audio stream) extraction for backward compatibility.

Add a public probe method to the interface (see §5.4) so the controller can call it directly:

```csharp
public async Task<(string? VideoCodec, string? AudioCodec, string? Container,
    IReadOnlyList<AudioStreamInfo> AudioStreams)> ProbeStreamsAsync(
    string videoFilePath, CancellationToken ct = default)
```

This runs `RunFfprobeAsync` once and returns `ParseCodecInfo(...)`. Refactor `DecideStreamingStrategyAsync` to call `ProbeStreamsAsync` internally (single ffprobe run per request, same as today).

### 5.4 Extend `IVideoTranscodingService`

File: `src/Modules/Video/DotNetCloud.Modules.Video/Services/IVideoTranscodingService.cs`

Add/change:

```csharp
// Add:
Task<(string? VideoCodec, string? AudioCodec, string? Container,
    IReadOnlyList<AudioStreamInfo> AudioStreams)> ProbeStreamsAsync(
    string videoFilePath, CancellationToken ct = default);

// Change — add int? audioStreamIndex = null as the last param:
Task<(Process Process, string Args)> StreamCopyAsync(
    string sourceFilePath,
    string? videoCodec,
    string? audioCodec,
    CancellationToken ct = default,
    TimeSpan? startTime = null,
    int? audioStreamIndex = null);

// Change — add int? audioStreamIndex = null:
Task<(string JobId, string OutputDir, string PlaylistPath)> TranscodeHlsAsync(
    Guid videoId,
    Guid userId,
    string sourceFilePath,
    string mimeType,
    string? sourceVideoCodec = null,
    string? sourceAudioCodec = null,
    TimeSpan? seekStart = null,
    CancellationToken ct = default,
    int? audioStreamIndex = null);
```

> `TranscodeAsync` (progressive MP4) is not used by the web player; leave it unchanged unless you choose to keep it symmetrical — not required.

### 5.5 Thread audio index through `FfmpegArgumentBuilder`

File: `src/Modules/Video/DotNetCloud.Modules.Video/Services/FfmpegArgumentBuilder.cs`

For each of `GetStreamCopyArgs`, `GetStreamCopyToFileArgs`, `BuildProgressiveMp4Args`, and `BuildHlsArgs`, add an optional `int? audioStreamIndex = null` parameter. Compute `var audioMap = audioStreamIndex is >= 0 ? $"-map 0:a:{audioStreamIndex}? " : "-map 0:a:0? ";` and use it in place of the hardcoded `-map 0:a:0?`.

The audio re-encode decision must use the **selected** stream's codec:

- `GetStreamCopyArgs` / `GetStreamCopyToFileArgs`: currently test `audioCodec` with `StreamCompatibilityMatrix.IsUniversalAudioCodec(audioCodec)`. That `audioCodec` argument is the first stream's codec from the existing call sites. When an explicit `audioStreamIndex` is passed, the controller must pass the **selected** stream's codec as `audioCodec`. Keep the method itself unchanged except for the `-map` string.
- `BuildHlsArgs`: same — the caller passes `sourceAudioCodec` for the selected stream.

### 5.6 Strategy decision with selected audio stream

File: `src/Modules/Video/DotNetCloud.Modules.Video/Services/StreamCompatibilityMatrix.cs`

No signature change required. In `VideoController.StreamVideo`, when `audioStreamIndex` is set and refers to a non-first stream, do **not** use the direct-play branch; force `StreamingStrategy.StreamCopy` (if the selected audio is universal) or `StreamingStrategy.Transcode` (if not). To decide that, use `StreamCompatibilityMatrix.IsUniversalAudioCodec(selectedCodec)`.

Concrete logic to add in `StreamVideo` after probing:

```csharp
StreamingStrategy strategy;
if (audioStreamIndex is >= 0)
{
    var selected = audioStreams.ElementAtOrDefault(audioStreamIndex.Value);
    var selectedCodec = selected?.Codec ?? audioCodec;
    strategy = StreamCompatibilityMatrix.IsUniversalAudioCodec(selectedCodec)
        ? StreamingStrategy.StreamCopy
        : StreamingStrategy.Transcode;
    audioCodec = selectedCodec; // used downstream for bitstream filter / audio copy
}
else
{
    strategy = /* existing DecideStreamingStrategyAsync result */;
}
```

> Note: `StreamCopy` of a container like MKV with an AAC second stream works today; the existing `GetStreamCopyArgs` already re-encodes non-universal audio to AAC. If the selected stream is non-universal (e.g. AC3/DTS), `Transcode` (HLS) is safer because the remux pipe path currently only re-encodes audio with a fixed AAC profile that has shown A/V-sync regressions (see comments in `FfmpegArgumentBuilder.GetStreamCopyArgs`). Using HLS for non-universal selected audio matches Jellyfin's behavior of transcoding when the audio stream is unsupported.

### 5.7 Controller changes

File: `src/Modules/Video/DotNetCloud.Modules.Video.Host/Controllers/VideoController.cs`

1. `StreamVideo`: add `[FromQuery] int? audioStreamIndex = null`; after the file is reconstructed and probed, apply the §5.6 logic; pass `audioStreamIndex` (and the selected audio codec) into the `StreamCopy`/`TranscodeHlsAsync` branches.

2. `SeekTranscode`: add `AudioStreamIndex` to `SeekTranscodeDto` (defined at the bottom of the same file):

```csharp
public sealed class SeekTranscodeDto
{
    public double PositionSeconds { get; set; }
    public int? AudioStreamIndex { get; set; }
}
```

Pass `audioStreamIndex` into `TranscodeHlsAsync`.

3. Add `GET /api/v1/videos/{videoId}/streams` (new action, `[AllowAnonymous]` is **not** required — it should be authenticated; reuse cookie auth like `ProbeStream`):

```csharp
[HttpGet("{videoId:guid}/streams")]
public async Task<IActionResult> GetAudioStreams(Guid videoId)
{
    // 1. GetAuthenticatedCaller()
    // 2. _videoService.GetVideoAsync(videoId, caller) → 404 if null
    // 3. SaveVideoToTempFile(video, caller) → 404 if no path
    // 4. _transcodingService.ProbeStreamsAsync(filePath)
    // 5. map AudioStreamInfo[] → VideoAudioStreamDto[] (omit Index? keep it)
    // 6. return Ok(Envelope(new { videoId, audioStreams }))
    // 7. finally TryDeleteTempFile(filePath)
}
```

Also extend `ProbeStream` to call `ProbeStreamsAsync` (so its existing behavior stays consistent).

### 5.8 No DB/migration changes

The default design enumerates audio streams from ffprobe at request time (the `/stream` endpoint already probes every request). No changes to `CanonicalVideoMetadata`, `VideoMetadataDto`, or EF migrations are required.

---

## 6. Phase 2 — Player JS module (rewrite `video-player.js`)

### 6.1 Overall shape

File: `src/Modules/Video/DotNetCloud.Modules.Video/wwwroot/video-player.js`

- Plain ES5 script (no modules, no build step), wrapped in an IIFE, exposing `window.DotNetCloudVideoPlayer`.
- Depends on `window.Hls` (from the already-bundled `hls.min.js`, loaded first).
- **Delete** the old `window.DotNetCloudVideo` namespace entirely.

Public API:

```js
window.DotNetCloudVideoPlayer.init(config); // builds DOM, attaches engine, starts playback
window.DotNetCloudVideoPlayer.destroy(); // full teardown (hls.js, listeners, DOM)
window.DotNetCloudVideoPlayer.setAudioStream(index); // optional convenience (or handled internally)
```

### 6.2 `config` object (passed from Blazor via `IJSRuntime.InvokeVoidAsync`)

```json
{
  "containerId": "video-player-root",
  "videoId": "00000000-0000-0000-0000-000000000000",
  "title": "Movie Title",
  "posterUrl": "/api/v1/videos/{id}/thumbnail",
  "streamUrl": "/api/v1/videos/{id}/stream",
  "durationSeconds": 7312.5,
  "resumeSeconds": 120.0,
  "subtitles": [
    { "id": "sub-id", "language": "en", "label": "English", "isDefault": true }
  ],
  "audioStreams": [
    {
      "index": 0,
      "codec": "aac",
      "language": "eng",
      "title": "Stereo",
      "isDefault": true
    }
  ],
  "defaultAudioIndex": 0,
  "dotNetRef": {}
}
```

`dotNetRef` is the `DotNetObjectReference<VideoPage>`; the player calls these JSInvokables:

- `OnError(code, message)` — fatal playback error (see §6.9).
- `OnStrategy(strategy)` — `"direct" | "remux" | "transcode"` once known.
- `OnNavigateEpisode(delta)` — `-1 | +1` for prev/next episode.
- `OnEnded()` — playback ended (for auto-advance and progress flush).

### 6.3 DOM structure the player creates inside `#video-player-root`

```
#video-player-root
├── .dnc-player                (position:relative; background:#000)
│   ├── video#dnc-video        (class="dnc-video"; playsinline; no controls)
│   ├── .dnc-spinner           (buffering indicator; hidden by default)
│   ├── .dnc-big-play          (center play button; shown when paused)
│   ├── .dnc-error             (error overlay + message; hidden by default)
│   └── .dnc-osd               (bottom overlay; auto-hides)
│       ├── .dnc-osd-title
│       ├── .dnc-seek (track)  ├── .dnc-seek-buffered ├── .dnc-seek-played ├── .dnc-seek-thumb
│       ├── .dnc-time          (elapsed / duration)
│       └── .dnc-controls
│           ├── button.dnc-btn-play
│           ├── button.dnc-btn-back10
│           ├── button.dnc-btn-fwd10
│           ├── button.dnc-btn-prev-episode
│           ├── button.dnc-btn-next-episode
│           ├── button.dnc-btn-cc        (subtitle menu)
│           ├── button.dnc-btn-audio     (audio menu)
│           ├── button.dnc-btn-rate      (playback-rate menu)
│           ├── .dnc-volume (mute btn + range)
│           ├── button.dnc-btn-pip
│           └── button.dnc-btn-fullscreen
```

All buttons use Material icon **ligature text** (the Material font is already loaded globally). Example: play/pause uses `"play_arrow"` / `"pause"`; CC uses `"closed_caption"`; audio uses `"audiotrack"`; rate uses `"speed"`; volume uses `"volume_up"`/`"volume_off"`; fullscreen uses `"fullscreen"`/`"fullscreen_exit"`; skip uses `"replay_10"`/`"forward_10"`; prev/next episode uses `"skip_previous"`/`"skip_next"`; PiP uses `"picture_in_picture_alt"`. This is a deliberate exception to the "all icons via `MaterialIcon` component" rule because the DOM is built in raw JS — document it in a code comment.

### 6.4 Engine selection (port of `enableHlsJsPlayer` + `setCurrentSrc`)

On `init`, call `showStreamProgress()` (§6.5) which resolves with the strategy. Then:

- `strategy === "direct" || "remux"` → set `video.src = streamUrl` and `video.play()` (swallow `NotAllowedError`/`AbortError` like Jellyfin's `playWithPromise`). `crossOrigin = "anonymous"` is NOT set (same-origin).
- `strategy === "transcode"` → HLS:
  - If `video.canPlayType("application/vnd.apple.mpegurl")` **and** NOT (Chromium/Firefox) → native HLS (`video.src = streamUrl`).
  - Otherwise, if `window.MediaSource && window.Hls` → use hls.js:
    - `Hls.DefaultConfig.lowLatencyMode = false; Hls.DefaultConfig.backBufferLength = Infinity;` (set once).
    - `var hls = new Hls({ manifestLoadingTimeOut: 20000, xhrSetup: xhr => { xhr.withCredentials = true; } });`
    - `hls.loadSource(streamUrl); hls.attachMedia(video);`
    - On `Hls.Events.MANIFEST_PARSED` → `video.play()`.
    - On `Hls.Events.ERROR` → port `bindEventsToHlsPlayer` + `handleHlsJsMediaError` recovery (NETWORK_ERROR → `startLoad`; MEDIA_ERROR → `recoverMediaError` then `swapAudioCodec` on retry; else destroy + `OnError`).
    - Store `this._hls = hls` on the player instance for `destroy()`.
  - Set the flag `_strategy = "transcode"` and report `OnStrategy("transcode")`.

Chromium/Firefox detection: simplest is `!!window.chrome || navigator.userAgent.includes("Firefox")` — but the correct port is `enableHlsJsPlayer`: use hls.js whenever `window.MediaSource` exists and the browser is not iOS Safari and not a TV. For this module, the rule "use hls.js on Chromium/Firefox/Edge, native on Safari/iOS" is sufficient.

### 6.5 Progress overlay

Port the existing `showStreamProgress` logic but render it inside the player (not via DOM injection after `#player-container`):

1. Immediately `fetch(streamUrl, { method: "GET", headers: { Range: "bytes=0-0" } })` **or** start polling `GET /api/v1/videos/{videoId}/stream-progress`.
2. **Recommended (matches current behavior):** kick off the stream by setting the engine as above, and in parallel poll `/stream-progress` every 500ms until `stage === "streaming"` (then read `strategy` and finalize engine) or `stage === "failed"` (then show error). Keep the 60s safety timeout that falls back to `strategy = "transcode"`.
3. Show a "Preparing stream…" message with a spinner and progress % during the wait.

### 6.6 Seek semantics (port of `seekable` + `getBufferedRanges` + `seekOnPlaybackStart`)

Maintain `_seekStartOffset` (seconds) for remux reloads so the OSD time is absolute.

- **Resume:** after `loadedmetadata`/`durationchange`/`loadeddata`/`play`, if `resumeSeconds > 0` and `video.duration >= resumeSeconds`, set `video.currentTime = resumeSeconds` (port `seekOnPlaybackStart`).
- **Slider seek (`seekTo(seconds)`):**
  - If strategy is `direct` or `transcode` (HLS): if `seconds <= bufferedEnd + 1`, set `video.currentTime = seconds`; else (HLS only) POST `/stream/seek` with `{ positionSeconds: seconds, audioStreamIndex }`, destroy old hls, re-init hls.js from the same `streamUrl`, and set `_seekStartOffset` appropriately.
  - If strategy is `remux` (non-seekable pipe): reload with `streamUrl + "&startSeconds=" + seconds` (cache-bust with `&_=Date.now()`), set `_seekStartOffset = seconds`, and `video.play()`.
- **Buffered fill:** on `progress`/`timeupdate`, draw `.dnc-seek-buffered` from `video.buffered` and `.dnc-seek-played` from `video.currentTime + _seekStartOffset`.

### 6.7 Subtitles (client-side, no backend change)

For each subtitle in `config.subtitles`:

1. `fetch("/api/v1/videos/" + videoId + "/subtitles/" + sub.id + "/content", { credentials: "same-origin" })`.
2. Read text. If it does not start with `WEBVTT`, convert SRT→VTT:
   - Prepend `WEBVTT\n\n`.
   - Replace timestamp separators `,` → `.` in every `HH:MM:SS,mmm --> HH:MM:SS,mmm` line (regex: `/(\d{2}:\d{2}:\d{2}),(\d{3})/g` → `$1.$2`).
3. `var url = URL.createObjectURL(new Blob([vtt], { type: "text/vtt" }))`.
4. Append `<track kind="subtitles" srclang=sub.language label=sub.label>` with `src = url`; set `track.default = sub.isDefault`.
5. CC menu: list tracks + "Off"; toggling sets `video.textTracks[i].mode = "showing" | "hidden"` (set all others to `"disabled"`). Track the mapping from menu item → `textTracks` index (textTracks excludes the metadata/other kinds; build the mapping after all tracks are appended).

Store blob URLs on the player instance and revoke them in `destroy()`.

### 6.8 Audio stream selection

- Audio menu lists `config.audioStreams` (label = `title || language || "Track " + (index+1)`), with the active one checked.
- On select (`index`):
  1. Save `position = video.currentTime + _seekStartOffset`.
  2. If `index === defaultAudioIndex` and strategy was direct: simplest is to **reload** anyway for consistency.
  3. Rebuild the stream URL: `streamUrl + (streamUrl.includes("?") ? "&" : "?") + "audioStreamIndex=" + index + "&startSeconds=" + position + "&_=" + Date.now()`.
  4. If HLS: POST `/stream/seek` with `{ positionSeconds: position, audioStreamIndex: index }`, destroy hls, re-init. (For simplicity, you may reuse the generic `reloadWithAudio(index)` that always reloads the stream URL and re-runs engine selection; the server will start the correct pipeline.)
  5. Update `_currentAudioIndex = index` and re-render the menu checkmark.
- Report nothing special to .NET (the server is stateless per stream URL).

### 6.9 Error handling

- Native `video.addEventListener("error")`: map `video.error.code` to a message (1 = aborted, 2 = network, 3 = decode, 4 = source not supported) and call `OnError(code, message)`.
- hls.js fatal error: destroy hls and call `OnError(2, data.details || "HLS error")`.
- On `OnError`, show the `.dnc-error` overlay with a friendly message. Port the codec-guidance text from the current C# `BuildCodecGuidance()` into JS (container/codec heuristics), driven by the metadata already available server-side if desired; simplest is a generic message + the specific codec string from the stream diagnostics if provided.

### 6.10 Watch progress + keyboard + OSD behavior

- Progress: every 60s of playback (`timeupdate` throttled) and on `pause`/`ended`, `PUT /api/v1/videos/{videoId}/progress` with `{ positionTicks: Math.round((currentTime + _seekStartOffset) * 10_000_000) }`.
- Keyboard (only when player focused/hovered and target is not an input): Space = play/pause; ←/→ = ±10s; F = fullscreen; M = mute; J/L = −10s/+10s; N/P = next/prev episode; Esc = exit fullscreen.
- OSD auto-hide after 3s idle (mouse move resets); click on video toggles play/pause; double-click toggles fullscreen.
- On `ended`: call `OnEnded()` (Blazor then decides auto-advance) and flush progress.

### 6.11 `destroy()`

- Revoke subtitle blob URLs; remove all `<track>`.
- `video.pause()`; `video.removeAttribute("src")`; `video.load()`.
- If `_hls`, `_hls.destroy()` and null it.
- Remove all event listeners and the created DOM; null `_dotNetRef`.

---

## 7. Phase 3 — Blazor host

### 7.1 `VideoPage.razor`

Replace the entire player section (currently starts at the `@* ─── Video Player ─── *@` comment and contains `player-view`, `player-container`, `<video>`, codec overlay, no-audio banner, `transcode-seek-bar`, and `player-info-panel`) with:

```razor
@if (_playerOpen && _playerVideo is not null)
{
    <div class="player-view">
        <div id="video-player-root" class="video-player-root"></div>
        @* keep the existing player-info-panel (title, actions, TMDB, metadata) unchanged *@
        <div class="player-info-panel">
            … (existing title/actions/metadata markup, minus the fullscreen button which moves into the OSD; the close button stays) …
        </div>
    </div>
}
```

Remove: the `<video>` element, the `<track>` loop, `codec-error-overlay`, `no-audio-banner`, `transcode-seek-bar`, and the fullscreen button in `player-actions` (the OSD owns fullscreen now). Keep the `Close` button and the other info-panel actions.

### 7.2 `VideoPage.razor.cs`

**Delete these members:** `_streamStrategy` seek-bar usage, `_seekBarPosition`, `_seekInProgress`, `OnSeekBarInput`, `OnSeekBarChanged`, `OnTranscodeSeekComplete`, `OnStreamStrategy`, the script-load block inside `OnAfterRenderAsync`, `attachHlsPlayer`/`attachVideoErrorListener`/`attachIdleAutoHide`/`attachKeyboardShortcuts`/`initSeekSlider`/`ToggleFullscreenAsync` (the JS OSD owns fullscreen), and the per-function interop in `ClosePlayer`/`DisposeAsync`.

**Keep/adapt:** `_playerOpen`, `_playerVideo`, `_playerMetadata`, `_playerSubtitles`, `_codecErrorMessage`/`_codecErrorGuidance` (for a fallback Blazor error banner if you prefer), `_dotNetRef`, `_playerSeriesContext`, `OpenVideoDetailAsync`, `ClosePlayer`, `OpenEpisodeVideoAsync`, `OpenSeriesVideoAsync`, `NavigateToSeriesFromPlayer`, `NavigateToSeasonFromPlayer`.

**New fields:**

```csharp
private IReadOnlyList<VideoAudioStreamDto> _playerAudioStreams = [];
private int _playerDefaultAudioIndex;
```

**New `OnAfterRenderAsync(firstRender)`:**

```csharp
if (_playerOpen && !_videoPlayerInitialized)
{
    _videoPlayerInitialized = true;
    _dotNetRef ??= DotNetObjectReference.Create(this);
    await LoadPlayerScriptsAsync();          // promise-chained onload; no Task.Delay
    await Js.InvokeVoidAsync("DotNetCloudVideoPlayer.init", BuildPlayerConfig());
}
```

`BuildPlayerConfig()` returns the JSON in §6.2. `GetStreamUrl(_playerVideo.Id)` is `/api/v1/videos/{id}/stream`. `resumeSeconds` from `_playerVideo.WatchPositionTicks` (`TimeSpan.FromTicks(...).TotalSeconds`). `durationSeconds` from `_playerVideo.Duration.TotalSeconds`.

**`LoadPlayerScriptsAsync()`** — replace the current `eval` + `Task.Delay(500)` with:

```csharp
await Js.InvokeVoidAsync("eval",
  "(function(){return new Promise(function(res){var h=document.createElement('script');h.src='/_content/DotNetCloud.Modules.Video/hls.min.js?v=1';h.onload=function(){var p=document.createElement('script');p.src='/api/v1/videos/video-player-js?v=1';p.onload=res;p.onerror=res;document.head.appendChild(p);};h.onerror=res;document.head.appendChild(h);});})()");
```

(Keep serving `video-player.js` via the existing `GetVideoPlayerJs` endpoint to avoid the static-assets bug; verify `/_content/...` as an alternative during testing — see §9.2.)

**New JSInvokables:**

```csharp
[JSInvokable] public void OnError(int code, string message) { /* build _codecErrorMessage, StateHasChanged */ }
[JSInvokable] public void OnStrategy(string strategy) { _streamStrategy = strategy; /* badge display only, no seek-bar init */ }
[JSInvokable] public void OnEnded() { _ = AutoAdvanceEpisodeAsync(); }
[JSInvokable] public async Task OnNavigateEpisode(int delta) { await NavigateEpisodeAsync(delta); }
```

**`NavigateEpisodeAsync(int delta)`:**

```csharp
private async Task NavigateEpisodeAsync(int delta)
{
    if (_playerSeriesContext is null) return;
    // TV series with seasons
    if (_playerSeriesContext.Season is not null)
    {
        var episodes = _seasonEpisodes; // List<VideoEpisodeDto> in order
        var idx = episodes.FindIndex(e => e.EpisodeNumber == _playerSeriesContext.EpisodeNumber);
        var next = idx + delta;
        if (next >= 0 && next < episodes.Count)
        {
            _playerSeriesContext = new PlayerSeriesContext(_playerSeriesContext.Series,
                _playerSeriesContext.Season, episodes[next].EpisodeNumber, null);
            await OpenEpisodeVideoAsync(episodes[next]);
        }
        return;
    }
    // Movie franchise (no seasons)
    var items = _seriesVideos; // List<VideoSeriesItemDto> in order
    var i = items.FindIndex(x => x.SortOrder == _playerSeriesContext.SortOrder);
    var n = i + delta;
    if (n >= 0 && n < items.Count)
    {
        _playerSeriesContext = new PlayerSeriesContext(_playerSeriesContext.Series,
            null, null, items[n].SortOrder);
        await OpenSeriesVideoAsync(items[n]);
    }
}

private async Task AutoAdvanceEpisodeAsync() => await NavigateEpisodeAsync(1);
```

> `OpenVideoDetailAsync` already resets player state and sets `_playerOpen = true`; it must also reset `_videoPlayerInitialized = false` so the new video re-inits the JS player. Also add `_videoPlayerInitialized = false` to `ClosePlayer`.

**`OpenVideoDetailAsync`** — additionally load audio streams:

```csharp
_playerAudioStreams = await GetAudioStreamsAsync(video.Id, caller); // GET /streams → data.audioStreams
_playerDefaultAudioIndex = _playerAudioStreams.FirstOrDefault(s => s.IsDefault)?.Index ?? 0;
```

(`GetAudioStreamsAsync` uses `HttpClient`/`IJSRuntime` fetch or an injected typed client; simplest is an `HttpClient` GET with cookie auth. Prefer an injected `HttpClient` registered for the module, or reuse the existing `IVideoApiClient` if it has a suitable method — otherwise add `HttpClient` via `@inject HttpClient Http` and parse the envelope.)

**`ClosePlayer`/`DisposeAsync`** — replace the many `Js.InvokeVoidAsync(...)` calls with a single `await Js.InvokeVoidAsync("DotNetCloudVideoPlayer.destroy")` (guarded with try/catch), then `_videoPlayerInitialized = false; _playerAudioStreams = [];`.

### 7.3 `VideoPage.razor.css`

- Remove: `.player-container`, `.video-player`, `.transcode-seek-bar`, `.transcode-seek-track/-fill/-thumb/-time-*`, `.seek-play-pause`, `.codec-error-*`, `.no-audio-banner` (the OSD replaces them).
- Add scoped styles for `.video-player-root`, `.dnc-player`, `.dnc-video`, `.dnc-osd`, `.dnc-seek*`, `.dnc-controls button`, `.dnc-spinner`, `.dnc-big-play`, `.dnc-error`, `.dnc-menu` (popover menus). Adapt from `jellyfin-web` `videoosd.scss` / `htmlVideoPlayer/style.scss`.

> Blazor CSS isolation adds an attribute to elements rendered by Razor but **not** to elements created by JS. Use selectors that rely on the host container class (`.video-player-root .dnc-osd { … }`) which is on a Razor-rendered element, so the isolation attribute applies to the container and the descendant selector still matches JS-created children.

---

## 8. Phase 4 — Cleanup and docs

1. Delete old `video-player.js` functions (the entire file is replaced) and any now-unused `[JSInvokable]` methods and Razor markup.
2. Update `docs/IMPLEMENTATION_CHECKLIST.md` and `docs/MASTER_PROJECT_PLAN.md` with **targeted edits** — mark this player rebuild and the three new features with `✓`/`☐` (never `[x]`/`[ ]`), and update the Quick Status Summary table.

---

## 9. Decisions and open items

1. **Single JS-owned DOM** — Blazor renders only `#video-player-root`; the JS player owns all controls. This is the fix for the `src`-reapply re-render bugs.
2. **No new npm dependencies** — reuse bundled `hls.min.js`; the player is a plain script. Verify the bundled hls.js version during implementation (run `hls.version` in a console, or check the file's first bytes for a version comment); pin/replace if older than 1.4.x.
3. **Audio streams not persisted** — enumerated from ffprobe at request time. Alternative (persist a JSON column on `CanonicalVideoMetadata` at index time) is out of scope unless requested.
4. **Subtitle conversion is client-side** — SRT→VTT Blob URL. No subtitle controller changes.
5. **Script serve path** — keep `GET /api/v1/videos/video-player-js` as the source of truth for `video-player.js` (it already works in prod); test `/_content/DotNetCloud.Modules.Video/video-player.js` as an alternative and pick whichever the deployed host serves reliably. The deploy script (`scripts/deploy.sh`) already copies the file to multiple locations — update it only if the chosen path changes.
6. **Icon rule exception** — OSD icons are Material ligature text (JS-built DOM cannot use the `MaterialIcon` Razor component). Add a code comment documenting the exception.

---

## 10. Test plan

### 10.1 Backend unit tests

**`tests/DotNetCloud.Modules.Video.Tests/Services/FfmpegArgumentBuilderTests.cs`** (existing file — add cases):

- `GetStreamCopyArgs_DefaultAudioStream_MapsFirstAudio` — default call contains `-map 0:a:0?`.
- `GetStreamCopyArgs_AudioStreamIndex_MapsSelectedAudio` — pass `audioStreamIndex: 1` → contains `-map 0:a:1?`, not `-map 0:a:0?`.
- `BuildHlsArgs_AudioStreamIndex_MapsSelectedAudio` — same assertion for `BuildHlsArgs` overload that accepts `audioStreamIndex`.
- `BuildProgressiveMp4Args_AudioStreamIndex_MapsSelectedAudio` — same assertion (if you add the param there).
- Existing assertions that check `-map 0:v:0? -map 0:a:0?` (e.g. line 187) must still pass for the default path.

**`tests/DotNetCloud.Modules.Video.Tests/Services/VideoTranscodingServiceTests.cs`** (add if not present, or extend `FfmpegArgumentBuilderTests`):

- `ParseCodecInfo_MultipleAudioStreams_ReturnsAll` — feed a JSON fixture with 2 audio streams and assert the returned `AudioStreams` list has index/codec/language/title/channels/isDefault correctly parsed. (`ParseCodecInfo` is `private static`; test via `ProbeStreamsAsync` if it stays private, or make it `internal static` and add `InternalsVisibleTo`. The test project already has `InternalsVisibleTo` for the module assembly — verify; otherwise test through `ProbeStreamsAsync` with a mocked ffprobe is hard because it shells out. **Simplest:** change `ParseCodecInfo` to `internal static` and unit-test directly.)

**`tests/DotNetCloud.Core.Tests/Media/VideoMetadataExtractorTests.cs`** (optional, only if you also enumerate audio streams at index time — not required by the default plan; skip otherwise).

**`tests/DotNetCloud.Modules.Video.Tests/VideoStreamingServiceTests.cs`** — no change required (token/range logic untouched).

### 10.2 Controller/route tests (if a controller test harness exists)

If `tests/DotNetCloud.Modules.Video.Tests` has controller tests (check; if not, add a minimal one using the module's existing test helper pattern in `tests/DotNetCloud.Modules.Video.Tests/TestHelpers.cs`):

- `GetAudioStreams_ReturnsAudioStreams_WhenVideoExists` — mock `_videoService`, `_downloadService` (returns a temp file), `_transcodingService.ProbeStreamsAsync`; assert 200 + `data.audioStreams` mapped correctly.
- `GetAudioStreams_ReturnsNotFound_WhenVideoMissing`.
- `SeekTranscode_AudioStreamIndex_IsPassedToTranscodeHlsAsync` — verify the selected index flows through (Moq verification on `TranscodeHlsAsync`).

### 10.3 Blazor host

- Existing `tests/DotNetCloud.Modules.Video.Tests` covers services; `VideoPage` has no unit tests today and the JS player cannot be unit-tested in this repo without a JS test runner (out of scope). Verify `VideoPage` compiles and the new `NavigateEpisodeAsync` logic is factored so the index-finding part can be extracted into a pure static helper and tested:
  - Add `static int? FindEpisodeIndex(...)` helper (or test via a small pure method `ComputeNextEpisodeIndex(count, currentIndex, delta)` in a testable static class) and cover with tests for: middle, first (prev at bound → null), last (next at bound → null), single episode, empty list, franchise `SortOrder` variant.

### 10.4 JS player (manual only)

No automated JS test harness exists. Verification is manual per §11. List exact console assertions in a test script doc if desired, but do not add a JS test dependency.

---

## 11. Verification matrix (before any commit — hard rule)

1. `dotnet build` (whole solution) — clean, `TreatWarningsAsErrors` satisfied.
2. `dotnet test tests/DotNetCloud.Modules.Video.Tests` — all green (existing + new).
3. Manual playback, Chromium **and** Firefox:
   - H.264/AAC MP4 → direct play, native seek works.
   - H.264/AAC MKV → remux, seek works via `startSeconds` reload.
   - Non-browser codec file → "Preparing stream…" overlay then HLS playback.
   - Two-audio-track file → audio menu lists both; switching plays the other track; unsupported audio re-encodes to AAC.
   - Subtitle SRT + VTT → render and toggle in both browsers; default auto-enabled.
   - Playback rate 0.5x–3.5x works.
   - Prev/next episode buttons and auto-advance work in a TV season and a franchise; stop at bounds.
   - Pause/resume, ±10s, slider seek (within and beyond buffer), fullscreen, volume/mute, PiP, auto-hide, keyboard shortcuts.
   - Resume: play partway, close, reopen → position restored.
4. Regression: close player, navigate sections, open another video → no leaked hls.js instances, no stuck overlay, no console errors.

---

## 12. Repository compliance reminders

- Use **targeted edits** for `docs/IMPLEMENTATION_CHECKLIST.md` and `docs/MASTER_PROJECT_PLAN.md`.
- Use `✓`/`☐` (never `[x]`/`[ ]`) in all docs.
- PowerShell for any terminal commands; backslash paths in docs.
- **Do not commit until all of §11 is complete.** If live verification cannot run in this environment, stop and report before committing.
- `read_file` may return a stale editor-buffer copy of files open in the editor; close tabs or use `git show`/`Get-Content` to confirm on-disk content before editing.
