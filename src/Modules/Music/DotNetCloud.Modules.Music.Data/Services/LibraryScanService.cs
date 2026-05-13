using DotNetCloud.Core.Authorization;
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
    private readonly string _artCacheDir;
    private readonly bool _autoFetchArt;
    private readonly bool _autoEnrichArtists;

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
        IMetadataEnrichmentService? enrichmentService = null)
    {
        _db = db;
        _metadataService = metadataService;
        _albumArtService = albumArtService;
        _eventBus = eventBus;
        _enrichmentService = enrichmentService;
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

        if (sourceTrack is not null)
        {
            _logger.LogInformation(
                "Cross-owner copy ({Strategy}): cloning metadata from track {SourceTrackId} (owner={SourceOwnerId}) for FileNode {FileNodeId} into owner {OwnerId}",
                copyStrategy, sourceTrack.Id, sourceTrack.OwnerId, fileNodeId, ownerId);

            // ── SAFETY: Never touch sourceTrack or anything owned by sourceTrack.OwnerId ──
            // All new records below use ownerId (the CURRENT user), never sourceTrack.OwnerId.

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
            await _db.SaveChangesAsync(cancellationToken);

            // Fill in ContentHash for Strategy 1 (not yet known at query time)
            if (newTrack.ContentHash is null)
            {
                newTrack.ContentHash = await LookupContentHashAsync(fileNodeId, cancellationToken);
                if (newTrack.ContentHash is not null)
                    await _db.SaveChangesAsync(cancellationToken);
            }

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

            // Update NEW album total duration (only counts tracks owned by current user)
            if (newAlbum is not null)
            {
                newAlbum.TotalDurationTicks = await _db.Tracks
                    .Where(t => t.AlbumId == newAlbum.Id)
                    .SumAsync(t => t.DurationTicks, cancellationToken);
            }

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

        // ── Extract metadata from the file ──
        AudioMetadata? metadata = null;
        if (audioStream is not null)
        {
            metadata = _metadataService.ExtractMetadata(audioStream, mimeType, fileName);
        }

        if (metadata is null && metadataFilePath is not null)
        {
            metadata = _metadataService.ExtractMetadata(metadataFilePath);
        }

        // Filepath fallback: if TagLib# produced garbage, merge with Artist/Album/Track
        // parsed from directory structure when available.
        if (metadata is not null && metadataFilePath is not null)
        {
            var parsed = TryParseMetadataFromPath(metadataFilePath, fileName);
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
                fileName, audioStream is not null, metadataFilePath);
            metadata = new AudioMetadata
            {
                Title = Path.GetFileNameWithoutExtension(fileName),
                Artist = "Unknown Artist",
                Album = "Unknown Album",
                DurationTicks = 0
            };
        }

        // Get or create artist
        var artist = await GetOrCreateArtistAsync(metadata.AlbumArtist ?? metadata.Artist, ownerId, cancellationToken);

        // Get or create album
        var album = await GetOrCreateAlbumAsync(metadata.Album, artist.Id, ownerId, metadata.Year, cancellationToken);

        // Handle album art
        if (!album.HasCoverArt)
        {
            string? artPath = null;
            if (audioStream is not null && audioStream.CanSeek)
            {
                audioStream.Position = 0;
                artPath = _albumArtService.ExtractAndCacheArt(audioStream, mimeType, fileName, _artCacheDir, album.Id);
            }
            else if (metadataFilePath is not null)
            {
                artPath = _albumArtService.ExtractAndCacheArt(metadataFilePath, _artCacheDir, album.Id);
            }

            if (artPath is not null)
            {
                album.HasCoverArt = true;
                album.CoverArtPath = artPath;
            }
        }

        // Get or create genre
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

        await _db.SaveChangesAsync(cancellationToken);

        // Store ContentHash for future cross-owner lookup
        track.ContentHash = await LookupContentHashAsync(fileNodeId, cancellationToken);
        if (track.ContentHash is not null)
            await _db.SaveChangesAsync(cancellationToken);

        // Create track-artist association
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

        // Update album total duration
        album.TotalDurationTicks = await _db.Tracks
            .Where(t => t.AlbumId == album.Id)
            .SumAsync(t => t.DurationTicks, cancellationToken);

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
        var artist = await _db.Artists
            .FirstOrDefaultAsync(a => a.OwnerId == ownerId && a.Name == name, cancellationToken);

        if (artist is not null)
            return artist;

        artist = new Artist
        {
            Name = name,
            OwnerId = ownerId,
            SortName = GenerateSortName(name)
        };
        _db.Artists.Add(artist);
        await _db.SaveChangesAsync(cancellationToken);
        return artist;
    }

    /// <summary>
    /// Gets or creates an album by title for a specific artist and owner.
    /// </summary>
    internal async Task<MusicAlbum> GetOrCreateAlbumAsync(string title, Guid artistId, Guid ownerId, int? year, CancellationToken cancellationToken)
    {
        var album = await _db.Albums
            .FirstOrDefaultAsync(a => a.ArtistId == artistId && a.Title == title && a.OwnerId == ownerId, cancellationToken);

        if (album is not null)
            return album;

        album = new MusicAlbum
        {
            Title = title,
            ArtistId = artistId,
            OwnerId = ownerId,
            Year = year
        };
        _db.Albums.Add(album);
        await _db.SaveChangesAsync(cancellationToken);
        return album;
    }

    /// <summary>
    /// Gets or creates a genre by name.
    /// </summary>
    internal async Task<Genre> GetOrCreateGenreAsync(string name, CancellationToken cancellationToken)
    {
        var genre = await _db.Genres
            .FirstOrDefaultAsync(g => g.Name == name, cancellationToken);

        if (genre is not null)
            return genre;

        genre = new Genre { Name = name };
        _db.Genres.Add(genre);
        await _db.SaveChangesAsync(cancellationToken);
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
            return await _db.Database
                .SqlQueryRaw<string>(@"SELECT ""ContentHash"" AS ""Value"" FROM core.""FileNodes"" WHERE ""Id"" = {0}", fileNodeId)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not look up ContentHash for FileNode {FileNodeId}", fileNodeId);
            return null;
        }
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
    /// Deletes all music library metadata (tracks, albums, artists, genres, play history, etc.)
    /// from the database. Does NOT delete the actual audio files — only the indexed metadata.
    /// After calling this, a re-scan will rebuild the entire library from scratch.
    /// </summary>
    public async Task ResetCollectionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("RESET: Deleting ALL music library metadata for ALL users. This is a destructive operation.");

        // Count tracks per owner before deleting, for audit trail
        var tracksByOwner = await _db.Tracks
            .IgnoreQueryFilters()
            .GroupBy(t => t.OwnerId)
            .Select(g => new { OwnerId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var trackOwnerSummary = string.Join(", ", tracksByOwner.Select(o => $"{o.OwnerId}={o.Count}"));
        _logger.LogWarning("RESET: Deleting tracks for {OwnerCount} owners: {Summary}", tracksByOwner.Count, trackOwnerSummary);

        // Delete in FK-safe order: junction/child tables first, then parents.
        // Use IgnoreQueryFilters to include soft-deleted records.
        var ph = await _db.PlaybackHistories.IgnoreQueryFilters().ToListAsync(cancellationToken);
        _db.PlaybackHistories.RemoveRange(ph);
        var sr = await _db.ScrobbleRecords.IgnoreQueryFilters().ToListAsync(cancellationToken);
        _db.ScrobbleRecords.RemoveRange(sr);
        var si = await _db.StarredItems.IgnoreQueryFilters().ToListAsync(cancellationToken);
        _db.StarredItems.RemoveRange(si);
        var pt = await _db.PlaylistTracks.IgnoreQueryFilters().ToListAsync(cancellationToken);
        _db.PlaylistTracks.RemoveRange(pt);
        var tg = await _db.TrackGenres.IgnoreQueryFilters().ToListAsync(cancellationToken);
        _db.TrackGenres.RemoveRange(tg);
        var ta = await _db.TrackArtists.IgnoreQueryFilters().ToListAsync(cancellationToken);
        _db.TrackArtists.RemoveRange(ta);
        var tr = await _db.Tracks.IgnoreQueryFilters().ToListAsync(cancellationToken);
        _db.Tracks.RemoveRange(tr);
        var pl = await _db.Playlists.IgnoreQueryFilters().ToListAsync(cancellationToken);
        _db.Playlists.RemoveRange(pl);
        var al = await _db.Albums.IgnoreQueryFilters().ToListAsync(cancellationToken);
        _db.Albums.RemoveRange(al);
        var ar = await _db.Artists.IgnoreQueryFilters().ToListAsync(cancellationToken);
        _db.Artists.RemoveRange(ar);
        var ge = await _db.Genres.IgnoreQueryFilters().ToListAsync(cancellationToken);
        _db.Genres.RemoveRange(ge);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("RESET complete: deleted {TrackCount} tracks, {AlbumCount} albums, {ArtistCount} artists, {GenreCount} genres, {PHCount} playback histories, {PTCount} playlist tracks, {PLCount} playlists",
            tr.Count, al.Count, ar.Count, ge.Count, ph.Count, pt.Count, pl.Count);
    }
}
