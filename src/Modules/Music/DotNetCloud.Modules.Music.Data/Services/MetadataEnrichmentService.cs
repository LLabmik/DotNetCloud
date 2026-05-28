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
        AlbumArtService albumArtService,
        ContentAddressedStorage contentStorage,
        IConfiguration configuration,
        ILogger<MetadataEnrichmentService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _musicBrainzClient = musicBrainzClient;
        _coverArtClient = coverArtClient;
        _albumArtService = albumArtService;
        _contentStorage = contentStorage;
        _httpClient = httpClientFactory.CreateClient("MusicBrainz");
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task EnrichAlbumAsync(Guid albumId, CallerContext caller, bool force = false, CancellationToken cancellationToken = default)
    {
        // Resolve UserAlbum -> CanonicalAlbum
        var userAlbum = await _db.UserAlbums
            .Include(ua => ua.CanonicalAlbum)
            .FirstOrDefaultAsync(ua => ua.Id == albumId && ua.OwnerId == caller.UserId, cancellationToken);

        // Fallback to old table
        var oldAlbum = await _db.Albums
            .Include(a => a.Artist)
            .FirstOrDefaultAsync(a => a.Id == albumId, cancellationToken);

        var canonicalAlbum = userAlbum?.CanonicalAlbum;

        if (canonicalAlbum is null && oldAlbum is null)
        {
            _logger.LogDebug("Album {AlbumId} not found for enrichment", albumId);
            return;
        }

        // Check cooldown on canonical first, fall back to old
        if (canonicalAlbum is not null && !force && canonicalAlbum.LastEnrichedAt.HasValue &&
            DateTime.UtcNow - canonicalAlbum.LastEnrichedAt.Value < EnrichmentCooldown)
        {
            _logger.LogDebug("Album {AlbumId} was recently enriched (canonical), skipping", albumId);
            return;
        }

        if (canonicalAlbum is null && oldAlbum is not null && !force && oldAlbum.LastEnrichedAt.HasValue &&
            DateTime.UtcNow - oldAlbum.LastEnrichedAt.Value < EnrichmentCooldown)
        {
            _logger.LogDebug("Album {AlbumId} was recently enriched (old table), skipping", albumId);
            return;
        }

        var title = canonicalAlbum?.Title ?? oldAlbum?.Title ?? "Unknown Album";

        // Resolve artist info — try canonical first
        string? artistMbid = null;
        string artistName = "Unknown Artist";
        if (canonicalAlbum is not null)
        {
            var albumArtist = await _db.CanonicalAlbumArtists
                .Include(aa => aa.Artist)
                .FirstOrDefaultAsync(aa => aa.AlbumId == canonicalAlbum.Id, cancellationToken);
            if (albumArtist?.Artist is not null)
            {
                artistName = albumArtist.Artist.Name;
                artistMbid = albumArtist.Artist.MusicBrainzId;
            }
        }
        else if (oldAlbum?.Artist is not null)
        {
            artistName = oldAlbum.Artist.Name;
        }

        _logger.LogInformation("Enriching album '{AlbumTitle}' by '{ArtistName}'", title, artistName);

        // ── Priority-based MusicBrainz lookup ──

        IReadOnlyList<MusicBrainzReleaseGroupResult>? releaseGroups = null;

        // Priority 1: Direct MBID lookup (release group ID already known)
        if (releaseGroups is null && canonicalAlbum?.MusicBrainzReleaseGroupId is not null)
        {
            var rgDetail = await _musicBrainzClient.GetReleaseGroupAsync(canonicalAlbum.MusicBrainzReleaseGroupId, cancellationToken);
            if (rgDetail is not null)
            {
                releaseGroups = [new MusicBrainzReleaseGroupResult { Id = rgDetail.Id, Title = rgDetail.Title, Score = 100 }];
                _logger.LogDebug("Used direct release group MBID lookup: {Mbid}", canonicalAlbum.MusicBrainzReleaseGroupId);
            }
        }

        // Priority 2: Release MBID known — get release to find its release group
        if (releaseGroups is null && canonicalAlbum?.MusicBrainzReleaseId is not null)
        {
            // Fetch the release group from the known release via the release-group lookup
            // MusicBrainz releases contain a release-group reference; get the release to discover it
            // Use: /release/{id}?inc=release-groups&fmt=json
            var releaseUrl = $"release/{Uri.EscapeDataString(canonicalAlbum.MusicBrainzReleaseId)}?inc=release-groups&fmt=json";
            var releaseJson = await GetMusicBrainzJsonAsync(releaseUrl, cancellationToken);
            if (releaseJson is not null)
            {
                try
                {
                    using var doc = JsonDocument.Parse(releaseJson);
                    var rgArray = doc.RootElement.TryGetProperty("release-group", out var rgElement)
                        ? new[] { rgElement }
                        : [];

                    foreach (var rg in rgArray)
                    {
                        if (rg.TryGetProperty("id", out var idProp))
                        {
                            var rgId = idProp.GetString();
                            if (rgId is not null)
                            {
                                var rgTitle = rg.TryGetProperty("title", out var tProp) ? tProp.GetString() : title;
                                releaseGroups = [new MusicBrainzReleaseGroupResult { Id = rgId, Title = rgTitle ?? title, Score = 100 }];
                                _logger.LogDebug("Resolved release group {RgId} from release MBID {ReleaseId}", rgId, canonicalAlbum.MusicBrainzReleaseId);
                                break;
                            }
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse release-group from release {ReleaseId}", canonicalAlbum.MusicBrainzReleaseId);
                }
            }
        }

        // Priority 3: Artist MBID known — search with arid:{mbid} AND releasegroup:"{title}"
        if (releaseGroups is null && artistMbid is not null)
        {
            releaseGroups = await _musicBrainzClient.SearchReleaseGroupByArtistMbidAsync(artistMbid, title, cancellationToken);
            _logger.LogDebug("Searched by artist MBID: {Mbid}, found {Count} results", artistMbid, releaseGroups?.Count ?? 0);
        }

        // Priority 4: Text search fallback
        if (releaseGroups is null || releaseGroups.Count == 0)
        {
            releaseGroups = await _musicBrainzClient.SearchReleaseGroupAsync(title, artistName, cancellationToken);
        }

        if (releaseGroups is null || releaseGroups.Count == 0)
        {
            _logger.LogDebug("No MusicBrainz release group found for '{AlbumTitle}' by '{ArtistName}'", title, artistName);
            if (canonicalAlbum is not null)
                canonicalAlbum.LastEnrichedAt = DateTime.UtcNow;
            if (oldAlbum is not null)
                oldAlbum.LastEnrichedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var topResult = releaseGroups[0];
        if (topResult.Score < MinMatchScore)
        {
            _logger.LogWarning("MusicBrainz release group match for '{AlbumTitle}' has low score {Score}, skipping", title, topResult.Score);
            if (canonicalAlbum is not null)
                canonicalAlbum.LastEnrichedAt = DateTime.UtcNow;
            if (oldAlbum is not null)
                oldAlbum.LastEnrichedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        // Write to canonical
        if (canonicalAlbum is not null)
        {
            canonicalAlbum.MusicBrainzReleaseGroupId = topResult.Id;
        }

        // Get release group details (with releases) for cover art lookup
        var releaseGroup = await _musicBrainzClient.GetReleaseGroupAsync(topResult.Id, cancellationToken);
        if (releaseGroup?.Releases is not null && releaseGroup.Releases.Count > 0)
        {
            var firstReleaseId = releaseGroup.Releases[0].Id;

            if (canonicalAlbum is not null)
            {
                canonicalAlbum.MusicBrainzReleaseId = firstReleaseId;
            }

            // Fetch cover art if album doesn't have it or the cached hash is missing
            var needsCoverArt = false;
            if (canonicalAlbum is not null)
            {
                needsCoverArt = !canonicalAlbum.HasCoverArt || (canonicalAlbum.CoverArtHash is not null && !_contentStorage.Exists(canonicalAlbum.CoverArtHash));
            }
            else if (oldAlbum is not null)
            {
                needsCoverArt = !oldAlbum.HasCoverArt || (oldAlbum.CoverArtPath is not null && !_contentStorage.Exists(oldAlbum.CoverArtPath));
            }

            if (needsCoverArt)
            {
                var coverArt = await _coverArtClient.GetFrontCoverFromReleasesAsync(releaseGroup.Releases, cancellationToken);
                if (coverArt is not null)
                {
                    var artHash = CacheExternalArt(coverArt.Data, coverArt.MimeType);
                    if (artHash is not null)
                    {
                        if (canonicalAlbum is not null)
                        {
                            canonicalAlbum.HasCoverArt = true;
                            canonicalAlbum.CoverArtHash = artHash;
                            canonicalAlbum.MusicBrainzReleaseId = coverArt.ReleaseMbid;
                        }

                        // Dual-write to old table
                        if (oldAlbum is not null)
                        {
                            oldAlbum.HasCoverArt = true;
                            oldAlbum.CoverArtPath = artHash;
                            oldAlbum.MusicBrainzReleaseId = coverArt.ReleaseMbid;
                        }

                        _logger.LogInformation("Fetched cover art for album '{AlbumTitle}' from release {ReleaseMbid}", title, coverArt.ReleaseMbid);
                    }
                }
            }
        }

        // Dual-write: update old MusicAlbum fields
        if (oldAlbum is not null)
        {
            oldAlbum.MusicBrainzReleaseGroupId = topResult.Id;
            if (releaseGroup?.Releases is { Count: > 0 })
            {
                oldAlbum.MusicBrainzReleaseId ??= releaseGroup.Releases[0].Id;
            }
            oldAlbum.LastEnrichedAt = DateTime.UtcNow;
            oldAlbum.UpdatedAt = DateTime.UtcNow;
        }

        if (canonicalAlbum is not null)
        {
            canonicalAlbum.LastEnrichedAt = DateTime.UtcNow;
            canonicalAlbum.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task EnrichArtistAsync(Guid artistId, CallerContext caller, bool force = false, CancellationToken cancellationToken = default)
    {
        // Resolve UserArtist -> CanonicalArtist
        var userArtist = await _db.UserArtists
            .Include(ua => ua.CanonicalArtist)
            .FirstOrDefaultAsync(ua => ua.Id == artistId && ua.OwnerId == caller.UserId, cancellationToken);

        // Fallback to old table
        var oldArtist = await _db.Artists
            .FirstOrDefaultAsync(a => a.Id == artistId, cancellationToken);

        var canonicalArtist = userArtist?.CanonicalArtist;

        if (canonicalArtist is null && oldArtist is null)
        {
            _logger.LogDebug("Artist {ArtistId} not found for enrichment", artistId);
            return;
        }

        // Check cooldown on canonical first
        if (canonicalArtist is not null && !force && canonicalArtist.LastEnrichedAt.HasValue &&
            DateTime.UtcNow - canonicalArtist.LastEnrichedAt.Value < EnrichmentCooldown)
        {
            _logger.LogDebug("Artist {ArtistId} was recently enriched (canonical), skipping", artistId);
            return;
        }

        if (canonicalArtist is null && oldArtist is not null && !force && oldArtist.LastEnrichedAt.HasValue &&
            DateTime.UtcNow - oldArtist.LastEnrichedAt.Value < EnrichmentCooldown)
        {
            _logger.LogDebug("Artist {ArtistId} was recently enriched (old table), skipping", artistId);
            return;
        }

        var name = canonicalArtist?.Name ?? oldArtist?.Name ?? "Unknown Artist";
        _logger.LogInformation("Enriching artist '{ArtistName}'", name);

        // ── Priority-based MusicBrainz lookup ──

        MusicBrainzArtistDetail? detail = null;

        // Priority 1: Direct MBID lookup if already known
        if (canonicalArtist?.MusicBrainzId is not null)
        {
            detail = await _musicBrainzClient.GetArtistAsync(canonicalArtist.MusicBrainzId, cancellationToken);
            if (detail is not null)
            {
                _logger.LogDebug("Used direct artist MBID lookup: {Mbid}", canonicalArtist.MusicBrainzId);
            }
        }

        // Priority 2: Text search fallback
        if (detail is null)
        {
            var artists = await _musicBrainzClient.SearchArtistAsync(name, cancellationToken);
            if (artists is null || artists.Count == 0)
            {
                _logger.LogDebug("No MusicBrainz artist found for '{ArtistName}'", name);
                if (canonicalArtist is not null)
                    canonicalArtist.LastEnrichedAt = DateTime.UtcNow;
                if (oldArtist is not null)
                    oldArtist.LastEnrichedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                return;
            }

            var topResult = artists[0];
            if (topResult.Score < MinMatchScore)
            {
                _logger.LogWarning("MusicBrainz artist match for '{ArtistName}' has low score {Score}, skipping", name, topResult.Score);
                if (canonicalArtist is not null)
                    canonicalArtist.LastEnrichedAt = DateTime.UtcNow;
                if (oldArtist is not null)
                    oldArtist.LastEnrichedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                return;
            }

            detail = await _musicBrainzClient.GetArtistAsync(topResult.Id, cancellationToken);
        }

        // Write to canonical
        if (canonicalArtist is not null && detail is not null)
        {
            canonicalArtist.MusicBrainzId = detail.Id;
            canonicalArtist.Biography = detail.Annotation;
            canonicalArtist.WikipediaUrl = detail.WikipediaUrl;
            canonicalArtist.DiscogsUrl = detail.DiscogsUrl;
            canonicalArtist.OfficialUrl = detail.OfficialUrl;
        }

        // Dual-write to old table
        if (oldArtist is not null && detail is not null)
        {
            oldArtist.MusicBrainzId = detail.Id;
            oldArtist.Biography = detail.Annotation;
            oldArtist.WikipediaUrl = detail.WikipediaUrl;
            oldArtist.DiscogsUrl = detail.DiscogsUrl;
            oldArtist.OfficialUrl = detail.OfficialUrl;
        }

        if (detail is not null)
        {
            _logger.LogInformation(
                "Enriched artist '{ArtistName}': bio={HasBio}, wikipedia={HasWiki}, discogs={HasDiscogs}, official={HasOfficial}",
                name,
                detail.Annotation is not null,
                detail.WikipediaUrl is not null,
                detail.DiscogsUrl is not null,
                detail.OfficialUrl is not null);
        }

        if (canonicalArtist is not null)
        {
            canonicalArtist.LastEnrichedAt = DateTime.UtcNow;
            canonicalArtist.UpdatedAt = DateTime.UtcNow;
        }

        if (oldArtist is not null)
        {
            oldArtist.LastEnrichedAt = DateTime.UtcNow;
            oldArtist.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task EnrichTrackAsync(Guid trackId, CallerContext caller, bool force = false, CancellationToken cancellationToken = default)
    {
        // Resolve UserTrack -> CanonicalTrack
        var userTrack = await _db.UserTracks
            .Include(ut => ut.CanonicalTrack)
            .FirstOrDefaultAsync(ut => ut.Id == trackId && ut.OwnerId == caller.UserId, cancellationToken);

        // Fallback to old table
        var oldTrack = await _db.Tracks
            .Include(t => t.TrackArtists)
                .ThenInclude(ta => ta.Artist)
            .FirstOrDefaultAsync(t => t.Id == trackId, cancellationToken);

        var canonicalTrack = userTrack?.CanonicalTrack;

        if (canonicalTrack is null && oldTrack is null)
        {
            _logger.LogDebug("Track {TrackId} not found for enrichment", trackId);
            return;
        }

        if (canonicalTrack is not null && !force && canonicalTrack.UpdatedAt != default &&
            DateTime.UtcNow - canonicalTrack.UpdatedAt < EnrichmentCooldown)
        {
            _logger.LogDebug("Track {TrackId} was recently enriched (canonical), skipping", trackId);
            return;
        }

        if (canonicalTrack is null && oldTrack is not null && !force && oldTrack.LastEnrichedAt.HasValue &&
            DateTime.UtcNow - oldTrack.LastEnrichedAt.Value < EnrichmentCooldown)
        {
            _logger.LogDebug("Track {TrackId} was recently enriched (old table), skipping", trackId);
            return;
        }

        var title = canonicalTrack?.Title ?? oldTrack?.Title ?? "Unknown Track";

        // Resolve artist info
        string? artistMbid = null;
        string artistName = "Unknown Artist";
        if (canonicalTrack is not null)
        {
            var trackArtist = await _db.CanonicalTrackArtists
                .Include(ta => ta.Artist)
                .FirstOrDefaultAsync(ta => ta.TrackContentHash == canonicalTrack.ContentHash, cancellationToken);
            if (trackArtist?.Artist is not null)
            {
                artistName = trackArtist.Artist.Name;
                artistMbid = trackArtist.Artist.MusicBrainzId;
            }
        }
        else if (oldTrack?.TrackArtists is not null)
        {
            var primary = oldTrack.TrackArtists.FirstOrDefault(ta => ta.IsPrimary);
            if (primary?.Artist is not null)
            {
                artistName = primary.Artist.Name;
            }
        }

        _logger.LogDebug("Enriching track '{TrackTitle}' by '{ArtistName}'", title, artistName);

        // ── Priority-based MusicBrainz lookup ──

        IReadOnlyList<MusicBrainzRecordingResult>? recordings = null;

        // Priority 1: Direct MBID lookup if recording ID already known
        if (recordings is null && canonicalTrack?.MusicBrainzRecordingId is not null)
        {
            var recordingDetail = await _musicBrainzClient.GetRecordingAsync(canonicalTrack.MusicBrainzRecordingId, cancellationToken);
            if (recordingDetail is not null)
            {
                recordings = [new MusicBrainzRecordingResult { Id = recordingDetail.Id, Title = recordingDetail.Title, Score = 100, Length = recordingDetail.Length }];
                _logger.LogDebug("Used direct recording MBID lookup: {Mbid}", canonicalTrack.MusicBrainzRecordingId);
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
                var durationTicks = canonicalTrack?.DurationTicks ?? oldTrack?.DurationTicks ?? 0;
                if (durationTicks > 0 && topResult.Length.HasValue)
                {
                    var durationMs = durationTicks / 10_000; // Convert ticks to milliseconds
                    var diffMs = Math.Abs(topResult.Length.Value - (int)durationMs);
                    if (diffMs > 2000) // ±2 seconds
                    {
                        _logger.LogWarning(
                            "MusicBrainz recording '{RecordingTitle}' length ({Length}ms) differs from track duration ({Duration}ms) by {Diff}ms, rejecting",
                            topResult.Title, topResult.Length, durationMs, diffMs);
                    }
                    else
                    {
                        ApplyTrackEnrichment(canonicalTrack, oldTrack, topResult.Id, title);
                    }
                }
                else
                {
                    ApplyTrackEnrichment(canonicalTrack, oldTrack, topResult.Id, title);
                }
            }
            else
            {
                _logger.LogDebug("MusicBrainz recording match for '{TrackTitle}' has low score {Score}, skipping", title, topResult.Score);
            }
        }

        if (canonicalTrack is not null)
        {
            canonicalTrack.UpdatedAt = DateTime.UtcNow;
        }

        if (oldTrack is not null)
        {
            oldTrack.LastEnrichedAt = DateTime.UtcNow;
            oldTrack.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Applies MusicBrainz recording enrichment to canonical and old track entities.
    /// </summary>
    private static void ApplyTrackEnrichment(CanonicalTrack? canonicalTrack, Track? oldTrack, string recordingId, string title)
    {
        if (canonicalTrack is not null)
        {
            canonicalTrack.MusicBrainzRecordingId = recordingId;
        }

        if (oldTrack is not null)
        {
            oldTrack.MusicBrainzRecordingId = recordingId;
        }
    }

    /// <inheritdoc/>
    public async Task EnrichAlbumsWithoutArtAsync(Guid ownerId, IProgress<EnrichmentProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var canonicalAlbumsWithoutArt = await _db.CanonicalAlbums
            .Where(a => !a.HasCoverArt)
            .ToListAsync(cancellationToken);

        var oldAlbumsWithoutArt = await _db.Albums
            .Include(a => a.Artist)
            .Where(a => a.OwnerId == ownerId && !a.HasCoverArt)
            .ToListAsync(cancellationToken);

        var totalAlbums = canonicalAlbumsWithoutArt.Count + oldAlbumsWithoutArt.Count;
        _logger.LogInformation("Found {Count} albums without cover art ({Canonical} canonical, {Legacy} legacy)",
            totalAlbums, canonicalAlbumsWithoutArt.Count, oldAlbumsWithoutArt.Count);

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

            await EnrichAlbumAsync(userAlbum.Id, caller, cancellationToken: cancellationToken);

            // Re-check if art was found after enrichment
            await _db.Entry(canonicalAlbum).ReloadAsync(cancellationToken);
            if (canonicalAlbum.HasCoverArt)
            {
                artFound++;
            }
        }

        // Also process old table albums without art (legacy)
        for (var i = 0; i < oldAlbumsWithoutArt.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var oldAlbum = oldAlbumsWithoutArt[i];
            processedCount++;
            progress?.Report(new EnrichmentProgress
            {
                Phase = "Fetching cover art (legacy)...",
                Current = processedCount,
                Total = totalAlbums,
                CurrentItem = oldAlbum.Title,
                AlbumArtFound = artFound,
                AlbumArtRemaining = Math.Max(0, totalAlbums - processedCount),
                ArtistBiosFound = 0
            });

            await EnrichAlbumAsync(oldAlbum.Id, caller, cancellationToken: cancellationToken);

            await _db.Entry(oldAlbum).ReloadAsync(cancellationToken);
            if (oldAlbum.HasCoverArt)
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

        // Also include old table artists as fallback
        var oldUnenrichedArtists = await _db.Artists
            .Where(a => a.OwnerId == ownerId && a.LastEnrichedAt == null)
            .ToListAsync(cancellationToken);

        var totalArtists = userArtists.Count + oldUnenrichedArtists.Count;
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

            await EnrichArtistAsync(ua.Id, caller, cancellationToken: cancellationToken);

            if (ua.CanonicalArtist?.Biography is not null)
            {
                biosFound++;
            }
        }

        foreach (var artist in oldUnenrichedArtists)
        {
            cancellationToken.ThrowIfCancellationRequested();

            artistIdx++;
            progress?.Report(new EnrichmentProgress
            {
                Phase = "Enriching artists (legacy)...",
                Current = artistIdx,
                Total = totalArtists,
                CurrentItem = artist.Name,
                AlbumArtFound = artFound,
                AlbumArtRemaining = 0,
                ArtistBiosFound = biosFound
            });

            await EnrichArtistAsync(artist.Id, caller, cancellationToken: cancellationToken);

            await _db.Entry(artist).ReloadAsync(cancellationToken);
            if (artist.Biography is not null)
            {
                biosFound++;
            }
        }

        // Phase 2: Enrich albums — operate on user albums referencing canonical
        var userAlbums = await _db.UserAlbums
            .Include(ua => ua.CanonicalAlbum)
            .Where(ua => ua.OwnerId == ownerId && ua.CanonicalAlbum != null
                && ua.CanonicalAlbum.LastEnrichedAt == null)
            .ToListAsync(cancellationToken);

        var oldUnenrichedAlbums = await _db.Albums
            .Include(a => a.Artist)
            .Where(a => a.OwnerId == ownerId && a.LastEnrichedAt == null)
            .ToListAsync(cancellationToken);

        var totalAlbums = userAlbums.Count + oldUnenrichedAlbums.Count;
        var pendingAlbumArtLookups = userAlbums.Count(ua => !ua.CanonicalAlbum!.HasCoverArt)
            + oldUnenrichedAlbums.Count(a => !a.HasCoverArt);

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

            await EnrichAlbumAsync(ua.Id, caller, cancellationToken: cancellationToken);

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

        foreach (var album in oldUnenrichedAlbums)
        {
            cancellationToken.ThrowIfCancellationRequested();

            albumIdx++;
            var needsAlbumArt = !album.HasCoverArt;

            progress?.Report(new EnrichmentProgress
            {
                Phase = "Enriching albums (legacy)...",
                Current = albumIdx,
                Total = totalAlbums,
                CurrentItem = album.Title,
                AlbumArtFound = artFound,
                AlbumArtRemaining = Math.Max(0, pendingAlbumArtLookups - (needsAlbumArt ? 1 : 0)),
                ArtistBiosFound = biosFound
            });

            await EnrichAlbumAsync(album.Id, caller, cancellationToken: cancellationToken);

            await _db.Entry(album).ReloadAsync(cancellationToken);
            if (album.HasCoverArt)
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

        var oldUnenrichedTracks = await _db.Tracks
            .Include(t => t.TrackArtists)
                .ThenInclude(ta => ta.Artist)
            .Where(t => t.OwnerId == ownerId && t.LastEnrichedAt == null)
            .ToListAsync(cancellationToken);

        var totalTracks = userTracks.Count + oldUnenrichedTracks.Count;
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

        foreach (var track in oldUnenrichedTracks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            trackIdx++;
            progress?.Report(new EnrichmentProgress
            {
                Phase = "Enriching tracks (legacy)...",
                Current = trackIdx,
                Total = totalTracks,
                CurrentItem = track.Title,
                AlbumArtFound = artFound,
                AlbumArtRemaining = pendingAlbumArtLookups,
                ArtistBiosFound = biosFound
            });

            await EnrichTrackAsync(track.Id, caller, cancellationToken: cancellationToken);
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
