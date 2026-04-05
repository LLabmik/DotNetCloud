using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Music.Data.Services;

/// <summary>
/// Service for managing playback history, scrobbles, and play count tracking.
/// </summary>
public sealed class PlaybackService
{
    private readonly MusicDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<PlaybackService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackService"/> class.
    /// </summary>
    public PlaybackService(MusicDbContext db, IEventBus eventBus, ILogger<PlaybackService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <summary>
    /// Records a track play event, incrementing play count and creating a history entry.
    /// </summary>
    public async Task RecordPlayAsync(Guid trackId, int durationPlayedSeconds, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var track = await _db.Tracks
            .Include(t => t.TrackArtists).ThenInclude(ta => ta.Artist)
            .FirstOrDefaultAsync(t => t.Id == trackId, cancellationToken);

        if (track is null)
        {
            _logger.LogWarning("Attempted to record play for non-existent track {TrackId}", trackId);
            return;
        }

        track.PlayCount++;
        track.UpdatedAt = DateTime.UtcNow;

        _db.PlaybackHistories.Add(new PlaybackHistory
        {
            UserId = caller.UserId,
            TrackId = trackId,
            DurationPlayedSeconds = durationPlayedSeconds
        });

        await _db.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new TrackPlayedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            TrackId = trackId,
            UserId = caller.UserId,
            DurationPlayedSeconds = durationPlayedSeconds
        }, caller, cancellationToken);
    }

    /// <summary>
    /// Records a scrobble (track play completion for last.fm-style history).
    /// </summary>
    public async Task ScrobbleAsync(Guid trackId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var track = await _db.Tracks
            .Include(t => t.TrackArtists).ThenInclude(ta => ta.Artist)
            .Include(t => t.Album)
            .FirstOrDefaultAsync(t => t.Id == trackId, cancellationToken);

        if (track is null) return;

        var primaryArtist = track.TrackArtists?
            .FirstOrDefault(ta => ta.IsPrimary)?.Artist
            ?? track.TrackArtists?.FirstOrDefault()?.Artist;

        var scrobble = new ScrobbleRecord
        {
            UserId = caller.UserId,
            TrackId = trackId,
            ArtistName = primaryArtist?.Name ?? "Unknown Artist",
            TrackTitle = track.Title,
            AlbumTitle = track.Album?.Title
        };

        _db.ScrobbleRecords.Add(scrobble);
        await _db.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new TrackScrobbledEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            TrackId = trackId,
            UserId = caller.UserId,
            ArtistName = scrobble.ArtistName,
            TrackTitle = scrobble.TrackTitle,
            AlbumTitle = scrobble.AlbumTitle
        }, caller, cancellationToken);
    }

    /// <summary>
    /// Gets recently played tracks for a user.
    /// </summary>
    public async Task<IReadOnlyList<PlaybackHistory>> GetRecentlyPlayedAsync(Guid userId, int count = 20, CancellationToken cancellationToken = default)
    {
        return await _db.PlaybackHistories
            .Include(h => h.Track)
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.PlayedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the most played tracks for a user.
    /// </summary>
    public async Task<IReadOnlyList<Track>> GetMostPlayedAsync(Guid userId, int count = 20, CancellationToken cancellationToken = default)
    {
        return await _db.Tracks
            .Where(t => t.OwnerId == userId && t.PlayCount > 0)
            .OrderByDescending(t => t.PlayCount)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Stars or unstars an item (artist, album, or track).
    /// </summary>
    public async Task ToggleStarAsync(Guid itemId, StarredItemType itemType, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var existing = await _db.StarredItems
            .FirstOrDefaultAsync(s =>
                s.UserId == caller.UserId && s.ItemType == itemType && s.ItemId == itemId,
                cancellationToken);

        if (existing is not null)
        {
            _db.StarredItems.Remove(existing);
        }
        else
        {
            _db.StarredItems.Add(new StarredItem
            {
                UserId = caller.UserId,
                ItemType = itemType,
                ItemId = itemId
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gets starred items of a specific type for a user.
    /// </summary>
    public async Task<IReadOnlyList<StarredItem>> GetStarredAsync(Guid userId, StarredItemType itemType, CancellationToken cancellationToken = default)
    {
        return await _db.StarredItems
            .Where(s => s.UserId == userId && s.ItemType == itemType)
            .OrderByDescending(s => s.StarredAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Checks if an item is starred by a user.
    /// </summary>
    public async Task<bool> IsStarredAsync(Guid userId, Guid itemId, StarredItemType itemType, CancellationToken cancellationToken = default)
    {
        return await _db.StarredItems
            .AnyAsync(s => s.UserId == userId && s.ItemType == itemType && s.ItemId == itemId, cancellationToken);
    }
}
