# Android Music Tab — Implementation Plan

**Branch:** `feature/android-music-tab`
**Date:** 2026-06-29
**Status:** planning

## Overview

Add a read-only Music tab to the Android MAUI app that streams music from the server via the existing REST API (`/api/v1/music/`), with background playback via Android foreground service + `WakeLock`, native Android equalizer, two-tier album art caching, and dynamic tab visibility based on server module availability.

## Features

- ✓ Browse artists, albums, tracks, and playlists (read-only)
- ✓ Stream audio from the server via Files module content endpoint
- ✓ Background playback via Android foreground service with media notification
- ✓ Native Android equalizer (maps server 10-band presets to device EQ)
- ✓ Two-tier album art caching (memory + disk)
- ✓ Dynamic tab visibility — hidden when server lacks music module
- ☐ Playlist creation (deferred — not supported on Android)
- ☐ Visualization (excluded)
- ☐ Music indexing (server-side only)
- ☐ Offline/cached playback (deferred)
- ☐ gRPC communication (REST used instead — Android has no gRPC infra)

---

## Architecture

```
┌──────────────────────────────────────────────────┐
│ Android MAUI App                                 │
│                                                  │
│  MusicPage.xaml ←→ MusicViewModel                │
│       │                │                         │
│       │                ├── IMusicRestClient ── REST ──> /api/v1/music/*
│       │                ├── IMusicPlayerService ── Android MediaPlayer
│       │                ├── IEqualizerService ──── Android AudioEffect.Equalizer
│       │                └── IAlbumArtCache ─────── Memory LRU + Disk
│       │                                           │
│  MusicPlaybackService (Foreground)                │
│       ├── WakeLock (PARTIAL)                      │
│       ├── MediaStyle Notification                 │
│       └── Sticky restart                          │
│                                                   │
│  ModuleAvailabilityState (static)                 │
│       └── GET /api/v1/core/modules/music/available│
└──────────────────────────────────────────────────┘
```

### Data Flow

1. **Module Detection:** `App.xaml.cs` → `GET /api/v1/core/modules/music/available` → `ModuleAvailabilityState.IsMusicModuleAvailable` → `AppShell` Music tab visibility
2. **Browse Library:** `MusicPage` → `MusicViewModel` → `IMusicRestClient` → `GET /api/v1/music/artists|albums|tracks|playlists` → DTOs → `CollectionView`
3. **Play Track:** Tap track → `MusicPlayerService.PlayAsync()` → `MediaPlayer.SetDataSource({server}/api/v1/files/{fileNodeId}/content, authHeaders)` → stream
4. **Background Audio:** `MusicPlayerService` starts `MusicPlaybackService` (foreground) → `WakeLock` acquired → notification shown
5. **Equalizer:** `AndroidEqualizerService` attaches to `MediaPlayer.AudioSessionId` → maps server 10-band preset (31Hz–16KHz) to device EQ bands
6. **Album Art:** `AlbumArtCache` → check memory → check disk → `GET /api/v1/music/albums/{albumId}/cover` → cache → `ImageSource`

---

## Phases

### Phase 1: Music REST Client

**Steps**
1. Create `src/Clients/DotNetCloud.Client.Android/Music/IMusicRestClient.cs` — interface with read-only methods for artists, albums, tracks, playlists, EQ presets, genres, playback recording, and starring. All methods accept `serverBaseUrl` + `accessToken` per existing `IFileRestClient` pattern.
2. Create `src/Clients/DotNetCloud.Client.Android/Music/HttpMusicRestClient.cs` — implementation backed by `HttpClient`. Parses `{ success: true, data: ... }` envelope. Deserializes to shared DTOs from `DotNetCloud.Core.DTOs` (`ArtistDto`, `MusicAlbumDto`, `TrackDto`, `PlaylistDto`, `EqPresetDto`).
3. Register in `MauiProgram.cs` via `AddHttpClient<IMusicRestClient, HttpMusicRestClient>()` with `AuthenticatedHttpClientHandler`.

*Parallel with Phase 4, 6*

**Relevant files**
- `src/Core/DotNetCloud.Core/DTOs/MusicDtos.cs` — shared DTOs (reuse as-is)
- `src/Clients/DotNetCloud.Client.Android/Files/IFileRestClient.cs` — interface pattern
- `src/Clients/DotNetCloud.Client.Android/Files/HttpFileRestClient.cs` — implementation pattern
- `src/Clients/DotNetCloud.Client.Android/MauiProgram.cs` — registration

**Deliverables**
- ☐ `Music/IMusicRestClient.cs`
- ☐ `Music/HttpMusicRestClient.cs`
- ☐ Registration in `MauiProgram.cs`

---

### Phase 2: Module Detection

**Steps**
1. Create `src/Clients/DotNetCloud.Client.Android/Services/ModuleAvailabilityState.cs` — static class with `bool IsMusicModuleAvailable` property.
2. In `App.xaml.cs` `OnStart()`, after navigating to main, call `GET /api/v1/core/modules/music/available` using an `HttpClient` with the stored token from `ISecureTokenStore`. Parse `{ installed: true/false }`. Set `ModuleAvailabilityState.IsMusicModuleAvailable`.

*Parallel with Phase 1, 4*

**Relevant files**
- `src/Core/DotNetCloud.Core.Server/Controllers/ModulesController.cs` — endpoint at `GET /api/v1/core/modules/{moduleId}/available`
- `src/Clients/DotNetCloud.Client.Android/App.xaml.cs` — add module check
- `src/Clients/DotNetCloud.Client.Android/Auth/ISecureTokenStore.cs` — reuse for token retrieval

**Deliverables**
- ☐ `Services/ModuleAvailabilityState.cs`
- ☐ Module check in `App.xaml.cs`

---

### Phase 3: Dynamic Tab Visibility in AppShell

**Steps**
1. Create `src/Clients/DotNetCloud.Client.Android/Services/MusicPageVisibilitySource.cs` — simple class with `bool IsMusicModuleAvailable` property that reads `ModuleAvailabilityState.IsMusicModuleAvailable`.
2. Add Music `ShellContent` in `AppShell.xaml` with `IsVisible="{Binding IsMusicModuleAvailable}"`, bound to a `MusicPageVisibilitySource` instance set as a resource or direct `BindingContext` on the `ShellContent`.
3. In `AppShell.xaml.cs`, set up the binding source and register any detail routes if needed.

**Approach:** MAUI Shell `ShellContent` supports `IsVisible`. Bind it to a wrapper object that reads the static `ModuleAvailabilityState` flag. The static flag is set before the Shell is first displayed (in `App.xaml.cs`).

**Relevant files**
- `src/Clients/DotNetCloud.Client.Android/AppShell.xaml` — add Music ShellContent
- `src/Clients/DotNetCloud.Client.Android/AppShell.xaml.cs` — binding setup

**Deliverables**
- ☐ `Services/MusicPageVisibilitySource.cs`
- ☐ Music `ShellContent` in `AppShell.xaml`
- ☐ Binding setup in `AppShell.xaml.cs`

---

### Phase 4: Music ViewModel & Browsing UI

**Steps**
1. Create `src/Clients/DotNetCloud.Client.Android/ViewModels/MusicViewModel.cs`:
   - Observable properties: `Artists`, `Albums`, `Tracks`, `Playlists`, `EqPresets`, `Genres`, `CurrentTrack`, `IsPlaying`, `CurrentPosition`, `Duration`, `IsShuffle`, `IsRepeat`
   - Drill-down state: `SelectedArtist`, `SelectedAlbum`, `CurrentView` enum
   - Commands: `LoadArtistsCommand`, `LoadAlbumsCommand`, `LoadTracksCommand`, `SelectArtistCommand`, `SelectAlbumCommand`, `PlayTrackCommand`, `PlayPlaylistCommand`, `ToggleStarCommand`, `TogglePlayPauseCommand`, `NextCommand`, `PreviousCommand`, `SeekCommand`, `LoadEqPresetsCommand`, `ApplyEqPresetCommand`, `RecordPlayCommand`
   - Injects: `IMusicRestClient`, `IMusicPlayerService`, `IEqualizerService`, `IServerConnectionStore`, `ISecureTokenStore`, `IAlbumArtCache`
2. Create `src/Clients/DotNetCloud.Client.Android/Views/MusicPage.xaml` + `.xaml.cs`:
   - Top: Now-playing bar (album art thumbnail, track title, artist name, play/pause/next buttons, seek slider)
   - Segmented control: Artists | Albums | Tracks | Playlists
   - `CollectionView` for each category with `DataTemplate`
   - Tap artist → show albums for that artist → tap album → show tracks → tap track → play
   - Equalizer button in now-playing bar → opens EQ preset bottom sheet
3. Register in `MauiProgram.cs`: transient `MusicViewModel`, transient `MusicPage`

*Parallel with Phase 1, 2*

**Relevant files**
- `src/Clients/DotNetCloud.Client.Android/Views/FileBrowserPage.xaml` — pattern for list-based page
- `src/Clients/DotNetCloud.Client.Android/ViewModels/FileBrowserViewModel.cs` — pattern for data-loading ViewModel

**Deliverables**
- ☐ `ViewModels/MusicViewModel.cs`
- ☐ `Views/MusicPage.xaml`
- ☐ `Views/MusicPage.xaml.cs`
- ☐ Registration in `MauiProgram.cs`

---

### Phase 5: Audio Playback Foreground Service

**Steps**
1. Create `src/Clients/DotNetCloud.Client.Android/Services/IMusicPlayerService.cs`:
   - Methods: `PlayAsync(TrackDto, serverBaseUrl, accessToken)`, `Pause()`, `Resume()`, `Stop()`, `Seek(TimeSpan)`, `SetVolume(float)`, `PlayNext()`, `PlayPrevious()`, `Enqueue(IEnumerable<TrackDto>)`
   - Properties: `CurrentPosition`, `Duration`, `IsPlaying`, `CurrentTrack`, `AudioSessionId`
   - Events: `PlaybackStateChanged`, `TrackEnded`
2. Create `src/Clients/DotNetCloud.Client.Android/Services/MusicPlayerService.cs`:
   - Wraps `Android.Media.MediaPlayer` (supports streaming from URL)
   - Streams from `{serverBaseUrl}/api/v1/files/{fileNodeId}/content`
   - Passes auth via `MediaPlayer.SetDataSource(context, uri, headers)` with `Authorization: Bearer {token}` header
   - Auto-plays next track on completion
   - Maintains internal play queue
   - Starts/stops `MusicPlaybackService` internally:
     - Start: when playback begins
     - Stop: when paused for >5 minutes or queue exhausted
   - Position updates via `System.Timers.Timer` at 1s interval → raises `PlaybackStateChanged`
3. Create `src/Clients/DotNetCloud.Client.Android/Platforms/Android/MusicPlaybackService.cs`:
   - Follows `ChatConnectionService` pattern exactly:
     - `[Service(Name = "net.dotnetcloud.client.MusicPlaybackService", ForegroundServiceType = MediaPlayback, Exported = false)]`
     - `WakeLock` (PARTIAL) to prevent CPU sleep during audio decoding
     - `StartCommandResult.Sticky` for auto-restart if killed
     - Persistent notification with `MediaStyle`:
       - Track title + artist as content text
       - Play/pause, skip next, skip previous actions via `MediaButtonReceiver`
       - Tap notification → open app to Music tab
   - Action intents: `ActionStart`, `ActionStop`, `ActionPlayPause`, `ActionNext`, `ActionPrevious`
   - References `IMusicPlayerService` singleton from DI (`Ioc.Default`)
4. Declare in `AndroidManifest.xml`:
   ```xml
   <service android:name=".MusicPlaybackService"
            android:foregroundServiceType="mediaPlayback"
            android:exported="false" />
   <uses-permission android:name="android.permission.FOREGROUND_SERVICE_MEDIA_PLAYBACK" />
   <uses-permission android:name="android.permission.WAKE_LOCK" />
   ```
5. Create notification channel `music_playback` in `MainApplication.cs` (same pattern as `chat_connection`).
6. Register `IMusicPlayerService` as singleton in `MauiProgram.cs`.

**Relevant files**
- `src/Clients/DotNetCloud.Client.Android/Platforms/Android/ChatConnectionService.cs` — foreground service pattern
- `src/Clients/DotNetCloud.Client.Android/Platforms/Android/AndroidManifest.xml` — declare service + permissions
- `src/Clients/DotNetCloud.Client.Android/Platforms/Android/MainApplication.cs` — notification channel

**Deliverables**
- ☐ `Services/IMusicPlayerService.cs`
- ☐ `Services/MusicPlayerService.cs`
- ☐ `Platforms/Android/MusicPlaybackService.cs`
- ☐ `AndroidManifest.xml` updated
- ☐ `MainApplication.cs` updated
- ☐ Registration in `MauiProgram.cs`

---

### Phase 6: Album Art Caching

**Steps**
1. Create `src/Clients/DotNetCloud.Client.Android/Services/IAlbumArtCache.cs`:
   - `Task<ImageSource?> GetAlbumArtAsync(Guid albumId, string serverBaseUrl, string accessToken, CancellationToken ct = default)`
   - `void Invalidate(Guid albumId)`
   - `void Clear()`
2. Create `src/Clients/DotNetCloud.Client.Android/Services/AlbumArtCache.cs`:
   - **Memory tier:** `ConcurrentDictionary<Guid, ImageSource>` with max size ~50. Evicts LRU entry when full. Tracks access order with a simple linked list or `DateTime` timestamp.
   - **Disk tier:** Files stored at `FileSystem.CacheDirectory/albumart/{albumId:N}.jpg`. Written on download, read on cache miss.
   - **Download:** Uses `HttpClient` (injected via constructor, registered with `AuthenticatedHttpClientHandler` in DI) → `GET /api/v1/music/albums/{albumId}/cover` → save to disk → load to memory → return `ImageSource.FromStream()`.
   - On hit (memory): return immediately.
   - On hit (disk only): load from file, promote to memory, return.
   - On miss: download, cache both tiers, return.
3. Register as singleton in `MauiProgram.cs` with its own `HttpClient` (registered with `AuthenticatedHttpClientHandler`).
4. Use in `MusicViewModel` for album art in lists and now-playing bar.

**Relevant files**
- `src/Clients/DotNetCloud.Client.Android/MauiProgram.cs` — registration
- `src/Clients/DotNetCloud.Client.Android/Music/HttpMusicRestClient.cs` — album cover URL pattern

**Deliverables**
- ☐ `Services/IAlbumArtCache.cs`
- ☐ `Services/AlbumArtCache.cs`
- ☐ Registration in `MauiProgram.cs`

---

### Phase 7: Native Android Equalizer

**Steps**
1. Create `src/Clients/DotNetCloud.Client.Android/Services/IEqualizerService.cs`:
   - `bool IsAvailable { get; }` — checks device support
   - `int NumberOfBands { get; }`
   - `int[] GetBandFrequenciesMhz()` — center frequencies in millihertz
   - `void SetBandLevel(int bandIndex, short gainMb)` — gain in millibels
   - `void SetAllBands(IDictionary<string, double> bands)` — maps server frequency labels → closest device band
   - `void ApplyPreset(EqPresetDto preset)` — convenience wrapper
   - `void Reset()` — flat EQ
   - `event EventHandler? AvailabilityChanged`
2. Create `src/Clients/DotNetCloud.Client.Android/Services/AndroidEqualizerService.cs`:
   - Listens to `IMusicPlayerService.PlaybackStateChanged`
   - When playback starts, gets `AudioSessionId` from `MusicPlayerService`
   - Creates `Android.Media.Audiofx.Equalizer(priority=0, audioSessionId)`
   - Priority 0 = highest, recommended for music apps
   - **Band mapping:** The server presets use 10 bands at these frequencies (Hz): `31, 63, 125, 250, 500, 1000, 2000, 4000, 8000, 16000`. For each server band, find the closest device EQ band by center frequency. Set gain on that band.
   - **Gain conversion:** Server uses dB (`double`, range ~-12 to +12). Android uses millibels (`short`, range ~-1500 to +1500). Convert: `gainMb = (short)(gainDb * 100)`.
   - Falls back gracefully if device has fewer bands (map to nearest available).
   - `IsAvailable` checks `Equalizer.GlobalNumberOfPresets > 0` or catches exception on creation.
   - Disposes `Equalizer` when playback stops.
3. Register as singleton in `MauiProgram.cs` (injects `IMusicPlayerService`).

**Note:** The Android `Equalizer` API works on the global audio output mix on most devices. This means EQ affects the entire audio session, not just our app's audio. This is the standard behavior and is acceptable.

**Relevant files**
- `src/Clients/DotNetCloud.Client.Android/Services/MusicPlayerService.cs` — exposes `AudioSessionId`
- `src/Core/DotNetCloud.Core/DTOs/MusicDtos.cs` — `EqPresetDto.Bands: IReadOnlyDictionary<string, double>`

**Deliverables**
- ☐ `Services/IEqualizerService.cs`
- ☐ `Services/AndroidEqualizerService.cs`
- ☐ Registration in `MauiProgram.cs`

---

### Phase 8: Resources & Integration

**Steps**
1. Add `Resources/Images/music_icon.svg` — tab bar icon (music note symbol, consistent style with existing icons).
2. Extend `src/Clients/DotNetCloud.Client.Android/Converters/AppConverters.cs`:
   - `TimeSpanToMmSsConverter` — `TimeSpan` → `"m:ss"` string
   - `BoolToPlayPauseIconConverter` — `true` (playing) → pause icon, `false` → play icon
3. Final wiring in `MauiProgram.cs` — ensure all service registrations are in correct order.
4. Final wiring in `AppShell.xaml` — Music tab with icon, conditional visibility binding.
5. Final wiring in `App.xaml.cs` — module availability check integrated into startup flow.

**Deliverables**
- ☐ `Resources/Images/music_icon.svg`
- ☐ `Converters/AppConverters.cs` updated
- ☐ All registrations complete and verified

---

## Files Summary

### Files to Create (15)

| File | Purpose |
|------|---------|
| `Music/IMusicRestClient.cs` | Music REST API interface |
| `Music/HttpMusicRestClient.cs` | Music REST API implementation |
| `Services/ModuleAvailabilityState.cs` | Static state for module detection |
| `Services/MusicPageVisibilitySource.cs` | Binding source for ShellContent IsVisible |
| `ViewModels/MusicViewModel.cs` | Music browsing & playback ViewModel |
| `Views/MusicPage.xaml` | Music browsing UI |
| `Views/MusicPage.xaml.cs` | Music page code-behind |
| `Services/IMusicPlayerService.cs` | Audio player interface |
| `Services/MusicPlayerService.cs` | Android MediaPlayer + streaming |
| `Platforms/Android/MusicPlaybackService.cs` | Foreground service for background audio |
| `Services/IAlbumArtCache.cs` | Album art cache interface |
| `Services/AlbumArtCache.cs` | Two-tier memory+disk album art cache |
| `Services/IEqualizerService.cs` | Equalizer interface |
| `Services/AndroidEqualizerService.cs` | Android AudioEffect Equalizer |
| `Resources/Images/music_icon.svg` | Tab bar icon |

### Files to Modify (7)

| File | Change |
|------|--------|
| `MauiProgram.cs` | Register all new services, HttpClient, ViewModels, Pages |
| `App.xaml.cs` | Module availability check on startup |
| `AppShell.xaml` | Add Music ShellContent with IsVisible binding |
| `AppShell.xaml.cs` | Binding setup, register detail routes |
| `Platforms/Android/AndroidManifest.xml` | Declare MusicPlaybackService, add permissions |
| `Platforms/Android/MainApplication.cs` | Create music_playback notification channel |
| `Converters/AppConverters.cs` | Add TimeSpan→mm:ss and play/pause icon converters |

---

## Verification Checklist

- ☐ `dotnet build src/Clients/DotNetCloud.Client.Android/ -f net10.0-android` succeeds
- ☐ Music tab hidden when server has no music module installed
- ☐ Music tab visible when music module is installed and active
- ☐ Artists list loads and displays correctly
- ☐ Albums list loads and displays correctly (with cover art)
- ☐ Drill-down: artist → albums → tracks works
- ☐ Tracks list loads and displays correctly
- ☐ Playlists list loads (read-only)
- ☐ Playlist tracks load when playlist is tapped
- ☐ Track streaming works (tap → audio plays)
- ☐ Play/pause toggle works
- ☐ Next/previous track works
- ☐ Seek slider works
- ☐ Background playback: switch away from app → audio continues
- ☐ Foreground service notification shows track info + controls
- ☐ Notification play/pause action works
- ☐ EQ presets load from server
- ☐ Applying EQ preset changes audio tonality
- ☐ Album art displays in lists and now-playing bar
- ☐ Album art cached (second load is instant)
- ☐ Token refresh works for music API calls (401 → refresh → retry)
- ☐ Chat tab unaffected
- ☐ Files tab unaffected
- ☐ Settings tab unaffected
- ☐ Login flow unaffected

---

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| **REST over gRPC** | Android app has zero gRPC infrastructure. Music module exposes full REST API at `/api/v1/music/`. Same `HttpClient` + `AuthenticatedHttpClientHandler` pattern as Chat/Files tabs. |
| **Foreground service (not power settings)** | Standard Android approach for background audio. Follows existing `ChatConnectionService` pattern with `WakeLock`, `Sticky`, media notification. More reliable than manipulating power settings. |
| **Two-tier album art cache** | Memory LRU (~50) for instant display + disk for persistence across sessions. Avoids re-downloading covers on every list scroll. |
| **Native Android Equalizer** | `android.media.audiofx.Equalizer` attached to `MediaPlayer` audio session. Maps server 10-band presets (31Hz–16KHz) to closest device band frequencies. dB ↔ millibels conversion. |
| **`ShellContent.IsVisible` binding** | MAUI Shell supports `IsVisible` on `ShellContent`. Bind to a wrapper that reads static `ModuleAvailabilityState`. Flag is set before Shell first displays. |
| **No playlist creation** | Read-only design for Android. Playlists are created via Blazor UI only. |
| **No visualization** | Excluded by design. |
| **No offline playback** | Streaming only, same as Blazor UI. |
| **No indexing** | Server-side only. Android is a consumer of already-indexed data. |
| **No gRPC** | Zero gRPC dependencies in the Android project. REST API is sufficient and already exists. |
