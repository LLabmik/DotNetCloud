using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Music.Models;
using DotNetCloud.Modules.Music.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Music.Data.Services;

/// <summary>
/// Service for managing artists — browse, search, artist detail with discography.
/// Uses UserArtist junction + CanonicalArtist (canonical/shared) tables.
/// </summary>
public sealed class ArtistService : IArtistService
{
    private readonly MusicDbContext _db;
    private readonly ILogger<ArtistService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtistService"/> class.
    /// </summary>
    public ArtistService(MusicDbContext db, ILogger<ArtistService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Gets an artist by canonical artist ID, scoped to the calling user.
    /// </summary>
    public async Task<ArtistDto?> GetArtistAsync(Guid artistId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var userArtist = await _db.UserArtists
            .Include(ua => ua.CanonicalArtist)
            .FirstOrDefaultAsync(ua => ua.CanonicalArtistId == artistId && ua.OwnerId == caller.UserId, cancellationToken);

        return userArtist is null ? null : await MapToDtoAsync(userArtist, caller.UserId, cancellationToken);
    }

    /// <summary>
    /// Lists artists for the authenticated user.
    /// </summary>
    public async Task<IReadOnlyList<ArtistDto>> ListArtistsAsync(CallerContext caller, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var userArtists = await _db.UserArtists
            .Include(ua => ua.CanonicalArtist)
            .Where(ua => ua.OwnerId == caller.UserId)
            .OrderBy(ua => ua.CanonicalArtist!.SortName ?? ua.CanonicalArtist!.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        var result = new List<ArtistDto>(userArtists.Count);
        foreach (var ua in userArtists)
            result.Add(await MapToDtoAsync(ua, caller.UserId, cancellationToken));
        return result;
    }

    /// <summary>
    /// Searches artists by name.
    /// </summary>
    public async Task<IReadOnlyList<ArtistDto>> SearchAsync(CallerContext caller, string query, int maxResults = 20, CancellationToken cancellationToken = default)
    {
        var queryLower = query.ToLowerInvariant();

        var userArtists = await _db.UserArtists
            .Include(ua => ua.CanonicalArtist)
            .Where(ua => ua.OwnerId == caller.UserId && ua.CanonicalArtist!.Name.ToLower().Contains(queryLower))
            .OrderBy(ua => ua.CanonicalArtist!.SortName ?? ua.CanonicalArtist!.Name)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        var result = new List<ArtistDto>(userArtists.Count);
        foreach (var ua in userArtists)
            result.Add(await MapToDtoAsync(ua, caller.UserId, cancellationToken));
        return result;
    }

    /// <summary>
    /// Soft-deletes a user-artist junction.
    /// </summary>
    public async Task DeleteArtistAsync(Guid artistId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var userArtist = await _db.UserArtists
            .FirstOrDefaultAsync(ua => ua.CanonicalArtistId == artistId && ua.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.ArtistNotFound, "Artist not found.");

        userArtist.IsDeleted = true;
        userArtist.DeletedAt = DateTime.UtcNow;
        userArtist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Artist {ArtistId} soft-deleted by user {UserId}", artistId, caller.UserId);
    }

    /// <summary>
    /// Gets the total count of artists for a user.
    /// </summary>
    public async Task<int> GetCountAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        return await _db.UserArtists.CountAsync(ua => ua.OwnerId == ownerId, cancellationToken);
    }

    /// <summary>
    /// Gets the artist biography and external links from canonical artist.
    /// </summary>
    public async Task<ArtistBioDto?> GetArtistBioAsync(Guid artistId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var userArtist = await _db.UserArtists
            .Include(ua => ua.CanonicalArtist)
            .FirstOrDefaultAsync(ua => ua.CanonicalArtistId == artistId && ua.OwnerId == caller.UserId, cancellationToken);

        if (userArtist?.CanonicalArtist is null)
            return null;

        var ca = userArtist.CanonicalArtist;
        return new ArtistBioDto
        {
            ArtistId = ca.Id,
            Name = ca.Name,
            Biography = ca.Biography,
            ImageUrl = ca.ImageUrl,
            WikipediaUrl = ca.WikipediaUrl,
            DiscogsUrl = ca.DiscogsUrl,
            OfficialUrl = ca.OfficialUrl,
            MusicBrainzId = ca.MusicBrainzId,
            LastEnrichedAt = ca.LastEnrichedAt
        };
    }

    private async Task<ArtistDto> MapToDtoAsync(UserArtist userArtist, Guid userId, CancellationToken cancellationToken)
    {
        var ca = userArtist.CanonicalArtist!;

        // Compute album/track counts from user junctions via canonical relationships
        var albumCount = await _db.UserAlbums
            .CountAsync(ua => ua.OwnerId == userId &&
                _db.CanonicalAlbumArtists.Any(caa => caa.AlbumId == ua.CanonicalAlbumId && caa.ArtistId == ca.Id),
                cancellationToken);

        var trackCount = await _db.UserTracks
            .CountAsync(ut => ut.OwnerId == userId &&
                _db.CanonicalTrackArtists.Any(cta => cta.TrackContentHash == ut.CanonicalTrackHash && cta.ArtistId == ca.Id),
                cancellationToken);

        var isStarred = await _db.StarredItems.AnyAsync(s =>
            s.UserId == userId && s.ItemType == StarredItemType.Artist && s.ItemId == ca.Id,
            cancellationToken);

        return new ArtistDto
        {
            Id = ca.Id,
            Name = ca.Name,
            SortName = ca.SortName,
            AlbumCount = albumCount,
            TrackCount = trackCount,
            IsStarred = isStarred,
            CreatedAt = userArtist.CreatedAt
        };
    }
}
