# Phase 5 Implementation Plan — Media (Photos, Music, Video)

> **Status:** In Progress (Sub-Phase A Complete)  
> **Created:** 2026-04-05  
> **Milestone:** Google Photos-like experience + streaming music player with equalizer + self-hosted video library  
> **Modules:** Photos, Music, Video (3 separate process-isolated modules)  
> **Estimated Steps:** 20 (across 5 sub-phases)  
> **Prerequisites:** Phases 0–4 complete ✅

---

## Table of Contents

1. [Overview](#overview)
2. [Key Technical Decisions](#key-technical-decisions)
3. [Dependency Graph](#dependency-graph)
4. [Sub-Phase A: Shared Media Infrastructure (5.1–5.2)](#sub-phase-a-shared-media-infrastructure-51-52)
5. [Sub-Phase B: Photos Module (5.3–5.7)](#sub-phase-b-photos-module-53-57)
6. [Sub-Phase C: Music Module (5.8–5.14)](#sub-phase-c-music-module-58-514)
7. [Sub-Phase D: Video Module (5.15–5.18)](#sub-phase-d-video-module-515-518)
8. [Sub-Phase E: Integration & Quality (5.19–5.20)](#sub-phase-e-integration--quality-519-520)
9. [Project Structure](#project-structure)
10. [Parallelism Opportunities](#parallelism-opportunities)
11. [Verification Criteria](#verification-criteria)
12. [Decisions Log](#decisions-log)
13. [Open Considerations](#open-considerations)

---

## Overview

Phase 5 adds three new process-isolated modules — **Photos**, **Music**, and **Video** — following the established modular monolith pattern. All three leverage the existing Files module for storage: media items are `FileNode` records with media-specific metadata overlays. There is no duplicated storage — Photos, Music, and Video are metadata + UI layers over the existing file storage engine.

**Scope includes:**
- Photos: library, albums, thumbnails, slideshow, basic non-destructive editing, EXIF/GPS, timeline, geo-grouping
- Music: library scanning, metadata, playlists, streaming, equalizer presets, recommendations, Subsonic API compatibility
- Video: library, collections/series, subtitles (SRT/VTT), watch progress/resume, streaming

**Deferred to post-Phase 5:**
- Android auto-upload (server-side upload API is already generic; client-side auto-upload is a sync client task)
- HLS/DASH adaptive bitrate streaming (initial impl is direct HTTP range-request streaming)
- Full smart playlist query builder (start with single-criteria presets)

---

## Key Technical Decisions

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | **Media ↔ Files Relationship** — Media records reference `FileNode` IDs from the Files module. No duplicated storage. | Single source of truth for files. Media modules are metadata + UI layers. Quota enforcement automatic via Files. |
| 2 | **Metadata Libraries** — ImageSharp (photos, already in project), TagLibSharp (music ID3/Vorbis/FLAC), FFprobe via existing FFmpeg integration (video). | Mature .NET libraries. ImageSharp already proven in codebase. TagLibSharp is the standard for audio metadata in .NET. |
| 3 | **HTTP Range-Request Streaming** — Shared middleware for all three modules. No HLS/DASH initially. | Simpler implementation, works for direct playback. HLS/DASH can be layered on later without breaking changes. |
| 4 | **Subsonic API v1.16 Subset** — ~25 key endpoints for third-party music app compatibility (DSub, Ultrasonic, play:Sub, Symfonium). | Huge ecosystem of existing Subsonic clients. Implementing the subset covers 95% of use cases without full API burden. |
| 5 | **Non-Destructive Photo Editing** — Edit operations stored as JSON, originals preserved, edited versions generated on demand and cached. | Users never lose originals. Edit history is replayable. Cache invalidation is straightforward. |
| 6 | **Client-Side Equalizer** — Web Audio API in browser. Server stores/retrieves EQ presets only. | No server-side audio processing needed. EQ is inherently a client-side real-time operation. |
| 7 | **Subsonic Auth Bridge** — Users generate a "Subsonic password" in account settings (app-password pattern), mapped to DotNetCloud auth. | Subsonic uses MD5(password+salt) tokens, incompatible with JWT/OIDC. App-password pattern is proven (used by Nextcloud, Navidrome). |
| 8 | **Three Separate Modules** — Photos, Music, Video each run as their own process. | Consistent with architecture. Independent scaling, deployment, and failure isolation. |
| 9 | **No Transcoding by Default** — Direct streaming. Optional transcoding via config (requires server-side FFmpeg). | Transcoding is CPU-intensive. Most modern devices handle common formats natively. Opt-in keeps default deployment simple. |
| 10 | **Map Provider** — Leaflet.js with OpenStreetMap tiles for photo geo-clustering. | Free, no API key required, well-maintained, privacy-respecting. |

---

## Dependency Graph

```
5.1 (Shared Streaming/Types)
├── 5.2 (Metadata Extractors)
│   ├── 5.5 (Photos Services)  ──→ 5.6 (Edit/Slideshow) ──→ 5.7 (Photos API+UI)
│   ├── 5.10 (Music Scanning)  ──→ 5.11 (Music Svcs) ──→ 5.12 (Streaming) ──→ 5.13 (Subsonic) ──┐
│   │                                                                          └→ 5.14 (Music UI) ←┘
│   └── 5.16 (Video Services)  ──→ 5.17 (Video API) ──→ 5.18 (Video UI)
├── 5.3 (Photos Contracts)  ──→ 5.4 (Photos Data)  ──→ 5.5
├── 5.8 (Music Contracts)   ──→ 5.9 (Music Data)   ──→ 5.10
└── 5.15 (Video Contracts+Data) ───────────────────────→ 5.16

All three module tracks can proceed in parallel after 5.1 + 5.2.
5.19 (Integration) depends on 5.7 + 5.14 + 5.18.
5.20 (Testing/Docs) depends on 5.19.
```

---

## Sub-Phase A: Shared Media Infrastructure (5.1–5.2)

### Step 5.1 — Media Streaming Middleware & Shared Types

**Dependencies:** Phase 1 (Files module) complete  
**Status:** ✅ Complete

**Deliverables:**
- ✓ `IMediaStreamingService` interface in `DotNetCloud.Core` — range-request streaming abstraction
- ✓ `MediaStreamingMiddleware` in Core.ServiceDefaults — handles HTTP Range headers, partial content (206), content-type detection
- ✓ Shared media DTOs: `MediaItemDto`, `MediaThumbnailDto`, `MediaMetadataDto`, `GeoCoordinate`
- ✓ `IMediaMetadataExtractor` interface with provider pattern (pluggable per media type)
- ✓ `MediaType` enum (Photo, Audio, Video) added to Core
- ✓ Unit tests for range-request parsing and streaming logic (19 middleware tests + 26 DTO/capability tests)

**Key files to create/modify:**
- `src/Core/DotNetCloud.Core/Capabilities/IMediaStreamingService.cs`
- `src/Core/DotNetCloud.Core/DTOs/Media/` (shared DTOs)
- `src/Core/DotNetCloud.Core.ServiceDefaults/Middleware/MediaStreamingMiddleware.cs`

---

### Step 5.2 — Metadata Extraction Framework

**Dependencies:** Step 5.1  
**Status:** ✅ Complete

**Deliverables:**
- ✓ `ExifMetadataExtractor` — uses ImageSharp for EXIF, GPS, camera info (TryGetValue API for v3.x)
- ✓ `AudioMetadataExtractor` — uses TagLibSharp for ID3v2, Vorbis comments, FLAC tags, album art
- ✓ `VideoMetadataExtractor` — uses FFprobe for duration, resolution, codec, bitrate, audio tracks
- ✓ `IMediaMetadataExtractor` registration pattern (DI-based, keyed by MediaType via `MediaServiceCollectionExtensions`)
- ✓ NuGet: added `TagLibSharp 2.3.0` to ServiceDefaults.csproj
- ✓ Unit tests for each extractor (12 EXIF + 10 audio + 9 video + 7 DI registration tests)

**Key files to create:**
- `src/Core/DotNetCloud.Core.ServiceDefaults/Media/ExifMetadataExtractor.cs`
- `src/Core/DotNetCloud.Core.ServiceDefaults/Media/AudioMetadataExtractor.cs`
- `src/Core/DotNetCloud.Core.ServiceDefaults/Media/VideoMetadataExtractor.cs`

---

## Sub-Phase B: Photos Module (5.3–5.7)

### Step 5.3 — Photos Architecture & Contracts

**Dependencies:** Step 5.1  
**Status:** ✓ Complete

**Deliverables:**
- ✓ `IPhotoDirectory` capability interface (Public tier) — photo/album lookup for other modules
- ✓ Photos DTOs: `PhotoDto`, `AlbumDto`, `PhotoMetadataDto`, `PhotoEditOperationDto`, `GeoClusterDto`
- ✓ Photos events: `PhotoUploadedEvent`, `PhotoDeletedEvent`, `AlbumCreatedEvent`, `AlbumSharedEvent`, `PhotoEditedEvent`
- ✓ `PhotosModuleManifest` (requires: IStorageProvider, IUserDirectory, ICurrentUserContext, INotificationService; publishes photo events; subscribes to FileUploadedEvent)
- ✓ Module project scaffolding: `DotNetCloud.Modules.Photos`, `.Photos.Data`, `.Photos.Data.SqlServer`, `.Photos.Host`
- ✓ Solution file and CI filter updates

**Pattern reference:** Follow `FilesModuleManifest` and `TracksModuleManifest` structure.

---

### Step 5.4 — Photos Data Model

**Dependencies:** Step 5.3  
**Status:** ✓ Complete

**Deliverables:**
- ✓ Entities: `Photo` (links to FileNode ID), `Album`, `AlbumPhoto` (junction), `PhotoMetadata` (EXIF, GPS, camera), `PhotoTag`, `PhotoShare`, `PhotoEditRecord` (non-destructive edit history)
- ✓ EF Core configurations in `Photos.Data/Configuration/`
- ✓ `PhotosDbContext` with schema `photos` (PostgreSQL) / prefix `photos_` (MariaDB)
- ☐ Initial migration
- ✓ Indexes: by user+date, by GPS coordinates (for geo queries), by album, by file node ID

---

### Step 5.5 — Photos Core Services

**Dependencies:** Steps 5.2, 5.4  
**Status:** ✓ Complete

**Deliverables:**
- ✓ `IPhotoService` + `PhotoService` — CRUD, search, timeline queries (photos by date range), favorites
- ✓ `IAlbumService` + `AlbumService` — CRUD, add/remove photos, cover photo, album sharing
- ✓ `IPhotoMetadataService` + `PhotoMetadataService` — EXIF extraction on upload (via ExifMetadataExtractor), auto-rotation based on EXIF orientation
- ✓ `IPhotoGeoService` + `PhotoGeoService` — geo-coordinate clustering (group nearby photos), reverse geocoding lookup (optional, external API)
- ✓ `IPhotoShareService` + `PhotoShareService` — per-photo and per-album sharing with permission levels
- ☐ `IPhotoThumbnailService` — extends Files thumbnail pattern with photo-specific sizes (grid thumb 300px, detail 1200px, full)
- ☐ Background service: `PhotoIndexingService` — watches for new image FileNodes via FileUploadedEvent, extracts metadata, creates Photo records automatically
- ✓ Unit tests: ≥60 tests covering all services (95 tests passing)

---

### Step 5.6 — Photos Editing & Slideshow Services

**Dependencies:** Step 5.5  
**Status:** ✓ Complete

**Deliverables:**
- ✓ `IPhotoEditService` + `PhotoEditService` — non-destructive editing pipeline
  - Operations: crop, rotate (90/180/270), flip (H/V), brightness, contrast, saturation, sharpen, blur
  - Edit stack stored as JSON array of operations on `PhotoEditRecord`
  - `ApplyEditsAsync()` — renders edited version via ImageSharp, caches result
  - `RevertAsync()` — removes edit stack, deletes cached version
- ✓ `ISlideshowService` + `SlideshowService` — create slideshow from album/selection, transition metadata, auto-play interval config
- ✓ Unit tests for edit operations (verify ImageSharp transforms produce correct dimensions, orientation)

---

### Step 5.7 — Photos API, gRPC & Web UI

**Dependencies:** Steps 5.5, 5.6  
**Status:** ✓ Complete (API/gRPC/Host — Blazor UI deferred to integration phase)

**Deliverables:**
- ✓ `PhotosController` — REST endpoints: photos CRUD, album CRUD, search, timeline, geo-clusters, edit operations, share management
- ✓ `PhotosGrpcService` + `photos_service.proto` — inter-module gRPC contract
- ✓ Photos Host project setup (Kestrel, gRPC, health checks)
- ☐ **Blazor UI components:**
  - `PhotoGallery` — masonry/grid layout with infinite scroll, lazy loading
  - `PhotoLightbox` — full-screen viewer with prev/next navigation, zoom, info panel (EXIF)
  - `AlbumManager` — create/edit/delete albums, drag-drop photos into albums
  - `PhotoTimeline` — vertical timeline grouped by year/month/day
  - `PhotoMapView` — map with photo clusters (using Leaflet.js + OpenStreetMap)
  - `PhotoEditor` — crop/rotate/filter controls with live preview
  - `SlideshowPlayer` — full-screen auto-advancing slideshow
- ☐ CSS: Professional gallery styling, responsive grid, lightbox overlay
- ☐ Integration tests (API layer)

---

## Sub-Phase C: Music Module (5.8–5.14)

### Step 5.8 — Music Architecture & Contracts

**Dependencies:** Step 5.1  
**Status:** ☐ Pending

**Deliverables:**
- ☐ `IMusicDirectory` capability interface (Public tier) — artist/album/track lookup
- ☐ Music DTOs: `ArtistDto`, `MusicAlbumDto`, `TrackDto`, `PlaylistDto`, `NowPlayingDto`, `EqPresetDto`, `LibraryScanResultDto`
- ☐ Music events: `TrackPlayedEvent`, `PlaylistCreatedEvent`, `LibraryScanCompletedEvent`, `TrackScrobbledEvent`
- ☐ `MusicModuleManifest`
- ☐ Module project scaffolding: `DotNetCloud.Modules.Music`, `.Music.Data`, `.Music.Data.SqlServer`, `.Music.Host`
- ☐ Solution file and CI filter updates

---

### Step 5.9 — Music Data Model

**Dependencies:** Step 5.8  
**Status:** ☐ Pending

**Deliverables:**
- ☐ Entities: `Artist`, `MusicAlbum`, `Track` (links to FileNode ID), `TrackArtist` (junction, handles multi-artist), `Genre`, `TrackGenre`
- ☐ Entities: `Playlist`, `PlaylistTrack` (ordered), `PlaybackHistory`, `EqPreset`, `UserMusicPreference`
- ☐ Entities: `ScrobbleRecord` (for last.fm-style history), `StarredItem` (favorites — artist/album/track polymorphic)
- ☐ `MusicDbContext` with schema `music`
- ☐ Initial migration
- ☐ Indexes: by artist name, album title, track title, genre, user+last_played

---

### Step 5.10 — Music Library Scanning & Metadata

**Dependencies:** Steps 5.2, 5.9  
**Status:** ☐ Pending

**Deliverables:**
- ☐ `ILibraryScanService` + `LibraryScanService` — scans user's Files for audio MIME types, reads metadata via AudioMetadataExtractor, creates/updates Artist → Album → Track hierarchy
- ☐ `IMusicMetadataService` + `MusicMetadataService` — tag reading/writing, album art extraction/embedding
- ☐ `LibraryScanBackgroundService` — periodic rescan (configurable interval), watches FileUploadedEvent for real-time indexing of new audio files
- ☐ `IAlbumArtService` + `AlbumArtService` — extract embedded art, cache as thumbnails, fallback to folder art (cover.jpg, folder.jpg)
- ☐ Supported formats: MP3, FLAC, OGG, AAC/M4A, OPUS, WAV, WMA
- ☐ Unit tests: ≥50 tests (metadata extraction, library scan logic, artist/album dedup)

---

### Step 5.11 — Music Core Services

**Dependencies:** Step 5.10  
**Status:** ☐ Pending

**Deliverables:**
- ☐ `IArtistService` + `ArtistService` — browse, search, artist detail with discography
- ☐ `IMusicAlbumService` + `MusicAlbumService` — browse, search, album tracks, album art
- ☐ `ITrackService` + `TrackService` — search, starred/favorites, recently added
- ☐ `IPlaylistService` + `PlaylistService` — CRUD, reorder tracks, smart playlists (by genre/year/rating), playlist sharing
- ☐ `IPlaybackService` + `PlaybackService` — track play history, scrobble recording, play queue management
- ☐ `IRecommendationService` + `RecommendationService` — recently played, most played, random by genre, "similar to" (same genre/artist), new additions
- ☐ `IEqPresetService` + `EqPresetService` — CRUD for equalizer presets (JSON of band frequencies + gains)
- ☐ Unit tests: ≥50 tests

---

### Step 5.12 — Music Streaming Service

**Dependencies:** Step 5.11  
**Status:** ☐ Pending

**Deliverables:**
- ☐ `IMusicStreamingService` + `MusicStreamingService` — serve audio files with HTTP Range support for seeking
- ☐ On-the-fly format transcoding (optional): FLAC → MP3/OGG at configurable bitrate for bandwidth-constrained clients
- ☐ Gapless playback metadata (track duration, silence trimming hints)
- ☐ Stream URL generation with auth token (time-limited, user-scoped)
- ☐ Concurrent stream limiting (configurable per-user max streams)
- ☐ Unit + integration tests for streaming endpoints

---

### Step 5.13 — Subsonic API Compatibility

**Dependencies:** Step 5.12  
**Status:** ☐ Pending

**Deliverables:**
- ☐ `SubsonicController` — implements Subsonic REST API v1.16 compatible endpoints
- ☐ Subsonic authentication: username + token (MD5 salt) or password authentication mapped to DotNetCloud auth via app-password bridge
- ☐ **System endpoints:** `ping`, `getLicense`, `getOpenSubsonicExtensions`
- ☐ **Browsing endpoints:** `getArtists`, `getArtist`, `getAlbum`, `getSong`, `getAlbumList2`, `getStarred2`, `getRandomSongs`, `getGenres`
- ☐ **Search:** `search3` (artist/album/track full-text search)
- ☐ **Media retrieval:** `stream` (audio streaming), `getCoverArt` (album/artist art), `download`
- ☐ **Playlists:** `getPlaylists`, `getPlaylist`, `createPlaylist`, `updatePlaylist`, `deletePlaylist`
- ☐ **User interaction:** `star`, `unstar`, `scrobble`
- ☐ XML + JSON response format support (Subsonic uses XML by default, JSON optional)
- ☐ Tested with DSub/Ultrasonic request patterns
- ☐ Integration tests: ≥30 tests covering all implemented endpoints

---

### Step 5.14 — Music gRPC, REST API & Web UI

**Dependencies:** Steps 5.11, 5.12, 5.13  
**Status:** ☐ Pending

**Deliverables:**
- ☐ `MusicController` — REST endpoints for web UI (separate from Subsonic API)
- ☐ `MusicGrpcService` + `music_service.proto`
- ☐ Music Host project setup
- ☐ **Blazor UI components:**
  - `MusicBrowser` — artist/album/genre navigation with sidebar, grid/list toggle
  - `AlbumDetail` — tracklist with play buttons, album art, metadata
  - `NowPlayingBar` — persistent bottom bar: track info, play/pause/skip, progress seek, volume, shuffle/repeat
  - `AudioPlayer` — HTML5 Audio element wrapper with Web Audio API for EQ
  - `PlaylistManager` — create/edit/delete playlists, drag-reorder tracks
  - `QueueView` — current play queue with reorder/remove
  - `EqualizerPanel` — 10-band EQ with presets (Rock, Jazz, Classical, Flat, Custom)
  - `MusicSearch` — instant search across artists/albums/tracks
  - `RecommendationsSidebar` — recently played, top tracks, new additions
- ☐ CSS: Music player styling, album grid, waveform seek bar, responsive layout
- ☐ Integration tests (API layer)

---

## Sub-Phase D: Video Module (5.15–5.18)

### Step 5.15 — Video Architecture, Contracts & Data Model

**Dependencies:** Step 5.1  
**Status:** ☐ Pending

**Deliverables:**
- ☐ `IVideoDirectory` capability interface (Public tier)
- ☐ Video DTOs: `VideoDto`, `VideoCollectionDto`, `SubtitleDto`, `WatchProgressDto`, `VideoMetadataDto`
- ☐ Video events: `VideoAddedEvent`, `VideoDeletedEvent`, `VideoWatchedEvent`
- ☐ `VideoModuleManifest`
- ☐ Entities: `Video` (links to FileNode ID), `VideoMetadata` (duration, resolution, codec, bitrate, audio tracks), `VideoCollection`, `VideoCollectionItem`, `Subtitle` (SRT/VTT), `WatchHistory`, `WatchProgress` (resume position), `VideoShare`
- ☐ `VideoDbContext` with schema `video`
- ☐ Module project scaffolding + initial migration
- ☐ Solution file and CI filter updates

---

### Step 5.16 — Video Core Services

**Dependencies:** Steps 5.2, 5.15  
**Status:** ☐ Pending

**Deliverables:**
- ☐ `IVideoService` + `VideoService` — CRUD, search, recently watched, favorites
- ☐ `IVideoMetadataService` + `VideoMetadataService` — FFprobe metadata extraction on upload via FileUploadedEvent
- ☐ `IVideoCollectionService` + `VideoCollectionService` — organize videos into collections/series
- ☐ `ISubtitleService` + `SubtitleService` — upload/parse SRT and VTT subtitles, associate with videos, serve subtitle tracks
- ☐ `IWatchProgressService` + `WatchProgressService` — track watch position per user per video, resume playback
- ☐ `IVideoThumbnailService` + `VideoThumbnailService` — extend existing FFmpeg thumbnail extraction to generate thumbnail strip (preview thumbnails at intervals for seek hover)
- ☐ `VideoIndexingBackgroundService` — watches for new video FileNodes, extracts metadata, generates thumbnails
- ☐ Unit tests: ≥40 tests

---

### Step 5.17 — Video Streaming & API

**Dependencies:** Step 5.16  
**Status:** ☐ Pending

**Deliverables:**
- ☐ `IVideoStreamingService` + `VideoStreamingService` — HTTP range-request streaming for direct playback
- ☐ Optional: HLS segment generation for adaptive bitrate (deferred — flag for future, initial impl is direct streaming only)
- ☐ Content-type negotiation (MP4, WebM, MKV → browser compatibility detection)
- ☐ `VideoController` — REST endpoints: video CRUD, collections, subtitles, watch progress, streaming URLs
- ☐ `VideoGrpcService` + `video_service.proto`
- ☐ Video Host project setup
- ☐ Integration tests

---

### Step 5.18 — Video Web UI

**Dependencies:** Step 5.17  
**Status:** ☐ Pending

**Deliverables:**
- ☐ **Blazor UI components:**
  - `VideoLibrary` — grid/list view with poster thumbnails, search, filters (recently added, recently watched, collections)
  - `VideoPlayer` — HTML5 video element with custom controls: play/pause, seek with thumbnail preview, volume, fullscreen, subtitle track selector, playback speed
  - `VideoCollectionView` — series/collection page with ordered episodes
  - `WatchProgressIndicator` — visual progress bar overlay on thumbnails
  - `SubtitleUploader` — upload SRT/VTT files, assign to video
- ☐ CSS: Video player chrome, library grid, responsive layout
- ☐ Resume playback: auto-resume from last position on re-open

---

## Sub-Phase E: Integration & Quality (5.19–5.20)

### Step 5.19 — Cross-Module Integration

**Dependencies:** Steps 5.7, 5.14, 5.18  
**Status:** ☐ Pending

**Deliverables:**
- ☐ Photos ↔ Files: `FileUploadedEvent` handler auto-creates Photo records for image MIME types
- ☐ Music ↔ Files: `FileUploadedEvent` handler auto-creates Track records for audio MIME types
- ☐ Video ↔ Files: `FileUploadedEvent` handler auto-creates Video records for video MIME types
- ☐ Cross-module search: media items appear in global search results (via existing search patterns)
- ☐ Shared notification patterns: album shared, playlist shared, video shared → notification service
- ☐ Dashboard widgets: recent photos, now playing, continue watching
- ☐ Navigation integration: media modules in sidebar/app launcher
- ☐ Quota enforcement: media storage counts against user quota via Files module

---

### Step 5.20 — Testing, Performance & Documentation

**Dependencies:** Step 5.19  
**Status:** ☐ Pending

**Deliverables:**
- ☐ **Test suites:**
  - Photos: ≥80 total tests (unit + integration + security)
  - Music: ≥100 total tests (unit + integration + security + Subsonic API)
  - Video: ≥60 total tests (unit + integration + security)
  - Cross-module: ≥20 integration tests
  - Security tests: auth bypass, tenant isolation, sharing permissions, stream URL token validation
  - Performance tests: thumbnail generation throughput, streaming latency, library scan speed, concurrent streams
- ☐ **Documentation:**
  - Admin guide: media module configuration, FFmpeg/FFprobe setup, transcoding options, Subsonic API setup
  - User guide: photo gallery, music player, video player, sharing, playlists
  - API reference: all REST endpoints, Subsonic API mapping, gRPC contracts
- ☐ Update IMPLEMENTATION_CHECKLIST.md and MASTER_PROJECT_PLAN.md

---

## Project Structure

### New Projects to Create

```
src/Modules/
├── Photos/
│   ├── DotNetCloud.Modules.Photos/              # Business logic, DTOs, events
│   ├── DotNetCloud.Modules.Photos.Data/          # EF Core entities, DbContext, migrations
│   ├── DotNetCloud.Modules.Photos.Data.SqlServer/ # SQL Server support
│   └── DotNetCloud.Modules.Photos.Host/          # gRPC host, REST API, Protos
├── Music/
│   ├── DotNetCloud.Modules.Music/                # Business logic, DTOs, events
│   ├── DotNetCloud.Modules.Music.Data/           # EF Core entities, DbContext, migrations
│   ├── DotNetCloud.Modules.Music.Data.SqlServer/  # SQL Server support
│   └── DotNetCloud.Modules.Music.Host/           # gRPC host, REST API, Subsonic API, Protos
└── Video/
    ├── DotNetCloud.Modules.Video/                # Business logic, DTOs, events
    ├── DotNetCloud.Modules.Video.Data/           # EF Core entities, DbContext, migrations
    ├── DotNetCloud.Modules.Video.Data.SqlServer/  # SQL Server support
    └── DotNetCloud.Modules.Video.Host/           # gRPC host, REST API, Protos

tests/
├── DotNetCloud.Modules.Photos.Tests/
├── DotNetCloud.Modules.Music.Tests/
└── DotNetCloud.Modules.Video.Tests/
```

### Existing Files to Modify

| File/Directory | Changes |
|----------------|---------|
| `src/Core/DotNetCloud.Core/Capabilities/` | Add `IPhotoDirectory`, `IMusicDirectory`, `IVideoDirectory`, `IMediaStreamingService` |
| `src/Core/DotNetCloud.Core/Events/` | Add media events (Photo*, Track*, Video*) |
| `src/Core/DotNetCloud.Core/DTOs/` | Add shared media DTOs under `Media/` |
| `src/Core/DotNetCloud.Core.ServiceDefaults/` | Add `MediaStreamingMiddleware`, metadata extractors under `Media/` |
| `DotNetCloud.sln` | Add 12 new module projects + 3 test projects |
| `DotNetCloud.CI.slnf` | Add new projects to CI filter |

### Reference Files (Pattern Templates)

| Pattern | Reference File |
|---------|---------------|
| Module manifest | `src/Modules/Files/DotNetCloud.Modules.Files/FilesModuleManifest.cs` |
| JSON manifest | `src/Modules/Example/manifest.json` |
| gRPC proto | `src/Modules/Files/DotNetCloud.Modules.Files.Host/Protos/files_service.proto` |
| Storage abstraction | `src/Modules/Files/DotNetCloud.Modules.Files/Services/IFileStorageEngine.cs` |
| Thumbnail pattern | `src/Modules/Files/DotNetCloud.Modules.Files/Services/IThumbnailService.cs` |
| FFmpeg integration | `src/Modules/Files/DotNetCloud.Modules.Files/Services/FfmpegVideoFrameExtractor.cs` |
| Background indexing | `src/Modules/Files/DotNetCloud.Modules.Files/Services/ISyncChangeNotifier.cs` |

---

## Parallelism Opportunities

| Parallel Track | Steps | Can Start After |
|----------------|-------|-----------------|
| **Photos track** | 5.3 → 5.4 → 5.5 → 5.6 → 5.7 | 5.1 + 5.2 complete |
| **Music track** | 5.8 → 5.9 → 5.10 → 5.11 → 5.12 → 5.13/5.14 | 5.1 + 5.2 complete |
| **Video track** | 5.15 → 5.16 → 5.17 → 5.18 | 5.1 + 5.2 complete |
| **Subsonic + Music UI** | 5.13 ‖ 5.14 | 5.12 complete |
| **Contracts + Data** | 5.3+5.4, 5.8+5.9, 5.15 | Independent of each other, only need 5.1 |

After completing 5.1 and 5.2, all three module tracks (B, C, D) are fully independent and can be worked in parallel. Within the Music track, steps 5.13 (Subsonic API) and 5.14 (REST + UI) are parallel after 5.12.

---

## Verification Criteria

### Build & Test

1. `dotnet build` — all new + existing projects compile without errors or warnings
2. `dotnet test` — all existing 2700+ tests pass + all new media module tests pass
3. Total new test count target: ≥260 (Photos 80 + Music 100 + Video 60 + Cross-module 20)

### Photos Verification

4. Upload image file via Files → verify Photo record auto-created with EXIF metadata extracted
5. Thumbnails generated at all sizes (300px grid, 1200px detail, full)
6. Photos appear in gallery UI with correct timeline grouping (year/month/day)
7. Create album, add photos, share album → verify permissions enforced for shared user
8. Apply edit (crop + rotate) → verify non-destructive: original preserved, edited version cached, revert restores original
9. Geo-grouping: photos with GPS data cluster on map view correctly
10. Slideshow: plays through album photos with configured transitions

### Music Verification

11. Upload audio files via Files → library scan creates Artist/Album/Track hierarchy with correct metadata (title, artist, album, track number, duration)
12. Album art extracted from embedded tags or folder art, displayed correctly
13. Play track in web UI → streaming with seek (HTTP Range requests working), no buffering gaps
14. Equalizer: adjust bands → audio output changes in real-time (client-side Web Audio API)
15. Playlists: create, reorder, share → all operations persist correctly
16. Connect third-party Subsonic app (DSub or Ultrasonic) → browse artists/albums, search, stream tracks, manage playlists, star/unstar — all functional
17. Scrobble records created on track completion

### Video Verification

18. Upload video file → metadata extracted (duration, resolution, codec), thumbnails generated (poster + seek strip)
19. Video plays in browser with seek (HTTP Range requests), no interruption
20. Subtitles: upload SRT/VTT → displayed correctly during playback, track selector works
21. Watch progress: pause at 5:30, navigate away, return → resumes from 5:30
22. Collections: create series, add videos in order, navigate sequentially

### Cross-Module & Security

23. File quota includes media storage — uploading large media files enforces quota limits
24. Notifications: album shared, playlist shared, video shared → recipient receives notification
25. Dashboard widgets: recent photos, now playing, continue watching — display correct data
26. Media modules appear in sidebar navigation / app launcher
27. **Security:** unauthenticated users cannot access private media
28. **Security:** stream URLs expire after configured timeout
29. **Security:** tenant isolation — user A cannot access user B's media
30. **Security:** sharing permissions enforced — ReadOnly share cannot edit/delete

### Performance

31. Thumbnail generation for 100 photos completes in <30 seconds
32. Music streaming seek latency <500ms
33. Library scan of 1000 audio files completes in <60 seconds
34. Video streaming start time <2 seconds for local network

---

## Decisions Log

| # | Decision | Alternatives Considered | Chosen Because |
|---|----------|------------------------|----------------|
| D-1 | Three separate modules (Photos, Music, Video) | Single combined "Media" module | Process isolation per architecture. Independent scaling and failure domains. Each module can be disabled independently. |
| D-2 | Media items reference FileNode IDs — no separate storage | Duplicate files into media-specific storage | Single source of truth. No storage duplication. Quota enforcement automatic. Files module handles chunked upload, versioning, sharing infrastructure. |
| D-3 | HTTP range-request streaming (no HLS/DASH) | HLS adaptive bitrate, WebRTC | Simpler to implement. Works for direct playback. HLS can be added as enhancement later without breaking changes. Most users are on local networks. |
| D-4 | Subsonic API v1.16 subset (~25 endpoints) | Full Subsonic API (100+ endpoints), no compatibility, Jellyfin API | Covers 95% of use cases. DSub/Ultrasonic/Symfonium only use this subset. Full API is massive with diminishing returns. |
| D-5 | Non-destructive photo editing (JSON edit stack) | Destructive editing (overwrite file), separate edited copies | Users never lose originals. Edit history is replayable and auditable. Storage efficient — only cache edited versions on demand. |
| D-6 | TagLibSharp for audio metadata | NAudio, FFprobe for audio tags, custom parser | TagLibSharp is the .NET standard for audio metadata. Supports all major formats. Well-maintained, Apache 2.0 license. |
| D-7 | Client-side EQ (Web Audio API) | Server-side audio processing, no EQ | No server CPU cost. Real-time adjustment. Natural fit for browser playback. Server just stores presets. |
| D-8 | Subsonic auth via app-password bridge | Map Subsonic tokens directly to JWT, shared credentials | Clean separation. App passwords are revocable. Doesn't expose primary credentials. Proven pattern (Nextcloud, Navidrome). |
| D-9 | Leaflet.js + OpenStreetMap for photo maps | Google Maps, Mapbox, no map view | Free, no API key. Privacy-respecting (no Google tracking). Open source. Well-maintained. |
| D-10 | Defer Android auto-upload | Include in Phase 5 | Server-side upload API is already generic (Files module). Auto-upload is a sync client feature requiring MAUI work on a different machine. Cleaner to handle separately. |

---

## Open Considerations

### 1. Reverse Geocoding for Photo Locations

Photos with GPS coordinates could show location names (e.g., "Paris, France" instead of "48.8566° N, 2.3522° E"). Options:
- **Nominatim (OpenStreetMap)** — free, self-hostable, rate-limited on public instance
- **Offline geocoding database** — bundle a lightweight reverse geocoding dataset
- **Skip for now** — show coordinates only, add location names later

**Recommendation:** Start with coordinates only. Add Nominatim integration as optional configuration later. Don't block Phase 5 on this.

### 2. Music Lyrics Support

Not in current scope but frequently requested:
- Embedded lyrics (USLT/SYLT ID3 frames) — TagLibSharp can read these
- External .lrc files (timed lyrics)
- Synced lyrics display during playback

**Recommendation:** Defer to Phase 5 polish or Phase 8 (Search & Polish). Can be added without schema changes — lyrics are in existing tag data.

### 3. Video Transcoding Pipeline

Current plan is direct streaming only. For broader device compatibility:
- Server-side FFmpeg transcoding queue for format conversion
- Multiple quality presets (1080p, 720p, 480p)
- Background transcoding on upload

**Recommendation:** Defer. Direct streaming of MP4/WebM covers 95% of cases. Transcoding adds significant complexity and CPU requirements. Flag for future enhancement.

### 4. Smart Playlist Complexity

Current plan: single-criteria presets (Recently Added, Most Played, Random by Genre). Full query builder would support:
- Multiple criteria with AND/OR logic
- Dynamic updates (playlist auto-refreshes when matching tracks change)
- Rule-based: "tracks added in last 30 days AND genre is Jazz AND rating ≥ 4"

**Recommendation:** Start with presets. Full query builder is Phase 5+ or Phase 8. The data model supports it — just need the UI and query engine.

### 5. Podcast Support

The Music module's infrastructure (audio streaming, metadata, playlists) could extend to podcasts:
- RSS feed import
- Episode tracking (listened/unlistened)
- Auto-download new episodes

**Recommendation:** Defer entirely. Podcasts are a different domain (feed management, episode scheduling). Could be a separate Phase 6+ module or Music module extension.
