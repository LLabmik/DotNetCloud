# MusicBrainz Metadata Enrichment + Scan Progress UI

## TL;DR

Add external metadata enrichment to the music module using **MusicBrainz + Cover Art Archive** (fully free, no API key required). Enriches albums with cover art, artists with bios/images/links, and tracks with corrected metadata. Triggered both automatically during library scan and manually via UI buttons. Also overhauls the scan UI to show real-time progress (current file, counts, phase) instead of the current "spinning then done" experience.

**External APIs Used:**
- [MusicBrainz Web Service v2](https://musicbrainz.org/doc/MusicBrainz_API) — artist/album/track metadata, relationships, annotations
- [Cover Art Archive](https://coverartarchive.org/) — album cover images (backed by Internet Archive CDN)

**Both are fully free, open-source, no API key required. Only a descriptive User-Agent header is mandatory.**

---

## Phase A: Data Model Changes (Migration)

### A1. Add external ID and enrichment fields to existing models

**Artist model** — add:
- `string? MusicBrainzId` — MBID for artist lookup
- `string? Biography` — from MusicBrainz annotation / Wikidata
- `string? ImageUrl` — artist image (from CAA or fanart if available)
- `string? WikipediaUrl` — link extracted from MusicBrainz URL relations
- `string? DiscogsUrl` — link extracted from MusicBrainz URL relations
- `string? OfficialUrl` — official website from MusicBrainz URL relations
- `DateTime? LastEnrichedAt` — throttle re-enrichment (skip if <30 days)

**MusicAlbum model** — add:
- `string? MusicBrainzReleaseGroupId` — MBID for the release group (album concept)
- `string? MusicBrainzReleaseId` — MBID for specific release (needed for CAA image lookup)
- `DateTime? LastEnrichedAt` — throttle re-enrichment

**Track model** — add:
- `string? MusicBrainzRecordingId` — MBID for the recording
- `DateTime? LastEnrichedAt`

**Files to modify:**
- `src/Modules/Music/DotNetCloud.Modules.Music.Data/Models/Artist.cs`
- `src/Modules/Music/DotNetCloud.Modules.Music.Data/Models/MusicAlbum.cs`
- `src/Modules/Music/DotNetCloud.Modules.Music.Data/Models/Track.cs`
- `src/Modules/Music/DotNetCloud.Modules.Music.Data/Configuration/ArtistConfiguration.cs`
- `src/Modules/Music/DotNetCloud.Modules.Music.Data/Configuration/MusicAlbumConfiguration.cs`
- `src/Modules/Music/DotNetCloud.Modules.Music.Data/Configuration/TrackConfiguration.cs`

### A2. EF Core Migration

```bash
dotnet ef migrations add AddMusicBrainzEnrichment \
  --project src/Modules/Music/DotNetCloud.Modules.Music.Data \
  --context MusicDbContext
```

**Depends on:** nothing  
**Parallel with:** nothing (must be first)

---

## Phase B: MusicBrainz + Cover Art Archive Services

### B1. `MusicBrainzClient` — Low-level HTTP client

**New file:** `src/Modules/Music/DotNetCloud.Modules.Music.Data/Services/MusicBrainzClient.cs`

Responsibilities:
- Typed HttpClient registered via `AddHttpClient<IMusicBrainzClient, MusicBrainzClient>()`
- Base URL: `https://musicbrainz.org/ws/2/`
- Required User-Agent: `DotNetCloud/0.2.0 (https://github.com/LLabmik/DotNetCloud)` — MusicBrainz mandates a descriptive UA
- **Rate limiting**: max 1 request/second (MusicBrainz enforces this server-side with 503; use `SemaphoreSlim` + `Task.Delay` client-side)
- Accept header: `application/json`
- Methods:
  - `SearchArtistAsync(string name)` → returns MB artist results (name, MBID, score, disambiguation)
  - `GetArtistAsync(string mbid, includes: "url-rels,annotation")` → full artist with URL relations + annotation text
  - `SearchReleaseGroupAsync(string album, string artist)` → returns release group results
  - `GetReleaseGroupAsync(string mbid, includes: "releases")` → release group with all releases (needed to find one with cover art)
  - `SearchRecordingAsync(string title, string artist)` → recording results
  - `GetRecordingAsync(string mbid)` → full recording detail

**Interface:** `IMusicBrainzClient` in `src/Modules/Music/DotNetCloud.Modules.Music/Services/IMusicBrainzClient.cs`

### B2. `CoverArtArchiveClient` — Album art fetcher

**New file:** `src/Modules/Music/DotNetCloud.Modules.Music.Data/Services/CoverArtArchiveClient.cs`

Responsibilities:
- Typed HttpClient, base URL: `https://coverartarchive.org/`
- Methods:
  - `GetFrontCoverAsync(string releaseMbid)` → returns `(byte[] data, string mimeType)?`
  - `GetCoverListAsync(string releaseMbid)` → returns list of available images (front, back, booklet, etc.)
- Falls back through releases in a release group until cover art is found (not all releases have art uploaded)
- Shares the 1 req/sec semaphore with MusicBrainz client for politeness

**Interface:** `ICoverArtArchiveClient` in `src/Modules/Music/DotNetCloud.Modules.Music/Services/ICoverArtArchiveClient.cs`

### B3. `MetadataEnrichmentService` — Orchestrator

**New file:** `src/Modules/Music/DotNetCloud.Modules.Music.Data/Services/MetadataEnrichmentService.cs`

Responsibilities:
- Orchestrates MusicBrainz lookups and applies results to database entities
- Methods:
  - `EnrichAlbumAsync(Guid albumId, CallerContext, bool force = false)` — search MB by album title + artist name → get release group → get release → fetch cover art from CAA → update Album entity fields
  - `EnrichArtistAsync(Guid artistId, CallerContext, bool force = false)` — search MB by artist name → get artist with relations → extract bio (annotation), URLs (Wikipedia, Discogs, official) → update Artist entity fields
  - `EnrichTrackAsync(Guid trackId, CallerContext, bool force = false)` — search MB recording → store MBID
  - `EnrichAlbumsWithoutArtAsync(Guid ownerId, IProgress<EnrichmentProgress>?, CancellationToken)` — batch: find all albums where `HasCoverArt == false`, enrich each
  - `EnrichAllAsync(Guid ownerId, IProgress<EnrichmentProgress>?, CancellationToken)` — batch: all unenriched items
- Respects `LastEnrichedAt` (skip if enriched within last 30 days unless `force = true`)
- Reports progress via `IProgress<EnrichmentProgress>`
- Accepts top MusicBrainz result only if search score ≥ 90 (skip ambiguous matches, log warning)

**Interface:** `IMetadataEnrichmentService` in `src/Modules/Music/DotNetCloud.Modules.Music/Services/IMetadataEnrichmentService.cs`

**DTO:** `EnrichmentProgress` record:
```
{ Phase (string), Current (int), Total (int), CurrentItem (string), AlbumArtFound (int), ArtistBiosFound (int) }
```

**Depends on:** Phase A (model fields must exist)  
**B1, B2 can be built in parallel; B3 depends on B1 + B2**

---

## Phase C: Scan Progress Infrastructure

### C1. `LibraryScanProgress` — Progress reporting model

**Update:** `src/Core/DotNetCloud.Core/DTOs/MusicDtos.cs`

```csharp
public sealed record LibraryScanProgress
{
    /// <summary>Current scan phase ("Discovering files", "Extracting metadata", "Enriching metadata", "Complete").</summary>
    public required string Phase { get; init; }
    /// <summary>Name of the file currently being processed.</summary>
    public string? CurrentFile { get; init; }
    /// <summary>Number of files processed so far.</summary>
    public int FilesProcessed { get; init; }
    /// <summary>Total files to process.</summary>
    public int TotalFiles { get; init; }
    /// <summary>Tracks successfully added.</summary>
    public int TracksAdded { get; init; }
    /// <summary>Tracks updated (re-indexed).</summary>
    public int TracksUpdated { get; init; }
    /// <summary>Tracks skipped (already up to date).</summary>
    public int TracksSkipped { get; init; }
    /// <summary>Tracks that failed to index.</summary>
    public int TracksFailed { get; init; }
    /// <summary>Album covers fetched from external source.</summary>
    public int AlbumArtFetched { get; init; }
    /// <summary>Completion percentage (0-100).</summary>
    public int PercentComplete { get; init; }
    /// <summary>Time elapsed since scan started.</summary>
    public TimeSpan ElapsedTime { get; init; }
}
```

### C2. Update `LibraryScanService` to report progress

**Modify:** `src/Modules/Music/DotNetCloud.Modules.Music.Data/Services/LibraryScanService.cs`

- Add `IProgress<LibraryScanProgress>?` parameter to `ScanLibraryAsync()` and `IndexFileAsync()`
- Report progress after each file: file name, running counts, percentage
- After metadata scan phase, optionally trigger enrichment phase (enrich albums without art)
- Support `CancellationToken` for cancellation during both scan and enrichment phases

### C3. `ScanProgressState` — Scoped state service for Blazor

**New file:** `src/Modules/Music/DotNetCloud.Modules.Music/Services/ScanProgressState.cs`

- Scoped service (one per Blazor circuit/tab)
- Properties: `bool IsScanning`, `LibraryScanProgress? CurrentProgress`, `event Action? OnProgressChanged`
- Bridges the `IProgress<LibraryScanProgress>` callback to Blazor's `StateHasChanged()` pattern
- The scan method updates this state; the UI component subscribes to `OnProgressChanged`

**Depends on:** C1; C2 can be parallel

---

## Phase D: API Endpoints

### D1. Enrichment endpoints on MusicController

**Modify:** `src/Modules/Music/DotNetCloud.Modules.Music.Host/Controllers/MusicController.cs`

New endpoints:
- `POST /api/v1/music/enrich/album/{albumId}` — enrich single album (fetch art + MB data)
- `POST /api/v1/music/enrich/artist/{artistId}` — enrich single artist (bio, links)
- `POST /api/v1/music/enrich/all` — batch enrich all unenriched items for user
- `POST /api/v1/music/enrich/missing-art` — batch enrich only albums missing cover art
- `GET /api/v1/music/artists/{artistId}/bio` — get artist biography and external links

### D2. Scan progress endpoint (for non-Blazor clients)

- `GET /api/v1/music/scan/progress` — returns current scan progress for the authenticated user

**Depends on:** Phase B (enrichment service) and Phase C (progress)

---

## Phase E: Blazor UI Updates

### E1. Scan progress UI overhaul

**Modify:** `src/Modules/Music/DotNetCloud.Modules.Music/UI/MusicPage.razor` + `.razor.cs`

Replace current scan UX (button → spinner → result banner) with:
- **Progress bar** showing percentage complete
- **Phase indicator**: "Discovering files..." → "Extracting metadata (42/198)..." → "Fetching missing album art (3/12)..." → "Complete"
- **Current file name** being processed (truncated to fit)
- **Running counts**: Added / Updated / Skipped / Failed (live updating as each file is processed)
- **Elapsed time** counter
- **Cancel button** (wires `CancellationToken`)
- **Result summary** on completion (same counts plus "X album covers fetched from MusicBrainz")

### E2. Album enrichment UI

**Modify:** Album detail view in MusicPage

- For albums with `HasCoverArt == false`: show placeholder art with "Fetch Cover Art" button
- Button calls `POST /enrich/album/{id}` or directly via injected `IMetadataEnrichmentService`
- Show spinner during fetch, update art display on success
- Show toast/notification on result (success or "no art found")

### E3. Artist enrichment UI

**Modify:** Artist detail view in MusicPage

- Show biography section if `Biography` is populated
- Show external links (Wikipedia, Discogs, Official site) as icon buttons/links
- "Fetch Info" button if `LastEnrichedAt` is null
- Artist image display if `ImageUrl` is populated

### E4. Settings: enrichment toggles

**Modify:** Library Settings section in MusicPage

- Toggle: "Auto-fetch metadata from MusicBrainz during scan" (default: on)
- Toggle: "Auto-fetch missing album art from Cover Art Archive" (default: on)
- These control whether the enrichment phase runs automatically after the metadata scan phase

**Depends on:** Phases B, C, D

---

## Phase F: Service Registration + Configuration

### F1. Register new services

**Modify:** `src/Modules/Music/DotNetCloud.Modules.Music.Data/MusicServiceRegistration.cs`

- Register `IMusicBrainzClient` / `MusicBrainzClient` via `AddHttpClient<>()`
- Register `ICoverArtArchiveClient` / `CoverArtArchiveClient` via `AddHttpClient<>()`
- Register `IMetadataEnrichmentService` / `MetadataEnrichmentService` as scoped
- Register `ScanProgressState` as scoped
- Configure User-Agent header for MusicBrainz HttpClient
- Add Polly retry policy (429/503 → exponential backoff + retry) for both HTTP clients

### F2. Configuration section

```json
{
  "Music": {
    "Enrichment": {
      "Enabled": true,
      "AutoFetchArt": true,
      "AutoEnrichArtists": true,
      "RateLimitMs": 1100
    }
  }
}
```

- `Music:Enrichment:Enabled` — master toggle (bool, default `true`)
- `Music:Enrichment:AutoFetchArt` — auto-fetch missing art during scan (bool, default `true`)
- `Music:Enrichment:AutoEnrichArtists` — auto-enrich artist data during scan (bool, default `true`)
- `Music:Enrichment:RateLimitMs` — minimum delay between MusicBrainz requests (int, default `1100` — slightly over 1 sec for safety margin)

**Depends on:** B1, B2, C3

---

## Phase G: Comprehensive Unit Tests

**Framework:** MSTest (`[TestClass]`, `[TestMethod]`, `[TestInitialize]`, `[TestCleanup]`)  
**Mocking:** Moq (`Mock<T>`, `.Setup()`, `.Verify()`)  
**DB:** In-memory `MusicDbContext` via `TestHelpers.CreateDb()` (fresh per test)  
**Naming:** `MethodUnderTest_Condition_ExpectedResult`  
**Pattern:** Arrange-Act-Assert, `NullLogger<T>` for non-inspected dependencies  

**All new/modified files in:** `tests/DotNetCloud.Modules.Music.Tests/`

---

### G1. `MusicBrainzClientTests.cs` (~20 tests)

Setup: `Mock<HttpMessageHandler>` → `HttpClient` → `MusicBrainzClient`. Use `MockHttpMessageHandler` helper to create mock HTTP responses with JSON payloads.

**URL Construction & Request Format:**
- `SearchArtist_BuildsCorrectUrl` — verify query: `?query=artist:"Pink Floyd"&fmt=json`
- `SearchReleaseGroup_BuildsCorrectUrl` — verify: `?query=releasegroup:"Dark Side" AND artist:"Pink Floyd"&fmt=json`
- `SearchRecording_BuildsCorrectUrl` — verify recording search URL format
- `GetArtist_IncludesRelations` — verify: `?inc=url-rels,annotation&fmt=json`
- `GetReleaseGroup_IncludesReleases` — verify: `?inc=releases&fmt=json`
- `AllRequests_IncludeUserAgent` — verify User-Agent header is `DotNetCloud/0.2.0 (...)` on every request

**JSON Deserialization:**
- `SearchArtist_DeserializesResults` — mock JSON with 3 artists, verify name/MBID/score/disambiguation mapped correctly
- `SearchArtist_EmptyResults_ReturnsEmptyList` — mock empty `artists` array
- `GetArtist_DeserializesRelations` — mock JSON with url-rels (Wikipedia, Discogs, official), verify extracted to correct fields
- `GetArtist_DeserializesAnnotation` — mock annotation text, verify mapped to bio field
- `GetReleaseGroup_DeserializesReleases` — mock release group with 3 releases, verify all parsed
- `SearchArtist_MalformedJson_ReturnsNull` — graceful handling of malformed response

**Rate Limiting:**
- `ConcurrentRequests_RespectRateLimit` — fire 5 requests simultaneously, verify they're serialized with ~1s gaps (total elapsed ≥ 4s)
- `SequentialRequests_DelayBetween` — 2 sequential calls take ≥ 1s total

**Error Handling:**
- `SearchArtist_Http503_ReturnsNull` — mock 503, verify no exception, returns null/empty
- `SearchArtist_Http429_ReturnsNull` — mock 429 Too Many Requests, verify graceful return
- `GetArtist_NetworkError_ReturnsNull` — mock `HttpRequestException`, verify null
- `GetArtist_Timeout_ReturnsNull` — mock `TaskCanceledException`, verify null

---

### G2. `CoverArtArchiveClientTests.cs` (~15 tests)

Setup: same `MockHttpMessageHandler` pattern.

**Successful Fetches:**
- `GetFrontCover_ValidRelease_ReturnsImageData` — mock 200 with JPEG bytes, verify `(data, "image/jpeg")` returned
- `GetFrontCover_PngImage_ReturnsPngMimeType` — mock PNG Content-Type, verify `"image/png"`
- `GetCoverList_ReturnsAllImages` — mock JSON listing with front/back/booklet entries, verify list parsed

**Fallback Logic:**
- `GetFrontCover_FirstRelease404_FallsToSecond` — mock 404 for first release MBID, 200 for second, verify second's image data returned
- `GetFrontCover_AllReleases404_ReturnsNull` — mock 404 for all releases in group, verify null
- `GetFrontCover_EmptyReleaseList_ReturnsNull` — no releases to try, returns null immediately

**Redirect Handling:**
- `GetFrontCover_FollowsRedirect` — CAA returns 307 redirect to archive.org CDN, verify final image data retrieved (HttpClient follows redirects by default, verify config allows it)

**Error Handling:**
- `GetFrontCover_Http503_ReturnsNull` — service unavailable, graceful return
- `GetFrontCover_NetworkError_ReturnsNull` — `HttpRequestException` caught
- `GetFrontCover_Timeout_ReturnsNull` — timeout handled
- `GetFrontCover_LargeImage_ReturnsData` — verify handles >5MB images without truncation
- `GetFrontCover_EmptyBody_ReturnsNull` — 200 OK but 0 bytes in body
- `GetFrontCover_InvalidMimeType_DefaultsToJpeg` — unknown Content-Type header, default to `image/jpeg`

---

### G3. `MetadataEnrichmentServiceTests.cs` (~30 tests)

Setup: `Mock<IMusicBrainzClient>`, `Mock<ICoverArtArchiveClient>`, real `MusicDbContext` (in-memory), `AlbumArtService` with temp directory for art caching. Seed entities via `TestHelpers`.

**Album Enrichment (10 tests):**
- `EnrichAlbum_NoArt_FetchesFromCAA` — seed album with `HasCoverArt=false`, mock MB returning release group + release, mock CAA returning image bytes, verify `HasCoverArt=true` and `CoverArtPath` set on entity
- `EnrichAlbum_AlreadyHasArt_SkipsCAAFetch` — seed album with `HasCoverArt=true`, verify CAA client never called
- `EnrichAlbum_StoresMusicBrainzIds` — verify `MusicBrainzReleaseGroupId` and `MusicBrainzReleaseId` saved to entity
- `EnrichAlbum_NoMBMatch_LeavesUnchanged` — mock empty MB search results, verify album entity not modified
- `EnrichAlbum_CAAReturnsNull_ArtStaysFalse` — MB match found but CAA has no art for any release, verify `HasCoverArt` still false
- `EnrichAlbum_SetsLastEnrichedAt` — verify `LastEnrichedAt` set to approximately `DateTime.UtcNow` after enrichment
- `EnrichAlbum_RecentlyEnriched_Skips` — set `LastEnrichedAt` to 5 days ago, verify MB client not called (threshold is 30 days)
- `EnrichAlbum_RecentlyEnriched_ForceFlag_Enriches` — set `LastEnrichedAt` to 5 days ago + `force=true`, verify MB client IS called
- `EnrichAlbum_LowMBScore_Skips` — mock MB result with score < 90, verify no enrichment applied to entity
- `EnrichAlbum_NonExistentAlbum_ReturnsGracefully` — non-existent album ID, verify no exception thrown

**Artist Enrichment (10 tests):**
- `EnrichArtist_FetchesBioAndLinks` — mock MB artist with annotation + url-rels, verify `Biography`, `WikipediaUrl`, `DiscogsUrl`, `OfficialUrl` all saved to entity
- `EnrichArtist_StoresMusicBrainzId` — verify `MusicBrainzId` field saved
- `EnrichArtist_NoAnnotation_BiographyStaysNull` — MB artist has no annotation, verify `Biography` remains null
- `EnrichArtist_NoRelations_LinksStayNull` — no url-rels in MB response, all URL fields remain null
- `EnrichArtist_NoMBMatch_LeavesUnchanged` — empty search results, entity unchanged
- `EnrichArtist_SetsLastEnrichedAt` — timestamp set after enrichment
- `EnrichArtist_RecentlyEnriched_Skips` — within 30-day window, skips
- `EnrichArtist_RecentlyEnriched_ForceFlag_Enriches` — force overrides window
- `EnrichArtist_PartialRelations_OnlyPopulatesFound` — has Wikipedia URL but no Discogs, verify Wikipedia set and Discogs remains null
- `EnrichArtist_NonExistent_ReturnsGracefully` — non-existent artist ID

**Track Enrichment (3 tests):**
- `EnrichTrack_StoresMusicBrainzRecordingId` — verify MBID saved
- `EnrichTrack_NoMatch_LeavesUnchanged` — empty search results
- `EnrichTrack_SetsLastEnrichedAt` — timestamp set

**Batch Enrichment (7 tests):**
- `EnrichAlbumsWithoutArt_ProcessesOnlyMissingArt` — seed 3 albums (2 without art, 1 with), verify only the 2 without art are enriched
- `EnrichAlbumsWithoutArt_ReportsProgress` — capture `IProgress<EnrichmentProgress>` callbacks, verify `Current`/`Total`/`Phase` values increment correctly
- `EnrichAll_ProcessesAllEntityTypes` — seed unenriched artist + album + track, verify all three enriched
- `EnrichAll_SkipsAlreadyEnriched` — mix of recently-enriched and unenriched entities, verify only unenriched processed
- `EnrichAll_CancellationToken_StopsEarly` — cancel after 2 items, verify not all items processed
- `EnrichAll_ProgressReporting_AllPhases` — verify progress reports phase transitions: "Enriching artists..." → "Enriching albums..." → "Fetching cover art..."
- `EnrichAll_EmptyLibrary_CompletesImmediately` — no entities to enrich, completes without error

---

### G4. `LibraryScanProgressTests.cs` (~12 tests)

Setup: real `LibraryScanService` with mocked `MusicMetadataService`, `AlbumArtService`, `IEventBus`, `IMetadataEnrichmentService` (mocked), in-memory DB. Use `Progress<LibraryScanProgress>` with callback to capture reports.

**Progress Reporting (7 tests):**
- `ScanLibrary_ReportsProgressPerFile` — provide 5 files, verify 5 progress callbacks fired
- `ScanLibrary_ProgressIncludesFileName` — verify `CurrentFile` field populated with each file's name
- `ScanLibrary_ProgressIncludesRunningCounts` — verify `TracksAdded` increments across callbacks
- `ScanLibrary_ProgressShowsPercentage` — verify `PercentComplete` goes 20→40→60→80→100 for 5 files
- `ScanLibrary_ProgressShowsPhase` — verify phase transitions ("Extracting metadata" → "Complete")
- `ScanLibrary_NullProgress_DoesNotThrow` — `IProgress` parameter is null, scan completes normally without error
- `ScanLibrary_EmptyFileList_ReportsComplete` — 0 files, still fires completion progress report

**Enrichment Integration (3 tests):**
- `ScanLibrary_WithEnrichmentEnabled_RunsEnrichmentPhase` — verify `MetadataEnrichmentService.EnrichAlbumsWithoutArtAsync` called after metadata scan completes
- `ScanLibrary_WithEnrichmentDisabled_SkipsEnrichmentPhase` — verify enrichment service NOT called when disabled via configuration
- `ScanLibrary_EnrichmentPhase_ReportsProgress` — verify progress phase changes to "Fetching missing album art..."

**Cancellation (2 tests):**
- `ScanLibrary_CancellationDuringScan_StopsProcessing` — cancel token after 2 files, verify only 2 files processed
- `ScanLibrary_CancellationDuringEnrichment_StopsEnrichment` — cancel during enrichment phase, verify scan results preserved

---

### G5. `ScanProgressStateTests.cs` (~8 tests)

Setup: direct construction of `ScanProgressState` (no dependencies).

- `IsScanning_DefaultFalse` — default state is not scanning
- `StartScan_SetsIsScanningTrue` — calling start method sets flag
- `UpdateProgress_SetsCurrentProgress` — after update, `CurrentProgress` property reflects latest data
- `UpdateProgress_FiresOnProgressChanged` — subscribe to `OnProgressChanged` event, verify it fires on update
- `CompleteScan_SetsIsScanningFalse` — completing scan resets flag
- `CompleteScan_FiresOnProgressChanged` — event fires on completion
- `MultipleSubscribers_AllNotified` — 2 event subscribers, both receive notification
- `UpdateProgress_NullListener_DoesNotThrow` — no subscribers registered, update doesn't throw

---

### G6. Update existing tests (~5 changes)

**Modify:** existing test files that call `ScanLibraryAsync` or `IndexFileAsync`:
- Add `null` for the new `IProgress<LibraryScanProgress>?` parameter where existing calls don't need progress reporting
- Add `null` for the optional `IMetadataEnrichmentService` parameter
- Verify all existing tests still pass with the updated method signatures

**Modify:** `TestHelpers.cs` — add new seeding helpers:
- `SeedAlbumWithoutArtAsync()` — seeds album with `HasCoverArt = false`, no `CoverArtPath`
- `SeedEnrichedArtistAsync()` — seeds artist with `MusicBrainzId`, `Biography`, `LastEnrichedAt` populated
- Static helper: `CreateMockMusicBrainzArtistJson(string name, string mbid, int score)` — returns JSON string for mocking MusicBrainz API responses

---

### G7. `MockHttpMessageHandler.cs` — Reusable HTTP mock helper

**New file:** `tests/DotNetCloud.Modules.Music.Tests/MockHttpMessageHandler.cs`

Reusable mock handler for all HTTP client tests:

```csharp
internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
    public List<HttpRequestMessage> ReceivedRequests { get; } = new();

    // Factory methods:
    public static MockHttpMessageHandler ForJson(string json);
    public static MockHttpMessageHandler ForBytes(byte[] data, string contentType);
    public static MockHttpMessageHandler ForStatus(HttpStatusCode code);
    public static MockHttpMessageHandler ForSequence(params HttpResponseMessage[] responses);
}
```

- Tracks all requests for assertion via `ReceivedRequests` list
- `ForJson()` — returns 200 with JSON content type
- `ForBytes()` — returns 200 with arbitrary binary content (for image data)
- `ForStatus()` — returns specified error status code
- `ForSequence()` — returns different response for each successive call (for testing fallback logic)

---

### Test Summary

| Test File | Test Count | What It Tests |
|-----------|-----------|---------------|
| `MusicBrainzClientTests.cs` | ~20 | URL construction, JSON parsing, rate limiting, error handling |
| `CoverArtArchiveClientTests.cs` | ~15 | Image fetching, release fallback, redirects, error handling |
| `MetadataEnrichmentServiceTests.cs` | ~30 | Album/artist/track enrichment, batch operations, progress, caching |
| `LibraryScanProgressTests.cs` | ~12 | Scan progress reporting, enrichment integration, cancellation |
| `ScanProgressStateTests.cs` | ~8 | Blazor state service, event notifications |
| Existing test updates | ~5 | Signature compatibility with new parameters |
| `MockHttpMessageHandler.cs` | — | Shared test infrastructure |
| **Total** | **~90** | |

---

## File Inventory

### New Files

| File | Purpose |
|------|---------|
| `src/Modules/Music/DotNetCloud.Modules.Music/Services/IMusicBrainzClient.cs` | Interface for MusicBrainz API client |
| `src/Modules/Music/DotNetCloud.Modules.Music/Services/ICoverArtArchiveClient.cs` | Interface for Cover Art Archive client |
| `src/Modules/Music/DotNetCloud.Modules.Music/Services/IMetadataEnrichmentService.cs` | Interface for enrichment orchestrator |
| `src/Modules/Music/DotNetCloud.Modules.Music/Services/ScanProgressState.cs` | Blazor scoped state for scan progress |
| `src/Modules/Music/DotNetCloud.Modules.Music.Data/Services/MusicBrainzClient.cs` | MusicBrainz API client implementation |
| `src/Modules/Music/DotNetCloud.Modules.Music.Data/Services/CoverArtArchiveClient.cs` | Cover Art Archive client implementation |
| `src/Modules/Music/DotNetCloud.Modules.Music.Data/Services/MetadataEnrichmentService.cs` | Enrichment orchestrator implementation |
| `tests/DotNetCloud.Modules.Music.Tests/MusicBrainzClientTests.cs` | MusicBrainz client unit tests |
| `tests/DotNetCloud.Modules.Music.Tests/CoverArtArchiveClientTests.cs` | CAA client unit tests |
| `tests/DotNetCloud.Modules.Music.Tests/MetadataEnrichmentServiceTests.cs` | Enrichment service unit tests |
| `tests/DotNetCloud.Modules.Music.Tests/LibraryScanProgressTests.cs` | Scan progress unit tests |
| `tests/DotNetCloud.Modules.Music.Tests/ScanProgressStateTests.cs` | Blazor state unit tests |
| `tests/DotNetCloud.Modules.Music.Tests/MockHttpMessageHandler.cs` | Shared HTTP mock helper |

### Modified Files

| File | Changes |
|------|---------|
| `src/Modules/Music/DotNetCloud.Modules.Music.Data/Models/Artist.cs` | Add enrichment fields (MBID, bio, URLs, LastEnrichedAt) |
| `src/Modules/Music/DotNetCloud.Modules.Music.Data/Models/MusicAlbum.cs` | Add MusicBrainz IDs + LastEnrichedAt |
| `src/Modules/Music/DotNetCloud.Modules.Music.Data/Models/Track.cs` | Add MusicBrainz recording ID + LastEnrichedAt |
| `src/Modules/Music/DotNetCloud.Modules.Music.Data/Configuration/ArtistConfiguration.cs` | EF config for new fields |
| `src/Modules/Music/DotNetCloud.Modules.Music.Data/Configuration/MusicAlbumConfiguration.cs` | EF config for new fields |
| `src/Modules/Music/DotNetCloud.Modules.Music.Data/Configuration/TrackConfiguration.cs` | EF config for new fields |
| `src/Modules/Music/DotNetCloud.Modules.Music.Data/Services/LibraryScanService.cs` | Add progress reporting + enrichment call |
| `src/Modules/Music/DotNetCloud.Modules.Music.Data/MusicServiceRegistration.cs` | Register new services + HTTP clients |
| `src/Modules/Music/DotNetCloud.Modules.Music.Data/DotNetCloud.Modules.Music.Data.csproj` | Add Polly package reference |
| `src/Modules/Music/DotNetCloud.Modules.Music.Host/Controllers/MusicController.cs` | Enrichment endpoints |
| `src/Modules/Music/DotNetCloud.Modules.Music/UI/MusicPage.razor` | Scan progress UI + enrichment buttons |
| `src/Modules/Music/DotNetCloud.Modules.Music/UI/MusicPage.razor.cs` | Scan progress logic + enrichment handlers |
| `src/Modules/Music/DotNetCloud.Modules.Music/UI/MusicPage.razor.css` | Progress bar + enrichment UI styles |
| `src/Core/DotNetCloud.Core/DTOs/MusicDtos.cs` | Add `LibraryScanProgress` + `EnrichmentProgress` DTOs |
| `src/UI/DotNetCloud.UI.Web/wwwroot/css/app.css` | Scan progress global styles |
| `tests/DotNetCloud.Modules.Music.Tests/TestHelpers.cs` | Add new seeding helpers |

---

## Verification Checklist

1. ☐ **Unit tests pass**: `dotnet test tests/DotNetCloud.Modules.Music.Tests/`
2. ☐ **Full build**: `dotnet build`
3. ☐ **Migration applies**: `dotnet ef database update --project src/Modules/Music/DotNetCloud.Modules.Music.Data --context MusicDbContext`
4. ☐ **Manual test — scan with progress**: Start a library scan on a folder with 20+ audio files, verify progress bar updates live with file names and counts
5. ☐ **Manual test — auto art fetch**: Scan a library with files that have and don't have embedded art. Albums without should get covers fetched from CAA after the scan phase
6. ☐ **Manual test — manual enrich**: Navigate to an album without art, click "Fetch Cover Art", verify art loads
7. ☐ **Manual test — artist bio**: Navigate to an artist, click "Fetch Info", verify bio and links populate
8. ☐ **Manual test — rate limiting**: Scan a large library, verify MusicBrainz requests don't exceed 1/sec (check logs)
9. ☐ **Manual test — cancellation**: Start a scan, click Cancel, verify it stops cleanly

---

## Design Decisions

| Decision | Rationale |
|----------|-----------|
| **MusicBrainz + Cover Art Archive only** | No API key needed, fully free and open-source, well-documented, massive database |
| **No Last.fm for now** | Can be added later if artist photos are needed (Last.fm has better artist images). Avoids requiring users to obtain API keys |
| **Rate limiting via `SemaphoreSlim` + delay** | Simpler than Polly RateLimiter for the 1 req/sec requirement. One shared semaphore for both MB and CAA |
| **Polly retry for 429/503** | MusicBrainz returns 503 when rate limited server-side. Exponential backoff retry keeps things resilient |
| **Enrichment opt-in during scan** | Toggle in settings (default: on). Users on slow connections can disable to speed up scans |
| **No background enrichment queue yet** | Enrichment runs inline after scan or on-demand. A background job queue is a natural follow-up for very large libraries (10k+ tracks) |
| **Scoped `ScanProgressState` for Blazor** | Simpler than SignalR since music scan is user-initiated within the same Blazor circuit. No need for cross-circuit communication |
| **Artist bio from MusicBrainz annotation** | If annotation is empty, following the Wikidata relation to get Wikipedia summary is a Phase 2 enhancement |
| **Score threshold ≥ 90 for auto-matching** | Prevents incorrect metadata from being applied. Ambiguous matches are logged as warnings and skipped |
| **30-day re-enrichment cooldown** | Prevents excessive API calls on repeated scans. Overridable with `force` flag for manual re-fetch |

---

## Dependency Graph

```
Phase A (Data Model) ─────────────────┐
                                      │
Phase B1 (MusicBrainz Client) ────┐   │
Phase B2 (CAA Client) ────────┐   │   │
                               │   │   │
Phase B3 (Enrichment Service) ←┘───┘───┘
                               │
Phase C1 (Progress DTO) ──┐   │
Phase C2 (Scan Progress) ←┘   │
Phase C3 (Blazor State) ──┐   │
                           │   │
Phase D (API Endpoints) ←──┘───┘
                           │
Phase E (Blazor UI) ←──────┘
                           │
Phase F (Registration) ←───┘
                           │
Phase G (Tests) ←──────────┘
```
