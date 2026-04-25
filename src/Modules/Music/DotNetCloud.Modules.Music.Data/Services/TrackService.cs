using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Music.Models;
using DotNetCloud.Modules.Music.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Music.Data.Services;

/// <summary>
/// Service for managing tracks — search, starred/favorites, recently added.
/// </summary>
public sealed class TrackService : ITrackService
{
    private readonly MusicDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<TrackService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackService"/> class.
    /// </summary>
    public TrackService(MusicDbContext db, IEventBus eventBus, ILogger<TrackService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <summary>
    /// Gets a track by ID.
    /// </summary>
    public async Task<TrackDto?> GetTrackAsync(Guid trackId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var track = await _db.Tracks
            .Include(t => t.Album)
            .Include(t => t.TrackArtists).ThenInclude(ta => ta.Artist)
            .Include(t => t.TrackGenres).ThenInclude(tg => tg.Genre)
            .FirstOrDefaultAsync(t => t.Id == trackId && t.OwnerId == caller.UserId, cancellationToken);

        return track is null ? null : MapToDto(track, caller.UserId);
    }

    /// <inheritdoc/>
    public async Task<TrackDto?> GetTrackByFileNodeIdAsync(Guid fileNodeId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var track = await _db.Tracks
            .Include(t => t.Album)
            .Include(t => t.TrackArtists).ThenInclude(ta => ta.Artist)
            .Include(t => t.TrackGenres).ThenInclude(tg => tg.Genre)
            .FirstOrDefaultAsync(t => t.FileNodeId == fileNodeId && t.OwnerId == caller.UserId, cancellationToken);

        return track is null ? null : MapToDto(track, caller.UserId);
    }

    /// <summary>
    /// Lists tracks for the authenticated user.
    /// </summary>
    public async Task<IReadOnlyList<TrackDto>> ListTracksAsync(CallerContext caller, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var tracks = await _db.Tracks
            .Include(t => t.Album)
            .Include(t => t.TrackArtists).ThenInclude(ta => ta.Artist)
            .Include(t => t.TrackGenres).ThenInclude(tg => tg.Genre)
            .Where(t => t.OwnerId == caller.UserId)
            .OrderBy(t => t.Title)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return tracks.Select(t => MapToDto(t, caller.UserId)).ToList();
    }

    /// <summary>
    /// Lists tracks by album.
    /// </summary>
    public async Task<IReadOnlyList<TrackDto>> ListTracksByAlbumAsync(Guid albumId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var tracks = await _db.Tracks
            .Include(t => t.Album)
            .Include(t => t.TrackArtists).ThenInclude(ta => ta.Artist)
            .Include(t => t.TrackGenres).ThenInclude(tg => tg.Genre)
            .Where(t => t.AlbumId == albumId && t.OwnerId == caller.UserId)
            .OrderBy(t => t.DiscNumber)
            .ThenBy(t => t.TrackNumber)
            .ToListAsync(cancellationToken);

        return tracks.Select(t => MapToDto(t, caller.UserId)).ToList();
    }

    /// <summary>
    /// Searches tracks by title, artist name, or album title (case-insensitive).
    /// </summary>
    public async Task<IReadOnlyList<TrackDto>> SearchAsync(CallerContext caller, string query, int maxResults = 20, CancellationToken cancellationToken = default)
    {
        var queryLower = query.ToLowerInvariant();

        var tracks = await _db.Tracks
            .Include(t => t.Album)
            .Include(t => t.TrackArtists).ThenInclude(ta => ta.Artist)
            .Include(t => t.TrackGenres).ThenInclude(tg => tg.Genre)
            .Where(t => t.OwnerId == caller.UserId && (
                t.Title.ToLower().Contains(queryLower)
                || (t.Album != null && t.Album.Title.ToLower().Contains(queryLower))
                || t.TrackArtists!.Any(ta => ta.Artist != null && ta.Artist.Name.ToLower().Contains(queryLower))
            ))
            .OrderBy(t => t.Title)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return tracks.Select(t => MapToDto(t, caller.UserId)).ToList();
    }

    /// <summary>
    /// Gets recently added tracks.
    /// </summary>
    public async Task<IReadOnlyList<TrackDto>> GetRecentTracksAsync(CallerContext caller, int count = 20, CancellationToken cancellationToken = default)
    {
        var tracks = await _db.Tracks
            .Include(t => t.Album)
            .Include(t => t.TrackArtists).ThenInclude(ta => ta.Artist)
            .Include(t => t.TrackGenres).ThenInclude(tg => tg.Genre)
            .Where(t => t.OwnerId == caller.UserId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

        return tracks.Select(t => MapToDto(t, caller.UserId)).ToList();
    }

    /// <summary>
    /// Gets random tracks, optionally by genre.
    /// </summary>
    public async Task<IReadOnlyList<TrackDto>> GetRandomTracksAsync(CallerContext caller, int count = 20, string? genre = null, CancellationToken cancellationToken = default)
    {
        var query = _db.Tracks
            .Include(t => t.Album)
            .Include(t => t.TrackArtists).ThenInclude(ta => ta.Artist)
            .Include(t => t.TrackGenres).ThenInclude(tg => tg.Genre)
            .Where(t => t.OwnerId == caller.UserId);

        if (!string.IsNullOrWhiteSpace(genre))
        {
            query = query.Where(t => t.TrackGenres.Any(tg => tg.Genre!.Name == genre));
        }

        // Use Guid to get random ordering
        var tracks = await query
            .OrderBy(_ => Guid.NewGuid())
            .Take(count)
            .ToListAsync(cancellationToken);

        return tracks.Select(t => MapToDto(t, caller.UserId)).ToList();
    }

    /// <summary>
    /// Soft-deletes a track.
    /// </summary>
    public async Task DeleteTrackAsync(Guid trackId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var track = await _db.Tracks
            .FirstOrDefaultAsync(t => t.Id == trackId && t.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.TrackNotFound, "Track not found.");

        track.IsDeleted = true;
        track.DeletedAt = DateTime.UtcNow;
        track.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Track {TrackId} soft-deleted by user {UserId}", trackId, caller.UserId);

        await _eventBus.PublishAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "music",
            EntityId = trackId.ToString(),
            Action = SearchIndexAction.Remove
        }, caller, cancellationToken);
    }

    /// <summary>
    /// Gets starred (favorited) tracks for the current user.
    /// </summary>
    public async Task<IReadOnlyList<TrackDto>> GetStarredTracksAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var starredTrackIds = await _db.StarredItems
            .Where(s => s.UserId == caller.UserId && s.ItemType == StarredItemType.Track)
            .OrderByDescending(s => s.StarredAt)
            .Select(s => s.ItemId)
            .ToListAsync(cancellationToken);

        if (starredTrackIds.Count == 0)
            return [];

        var tracks = await _db.Tracks
            .Include(t => t.Album)
            .Include(t => t.TrackArtists).ThenInclude(ta => ta.Artist)
            .Include(t => t.TrackGenres).ThenInclude(tg => tg.Genre)
            .Where(t => starredTrackIds.Contains(t.Id) && t.OwnerId == caller.UserId)
            .ToListAsync(cancellationToken);

        // Preserve starred-at ordering
        var trackMap = tracks.ToDictionary(t => t.Id);
        return starredTrackIds
            .Where(id => trackMap.ContainsKey(id))
            .Select(id => MapToDto(trackMap[id], caller.UserId))
            .ToList();
    }

    internal TrackDto MapToDto(Track track, Guid userId)
    {
        var primaryArtist = track.TrackArtists?
            .FirstOrDefault(ta => ta.IsPrimary)?.Artist
            ?? track.TrackArtists?.FirstOrDefault()?.Artist;

        var primaryGenre = track.TrackGenres?.FirstOrDefault()?.Genre?.Name;

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
            Genre = primaryGenre,
            Year = track.Year,
            IsStarred = isStarred,
            CreatedAt = track.CreatedAt
        };
    }
}
