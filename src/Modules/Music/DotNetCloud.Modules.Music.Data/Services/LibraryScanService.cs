using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Music.Models;
using DotNetCloud.Modules.Music.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace DotNetCloud.Modules.Music.Data.Services;

/// <summary>
/// Scans a user's files for audio content and builds/updates the music library.
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

    // Per-scan caches to eliminate redundant DB round trips for repeated
    // Artist/Album/Genre lookups across many files (common during bulk scans).
    private readonly Dictionary<(string Name, Guid OwnerId), Artist> _artistCache = new();
    private readonly Dictionary<(string Title, Guid ArtistId, Guid OwnerId), MusicAlbum> _albumCache = new();
    private readonly Dictionary<string, Genre> _genreCache = new();

    // Tracks album total duration incrementally to avoid O(n²) SUM queries.
    private readonly Dictionary<Guid, long> _albumDurationCache = new();

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
    /// Indexes a single audio file into the music library. Creates or updates
    /// artist, album, track, and genre records from the file's metadata.
    /// </summary>
    public async Task<Track?> IndexFileAsync(
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

        // Check if already indexed for this user
        var existing = await _db.Tracks
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.FileNodeId == fileNodeId && t.OwnerId == ownerId, cancellationToken);

        if (existing is not null && !existing.IsDeleted)
        {
            _logger.LogDebug("File {FileNodeId} already indexed as track {TrackId}", fileNodeId, existing.Id);
            return existing;
        }

        // ── Cross-owner copy: if another user already indexed this file, clone their
        //     metadata to avoid re-running expensive TagLib/ffmpeg extraction.
        var crossOwnerTrack = await TryIndexFromExistingOwnerAsync(
            fileNodeId, fileName, mimeType, sizeBytes, ownerId, cancellationToken);
        if (crossOwnerTrack is not null)
        {
            return crossOwnerTrack;
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
                DurationTicks = 0
            };
        }

        // Get or create artist (uses in-memory cache, defers save)
        var artist = await GetOrCreateArtistAsync(metadata.AlbumArtist ?? metadata.Artist, ownerId, cancellationToken);

        // Get or create album (uses in-memory cache, defers save)
        var album = await GetOrCreateAlbumAsync(metadata.Album, artist.Id, ownerId, metadata.Year, cancellationToken);

        // Handle album art
        if (!album.HasCoverArt)
        {
            string? artPath = null;
            if (resolvedPath is not null)
            {
                artPath = _albumArtService.ExtractAndCacheArt(resolvedPath, _artCacheDir, album.Id);
            }
            else if (audioStream is not null && audioStream.CanSeek)
            {
                audioStream.Position = 0;
                artPath = _albumArtService.ExtractAndCacheArt(audioStream, mimeType, fileName, _artCacheDir, album.Id);
            }

            if (artPath is not null)
            {
                album.HasCoverArt = true;
                album.CoverArtPath = artPath;
            }
        }

        // Get or create genre (uses in-memory cache, defers save)
        Genre? genre = null;
        if (!string.IsNullOrWhiteSpace(metadata.Genre))
        {
            genre = await GetOrCreateGenreAsync(metadata.Genre, cancellationToken);
        }

        // Create or update track
        Track track;
        if (existing is not null)
        {
            // Re-index previously deleted track
            track = existing;
            track.IsDeleted = false;
            track.DeletedAt = null;
        }
        else
        {
            track = new Track
            {
                FileNodeId = fileNodeId,
                OwnerId = ownerId,
                Title = metadata.Title,
                MimeType = mimeType,
                FileName = Path.GetFileName(fileName)
            };
            _db.Tracks.Add(track);
        }

        track.Title = metadata.Title;
        track.TrackNumber = metadata.TrackNumber;
        track.DiscNumber = metadata.DiscNumber;
        track.DurationTicks = metadata.DurationTicks;
        track.SizeBytes = sizeBytes;
        track.Bitrate = metadata.Bitrate;
        track.SampleRate = metadata.SampleRate;
        track.Channels = metadata.Channels;
        track.AlbumId = album.Id;
        track.Year = metadata.Year;
        track.UpdatedAt = DateTime.UtcNow;

        // Store ContentHash for future cross-owner lookup
        track.ContentHash = await LookupContentHashAsync(fileNodeId, cancellationToken);

        // Create track-artist association (if not already existing for re-index)
        var trackArtistExists = await _db.TrackArtists
            .AnyAsync(ta => ta.TrackId == track.Id && ta.ArtistId == artist.Id, cancellationToken);
        if (!trackArtistExists)
        {
            _db.TrackArtists.Add(new TrackArtist
            {
                TrackId = track.Id,
                ArtistId = artist.Id,
                IsPrimary = true
            });
        }

        // Create track-genre association
        if (genre is not null)
        {
            var trackGenreExists = await _db.TrackGenres
                .AnyAsync(tg => tg.TrackId == track.Id && tg.GenreId == genre.Id, cancellationToken);
            if (!trackGenreExists)
            {
                _db.TrackGenres.Add(new TrackGenre
                {
                    TrackId = track.Id,
                    GenreId = genre.Id
                });
            }
        }

        // Update album total duration incrementally (avoids O(n²) SUM queries)
        if (!_albumDurationCache.TryGetValue(album.Id, out var currentDuration))
        {
            currentDuration = await _db.Tracks
                .Where(t => t.AlbumId == album.Id)
                .SumAsync(t => t.DurationTicks, cancellationToken);
        }
        currentDuration += track.DurationTicks;
        _albumDurationCache[album.Id] = currentDuration;
        album.TotalDurationTicks = currentDuration;

        // Single batch save for all changes in this file.
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Indexed track {TrackId} '{Title}' by '{Artist}' on '{Album}'",
            track.Id, track.Title, artist.Name, album.Title);

        await _eventBus.PublishAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "music",
            EntityId = track.Id.ToString(),
            Action = SearchIndexAction.Index
        }, new CallerContext(ownerId, ["user"], CallerType.User), cancellationToken);

        return track;
    }

    /// <summary>
    /// Attempts to find an existing track for this file owned by another user and clones
    /// their metadata into a new track record for the current owner.
    /// Returns the new track if a source was found and copied, or null if no cross-owner
    /// match exists (caller should proceed with full metadata extraction).
    /// This is READ-ONLY on the source user's data — never modifies, deletes, or reassigns.
    /// Only CREATEs new records for the current user.
    /// </summary>
    public async Task<Track?> TryIndexFromExistingOwnerAsync(
        Guid fileNodeId,
        string fileName,
        string mimeType,
        long sizeBytes,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        // ── Cross-owner copy: if another user already indexed this file, clone their
        //     metadata to avoid re-running expensive TagLib/ffmpeg extraction.
        //     IMPORTANT: This is READ-ONLY on the other user's data. Nothing is modified,
        //     deleted, or reassigned. We only CREATE new records for the current owner.
        Track? sourceTrack = null;
        string? contentHash = null;
        string? copyStrategy = null;

        // Strategy 1: Same FileNodeId (same file visible to multiple users via sharing)
        sourceTrack = await _db.Tracks
            .Include(t => t.Album)
            .Include(t => t.TrackArtists).ThenInclude(ta => ta.Artist)
            .Include(t => t.TrackGenres).ThenInclude(tg => tg.Genre)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.FileNodeId == fileNodeId && t.OwnerId != ownerId && !t.IsDeleted,
                cancellationToken);

        if (sourceTrack is not null)
        {
            copyStrategy = "FileNodeId";
        }
        else
        {
            // Strategy 2: Same ContentHash (different uploads of identical file content)
            contentHash = await LookupContentHashAsync(fileNodeId, cancellationToken);

            if (contentHash is not null)
            {
                sourceTrack = await _db.Tracks
                    .Include(t => t.Album)
                    .Include(t => t.TrackArtists).ThenInclude(ta => ta.Artist)
                    .Include(t => t.TrackGenres).ThenInclude(tg => tg.Genre)
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(t => t.ContentHash == contentHash && t.OwnerId != ownerId && !t.IsDeleted,
                        cancellationToken);

                if (sourceTrack is not null)
                    copyStrategy = "ContentHash";
            }
        }

        if (sourceTrack is null)
        {
            return null;
        }

        _logger.LogInformation(
            "Cross-owner copy ({Strategy}): cloning metadata from track {SourceTrackId} (owner={SourceOwnerId}) for FileNode {FileNodeId} into owner {OwnerId}",
            copyStrategy, sourceTrack.Id, sourceTrack.OwnerId, fileNodeId, ownerId);

        // ── SAFETY: Never touch sourceTrack or anything owned by sourceTrack.OwnerId ──
        // All new records below use ownerId (the CURRENT user), never sourceTrack.OwnerId.

        // Look up content hash upfront so we don't need an intermediate save.
        if (contentHash is null)
            contentHash = await LookupContentHashAsync(fileNodeId, cancellationToken);

        // Clone artist for the current owner
        var sourceArtistName = sourceTrack.TrackArtists
            .FirstOrDefault(ta => ta.IsPrimary)?.Artist?.Name
            ?? sourceTrack.TrackArtists.FirstOrDefault()?.Artist?.Name
            ?? "Unknown Artist";

        var newArtist = await GetOrCreateArtistAsync(sourceArtistName, ownerId, cancellationToken);

        // Clone album for the current owner
        MusicAlbum? newAlbum = null;
        if (sourceTrack.AlbumId.HasValue && sourceTrack.Album is not null)
        {
            newAlbum = await GetOrCreateAlbumAsync(
                sourceTrack.Album.Title, newArtist.Id, ownerId, sourceTrack.Album.Year, cancellationToken);

            // Copy album art from the source user's cached art (avoids re-extracting from file).
            // Only copy if the source has art and the new album doesn't already have it.
            if (!newAlbum.HasCoverArt && sourceTrack.Album.HasCoverArt)
            {
                var artPath = _albumArtService.CopyArtFromExisting(
                    _artCacheDir, sourceTrack.Album.Id, newAlbum.Id);
                if (artPath is not null)
                {
                    newAlbum.HasCoverArt = true;
                    newAlbum.CoverArtPath = artPath;
                }
            }
        }

        // Clone genre
        Genre? newGenre = null;
        var sourceGenre = sourceTrack.TrackGenres.FirstOrDefault()?.Genre;
        if (sourceGenre is not null)
            newGenre = await GetOrCreateGenreAsync(sourceGenre.Name, cancellationToken);

        // Create NEW track record for the current owner — copies metadata only
        var newTrack = new Track
        {
            FileNodeId = fileNodeId,
            OwnerId = ownerId,  // <-- CURRENT user, NOT source owner
            Title = sourceTrack.Title,
            FileName = sourceTrack.FileName,
            MimeType = sourceTrack.MimeType,
            TrackNumber = sourceTrack.TrackNumber,
            DiscNumber = sourceTrack.DiscNumber,
            DurationTicks = sourceTrack.DurationTicks,
            SizeBytes = sourceTrack.SizeBytes,
            Bitrate = sourceTrack.Bitrate,
            SampleRate = sourceTrack.SampleRate,
            Channels = sourceTrack.Channels,
            AlbumId = newAlbum?.Id,
            Year = sourceTrack.Year,
            ContentHash = contentHash,
            MusicBrainzRecordingId = sourceTrack.MusicBrainzRecordingId,
        };

        _db.Tracks.Add(newTrack);

        // Create track-artist junction for the NEW track
        _db.TrackArtists.Add(new TrackArtist
        {
            TrackId = newTrack.Id,
            ArtistId = newArtist.Id,
            IsPrimary = true
        });

        // Create track-genre junction for the NEW track
        if (newGenre is not null)
        {
            _db.TrackGenres.Add(new TrackGenre
            {
                TrackId = newTrack.Id,
                GenreId = newGenre.Id
            });
        }

        // Update NEW album total duration incrementally (avoids O(n²) SUM queries)
        if (newAlbum is not null)
        {
            if (!_albumDurationCache.TryGetValue(newAlbum.Id, out var currentDuration))
            {
                // First time seeing this album in this scan — load existing total
                currentDuration = await _db.Tracks
                    .Where(t => t.AlbumId == newAlbum.Id)
                    .SumAsync(t => t.DurationTicks, cancellationToken);
            }
            currentDuration += newTrack.DurationTicks;
            _albumDurationCache[newAlbum.Id] = currentDuration;
            newAlbum.TotalDurationTicks = currentDuration;
        }

        // Single batch save for all changes in this file.
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Indexed track {TrackId} '{Title}' by '{Artist}' on '{Album}' (cloned from {SourceTrackId}, owner={OwnerId})",
            newTrack.Id, newTrack.Title, newArtist.Name, newAlbum?.Title ?? "(none)", sourceTrack.Id, ownerId);

        await _eventBus.PublishAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "music",
            EntityId = newTrack.Id.ToString(),
            Action = SearchIndexAction.Index
        }, new CallerContext(ownerId, ["user"], CallerType.User), cancellationToken);

        // ── SAFETY AUDIT: Verify source track was NOT modified ──
        var verifySource = await _db.Tracks
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == sourceTrack.Id, cancellationToken);

        if (verifySource is null || verifySource.IsDeleted || verifySource.OwnerId != sourceTrack.OwnerId)
        {
            _logger.LogError(
                "CRITICAL: Source track {SourceTrackId} was unexpectedly modified during cross-owner copy! " +
                "exists={Exists}, isDeleted={IsDeleted}, ownerId={OwnerId}, expectedOwner={ExpectedOwner}",
                sourceTrack.Id,
                verifySource is not null, verifySource?.IsDeleted, verifySource?.OwnerId, sourceTrack.OwnerId);
        }

        return newTrack;
    }

    /// <summary>
    /// Attempts a bulk cross-owner copy for a set of file nodes. Queries all source tracks
    /// matching by ContentHash in a single query, then does a single batch insert for all
    /// new Track/Artist/Album/Genre records. Skips files already indexed for this owner.
    /// Returns the set of FileNode IDs that were successfully indexed via cross-owner copy.
    /// </summary>
    public async Task<HashSet<Guid>> TryBulkIndexFromExistingAsync(
        IReadOnlyCollection<Guid> fileNodeIds,
        IReadOnlyDictionary<Guid, string?> contentHashMap,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        if (fileNodeIds.Count == 0)
            return [];

        // ── Strategy 1: Match by FileNodeId (same virtual FileNode for both users, e.g. admin shared folders) ──
        var sourceById = new Dictionary<Guid, Track>();
        var fileNodeIdTracks = await _db.Tracks
            .Include(t => t.Album)
            .Include(t => t.TrackArtists).ThenInclude(ta => ta.Artist)
            .Include(t => t.TrackGenres).ThenInclude(tg => tg.Genre)
            .IgnoreQueryFilters()
            .Where(t => fileNodeIds.Contains(t.FileNodeId) && t.OwnerId != ownerId && !t.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var track in fileNodeIdTracks)
        {
            if (!sourceById.ContainsKey(track.FileNodeId))
                sourceById[track.FileNodeId] = track;
        }

        // ── Strategy 2: Match by ContentHash (different FileNodes with same content) ──
        var sourceByHash = new Dictionary<string, Track>();
        var hashes = contentHashMap.Values.Where(h => h is not null).Cast<string>().Distinct().ToList();

        if (hashes.Count > 0)
        {
            var hashTracks = await _db.Tracks
                .Include(t => t.Album)
                .Include(t => t.TrackArtists).ThenInclude(ta => ta.Artist)
                .Include(t => t.TrackGenres).ThenInclude(tg => tg.Genre)
                .IgnoreQueryFilters()
                .Where(t => t.ContentHash != null && hashes.Contains(t.ContentHash) && t.OwnerId != ownerId && !t.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var track in hashTracks)
            {
                if (track.ContentHash is not null && !sourceByHash.ContainsKey(track.ContentHash))
                    sourceByHash[track.ContentHash] = track;
            }
        }

        if (sourceById.Count == 0 && sourceByHash.Count == 0)
            return [];

        // Step 3: Get already-indexed FileNodeIds for this user
        var alreadyIndexed = await _db.Tracks
            .Where(t => t.OwnerId == ownerId && fileNodeIds.Contains(t.FileNodeId) && !t.IsDeleted)
            .Select(t => t.FileNodeId)
            .ToListAsync(cancellationToken);
        var alreadyIndexedSet = alreadyIndexed.ToHashSet();

        // Step 5: Build all records in memory using caches
        var newTracks = new List<Track>();
        var newTrackArtists = new List<TrackArtist>();
        var newTrackGenres = new List<TrackGenre>();
        var copiedIds = new HashSet<Guid>();

        foreach (var fileNodeId in fileNodeIds)
        {
            if (alreadyIndexedSet.Contains(fileNodeId))
                continue;

            // Try Strategy 1: Same FileNodeId (admin shared folders / mounted entries)
            Track? sourceTrack = null;
            string? contentHash = null;
            if (sourceById.TryGetValue(fileNodeId, out var idMatch))
            {
                sourceTrack = idMatch;
            }
            else
            {
                // Try Strategy 2: Same ContentHash (different uploads of identical files)
                if (!contentHashMap.TryGetValue(fileNodeId, out contentHash) || contentHash is null)
                    continue;
                if (!sourceByHash.TryGetValue(contentHash, out var hashMatch))
                    continue;
                sourceTrack = hashMatch;
            }

            // Get or create artist (uses in-memory cache, no DB hit after first unique name)
            var sourceArtistName = sourceTrack.TrackArtists
                .FirstOrDefault(ta => ta.IsPrimary)?.Artist?.Name
                ?? sourceTrack.TrackArtists.FirstOrDefault()?.Artist?.Name
                ?? "Unknown Artist";
            var newArtist = await GetOrCreateArtistAsync(sourceArtistName, ownerId, cancellationToken);

            // Get or create album
            MusicAlbum? newAlbum = null;
            if (sourceTrack.AlbumId.HasValue && sourceTrack.Album is not null)
            {
                newAlbum = await GetOrCreateAlbumAsync(
                    sourceTrack.Album.Title, newArtist.Id, ownerId, sourceTrack.Album.Year, cancellationToken);

                // Copy album art from the source user's cached art
                if (!newAlbum.HasCoverArt && sourceTrack.Album.HasCoverArt)
                {
                    var artPath = _albumArtService.CopyArtFromExisting(
                        _artCacheDir, sourceTrack.Album.Id, newAlbum.Id);
                    if (artPath is not null)
                    {
                        newAlbum.HasCoverArt = true;
                        newAlbum.CoverArtPath = artPath;
                    }
                }
            }

            // Get or create genre
            Genre? newGenre = null;
            var sourceGenre = sourceTrack.TrackGenres.FirstOrDefault()?.Genre;
            if (sourceGenre is not null)
                newGenre = await GetOrCreateGenreAsync(sourceGenre.Name, cancellationToken);

            // Create track record
            var newTrack = new Track
            {
                FileNodeId = fileNodeId,
                OwnerId = ownerId,
                Title = sourceTrack.Title,
                FileName = sourceTrack.FileName,
                MimeType = sourceTrack.MimeType,
                TrackNumber = sourceTrack.TrackNumber,
                DiscNumber = sourceTrack.DiscNumber,
                DurationTicks = sourceTrack.DurationTicks,
                SizeBytes = sourceTrack.SizeBytes,
                Bitrate = sourceTrack.Bitrate,
                SampleRate = sourceTrack.SampleRate,
                Channels = sourceTrack.Channels,
                AlbumId = newAlbum?.Id,
                Year = sourceTrack.Year,
                ContentHash = contentHash,
                MusicBrainzRecordingId = sourceTrack.MusicBrainzRecordingId,
            };
            newTracks.Add(newTrack);

            // Track-artist junction
            newTrackArtists.Add(new TrackArtist
            {
                TrackId = newTrack.Id,
                ArtistId = newArtist.Id,
                IsPrimary = true
            });

            // Track-genre junction
            if (newGenre is not null)
            {
                newTrackGenres.Add(new TrackGenre
                {
                    TrackId = newTrack.Id,
                    GenreId = newGenre.Id
                });
            }

            // Update album duration incrementally
            if (newAlbum is not null)
            {
                if (!_albumDurationCache.TryGetValue(newAlbum.Id, out var currentDuration))
                {
                    currentDuration = await _db.Tracks
                        .Where(t => t.AlbumId == newAlbum.Id)
                        .SumAsync(t => t.DurationTicks, cancellationToken);
                }
                currentDuration += newTrack.DurationTicks;
                _albumDurationCache[newAlbum.Id] = currentDuration;
                newAlbum.TotalDurationTicks = currentDuration;
            }

            copiedIds.Add(fileNodeId);
        }

        if (newTracks.Count == 0)
            return copiedIds;

        // Step 6: Single bulk save for ALL files at once
        _db.Tracks.AddRange(newTracks);
        _db.TrackArtists.AddRange(newTrackArtists);
        _db.TrackGenres.AddRange(newTrackGenres);
        await _db.SaveChangesAsync(cancellationToken);

        // Step 7: Publish search index events
        foreach (var track in newTracks)
        {
            await _eventBus.PublishAsync(new SearchIndexRequestEvent
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                ModuleId = "music",
                EntityId = track.Id.ToString(),
                Action = SearchIndexAction.Index
            }, new CallerContext(ownerId, ["user"], CallerType.User), cancellationToken);
        }

        _logger.LogInformation(
            "Bulk cross-owner copy: indexed {Count} tracks for owner {OwnerId} from {SourceCount} unique source tracks",
            newTracks.Count, ownerId, sourceByHash.Count);

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

            var existingTrack = await _db.Tracks
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.FileNodeId == file.FileNodeId && t.OwnerId == ownerId, cancellationToken);

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

        var totalTracks = await _db.Tracks.CountAsync(t => t.OwnerId == ownerId, cancellationToken);
        var totalArtists = await _db.Artists.CountAsync(a => a.OwnerId == ownerId, cancellationToken);
        var totalAlbums = await _db.Albums.CountAsync(a => a.OwnerId == ownerId, cancellationToken);

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
            EventId = Guid.NewGuid(),
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
    /// Gets or creates an artist by name for a specific owner.
    /// </summary>
    internal async Task<Artist> GetOrCreateArtistAsync(string name, Guid ownerId, CancellationToken cancellationToken)
    {
        var key = (name, ownerId);
        if (_artistCache.TryGetValue(key, out var cached))
            return cached;

        var artist = await _db.Artists
            .FirstOrDefaultAsync(a => a.OwnerId == ownerId && a.Name == name, cancellationToken);

        if (artist is not null)
        {
            _artistCache[key] = artist;
            return artist;
        }

        artist = new Artist
        {
            Name = name,
            OwnerId = ownerId,
            SortName = GenerateSortName(name)
        };
        _db.Artists.Add(artist);
        _artistCache[key] = artist;
        // Note: SaveChangesAsync is deferred to the caller for batching.
        return artist;
    }

    /// <summary>
    /// Gets or creates an album by title for a specific artist and owner.
    /// </summary>
    internal async Task<MusicAlbum> GetOrCreateAlbumAsync(string title, Guid artistId, Guid ownerId, int? year, CancellationToken cancellationToken)
    {
        var key = (title, artistId, ownerId);
        if (_albumCache.TryGetValue(key, out var cached))
            return cached;

        var album = await _db.Albums
            .FirstOrDefaultAsync(a => a.ArtistId == artistId && a.Title == title && a.OwnerId == ownerId, cancellationToken);

        if (album is not null)
        {
            _albumCache[key] = album;
            return album;
        }

        album = new MusicAlbum
        {
            Title = title,
            ArtistId = artistId,
            OwnerId = ownerId,
            Year = year
        };
        _db.Albums.Add(album);
        _albumCache[key] = album;
        // Note: SaveChangesAsync is deferred to the caller for batching.
        return album;
    }

    /// <summary>
    /// Gets or creates a genre by name.
    /// </summary>
    internal async Task<Genre> GetOrCreateGenreAsync(string name, CancellationToken cancellationToken)
    {
        if (_genreCache.TryGetValue(name, out var cached))
            return cached;

        var genre = await _db.Genres
            .FirstOrDefaultAsync(g => g.Name == name, cancellationToken);

        if (genre is not null)
        {
            _genreCache[name] = genre;
            return genre;
        }

        genre = new Genre { Name = name };
        _db.Genres.Add(genre);
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

        return false;
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
    /// Returns all distinct ContentHashes from any other user's tracks.
    /// Used by the scanner to pre-resolve cross-owner matches without enumerating files.
    /// </summary>
    public async Task<HashSet<string>> GetExistingContentHashesAsync(CancellationToken cancellationToken = default)
    {
        var hashes = await _db.Tracks
            .IgnoreQueryFilters()
            .Where(t => t.ContentHash != null && !t.IsDeleted)
            .Select(t => t.ContentHash!)
            .Distinct()
            .ToListAsync(cancellationToken);
        return [.. hashes];
    }

    /// <summary>
    /// Clones another user's entire music library into the current user in a single batch.
    /// No file discovery, no tree walk, no per-file processing — just copies all track
    /// metadata where the FileNodeId matches (admin shared folders use shared virtual IDs).
    /// Skips tracks already indexed for the current user.
    /// </summary>
    public async Task<int> CloneLibraryFromExistingAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        // Get all source tracks from any other user
        var sourceTracks = await _db.Tracks
            .Include(t => t.Album)
            .Include(t => t.TrackArtists).ThenInclude(ta => ta.Artist)
            .Include(t => t.TrackGenres).ThenInclude(tg => tg.Genre)
            .IgnoreQueryFilters()
            .Where(t => t.OwnerId != ownerId && !t.IsDeleted)
            .ToListAsync(cancellationToken);

        if (sourceTracks.Count == 0)
            return 0;

        // Get already-indexed FileNodeIds for this user
        var existingFileNodeIds = await _db.Tracks
            .Where(t => t.OwnerId == ownerId && !t.IsDeleted)
            .Select(t => t.FileNodeId)
            .ToListAsync(cancellationToken);
        var existingSet = existingFileNodeIds.ToHashSet();

        // Build all records in memory
        var newTracks = new List<Track>();
        var newTrackArtists = new List<TrackArtist>();
        var newTrackGenres = new List<TrackGenre>();
        var processed = 0;

        foreach (var sourceTrack in sourceTracks)
        {
            if (existingSet.Contains(sourceTrack.FileNodeId))
                continue;

            // Get or create artist (uses in-memory cache)
            var sourceArtistName = sourceTrack.TrackArtists
                .FirstOrDefault(ta => ta.IsPrimary)?.Artist?.Name
                ?? sourceTrack.TrackArtists.FirstOrDefault()?.Artist?.Name
                ?? "Unknown Artist";
            var newArtist = await GetOrCreateArtistAsync(sourceArtistName, ownerId, cancellationToken);

            // Get or create album
            MusicAlbum? newAlbum = null;
            if (sourceTrack.AlbumId.HasValue && sourceTrack.Album is not null)
            {
                newAlbum = await GetOrCreateAlbumAsync(
                    sourceTrack.Album.Title, newArtist.Id, ownerId, sourceTrack.Album.Year, cancellationToken);

                // Copy album art
                if (!newAlbum.HasCoverArt && sourceTrack.Album.HasCoverArt)
                {
                    var artPath = _albumArtService.CopyArtFromExisting(
                        _artCacheDir, sourceTrack.Album.Id, newAlbum.Id);
                    if (artPath is not null)
                    {
                        newAlbum.HasCoverArt = true;
                        newAlbum.CoverArtPath = artPath;
                    }
                }
            }

            // Get or create genre
            Genre? newGenre = null;
            var sourceGenre = sourceTrack.TrackGenres.FirstOrDefault()?.Genre;
            if (sourceGenre is not null)
                newGenre = await GetOrCreateGenreAsync(sourceGenre.Name, cancellationToken);

            // Create track
            var newTrack = new Track
            {
                FileNodeId = sourceTrack.FileNodeId,
                OwnerId = ownerId,
                Title = sourceTrack.Title,
                FileName = sourceTrack.FileName,
                MimeType = sourceTrack.MimeType,
                TrackNumber = sourceTrack.TrackNumber,
                DiscNumber = sourceTrack.DiscNumber,
                DurationTicks = sourceTrack.DurationTicks,
                SizeBytes = sourceTrack.SizeBytes,
                Bitrate = sourceTrack.Bitrate,
                SampleRate = sourceTrack.SampleRate,
                Channels = sourceTrack.Channels,
                AlbumId = newAlbum?.Id,
                Year = sourceTrack.Year,
                ContentHash = sourceTrack.ContentHash,
                MusicBrainzRecordingId = sourceTrack.MusicBrainzRecordingId,
            };
            newTracks.Add(newTrack);

            newTrackArtists.Add(new TrackArtist
            {
                TrackId = newTrack.Id,
                ArtistId = newArtist.Id,
                IsPrimary = true
            });

            if (newGenre is not null)
            {
                newTrackGenres.Add(new TrackGenre
                {
                    TrackId = newTrack.Id,
                    GenreId = newGenre.Id
                });
            }

            // Update album duration incrementally
            if (newAlbum is not null)
            {
                if (!_albumDurationCache.TryGetValue(newAlbum.Id, out var currentDuration))
                {
                    currentDuration = await _db.Tracks
                        .Where(t => t.AlbumId == newAlbum.Id)
                        .SumAsync(t => t.DurationTicks, cancellationToken);
                }
                currentDuration += newTrack.DurationTicks;
                _albumDurationCache[newAlbum.Id] = currentDuration;
                newAlbum.TotalDurationTicks = currentDuration;
            }

            processed++;
            existingSet.Add(sourceTrack.FileNodeId);
        }

        if (newTracks.Count == 0)
            return 0;

        _db.Tracks.AddRange(newTracks);
        _db.TrackArtists.AddRange(newTrackArtists);
        _db.TrackGenres.AddRange(newTrackGenres);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var track in newTracks)
        {
            await _eventBus.PublishAsync(new SearchIndexRequestEvent
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                ModuleId = "music",
                EntityId = track.Id.ToString(),
                Action = SearchIndexAction.Index
            }, new CallerContext(ownerId, ["user"], CallerType.User), cancellationToken);
        }

        _logger.LogInformation(
            "CloneLibraryFromExisting: cloned {Count} tracks for owner {OwnerId} from {SourceCount} source tracks",
            newTracks.Count, ownerId, sourceTracks.Count);

        return newTracks.Count;
    }

    /// <summary>
    /// Returns the set of FileNode IDs that are already indexed in the music library for the given owner.
    /// Only non-deleted tracks are returned; soft-deleted tracks are excluded so they can be re-indexed
    /// if the source file reappears.
    /// </summary>
    public async Task<HashSet<Guid>> GetIndexedFileNodeIdsAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var ids = await _db.Tracks
            .Where(t => t.OwnerId == ownerId)
            .Select(t => t.FileNodeId)
            .ToListAsync(cancellationToken);
        return [.. ids];
    }

    /// <summary>
    /// Soft-deletes Track records whose source FileNodes no longer exist, then removes any
    /// albums and artists that have zero remaining non-deleted tracks (orphan cleanup).
    /// </summary>
    /// <param name="deletedFileNodeIds">FileNode IDs whose backing files have been deleted.</param>
    /// <param name="ownerId">Owner user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of tracks soft-deleted.</returns>
    public async Task<int> SoftDeleteTracksAsync(IReadOnlyCollection<Guid> deletedFileNodeIds, Guid ownerId, CancellationToken cancellationToken = default)
    {
        if (deletedFileNodeIds.Count == 0)
            return 0;

        var now = DateTime.UtcNow;

        var tracksToDelete = await _db.Tracks
            .Where(t => t.OwnerId == ownerId && deletedFileNodeIds.Contains(t.FileNodeId) && !t.IsDeleted)
            .ToListAsync(cancellationToken);

        if (tracksToDelete.Count == 0)
            return 0;

        var affectedAlbumIds = tracksToDelete
            .Where(t => t.AlbumId.HasValue)
            .Select(t => t.AlbumId!.Value)
            .ToHashSet();

        // Soft-delete tracks and remove their junction rows
        var trackIds = tracksToDelete.Select(t => t.Id).ToHashSet();

        var trackArtists = await _db.TrackArtists
            .Where(ta => trackIds.Contains(ta.TrackId))
            .ToListAsync(cancellationToken);
        _db.TrackArtists.RemoveRange(trackArtists);

        var trackGenres = await _db.TrackGenres
            .Where(tg => trackIds.Contains(tg.TrackId))
            .ToListAsync(cancellationToken);
        _db.TrackGenres.RemoveRange(trackGenres);

        foreach (var track in tracksToDelete)
        {
            track.IsDeleted = true;
            track.DeletedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Remove orphaned albums: albums whose every track is now deleted
        foreach (var albumId in affectedAlbumIds)
        {
            var hasActiveTracks = await _db.Tracks
                .AnyAsync(t => t.AlbumId == albumId && !t.IsDeleted, cancellationToken);

            if (!hasActiveTracks)
            {
                var album = await _db.Albums.FindAsync([albumId], cancellationToken);
                if (album is not null)
                {
                    var artistId = album.ArtistId;
                    _db.Albums.Remove(album);
                    await _db.SaveChangesAsync(cancellationToken);

                    // Remove orphaned artist: artist with no remaining albums and no remaining active tracks
                    var hasActiveAlbums = await _db.Albums
                        .AnyAsync(a => a.ArtistId == artistId && a.OwnerId == ownerId, cancellationToken);
                    var hasActiveTracksDirect = await _db.TrackArtists
                        .AnyAsync(ta => ta.ArtistId == artistId, cancellationToken);

                    if (!hasActiveAlbums && !hasActiveTracksDirect)
                    {
                        var artist = await _db.Artists.FindAsync([artistId], cancellationToken);
                        if (artist is not null && artist.OwnerId == ownerId)
                        {
                            _db.Artists.Remove(artist);
                            await _db.SaveChangesAsync(cancellationToken);
                        }
                    }
                }
            }
            else
            {
                // Recalculate album total duration after track removal
                var album = await _db.Albums.FindAsync([albumId], cancellationToken);
                if (album is not null)
                {
                    album.TotalDurationTicks = await _db.Tracks
                        .Where(t => t.AlbumId == albumId && !t.IsDeleted)
                        .SumAsync(t => t.DurationTicks, cancellationToken);
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }
        }

        _logger.LogInformation(
            "Soft-deleted {Count} tracks for user {OwnerId} (source files removed from library)",
            tracksToDelete.Count, ownerId);

        // ── CROSS-OWNER AUDIT: Check if any other users still have tracks for these FileNodeIds ──
        if (tracksToDelete.Count > 0)
        {
            var affectedFileNodeIds = tracksToDelete.Select(t => t.FileNodeId).Distinct().ToList();
            var otherOwnerTracks = await _db.Tracks
                .IgnoreQueryFilters()
                .Where(t => affectedFileNodeIds.Contains(t.FileNodeId) && t.OwnerId != ownerId && !t.IsDeleted)
                .GroupBy(t => t.OwnerId)
                .Select(g => new { OwnerId = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            if (otherOwnerTracks.Count > 0)
            {
                var otherOwnerSummary = string.Join(", ", otherOwnerTracks.Select(o => $"{o.OwnerId}={o.Count}"));
                _logger.LogWarning(
                    "Cross-owner audit: {Count} tracks soft-deleted for owner {OwnerId}, but {OtherCount} other owners still have tracks for the same FileNodeIds: {Summary}",
                    tracksToDelete.Count, ownerId, otherOwnerTracks.Count, otherOwnerSummary);
            }
            else
            {
                _logger.LogDebug(
                    "Cross-owner audit: {Count} tracks soft-deleted for owner {OwnerId}, no other owners affected",
                    tracksToDelete.Count, ownerId);
            }
        }

        return tracksToDelete.Count;
    }

    /// <summary>
    /// Deletes all music library metadata for a specific owner (tracks, albums, artists, genres,
    /// play history, etc.) from the database. Does NOT delete the actual audio files — only the
    /// indexed metadata. After calling this, a re-scan will rebuild the library from scratch.
    /// Other users' libraries are NEVER affected.
    /// </summary>
    /// <param name="ownerId">Owner whose library will be reset.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ResetCollectionAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("RESET: Deleting music library metadata for owner {OwnerId}", ownerId);

        // Count tracks for audit trail (scoped to this owner)
        var trackCount = await _db.Tracks
            .IgnoreQueryFilters()
            .Where(t => t.OwnerId == ownerId)
            .CountAsync(cancellationToken);

        _logger.LogWarning("RESET: Deleting {TrackCount} tracks for owner {OwnerId}", trackCount, ownerId);

        // Delete in FK-safe order: junction/child tables first, then parents.
        // ALL deletes are scoped to ownerId — never touch other users' data.

        // Junction tables: filter by Track → OwnerId
        var ownedTrackIds = await _db.Tracks
            .IgnoreQueryFilters()
            .Where(t => t.OwnerId == ownerId)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);
        var ownedTrackIdSet = ownedTrackIds.ToHashSet();

        // PlaybackHistories: filter by owned tracks
        var ph = await _db.PlaybackHistories
            .IgnoreQueryFilters()
            .Where(h => ownedTrackIdSet.Contains(h.TrackId))
            .ToListAsync(cancellationToken);

        // ScrobbleRecords: filter by owned tracks
        var sr = await _db.ScrobbleRecords
            .IgnoreQueryFilters()
            .Where(s => ownedTrackIdSet.Contains(s.TrackId))
            .ToListAsync(cancellationToken);

        // StarredItems: filter by UserId (owner)
        var si = await _db.StarredItems
            .IgnoreQueryFilters()
            .Where(s => s.UserId == ownerId)
            .ToListAsync(cancellationToken);

        // PlaylistTracks: filter by owned tracks
        var pt = await _db.PlaylistTracks
            .IgnoreQueryFilters()
            .Where(pt => ownedTrackIdSet.Contains(pt.TrackId))
            .ToListAsync(cancellationToken);

        // TrackGenres: filter by owned tracks
        var tg = await _db.TrackGenres
            .IgnoreQueryFilters()
            .Where(tg => ownedTrackIdSet.Contains(tg.TrackId))
            .ToListAsync(cancellationToken);

        // TrackArtists: filter by owned tracks
        var ta = await _db.TrackArtists
            .IgnoreQueryFilters()
            .Where(ta => ownedTrackIdSet.Contains(ta.TrackId))
            .ToListAsync(cancellationToken);

        // Tracks: directly scoped to ownerId
        var tr = await _db.Tracks
            .IgnoreQueryFilters()
            .Where(t => t.OwnerId == ownerId)
            .ToListAsync(cancellationToken);

        // Playlists: scoped to ownerId (if applicable)
        var pl = await _db.Playlists
            .IgnoreQueryFilters()
            .Where(p => p.OwnerId == ownerId)
            .ToListAsync(cancellationToken);

        // Albums: scoped to ownerId
        var al = await _db.Albums
            .IgnoreQueryFilters()
            .Where(a => a.OwnerId == ownerId)
            .ToListAsync(cancellationToken);

        // Artists: scoped to ownerId
        var ar = await _db.Artists
            .IgnoreQueryFilters()
            .Where(a => a.OwnerId == ownerId)
            .ToListAsync(cancellationToken);

        // Genres: only delete if no tracks from ANY owner reference them
        var allRemainingGenreIds = await _db.TrackGenres
            .IgnoreQueryFilters()
            .Select(tg => tg.GenreId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var affectedGenreIds = tg.Select(tg => tg.GenreId).Distinct().ToHashSet();
        var orphanedGenreIds = affectedGenreIds
            .Where(gid => !allRemainingGenreIds.Except(affectedGenreIds).Contains(gid))
            .ToList();
        var ge = await _db.Genres
            .IgnoreQueryFilters()
            .Where(g => orphanedGenreIds.Contains(g.Id))
            .ToListAsync(cancellationToken);

        // Execute deletes
        _db.PlaybackHistories.RemoveRange(ph);
        _db.ScrobbleRecords.RemoveRange(sr);
        _db.StarredItems.RemoveRange(si);
        _db.PlaylistTracks.RemoveRange(pt);
        _db.TrackGenres.RemoveRange(tg);
        _db.TrackArtists.RemoveRange(ta);
        _db.Tracks.RemoveRange(tr);
        _db.Playlists.RemoveRange(pl);
        _db.Albums.RemoveRange(al);
        _db.Artists.RemoveRange(ar);
        _db.Genres.RemoveRange(ge);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("RESET complete for owner {OwnerId}: {TrackCount} tracks, {AlbumCount} albums, {ArtistCount} artists, {GenreCount} genres, {PHCount} playback histories",
            ownerId, tr.Count, al.Count, ar.Count, ge.Count, ph.Count);
    }
}
