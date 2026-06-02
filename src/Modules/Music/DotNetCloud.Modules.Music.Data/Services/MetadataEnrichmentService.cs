using System.Net.Http;
using System.Text.Json;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Storage;
using DotNetCloud.Modules.Music.Models;
using DotNetCloud.Modules.Music.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Music.Data.Services;

/// <summary>
/// Orchestrates MusicBrainz lookups and applies enrichment results to database entities.
/// Handles album cover art fetching, artist bio/links enrichment, and track MBID tagging.
/// </summary>
public sealed class MetadataEnrichmentService : IMetadataEnrichmentService
{
    private readonly MusicDbContext _db;
    private readonly IMusicBrainzClient _musicBrainzClient;
    private readonly ICoverArtArchiveClient _coverArtClient;
    private readonly IAudioDbClient _audioDbClient;
    private readonly AlbumArtService _albumArtService;
    private readonly ContentAddressedStorage _contentStorage;
    private readonly HttpClient _httpClient;
    private readonly ILogger<MetadataEnrichmentService> _logger;

    /// <summary>
    /// Minimum score threshold for accepting MusicBrainz search results.
    /// Results below this score are considered ambiguous and skipped.
    /// </summary>
    private const int MinMatchScore = 90;

    /// <summary>
    /// Default re-enrichment cooldown period. Entities enriched within this window
    /// are skipped unless the force flag is set.
    /// </summary>
    private static readonly TimeSpan EnrichmentCooldown = TimeSpan.FromDays(30);

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataEnrichmentService"/> class.
    /// </summary>
    public MetadataEnrichmentService(
        MusicDbContext db,
        IMusicBrainzClient musicBrainzClient,
        ICoverArtArchiveClient coverArtClient,
        IAudioDbClient audioDbClient,
        AlbumArtService albumArtService,
        ContentAddressedStorage contentStorage,
        IConfiguration configuration,
        ILogger<MetadataEnrichmentService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _musicBrainzClient = musicBrainzClient;
        _coverArtClient = coverArtClient;
        _audioDbClient = audioDbClient;
        _albumArtService = albumArtService;
        _contentStorage = contentStorage;
        _httpClient = httpClientFactory.CreateClient("MusicBrainz");
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task EnrichAlbumAsync(Guid albumId, CallerContext caller, bool force = false, CancellationToken cancellationToken = default)
    {
        var canonicalAlbum = await _db.CanonicalAlbums.FindAsync([albumId], cancellationToken);

        if (canonicalAlbum is null)
        {
            _logger.LogDebug("Album {AlbumId} not found for enrichment", albumId);
            return;
        }

        if (!force && canonicalAlbum.LastEnrichedAt.HasValue &&
            DateTime.UtcNow - canonicalAlbum.LastEnrichedAt.Value < EnrichmentCooldown)
        {
            _logger.LogDebug("Album {AlbumId} was recently enriched, skipping", albumId);
            return;
        }

        var title = canonicalAlbum.Title;

        // Resolve artist info from CanonicalAlbumArtist
        string? artistMbid = null;
        string artistName = "Unknown Artist";
        var albumArtist = await _db.CanonicalAlbumArtists
            .Include(aa => aa.Artist)
            .FirstOrDefaultAsync(aa => aa.AlbumId == canonicalAlbum.Id, cancellationToken);
        if (albumArtist?.Artist is not null)
        {
            artistName = albumArtist.Artist.Name;
            artistMbid = albumArtist.Artist.MusicBrainzId;
        }

        _logger.LogInformation("Enriching album '{AlbumTitle}' by '{ArtistName}'", title, artistName);

        IReadOnlyList<MusicBrainzReleaseGroupResult>? releaseGroups = null;

        // Priority 1: Direct MBID lookup (release group ID already known)
        if (releaseGroups is null && canonicalAlbum.MusicBrainzReleaseGroupId is not null && canonicalAlbum.HasCoverArt)
        {
            var rgDetail = await _musicBrainzClient.GetReleaseGroupAsync(canonicalAlbum.MusicBrainzReleaseGroupId, cancellationToken);
            if (rgDetail is not null)
                releaseGroups = [new MusicBrainzReleaseGroupResult { Id = rgDetail.Id, Title = rgDetail.Title, Score = 100 }];
        }

        // Priority 2: Release MBID known
        if (releaseGroups is null && canonicalAlbum.MusicBrainzReleaseId is not null && canonicalAlbum.HasCoverArt)
        {
            var releaseUrl = $"release/{Uri.EscapeDataString(canonicalAlbum.MusicBrainzReleaseId)}?inc=release-groups&fmt=json";
            var releaseJson = await GetMusicBrainzJsonAsync(releaseUrl, cancellationToken);
            if (releaseJson is not null)
            {
                try
                {
                    using var doc = JsonDocument.Parse(releaseJson);
                    if (doc.RootElement.TryGetProperty("release-group", out var rgElement) && rgElement.TryGetProperty("id", out var idProp))
                    {
                        var rgId = idProp.GetString();
                        if (rgId is not null)
                        {
                            var rgTitle = rgElement.TryGetProperty("title", out var tProp) ? tProp.GetString() : title;
                            releaseGroups = [new MusicBrainzReleaseGroupResult { Id = rgId, Title = rgTitle ?? title, Score = 100 }];
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse release-group from release {ReleaseId}", canonicalAlbum.MusicBrainzReleaseId);
                }
            }
        }

        // Priority 3: Artist MBID known
        if (releaseGroups is null && artistMbid is not null)
            releaseGroups = await _musicBrainzClient.SearchReleaseGroupByArtistMbidAsync(artistMbid, title, cancellationToken);

        // Priority 4: Text search fallback
        releaseGroups ??= await _musicBrainzClient.SearchReleaseGroupAsync(title, artistName, cancellationToken);

        if (releaseGroups is null || releaseGroups.Count == 0)
        {
            _logger.LogInformation("No MusicBrainz release group found for '{AlbumTitle}' by '{ArtistName}'", title, artistName);
            // Don't set LastEnrichedAt — allow retry on next scan
            return;
        }

        var topResult = releaseGroups[0];
        if (topResult.Score < MinMatchScore)
        {
            _logger.LogInformation("MusicBrainz release group match for '{AlbumTitle}' has low score {Score}, skipping", title, topResult.Score);
            // Don't set LastEnrichedAt — allow retry on next scan
            return;
        }

        canonicalAlbum.MusicBrainzReleaseGroupId = topResult.Id;
        var enrichmentSucceeded = false;

        var releaseGroup = await _musicBrainzClient.GetReleaseGroupAsync(topResult.Id, cancellationToken);
        if (releaseGroup?.Releases is { Count: > 0 })
        {
            var firstReleaseId = releaseGroup.Releases[0].Id;
            canonicalAlbum.MusicBrainzReleaseId = firstReleaseId;
            enrichmentSucceeded = true;

            var needsCoverArt = !canonicalAlbum.HasCoverArt || (canonicalAlbum.CoverArtHash is not null && !_contentStorage.Exists(canonicalAlbum.CoverArtHash));
            if (needsCoverArt)
            {
                var coverArt = await _coverArtClient.GetFrontCoverFromReleasesAsync(releaseGroup.Releases, cancellationToken);
                if (coverArt is not null)
                {
                    var artHash = CacheExternalArt(coverArt.Data, coverArt.MimeType);
                    if (artHash is not null)
                    {
                        canonicalAlbum.HasCoverArt = true;
                        canonicalAlbum.CoverArtHash = artHash;
                        canonicalAlbum.MusicBrainzReleaseId = coverArt.ReleaseMbid;
                        _logger.LogInformation("Fetched cover art for album '{AlbumTitle}' from release {ReleaseMbid}", title, coverArt.ReleaseMbid);
                    }
                }
                else
                {
                    _logger.LogInformation("No cover art available on Cover Art Archive for '{AlbumTitle}' ({ReleaseCount} releases tried)", title, releaseGroup.Releases.Count);
                }
            }
        }
        else
        {
            _logger.LogInformation("MusicBrainz release group for '{AlbumTitle}' has no releases — can't fetch cover art", title);
        }

        // Only set cooldown when cover art was actually fetched or already existed.
        // If we found the release group but couldn't get art, leave LastEnrichedAt
        // null so the next scan can retry.
        var gotCoverArt = canonicalAlbum.HasCoverArt;
        if (enrichmentSucceeded && gotCoverArt)
        {
            canonicalAlbum.LastEnrichedAt = DateTime.UtcNow;
            canonicalAlbum.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
        else if (enrichmentSucceeded)
        {
            // Found release group info but no cover art — save the MBIDs but don't
            // set cooldown so cover art fetch can retry on next scan.
            canonicalAlbum.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FetchArtSearchResult>> SearchArtCandidatesAsync(FetchArtSearchRequest request, CancellationToken cancellationToken = default)
    {
        var results = new List<FetchArtSearchResult>();

        // ── Search MusicBrainz in parallel ──
        var musicBrainzTask = SearchMusicBrainzAsync(request, cancellationToken);

        // ── Search TheAudioDB in parallel ──
        var audioDbTask = SearchAudioDbAsync(request, cancellationToken);

        await Task.WhenAll(musicBrainzTask, audioDbTask);

        if (musicBrainzTask.Result is { Count: > 0 })
            results.AddRange(musicBrainzTask.Result);

        if (audioDbTask.Result is { Count: > 0 })
            results.AddRange(audioDbTask.Result);

        // Deduplicate by title similarity within same source — keep highest score
        results = [.. results
            .GroupBy(r => (r.Source, r.Title.ToLowerInvariant()))
            .Select(g => g.OrderByDescending(r => r.Score).First())
            .OrderByDescending(r => r.Score)];

        return results;
    }

    /// <summary>
    /// Searches MusicBrainz release groups using the provided request parameters.
    /// Uses the same search strategy as <see cref="EnrichAlbumAsync"/> but returns
    /// all qualifying results instead of taking only the top one.
    /// </summary>
    private async Task<IReadOnlyList<FetchArtSearchResult>?> SearchMusicBrainzAsync(FetchArtSearchRequest request, CancellationToken cancellationToken)
    {
        IReadOnlyList<MusicBrainzReleaseGroupResult>? releaseGroups;

        // Priority 1: Artist MBID search (most precise)
        if (request.ArtistMbid is not null)
            releaseGroups = await _musicBrainzClient.SearchReleaseGroupByArtistMbidAsync(request.ArtistMbid, request.AlbumTitle, cancellationToken);
        else
            releaseGroups = null;

        // Priority 2: Text search fallback
        releaseGroups ??= await _musicBrainzClient.SearchReleaseGroupAsync(request.AlbumTitle, request.ArtistName, cancellationToken);

        if (releaseGroups is null || releaseGroups.Count == 0)
            return [];

        return releaseGroups
            .Select(rg => new FetchArtSearchResult
            {
                Source = "MusicBrainz",
                SourceId = rg.Id,
                Title = rg.Title,
                ArtistName = request.ArtistName,
                PrimaryType = rg.PrimaryType,
                Score = rg.Score,
                ThumbnailUrl = null, // Thumbnails fetched during Apply to keep search fast
                Year = request.Year
            })
            .ToList();
    }

    /// <summary>
    /// Searches TheAudioDB for album art using the provided request parameters.
    /// TheAudioDB returns cover art URLs directly in search results.
    /// </summary>
    private async Task<IReadOnlyList<FetchArtSearchResult>?> SearchAudioDbAsync(FetchArtSearchRequest request, CancellationToken cancellationToken)
    {
        var audioResults = await _audioDbClient.SearchAlbumAsync(request.AlbumTitle, request.ArtistName, cancellationToken);
        if (audioResults is null || audioResults.Count == 0)
            return [];

        return audioResults
            .Select((a, i) => new FetchArtSearchResult
            {
                Source = "TheAudioDB",
                SourceId = a.AlbumId,
                Title = a.AlbumTitle,
                ArtistName = a.ArtistName,
                PrimaryType = null,
                Score = Math.Max(100 - i * 5, 50), // AudioDB doesn't provide scores — descending by position
                ThumbnailUrl = a.ThumbnailUrl,
                Year = a.Year
            })
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<ApplyArtResult> ApplyArtSelectionAsync(Guid albumId, FetchArtApplyRequest request, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var canonicalAlbum = await _db.CanonicalAlbums
            .Include(a => a.AlbumArtists)
            .ThenInclude(aa => aa.Artist)
            .FirstOrDefaultAsync(a => a.Id == albumId, cancellationToken);

        if (canonicalAlbum is null)
        {
            _logger.LogDebug("Album {AlbumId} not found for art application", albumId);
            return new ApplyArtResult { Success = false, ErrorMessage = "Album not found." };
        }

        try
        {
            switch (request.Source)
            {
                case "MusicBrainz":
                    return await ApplyMusicBrainzArtAsync(canonicalAlbum, request.SourceId, cancellationToken);

                case "TheAudioDB":
                    return await ApplyAudioDbArtAsync(canonicalAlbum, request.SourceId, request.ThumbnailUrl, cancellationToken);

                default:
                    _logger.LogWarning("Unknown art source '{Source}' for album {AlbumId}", request.Source, albumId);
                    return new ApplyArtResult { Success = false, ErrorMessage = $"Unknown art source: {request.Source}." };
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to apply art from {Source} for album {AlbumId}", request.Source, albumId);
            return new ApplyArtResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// Applies art from a MusicBrainz release group: looks up releases, fetches cover from CAA, saves to storage.
    /// </summary>
    private async Task<ApplyArtResult> ApplyMusicBrainzArtAsync(Models.CanonicalAlbum canonicalAlbum, string releaseGroupId, CancellationToken cancellationToken)
    {
        // Get full release group details to find releases with cover art
        var releaseGroup = await _musicBrainzClient.GetReleaseGroupAsync(releaseGroupId, cancellationToken);
        if (releaseGroup?.Releases is not { Count: > 0 })
        {
            _logger.LogInformation("MusicBrainz release group {ReleaseGroupId} has no releases — can't fetch cover art", releaseGroupId);
            return new ApplyArtResult { Success = false, ErrorMessage = "No releases found in this release group." };
        }

        var firstReleaseId = releaseGroup.Releases[0].Id;
        canonicalAlbum.MusicBrainzReleaseGroupId = releaseGroupId;
        canonicalAlbum.MusicBrainzReleaseId = firstReleaseId;

        // Fetch cover art from Cover Art Archive
        var coverArt = await _coverArtClient.GetFrontCoverFromReleasesAsync(releaseGroup.Releases, cancellationToken);
        if (coverArt is null)
        {
            _logger.LogInformation("No cover art available on Cover Art Archive for release group {ReleaseGroupId}", releaseGroupId);
            return new ApplyArtResult { Success = false, ErrorMessage = "No cover art found on Cover Art Archive." };
        }

        var artHash = CacheExternalArt(coverArt.Data, coverArt.MimeType);
        if (artHash is null)
        {
            return new ApplyArtResult { Success = false, ErrorMessage = "Failed to cache cover art image." };
        }

        canonicalAlbum.HasCoverArt = true;
        canonicalAlbum.CoverArtHash = artHash;
        canonicalAlbum.MusicBrainzReleaseId = coverArt.ReleaseMbid;
        canonicalAlbum.LastEnrichedAt = DateTime.UtcNow;
        canonicalAlbum.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Applied MusicBrainz cover art for album '{AlbumTitle}' from release {ReleaseMbid}",
            canonicalAlbum.Title, coverArt.ReleaseMbid);

        return new ApplyArtResult { Success = true };
    }

    /// <summary>
    /// Applies art from TheAudioDB: downloads the image from the thumbnail URL and saves to storage.
    /// </summary>
    private async Task<ApplyArtResult> ApplyAudioDbArtAsync(Models.CanonicalAlbum canonicalAlbum, string albumId, string? thumbnailUrl, CancellationToken cancellationToken)
    {
        // Use the thumbnail URL passed from the search result. If missing, re-fetch from TheAudioDB.
        var imageUrl = thumbnailUrl;
        if (string.IsNullOrEmpty(imageUrl))
        {
            var artistName = canonicalAlbum.AlbumArtists.FirstOrDefault()?.Artist?.Name ?? "Unknown Artist";
            var audioResults = await _audioDbClient.SearchAlbumAsync(canonicalAlbum.Title, artistName, cancellationToken);
            var targetAlbum = audioResults?.FirstOrDefault(a => a.AlbumId == albumId);
            imageUrl = targetAlbum?.ThumbnailUrl;
        }

        if (string.IsNullOrEmpty(imageUrl))
        {
            _logger.LogInformation("No thumbnail URL available on TheAudioDB for album {AlbumId}", albumId);
            return new ApplyArtResult { Success = false, ErrorMessage = "No cover art URL available from TheAudioDB." };
        }

        // Download the image
        byte[] imageData;
        string mimeType;
        using (var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) })
        {
            var response = await httpClient.GetAsync(imageUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to download TheAudioDB cover art from {Url} (HTTP {StatusCode})",
                    imageUrl, (int)response.StatusCode);
                return new ApplyArtResult { Success = false, ErrorMessage = "Failed to download cover art image." };
            }

            imageData = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            mimeType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        }

        if (imageData.Length == 0)
        {
            return new ApplyArtResult { Success = false, ErrorMessage = "Downloaded image is empty." };
        }

        var artHash = CacheExternalArt(imageData, mimeType);
        if (artHash is null)
        {
            return new ApplyArtResult { Success = false, ErrorMessage = "Failed to cache cover art image." };
        }

        canonicalAlbum.HasCoverArt = true;
        canonicalAlbum.CoverArtHash = artHash;
        canonicalAlbum.LastEnrichedAt = DateTime.UtcNow;
        canonicalAlbum.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Applied TheAudioDB cover art for album '{AlbumTitle}' (AudioDB ID: {AlbumId})",
            canonicalAlbum.Title, albumId);

        return new ApplyArtResult { Success = true };
    }

    /// <inheritdoc/>
    public async Task EnrichArtistAsync(Guid artistId, CallerContext caller, bool force = false, CancellationToken cancellationToken = default)
    {
        var canonicalArtist = await _db.CanonicalArtists.FindAsync([artistId], cancellationToken);

        if (canonicalArtist is null)
        {
            _logger.LogDebug("Artist {ArtistId} not found for enrichment", artistId);
            return;
        }

        if (!force && canonicalArtist.LastEnrichedAt.HasValue &&
            DateTime.UtcNow - canonicalArtist.LastEnrichedAt.Value < EnrichmentCooldown)
        {
            _logger.LogDebug("Artist {ArtistId} was recently enriched, skipping", artistId);
            return;
        }

        var name = canonicalArtist.Name;
        _logger.LogInformation("Enriching artist '{ArtistName}'", name);

        MusicBrainzArtistDetail? detail = null;

        // Priority 1: Direct MBID lookup if already known
        if (canonicalArtist.MusicBrainzId is not null)
            detail = await _musicBrainzClient.GetArtistAsync(canonicalArtist.MusicBrainzId, cancellationToken);

        // Priority 2: Text search fallback
        if (detail is null)
        {
            var artists = await _musicBrainzClient.SearchArtistAsync(name, cancellationToken);
            if (artists is null || artists.Count == 0)
            {
                _logger.LogDebug("No MusicBrainz artist found for '{ArtistName}'", name);
                canonicalArtist.LastEnrichedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                return;
            }

            var topResult = artists[0];
            if (topResult.Score < MinMatchScore)
            {
                _logger.LogWarning("MusicBrainz artist match for '{ArtistName}' has low score {Score}, skipping", name, topResult.Score);
                canonicalArtist.LastEnrichedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                return;
            }

            detail = await _musicBrainzClient.GetArtistAsync(topResult.Id, cancellationToken);
        }

        // Write enrichment data to canonical artist
        if (detail is not null)
        {
            // Check if another canonical artist already has this MBID (unique index constraint)
            if (detail.Id != canonicalArtist.MusicBrainzId)
            {
                var existingOwner = await _db.CanonicalArtists
                    .AnyAsync(a => a.Id != canonicalArtist.Id && a.MusicBrainzId == detail.Id, cancellationToken);

                if (existingOwner)
                {
                    _logger.LogWarning(
                        "MBID '{Mbid}' already assigned to another canonical artist (different from '{ArtistId}'). Skipping MBID assignment.",
                        detail.Id, canonicalArtist.Id);
                }
                else
                {
                    canonicalArtist.MusicBrainzId = detail.Id;
                }
            }

            canonicalArtist.Biography = detail.Annotation;
            canonicalArtist.WikipediaUrl = detail.WikipediaUrl;
            canonicalArtist.DiscogsUrl = detail.DiscogsUrl;
            canonicalArtist.OfficialUrl = detail.OfficialUrl;

            _logger.LogInformation(
                "Enriched artist '{ArtistName}': bio={HasBio}, wikipedia={HasWiki}, discogs={HasDiscogs}, official={HasOfficial}",
                name,
                detail.Annotation is not null,
                detail.WikipediaUrl is not null,
                detail.DiscogsUrl is not null,
                detail.OfficialUrl is not null);

            // Fetch artist logo from TheAudioDB (only if we have an MBID)
            if (!string.IsNullOrWhiteSpace(detail.Id))
            {
                try
                {
                    var artwork = await _audioDbClient.GetArtistArtworkAsync(detail.Id, cancellationToken);
                    if (artwork?.LogoUrl is not null)
                    {
                        canonicalArtist.LogoUrl = artwork.LogoUrl;
                        _logger.LogInformation("Fetched logo for artist '{ArtistName}'", name);
                    }
                    else
                    {
                        _logger.LogDebug("No logo available on TheAudioDB for artist '{ArtistName}'", name);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Failed to fetch logo from TheAudioDB for artist '{ArtistName}'", name);
                }
            }
        }

        canonicalArtist.LastEnrichedAt = DateTime.UtcNow;
        canonicalArtist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Batch-fetches artist logos from TheAudioDB for artists that have an MBID but no logo yet.
    /// Runs requests concurrently (up to 10 at a time) since TheAudioDB allows ~20 req/s.
    /// </summary>
    private async Task<int> BatchFetchArtistLogosAsync(List<UserArtist> userArtists, CancellationToken cancellationToken)
    {
        // Collect artists that have an MBID but no logo yet
        var candidates = userArtists
            .Select(ua => ua.CanonicalArtist)
            .Where(ca => ca is not null && !string.IsNullOrWhiteSpace(ca.MusicBrainzId) && string.IsNullOrWhiteSpace(ca.LogoUrl))
            .Select(ca => (Id: ca!.Id, Mbid: ca.MusicBrainzId!, Name: ca.Name))
            .ToList();

        if (candidates.Count == 0)
            return 0;

        _logger.LogInformation("Batch fetching logos for {Count} artists from TheAudioDB", candidates.Count);

        var fetchedCount = 0;
        var semaphore = new SemaphoreSlim(10, 10);
        var tasks = new List<Task>();

        foreach (var (id, mbid, name) in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await semaphore.WaitAsync(cancellationToken);

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var artwork = await _audioDbClient.GetArtistArtworkAsync(mbid, cancellationToken);
                    if (artwork?.LogoUrl is not null)
                    {
                        lock (candidates)
                        {
                            var artist = _db.CanonicalArtists.Local.FirstOrDefault(a => a.Id == id);
                            if (artist is not null)
                            {
                                artist.LogoUrl = artwork.LogoUrl;
                                artist.UpdatedAt = DateTime.UtcNow;
                                Interlocked.Increment(ref fetchedCount);
                                _logger.LogInformation("Fetched logo for artist '{ArtistName}'", name);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogDebug("No logo available on TheAudioDB for artist '{ArtistName}'", name);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Failed to fetch logo from TheAudioDB for artist '{ArtistName}'", name);
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks);
        await _db.SaveChangesAsync(cancellationToken);

        return fetchedCount;
    }

    /// <inheritdoc/>
    public async Task EnrichTrackAsync(Guid trackId, CallerContext caller, bool force = false, CancellationToken cancellationToken = default)
    {
        var userTrack = await _db.UserTracks
            .IgnoreQueryFilters()
            .Include(ut => ut.CanonicalTrack)
            .FirstOrDefaultAsync(ut => ut.Id == trackId, cancellationToken);

        if (userTrack?.CanonicalTrack is null)
        {
            _logger.LogDebug("Track {TrackId} not found for enrichment", trackId);
            return;
        }

        var canonicalTrack = userTrack.CanonicalTrack;
        var ct = canonicalTrack;

        if (!force && ct.UpdatedAt != default &&
            DateTime.UtcNow - ct.UpdatedAt < EnrichmentCooldown)
        {
            _logger.LogDebug("Track {TrackId} was recently enriched, skipping", trackId);
            return;
        }

        var title = ct.Title;

        // Resolve artist info from CanonicalTrackArtist
        string? artistMbid = null;
        string artistName = "Unknown Artist";
        var trackArtist = await _db.CanonicalTrackArtists
            .Include(ta => ta.Artist)
            .FirstOrDefaultAsync(ta => ta.TrackContentHash == ct.ContentHash, cancellationToken);
        if (trackArtist?.Artist is not null)
        {
            artistName = trackArtist.Artist.Name;
            artistMbid = trackArtist.Artist.MusicBrainzId;
        }

        _logger.LogDebug("Enriching track '{TrackTitle}' by '{ArtistName}'", title, artistName);

        // ── Priority-based MusicBrainz lookup ──

        IReadOnlyList<MusicBrainzRecordingResult>? recordings = null;

        // Priority 1: Direct MBID lookup if recording ID already known
        if (recordings is null && ct.MusicBrainzRecordingId is not null)
        {
            var recordingDetail = await _musicBrainzClient.GetRecordingAsync(ct.MusicBrainzRecordingId!, cancellationToken);
            if (recordingDetail is not null)
            {
                recordings = [new MusicBrainzRecordingResult { Id = recordingDetail.Id, Title = recordingDetail.Title, Score = 100, Length = recordingDetail.Length }];
                _logger.LogDebug("Used direct recording MBID lookup: {Mbid}", ct.MusicBrainzRecordingId);
            }
        }

        // Priority 2: Artist MBID known — search with arid:{mbid} AND recording:"{title}"
        if (recordings is null && artistMbid is not null)
        {
            recordings = await _musicBrainzClient.SearchRecordingByArtistMbidAsync(artistMbid, title, cancellationToken);
            _logger.LogDebug("Searched recording by artist MBID: {Mbid}, found {Count} results", artistMbid, recordings?.Count ?? 0);
        }

        // Priority 3: Text search fallback
        if (recordings is null || recordings.Count == 0)
        {
            recordings = await _musicBrainzClient.SearchRecordingAsync(title, artistName, cancellationToken);
        }

        if (recordings is not null && recordings.Count > 0)
        {
            var topResult = recordings[0];

            if (topResult.Score >= MinMatchScore)
            {
                // Verify recording length vs track duration (±2s tolerance)
                var durationTicks = ct.DurationTicks;
                if (durationTicks > 0 && topResult.Length.HasValue)
                {
                    var durationMs = durationTicks / 10_000;
                    var diffMs = Math.Abs(topResult.Length.Value - (int)durationMs);
                    if (diffMs > 2000)
                    {
                        _logger.LogWarning(
                            "MusicBrainz recording '{RecordingTitle}' length ({Length}ms) differs from track duration ({Duration}ms) by {Diff}ms, rejecting",
                            topResult.Title, topResult.Length, durationMs, diffMs);
                    }
                    else
                    {
                        ApplyTrackEnrichment(ct, topResult.Id, title);
                    }
                }
                else
                {
                    ApplyTrackEnrichment(ct, topResult.Id, title);
                }
            }
            else
            {
                _logger.LogDebug("MusicBrainz recording match for '{TrackTitle}' has low score {Score}, skipping", title, topResult.Score);
            }
        }

        ct.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static void ApplyTrackEnrichment(CanonicalTrack canonicalTrack, string recordingId, string title)
    {
        canonicalTrack.MusicBrainzRecordingId = recordingId;
    }

    /// <inheritdoc/>
    public async Task<int> EnrichArtistLogosAsync(Guid ownerId, IProgress<EnrichmentProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var userArtists = await _db.UserArtists
            .Include(ua => ua.CanonicalArtist)
            .Where(ua => ua.OwnerId == ownerId && ua.CanonicalArtist != null
                && ua.CanonicalArtist.MusicBrainzId != null
                && ua.CanonicalArtist.LogoUrl == null)
            .ToListAsync(cancellationToken);

        var count = await BatchFetchArtistLogosAsync(userArtists, cancellationToken);

        _logger.LogInformation("Standalone logo fetch complete: {Count} logos retrieved from TheAudioDB", count);
        return count;
    }

    /// <inheritdoc/>
    public async Task EnrichAlbumsWithoutArtAsync(Guid ownerId, IProgress<EnrichmentProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var canonicalAlbumsWithoutArt = await _db.CanonicalAlbums
            .Where(a => !a.HasCoverArt)
            .ToListAsync(cancellationToken);

        var totalAlbums = canonicalAlbumsWithoutArt.Count;
        _logger.LogInformation("Found {Count} albums without cover art", totalAlbums);

        var artFound = 0;
        var caller = new CallerContext(ownerId, ["user"], CallerType.User);
        var processedCount = 0;

        for (var i = 0; i < canonicalAlbumsWithoutArt.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var canonicalAlbum = canonicalAlbumsWithoutArt[i];
            processedCount++;
            progress?.Report(new EnrichmentProgress
            {
                Phase = "Fetching cover art...",
                Current = processedCount,
                Total = totalAlbums,
                CurrentItem = canonicalAlbum.Title,
                AlbumArtFound = artFound,
                AlbumArtRemaining = Math.Max(0, totalAlbums - processedCount),
                ArtistBiosFound = 0
            });

            // Find the first user album referencing this canonical album for the caller context
            var userAlbum = await _db.UserAlbums
                .FirstOrDefaultAsync(ua => ua.CanonicalAlbumId == canonicalAlbum.Id && ua.OwnerId == ownerId, cancellationToken);

            if (userAlbum is null)
                continue;

            await EnrichAlbumAsync(userAlbum.CanonicalAlbumId, caller, cancellationToken: cancellationToken);

            // Re-check if art was found after enrichment
            await _db.Entry(canonicalAlbum).ReloadAsync(cancellationToken);
            if (canonicalAlbum.HasCoverArt)
            {
                artFound++;
            }
        }

        _logger.LogInformation("Cover art enrichment complete: {ArtFound} total", artFound);
    }

    /// <inheritdoc/>
    public async Task EnrichAllAsync(Guid ownerId, IProgress<EnrichmentProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var caller = new CallerContext(ownerId, ["user"], CallerType.User);
        var artFound = 0;
        var biosFound = 0;

        // Phase 1: Enrich artists — operate on canonical artists via user junctions
        var userArtists = await _db.UserArtists
            .Include(ua => ua.CanonicalArtist)
            .Where(ua => ua.OwnerId == ownerId && ua.CanonicalArtist != null
                && (ua.CanonicalArtist.LastEnrichedAt == null))
            .ToListAsync(cancellationToken);

        var totalArtists = userArtists.Count;
        _logger.LogInformation("Enriching {Count} artists for user {OwnerId}", totalArtists, ownerId);

        var artistIdx = 0;
        foreach (var ua in userArtists)
        {
            cancellationToken.ThrowIfCancellationRequested();

            artistIdx++;
            var name = ua.CanonicalArtist?.Name ?? "Unknown";
            progress?.Report(new EnrichmentProgress
            {
                Phase = "Enriching artists...",
                Current = artistIdx,
                Total = totalArtists,
                CurrentItem = name,
                AlbumArtFound = artFound,
                AlbumArtRemaining = 0,
                ArtistBiosFound = biosFound
            });

            await EnrichArtistAsync(ua.CanonicalArtistId, caller, cancellationToken: cancellationToken);

            if (ua.CanonicalArtist?.Biography is not null)
            {
                biosFound++;
            }
        }

        // Phase 1b: Batch-fetch artist logos from TheAudioDB (high concurrency allowed — 20 req/s)
        var logoCount = await BatchFetchArtistLogosAsync(userArtists, cancellationToken);

        if (logoCount > 0)
        {
            _logger.LogInformation("Batch logo fetch complete: {LogoCount} logos retrieved from TheAudioDB", logoCount);
        }

        // Phase 2: Enrich albums — operate on user albums referencing canonical
        var userAlbums = await _db.UserAlbums
            .Include(ua => ua.CanonicalAlbum)
            .Where(ua => ua.OwnerId == ownerId && ua.CanonicalAlbum != null
                && ua.CanonicalAlbum.LastEnrichedAt == null)
            .ToListAsync(cancellationToken);

        var totalAlbums = userAlbums.Count;
        var pendingAlbumArtLookups = userAlbums.Count(ua => !ua.CanonicalAlbum!.HasCoverArt);

        _logger.LogInformation("Enriching {Count} albums for user {OwnerId}", totalAlbums, ownerId);

        var albumIdx = 0;
        foreach (var ua in userAlbums)
        {
            cancellationToken.ThrowIfCancellationRequested();

            albumIdx++;
            var title = ua.CanonicalAlbum?.Title ?? "Unknown";
            var needsAlbumArt = !ua.CanonicalAlbum!.HasCoverArt;

            progress?.Report(new EnrichmentProgress
            {
                Phase = "Enriching albums...",
                Current = albumIdx,
                Total = totalAlbums,
                CurrentItem = title,
                AlbumArtFound = artFound,
                AlbumArtRemaining = Math.Max(0, pendingAlbumArtLookups - (needsAlbumArt ? 1 : 0)),
                ArtistBiosFound = biosFound
            });

            await EnrichAlbumAsync(ua.CanonicalAlbumId, caller, cancellationToken: cancellationToken);

            await _db.Entry(ua.CanonicalAlbum).ReloadAsync(cancellationToken);
            if (ua.CanonicalAlbum.HasCoverArt)
            {
                artFound++;
            }
            if (needsAlbumArt)
            {
                pendingAlbumArtLookups = Math.Max(0, pendingAlbumArtLookups - 1);
            }
        }

        // Phase 3: Enrich tracks
        var userTracks = await _db.UserTracks
            .Include(ut => ut.CanonicalTrack)
            .Where(ut => ut.OwnerId == ownerId && ut.CanonicalTrack != null
                && ut.CanonicalTrack.UpdatedAt == default)
            .ToListAsync(cancellationToken);

        var totalTracks = userTracks.Count;
        _logger.LogInformation("Enriching {Count} tracks for user {OwnerId}", totalTracks, ownerId);

        var trackIdx = 0;
        foreach (var ut in userTracks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            trackIdx++;
            progress?.Report(new EnrichmentProgress
            {
                Phase = "Enriching tracks...",
                Current = trackIdx,
                Total = totalTracks,
                CurrentItem = ut.CanonicalTrack?.Title ?? "Unknown",
                AlbumArtFound = artFound,
                AlbumArtRemaining = pendingAlbumArtLookups,
                ArtistBiosFound = biosFound
            });

            await EnrichTrackAsync(ut.Id, caller, cancellationToken: cancellationToken);
        }

        _logger.LogInformation(
            "Full enrichment complete for user {OwnerId}: {ArtFound} covers, {BiosFound} bios",
            ownerId, artFound, biosFound);
    }

    /// <summary>
    /// Sends a rate-limited GET request to MusicBrainz and returns the raw JSON string.
    /// </summary>
    private async Task<string?> GetMusicBrainzJsonAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        // This reuses the same rate-limited approach as the MusicBrainzClient;
        // the HTTP client is configured with the same base address and User-Agent.
        try
        {
            using var response = await _httpClient.GetAsync(relativeUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("MusicBrainz returned {StatusCode} for {Url}", (int)response.StatusCode, relativeUrl);
                return null;
            }
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error calling MusicBrainz for {Url}", relativeUrl);
            return null;
        }
    }

    /// <summary>
    /// Caches externally-fetched art data using content-addressed storage.
    /// </summary>
    private string? CacheExternalArt(byte[] data, string mimeType)
    {
        try
        {
            var extension = mimeType.Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
            var hash = _contentStorage.Store(data, extension);
            _logger.LogDebug("Cached external album art with content hash {Hash}", hash);
            return hash;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache external album art");
            return null;
        }
    }
}
