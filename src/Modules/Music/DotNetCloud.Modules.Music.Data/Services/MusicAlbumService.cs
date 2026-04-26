using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.Modules.Music.Models;
using DotNetCloud.Modules.Music.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Music.Data.Services;

/// <summary>
/// Service for managing music albums — browse, search, album tracks, album art.
/// </summary>
public sealed class MusicAlbumService : IMusicAlbumService
{
    private readonly MusicDbContext _db;
    private readonly AlbumArtService _albumArtService;
    private readonly IDownloadService _downloadService;
    private readonly ILogger<MusicAlbumService> _logger;
    private readonly string _artCacheDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="MusicAlbumService"/> class.
    /// </summary>
    public MusicAlbumService(
        MusicDbContext db,
        AlbumArtService albumArtService,
        IDownloadService downloadService,
        IConfiguration configuration,
        ILogger<MusicAlbumService> logger)
    {
        _db = db;
        _albumArtService = albumArtService;
        _downloadService = downloadService;
        _logger = logger;
        var storageRoot = configuration["Files:Storage:RootPath"] ?? Path.GetTempPath();
        _artCacheDir = Path.Combine(storageRoot, ".album-art");
    }

    /// <summary>
    /// Gets an album by ID.
    /// </summary>
    public async Task<MusicAlbumDto?> GetAlbumAsync(Guid albumId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var album = await _db.Albums
            .Include(a => a.Artist)
            .Include(a => a.Tracks)
            .FirstOrDefaultAsync(a => a.Id == albumId && a.OwnerId == caller.UserId, cancellationToken);

        return album is null ? null : MapToDto(album, caller.UserId);
    }

    /// <summary>
    /// Gets the total album count for a user.
    /// </summary>
    public async Task<int> GetCountAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        return await _db.Albums.CountAsync(a => a.OwnerId == ownerId, cancellationToken);
    }

    /// <summary>
    /// Lists albums for the authenticated user.
    /// </summary>
    public async Task<IReadOnlyList<MusicAlbumDto>> ListAlbumsAsync(CallerContext caller, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var albums = await _db.Albums
            .Include(a => a.Artist)
            .Include(a => a.Tracks)
            .Where(a => a.OwnerId == caller.UserId)
            .OrderBy(a => a.Artist!.SortName ?? a.Artist!.Name)
            .ThenBy(a => a.Year)
            .ThenBy(a => a.Title)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return albums.Select(a => MapToDto(a, caller.UserId)).ToList();
    }

    /// <summary>
    /// Lists albums by a specific artist.
    /// </summary>
    public async Task<IReadOnlyList<MusicAlbumDto>> ListAlbumsByArtistAsync(Guid artistId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var albums = await _db.Albums
            .Include(a => a.Artist)
            .Include(a => a.Tracks)
            .Where(a => a.ArtistId == artistId && a.OwnerId == caller.UserId)
            .OrderBy(a => a.Year)
            .ThenBy(a => a.Title)
            .ToListAsync(cancellationToken);

        return albums.Select(a => MapToDto(a, caller.UserId)).ToList();
    }

    /// <summary>
    /// Searches albums by title.
    /// </summary>
    public async Task<IReadOnlyList<MusicAlbumDto>> SearchAsync(CallerContext caller, string query, int maxResults = 20, CancellationToken cancellationToken = default)
    {
        var albums = await _db.Albums
            .Include(a => a.Artist)
            .Include(a => a.Tracks)
            .Where(a => a.OwnerId == caller.UserId && a.Title.ToLower().Contains(query.ToLower()))
            .OrderBy(a => a.Title)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return albums.Select(a => MapToDto(a, caller.UserId)).ToList();
    }

    /// <summary>
    /// Gets recently added albums.
    /// </summary>
    public async Task<IReadOnlyList<MusicAlbumDto>> GetRecentAlbumsAsync(CallerContext caller, int count = 20, CancellationToken cancellationToken = default)
    {
        var albums = await _db.Albums
            .Include(a => a.Artist)
            .Include(a => a.Tracks)
            .Where(a => a.OwnerId == caller.UserId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

        return albums.Select(a => MapToDto(a, caller.UserId)).ToList();
    }

    /// <summary>
    /// Deletes an album (soft delete).
    /// </summary>
    public async Task DeleteAlbumAsync(Guid albumId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var album = await _db.Albums
            .FirstOrDefaultAsync(a => a.Id == albumId && a.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.MusicAlbumNotFound, "Album not found.");

        album.IsDeleted = true;
        album.DeletedAt = DateTime.UtcNow;
        album.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Album {AlbumId} soft-deleted by user {UserId}", albumId, caller.UserId);
    }

    /// <summary>
    /// Gets the cover art path for an album. Attempts on-demand extraction if not cached.
    /// </summary>
    public async Task<string?> GetCoverArtPathAsync(Guid albumId, CancellationToken cancellationToken = default)
    {
        var album = await _db.Albums
            .Include(a => a.Tracks)
            .FirstOrDefaultAsync(a => a.Id == albumId, cancellationToken);
        if (album is null) return null;

        // If we have a cached path and the file exists, return it
        if (album.CoverArtPath is not null && File.Exists(album.CoverArtPath))
            return album.CoverArtPath;

        // On-demand extraction: try to extract from one of the album's tracks
        var artPath = await ExtractCoverArtForAlbumAsync(album, cancellationToken);

        // If on-demand extraction also failed and the DB still claims we have art,
        // clear the stale state so the next enrichment run can re-fetch from MusicBrainz.
        if (artPath is null && album.HasCoverArt)
        {
            album.HasCoverArt = false;
            album.CoverArtPath = null;
            album.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Cleared stale cover art state for album {AlbumId} (file missing, extraction failed)", album.Id);
        }

        return artPath;
    }

    private async Task<string?> ExtractCoverArtForAlbumAsync(MusicAlbum album, CancellationToken cancellationToken)
    {
        var tracks = album.Tracks?.Where(t => t.FileNodeId != Guid.Empty).ToList()
            ?? await _db.Tracks.Where(t => t.AlbumId == album.Id && t.FileNodeId != Guid.Empty)
                .Take(3).ToListAsync(cancellationToken);

        if (tracks.Count == 0) return null;

        foreach (var track in tracks)
        {
            try
            {
                var caller = new CallerContext(album.OwnerId, [], CallerType.System);
                await using var stream = await _downloadService.DownloadCurrentAsync(track.FileNodeId, caller, cancellationToken);
                if (stream is null) continue;

                Directory.CreateDirectory(_artCacheDir);
                var artPath = _albumArtService.ExtractAndCacheArt(stream, track.MimeType, track.FileName, _artCacheDir, album.Id);
                if (artPath is not null)
                {
                    album.HasCoverArt = true;
                    album.CoverArtPath = artPath;
                    album.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("On-demand cover art extracted for album {AlbumId} from track {TrackId}", album.Id, track.Id);
                    return artPath;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to extract cover art from track {TrackId} for album {AlbumId}", track.Id, album.Id);
            }
        }

        return null;
    }

    /// <summary>
    /// Gets starred (favorited) albums for the current user.
    /// </summary>
    public async Task<IReadOnlyList<MusicAlbumDto>> GetStarredAlbumsAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var starredAlbumIds = await _db.StarredItems
            .Where(s => s.UserId == caller.UserId && s.ItemType == StarredItemType.Album)
            .OrderByDescending(s => s.StarredAt)
            .Select(s => s.ItemId)
            .ToListAsync(cancellationToken);

        if (starredAlbumIds.Count == 0)
            return [];

        var albums = await _db.Albums
            .Include(a => a.Artist)
            .Include(a => a.Tracks)
            .Where(a => starredAlbumIds.Contains(a.Id) && a.OwnerId == caller.UserId)
            .ToListAsync(cancellationToken);

        var albumMap = albums.ToDictionary(a => a.Id);
        return starredAlbumIds
            .Where(id => albumMap.ContainsKey(id))
            .Select(id => MapToDto(albumMap[id], caller.UserId))
            .ToList();
    }

    internal MusicAlbumDto MapToDto(MusicAlbum album, Guid userId)
    {
        var isStarred = _db.StarredItems.Any(s =>
            s.UserId == userId && s.ItemType == StarredItemType.Album && s.ItemId == album.Id);

        var primaryGenre = _db.TrackGenres
            .Include(tg => tg.Genre)
            .Where(tg => tg.Track!.AlbumId == album.Id)
            .GroupBy(tg => tg.Genre!.Name)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

        return new MusicAlbumDto
        {
            Id = album.Id,
            Title = album.Title,
            ArtistId = album.ArtistId,
            ArtistName = album.Artist?.Name ?? "Unknown Artist",
            Year = album.Year,
            Genre = primaryGenre,
            TrackCount = album.Tracks?.Count ?? 0,
            TotalDuration = TimeSpan.FromTicks(album.TotalDurationTicks),
            HasCoverArt = album.HasCoverArt,
            IsStarred = isStarred,
            CreatedAt = album.CreatedAt
        };
    }
}
