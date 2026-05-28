# Media Content Deduplication Plan (Music + Video)

> **Status:** Implemented ✅ (Phases 1-3 complete)  
> **Date:** 2026-05-28  
> **Scope:** Music + Video modules  
> **Approach:** Full canonical data model with content-addressed binary cache

---

**TL;DR:** Music and Video modules duplicate metadata + binary assets per-user for the same content. Restructure both modules with content-addressed canonical tables (keyed by ContentHash) so intrinsic media properties are stored once. Per-user tables become lightweight junctions holding only user-specific data (play count, watch progress, favorites). Binary assets (album art, posters, thumbnails) move to a shared content-addressed filesystem cache.

---

## Key Observations from Research

**Current duplication in Music** (fully per-user, despite cross-owner clone):

- `music.Artists` — every user who indexes "The Beatles" gets a duplicate row (same name, same MusicBrainzId)
- `music.Albums` — every user who indexes the same album gets a duplicate row with identical CoverArtPath pointing to a per-album-ID file in `.album-art/`
- `music.Tracks` — every user who indexes the same audio file gets a duplicate row (same Title, DurationTicks, Bitrate, etc.)
- _Cross-owner cloning exists_ (`LibraryScanService.TryIndexFromExistingOwnerAsync`) but still creates per-user copies of everything — it just skips TagLib re-extraction

**Current duplication in Video** (fully per-user, no cross-owner mechanism at all):

- `video.Videos` — every user who indexes the same video gets a duplicate row with identical `ThumbnailPoster` blob (JPEG bytes stored inline!), same TMDB enrichment data, same technical metadata
- `video.VideoMetadata` — same resolution/codec/bitrate duplicated per-user
- `video.VideoSeries`, `video.VideoSeason` — same series info duplicated per-user
- _No cross-owner detection exists_ — each user independently creates everything

**What's truly per-user (keep as-is):**

- Music: `PlayCount` (on Track), `PlaybackHistory`, `Playlist`/`PlaylistTrack`, `StarredItem`, `ScrobbleRecord`, `EqPreset`, `UserMusicPreference`
- Video: `WatchProgress` (resume position), `WatchHistory`, `IsFavorite`, `ViewCount`, `VideoShare`
- Both: FileNodeId mapping (each user may see the same file via a different FileNode)

---

## Design: Canonical Content Model

### Identity Model

Content is identified by **ContentHash** (SHA-256 of file bytes), which already exists in the Files module. Canonical records are keyed by ContentHash first, with secondary fallback logic for cases where hash isn't available.

```
ContentHash (SHA-256) → Canonical Record (shared, no OwnerId)
                            ↕
                    User Track/Video (per-user, with OwnerId)
```

### Canonical Entities

**Music Canonical:**

| Table                           | Key Fields                      | Intrinsic Data                                                                                                                             |
| ------------------------------- | ------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| `music.canonical_tracks`        | ContentHash (PK)                | Title, TrackNumber, DiscNumber, DurationTicks, Bitrate, SampleRate, Channels, MimeType, Year, MusicBrainzRecordingId, ISRC, BPM, Composers |
| `music.canonical_albums`        | ContentHash-derived composite   | Title, Year, HasCoverArt, CoverArtPath (→ `media-cache`), TotalDurationTicks, MusicBrainzReleaseGroupId/ReleaseId                          |
| `music.canonical_artists`       | MusicBrainzId or name composite | Name, SortName, Biography, ImageUrl, WikipediaUrl, DiscogsUrl, OfficialUrl                                                                 |
| `music.canonical_genres`        | Name (unique)                   | Name                                                                                                                                       |
| `music.canonical_track_artists` | Junction                        | IsPrimary                                                                                                                                  |
| `music.canonical_track_genres`  | Junction                        | —                                                                                                                                          |
| `music.canonical_album_artists` | Junction                        | IsPrimary                                                                                                                                  |

**Video Canonical:**

| Table                      | Key Fields               | Intrinsic Data                                                                                                                                                                               |
| -------------------------- | ------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `video.canonical_videos`   | ContentHash (PK)         | Title, FileName, MimeType, SizeBytes, DurationTicks, ThumbnailPosterHash (→ `media-cache`), HasExternalPoster, EmbeddedTitle, EmbeddedImdbId, EmbeddedTmdbId, EmbeddedDate, EmbeddedLanguage |
| `video.canonical_metadata` | ContentHash (FK)         | Width, Height, FrameRate, VideoCodec, AudioCodec, Bitrate, AudioTrackCount, SubtitleTrackCount, ContainerFormat                                                                              |
| `video.canonical_tmdb`     | TmdbId (PK)              | TmdbTitle, Overview, ReleaseDate, TmdbRating, Genres, ExternalPosterHash (→ `media-cache`)                                                                                                   |
| `video.canonical_series`   | TmdbId or name composite | Name, Description, Type, TmdbName, TmdbOverview, Genres, Status, TotalSeasons, TotalEpisodes, PosterHash                                                                                     |
| `video.canonical_seasons`  | SeriesId + SeasonNumber  | Name, Overview, EpisodeCount, AirDate, PosterHash                                                                                                                                            |
| `video.canonical_episodes` | SeasonId + EpisodeNumber | Title, Overview, VideoContentHash (→ canonical_videos)                                                                                                                                       |

### Per-User Junction Tables (modified)

**Music User Tables (replace current per-user tables):**

| Table                          | Changes                                                                                                                                                                                                                            |
| ------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `music.user_tracks`            | Replaces `music.tracks`. Columns: Id, UserId, FileNodeId, CanonicalTrackHash (FK→canonical_tracks), CanonicalAlbumId (FK→canonical_albums), ContentHash, PlayCount, IsDeleted, Dates. REMOVED: Title, DurationTicks, Bitrate, etc. |
| `music.user_artists`           | Replaces `music.artists`. Columns: Id, UserId, CanonicalArtistId, IsDeleted, Dates. REMOVED: Name, SortName, Biography, MusicBrainzId, etc.                                                                                        |
| `music.user_albums`            | Replaces `music.albums`. Columns: Id, UserId, CanonicalAlbumId, IsDeleted, Dates. REMOVED: Title, Year, TotalDurationTicks, CoverArtPath, HasCoverArt, etc.                                                                        |
| `music.playback_history`       | Keep as-is (already user-scoped)                                                                                                                                                                                                   |
| `music.playlists`              | Keep as-is (already user-scoped)                                                                                                                                                                                                   |
| `music.playlist_tracks`        | Keep as-is (already user-scoped)                                                                                                                                                                                                   |
| `music.starred_items`          | Keep as-is (already user-scoped)                                                                                                                                                                                                   |
| `music.eq_presets`             | Keep as-is (already user-scoped)                                                                                                                                                                                                   |
| `music.user_music_preferences` | Keep as-is (already user-scoped)                                                                                                                                                                                                   |
| `music.scrobble_records`       | Keep as-is (already user-scoped)                                                                                                                                                                                                   |

**Video User Tables (replace current per-user tables):**

| Table                      | Changes                                                                                                                                                                                                                                                                                                                                                |
| -------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `video.user_videos`        | Replaces `video.videos`. Columns: Id, UserId, FileNodeId, CanonicalContentHash (FK→canonical_videos), IsFavorite, ViewCount, IsDeleted, Dates. REMOVED: Title, FileName, MimeType, SizeBytes, DurationTicks, ThumbnailPoster blob, TmdbId, TmdbTitle, Overview, ReleaseDate, TmdbRating, Genres, HasExternalPoster, ExternalPosterPath, LastEnrichedAt |
| `video.watch_progress`     | Keep as-is (already user-scoped)                                                                                                                                                                                                                                                                                                                       |
| `video.watch_history`      | Keep as-is (already user-scoped)                                                                                                                                                                                                                                                                                                                       |
| `video.video_shares`       | Keep as-is (already user-scoped)                                                                                                                                                                                                                                                                                                                       |
| `video.user_collections`   | New: UserId + CanonicalCollection junction                                                                                                                                                                                                                                                                                                             |
| `video.subtitles`          | Move to canonical (subtitles are intrinsic to the file)                                                                                                                                                                                                                                                                                                |
| `video.video_series_items` | Junction: SeriesId → canonical_video via ContentHash                                                                                                                                                                                                                                                                                                   |
| `video.video_episodes`     | Junction: SeasonId → canonical_video via ContentHash                                                                                                                                                                                                                                                                                                   |

### Content-Addressed Binary Cache

New filesystem location: `{Files:Storage:RootPath}/.media-cache/`

Structure:

```
.media-cache/
  images/
    ab/
      abc123def456...jpg   (SHA-256 hash of the image content)
      abc123def456...png
    12/
      123456789abc...webp
  metadata/
    ab/
      abc123def456...json   (cached ffprobe output, etc.)
```

Binary assets stored once, referenced by hash:

- Album art: `music.canonical_albums.CoverArtPath` → `{media-cache}/images/{hash[0:2]}/{hash}.jpg`
- Video poster: `video.canonical_videos.ThumbnailPosterHash` → same
- TMDB poster: `video.canonical_tmdb.ExternalPosterHash` → same

---

## Phases & Steps

### ✅ Phase 1: Foundation — Shared Cache Infrastructure (Complete)

**Step 1.1 — Create media cache directory and utility**

- _Files affected:_ New file or add to existing Files module infrastructure
- New config key `Files:Storage:MediaCachePath` (default: `{Files:Storage:RootPath}/.media-cache`)
- New utility class `ContentAddressedStorage` with methods:
  - `StoreAsync(Stream data, string extension)` → returns hash-based path
  - `GetPath(string contentHash, string extension)` → returns full path
  - `Exists(string contentHash)` → checks if content is cached
  - `Delete(string contentHash)` → removes from cache
- Uses SHA-256 hash, 2-level directory prefix (first 2 chars)

**Step 1.2 ✅ — Add ContentHash tracking to Video module**

- _Files affected:_ `src/Modules/Video/DotNetCloud.Modules.Video.Data/Models/Video.cs`
- Add `ContentHash` property (string, max 64 chars) to Video entity
- Update `VideoConfiguration.cs` to add the column + index
- _Dependency for:_ Phase 3 (Video needs ContentHash for canonical lookup)

---

### ✅ Phase 2: Music Canonical Deduplication (Complete)

**Step 2.1 ✅ — Create canonical Music tables**

- _Files affected:_
  - `src/Modules/Music/DotNetCloud.Modules.Music.Data/MusicDbContext.cs` — add new DbSets
  - New files in `src/Modules/Music/DotNetCloud.Modules.Music.Data/Models/`:
    - `CanonicalTrack.cs`
    - `CanonicalAlbum.cs`
    - `CanonicalArtist.cs`
    - `CanonicalGenre.cs`
    - `CanonicalTrackArtist.cs`
    - `CanonicalTrackGenre.cs`
    - `CanonicalAlbumArtist.cs`
  - New files in `src/Modules/Music/DotNetCloud.Modules.Music.Data/Configuration/`:
    - `CanonicalTrackConfiguration.cs`, etc.
- New tables in `music` schema, no OwnerId column
- Unique constraints:
  - `canonical_tracks` — `ContentHash` unique
  - `canonical_artists` — `Name` + `MusicBrainzId` (nullable)
  - `canonical_albums` — `Title` + composite from canonical artist
  - `canonical_genres` — `Name` unique

**Step 2.2 ✅ — Create per-user junction tables for Music**

- _Files affected:_
  - `MusicDbContext.cs` — add/replace DbSets
  - New/modified model files:
    - `UserTrack.cs` — replaces `Track.cs` (Id, UserId, FileNodeId, CanonicalTrackHash, CanonicalAlbumId, ContentHash, PlayCount, IsDeleted, dates)
    - `UserArtist.cs` — Id, UserId, CanonicalArtistId, IsDeleted, dates
    - `UserAlbum.cs` — Id, UserId, CanonicalAlbumId, IsDeleted, dates
- Update EF Core configurations for new tables
- Update `TrackArtist` → `UserTrackArtist` if needed
- Update `TrackGenre` → `UserTrackGenre` if needed

**Step 2.3 — Update LibraryScanService**

- _Files affected:_ `src/Modules/Music/DotNetCloud.Modules.Music.Data/Services/LibraryScanService.cs`
- `IndexFileAsync`: First check canonical table by ContentHash. If canonical record exists, create user junction + link. If not, extract metadata, create canonical record first, then create user junction.
- `TryIndexFromExistingOwnerAsync`: Simplify — now just looks up canonical track by hash, creates user junction. No more per-user cloning.
- `TryBulkIndexFromExistingAsync`: Same simplification.
- Remove per-scan `_artistCache` / `_albumCache` / `_genreCache` (canonical records are global so lookups are simpler)

**Step 2.4 — Update AlbumArtService + MetadataEnrichmentService**

- _Files affected:_
  - `src/Modules/Music/DotNetCloud.Modules.Music.Data/Services/AlbumArtService.cs`
  - `src/Modules/Music/DotNetCloud.Modules.Music.Data/Services/MetadataEnrichmentService.cs`
- Album art stored in `.media-cache/images/` keyed by hash of the image data, not by album ID
- `CacheArtData()` → uses `ContentAddressedStorage.StoreAsync()`
- `CopyArtFromExisting()` → just returns the existing hash path (no copy needed since it's content-addressed)
- Enrichment writes to canonical tables, not per-user tables

**Step 2.5 — Update services that query music data**

- _Files affected:_
  - `src/Modules/Music/DotNetCloud.Modules.Music.Data/Services/MusicAlbumService.cs`
  - `src/Modules/Music/DotNetCloud.Modules.Music.Data/Services/MusicTrackService.cs` (if exists)
  - Any gRPC service impl that maps to DTOs
- Queries join canonical + user tables
- DTO mapping pulls from canonical for shared data + user table for per-user data

**Step 2.6 — Migrate Music database (reset approach)**

- Create new migration with new tables
- Drop old per-user tables (since we're starting fresh)
- Keep: `playback_history`, `playlists`, `playlist_tracks`, `starred_items`, `eq_presets`, `user_music_preferences`, `scrobble_records`
- _Parallel with Step 2.7_

**Step 2.7 — Generate SQL Server migrations for Music**

- _Files affected:_ `src/Modules/Music/DotNetCloud.Modules.Music.Data.SqlServer/Migrations/`
- Run `dotnet ef migrations add` for SQL Server

---

### ✅ Phase 3: Video Canonical Deduplication (Complete)

**Step 3.1 ✅ — Create canonical Video tables**

- _Files affected:_
  - `src/Modules/Video/DotNetCloud.Modules.Video.Data/VideoDbContext.cs` — add new DbSets
  - New files in `src/Modules/Video/DotNetCloud.Modules.Video.Data/Models/`:
    - `CanonicalVideo.cs` — ContentHash PK, Title, FileName, MimeType, SizeBytes, DurationTicks, ThumbnailPosterHash
    - `CanonicalVideoMetadata.cs` — ContentHash FK, Width, Height, FrameRate, codecs, bitrate
    - `CanonicalTmdbData.cs` — TmdbId PK, title, overview, release date, rating, genres, poster hash
    - `CanonicalVideoSeries.cs` — series info, poster hash
    - `CanonicalVideoSeason.cs` — season info, poster hash
    - `CanonicalVideoEpisode.cs` — episode info, VideoContentHash FK
    - `CanonicalSubtitle.cs` — subtitles (intrinsic to the file)
  - New configuration files in `src/Modules/Video/DotNetCloud.Modules.Video.Data/Configuration/`

**Step 3.2 — Create per-user junction table for Video**

- _Files affected:_
  - Rename/modify `Video.cs` → `UserVideo.cs`
  - Columns: Id, UserId, FileNodeId, CanonicalContentHash, IsFavorite, ViewCount, IsDeleted, dates
  - All intrinsic properties removed (they're on CanonicalVideo/TmdbData)

**Step 3.3 — Create per-user Video collection tables**

- _Files affected:_
  - `UserVideoCollection.cs` — UserId, collection name, description
  - `UserVideoCollectionItem.cs` — junction: UserCollectionId → CanonicalContentHash
- Series/seasons/episodes are mostly canonical (TV show metadata is the same for everyone)
- But which episodes a user has in their library is per-user (junction from canonical episode to user's video)

**Step 3.4 — Update VideoThumbnailService**

- _Files affected:_ `src/Modules/Video/DotNetCloud.Modules.Video.Data/Services/VideoThumbnailService.cs`
- Generate thumbnail → store in `.media-cache/images/` by content hash
- Store hash path in canonical table, not blob in per-user table
- `GetThumbnailAsync`: lookup canonical record → serve from filesystem cache
- `ExtractMetadataAsync`: write to canonical metadata table (check if exists first)

**Step 3.5 — Update VideoService + VideoIndexingCallback**

- _Files affected:_
  - `src/Modules/Video/DotNetCloud.Modules.Video.Data/Services/VideoService.cs`
  - `src/Modules/Video/DotNetCloud.Modules.Video.Data/Services/VideoIndexingCallback.cs`
- `CreateVideoAsync`: check canonical table by ContentHash first. If exists, create UserVideo junction + link. If not, create canonical record → create UserVideo.
- Add cross-owner/content-hash lookup (mirroring what Music already does, but now operating on canonical records)
- Update `IVideoIndexingCallback` interface if needed

**Step 3.6 — Update Video series/enrichment services**

- _Files affected:_
  - `src/Modules/Video/DotNetCloud.Modules.Video.Data/Services/VideoEnrichmentService.cs`
  - `src/Modules/Video/DotNetCloud.Modules.Video.Data/Services/VideoSeriesService.cs`
  - `src/Modules/Video/DotNetCloud.Modules.Video/Services/IVideoEnrichmentService.cs`
  - `src/Modules/Video/DotNetCloud.Modules.Video/Services/IVideoSeriesService.cs`
- TMDB enrichment writes to canonical_tmdb table (shared by all users for the same TMDB ID)
- Series creation writes to canonical_series table
- Per-user series membership is handled via junction

**Step 3.7 — Migrate Video database (reset approach)**

- Create new migration with new tables
- Drop old per-user tables (videos, metadata, subtitles, series, seasons, episodes)
- Keep: `watch_progress`, `watch_history`, `video_shares`, `user_collections`

---

### Phase 4: Enrichment Improvements

**Step 4.1 — Extract embedded MusicBrainz IDs from audio file tags**

- _Files affected:_ `src/Modules/Music/DotNetCloud.Modules.Music.Data/Services/MusicMetadataService.cs`
- TagLib already reads audio tags. Currently extracts: Title, Artist, AlbumArtist, Album, TrackNumber, DiscNumber, Year, Genre. Also read the following from TagLib custom/FLAC/Vorbis tags (TagLib exposes these via `tag.TagTypes` or as custom `TXXX` frames):
  - `MUSICBRAINZ_TRACK_ID` → `CanonicalTrack.MusicBrainzRecordingId`
  - `MUSICBRAINZ_ARTIST_ID` → `CanonicalArtist.MusicBrainzId`
  - `MUSICBRAINZ_ALBUM_ID` → `CanonicalAlbum.MusicBrainzReleaseId` (this IS a release MBID, not release group)
  - `MUSICBRAINZ_RELEASE_GROUP_ID` → `CanonicalAlbum.MusicBrainzReleaseGroupId`
  - `MUSICBRAINZ_RELEASE_ARTIST_ID` → secondary artist match verification
  - `MUSICBRAINZ_DISC_ID` → disc validation
  - `ISRC` → store on `CanonicalTrack` for future enhanceability
  - `BPM` → store on `CanonicalTrack` for BPM analysis features
  - `Composer` → store on `CanonicalTrack` for classical music support
- Update `AudioMetadata` DTO to include these new fields
- Update `BuildMetadata()` to populate them
- Add DB columns to `canonical_tracks` (MusicBrainzRecordingId already exists, add ISRC, BPM, Composers)
- Add to `canonical_artists` (MusicBrainzId already exists, add ReleaseArtistId)
- _Why this matters:_ If a user tags their files with MusicBrainz Picard or beets, the files already contain the exact MBIDs for every entity. Extracting them from tags means we can do DIRECT API lookups (`/recording/{mbid}`) instead of text searches, giving 100% accurate matches instantly.

**Step 4.2 — Extract container-level metadata tags from video files via ffprobe**

- _Files affected:_ `src/Modules/Video/DotNetCloud.Modules.Video.Data/Services/VideoThumbnailService.cs` (ExtractMetadataAsync)
- Current ffprobe command: `ffprobe -v quiet -print_format json -show_format -show_streams {file}`
- The `format.tags` object in ffprobe JSON output contains container-level metadata that is completely ignored today. Add extraction of:
  - `format.tags.title` → better search query than filename-derived title
  - `format.tags.date` / `format.tags.creation_time` → movie year filter
  - `format.tags.IMDB` / `format.tags.imdb` → IMDB ID (can cross-reference with TMDB external IDs)
  - `format.tags.TMDB` / `format.tags.tmdb` → direct TMDB ID match, skip search entirely!
  - `format.tags.artist` → studio/performer (content type disambiguation)
  - `format.tags.composer` → score composer (additional search axis)
  - `format.tags.genre` → confidence verification against TMDB result
  - `format.tags.language` → region/language inference for TMDB searches
  - `format.tags.synopsis` / `format.tags.description` → match confidence verification
- Store extracted tags in `canonical_videos` (add columns: `EmbeddedTitle`, `EmbeddedImdbId`, `EmbeddedTmdbId`, `EmbeddedDate`, `EmbeddedLanguage`)
- _Why this matters:_ Many video files (from MakeMKV, HandBrake, or Plex-compatible naming tools) have IMDB IDs or even TMDB IDs embedded in container metadata. If present, these allow instant exact matches.

**Step 4.3 — Use embedded MBIDs for direct MusicBrainz lookups (skip text search)**

- _Files affected:_
  - `src/Modules/Music/DotNetCloud.Modules.Music.Data/Services/MetadataEnrichmentService.cs`
  - `src/Modules/Music/DotNetCloud.Modules.Music/Services/IMusicBrainzClient.cs`
- Add new method: `IMusicBrainzClient.GetRecordingByMbidAsync(mbid)` — direct lookup
- Add new method: `IMusicBrainzClient.GetArtistByMbidAsync(mbid)` — already exists as `GetArtistAsync`
- Add new method: `IMusicBrainzClient.GetReleaseGroupByMbidAsync(mbid)` — already exists as `GetReleaseGroupAsync`
- **Enrichment priority order for each entity type:**

  **Track enrichment (EnrichTrackAsync):**
  1. If `CanonicalTrack.MusicBrainzRecordingId` is already populated (from tags or prior enrichment) → verify with `/recording/{mbid}` GET (includes `inc=artists+releases`). No search needed.
  2. If artist MBID known AND recording MBID unknown → search with `arid:{mbid} AND recording:"{title}" AND dur:{±duration}` — much more precise than text-only search.
  3. Fallback: text search as today, but add `AND dur:{duration_ms}` filter.
  4. After match, verify recording's `length` against track's `DurationTicks` (±2s tolerance). Reject matches outside tolerance.

  **Album enrichment (EnrichAlbumAsync):**
  1. If `CanonicalAlbum.MusicBrainzReleaseGroupId` populated → direct GET, skip search.
  2. If `CanonicalAlbum.MusicBrainzReleaseId` populated → GET release details, get release group from it.
  3. If artist MBID known → search with `arid:{mbid} AND releasegroup:"{title}"`.
  4. Fallback: text search as today, but filter results by `primary-type` preferring "Album" over "Single"/"EP"/"Compilation".
  5. Add year filter when track tags have a year.

  **Artist enrichment (EnrichArtistAsync):**
  1. If `CanonicalArtist.MusicBrainzId` populated → direct GET.
  2. Fallback: text search as today.

**Step 4.4 — Improve TMDB search queries**

- _Files affected:_
  - `src/Modules/Video/DotNetCloud.Modules.Video.Data/Services/VideoEnrichmentService.cs`
  - `src/Modules/Video/DotNetCloud.Modules.Video.Data/Services/TmdbClient.cs`
  - `src/Modules/Video/DotNetCloud.Modules.Video/Services/ITmdbClient.cs`
- Add `&include_adult=false` to all TMDB search calls
- Replace `&year=` with `&primary_release_year=` for movies (more lenient matching — accepts partial dates)
- Source year from embedded metadata first (ffprobe `format.tags.date`), then from folder context, then from filename regex
- Fix `ExtractYear()` to not match the first 4-digit number in any filename (e.g., "The 400 Blows.mp4" → year=400). Strategies:
  - Prefer year from embedded metadata (ffprobe tags)
  - Prefer year from parent folder name patterns (e.g., `Movies/2022/`)
  - Prefer years in common movie year positions (before `p`/`i` resolution markers: `Movie.2022.1080p.mkv`)
  - Use regex `(?<![0-9])(19[0-9]{2}|20[0-9]{2})(?![0-9])` isolated from other numbers
- Add `TMDBClient.SearchMovieByImdbIdAsync(imdbId)` — TMDB supports `/find/{imdbId}?external_source=imdb_id` for cross-referencing IMDB IDs found in container metadata
- Add enrichment priority:
  1. If `EmbeddedTmdbId` present → direct `/movie/{tmdbId}` GET, skip search
  2. If `EmbeddedImdbId` present → use `/find/{imdbId}?external_source=imdb_id` for exact match
  3. Search by title + year (improved) as above
  4. After match, verify genre consistency against embedded `genre` tag if available (log warnings on mismatch)

**Step 4.5 — Improve TV series TMDB enrichment**

- _Files affected:_ `src/Modules/Video/DotNetCloud.Modules.Video.Data/Services/VideoEnrichmentService.cs`
- Add `&first_air_date_year=` filter when year can be determined from folder context or embedded metadata
- Add `&include_adult=false`
- For season enrichment, add episode-level matching: after series + season are identified, match each user's episode video to a canonical episode by episode number. Store the `CanonicalContentHash` reference so multiple users who have the same episode file share one canonical record.

---

### Phase 5: Service-Layer Updates & Verification

**Step 5.1 — Update DTO mapping for both modules**

- All DTOs that return video/music data must join canonical + user tables
- Key DTOs to check:
  - `VideoDto` — map from CanonicalVideo + UserVideo + CanonicalTmdbData
  - Music track/album/artist DTOs — map from canonical + user junction

**Step 5.2 — Update gRPC service implementations**

- Both Video and Music gRPC services that query/return content

**Step 5.3 — Build and test**

- `dotnet build DotNetCloud.CI.slnf`
- `dotnet test`
- Separate unit test runs for Music and Video module tests

---

## Relevant Files

### Music Module

- `src/Modules/Music/DotNetCloud.Modules.Music.Data/MusicDbContext.cs` — DbSets for new canonical + user tables
- `src/Modules/Music/DotNetCloud.Modules.Music.Data/Models/Track.cs` → split into CanonicalTrack + UserTrack
- `src/Modules/Music/DotNetCloud.Modules.Music.Data/Models/Artist.cs` → split into CanonicalArtist + UserArtist
- `src/Modules/Music/DotNetCloud.Modules.Music.Data/Models/MusicAlbum.cs` → split into CanonicalAlbum + UserAlbum
- `src/Modules/Music/DotNetCloud.Modules.Music.Data/Models/TrackArtist.cs` → CanonicalTrackArtist
- `src/Modules/Music/DotNetCloud.Modules.Music.Data/Models/TrackGenre.cs` → CanonicalTrackGenre
- `src/Modules/Music/DotNetCloud.Modules.Music.Data/Models/Genre.cs` → CanonicalGenre
- `src/Modules/Music/DotNetCloud.Modules.Music.Data/Services/LibraryScanService.cs` — major rework
- `src/Modules/Music/DotNetCloud.Modules.Music.Data/Services/AlbumArtService.cs` — use content-addressed cache
- `src/Modules/Music/DotNetCloud.Modules.Music.Data/Services/MusicAlbumService.cs` — query canonical tables
- `src/Modules/Music/DotNetCloud.Modules.Music.Data/Services/MusicMetadataService.cs` — extract MBIDs, ISRC, BPM, Composer from tags
- `src/Modules/Music/DotNetCloud.Modules.Music.Data/Services/MetadataEnrichmentService.cs` — embedded MBID priority, duration matching, type filtering
- `src/Modules/Music/DotNetCloud.Modules.Music.Data/Services/MusicBrainzClient.cs` — new direct-by-MBID methods
- `src/Modules/Music/DotNetCloud.Modules.Music/Services/IMusicBrainzClient.cs` — interface additions for MBID lookups
- Configuration files in `src/Modules/Music/DotNetCloud.Modules.Music.Data/Configuration/`
- PostgreSQL/SQL Server migration folders

### Video Module

- `src/Modules/Video/DotNetCloud.Modules.Video.Data/VideoDbContext.cs` — DbSets for new canonical + user tables
- `src/Modules/Video/DotNetCloud.Modules.Video.Data/Models/Video.cs` → split into CanonicalVideo + UserVideo
- `src/Modules/Video/DotNetCloud.Modules.Video.Data/Models/VideoMetadata.cs` → CanonicalVideoMetadata
- `src/Modules/Video/DotNetCloud.Modules.Video.Data/Models/VideoSeries.cs` → CanonicalVideoSeries
- `src/Modules/Video/DotNetCloud.Modules.Video.Data/Models/VideoSeason.cs` → CanonicalVideoSeason
- `src/Modules/Video/DotNetCloud.Modules.Video.Data/Models/VideoEpisode.cs` → CanonicalVideoEpisode
- `src/Modules/Video/DotNetCloud.Modules.Video.Data/Models/VideoSeriesItem.cs` → CanonicalVideoSeriesItem
- `src/Modules/Video/DotNetCloud.Modules.Video.Data/Models/Subtitle.cs` → CanonicalSubtitle
- `src/Modules/Video/DotNetCloud.Modules.Video.Data/Services/VideoService.cs` — content-hash lookup, canonical
- `src/Modules/Video/DotNetCloud.Modules.Video.Data/Services/VideoThumbnailService.cs` — content-addressed cache, extract format.tags
- `src/Modules/Video/DotNetCloud.Modules.Video.Data/Services/VideoIndexingCallback.cs` — cross-owner by content hash
- `src/Modules/Video/DotNetCloud.Modules.Video.Data/Services/VideoEnrichmentService.cs` — canonical TMDB, IMDB cross-ref, ExtractYear fix
- `src/Modules/Video/DotNetCloud.Modules.Video.Data/Services/TmdbClient.cs` — new SearchMovieByImdbIdAsync, include_adult, primary_release_year
- `src/Modules/Video/DotNetCloud.Modules.Video.Data/Services/VideoSeriesService.cs` — canonical series
- `src/Modules/Video/DotNetCloud.Modules.Video/Services/ITmdbClient.cs` — interface additions
- `src/Modules/Video/DotNetCloud.Modules.Video/Services/IVideoEnrichmentService.cs` — interface updates
- Configuration files in `src/Modules/Video/DotNetCloud.Modules.Video.Data/Configuration/`
- PostgreSQL/SQL Server migration folders

### Shared Infrastructure

- New utility: `ContentAddressedStorage` (add to `DotNetCloud.Core` or `DotNetCloud.Core.Data`)
- Config: `Files:Storage:MediaCachePath` in app settings

---

## Verification

1. **Build:** `dotnet build DotNetCloud.CI.slnf -c Release` — zero errors
2. **Tests:** `dotnet test` — all existing tests pass (update tests for new schema)
3. **DB Schema:** Inspect migrations — canonical tables have no OwnerId, user tables have OwnerId + FK to canonical
4. **Duplicate detection:** Index same music file as User A → User B: only 1 canonical_track, 2 user_tracks. Before = 2 tracks with all data duplicated.
5. **Binary dedup:** Same album art for two albums: only 1 file in `.media-cache/images/`. Before = 2 files in `.album-art/`.
6. **Video thumbnails:** Same video indexed by User A → User B: 1 canonical_video with poster hash, no blob duplication.
7. **Reset:** `ResetCollectionAsync` only deletes user junction records, never canonical records
8. **Cross-user isolation:** Deleting User A's library doesn't affect User B's access to shared canonical content
9. **Embedded MBID extraction:** Index a file tagged with MusicBrainz Picard (contains `MUSICBRAINZ_TRACK_ID`, `MUSICBRAINZ_ARTIST_ID`, `MUSICBRAINZ_ALBUM_ID`) → verify canonical records are populated with these MBIDs directly from tags (no API search needed)
10. **Direct MBID enrichment:** After indexing a Picard-tagged file, trigger enrichment → verify it calls `/recording/{mbid}` GET (not search) and `/artist/{mbid}` GET
11. **Duration-matched recording:** Index a file where the embedded track duration differs from the MusicBrainz recording by >2s → verify the match is rejected and logged as low-confidence
12. **Album type filtering:** Index a compilation → verify enrichment prefers `primary-type:Album` results, not singles/EPs
13. **TMDB IMDB cross-ref:** Enrich a video file with embedded IMDB ID → verify TMDB is queried via `/find/{imdbId}?external_source=imdb_id` instead of title search
14. **ExtractYear fix:** File named "The 400 Blows.mp4" → verify year is NOT extracted as 400 (should be null, or from embedded metadata/folder)
15. **include_adult:** Verify all TMDB search calls include `&include_adult=false`

---

## Decisions

| Decision                 | Choice                                                                                                                       |
| ------------------------ | ---------------------------------------------------------------------------------------------------------------------------- |
| **Scope**                | Music + Video modules only (Photos is inherently per-user)                                                                   |
| **Approach**             | Full canonical data model with content-addressed binary cache                                                                |
| **Cache location**       | New `Files:Storage:MediaCachePath` → `{RootPath}/.media-cache/`                                                              |
| **Content identity**     | SHA-256 ContentHash (primary), force re-hash if missing (no metadata composite fallback)                                     |
| **Existing data**        | Reset (drop old per-user tables, fresh start). Keeps per-user interaction data (playlists, playback history, watch progress) |
| **Migration strategy**   | Reset rather than migrate — the per-user duplication is too entangled to cleanly migrate without a script per table          |
| **No new shared schema** | Keep canonical tables in their module's schema (`music.*`, `video.*`) but without OwnerId columns                            |
| **Entity naming**        | `canonical_` prefix for shared tables, `user_` prefix for junction tables                                                    |
| **Subtitles**            | Move to canonical (intrinsic to the video file)                                                                              |
| **Series/Seasons**       | Move to canonical (TMDB metadata is same for everyone). Per-user membership via junction.                                    |
| **Enrichment priority**  | Embedded IDs > direct-by-ID API > improved text search with filters                                                          |

---

## Further Considerations (resolved during research)

- **ContentHash availability:** Files module already computes SHA-256 hashes for FileNodes. Music Track already stores ContentHash. Video needs to add it.
- **Existing cross-owner in Music:** The current `TryIndexFromExistingOwnerAsync` approach proves the concept but implementation becomes much simpler with canonical tables.
- **Album art dedup:** Currently cached as `{albumId}.jpg` in `.album-art/`. With content-addressed cache, same art from different albums maps to same hash → single file.
- **Subtitles:** Moved to canonical since they're intrinsic to the video file (embedded or sidecar).
- **Series/Seasons:** Series metadata (name, overview, poster) is canonical since it comes from TMDB. Per-user membership (which episodes a user has) is the junction.
- **Album art corner case:** Different releases of the same album may have different cover art. Content-addressed cache handles this naturally — different images = different hashes = separate files on disk.
- **MusicBrainz ID in tags:** Picard-tagged files contain embedded MBIDs (track, artist, album, release group). Currently completely ignored — extracting them enables direct API lookups instead of text searches.
- **ffprobe format.tags:** Video container metadata (title, IMDB ID, TMDB ID, date) is available in ffprobe JSON but currently completely ignored.
