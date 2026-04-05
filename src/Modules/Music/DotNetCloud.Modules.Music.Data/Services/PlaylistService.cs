using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Music.Data.Services;

/// <summary>
/// Service for managing playlists — CRUD, reorder tracks, playlist sharing.
/// </summary>
public sealed class PlaylistService
{
    private readonly MusicDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<PlaylistService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistService"/> class.
    /// </summary>
    public PlaylistService(MusicDbContext db, IEventBus eventBus, ILogger<PlaylistService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new playlist.
    /// </summary>
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

        _logger.LogInformation("Playlist {PlaylistId} '{Name}' created by user {UserId}",
            playlist.Id, playlist.Name, caller.UserId);

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

    /// <summary>
    /// Gets a playlist by ID.
    /// </summary>
    public async Task<PlaylistDto?> GetPlaylistAsync(Guid playlistId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var playlist = await _db.Playlists
            .Include(p => p.PlaylistTracks).ThenInclude(pt => pt.Track)
            .FirstOrDefaultAsync(p => p.Id == playlistId &&
                (p.OwnerId == caller.UserId || p.IsPublic), cancellationToken);

        return playlist is null ? null : MapToDto(playlist);
    }

    /// <summary>
    /// Lists playlists for the authenticated user.
    /// </summary>
    public async Task<IReadOnlyList<PlaylistDto>> ListPlaylistsAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var playlists = await _db.Playlists
            .Include(p => p.PlaylistTracks).ThenInclude(pt => pt.Track)
            .Where(p => p.OwnerId == caller.UserId)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return playlists.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Updates a playlist.
    /// </summary>
    public async Task<PlaylistDto> UpdatePlaylistAsync(Guid playlistId, UpdatePlaylistDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var playlist = await _db.Playlists
            .Include(p => p.PlaylistTracks).ThenInclude(pt => pt.Track)
            .FirstOrDefaultAsync(p => p.Id == playlistId && p.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.PlaylistNotFound, "Playlist not found.");

        if (dto.Name is not null) playlist.Name = dto.Name;
        if (dto.Description is not null) playlist.Description = dto.Description;
        if (dto.IsPublic.HasValue) playlist.IsPublic = dto.IsPublic.Value;
        playlist.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return MapToDto(playlist);
    }

    /// <summary>
    /// Deletes a playlist (soft delete).
    /// </summary>
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

    /// <summary>
    /// Adds a track to a playlist.
    /// </summary>
    public async Task AddTrackAsync(Guid playlistId, Guid trackId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var playlist = await _db.Playlists
            .FirstOrDefaultAsync(p => p.Id == playlistId && p.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.PlaylistNotFound, "Playlist not found.");

        var trackExists = await _db.Tracks.AnyAsync(t => t.Id == trackId, cancellationToken);
        if (!trackExists)
            throw new BusinessRuleException(ErrorCodes.TrackNotFound, "Track not found.");

        var alreadyInPlaylist = await _db.PlaylistTracks
            .AnyAsync(pt => pt.PlaylistId == playlistId && pt.TrackId == trackId, cancellationToken);
        if (alreadyInPlaylist)
            throw new BusinessRuleException(ErrorCodes.TrackAlreadyInPlaylist, "Track is already in this playlist.");

        var maxOrder = await _db.PlaylistTracks
            .Where(pt => pt.PlaylistId == playlistId)
            .MaxAsync(pt => (int?)pt.SortOrder, cancellationToken) ?? -1;

        _db.PlaylistTracks.Add(new PlaylistTrack
        {
            PlaylistId = playlistId,
            TrackId = trackId,
            SortOrder = maxOrder + 1
        });

        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Removes a track from a playlist.
    /// </summary>
    public async Task RemoveTrackAsync(Guid playlistId, Guid trackId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var playlist = await _db.Playlists
            .FirstOrDefaultAsync(p => p.Id == playlistId && p.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.PlaylistNotFound, "Playlist not found.");

        var playlistTrack = await _db.PlaylistTracks
            .FirstOrDefaultAsync(pt => pt.PlaylistId == playlistId && pt.TrackId == trackId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.TrackNotFound, "Track is not in this playlist.");

        _db.PlaylistTracks.Remove(playlistTrack);
        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gets ordered track DTOs for a playlist.
    /// </summary>
    public async Task<IReadOnlyList<TrackDto>> GetPlaylistTracksAsync(Guid playlistId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var playlist = await _db.Playlists
            .FirstOrDefaultAsync(p => p.Id == playlistId &&
                (p.OwnerId == caller.UserId || p.IsPublic), cancellationToken);

        if (playlist is null) return [];

        var tracks = await _db.PlaylistTracks
            .Include(pt => pt.Track).ThenInclude(t => t!.Album)
            .Include(pt => pt.Track).ThenInclude(t => t!.TrackArtists).ThenInclude(ta => ta.Artist)
            .Include(pt => pt.Track).ThenInclude(t => t!.TrackGenres).ThenInclude(tg => tg.Genre)
            .Where(pt => pt.PlaylistId == playlistId)
            .OrderBy(pt => pt.SortOrder)
            .Select(pt => pt.Track!)
            .ToListAsync(cancellationToken);

        return tracks.Select(t => MapTrackToDto(t, caller.UserId)).ToList();
    }

    private PlaylistDto MapToDto(Playlist playlist)
    {
        var totalDurationTicks = playlist.PlaylistTracks?
            .Sum(pt => pt.Track?.DurationTicks ?? 0) ?? 0;

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

    private TrackDto MapTrackToDto(Track track, Guid userId)
    {
        var primaryArtist = track.TrackArtists?
            .FirstOrDefault(ta => ta.IsPrimary)?.Artist
            ?? track.TrackArtists?.FirstOrDefault()?.Artist;

        var isStarred = _db.StarredItems.Any(s =>
            s.UserId == userId && s.ItemType == StarredItemType.Track && s.ItemId == track.Id);

        return new TrackDto
        {
            Id = track.Id,
            FileNodeId = track.FileNodeId,
            Title = track.Title,
            TrackNumber = track.TrackNumber,
            DiscNumber = track.DiscNumber,
            Duration = TimeSpan.FromTicks(track.DurationTicks),
            SizeBytes = track.SizeBytes,
            Bitrate = track.Bitrate,
            MimeType = track.MimeType,
            AlbumId = track.AlbumId,
            AlbumTitle = track.Album?.Title,
            ArtistId = primaryArtist?.Id ?? Guid.Empty,
            ArtistName = primaryArtist?.Name ?? "Unknown Artist",
            Genre = track.TrackGenres?.FirstOrDefault()?.Genre?.Name,
            Year = track.Year,
            IsStarred = isStarred,
            CreatedAt = track.CreatedAt
        };
    }
}
