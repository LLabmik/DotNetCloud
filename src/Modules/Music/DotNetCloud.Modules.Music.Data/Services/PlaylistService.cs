using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Music.Models;
using DotNetCloud.Modules.Music.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Music.Data.Services;

/// <summary>
/// Service for managing playlists — CRUD, reorder tracks, playlist sharing.
/// Uses UserTrack (canonical) instead of legacy Track.
/// </summary>
public sealed class PlaylistService : Music.Services.IPlaylistService
{
    private readonly MusicDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<PlaylistService> _logger;

    public PlaylistService(MusicDbContext db, IEventBus eventBus, ILogger<PlaylistService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<PlaylistDto> CreatePlaylistAsync(CreatePlaylistDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var playlist = new Playlist
        {
            OwnerId = caller.UserId,
            Name = dto.Name,
            Description = dto.Description,
            IsPublic = dto.IsPublic
        };
        _db.Playlists.Add(playlist);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Playlist {PlaylistId} '{Name}' created by user {UserId}", playlist.Id, playlist.Name, caller.UserId);
        await _eventBus.PublishAsync(new PlaylistCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            PlaylistId = playlist.Id,
            Name = playlist.Name,
            OwnerId = caller.UserId
        }, caller, cancellationToken);

        return MapToDto(playlist);
    }

    public async Task<PlaylistDto?> GetPlaylistAsync(Guid playlistId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var playlist = await _db.Playlists
            .Include(p => p.PlaylistTracks).ThenInclude(pt => pt.UserTrack).ThenInclude(ut => ut!.CanonicalTrack)
            .FirstOrDefaultAsync(p => p.Id == playlistId && (p.OwnerId == caller.UserId || p.IsPublic), cancellationToken);
        return playlist is null ? null : MapToDto(playlist);
    }

    public async Task<IReadOnlyList<PlaylistDto>> ListPlaylistsAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var playlists = await _db.Playlists
            .Include(p => p.PlaylistTracks).ThenInclude(pt => pt.UserTrack).ThenInclude(ut => ut!.CanonicalTrack)
            .Where(p => p.OwnerId == caller.UserId || p.IsPublic)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
        return playlists.Select(MapToDto).ToList();
    }

    public async Task<PlaylistDto> UpdatePlaylistAsync(Guid playlistId, UpdatePlaylistDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var playlist = await _db.Playlists
            .Include(p => p.PlaylistTracks).ThenInclude(pt => pt.UserTrack).ThenInclude(ut => ut!.CanonicalTrack)
            .FirstOrDefaultAsync(p => p.Id == playlistId && p.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.PlaylistNotFound, "Playlist not found.");
        if (dto.Name is not null)
            playlist.Name = dto.Name;
        if (dto.Description is not null)
            playlist.Description = dto.Description;
        if (dto.IsPublic.HasValue)
            playlist.IsPublic = dto.IsPublic.Value;
        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapToDto(playlist);
    }

    public async Task DeletePlaylistAsync(Guid playlistId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var playlist = await _db.Playlists
            .FirstOrDefaultAsync(p => p.Id == playlistId && p.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.PlaylistNotFound, "Playlist not found.");
        playlist.IsDeleted = true;
        playlist.DeletedAt = DateTime.UtcNow;
        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Playlist {PlaylistId} soft-deleted by user {UserId}", playlistId, caller.UserId);
    }

    public async Task AddTrackAsync(Guid playlistId, Guid trackId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var playlist = await _db.Playlists
            .FirstOrDefaultAsync(p => p.Id == playlistId && p.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.PlaylistNotFound, "Playlist not found.");

        var trackExists = await _db.UserTracks.AnyAsync(ut => ut.Id == trackId, cancellationToken);
        if (!trackExists)
            throw new BusinessRuleException(ErrorCodes.TrackNotFound, "Track not found.");

        var alreadyInPlaylist = await _db.PlaylistTracks
            .AnyAsync(pt => pt.PlaylistId == playlistId && pt.UserTrackId == trackId, cancellationToken);
        if (alreadyInPlaylist)
            throw new BusinessRuleException(ErrorCodes.TrackAlreadyInPlaylist, "Track is already in this playlist.");

        var maxOrder = await _db.PlaylistTracks
            .Where(pt => pt.PlaylistId == playlistId)
            .MaxAsync(pt => (int?)pt.SortOrder, cancellationToken) ?? -1;

        _db.PlaylistTracks.Add(new PlaylistTrack { PlaylistId = playlistId, UserTrackId = trackId, SortOrder = maxOrder + 1 });
        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddTrackRangeAsync(Guid playlistId, IReadOnlyList<Guid> trackIds, CallerContext caller, CancellationToken cancellationToken = default)
    {
        if (trackIds.Count == 0)
            return;

        var playlist = await _db.Playlists
            .FirstOrDefaultAsync(p => p.Id == playlistId && p.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.PlaylistNotFound, "Playlist not found.");

        var validTracks = await _db.UserTracks
            .Include(ut => ut.CanonicalAlbum)
            .Include(ut => ut.CanonicalTrack)
            .Where(ut => trackIds.Contains(ut.Id))
            .Select(ut => new { ut.Id, ut.CanonicalAlbumId, AlbumTitle = ut.CanonicalAlbum != null ? ut.CanonicalAlbum.Title : (string?)null, DiscNumber = ut.CanonicalTrack!.DiscNumber, TrackNumber = ut.CanonicalTrack.TrackNumber })
            .ToListAsync(cancellationToken);

        if (validTracks.Count == 0)
            return;

        var validTrackIds = validTracks.Select(t => t.Id).ToList();
        var alreadyInPlaylist = await _db.PlaylistTracks
            .Where(pt => pt.PlaylistId == playlistId && validTrackIds.Contains(pt.UserTrackId))
            .Select(pt => pt.UserTrackId)
            .ToListAsync(cancellationToken);

        var newTracks = validTracks
            .Where(t => !alreadyInPlaylist.Contains(t.Id))
            .OrderBy(t => t.AlbumTitle ?? "")
            .ThenBy(t => t.DiscNumber ?? 1)
            .ThenBy(t => t.TrackNumber ?? 0)
            .ToList();

        if (newTracks.Count == 0)
            return;

        var maxOrder = await _db.PlaylistTracks
            .Where(pt => pt.PlaylistId == playlistId)
            .MaxAsync(pt => (int?)pt.SortOrder, cancellationToken) ?? -1;

        var playlistTracks = new List<PlaylistTrack>(newTracks.Count);
        foreach (var track in newTracks)
        {
            maxOrder++;
            playlistTracks.Add(new PlaylistTrack { PlaylistId = playlistId, UserTrackId = track.Id, SortOrder = maxOrder });
        }
        _db.PlaylistTracks.AddRange(playlistTracks);
        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Added {Count} tracks to playlist {PlaylistId}", newTracks.Count, playlistId);
    }

    public async Task RemoveTrackAsync(Guid playlistId, Guid trackId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var playlist = await _db.Playlists
            .FirstOrDefaultAsync(p => p.Id == playlistId && p.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.PlaylistNotFound, "Playlist not found.");
        var playlistTrack = await _db.PlaylistTracks
            .FirstOrDefaultAsync(pt => pt.PlaylistId == playlistId && pt.UserTrackId == trackId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.TrackNotFound, "Track is not in this playlist.");
        _db.PlaylistTracks.Remove(playlistTrack);
        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TrackDto>> GetPlaylistTracksAsync(Guid playlistId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var playlist = await _db.Playlists
            .FirstOrDefaultAsync(p => p.Id == playlistId && (p.OwnerId == caller.UserId || p.IsPublic), cancellationToken);
        if (playlist is null)
            return [];

        var userTracks = await _db.PlaylistTracks
            .Include(pt => pt.UserTrack).ThenInclude(ut => ut!.CanonicalTrack).ThenInclude(ct => ct!.TrackArtists).ThenInclude(cta => cta.Artist)
            .Include(pt => pt.UserTrack).ThenInclude(ut => ut!.CanonicalTrack).ThenInclude(ct => ct!.TrackGenres).ThenInclude(ctg => ctg.Genre)
            .Include(pt => pt.UserTrack).ThenInclude(ut => ut!.CanonicalAlbum)
            .Where(pt => pt.PlaylistId == playlistId)
            .OrderBy(pt => pt.SortOrder)
            .Select(pt => pt.UserTrack!)
            .ToListAsync(cancellationToken);

        return userTracks.Select(ut => MapTrackToDto(ut, caller.UserId)).ToList();
    }

    private PlaylistDto MapToDto(Playlist playlist)
    {
        var totalDurationTicks = playlist.PlaylistTracks?
            .Sum(pt => pt.UserTrack?.CanonicalTrack?.DurationTicks ?? 0) ?? 0;
        return new PlaylistDto
        {
            Id = playlist.Id,
            OwnerId = playlist.OwnerId,
            Name = playlist.Name,
            Description = playlist.Description,
            IsPublic = playlist.IsPublic,
            TrackCount = playlist.PlaylistTracks?.Count ?? 0,
            TotalDuration = TimeSpan.FromTicks(totalDurationTicks),
            CreatedAt = playlist.CreatedAt,
            UpdatedAt = playlist.UpdatedAt
        };
    }

    private TrackDto MapTrackToDto(UserTrack userTrack, Guid userId)
    {
        var ct = userTrack.CanonicalTrack;
        var primaryArtist = ct?.TrackArtists.FirstOrDefault(cta => cta.IsPrimary)?.Artist ?? ct?.TrackArtists.FirstOrDefault()?.Artist;
        var isStarred = _db.StarredItems.Any(s => s.UserId == userId && s.ItemType == StarredItemType.Track && s.ItemId == userTrack.Id);
        return new TrackDto
        {
            Id = userTrack.Id,
            FileNodeId = userTrack.FileNodeId,
            Title = ct?.Title ?? "Unknown",
            TrackNumber = ct?.TrackNumber,
            DiscNumber = ct?.DiscNumber,
            Duration = TimeSpan.FromTicks(ct?.DurationTicks ?? 0),
            SizeBytes = 0,
            Bitrate = ct?.Bitrate,
            MimeType = ct?.MimeType ?? "audio/mpeg",
            AlbumId = userTrack.CanonicalAlbumId,
            AlbumTitle = userTrack.CanonicalAlbum?.Title,
            ArtistId = primaryArtist?.Id ?? Guid.Empty,
            ArtistName = primaryArtist?.Name ?? "Unknown Artist",
            Genre = ct?.TrackGenres.FirstOrDefault()?.Genre?.Name,
            Year = ct?.Year,
            IsStarred = isStarred,
            CreatedAt = userTrack.CreatedAt
        };
    }
}
