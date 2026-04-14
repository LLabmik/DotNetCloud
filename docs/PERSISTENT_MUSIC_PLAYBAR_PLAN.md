# Plan: Persistent Music Playbar Across Navigation

## TL;DR
Make the music player bar persistent so it stays visible and keeps playing audio when the user navigates away from the Music page to other modules. This requires extracting playback state from `MusicPage` into a shared service, moving the playbar UI (including queue/EQ slide-out panels) into `MainLayout`, handling the music module being optional, and keeping the JS audio element alive globally.

## Current Architecture (Problem)
- **All playback state** (now playing, queue, position, shuffle, repeat, volume, EQ) lives as **component-local fields** in `MusicPage.razor.cs`
- **The playbar UI** (`now-playing-bar`) is rendered inside the `.music-layout` div in `MusicPage.razor`
- **JS interop** (`music-player.js`) is initialized per-component via `DotNetObjectReference<MusicPage>` — disposed when MusicPage unmounts
- **When user navigates away** from `/apps/music`, the entire MusicPage component is disposed → audio stops, `<audio>` element is destroyed, JS interop reference is nullified

## Approach
Follow the same pattern as `ToastService` / `ToastContainer`: a scoped service holds state, a component in `MainLayout` renders the persistent UI outside of `@Body`. Queue and EQ panels slide out from the global playbar (Spotify-like). Music module optionality handled via `ModuleUiRegistry` checks.

---

## Steps

### Phase 1: Shared Playback State Service

1. **Create `MusicPlaybackState` service** in `DotNetCloud.Modules.Music`
   - New file: `src/Modules/Music/DotNetCloud.Modules.Music/Services/MusicPlaybackState.cs`
   - Move all playback-related fields from `MusicPage.razor.cs` into this service:
     - `NowPlaying` (TrackDto?), `IsPlaying`, `Queue` (List<TrackDto>), `QueueIndex`, `Shuffle`, `Repeat`, `Volume`, `Muted`, `PlaybackPosition`, `EqBands`, `ActivePresetId`
     - **Queue/EQ panel visibility**: `ShowQueue`, `ShowEqualizer` booleans
     - **EQ presets cache**: `EqPresets` (List<EqPresetDto>) — loaded lazily on first EQ panel open
   - Add `event Action? OnChange` for UI notification (same pattern as `ToastService`)
   - Add public methods: `PlayTrack()`, `PlayNext()`, `PlayPrevious()`, `TogglePlayPause()`, `Seek()`, `SetVolume()`, `ToggleMute()`, `ToggleShuffle()`, `CycleRepeat()`, `AddToQueue()`, `RemoveFromQueue()`, `SetEqBands()`, `ApplyPreset()`, `SavePreset()`, `ToggleQueue()`, `ToggleEqualizer()`, `Stop()`
   - Encapsulate music service calls internally (inject `IPlaybackService`, `IEqPresetService` etc.) so the playbar component doesn't need individual music service injections
   - These methods mutate state and raise `OnChange`
   - Register as **scoped** service (one per circuit, matches Blazor Server semantics) in DI

2. **Register `MusicPlaybackState` in DI** — Add to `MusicServiceRegistration.AddMusicServices()` in `MusicServiceRegistration.cs` (keeps all music DI in one place, already always called at startup)

### Phase 2: Global Playbar Component

3. **Create `GlobalMusicPlaybar.razor`** in `src/UI/DotNetCloud.UI.Web/Components/Shared/`
   - Injects `MusicPlaybackState` and `ModuleUiRegistry`
   - **Module-optional guard**: Check `ModuleUiRegistry.NavItems` for `dotnetcloud.music` — if not present (module disabled/uninstalled), render nothing. This uses the existing `ModuleUiRegistry` singleton which auto-refreshes every 15s from the database.
   - Renders the now-playing bar UI (extracted from MusicPage.razor)
   - Only renders when music module is enabled AND `MusicPlaybackState.NowPlaying is not null`
   - Subscribes to `MusicPlaybackState.OnChange` → calls `StateHasChanged()`
   - Handles JS interop for the audio element (`music-player.js` init/play/pause/seek/volume/etc.)
   - Owns the `DotNetObjectReference` for JS callbacks (`OnJsTimeUpdate`, `OnJsTrackEnded`, `OnJsPlaybackError`, `OnJsMetadataLoaded`)
   - **Queue slide-out panel**: Renders queue panel inline (same markup as current MusicPage queue panel). Toggle via queue button in playbar.
   - **EQ slide-out panel**: Renders EQ panel with presets and 10-band sliders. Lazily loads presets on first open via `MusicPlaybackState.LoadEqPresetsAsync()`.
   - **Track info click → navigate to Music page** with auto-scroll context (see step 7)

4. **Add `GlobalMusicPlaybar` to `MainLayout.razor`**
   - Place it after `<ToastContainer />`, outside `@Body`, at the bottom of `.main-content`
   - This ensures it persists across all page navigations
   - Add conditional bottom padding to `.page-content` when playbar is visible (80px for playbar height)

5. **Move playbar CSS from `MusicPage.razor.css` to `app.css`**
   - The `now-playing-bar`, `np-*`, queue panel, and EQ panel classes need to be global since they're now rendered outside the music module's scoped CSS
   - Queue panel: right-side slide-out (position: fixed, bottom: 80px, right: 0, width: 320px)
   - EQ panel: right-side slide-out (position: fixed, bottom: 80px, right: 0, width: 320px) — stacks or replaces queue

### Phase 3: Refactor MusicPage

6. **Refactor `MusicPage.razor.cs` to use `MusicPlaybackState`**
   - Remove all playback state fields (they now live in the service)
   - Inject `MusicPlaybackState`
   - Replace direct state mutations with service method calls
   - Remove the playbar markup from `MusicPage.razor` (it's now in `GlobalMusicPlaybar`)
   - Remove the queue panel and EQ panel markup (now in `GlobalMusicPlaybar`)
   - Remove JS audio interop from MusicPage (it's now in `GlobalMusicPlaybar`)
   - Remove `DisposeAsync` logic for audio cleanup (owned by `GlobalMusicPlaybar`)
   - Keep: music library browsing, section navigation, playlist management, settings, visualizer — these are page-specific
   - Subscribe to `MusicPlaybackState.OnChange` to keep track highlighting in sync (e.g. `_nowPlaying?.Id == track.Id` for `.playing` CSS class)

7. **Auto-scroll to currently playing track**
   - When navigating to Music page (clicking track title in global playbar), pass context via `NavigationManager.NavigateTo("/apps/music?playing=true")`
   - `MusicPage.OnParametersSetAsync` detects `playing=true` query param → reads `MusicPlaybackState.NowPlaying`
   - Auto-navigates to the correct section (Album detail view for the playing track's album) and highlights the playing track
   - Uses JS interop `element.scrollIntoView()` to scroll to the highlighted track row

### Phase 4: JS Interop Lifecycle

8. **Modify `music-player.js` to be idempotent / re-entrant**
   - The `init()` function already creates the audio element on `document.body` if not found — this is good
   - Ensure `dispose()` does NOT remove the audio element from DOM when called from old component disposal
   - Instead, add a `detach()` method that just clears the dotNetRef (for when the global playbar component re-renders)
   - The audio element should survive across component mount/unmount cycles
   - The `init()` already checks for existing `#dnc-music-audio` — ensure reconnection works cleanly

### Phase 5: CSS & Layout Adjustments

9. **Add global playbar CSS to `app.css`**
   - Move `.now-playing-bar` and all `np-*` styles from `MusicPage.razor.css` to `app.css`
   - Add queue panel and EQ panel global styles
   - Add a CSS class on `.page-content` for bottom padding when playbar is active
   - Ensure z-index stacking: playbar z-100, queue/EQ panels z-90, above page content, below modals

10. **Adjust `.page-content` padding**
    - When the global playbar is showing, `page-content` needs `padding-bottom: 80px` so content isn't hidden behind the fixed playbar
    - Handled via a cascading value from `GlobalMusicPlaybar` or a CSS class on `.main-content`

---

## Relevant Files

| File | Action |
|------|--------|
| `src/Modules/Music/DotNetCloud.Modules.Music/UI/MusicPage.razor` | Remove playbar/queue/EQ markup |
| `src/Modules/Music/DotNetCloud.Modules.Music/UI/MusicPage.razor.cs` | Remove playback state, inject shared service, add auto-scroll |
| `src/Modules/Music/DotNetCloud.Modules.Music/UI/MusicPage.razor.css` | Remove playbar/queue/EQ styles |
| `src/Modules/Music/DotNetCloud.Modules.Music.Data/MusicServiceRegistration.cs` | Register `MusicPlaybackState` |
| `src/UI/DotNetCloud.UI.Web/Components/Layout/MainLayout.razor` | Add `<GlobalMusicPlaybar />` |
| `src/UI/DotNetCloud.UI.Web/wwwroot/css/app.css` | Global playbar + queue/EQ panel CSS |
| `src/UI/DotNetCloud.UI.Web/wwwroot/js/music-player.js` | Add `detach()`, make idempotent |
| `src/UI/DotNetCloud.UI.Web/Services/ModuleUiRegistry.cs` | Already exists; used for module-enabled checks |
| **NEW:** `src/Modules/Music/DotNetCloud.Modules.Music/Services/MusicPlaybackState.cs` | Shared playback state + encapsulated service calls |
| **NEW:** `src/UI/DotNetCloud.UI.Web/Components/Shared/GlobalMusicPlaybar.razor` | Persistent playbar + queue/EQ slide-outs |

## Verification

1. `dotnet build` — ensure no compile errors across the solution
2. `dotnet test` — ensure existing music module tests still pass
3. **Persistent playback:** Navigate to Music → play a track → navigate to Files → verify audio keeps playing, playbar visible, controls work, progress updates
4. **Queue slide-out:** From a non-music page, click queue icon in playbar → queue panel slides out
5. **EQ slide-out:** From a non-music page, click EQ icon in playbar → EQ panel slides out, presets load
6. **Auto-scroll:** Click track title in global playbar → Music page opens → album detail view → playing track highlighted and scrolled into view
7. **Module disabled:** Disable music module in admin → playbar disappears → re-enable → works again
8. **Module not installed:** Fresh install without music module enabled → no errors, no playbar, no JS console errors
9. **Stop/close:** Stop music from playbar → playbar disappears on all pages
10. **EQ persistence:** Set EQ bands → navigate away → navigate back → settings preserved
11. No JS console errors during navigation with music playing

## Decisions

- **Service lifetime: Scoped** — one per circuit (per browser tab). Each tab has independent playback state and its own DOM audio element.
- **Queue and EQ panels** are slide-out panels attached to the global playbar (Spotify-like UX). Available from any page when music is playing.
- **Visualizer** stays entirely on MusicPage — full-page overlay doesn't make sense globally.
- **The `<audio>` element** stays on `document.body` (already the case in `music-player.js`). Naturally survives Blazor navigation since it's outside the Blazor DOM.
- **Module optionality:** Use `ModuleUiRegistry` singleton (already refreshes every 15s from DB) to check if `dotnetcloud.music` is in `NavItems`. Since `AddMusicServices()` is always called at startup, `MusicPlaybackState` is always in DI — the guard is purely at the UI rendering level.
- **Auto-scroll mechanism:** Query parameter `?playing=true`. MusicPage reads `MusicPlaybackState.NowPlaying`, navigates to album detail, JS `scrollIntoView()` on playing track row.
- **MusicPlaybackState encapsulates service calls:** Injects `IPlaybackService`, `IEqPresetService`, `IMusicStreamingService` internally. `GlobalMusicPlaybar` only injects `MusicPlaybackState`.
