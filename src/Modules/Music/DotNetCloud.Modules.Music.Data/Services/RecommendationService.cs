using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Music.Models;
using DotNetCloud.Modules.Music.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Music.Data.Services;

/// <summary>
/// Service for music recommendations — recently played, most played, random by genre, similar tracks, new additions.
/// </summary>
public sealed class RecommendationService : IRecommendationService
{
    private readonly MusicDbContext _db;
    private readonly TrackService _trackService;
    private readonly ILogger<RecommendationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecommendationService"/> class.
    /// </summary>
    public RecommendationService(MusicDbContext db, TrackService trackService, ILogger<RecommendationService> logger)
    {
        _db = db;
        _trackService = trackService;
        _logger = logger;
    }

    /// <summary>
    /// Gets recently played tracks for a user.
    /// </summary>
    public async Task<IReadOnlyList<TrackDto>> GetRecentlyPlayedAsync(CallerContext caller, int count = 20, CancellationToken cancellationToken = default)
    {
        var trackIds = await _db.PlaybackHistories
            .Where(h => h.UserId == caller.UserId)
            .OrderByDescending(h => h.PlayedAt)
            .Select(h => h.TrackId)
            .Distinct()
            .Take(count)
            .ToListAsync(cancellationToken);

        var tracks = await _db.Tracks
            .Include(t => t.Album)
            .Include(t => t.TrackArtists).ThenInclude(ta => ta.Artist)
            .Include(t => t.TrackGenres).ThenInclude(tg => tg.Genre)
            .Where(t => trackIds.Contains(t.Id))
            .ToListAsync(cancellationToken);

        return tracks.Select(t => _trackService.MapToDto(t, caller.UserId)).ToList();
    }

    /// <summary>
    /// Gets the most played tracks for a user.
    /// </summary>
    public async Task<IReadOnlyList<TrackDto>> GetMostPlayedAsync(CallerContext caller, int count = 20, CancellationToken cancellationToken = default)
    {
        var tracks = await _db.Tracks
            .Include(t => t.Album)
            .Include(t => t.TrackArtists).ThenInclude(ta => ta.Artist)
            .Include(t => t.TrackGenres).ThenInclude(tg => tg.Genre)
            .Where(t => t.OwnerId == caller.UserId && t.PlayCount > 0)
            .OrderByDescending(t => t.PlayCount)
            .Take(count)
            .ToListAsync(cancellationToken);

        return tracks.Select(t => _trackService.MapToDto(t, caller.UserId)).ToList();
    }

    /// <summary>
    /// Gets tracks similar to a given track (same genre/artist).
    /// </summary>
    public async Task<IReadOnlyList<TrackDto>> GetSimilarTracksAsync(Guid trackId, CallerContext caller, int count = 20, CancellationToken cancellationToken = default)
    {
        var track = await _db.Tracks
            .Include(t => t.TrackArtists)
            .Include(t => t.TrackGenres)
            .FirstOrDefaultAsync(t => t.Id == trackId, cancellationToken);

        if (track is null) return [];

        var genreIds = track.TrackGenres.Select(tg => tg.GenreId).ToList();
        var artistIds = track.TrackArtists.Select(ta => ta.ArtistId).ToList();

        var similar = await _db.Tracks
            .Include(t => t.Album)
            .Include(t => t.TrackArtists).ThenInclude(ta => ta.Artist)
            .Include(t => t.TrackGenres).ThenInclude(tg => tg.Genre)
            .Where(t => t.Id != trackId && t.OwnerId == caller.UserId &&
                (t.TrackGenres.Any(tg => genreIds.Contains(tg.GenreId)) ||
                 t.TrackArtists.Any(ta => artistIds.Contains(ta.ArtistId))))
            .OrderBy(_ => Guid.NewGuid())
            .Take(count)
            .ToListAsync(cancellationToken);

        return similar.Select(t => _trackService.MapToDto(t, caller.UserId)).ToList();
    }

    /// <summary>
    /// Gets newly added tracks.
    /// </summary>
    public async Task<IReadOnlyList<TrackDto>> GetNewAdditionsAsync(CallerContext caller, int count = 20, CancellationToken cancellationToken = default)
    {
        return await _trackService.GetRecentTracksAsync(caller, count, cancellationToken);
    }

    /// <summary>
    /// Gets all genres for a user.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetGenresAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.TrackGenres
            .Include(tg => tg.Genre)
            .Include(tg => tg.Track)
            .Where(tg => tg.Track!.OwnerId == userId)
            .Select(tg => tg.Genre!.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);
    }
}
