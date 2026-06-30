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
/// Uses <see cref="IDbContextFactory{TContext}"/> to create short-lived contexts,
/// avoiding DbContext threading issues in Blazor Server's concurrent rendering model.
/// </summary>
public sealed class ArtistService : IArtistService
{
    private readonly IDbContextFactory<MusicDbContext> _dbFactory;
    private readonly ILogger<ArtistService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtistService"/> class.
    /// </summary>
    public ArtistService(IDbContextFactory<MusicDbContext> dbFactory, ILogger<ArtistService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <summary>
    /// Gets an artist by canonical artist ID, scoped to the calling user.
    /// </summary>
    public async Task<ArtistDto?> GetArtistAsync(Guid artistId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var userArtist = await db.UserArtists
            .Include(ua => ua.CanonicalArtist)
            .FirstOrDefaultAsync(ua => ua.CanonicalArtistId == artistId && ua.OwnerId == caller.UserId, cancellationToken);

        if (userArtist is null)
            return null;

        return await MapToDtoAsync(db, userArtist, caller.UserId, cancellationToken);
    }

    /// <summary>
    /// Lists artists for the authenticated user.
    /// Uses batched queries to avoid N+1 per-artist count lookups.
    /// </summary>
    public async Task<IReadOnlyList<ArtistDto>> ListArtistsAsync(CallerContext caller, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var userArtists = await db.UserArtists
            .Include(ua => ua.CanonicalArtist)
            .Where(ua => ua.OwnerId == caller.UserId)
            .OrderBy(ua => ua.CanonicalArtist!.SortName ?? ua.CanonicalArtist!.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return await MapBatchToDtoAsync(db, userArtists, caller.UserId, cancellationToken);
    }

    /// <summary>
    /// Searches artists by name.
    /// </summary>
    public async Task<IReadOnlyList<ArtistDto>> SearchAsync(CallerContext caller, string query, int maxResults = 20, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var queryLower = query.ToLowerInvariant();

        var userArtists = await db.UserArtists
            .Include(ua => ua.CanonicalArtist)
            .Where(ua => ua.OwnerId == caller.UserId && ua.CanonicalArtist!.Name.ToLower().Contains(queryLower))
            .OrderBy(ua => ua.CanonicalArtist!.SortName ?? ua.CanonicalArtist!.Name)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return await MapBatchToDtoAsync(db, userArtists, caller.UserId, cancellationToken);
    }

    /// <summary>
    /// Hard-deletes a user-artist junction. Canonical artist data is preserved.
    /// </summary>
    public async Task DeleteArtistAsync(Guid artistId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var userArtist = await db.UserArtists
            .FirstOrDefaultAsync(ua => ua.CanonicalArtistId == artistId && ua.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.ArtistNotFound, "Artist not found.");

        db.UserArtists.Remove(userArtist);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Artist {ArtistId} hard-deleted by user {UserId}", artistId, caller.UserId);
    }

    /// <summary>
    /// Gets the total count of artists for a user.
    /// </summary>
    public async Task<int> GetCountAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.UserArtists.CountAsync(ua => ua.OwnerId == ownerId, cancellationToken);
    }

    /// <summary>
    /// Gets the artist biography and external links from canonical artist.
    /// </summary>
    public async Task<ArtistBioDto?> GetArtistBioAsync(Guid artistId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var userArtist = await db.UserArtists
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
            LogoUrl = ca.LogoUrl,
            WikipediaUrl = ca.WikipediaUrl,
            DiscogsUrl = ca.DiscogsUrl,
            OfficialUrl = ca.OfficialUrl,
            MusicBrainzId = ca.MusicBrainzId,
            LastEnrichedAt = ca.LastEnrichedAt
        };
    }

    /// <summary>
    /// Batched DTO mapping: runs 3 bulk queries for all artists instead of N+1 per-artist queries.
    /// </summary>
    private async Task<List<ArtistDto>> MapBatchToDtoAsync(
        MusicDbContext db,
        List<UserArtist> userArtists,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userArtists.Count == 0)
            return [];

        var artistIds = userArtists.Select(ua => ua.CanonicalArtistId).ToList();

        // ── Batch 1: Album counts per artist ──
        var albumCounts = await db.UserAlbums
            .Where(ua => ua.OwnerId == userId)
            .Join(db.CanonicalAlbumArtists,
                ua => ua.CanonicalAlbumId,
                caa => caa.AlbumId,
                (_, caa) => caa.ArtistId)
            .Where(artistId => artistIds.Contains(artistId))
            .GroupBy(artistId => artistId)
            .Select(g => new { ArtistId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ArtistId, x => x.Count, cancellationToken);

        // ── Batch 2: Track counts per artist ──
        var trackCounts = await db.UserTracks
            .Where(ut => ut.OwnerId == userId)
            .Join(db.CanonicalTrackArtists,
                ut => ut.CanonicalTrackHash,
                cta => cta.TrackContentHash,
                (_, cta) => cta.ArtistId)
            .Where(artistId => artistIds.Contains(artistId))
            .GroupBy(artistId => artistId)
            .Select(g => new { ArtistId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ArtistId, x => x.Count, cancellationToken);

        // ── Batch 3: Starred artist IDs ──
        var starredIds = await db.StarredItems
            .Where(s => s.UserId == userId && s.ItemType == StarredItemType.Artist && artistIds.Contains(s.ItemId))
            .Select(s => s.ItemId)
            .ToHashSetAsync(cancellationToken);

        // ── Assemble results ──
        var result = new List<ArtistDto>(userArtists.Count);
        foreach (var ua in userArtists)
        {
            var ca = ua.CanonicalArtist!;
            var artistId = ca.Id;

            result.Add(new ArtistDto
            {
                Id = artistId,
                Name = ca.Name,
                SortName = ca.SortName,
                AlbumCount = albumCounts.GetValueOrDefault(artistId, 0),
                TrackCount = trackCounts.GetValueOrDefault(artistId, 0),
                IsStarred = starredIds.Contains(artistId),
                LogoUrl = ca.LogoUrl,
                CreatedAt = ua.CreatedAt
            });
        }
        return result;
    }

    /// <summary>
    /// Single-artist DTO mapping (used by <see cref="GetArtistAsync"/>).
    /// </summary>
    private static async Task<ArtistDto> MapToDtoAsync(
        MusicDbContext db,
        UserArtist userArtist,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var ca = userArtist.CanonicalArtist!;

        var albumCount = await db.UserAlbums
            .CountAsync(ua => ua.OwnerId == userId &&
                db.CanonicalAlbumArtists.Any(caa => caa.AlbumId == ua.CanonicalAlbumId && caa.ArtistId == ca.Id),
                cancellationToken);

        var trackCount = await db.UserTracks
            .CountAsync(ut => ut.OwnerId == userId &&
                db.CanonicalTrackArtists.Any(cta => cta.TrackContentHash == ut.CanonicalTrackHash && cta.ArtistId == ca.Id),
                cancellationToken);

        var isStarred = await db.StarredItems.AnyAsync(s =>
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

    /// <inheritdoc />
    public async Task<List<string>> ListArtistAlphabetAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.UserArtists
            .Where(ua => ua.OwnerId == caller.UserId)
            .Select(ua => ua.CanonicalArtist!.Name.Substring(0, 1).ToUpper())
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);
    }
}
