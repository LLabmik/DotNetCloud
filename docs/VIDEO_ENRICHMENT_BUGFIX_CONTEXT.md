# Video Enrichment Module — Bugfix Context

> Copy this entire prompt into a new chat session.

---

```
We're fixing bugs in the DotNetCloud video enrichment module on the production server (cloud @ /home/benk/Repos/DotNetCloud).

## What's already been fixed and deployed
1. **TmdbClient JSON crash** — `TmdbMovieDetail.ReleaseDate` was `DateTime?` but TMDB returns `""` empty strings. Changed to `string?`. Added `JsonException` catch in `GetJsonAsync`.
2. **Series enrichment stub** — `EnrichSeriesInBackground` was an empty method. Implemented actual TMDB enrichment in `VideoSeriesService.EnrichSeriesAsync` that searches TMDB, downloads posters, and stores metadata on `CanonicalVideoSeries`.
3. **Missing DB migration** — Added `Tagline`, `VoteCount`, `OriginalLanguage`, `OriginalTitle` columns to `canonical_tmdb_data` table via sqlcmd.

## Current enrichment results (batch completed)
- 769 TMDB enriched, 2 screenshot fallback, 0 failed out of 3427

## Remaining bugs to fix

### Bug 1: `GetCollectionContentAsync` returns empty `Series` list (CRITICAL)
File: `src/Modules/Video/DotNetCloud.Modules.Video.Data/Services/VideoCollectionService.cs`
Method at ~line 245 filters series items out of standaloneVideos but returns `Series = []`. Items that belong to a series simply disappear from collection views (Tv shows count 3011 but only 3 items).

### Bug 2: `MapFromCanonical` can't find TMDB data (CRITICAL)
Files: `VideoService.cs` ~line 395, `VideoCollectionService.cs` ~line 325
Both only look up `CanonicalTmdbData` by `EmbeddedTmdbId` (file metadata), but `EnrichVideoAsync` stores TMDB data under the search-found `TmdbId` and never writes it to `CanonicalVideo`. Need to add a `TmdbId` column to `CanonicalVideo`, set it during enrichment, and update both MapFromCanonical methods. Requires EF migration.

### Bug 3: Existing series never enriched (CRITICAL)
All 613 existing series have no TMDB data. Need `EnrichAllUnenrichedSeriesAsync` on `IVideoSeriesService`/`VideoSeriesService`, called at end of batch enrichment in `VideoEnrichmentBackgroundService.RunJobAsync`.

### Bug 4: `ListSeriesAsync` double-filter excludes franchises (MEDIUM)
File: `VideoSeriesService.cs` lines 124-127
Two conflicting `.Where()`: first keeps `TotalEpisodes > 1 || Items.Count > 1`, second re-filters to only `TotalEpisodes > 1`. Franchises get dropped.

### Bug 5: `GetCollectionContentAsync` loads ALL hashes (PERFORMANCE)
Loads ALL episode/franchise hashes from entire DB instead of scoping to collection.

## Project details
- .NET 10, SQL Server (production at hyperdrive.kimball.home)
- CI solution filter: `DotNetCloud.CI.slnf`
- Services are registered in `VideoServiceRegistration.cs`
- Edit tools available, build then deploy with `sudo systemctl stop dotnetcloud && dotnet publish DotNetCloud.CI.slnf -c Release -o /tmp/dotnetcloud-publish --no-self-contained --no-build && sudo cp -r /tmp/dotnetcloud-publish/* /opt/dotnetcloud/server/ && sudo systemctl restart dotnetcloud`
- SQL Server migrations: `src/Modules/Video/DotNetCloud.Modules.Video.Data.SqlServer/Migrations/`
- SQL password is in `/etc/dotnetcloud/config.json`
- For sqlcmd use: `/opt/mssql-tools18/bin/sqlcmd -S hyperdrive.kimball.home -d DotNetCloud -U dotnetcloud -P '<password>' -C`
```
