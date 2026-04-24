using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
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
    private readonly ILogger<MetadataEnrichmentService> _logger;
    private readonly string _artCacheDir;

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
        IConfiguration configuration,
        ILogger<MetadataEnrichmentService> logger)
    {
        _db = db;
        _musicBrainzClient = musicBrainzClient;
        _coverArtClient = coverArtClient;
        _albumArtService = albumArtService;
        _logger = logger;
        var storageRoot = configuration["Files:Storage:RootPath"] ?? Path.GetTempPath();
        _artCacheDir = Path.Combine(storageRoot, ".album-art");
        Directory.CreateDirectory(_artCacheDir);
    }

    /// <inheritdoc/>
    public async Task EnrichAlbumAsync(Guid albumId, CallerContext caller, bool force = false, CancellationToken cancellationToken = default)
    {
        var album = await _db.Albums
            .Include(a => a.Artist)
            .FirstOrDefaultAsync(a => a.Id == albumId, cancellationToken);

        if (album is null)
        {
            _logger.LogDebug("Album {AlbumId} not found for enrichment", albumId);
            return;
        }

        if (!force && album.LastEnrichedAt.HasValue && DateTime.UtcNow - album.LastEnrichedAt.Value < EnrichmentCooldown)
        {
            _logger.LogDebug("Album {AlbumId} was recently enriched, skipping (last: {LastEnrichedAt})", albumId, album.LastEnrichedAt);
            return;
        }

        var artistName = album.Artist?.Name ?? "Unknown Artist";
        _logger.LogInformation("Enriching album '{AlbumTitle}' by '{ArtistName}'", album.Title, artistName);

        // Search MusicBrainz for the release group
        var releaseGroups = await _musicBrainzClient.SearchReleaseGroupAsync(album.Title, artistName, cancellationToken);
        if (releaseGroups is null || releaseGroups.Count == 0)
        {
            _logger.LogDebug("No MusicBrainz release group found for '{AlbumTitle}' by '{ArtistName}'", album.Title, artistName);
            album.LastEnrichedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var topResult = releaseGroups[0];
        if (topResult.Score < MinMatchScore)
        {
            _logger.LogWarning("MusicBrainz release group match for '{AlbumTitle}' has low score {Score}, skipping", album.Title, topResult.Score);
            album.LastEnrichedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        album.MusicBrainzReleaseGroupId = topResult.Id;

        // Get release group details (with releases) for cover art lookup
        var releaseGroup = await _musicBrainzClient.GetReleaseGroupAsync(topResult.Id, cancellationToken);
        if (releaseGroup?.Releases is not null && releaseGroup.Releases.Count > 0)
        {
            // Store the first release ID
            album.MusicBrainzReleaseId = releaseGroup.Releases[0].Id;

            // Fetch cover art if album doesn't have it or the cached file is missing
            if (!album.HasCoverArt || (album.CoverArtPath is not null && !File.Exists(album.CoverArtPath)))
            {
                var coverArt = await _coverArtClient.GetFrontCoverFromReleasesAsync(releaseGroup.Releases, cancellationToken);
                if (coverArt is not null)
                {
                    var cachePath = CacheExternalArt(coverArt.Data, coverArt.MimeType, album.Id);
                    if (cachePath is not null)
                    {
                        album.HasCoverArt = true;
                        album.CoverArtPath = cachePath;
                        album.MusicBrainzReleaseId = coverArt.ReleaseMbid;
                        _logger.LogInformation("Fetched cover art for album '{AlbumTitle}' from release {ReleaseMbid}", album.Title, coverArt.ReleaseMbid);
                    }
                }
            }
        }

        album.LastEnrichedAt = DateTime.UtcNow;
        album.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task EnrichArtistAsync(Guid artistId, CallerContext caller, bool force = false, CancellationToken cancellationToken = default)
    {
        var artist = await _db.Artists
            .FirstOrDefaultAsync(a => a.Id == artistId, cancellationToken);

        if (artist is null)
        {
            _logger.LogDebug("Artist {ArtistId} not found for enrichment", artistId);
            return;
        }

        if (!force && artist.LastEnrichedAt.HasValue && DateTime.UtcNow - artist.LastEnrichedAt.Value < EnrichmentCooldown)
        {
            _logger.LogDebug("Artist {ArtistId} was recently enriched, skipping (last: {LastEnrichedAt})", artistId, artist.LastEnrichedAt);
            return;
        }

        _logger.LogInformation("Enriching artist '{ArtistName}'", artist.Name);

        // Search MusicBrainz for the artist
        var artists = await _musicBrainzClient.SearchArtistAsync(artist.Name, cancellationToken);
        if (artists is null || artists.Count == 0)
        {
            _logger.LogDebug("No MusicBrainz artist found for '{ArtistName}'", artist.Name);
            artist.LastEnrichedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var topResult = artists[0];
        if (topResult.Score < MinMatchScore)
        {
            _logger.LogWarning("MusicBrainz artist match for '{ArtistName}' has low score {Score}, skipping", artist.Name, topResult.Score);
            artist.LastEnrichedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        artist.MusicBrainzId = topResult.Id;

        // Get full artist details with URL relations and annotation
        var detail = await _musicBrainzClient.GetArtistAsync(topResult.Id, cancellationToken);
        if (detail is not null)
        {
            artist.Biography = detail.Annotation;
            artist.WikipediaUrl = detail.WikipediaUrl;
            artist.DiscogsUrl = detail.DiscogsUrl;
            artist.OfficialUrl = detail.OfficialUrl;

            _logger.LogInformation(
                "Enriched artist '{ArtistName}': bio={HasBio}, wikipedia={HasWiki}, discogs={HasDiscogs}, official={HasOfficial}",
                artist.Name,
                detail.Annotation is not null,
                detail.WikipediaUrl is not null,
                detail.DiscogsUrl is not null,
                detail.OfficialUrl is not null);
        }

        artist.LastEnrichedAt = DateTime.UtcNow;
        artist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task EnrichTrackAsync(Guid trackId, CallerContext caller, bool force = false, CancellationToken cancellationToken = default)
    {
        var track = await _db.Tracks
            .Include(t => t.TrackArtists)
                .ThenInclude(ta => ta.Artist)
            .FirstOrDefaultAsync(t => t.Id == trackId, cancellationToken);

        if (track is null)
        {
            _logger.LogDebug("Track {TrackId} not found for enrichment", trackId);
            return;
        }

        if (!force && track.LastEnrichedAt.HasValue && DateTime.UtcNow - track.LastEnrichedAt.Value < EnrichmentCooldown)
        {
            _logger.LogDebug("Track {TrackId} was recently enriched, skipping", trackId);
            return;
        }

        var artistName = track.TrackArtists.FirstOrDefault(ta => ta.IsPrimary)?.Artist?.Name ?? "Unknown Artist";
        _logger.LogDebug("Enriching track '{TrackTitle}' by '{ArtistName}'", track.Title, artistName);

        var recordings = await _musicBrainzClient.SearchRecordingAsync(track.Title, artistName, cancellationToken);
        if (recordings is not null && recordings.Count > 0)
        {
            var topResult = recordings[0];
            if (topResult.Score >= MinMatchScore)
            {
                track.MusicBrainzRecordingId = topResult.Id;
                _logger.LogDebug("Tagged track '{TrackTitle}' with MusicBrainz recording {RecordingId}", track.Title, topResult.Id);
            }
            else
            {
                _logger.LogDebug("MusicBrainz recording match for '{TrackTitle}' has low score {Score}, skipping", track.Title, topResult.Score);
            }
        }

        track.LastEnrichedAt = DateTime.UtcNow;
        track.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task EnrichAlbumsWithoutArtAsync(Guid ownerId, IProgress<EnrichmentProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var albumsWithoutArt = await _db.Albums
            .Include(a => a.Artist)
            .Where(a => a.OwnerId == ownerId && !a.HasCoverArt)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Found {Count} albums without cover art for user {OwnerId}", albumsWithoutArt.Count, ownerId);

        var artFound = 0;
        var caller = new CallerContext(ownerId, ["user"], CallerType.User);

        for (var i = 0; i < albumsWithoutArt.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var album = albumsWithoutArt[i];
            progress?.Report(new EnrichmentProgress
            {
                Phase = "Fetching cover art...",
                Current = i + 1,
                Total = albumsWithoutArt.Count,
                CurrentItem = album.Title,
                AlbumArtFound = artFound,
                AlbumArtRemaining = Math.Max(0, albumsWithoutArt.Count - (i + 1)),
                ArtistBiosFound = 0
            });

            await EnrichAlbumAsync(album.Id, caller, cancellationToken: cancellationToken);

            // Re-check if art was found after enrichment
            await _db.Entry(album).ReloadAsync(cancellationToken);
            if (album.HasCoverArt)
            {
                artFound++;
            }
        }

        _logger.LogInformation("Cover art enrichment complete: {ArtFound}/{Total} albums enriched", artFound, albumsWithoutArt.Count);
    }

    /// <inheritdoc/>
    public async Task EnrichAllAsync(Guid ownerId, IProgress<EnrichmentProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var caller = new CallerContext(ownerId, ["user"], CallerType.User);
        var artFound = 0;
        var biosFound = 0;

        // Phase 1: Enrich artists
        var unenrichedArtists = await _db.Artists
            .Where(a => a.OwnerId == ownerId && a.LastEnrichedAt == null)
            .ToListAsync(cancellationToken);
        var pendingAlbumArtLookups = await _db.Albums
            .Where(a => a.OwnerId == ownerId && a.LastEnrichedAt == null && !a.HasCoverArt)
            .CountAsync(cancellationToken);

        _logger.LogInformation("Enriching {Count} artists for user {OwnerId}", unenrichedArtists.Count, ownerId);

        for (var i = 0; i < unenrichedArtists.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var artist = unenrichedArtists[i];
            progress?.Report(new EnrichmentProgress
            {
                Phase = "Enriching artists...",
                Current = i + 1,
                Total = unenrichedArtists.Count,
                CurrentItem = artist.Name,
                AlbumArtFound = artFound,
                AlbumArtRemaining = pendingAlbumArtLookups,
                ArtistBiosFound = biosFound
            });

            await EnrichArtistAsync(artist.Id, caller, cancellationToken: cancellationToken);

            // Check if bio was found
            await _db.Entry(artist).ReloadAsync(cancellationToken);
            if (artist.Biography is not null)
            {
                biosFound++;
            }
        }

        // Phase 2: Enrich albums
        var unenrichedAlbums = await _db.Albums
            .Include(a => a.Artist)
            .Where(a => a.OwnerId == ownerId && a.LastEnrichedAt == null)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Enriching {Count} albums for user {OwnerId}", unenrichedAlbums.Count, ownerId);

        for (var i = 0; i < unenrichedAlbums.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var album = unenrichedAlbums[i];
            var needsAlbumArtLookup = !album.HasCoverArt;
            progress?.Report(new EnrichmentProgress
            {
                Phase = "Enriching albums...",
                Current = i + 1,
                Total = unenrichedAlbums.Count,
                CurrentItem = album.Title,
                AlbumArtFound = artFound,
                AlbumArtRemaining = Math.Max(0, pendingAlbumArtLookups - (needsAlbumArtLookup ? 1 : 0)),
                ArtistBiosFound = biosFound
            });

            await EnrichAlbumAsync(album.Id, caller, cancellationToken: cancellationToken);

            await _db.Entry(album).ReloadAsync(cancellationToken);
            if (album.HasCoverArt)
            {
                artFound++;
            }

            if (needsAlbumArtLookup)
            {
                pendingAlbumArtLookups = Math.Max(0, pendingAlbumArtLookups - 1);
            }
        }

        // Phase 3: Enrich tracks
        var unenrichedTracks = await _db.Tracks
            .Include(t => t.TrackArtists)
                .ThenInclude(ta => ta.Artist)
            .Where(t => t.OwnerId == ownerId && t.LastEnrichedAt == null)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Enriching {Count} tracks for user {OwnerId}", unenrichedTracks.Count, ownerId);

        for (var i = 0; i < unenrichedTracks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var track = unenrichedTracks[i];
            progress?.Report(new EnrichmentProgress
            {
                Phase = "Enriching tracks...",
                Current = i + 1,
                Total = unenrichedTracks.Count,
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
    /// Caches externally-fetched art data to the album art cache directory.
    /// </summary>
    private string? CacheExternalArt(byte[] data, string mimeType, Guid albumId)
    {
        try
        {
            Directory.CreateDirectory(_artCacheDir);
            var extension = mimeType.Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
            var fileName = $"{albumId}{extension}";
            var cachePath = Path.Combine(_artCacheDir, fileName);
            File.WriteAllBytes(cachePath, data);
            _logger.LogDebug("Cached external album art for {AlbumId} at {Path}", albumId, cachePath);
            return cachePath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache external album art for {AlbumId}", albumId);
            return null;
        }
    }
}
