using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Storage;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.Modules.Music.Models;
using DotNetCloud.Modules.Music.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Music.Data.Services;

/// <summary>
/// Service for managing music albums — browse, search, album tracks, album art.
/// Uses UserAlbum junction + CanonicalAlbum (canonical/shared) tables.
/// </summary>
public sealed class MusicAlbumService : IMusicAlbumService
{
    private readonly MusicDbContext _db;
    private readonly AlbumArtService _albumArtService;
    private readonly IDownloadService _downloadService;
    private readonly ContentAddressedStorage _contentStorage;
    private readonly ILogger<MusicAlbumService> _logger;

    public MusicAlbumService(
        MusicDbContext db,
        AlbumArtService albumArtService,
        IDownloadService downloadService,
        ContentAddressedStorage contentStorage,
        IConfiguration configuration,
        ILogger<MusicAlbumService> logger)
    {
        _db = db;
        _albumArtService = albumArtService;
        _downloadService = downloadService;
        _contentStorage = contentStorage;
        _logger = logger;
    }

    public async Task<MusicAlbumDto?> GetAlbumAsync(Guid albumId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var userAlbum = await _db.UserAlbums
            .Include(ua => ua.CanonicalAlbum)
            .FirstOrDefaultAsync(ua => ua.CanonicalAlbumId == albumId && ua.OwnerId == caller.UserId, cancellationToken);
        return userAlbum is null ? null : await MapToDtoAsync(userAlbum, caller.UserId, cancellationToken);
    }

    public async Task<int> GetCountAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        return await _db.UserAlbums.CountAsync(ua => ua.OwnerId == ownerId, cancellationToken);
    }

    public async Task<IReadOnlyList<MusicAlbumDto>> ListAlbumsAsync(CallerContext caller, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var userAlbums = await _db.UserAlbums
            .Include(ua => ua.CanonicalAlbum)
            .Where(ua => ua.OwnerId == caller.UserId)
            .OrderBy(ua => ua.CanonicalAlbum!.Title)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        var result = new List<MusicAlbumDto>(userAlbums.Count);
        foreach (var ua in userAlbums)
            result.Add(await MapToDtoAsync(ua, caller.UserId, cancellationToken));
        return result;
    }

    public async Task<IReadOnlyList<MusicAlbumDto>> ListAlbumsByArtistAsync(Guid artistId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var userAlbums = await _db.UserAlbums
            .Include(ua => ua.CanonicalAlbum)
            .Where(ua => ua.OwnerId == caller.UserId &&
                _db.CanonicalAlbumArtists.Any(caa => caa.AlbumId == ua.CanonicalAlbumId && caa.ArtistId == artistId))
            .OrderBy(ua => ua.CanonicalAlbum!.Year)
            .ThenBy(ua => ua.CanonicalAlbum!.Title)
            .ToListAsync(cancellationToken);
        var result = new List<MusicAlbumDto>(userAlbums.Count);
        foreach (var ua in userAlbums)
            result.Add(await MapToDtoAsync(ua, caller.UserId, cancellationToken));
        return result;
    }

    public async Task<IReadOnlyList<MusicAlbumDto>> SearchAsync(CallerContext caller, string query, int maxResults = 20, CancellationToken cancellationToken = default)
    {
        var queryLower = query.ToLowerInvariant();
        var userAlbums = await _db.UserAlbums
            .Include(ua => ua.CanonicalAlbum)
            .Where(ua => ua.OwnerId == caller.UserId && ua.CanonicalAlbum!.Title.ToLower().Contains(queryLower))
            .OrderBy(ua => ua.CanonicalAlbum!.Title)
            .Take(maxResults)
            .ToListAsync(cancellationToken);
        var result = new List<MusicAlbumDto>(userAlbums.Count);
        foreach (var ua in userAlbums)
            result.Add(await MapToDtoAsync(ua, caller.UserId, cancellationToken));
        return result;
    }

    public async Task<IReadOnlyList<MusicAlbumDto>> GetRecentAlbumsAsync(CallerContext caller, int count = 20, CancellationToken cancellationToken = default)
    {
        var userAlbums = await _db.UserAlbums
            .Include(ua => ua.CanonicalAlbum)
            .Where(ua => ua.OwnerId == caller.UserId)
            .OrderByDescending(ua => ua.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
        var result = new List<MusicAlbumDto>(userAlbums.Count);
        foreach (var ua in userAlbums)
            result.Add(await MapToDtoAsync(ua, caller.UserId, cancellationToken));
        return result;
    }

    public async Task DeleteAlbumAsync(Guid albumId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var userAlbum = await _db.UserAlbums
            .FirstOrDefaultAsync(ua => ua.CanonicalAlbumId == albumId && ua.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.MusicAlbumNotFound, "Album not found.");
        userAlbum.IsDeleted = true;
        userAlbum.DeletedAt = DateTime.UtcNow;
        userAlbum.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Album {AlbumId} soft-deleted by user {UserId}", albumId, caller.UserId);
    }

    public async Task<string?> GetCoverArtPathAsync(Guid albumId, CancellationToken cancellationToken = default)
    {
        var canonicalAlbum = await _db.CanonicalAlbums.FindAsync([albumId], cancellationToken);
        if (canonicalAlbum is null)
            return null;
        if (canonicalAlbum.CoverArtHash is not null && _contentStorage.Exists(canonicalAlbum.CoverArtHash))
            return canonicalAlbum.CoverArtHash;

        var anyUserTrack = await _db.UserTracks
            .Include(ut => ut.CanonicalTrack)
            .FirstOrDefaultAsync(ut => ut.CanonicalAlbumId == albumId && ut.FileNodeId != Guid.Empty, cancellationToken);
        if (anyUserTrack is null)
            return null;

        try
        {
            var caller = new CallerContext(anyUserTrack.OwnerId, [], CallerType.System);
            await using var stream = await _downloadService.DownloadCurrentAsync(anyUserTrack.FileNodeId, caller, cancellationToken);
            if (stream is null)
                return null;
            var artHash = _albumArtService.ExtractAndCacheArt(stream,
                anyUserTrack.CanonicalTrack?.MimeType ?? "audio/mpeg",
                anyUserTrack.CanonicalTrack?.Title ?? "Unknown");
            if (artHash is not null)
            {
                canonicalAlbum.HasCoverArt = true;
                canonicalAlbum.CoverArtHash = artHash;
                canonicalAlbum.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                return artHash;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to extract cover art for album {AlbumId}", albumId);
        }
        if (canonicalAlbum.HasCoverArt)
        {
            canonicalAlbum.HasCoverArt = false;
            canonicalAlbum.CoverArtHash = null;
            canonicalAlbum.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
        return null;
    }

    public async Task<IReadOnlyList<MusicAlbumDto>> GetStarredAlbumsAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var starredAlbumIds = await _db.StarredItems
            .Where(s => s.UserId == caller.UserId && s.ItemType == StarredItemType.Album)
            .OrderByDescending(s => s.StarredAt)
            .Select(s => s.ItemId)
            .ToListAsync(cancellationToken);
        if (starredAlbumIds.Count == 0)
            return [];
        var userAlbums = await _db.UserAlbums
            .Include(ua => ua.CanonicalAlbum)
            .Where(ua => starredAlbumIds.Contains(ua.CanonicalAlbumId) && ua.OwnerId == caller.UserId)
            .ToListAsync(cancellationToken);
        var albumMap = userAlbums.ToDictionary(ua => ua.CanonicalAlbumId);
        var result = new List<MusicAlbumDto>(starredAlbumIds.Count);
        foreach (var id in starredAlbumIds)
        {
            if (albumMap.TryGetValue(id, out var ua))
                result.Add(await MapToDtoAsync(ua, caller.UserId, cancellationToken));
        }
        return result;
    }

    private async Task<MusicAlbumDto> MapToDtoAsync(UserAlbum userAlbum, Guid userId, CancellationToken cancellationToken)
    {
        var ca = userAlbum.CanonicalAlbum!;
        var primaryArtist = await _db.CanonicalAlbumArtists
            .Include(caa => caa.Artist)
            .Where(caa => caa.AlbumId == ca.Id && caa.IsPrimary)
            .Select(caa => caa.Artist)
            .FirstOrDefaultAsync(cancellationToken);
        var primaryGenre = await _db.CanonicalTrackGenres
            .Include(ctg => ctg.Genre)
            .Where(ctg => ctg.Track!.UserTracks.Any(ut => ut.CanonicalAlbumId == ca.Id))
            .GroupBy(ctg => ctg.Genre!.Name)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefaultAsync(cancellationToken);
        var trackCount = await _db.UserTracks
            .CountAsync(ut => ut.CanonicalAlbumId == ca.Id && ut.OwnerId == userId && !ut.IsDeleted, cancellationToken);
        var isStarred = await _db.StarredItems.AnyAsync(s =>
            s.UserId == userId && s.ItemType == StarredItemType.Album && s.ItemId == ca.Id, cancellationToken);
        return new MusicAlbumDto
        {
            Id = ca.Id,
            Title = ca.Title,
            ArtistId = primaryArtist?.Id ?? Guid.Empty,
            ArtistName = primaryArtist?.Name ?? "Unknown Artist",
            Year = ca.Year,
            Genre = primaryGenre,
            TrackCount = trackCount,
            TotalDuration = TimeSpan.FromTicks(ca.TotalDurationTicks),
            HasCoverArt = ca.HasCoverArt,
            IsStarred = isStarred,
            CreatedAt = userAlbum.CreatedAt
        };
    }
}
