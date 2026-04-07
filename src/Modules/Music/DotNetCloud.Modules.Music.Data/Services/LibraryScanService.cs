using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<LibraryScanService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryScanService"/> class.
    /// </summary>
    public LibraryScanService(
        MusicDbContext db,
        MusicMetadataService metadataService,
        AlbumArtService albumArtService,
        IEventBus eventBus,
        ILogger<LibraryScanService> logger)
    {
        _db = db;
        _metadataService = metadataService;
        _albumArtService = albumArtService;
        _eventBus = eventBus;
        _logger = logger;
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
        string? artCacheDir = null,
        CancellationToken cancellationToken = default)
    {
        // Check if already indexed
        var existing = await _db.Tracks
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.FileNodeId == fileNodeId, cancellationToken);

        if (existing is not null && !existing.IsDeleted)
        {
            _logger.LogDebug("File {FileNodeId} already indexed as track {TrackId}", fileNodeId, existing.Id);
            return existing;
        }

        // Extract metadata: prefer stream (reassembled from chunks) → file path → filename fallback.
        AudioMetadata? metadata = null;
        if (audioStream is not null)
        {
            metadata = _metadataService.ExtractMetadata(audioStream, mimeType, fileName);
        }

        if (metadata is null && metadataFilePath is not null)
        {
            metadata = _metadataService.ExtractMetadata(metadataFilePath);
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
        if (!album.HasCoverArt && artCacheDir is not null)
        {
            string? artPath = null;
            if (audioStream is not null && audioStream.CanSeek)
            {
                audioStream.Position = 0;
                artPath = _albumArtService.ExtractAndCacheArt(audioStream, mimeType, fileName, artCacheDir, album.Id);
            }
            else if (metadataFilePath is not null)
            {
                artPath = _albumArtService.ExtractAndCacheArt(metadataFilePath, artCacheDir, album.Id);
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

        return track;
    }

    /// <summary>
    /// Performs a full library scan for a user, indexing all audio files found at the given paths.
    /// </summary>
    public async Task<LibraryScanResultDto> ScanLibraryAsync(
        IEnumerable<(Guid FileNodeId, string FilePath, string MimeType, long SizeBytes)> audioFiles,
        Guid ownerId,
        CallerContext caller,
        string? artCacheDir = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var added = 0;
        var updated = 0;

        foreach (var file in audioFiles)
        {
            var existingTrack = await _db.Tracks
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.FileNodeId == file.FileNodeId, cancellationToken);

            var track = await IndexFileAsync(
                file.FileNodeId, file.FilePath, file.MimeType, file.SizeBytes, ownerId,
                metadataFilePath: file.FilePath, artCacheDir: artCacheDir, cancellationToken: cancellationToken);

            if (track is not null)
            {
                if (existingTrack is null)
                    added++;
                else
                    updated++;
            }
        }

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
            Duration = DateTime.UtcNow - startTime
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
    /// Deletes all music library metadata (tracks, albums, artists, genres, play history, etc.)
    /// from the database. Does NOT delete the actual audio files — only the indexed metadata.
    /// After calling this, a re-scan will rebuild the entire library from scratch.
    /// </summary>
    public async Task ResetCollectionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resetting music collection — deleting all metadata");

        // Delete in FK-safe order: junction/child tables first, then parents.
        // Use IgnoreQueryFilters to include soft-deleted records.
        _db.PlaybackHistories.RemoveRange(await _db.PlaybackHistories.IgnoreQueryFilters().ToListAsync(cancellationToken));
        _db.ScrobbleRecords.RemoveRange(await _db.ScrobbleRecords.IgnoreQueryFilters().ToListAsync(cancellationToken));
        _db.StarredItems.RemoveRange(await _db.StarredItems.IgnoreQueryFilters().ToListAsync(cancellationToken));
        _db.PlaylistTracks.RemoveRange(await _db.PlaylistTracks.IgnoreQueryFilters().ToListAsync(cancellationToken));
        _db.TrackGenres.RemoveRange(await _db.TrackGenres.IgnoreQueryFilters().ToListAsync(cancellationToken));
        _db.TrackArtists.RemoveRange(await _db.TrackArtists.IgnoreQueryFilters().ToListAsync(cancellationToken));
        _db.Tracks.RemoveRange(await _db.Tracks.IgnoreQueryFilters().ToListAsync(cancellationToken));
        _db.Playlists.RemoveRange(await _db.Playlists.IgnoreQueryFilters().ToListAsync(cancellationToken));
        _db.Albums.RemoveRange(await _db.Albums.IgnoreQueryFilters().ToListAsync(cancellationToken));
        _db.Artists.RemoveRange(await _db.Artists.IgnoreQueryFilters().ToListAsync(cancellationToken));
        _db.Genres.RemoveRange(await _db.Genres.IgnoreQueryFilters().ToListAsync(cancellationToken));

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Music collection reset complete");
    }
}
