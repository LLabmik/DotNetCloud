using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Music.Models;
using DotNetCloud.Modules.Music.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Music.Data.Services;

/// <summary>
/// Service for music recommendations — recently played, most played, similar tracks, new additions.
/// Uses UserTrack + canonical tables instead of legacy Track.
/// </summary>
public sealed class RecommendationService : IRecommendationService
{
    private readonly MusicDbContext _db;
    private readonly TrackService _trackService;
    private readonly ILogger<RecommendationService> _logger;

    public RecommendationService(MusicDbContext db, TrackService trackService, ILogger<RecommendationService> logger)
    {
        _db = db;
        _trackService = trackService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TrackDto>> GetRecentlyPlayedAsync(CallerContext caller, int count = 20, CancellationToken cancellationToken = default)
    {
        var userTrackIds = await _db.PlaybackHistories
            .Where(h => h.UserId == caller.UserId)
            .OrderByDescending(h => h.PlayedAt)
            .Select(h => h.UserTrackId)
            .Distinct()
            .Take(count)
            .ToListAsync(cancellationToken);

        var userTracks = await _db.UserTracks
            .Include(ut => ut.CanonicalTrack).ThenInclude(ct => ct!.TrackArtists).ThenInclude(cta => cta.Artist)
            .Include(ut => ut.CanonicalTrack).ThenInclude(ct => ct!.TrackGenres).ThenInclude(ctg => ctg.Genre)
            .Include(ut => ut.CanonicalAlbum)
            .Where(ut => userTrackIds.Contains(ut.Id))
            .ToListAsync(cancellationToken);

        return userTracks.Select(ut => _trackService.MapToDto(ut, caller.UserId)).ToList();
    }

    public async Task<IReadOnlyList<TrackDto>> GetMostPlayedAsync(CallerContext caller, int count = 20, CancellationToken cancellationToken = default)
    {
        var userTracks = await _db.UserTracks
            .Include(ut => ut.CanonicalTrack).ThenInclude(ct => ct!.TrackArtists).ThenInclude(cta => cta.Artist)
            .Include(ut => ut.CanonicalTrack).ThenInclude(ct => ct!.TrackGenres).ThenInclude(ctg => ctg.Genre)
            .Include(ut => ut.CanonicalAlbum)
            .Where(ut => ut.OwnerId == caller.UserId && ut.PlayCount > 0)
            .OrderByDescending(ut => ut.PlayCount)
            .Take(count)
            .ToListAsync(cancellationToken);

        return userTracks.Select(ut => _trackService.MapToDto(ut, caller.UserId)).ToList();
    }

    public async Task<IReadOnlyList<TrackDto>> GetSimilarTracksAsync(Guid trackId, CallerContext caller, int count = 20, CancellationToken cancellationToken = default)
    {
        var sourceTrack = await _db.UserTracks
            .Include(ut => ut.CanonicalTrack).ThenInclude(ct => ct!.TrackArtists)
            .Include(ut => ut.CanonicalTrack).ThenInclude(ct => ct!.TrackGenres)
            .FirstOrDefaultAsync(ut => ut.Id == trackId, cancellationToken);

        if (sourceTrack?.CanonicalTrack is null)
            return [];

        var ct = sourceTrack.CanonicalTrack;
        var genreIds = ct.TrackGenres.Select(ctg => ctg.GenreId).ToList();
        var artistIds = ct.TrackArtists.Select(cta => cta.ArtistId).ToList();

        var similar = await _db.UserTracks
            .Include(ut => ut.CanonicalTrack).ThenInclude(ct => ct!.TrackArtists).ThenInclude(cta => cta.Artist)
            .Include(ut => ut.CanonicalTrack).ThenInclude(ct => ct!.TrackGenres).ThenInclude(ctg => ctg.Genre)
            .Include(ut => ut.CanonicalAlbum)
            .Where(ut => ut.Id != trackId && ut.OwnerId == caller.UserId &&
                (ut.CanonicalTrack!.TrackGenres.Any(ctg => genreIds.Contains(ctg.GenreId)) ||
                 ut.CanonicalTrack.TrackArtists.Any(cta => artistIds.Contains(cta.ArtistId))))
            .OrderBy(_ => Guid.NewGuid())
            .Take(count)
            .ToListAsync(cancellationToken);

        return similar.Select(ut => _trackService.MapToDto(ut, caller.UserId)).ToList();
    }

    public async Task<IReadOnlyList<TrackDto>> GetNewAdditionsAsync(CallerContext caller, int count = 20, CancellationToken cancellationToken = default)
    {
        return await _trackService.GetRecentTracksAsync(caller, count, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetGenresAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.UserTracks
            .Where(ut => ut.OwnerId == userId)
            .SelectMany(ut => ut.CanonicalTrack!.TrackGenres)
            .Select(ctg => ctg.Genre!.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);
    }
}
