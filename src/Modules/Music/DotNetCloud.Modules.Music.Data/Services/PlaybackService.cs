using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Music.Models;
using DotNetCloud.Modules.Music.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Music.Data.Services;

/// <summary>
/// Service for managing playback history, scrobbles, and play count tracking.
/// Uses UserTrack (canonical) instead of the legacy Track table.
/// </summary>
public sealed class PlaybackService : IPlaybackService
{
    private readonly MusicDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<PlaybackService> _logger;

    public PlaybackService(MusicDbContext db, IEventBus eventBus, ILogger<PlaybackService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task RecordPlayAsync(Guid trackId, int durationPlayedSeconds, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var userTrack = await _db.UserTracks
            .Include(ut => ut.CanonicalTrack)
                .ThenInclude(ct => ct!.TrackArtists).ThenInclude(cta => cta.Artist)
            .FirstOrDefaultAsync(ut => ut.Id == trackId, cancellationToken);

        if (userTrack is null)
        {
            _logger.LogWarning("Attempted to record play for non-existent track {TrackId}", trackId);
            return;
        }

        userTrack.PlayCount++;
        userTrack.UpdatedAt = DateTime.UtcNow;

        _db.PlaybackHistories.Add(new PlaybackHistory
        {
            UserId = caller.UserId,
            UserTrackId = userTrack.Id,
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

    public async Task ScrobbleAsync(Guid trackId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var userTrack = await _db.UserTracks
            .Include(ut => ut.CanonicalTrack)
                .ThenInclude(ct => ct!.TrackArtists).ThenInclude(cta => cta.Artist)
            .Include(ut => ut.CanonicalAlbum)
            .FirstOrDefaultAsync(ut => ut.Id == trackId, cancellationToken);

        if (userTrack?.CanonicalTrack is null)
            return;

        var ct = userTrack.CanonicalTrack;
        var primaryArtist = ct.TrackArtists
            .FirstOrDefault(cta => cta.IsPrimary)?.Artist
            ?? ct.TrackArtists.FirstOrDefault()?.Artist;

        var scrobble = new ScrobbleRecord
        {
            UserId = caller.UserId,
            UserTrackId = userTrack.Id,
            ArtistName = primaryArtist?.Name ?? "Unknown Artist",
            TrackTitle = ct.Title,
            AlbumTitle = userTrack.CanonicalAlbum?.Title
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

    public async Task<IReadOnlyList<PlaybackHistory>> GetRecentlyPlayedAsync(Guid userId, int count = 20, CancellationToken cancellationToken = default)
    {
        return await _db.PlaybackHistories
            .Include(h => h.UserTrack)
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.PlayedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserTrack>> GetMostPlayedAsync(Guid userId, int count = 20, CancellationToken cancellationToken = default)
    {
        return await _db.UserTracks
            .Where(ut => ut.OwnerId == userId && ut.PlayCount > 0)
            .OrderByDescending(ut => ut.PlayCount)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task ToggleStarAsync(Guid itemId, StarredItemType itemType, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var existing = await _db.StarredItems
            .FirstOrDefaultAsync(s =>
                s.UserId == caller.UserId && s.ItemType == itemType && s.ItemId == itemId,
                cancellationToken);

        if (existing is not null)
            _db.StarredItems.Remove(existing);
        else
            _db.StarredItems.Add(new StarredItem { UserId = caller.UserId, ItemType = itemType, ItemId = itemId });

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StarredItem>> GetStarredAsync(Guid userId, StarredItemType itemType, CancellationToken cancellationToken = default)
    {
        return await _db.StarredItems
            .Where(s => s.UserId == userId && s.ItemType == itemType)
            .OrderByDescending(s => s.StarredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsStarredAsync(Guid userId, Guid itemId, StarredItemType itemType, CancellationToken cancellationToken = default)
    {
        return await _db.StarredItems
            .AnyAsync(s => s.UserId == userId && s.ItemType == itemType && s.ItemId == itemId, cancellationToken);
    }
}
