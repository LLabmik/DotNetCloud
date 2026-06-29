using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Music.Models;
using DotNetCloud.Modules.Music.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DotNetCloud.Modules.Music.Data.Services;

/// <summary>
/// Scans a user's files for audio content and builds/updates the music library.
/// Uses a dual-write strategy: canonical (shared) tables for deduplicated metadata
/// and per-user legacy tables for backward compatibility.
/// </summary>
public sealed class LibraryScanService
{
    private readonly MusicDbContext _db;
    private readonly MusicMetadataService _metadataService;
    private readonly AlbumArtService _albumArtService;
    private readonly IEventBus _eventBus;
    private readonly IMetadataEnrichmentService? _enrichmentService;
    private readonly ILogger<LibraryScanService> _logger;
    private readonly ITableNamingStrategy _namingStrategy;
    private readonly string _artCacheDir;
    private readonly bool _autoFetchArt;
    private readonly bool _autoEnrichArtists;

    // Per-scan caches for canonical (shared) entities to eliminate redundant DB round trips.
    private readonly Dictionary<string, CanonicalArtist> _artistCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CanonicalAlbum> _albumCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CanonicalGenre> _genreCache = new(StringComparer.OrdinalIgnoreCase);

    // Tracks album total duration incrementally to avoid O(n²) SUM queries.
    private readonly Dictionary<Guid, long> _albumDurationCache = new();

    // Pre-loaded junction data for batch operations (CloneLibraryFromExistingAsync, TryBulkIndexFromExistingAsync).
    // Populated once at the start, then used for in-memory lookups instead of per-track DB queries.
    private HashSet<(string ContentHash, Guid ArtistId)>? _preloadedTrackArtists;
    private HashSet<(string ContentHash, Guid GenreId)>? _preloadedTrackGenres;
    private HashSet<(Guid AlbumId, Guid ArtistId)>? _preloadedAlbumArtists;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryScanService"/> class.
    /// </summary>
    public LibraryScanService(
        MusicDbContext db,
        MusicMetadataService metadataService,
        AlbumArtService albumArtService,
        IEventBus eventBus,
        IConfiguration configuration,
        ILogger<LibraryScanService> logger,
        ITableNamingStrategy namingStrategy,
        IMetadataEnrichmentService? enrichmentService = null)
    {
        _db = db;
        _metadataService = metadataService;
        _albumArtService = albumArtService;
        _eventBus = eventBus;
        _enrichmentService = enrichmentService;
        _namingStrategy = namingStrategy;
        _logger = logger;
        var storageRoot = configuration["Files:Storage:RootPath"] ?? Path.GetTempPath();
        _artCacheDir = Path.Combine(storageRoot, ".album-art");
        Directory.CreateDirectory(_artCacheDir);

        var enrichmentEnabled = configuration.GetValue("Music:Enrichment:Enabled", true);
        _autoFetchArt = enrichmentEnabled && configuration.GetValue("Music:Enrichment:AutoFetchArt", true);
        _autoEnrichArtists = enrichmentEnabled && configuration.GetValue("Music:Enrichment:AutoEnrichArtists", true);
    }

    /// <summary>
    /// Indexes a single audio file into the music library. Creates canonical (shared) entities
    /// and per-user junction records (UserTrack, UserAlbum, UserArtist).
    /// </summary>
    public async Task<UserTrack?> IndexFileAsync(
        Guid fileNodeId,
        string fileName,
        string mimeType,
        long sizeBytes,
        Guid ownerId,
        string? metadataFilePath = null,
        Stream? audioStream = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("IndexFileAsync: FileNode={FileNodeId}, File={FileName}, Owner={OwnerId}", fileNodeId, fileName, ownerId);

        // Check if already indexed for this user (via UserTrack)
        var existing = await _db.UserTracks
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ut => ut.FileNodeId == fileNodeId && ut.OwnerId == ownerId, cancellationToken);

        if (existing is not null)
        {
            if (!existing.IsDeleted)
            {
                // ── Backfill: if the canonical track was created via GUID fallback (no ContentHash),
                //     it will have NULL TrackNumber/DiscNumber/etc. Re-extract metadata from the
                //     actual audio file and update the canonical track with real values. ──
                var canonical = await _db.CanonicalTracks
                    .FirstOrDefaultAsync(ct => ct.ContentHash == existing.CanonicalTrackHash, cancellationToken);

                if (canonical is not null && canonical.TrackNumber is null && canonical.DiscNumber is null
                    && canonical.ContentHash.Length == 36) // GUID length, not SHA-256 (64)
                {
                    _logger.LogInformation("Backfilling metadata for canonical track {Hash} (FileNode {FileNodeId})",
                        existing.CanonicalTrackHash, fileNodeId);

                    AudioMetadata? backfillMetadata = null;
                    var backfillPath = audioStream is FileStream backfillFs ? backfillFs.Name : metadataFilePath ?? fileName;

                    if (backfillPath is not null && File.Exists(backfillPath))
                    {
                        backfillMetadata = _metadataService.ExtractMetadata(backfillPath);
                    }
                    else if (audioStream is not null)
                    {
                        backfillMetadata = _metadataService.ExtractMetadata(audioStream, mimeType, fileName);
                    }

                    if (backfillMetadata is not null)
                    {
                        canonical.Title = !IsGarbageValue(backfillMetadata.Title)
                            ? backfillMetadata.Title
                            : canonical.Title;
                        canonical.TrackNumber = backfillMetadata.TrackNumber;
                        canonical.DiscNumber = backfillMetadata.DiscNumber;
                        canonical.Year = backfillMetadata.Year;
                        canonical.Bitrate = backfillMetadata.Bitrate;
                        canonical.SampleRate = backfillMetadata.SampleRate;
                        canonical.Channels = backfillMetadata.Channels;
                        canonical.UpdatedAt = DateTime.UtcNow;
                        await _db.SaveChangesAsync(cancellationToken);
                        _logger.LogInformation("Backfilled metadata for canonical track {Hash}: Track#={TrackNum}, Disc#={DiscNum}, Year={Year}",
                            existing.CanonicalTrackHash, backfillMetadata.TrackNumber, backfillMetadata.DiscNumber, backfillMetadata.Year);
                    }
                }

                _logger.LogDebug("File {FileNodeId} already indexed as user track {TrackId}", fileNodeId, existing.Id);
                return existing;
            }

            // Restore previously soft-deleted track — same FileNodeId/OwnerId pair
            _logger.LogDebug("Restoring soft-deleted user track {TrackId} for file {FileNodeId}", existing.Id, fileNodeId);
            existing.IsDeleted = false;
            existing.DeletedAt = null;
            existing.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return existing;
        }

        // ── Extract content hash first for canonical lookup ──
        var contentHash = await LookupContentHashAsync(fileNodeId, cancellationToken);

        // ── Check canonical tracks by content hash ──
        if (contentHash is not null)
        {
            var canonicalTrack = await _db.CanonicalTracks
                .Include(ct => ct.TrackArtists)
                .Include(ct => ct.TrackGenres)
                .FirstOrDefaultAsync(ct => ct.ContentHash == contentHash, cancellationToken);

            if (canonicalTrack is not null)
            {
                // Canonical track exists — ensure canonical artist/album/genre exist, then dual-write
                _logger.LogDebug("Found existing canonical track {ContentHash} for FileNode {FileNodeId}", contentHash, fileNodeId);

                // Resolve canonical entities from the existing track's junctions
                var firstArtist = canonicalTrack.TrackArtists.FirstOrDefault();
                var foundArtist = firstArtist is not null
                    ? await _db.CanonicalArtists.FindAsync([firstArtist.ArtistId], cancellationToken)
                    : await GetOrCreateCanonicalArtistAsync("Unknown Artist", cancellationToken);
                if (foundArtist is null)
                    foundArtist = await GetOrCreateCanonicalArtistAsync("Unknown Artist", cancellationToken);

                var firstGenre = canonicalTrack.TrackGenres.FirstOrDefault();
                CanonicalGenre? foundGenre = null;
                if (firstGenre is not null)
                {
                    foundGenre = await _db.CanonicalGenres.FindAsync([firstGenre.GenreId], cancellationToken);
                }

                // Extract album metadata from the file to find/create canonical album.
                // After a hard-delete reset, no UserTracks exist to look up CanonicalAlbumId from,
                // so we must extract album info from the file itself.
                CanonicalAlbum? foundAlbum = null;
                var albumPath = audioStream is FileStream fileStream ? fileStream.Name : metadataFilePath;
                if (albumPath is not null)
                {
                    try
                    {
                        var albumMetadata = _metadataService.ExtractMetadata(albumPath);
                        if (albumMetadata is not null && !string.IsNullOrWhiteSpace(albumMetadata.Album))
                        {
                            foundAlbum = await GetOrCreateCanonicalAlbumAsync(albumMetadata.Album, albumMetadata.Year, cancellationToken);

                            // Create CanonicalAlbumArtist junction if needed
                            if (foundArtist is not null)
                            {
                                var existingAlbumArtist = await _db.CanonicalAlbumArtists
                                    .FirstOrDefaultAsync(aa => aa.AlbumId == foundAlbum.Id && aa.ArtistId == foundArtist.Id, cancellationToken);
                                if (existingAlbumArtist is null)
                                {
                                    _db.CanonicalAlbumArtists.Add(new CanonicalAlbumArtist
                                    {
                                        AlbumId = foundAlbum.Id,
                                        ArtistId = foundArtist.Id
                                    });
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to extract album metadata from {Path} for canonical track lookup", albumPath);
                    }
                }

                // Fallback: try to find album via any UserTrack (including soft-deleted/other-owner)
                if (foundAlbum is null)
                {
                    var otherUserTrack = await _db.UserTracks
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(ut => ut.CanonicalTrackHash == contentHash && ut.CanonicalAlbumId != null, cancellationToken);
                    if (otherUserTrack?.CanonicalAlbumId is not null)
                    {
                        foundAlbum = await _db.CanonicalAlbums.FindAsync([otherUserTrack.CanonicalAlbumId.Value], cancellationToken);
                    }
                }

                var indexedTrack = await CreateUserTrackJunctionsAsync(
                    fileNodeId, fileName, mimeType, sizeBytes, ownerId,
                    contentHash, canonicalTrack.Title,
                    canonicalTrack, foundAlbum,
                    foundArtist ?? await GetOrCreateCanonicalArtistAsync("Unknown Artist", cancellationToken),
                    foundGenre,
                    canonicalTrack.TrackNumber, canonicalTrack.DiscNumber,
                    canonicalTrack.DurationTicks, canonicalTrack.Bitrate,
                    canonicalTrack.SampleRate, canonicalTrack.Channels,
                    canonicalTrack.Year, canonicalTrack.MusicBrainzRecordingId,
                    cancellationToken);

                await _db.SaveChangesAsync(cancellationToken);
                return indexedTrack;
            }
        }

        // ── Cross-owner copy (fallback): if canonical lookup failed, try legacy cross-owner ──
        if (contentHash is null)
        {
            var crossOwnerTrack = await TryIndexFromExistingOwnerAsync(
                fileNodeId, fileName, mimeType, sizeBytes, ownerId, cancellationToken);
            if (crossOwnerTrack is not null)
            {
                await _db.SaveChangesAsync(cancellationToken);
                return crossOwnerTrack;
            }
        }

        // ── Extract metadata from the file ──
        // Prefer file-path-based extraction — TagLib is significantly faster
        // reading from a local file path than from a stream (especially when the
        // stream wraps a temp file reassembled from chunks or a direct mount).
        AudioMetadata? metadata = null;
        var resolvedPath = audioStream is FileStream fs ? fs.Name : metadataFilePath;

        if (resolvedPath is not null)
        {
            metadata = _metadataService.ExtractMetadata(resolvedPath);
        }

        if (metadata is null && audioStream is not null)
        {
            metadata = _metadataService.ExtractMetadata(audioStream, mimeType, fileName);
        }

        // Filepath fallback: if TagLib# produced garbage, merge with Artist/Album/Track
        // parsed from directory structure when available.
        if (metadata is not null && resolvedPath is not null)
        {
            var parsed = TryParseMetadataFromPath(resolvedPath, fileName);
            if (parsed is not null)
            {
                var hasGarbageArtist = IsGarbageValue(metadata.Artist);
                var hasGarbageAlbum = IsGarbageValue(metadata.Album);
                var hasGarbageTitle = IsGarbageValue(metadata.Title);

                if (hasGarbageArtist || hasGarbageAlbum || hasGarbageTitle)
                {
                    metadata = new AudioMetadata
                    {
                        Title = hasGarbageTitle
                            ? (parsed.Title ?? Path.GetFileNameWithoutExtension(fileName))
                            : metadata.Title,
                        Artist = (hasGarbageArtist && !string.IsNullOrWhiteSpace(parsed.Artist))
                            ? parsed.Artist
                            : metadata.Artist,
                        Album = (hasGarbageAlbum && !string.IsNullOrWhiteSpace(parsed.Album))
                            ? parsed.Album
                            : metadata.Album,
                        AlbumArtist = metadata.AlbumArtist,
                        TrackNumber = metadata.TrackNumber,
                        DiscNumber = metadata.DiscNumber,
                        Year = metadata.Year,
                        Genre = metadata.Genre,
                        DurationTicks = metadata.DurationTicks,
                        Bitrate = metadata.Bitrate,
                        SampleRate = metadata.SampleRate,
                        Channels = metadata.Channels,
                        HasEmbeddedArt = metadata.HasEmbeddedArt,
                    };
                }
            }
        }

        if (metadata is null)
        {
            _logger.LogWarning("Could not extract metadata for {FileName} (stream={HasStream}, path={Path}), creating track from filename",
                fileName, audioStream is not null, resolvedPath);
            metadata = new AudioMetadata
            {
                Title = Path.GetFileNameWithoutExtension(fileName),
                Artist = "Unknown Artist",
                Album = "Unknown Album",
                DurationTicks = 0,
                TrackNumber = TryExtractTrackNumberFromFileName(fileName)
            };
        }

        // ── Canonical: Get or create shared entities ──
        var canonicalArtist = await GetOrCreateCanonicalArtistAsync(metadata.AlbumArtist ?? metadata.Artist, cancellationToken);
        var canonicalAlbum = await GetOrCreateCanonicalAlbumAsync(metadata.Album, metadata.Year, cancellationToken);

        // Handle album art on canonical album
        if (!canonicalAlbum.HasCoverArt)
        {
            string? artHash = null;
            if (resolvedPath is not null)
            {
                artHash = _albumArtService.ExtractAndCacheArt(resolvedPath);
            }
            else if (audioStream is not null && audioStream.CanSeek)
            {
                audioStream.Position = 0;
                artHash = _albumArtService.ExtractAndCacheArt(audioStream, mimeType, fileName);
            }

            if (artHash is not null)
            {
                canonicalAlbum.HasCoverArt = true;
                canonicalAlbum.CoverArtHash = artHash;
            }
        }

        CanonicalGenre? canonicalGenre = null;
        if (!string.IsNullOrWhiteSpace(metadata.Genre))
        {
            canonicalGenre = await GetOrCreateCanonicalGenreAsync(metadata.Genre, cancellationToken);
        }

        // ── Create canonical track record (only if content hash is available) ──
        if (contentHash is not null)
        {
            var existingCanonical = await _db.CanonicalTracks
                .FindAsync([contentHash], cancellationToken);

            if (existingCanonical is null)
            {
                var newCanonicalTrack = new CanonicalTrack
                {
                    ContentHash = contentHash,
                    Title = metadata.Title,
                    TrackNumber = metadata.TrackNumber,
                    DiscNumber = metadata.DiscNumber,
                    DurationTicks = metadata.DurationTicks,
                    Bitrate = metadata.Bitrate,
                    SampleRate = metadata.SampleRate,
                    Channels = metadata.Channels,
                    MimeType = mimeType,
                    Year = metadata.Year
                };
                _db.CanonicalTracks.Add(newCanonicalTrack);

                // Create canonical junction records
                await EnsureCanonicalJunctionsAsync(contentHash, canonicalArtist, canonicalAlbum, canonicalGenre, cancellationToken);
            }
        }

        // ── Dual-write: user junctions + old per-user records ──
        CanonicalTrack canonicalTrackForDualWrite;
        if (contentHash is not null)
        {
            canonicalTrackForDualWrite = (await _db.CanonicalTracks.FindAsync([contentHash], cancellationToken))!;
        }
        else
        {
            // Generate a deterministic content hash from the file path when the Files module
            // hasn't computed one (e.g., shared mount). This ensures UserTrack.CanonicalTrackHash
            // has a matching CanonicalTrack row for the FK constraint.
            var fallbackHash = Guid.CreateVersion7().ToString();
            canonicalTrackForDualWrite = new CanonicalTrack
            {
                ContentHash = fallbackHash,
                Title = metadata.Title,
                TrackNumber = metadata.TrackNumber,
                DiscNumber = metadata.DiscNumber,
                DurationTicks = metadata.DurationTicks,
                Bitrate = metadata.Bitrate,
                SampleRate = metadata.SampleRate,
                Channels = metadata.Channels,
                MimeType = mimeType,
                Year = metadata.Year,
                MusicBrainzRecordingId = metadata.MusicBrainzTrackId,
                Bpm = metadata.Bpm,
                Composers = TruncateComposers(metadata.Composers),
                Isrc = ValidateIsrc(metadata.Isrc)
            };
            _db.CanonicalTracks.Add(canonicalTrackForDualWrite);
        }

        var track = await CreateUserTrackJunctionsAsync(
            fileNodeId, fileName, mimeType, sizeBytes, ownerId,
            contentHash ?? canonicalTrackForDualWrite.ContentHash,
            metadata.Title,
            canonicalTrackForDualWrite, canonicalAlbum, canonicalArtist, canonicalGenre,
            metadata.TrackNumber, metadata.DiscNumber, metadata.DurationTicks,
            metadata.Bitrate, metadata.SampleRate, metadata.Channels,
            metadata.Year, null,
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return track;
    }

    /// <summary>
    /// Attempts to find an existing canonical track for this file and creates user junctions
    /// for the current owner. Looks up by canonical ContentHash first, then falls back to
    /// cross-owner UserTrack lookup by FileNodeId.
    /// </summary>
    public async Task<UserTrack?> TryIndexFromExistingOwnerAsync(
        Guid fileNodeId,
        string fileName,
        string mimeType,
        long sizeBytes,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        string? contentHash = null;

        // Strategy 1: Look up canonical track by ContentHash
        contentHash = await LookupContentHashAsync(fileNodeId, cancellationToken);

        if (contentHash is not null)
        {
            var canonicalTrack = await _db.CanonicalTracks
                .Include(ct => ct.TrackArtists).ThenInclude(cta => cta.Artist)
                .Include(ct => ct.TrackGenres).ThenInclude(ctg => ctg.Genre)
                .FirstOrDefaultAsync(ct => ct.ContentHash == contentHash, cancellationToken);

            if (canonicalTrack is not null)
            {
                _logger.LogInformation(
                    "Cross-owner copy (CanonicalContentHash): creating user junctions for FileNode {FileNodeId} into owner {OwnerId}",
                    fileNodeId, ownerId);

                // Get or create canonical entities
                var foundArtist = canonicalTrack.TrackArtists
                    .Select(cta => cta.Artist)
                    .FirstOrDefault() ?? await GetOrCreateCanonicalArtistAsync("Unknown Artist", cancellationToken);

                var foundGenre = canonicalTrack.TrackGenres
                    .Select(ctg => ctg.Genre)
                    .FirstOrDefault();

                // Find canonical album from UserTracks
                var anyUserTrack = await _db.UserTracks
                    .FirstOrDefaultAsync(ut => ut.CanonicalTrackHash == contentHash, cancellationToken);
                CanonicalAlbum? foundAlbum = null;
                if (anyUserTrack?.CanonicalAlbumId is not null)
                {
                    foundAlbum = await _db.CanonicalAlbums.FindAsync(
                        [anyUserTrack.CanonicalAlbumId.Value], cancellationToken);
                }

                var indexedTrack = await CreateUserTrackJunctionsAsync(
                    fileNodeId, fileName, mimeType, sizeBytes, ownerId,
                    contentHash, canonicalTrack.Title,
                    canonicalTrack, foundAlbum, foundArtist, foundGenre,
                    canonicalTrack.TrackNumber, canonicalTrack.DiscNumber,
                    canonicalTrack.DurationTicks, canonicalTrack.Bitrate,
                    canonicalTrack.SampleRate, canonicalTrack.Channels,
                    canonicalTrack.Year, canonicalTrack.MusicBrainzRecordingId,
                    cancellationToken);

                await _db.SaveChangesAsync(cancellationToken);
                return indexedTrack;
            }
        }

        // Strategy 2: Cross-owner UserTrack lookup by FileNodeId (shared folders)
        var sourceUserTrack = await _db.UserTracks
            .Include(ut => ut.CanonicalTrack)
                .ThenInclude(ct => ct!.TrackArtists).ThenInclude(cta => cta.Artist)
            .Include(ut => ut.CanonicalTrack)
                .ThenInclude(ct => ct!.TrackGenres).ThenInclude(ctg => ctg.Genre)
            .Include(ut => ut.CanonicalAlbum)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ut => ut.FileNodeId == fileNodeId && ut.OwnerId != ownerId && !ut.IsDeleted,
                cancellationToken);

        if (sourceUserTrack is null)
            return null;

        _logger.LogInformation(
            "Cross-owner copy (UserTrack FileNodeId): cloning for FileNode {FileNodeId} into owner {OwnerId}",
            fileNodeId, ownerId);

        if (contentHash is null)
            contentHash = await LookupContentHashAsync(fileNodeId, cancellationToken);

        var sourceCanonicalTrack = sourceUserTrack.CanonicalTrack;
        if (sourceCanonicalTrack is null)
            return null;

        // Get or create canonical entities from source user track's canonical metadata
        var sourceArtistName = sourceCanonicalTrack.TrackArtists
            .FirstOrDefault(cta => cta.IsPrimary)?.Artist?.Name
            ?? sourceCanonicalTrack.TrackArtists.FirstOrDefault()?.Artist?.Name
            ?? "Unknown Artist";

        var canonicalArtist = await GetOrCreateCanonicalArtistAsync(sourceArtistName, cancellationToken);

        CanonicalAlbum? canonicalAlbum = sourceUserTrack.CanonicalAlbum;

        CanonicalGenre? canonicalGenre = null;
        var sourceGenre = sourceCanonicalTrack.TrackGenres.FirstOrDefault()?.Genre;
        if (sourceGenre is not null)
            canonicalGenre = await GetOrCreateCanonicalGenreAsync(sourceGenre.Name, cancellationToken);

        // Create canonical track if content hash is available and it doesn't exist yet
        if (contentHash is not null)
        {
            var existingCanonical = await _db.CanonicalTracks.FindAsync([contentHash], cancellationToken);
            if (existingCanonical is null)
            {
                var newCanonical = new CanonicalTrack
                {
                    ContentHash = contentHash,
                    Title = sourceCanonicalTrack.Title,
                    TrackNumber = sourceCanonicalTrack.TrackNumber,
                    DiscNumber = sourceCanonicalTrack.DiscNumber,
                    DurationTicks = sourceCanonicalTrack.DurationTicks,
                    Bitrate = sourceCanonicalTrack.Bitrate,
                    SampleRate = sourceCanonicalTrack.SampleRate,
                    Channels = sourceCanonicalTrack.Channels,
                    MimeType = sourceCanonicalTrack.MimeType,
                    Year = sourceCanonicalTrack.Year,
                    MusicBrainzRecordingId = sourceCanonicalTrack.MusicBrainzRecordingId
                };
                _db.CanonicalTracks.Add(newCanonical);

                await EnsureCanonicalJunctionsAsync(contentHash, canonicalArtist, canonicalAlbum, canonicalGenre, cancellationToken);
            }
        }

        CanonicalTrack canonicalForDualWrite;
        if (contentHash is not null)
        {
            canonicalForDualWrite = (await _db.CanonicalTracks.FindAsync([contentHash], cancellationToken))!;
        }
        else
        {
            var fallbackHash = Guid.CreateVersion7().ToString();
            canonicalForDualWrite = new CanonicalTrack
            {
                ContentHash = fallbackHash,
                Title = sourceCanonicalTrack.Title,
                DurationTicks = sourceCanonicalTrack.DurationTicks,
                MimeType = sourceCanonicalTrack.MimeType
            };
            _db.CanonicalTracks.Add(canonicalForDualWrite);
        }

        var finalTrack = await CreateUserTrackJunctionsAsync(
            fileNodeId, fileName, mimeType, sizeBytes, ownerId,
            contentHash ?? canonicalForDualWrite.ContentHash,
            sourceCanonicalTrack.Title,
            canonicalForDualWrite, canonicalAlbum, canonicalArtist, canonicalGenre,
            sourceCanonicalTrack.TrackNumber, sourceCanonicalTrack.DiscNumber, sourceCanonicalTrack.DurationTicks,
            sourceCanonicalTrack.Bitrate, sourceCanonicalTrack.SampleRate, sourceCanonicalTrack.Channels,
            sourceCanonicalTrack.Year, sourceCanonicalTrack.MusicBrainzRecordingId,
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return finalTrack;
    }

    /// <summary>
    /// Attempts a bulk cross-owner copy for a set of file nodes. Queries all canonical tracks
    /// matching by ContentHash, then creates user junctions and dual-write old records in a
    /// single batch. Skips files already indexed for this owner.
    /// Returns the set of FileNode IDs that were successfully indexed.
    /// </summary>
    public async Task<HashSet<Guid>> TryBulkIndexFromExistingAsync(
        IReadOnlyCollection<Guid> fileNodeIds,
        IReadOnlyDictionary<Guid, string?> contentHashMap,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        if (fileNodeIds.Count == 0)
            return [];

        // Get all distinct content hashes
        var hashes = contentHashMap.Values
            .Where(h => h is not null)
            .Cast<string>()
            .Distinct()
            .ToList();

        // ── Strategy 1: Match by canonical ContentHash ──
        var canonicalTracksByHash = new Dictionary<string, CanonicalTrack>();
        if (hashes.Count > 0)
        {
            var found = await _db.CanonicalTracks
                .Include(ct => ct.TrackArtists).ThenInclude(cta => cta.Artist)
                .Include(ct => ct.TrackGenres).ThenInclude(ctg => ctg.Genre)
                .Where(ct => hashes.Contains(ct.ContentHash))
                .ToListAsync(cancellationToken);

            foreach (var ct in found)
            {
                if (!canonicalTracksByHash.ContainsKey(ct.ContentHash))
                    canonicalTracksByHash[ct.ContentHash] = ct;
            }
        }

        // ── Strategy 2: Cross-owner UserTrack match by FileNodeId ──
        var sourceById = new Dictionary<Guid, UserTrack>();
        var fileNodeIdTracks = await _db.UserTracks
            .Include(ut => ut.CanonicalTrack).ThenInclude(ct => ct!.TrackArtists).ThenInclude(cta => cta.Artist)
            .Include(ut => ut.CanonicalTrack).ThenInclude(ct => ct!.TrackGenres).ThenInclude(ctg => ctg.Genre)
            .Include(ut => ut.CanonicalAlbum)
            .IgnoreQueryFilters()
            .Where(ut => fileNodeIds.Contains(ut.FileNodeId) && ut.OwnerId != ownerId && !ut.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var ut in fileNodeIdTracks)
        {
            if (!sourceById.ContainsKey(ut.FileNodeId))
                sourceById[ut.FileNodeId] = ut;
        }

        if (canonicalTracksByHash.Count == 0 && sourceById.Count == 0)
            return [];

        // Get already-indexed FileNodeIds for this user
        var alreadyIndexed = await _db.UserTracks
            .Where(ut => ut.OwnerId == ownerId && fileNodeIds.Contains(ut.FileNodeId) && !ut.IsDeleted)
            .Select(ut => ut.FileNodeId)
            .ToListAsync(cancellationToken);
        var alreadyIndexedSet = alreadyIndexed.ToHashSet();

        // Build all records in memory
        var copiedIds = new HashSet<Guid>();

        foreach (var fileNodeId in fileNodeIds)
        {
            if (alreadyIndexedSet.Contains(fileNodeId))
                continue;

            if (!contentHashMap.TryGetValue(fileNodeId, out var contentHash) || contentHash is null)
            {
                // Fallback: try legacy FileNodeId match
                if (!sourceById.TryGetValue(fileNodeId, out var legacySource))
                    continue;

                // Create canonical entities from cross-owner user track
                contentHash = await LookupContentHashAsync(fileNodeId, cancellationToken);
                if (contentHash is null)
                    continue;

                var srcCt = legacySource.CanonicalTrack;
                if (srcCt is null)
                    continue;

                var sourceArtistName = srcCt.TrackArtists
                    .FirstOrDefault(cta => cta.IsPrimary)?.Artist?.Name
                    ?? srcCt.TrackArtists.FirstOrDefault()?.Artist?.Name
                    ?? "Unknown Artist";

                var cArtist = await GetOrCreateCanonicalArtistAsync(sourceArtistName, cancellationToken);
                CanonicalAlbum? cAlbum = legacySource.CanonicalAlbum;
                CanonicalGenre? cGenre = null;
                var sGenre = srcCt.TrackGenres.FirstOrDefault()?.Genre;
                if (sGenre is not null)
                    cGenre = await GetOrCreateCanonicalGenreAsync(sGenre.Name, cancellationToken);

                // Ensure canonical track exists
                var existingCt = await _db.CanonicalTracks.FindAsync([contentHash], cancellationToken);
                if (existingCt is null)
                {
                    var newCt = new CanonicalTrack
                    {
                        ContentHash = contentHash,
                        Title = srcCt.Title,
                        TrackNumber = srcCt.TrackNumber,
                        DiscNumber = srcCt.DiscNumber,
                        DurationTicks = srcCt.DurationTicks,
                        Bitrate = srcCt.Bitrate,
                        SampleRate = srcCt.SampleRate,
                        Channels = srcCt.Channels,
                        MimeType = srcCt.MimeType,
                        Year = srcCt.Year,
                        MusicBrainzRecordingId = srcCt.MusicBrainzRecordingId
                    };
                    _db.CanonicalTracks.Add(newCt);
                    await EnsureCanonicalJunctionsAsync(contentHash, cArtist, cAlbum, cGenre, cancellationToken);
                }

                var ctForWrite = (await _db.CanonicalTracks.FindAsync([contentHash], cancellationToken))!;
                await CreateUserTrackJunctionsAsync(
                    fileNodeId, "Unknown", srcCt.MimeType, srcCt.DurationTicks,
                    ownerId, contentHash, srcCt.Title,
                    ctForWrite, cAlbum, cArtist, cGenre,
                    srcCt.TrackNumber, srcCt.DiscNumber, srcCt.DurationTicks,
                    srcCt.Bitrate, srcCt.SampleRate, srcCt.Channels,
                    srcCt.Year, srcCt.MusicBrainzRecordingId,
                    cancellationToken);

                copiedIds.Add(fileNodeId);
                continue;
            }

            // Primary: use canonical track
            if (!canonicalTracksByHash.TryGetValue(contentHash, out var canonicalTrack))
                continue;

            var artist = canonicalTrack.TrackArtists.Select(cta => cta.Artist).FirstOrDefault()
                ?? await GetOrCreateCanonicalArtistAsync("Unknown Artist", cancellationToken);

            var genre = canonicalTrack.TrackGenres.Select(ctg => ctg.Genre).FirstOrDefault();

            var anyUt = await _db.UserTracks
                .FirstOrDefaultAsync(ut => ut.CanonicalTrackHash == contentHash, cancellationToken);
            CanonicalAlbum? album = null;
            if (anyUt?.CanonicalAlbumId is not null)
                album = await _db.CanonicalAlbums.FindAsync([anyUt.CanonicalAlbumId.Value], cancellationToken);

            // Look up filename from any existing old track for this content hash
            var bulkFileName = contentHash;

            await CreateUserTrackJunctionsAsync(
                fileNodeId, bulkFileName, canonicalTrack.MimeType, 0,
                ownerId, contentHash, canonicalTrack.Title,
                canonicalTrack, album, artist, genre,
                canonicalTrack.TrackNumber, canonicalTrack.DiscNumber, canonicalTrack.DurationTicks,
                canonicalTrack.Bitrate, canonicalTrack.SampleRate, canonicalTrack.Channels,
                canonicalTrack.Year, canonicalTrack.MusicBrainzRecordingId,
                cancellationToken);

            copiedIds.Add(fileNodeId);
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Bulk cross-owner copy: indexed {Count} tracks for owner {OwnerId} via canonical dedup",
            copiedIds.Count, ownerId);

        return copiedIds;
    }

    /// <summary>
    /// Performs a full library scan for a user, indexing all audio files found at the given paths.
    /// Optionally reports real-time progress and runs metadata enrichment after the scan phase.
    /// </summary>
    /// <param name="audioFiles">Audio files to index.</param>
    /// <param name="ownerId">User whose library is being scanned.</param>
    /// <param name="caller">Caller context for authorization and event publishing.</param>
    /// <param name="progress">Optional progress reporter for real-time scan status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<LibraryScanResultDto> ScanLibraryAsync(
        IEnumerable<(Guid FileNodeId, string FilePath, string MimeType, long SizeBytes)> audioFiles,
        Guid ownerId,
        CallerContext caller,
        IProgress<LibraryScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var fileList = audioFiles.ToList();
        var totalFiles = fileList.Count;
        var added = 0;
        var updated = 0;
        var skipped = 0;
        var failed = 0;

        // Report initial progress
        progress?.Report(new LibraryScanProgress
        {
            Phase = "Extracting metadata",
            FilesProcessed = 0,
            TotalFiles = totalFiles,
            PercentComplete = 0,
            ElapsedTime = sw.Elapsed
        });

        for (var i = 0; i < fileList.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var file = fileList[i];
            var fileName = Path.GetFileName(file.FilePath);

            progress?.Report(new LibraryScanProgress
            {
                Phase = "Extracting metadata",
                CurrentFile = fileName,
                FilesProcessed = i,
                TotalFiles = totalFiles,
                TracksAdded = added,
                TracksUpdated = updated,
                TracksSkipped = skipped,
                TracksFailed = failed,
                PercentComplete = totalFiles > 0 ? (int)((long)i * 100 / totalFiles) : 0,
                ElapsedTime = sw.Elapsed
            });

            var existingTrack = await _db.UserTracks
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(ut => ut.FileNodeId == file.FileNodeId && ut.OwnerId == ownerId, cancellationToken);

            try
            {
                var track = await IndexFileAsync(
                    file.FileNodeId, file.FilePath, file.MimeType, file.SizeBytes, ownerId,
                    metadataFilePath: file.FilePath, cancellationToken: cancellationToken);

                if (track is not null)
                {
                    if (existingTrack is null)
                        added++;
                    else
                        updated++;
                }
                else
                {
                    skipped++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to index file {FileName}", fileName);
                failed++;
            }
        }

        // Report metadata phase complete
        progress?.Report(new LibraryScanProgress
        {
            Phase = "Extracting metadata",
            FilesProcessed = totalFiles,
            TotalFiles = totalFiles,
            TracksAdded = added,
            TracksUpdated = updated,
            TracksSkipped = skipped,
            TracksFailed = failed,
            PercentComplete = 100,
            ElapsedTime = sw.Elapsed
        });

        // Enrichment phase: fetch missing album art and artist data from MusicBrainz
        var albumArtFetched = 0;
        if (_enrichmentService is not null && (_autoFetchArt || _autoEnrichArtists) && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Starting metadata enrichment phase for user {UserId}", ownerId);

            progress?.Report(new LibraryScanProgress
            {
                Phase = "Enriching metadata",
                FilesProcessed = totalFiles,
                TotalFiles = totalFiles,
                TracksAdded = added,
                TracksUpdated = updated,
                TracksSkipped = skipped,
                TracksFailed = failed,
                PercentComplete = 100,
                ElapsedTime = sw.Elapsed
            });

            try
            {
                var enrichmentProgress = new Progress<EnrichmentProgress>(ep =>
                {
                    var phase = ep.Phase ?? "Enriching metadata";
                    albumArtFetched = ep.AlbumArtFound;
                    progress?.Report(new LibraryScanProgress
                    {
                        Phase = phase,
                        CurrentFile = ep.CurrentItem,
                        FilesProcessed = totalFiles,
                        TotalFiles = totalFiles,
                        TracksAdded = added,
                        TracksUpdated = updated,
                        TracksSkipped = skipped,
                        TracksFailed = failed,
                        AlbumArtFetched = ep.AlbumArtFound,
                        PercentComplete = 100,
                        ElapsedTime = sw.Elapsed
                    });
                });

                if (_autoFetchArt)
                {
                    await _enrichmentService.EnrichAlbumsWithoutArtAsync(ownerId, enrichmentProgress, cancellationToken);
                }

                if (_autoEnrichArtists)
                {
                    await _enrichmentService.EnrichAllAsync(ownerId, enrichmentProgress, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Enrichment phase cancelled for user {UserId}", ownerId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Enrichment phase failed for user {UserId}, scan results preserved", ownerId);
            }
        }

        sw.Stop();

        var totalTracks = await _db.UserTracks.CountAsync(ut => ut.OwnerId == ownerId, cancellationToken);
        var totalArtists = await _db.UserArtists.CountAsync(ua => ua.OwnerId == ownerId, cancellationToken);
        var totalAlbums = await _db.UserAlbums.CountAsync(ua => ua.OwnerId == ownerId, cancellationToken);

        var result = new LibraryScanResultDto
        {
            TracksAdded = added,
            TracksUpdated = updated,
            TracksRemoved = 0,
            TotalTracks = totalTracks,
            TotalArtists = totalArtists,
            TotalAlbums = totalAlbums,
            Duration = sw.Elapsed
        };

        await _eventBus.PublishAsync(new LibraryScanCompletedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            UserId = ownerId,
            TracksAdded = added,
            TracksUpdated = updated,
            TracksRemoved = 0
        }, caller, cancellationToken);

        // Report completion
        progress?.Report(new LibraryScanProgress
        {
            Phase = "Complete",
            FilesProcessed = totalFiles,
            TotalFiles = totalFiles,
            TracksAdded = added,
            TracksUpdated = updated,
            TracksSkipped = skipped,
            TracksFailed = failed,
            AlbumArtFetched = albumArtFetched,
            PercentComplete = 100,
            ElapsedTime = sw.Elapsed
        });

        _logger.LogInformation(
            "Library scan complete for user {UserId}: {Added} added, {Updated} updated, {Total} total tracks",
            ownerId, added, updated, totalTracks);

        return result;
    }

    /// <summary>
    /// Gets or creates a canonical (shared) artist by name.
    /// No OwnerId filter — canonical artists are shared across all users.
    /// </summary>
    internal async Task<CanonicalArtist> GetOrCreateCanonicalArtistAsync(string name, CancellationToken cancellationToken)
    {
        if (_artistCache.TryGetValue(name, out var cached))
            return cached;

        var artist = await _db.CanonicalArtists
            .FirstOrDefaultAsync(a => a.Name == name, cancellationToken);

        if (artist is not null)
        {
            _artistCache[name] = artist;
            return artist;
        }

        artist = new CanonicalArtist
        {
            Name = name,
            SortName = GenerateSortName(name)
        };
        _db.CanonicalArtists.Add(artist);
        _artistCache[name] = artist;
        // Note: SaveChangesAsync is deferred to the caller for batching.
        return artist;
    }

    /// <summary>
    /// Normalizes an album title by stripping parenthetical suffixes (both complete and
    /// truncated ID3v1 variants) to prevent duplicate canonical album entries.
    /// Examples: "Led Zeppelin IV (1994 Remaster)" → "Led Zeppelin IV"
    ///           "In Through The Out Door (1994 " → "In Through The Out Door"
    /// </summary>
    private static string NormalizeAlbumTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return title;

        // Strip complete parenthetical suffixes: (Remastered), (1994 Remaster), (Disc 1), etc.
        var cleaned = Regex.Replace(title, @"\s*\([^)]*\)\s*$", "").Trim();

        // Strip truncated/broken parentheticals from ID3v1 tag truncation.
        // Only strips if open-paren appears near the end indicating truncation.
        cleaned = Regex.Replace(cleaned, @"\s*\([^)]{1,25}$", "").Trim();

        return cleaned;
    }

    /// <summary>
    /// Gets or creates a canonical (shared) album by title.
    /// No OwnerId/ArtistId filter — canonical albums are shared across all users.
    /// Album titles are normalized to prevent duplicates from ID3v1 truncation.
    /// </summary>
    internal async Task<CanonicalAlbum> GetOrCreateCanonicalAlbumAsync(string title, int? year, CancellationToken cancellationToken)
    {
        // Normalize the title to prevent duplicates from ID3v1 tag truncation
        var normalizedTitle = NormalizeAlbumTitle(title);

        if (_albumCache.TryGetValue(normalizedTitle, out var cached))
            return cached;

        // Try exact match on normalized title first
        var album = await _db.CanonicalAlbums
            .FirstOrDefaultAsync(a => a.Title == normalizedTitle, cancellationToken);

        if (album is not null)
        {
            _albumCache[normalizedTitle] = album;
            return album;
        }

        // Also try exact match on the original title (for legacy data)
        if (normalizedTitle != title)
        {
            album = await _db.CanonicalAlbums
                .FirstOrDefaultAsync(a => a.Title == title, cancellationToken);

            if (album is not null)
            {
                _albumCache[normalizedTitle] = album;
                return album;
            }
        }

        album = new CanonicalAlbum
        {
            Title = normalizedTitle,
            Year = year
        };
        _db.CanonicalAlbums.Add(album);
        _albumCache[normalizedTitle] = album;
        // Note: SaveChangesAsync is deferred to the caller for batching.
        return album;
    }

    /// <summary>
    /// Gets or creates a canonical (shared) genre by name.
    /// No OwnerId filter — canonical genres are shared across all users.
    /// </summary>
    internal async Task<CanonicalGenre> GetOrCreateCanonicalGenreAsync(string name, CancellationToken cancellationToken)
    {
        if (_genreCache.TryGetValue(name, out var cached))
            return cached;

        var genre = await _db.CanonicalGenres
            .FirstOrDefaultAsync(g => g.Name == name, cancellationToken);

        if (genre is not null)
        {
            _genreCache[name] = genre;
            return genre;
        }

        genre = new CanonicalGenre { Name = name };
        _db.CanonicalGenres.Add(genre);
        _genreCache[name] = genre;
        // Note: SaveChangesAsync is deferred to the caller for batching.
        return genre;
    }

    internal static string GenerateSortName(string name)
    {
        if (name.StartsWith("The ", StringComparison.OrdinalIgnoreCase))
            return name[4..] + ", The";
        if (name.StartsWith("A ", StringComparison.OrdinalIgnoreCase))
            return name[2..] + ", A";
        if (name.StartsWith("An ", StringComparison.OrdinalIgnoreCase))
            return name[3..] + ", An";
        return name;
    }

    /// <summary>
    /// Creates a UserTrack junction and user-album/artist junctions for a canonical track.
    /// Legacy dual-write tables have been removed — only canonical + junction tables are used.
    /// </summary>
    private async Task<UserTrack> CreateUserTrackJunctionsAsync(
        Guid fileNodeId,
        string fileName,
        string mimeType,
        long sizeBytes,
        Guid ownerId,
        string contentHash,
        string title,
        CanonicalTrack canonicalTrack,
        CanonicalAlbum? canonicalAlbum,
        CanonicalArtist canonicalArtist,
        CanonicalGenre? canonicalGenre,
        int? trackNumber,
        int? discNumber,
        long durationTicks,
        long? bitrate,
        int? sampleRate,
        int? channels,
        int? year,
        string? musicBrainzRecordingId,
        CancellationToken cancellationToken)
    {
        // ── 1. Create UserTrack junction ──
        var userTrack = new UserTrack
        {
            OwnerId = ownerId,
            FileNodeId = fileNodeId,
            CanonicalTrackHash = contentHash,
            ContentHash = contentHash,
            CanonicalAlbumId = canonicalAlbum?.Id,
            PlayCount = 0
        };
        _db.UserTracks.Add(userTrack);

        // ── 2. Create UserAlbum junction if applicable ──
        if (canonicalAlbum is not null)
        {
            // Check ChangeTracker first to handle batched SaveChanges scenarios
            // (CloneLibraryFromExistingAsync, TryBulkIndexFromExistingAsync) where
            // a UserAlbum for this album may have been added but not yet saved.
            var existingUserAlbum = _db.UserAlbums.Local
                .FirstOrDefault(ua => ua.OwnerId == ownerId && ua.CanonicalAlbumId == canonicalAlbum.Id)
                ?? await _db.UserAlbums
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(ua => ua.OwnerId == ownerId && ua.CanonicalAlbumId == canonicalAlbum.Id, cancellationToken);
            if (existingUserAlbum is null)
            {
                _db.UserAlbums.Add(new UserAlbum
                {
                    OwnerId = ownerId,
                    CanonicalAlbumId = canonicalAlbum.Id
                });
            }
            else if (existingUserAlbum.IsDeleted)
            {
                existingUserAlbum.IsDeleted = false;
                existingUserAlbum.DeletedAt = null;
                existingUserAlbum.UpdatedAt = DateTime.UtcNow;
            }
        }

        // ── 3. Create UserArtist junction ──
        // Check ChangeTracker first (same batched SaveChanges consideration)
        var existingUserArtist = _db.UserArtists.Local
            .FirstOrDefault(ua => ua.OwnerId == ownerId && ua.CanonicalArtistId == canonicalArtist.Id)
            ?? await _db.UserArtists
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(ua => ua.OwnerId == ownerId && ua.CanonicalArtistId == canonicalArtist.Id, cancellationToken);
        if (existingUserArtist is null)
        {
            _db.UserArtists.Add(new UserArtist
            {
                OwnerId = ownerId,
                CanonicalArtistId = canonicalArtist.Id
            });
        }
        else if (existingUserArtist.IsDeleted)
        {
            existingUserArtist.IsDeleted = false;
            existingUserArtist.DeletedAt = null;
            existingUserArtist.UpdatedAt = DateTime.UtcNow;
        }

        // ── 3b. Ensure canonical junctions exist (track-artist, track-genre, album-artist) ──
        await EnsureCanonicalJunctionsAsync(contentHash, canonicalArtist, canonicalAlbum, canonicalGenre, cancellationToken);

        // ── 4. Update canonical album total duration ──
        if (canonicalAlbum is not null)
        {
            if (!_albumDurationCache.TryGetValue(canonicalAlbum.Id, out var canonicalDuration))
            {
                canonicalDuration = await _db.CanonicalTracks
                    .Where(ct => ct.UserTracks.Any(ut => ut.CanonicalAlbumId == canonicalAlbum.Id))
                    .SumAsync(ct => ct.DurationTicks, cancellationToken);
            }
            canonicalDuration += durationTicks;
            _albumDurationCache[canonicalAlbum.Id] = canonicalDuration;
            canonicalAlbum.TotalDurationTicks = canonicalDuration;
        }

        return userTrack;
    }

    /// <summary>
    /// Ensures canonical junction records (CanonicalTrackArtist, CanonicalTrackGenre, CanonicalAlbumArtist)
    /// exist. Uses pre-loaded in-memory sets when available (batch operations) to avoid per-track DB queries.
    /// Falls back to DB queries for single-file indexing paths.
    /// </summary>
    private async Task EnsureCanonicalJunctionsAsync(
        string contentHash,
        CanonicalArtist artist,
        CanonicalAlbum? album,
        CanonicalGenre? genre,
        CancellationToken cancellationToken)
    {
        // ── Track-artist junction ──
        if (_preloadedTrackArtists?.Contains((contentHash, artist.Id)) == true)
        {
            // Already exists per pre-loaded data — skip
        }
        else if (_preloadedTrackArtists is not null)
        {
            // Pre-loaded but not in set — doesn't exist, create it
            _db.CanonicalTrackArtists.Add(new CanonicalTrackArtist
            {
                TrackContentHash = contentHash,
                ArtistId = artist.Id,
                IsPrimary = true
            });
            _preloadedTrackArtists.Add((contentHash, artist.Id));
        }
        else
        {
            // No pre-loaded data (single-file path) — query DB
            var exists = await _db.CanonicalTrackArtists
                .AnyAsync(cta => cta.TrackContentHash == contentHash && cta.ArtistId == artist.Id, cancellationToken);
            if (!exists)
            {
                _db.CanonicalTrackArtists.Add(new CanonicalTrackArtist
                {
                    TrackContentHash = contentHash,
                    ArtistId = artist.Id,
                    IsPrimary = true
                });
            }
        }

        // ── Track-genre junction ──
        if (genre is not null)
        {
            if (_preloadedTrackGenres?.Contains((contentHash, genre.Id)) == true)
            {
                // Already exists — skip
            }
            else if (_preloadedTrackGenres is not null)
            {
                // Pre-loaded but not in set — doesn't exist, create it
                _db.CanonicalTrackGenres.Add(new CanonicalTrackGenre
                {
                    TrackContentHash = contentHash,
                    GenreId = genre.Id
                });
                _preloadedTrackGenres.Add((contentHash, genre.Id));
            }
            else
            {
                // No pre-loaded data — query DB
                var exists = await _db.CanonicalTrackGenres
                    .AnyAsync(ctg => ctg.TrackContentHash == contentHash && ctg.GenreId == genre.Id, cancellationToken);
                if (!exists)
                {
                    _db.CanonicalTrackGenres.Add(new CanonicalTrackGenre
                    {
                        TrackContentHash = contentHash,
                        GenreId = genre.Id
                    });
                }
            }
        }

        // ── Album-artist junction ──
        if (album is not null)
        {
            if (_preloadedAlbumArtists?.Contains((album.Id, artist.Id)) == true)
            {
                // Already exists — skip
            }
            else if (_preloadedAlbumArtists is not null)
            {
                // Pre-loaded but not in set — doesn't exist, create it
                _db.CanonicalAlbumArtists.Add(new CanonicalAlbumArtist
                {
                    AlbumId = album.Id,
                    ArtistId = artist.Id,
                    IsPrimary = true
                });
                _preloadedAlbumArtists.Add((album.Id, artist.Id));
            }
            else
            {
                // No pre-loaded data — query DB
                var exists = await _db.CanonicalAlbumArtists
                    .AnyAsync(caa => caa.AlbumId == album.Id && caa.ArtistId == artist.Id, cancellationToken);
                if (!exists)
                {
                    _db.CanonicalAlbumArtists.Add(new CanonicalAlbumArtist
                    {
                        AlbumId = album.Id,
                        ArtistId = artist.Id,
                        IsPrimary = true
                    });
                }
            }
        }
    }

    /// <summary>
    /// Parses artist and album names from a file path using the standard
    /// music library convention: Artist/Album/Track.ext.
    /// </summary>
    private static AudioMetadata? TryParseMetadataFromPath(string filePath, string fileName)
    {
        var parentDir = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(parentDir))
            return null;

        var grandparentDir = Path.GetDirectoryName(parentDir);

        return new AudioMetadata
        {
            Artist = grandparentDir is not null ? Path.GetFileName(grandparentDir) ?? "Unknown Artist" : "Unknown Artist",
            Album = Path.GetFileName(parentDir) ?? "Unknown Album",
            Title = Path.GetFileNameWithoutExtension(fileName) ?? "Unknown Track"
        };
    }

    /// <summary>
    /// Heuristic: returns true if a TagLib#-extracted value looks like garbage
    /// (empty/whitespace, or numeric-only like "01" leaked from a track-number field).
    /// </summary>
    private static bool IsGarbageValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        // Numeric-only values (e.g., "01", "123") are almost certainly not real names
        if (int.TryParse(value, out _))
            return true;


        // Windows Media Player / generic tagger filler patterns
        if (value.StartsWith("Unknown Album", StringComparison.OrdinalIgnoreCase))
            return true;
        if (value.StartsWith("Unknown Artist", StringComparison.OrdinalIgnoreCase))
            return true;
        if (value.StartsWith("Unknown Genre", StringComparison.OrdinalIgnoreCase))
            return true;

        // "Track N" pattern (e.g., "Track 1", "Track 12")
        if (value.StartsWith("Track ", StringComparison.OrdinalIgnoreCase)
            && value.Length > 6
            && int.TryParse(value.AsSpan(6), out _))
            return true;
        return false;
    }

    /// <summary>
    /// Extracts a numeric track number from the beginning of a filename.
    /// Handles patterns like "01 Title.mp3", "01 - Title.mp3", "1. Title.mp3".
    /// Returns null if no leading digits are found.
    /// </summary>
    private static int? TryExtractTrackNumberFromFileName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrEmpty(name))
            return null;

        int i = 0;
        while (i < name.Length && !char.IsDigit(name[i]))
            i++;

        if (i >= name.Length)
            return null;

        int start = i;
        while (i < name.Length && char.IsDigit(name[i]))
            i++;

        var span = name.AsSpan(start, i - start);
        return int.TryParse(span, out var num) && num > 0 ? num : null;
    }

    /// <summary>
    /// ISRC codes are always exactly 12 characters (e.g., USRC17607839).
    /// Reject anything that doesn't match this format — some audio files
    /// have garbage data (URLs, etc.) in the ISRC field.
    /// </summary>
    private static string? ValidateIsrc(string? isrc)
    {
        if (string.IsNullOrWhiteSpace(isrc))
            return null;

        // ISRC format: CC-XXX-YY-NNNNN (12 chars, alphanumeric)
        // Validate length and basic format
        var trimmed = isrc.Trim();
        if (trimmed.Length < 5 || trimmed.Length > 20)
            return null;

        // Must be mostly alphanumeric (allow hyphens)
        if (trimmed.Any(c => !char.IsLetterOrDigit(c) && c != '-'))
            return null;

        return trimmed;
    }

    /// <summary>
    /// Truncates composers string to fit the database column (nvarchar(512)).
    /// </summary>
    private static string? TruncateComposers(string? composers)
    {
        if (string.IsNullOrWhiteSpace(composers))
            return null;

        return composers.Length <= 500 ? composers : composers[..500];
    }

    /// <summary>
    /// Looks up the SHA-256 content hash for a FileNode from the Files module.
    /// Returns null if the FileNode doesn't exist or has no hash.
    /// </summary>
    private async Task<string?> LookupContentHashAsync(Guid fileNodeId, CancellationToken cancellationToken)
    {
        try
        {
            // Note: This raw SQL goes against the shared "core"."FileNodes" table which is not
            // part of MusicDbContext's model. The table/column names are resolved via the
            // injected ITableNamingStrategy for multi-provider support.
            var tableName = _namingStrategy.GetTableName("FileNodes", "core");
            var idCol = _namingStrategy.GetColumnName("Id");
            var hashCol = _namingStrategy.GetColumnName("ContentHash");
            var sql = $"SELECT {hashCol} AS Value FROM {tableName} WHERE {idCol} = {{0}}";
            return await _db.Database
                .SqlQueryRaw<string>(sql, fileNodeId)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not look up ContentHash for FileNode {FileNodeId}", fileNodeId);
            return null;
        }
    }

    /// <summary>
    /// Returns all distinct ContentHashes from canonical tracks.
    /// Used by the scanner to pre-resolve deduplicated matches without enumerating files.
    /// </summary>
    public async Task<HashSet<string>> GetExistingContentHashesAsync(CancellationToken cancellationToken = default)
    {
        var hashes = await _db.CanonicalTracks
            .Select(ct => ct.ContentHash)
            .Distinct()
            .ToListAsync(cancellationToken);
        return [.. hashes];
    }

    /// <summary>
    /// Clones another user's entire music library into the current user in a single batch.
    /// Uses canonical tables for deduplication — only creates UserTrack/UserAlbum/UserArtist
    /// junctions for the target owner. Skips tracks already indexed for the current user.
    /// </summary>
    public async Task<int> CloneLibraryFromExistingAsync(Guid ownerId, IProgress<MediaScanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var existingUserTrackFileNodeIds = await _db.UserTracks
            .Where(ut => ut.OwnerId == ownerId)
            .Select(ut => ut.FileNodeId)
            .ToListAsync(cancellationToken);
        var existingSet = existingUserTrackFileNodeIds.ToHashSet();

        // Get source user tracks from other users (with canonical includes)
        var sourceUserTracks = await _db.UserTracks
            .Include(ut => ut.CanonicalTrack)
                .ThenInclude(ct => ct!.TrackArtists).ThenInclude(cta => cta.Artist)
            .Include(ut => ut.CanonicalTrack)
                .ThenInclude(ct => ct!.TrackGenres).ThenInclude(ctg => ctg.Genre)
            .Include(ut => ut.CanonicalAlbum)
            .IgnoreQueryFilters()
            .Where(ut => ut.OwnerId != ownerId && !ut.IsDeleted && ut.CanonicalTrack != null)
            .ToListAsync(cancellationToken);

        if (sourceUserTracks.Count == 0)
            return 0;

        // Filter to only source tracks that are not already indexed for this owner
        var candidates = sourceUserTracks
            .Where(ut => !existingSet.Contains(ut.FileNodeId))
            .ToList();

        var totalCandidates = candidates.Count;
        if (totalCandidates == 0)
            return 0;

        // ── Pre-load ALL existing canonical data into memory ──
        // This eliminates ALL per-track DB round trips. Everything is
        // resolved from in-memory dictionaries during the batch loop.
        var canonicalTrackLookup = (await _db.CanonicalTracks
            .ToListAsync(cancellationToken))
            .ToDictionary(ct => ct.ContentHash);

        var userAlbumLookup = (await _db.UserAlbums
            .IgnoreQueryFilters()
            .Where(ua => ua.OwnerId == ownerId)
            .ToListAsync(cancellationToken))
            .ToDictionary(ua => ua.CanonicalAlbumId);

        var userArtistLookup = (await _db.UserArtists
            .IgnoreQueryFilters()
            .Where(ua => ua.OwnerId == ownerId)
            .ToListAsync(cancellationToken))
            .ToDictionary(ua => ua.CanonicalArtistId);

        _preloadedTrackArtists = (await _db.CanonicalTrackArtists
            .Select(cta => new { cta.TrackContentHash, cta.ArtistId })
            .ToListAsync(cancellationToken))
            .Select(x => (x.TrackContentHash, x.ArtistId))
            .ToHashSet();
        _preloadedTrackGenres = (await _db.CanonicalTrackGenres
            .Select(ctg => new { ctg.TrackContentHash, ctg.GenreId })
            .ToListAsync(cancellationToken))
            .Select(x => (x.TrackContentHash, x.GenreId))
            .ToHashSet();
        _preloadedAlbumArtists = (await _db.CanonicalAlbumArtists
            .Select(caa => new { caa.AlbumId, caa.ArtistId })
            .ToListAsync(cancellationToken))
            .Select(x => (x.AlbumId, x.ArtistId))
            .ToHashSet();

        // ── Disable auto-detect changes ──
        // FindAsync triggers DetectChanges which is O(n) in tracked entity count.
        // Since we batch-flush, we keep the tracker small and manage it manually.
        var prevAutoDetect = _db.ChangeTracker.AutoDetectChangesEnabled;
        _db.ChangeTracker.AutoDetectChangesEnabled = false;

        const int BatchSize = 500;
        var processed = 0;

        try
        {
            var batchUserTracks = new List<UserTrack>(BatchSize);
            var batchUserAlbums = new List<UserAlbum>();
            var batchUserArtists = new List<UserArtist>();
            var batchCanonicalTracks = new List<CanonicalTrack>();

            foreach (var sourceUt in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new MediaScanProgress
                {
                    Phase = "Cloning music library...",
                    CurrentFile = sourceUt.CanonicalTrack?.Title ?? "Unknown",
                    FilesProcessed = processed,
                    TotalFiles = totalCandidates,
                    Imported = processed,
                    PercentComplete = totalCandidates > 0
                        ? (int)((long)processed * 100 / totalCandidates)
                        : 0,
                });

                var canonicalTrack = sourceUt.CanonicalTrack!;
                var contentHash = sourceUt.CanonicalTrackHash;

                if (contentHash is null)
                {
                    processed++;
                    continue;
                }

                // ── Resolve/create canonical artist (in-memory cache) ──
                var sourceArtistName = canonicalTrack.TrackArtists
                    .FirstOrDefault(cta => cta.IsPrimary)?.Artist?.Name
                    ?? canonicalTrack.TrackArtists.FirstOrDefault()?.Artist?.Name
                    ?? "Unknown Artist";
                if (!_artistCache.TryGetValue(sourceArtistName, out var canonicalArtist))
                {
                    canonicalArtist = await _db.CanonicalArtists
                        .FirstOrDefaultAsync(a => a.Name == sourceArtistName, cancellationToken)
                        ?? new CanonicalArtist { Name = sourceArtistName, SortName = GenerateSortName(sourceArtistName) };
                    if (canonicalArtist.Id == Guid.Empty)
                        _db.CanonicalArtists.Add(canonicalArtist);
                    _artistCache[sourceArtistName] = canonicalArtist;
                }

                var canonicalAlbum = sourceUt.CanonicalAlbum;

                // ── Resolve/create canonical genre (in-memory cache) ──
                CanonicalGenre? canonicalGenre = null;
                var sourceGenre = canonicalTrack.TrackGenres.FirstOrDefault()?.Genre;
                if (sourceGenre is not null && !_genreCache.TryGetValue(sourceGenre.Name, out canonicalGenre))
                {
                    canonicalGenre = await _db.CanonicalGenres
                        .FirstOrDefaultAsync(g => g.Name == sourceGenre.Name, cancellationToken)
                        ?? new CanonicalGenre { Name = sourceGenre.Name };
                    if (canonicalGenre.Id == Guid.Empty)
                        _db.CanonicalGenres.Add(canonicalGenre);
                    _genreCache[sourceGenre.Name] = canonicalGenre;
                }

                // ── Ensure canonical track exists (in-memory dict, 0 DB queries) ──
                if (!canonicalTrackLookup.TryGetValue(contentHash, out var ct))
                {
                    ct = new CanonicalTrack
                    {
                        ContentHash = contentHash,
                        Title = canonicalTrack.Title,
                        TrackNumber = canonicalTrack.TrackNumber,
                        DiscNumber = canonicalTrack.DiscNumber,
                        DurationTicks = canonicalTrack.DurationTicks,
                        Bitrate = canonicalTrack.Bitrate,
                        SampleRate = canonicalTrack.SampleRate,
                        Channels = canonicalTrack.Channels,
                        MimeType = canonicalTrack.MimeType,
                        Year = canonicalTrack.Year,
                        MusicBrainzRecordingId = canonicalTrack.MusicBrainzRecordingId
                    };
                    batchCanonicalTracks.Add(ct);
                    canonicalTrackLookup[contentHash] = ct;
                }

                // ── UserTrack junction ──
                batchUserTracks.Add(new UserTrack
                {
                    OwnerId = ownerId,
                    FileNodeId = sourceUt.FileNodeId,
                    CanonicalTrackHash = contentHash,
                    ContentHash = contentHash,
                    CanonicalAlbumId = canonicalAlbum?.Id,
                    PlayCount = 0
                });

                // ── UserAlbum junction (in-memory dict, 0 DB queries) ──
                if (canonicalAlbum is not null && !userAlbumLookup.ContainsKey(canonicalAlbum.Id))
                {
                    batchUserAlbums.Add(new UserAlbum
                    {
                        OwnerId = ownerId,
                        CanonicalAlbumId = canonicalAlbum.Id
                    });
                    userAlbumLookup[canonicalAlbum.Id] = null!;
                }

                // ── UserArtist junction (in-memory dict, 0 DB queries) ──
                if (!userArtistLookup.ContainsKey(canonicalArtist.Id))
                {
                    batchUserArtists.Add(new UserArtist
                    {
                        OwnerId = ownerId,
                        CanonicalArtistId = canonicalArtist.Id
                    });
                    userArtistLookup[canonicalArtist.Id] = null!;
                }

                processed++;

                // ── Flush batch ──
                if (batchUserTracks.Count >= BatchSize)
                {
                    _db.UserTracks.AddRange(batchUserTracks);
                    if (batchUserAlbums.Count > 0)
                        _db.UserAlbums.AddRange(batchUserAlbums);
                    if (batchUserArtists.Count > 0)
                        _db.UserArtists.AddRange(batchUserArtists);
                    if (batchCanonicalTracks.Count > 0)
                        _db.CanonicalTracks.AddRange(batchCanonicalTracks);
                    await _db.SaveChangesAsync(cancellationToken);

                    batchUserTracks.Clear();
                    batchUserAlbums.Clear();
                    batchUserArtists.Clear();
                    batchCanonicalTracks.Clear();
                }
            }

            // ── Flush remaining batch ──
            if (batchUserTracks.Count > 0)
            {
                _db.UserTracks.AddRange(batchUserTracks);
                if (batchUserAlbums.Count > 0)
                    _db.UserAlbums.AddRange(batchUserAlbums);
                if (batchUserArtists.Count > 0)
                    _db.UserArtists.AddRange(batchUserArtists);
                if (batchCanonicalTracks.Count > 0)
                    _db.CanonicalTracks.AddRange(batchCanonicalTracks);
                await _db.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation(
                "CloneLibraryFromExisting: cloned {Count} tracks for owner {OwnerId} via canonical dedup",
                processed, ownerId);

            progress?.Report(new MediaScanProgress
            {
                Phase = "Clone complete",
                FilesProcessed = processed,
                TotalFiles = totalCandidates,
                Imported = processed,
                PercentComplete = 100,
            });

            return processed;
        }
        finally
        {
            _db.ChangeTracker.AutoDetectChangesEnabled = prevAutoDetect;
            ClearPreloadedJunctions();
        }
    }

    /// <summary>
    /// Clears pre-loaded junction caches to free memory after batch operations complete.
    /// </summary>
    private void ClearPreloadedJunctions()
    {
        _preloadedTrackArtists = null;
        _preloadedTrackGenres = null;
        _preloadedAlbumArtists = null;
    }

    /// <summary>
    /// Returns the set of FileNode IDs that are already indexed in the music library for the given owner.
    /// Only non-deleted tracks are returned; soft-deleted tracks are excluded so they can be re-indexed
    /// if the source file reappears.
    /// </summary>
    public async Task<HashSet<Guid>> GetIndexedFileNodeIdsAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var ids = await _db.UserTracks
            .Where(ut => ut.OwnerId == ownerId)
            .Select(ut => ut.FileNodeId)
            .ToListAsync(cancellationToken);
        return [.. ids];
    }

    /// <summary>
    /// Hard-deletes UserTrack records whose source FileNodes no longer exist.
    /// Canonical data (CanonicalTrack, CanonicalAlbum, CanonicalArtist) is preserved.
    /// </summary>
    /// <param name="deletedFileNodeIds">FileNode IDs whose backing files have been deleted.</param>
    /// <param name="ownerId">Owner user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of tracks hard-deleted.</returns>
    public async Task<int> SoftDeleteTracksAsync(IReadOnlyCollection<Guid> deletedFileNodeIds, Guid ownerId, CancellationToken cancellationToken = default)
    {
        if (deletedFileNodeIds.Count == 0)
            return 0;

        var tracksToDelete = await _db.UserTracks
            .Where(ut => ut.OwnerId == ownerId && deletedFileNodeIds.Contains(ut.FileNodeId) && !ut.IsDeleted)
            .ToListAsync(cancellationToken);

        if (tracksToDelete.Count == 0)
            return 0;

        _db.UserTracks.RemoveRange(tracksToDelete);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Hard-deleted {Count} tracks for user {OwnerId} (source files removed from library)",
            tracksToDelete.Count, ownerId);

        return tracksToDelete.Count;
    }

    /// <summary>
    /// Deletes all music library metadata for a specific owner (tracks, albums, artists,
    /// play history, etc.) from the database. Does NOT delete the actual audio files — only the
    /// indexed metadata. After calling this, a re-scan will rebuild the library from scratch.
    /// Other users' libraries are NEVER affected.
    /// </summary>
    /// <param name="ownerId">Owner whose library will be reset.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ResetCollectionAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("RESET: Deleting music library metadata for owner {OwnerId}", ownerId);

        // Delete in FK-safe order: child tables first, then parents.
        // ALL deletes are scoped to ownerId — never touch other users' data.

        // UserTrack IDs for this owner
        var ownedTrackIds = await _db.UserTracks
            .IgnoreQueryFilters()
            .Where(ut => ut.OwnerId == ownerId)
            .Select(ut => ut.Id)
            .ToListAsync(cancellationToken);
        var ownedTrackIdSet = ownedTrackIds.ToHashSet();

        // PlaybackHistories: filter by owned user tracks
        var ph = await _db.PlaybackHistories
            .IgnoreQueryFilters()
            .Where(h => ownedTrackIdSet.Contains(h.UserTrackId))
            .ToListAsync(cancellationToken);

        // ScrobbleRecords: filter by owned user tracks
        var sr = await _db.ScrobbleRecords
            .IgnoreQueryFilters()
            .Where(s => ownedTrackIdSet.Contains(s.UserTrackId))
            .ToListAsync(cancellationToken);

        // StarredItems: filter by UserId (owner)
        var si = await _db.StarredItems
            .IgnoreQueryFilters()
            .Where(s => s.UserId == ownerId)
            .ToListAsync(cancellationToken);

        // PlaylistTracks: filter by owned user tracks
        var pt = await _db.PlaylistTracks
            .IgnoreQueryFilters()
            .Where(pt => ownedTrackIdSet.Contains(pt.UserTrackId))
            .ToListAsync(cancellationToken);

        // UserTracks: directly scoped to ownerId
        var ut = await _db.UserTracks
            .IgnoreQueryFilters()
            .Where(u => u.OwnerId == ownerId)
            .ToListAsync(cancellationToken);

        // Playlists: scoped to ownerId
        var pl = await _db.Playlists
            .IgnoreQueryFilters()
            .Where(p => p.OwnerId == ownerId)
            .ToListAsync(cancellationToken);

        // UserAlbums: scoped to ownerId
        var ua = await _db.UserAlbums
            .IgnoreQueryFilters()
            .Where(a => a.OwnerId == ownerId)
            .ToListAsync(cancellationToken);

        // UserArtists: scoped to ownerId
        var uar = await _db.UserArtists
            .IgnoreQueryFilters()
            .Where(a => a.OwnerId == ownerId)
            .ToListAsync(cancellationToken);

        // Execute deletes
        _db.PlaybackHistories.RemoveRange(ph);
        _db.ScrobbleRecords.RemoveRange(sr);
        _db.StarredItems.RemoveRange(si);
        _db.PlaylistTracks.RemoveRange(pt);
        _db.UserTracks.RemoveRange(ut);
        _db.Playlists.RemoveRange(pl);
        _db.UserAlbums.RemoveRange(ua);
        _db.UserArtists.RemoveRange(uar);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("RESET complete for owner {OwnerId}: {TrackCount} user tracks, {AlbumCount} user albums, {ArtistCount} user artists, {PHCount} playback histories",
            ownerId, ut.Count, ua.Count, uar.Count, ph.Count);
    }
}
