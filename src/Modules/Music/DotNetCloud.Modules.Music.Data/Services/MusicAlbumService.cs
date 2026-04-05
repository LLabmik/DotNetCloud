using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Music.Models;
using DotNetCloud.Modules.Music.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Music.Data.Services;

/// <summary>
/// Service for managing music albums — browse, search, album tracks, album art.
/// </summary>
public sealed class MusicAlbumService : IMusicAlbumService
{
    private readonly MusicDbContext _db;
    private readonly ILogger<MusicAlbumService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MusicAlbumService"/> class.
    /// </summary>
    public MusicAlbumService(MusicDbContext db, ILogger<MusicAlbumService> logger)
    {
        _db = db;
        _logger = logger;
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
            .Where(a => a.OwnerId == caller.UserId && a.Title.Contains(query))
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
    /// Gets the cover art path for an album.
    /// </summary>
    public async Task<string?> GetCoverArtPathAsync(Guid albumId, CancellationToken cancellationToken = default)
    {
        var album = await _db.Albums
            .FirstOrDefaultAsync(a => a.Id == albumId, cancellationToken);
        return album?.CoverArtPath;
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
