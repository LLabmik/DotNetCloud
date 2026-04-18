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
    /// Gets an artist by ID.
    /// </summary>
    public async Task<ArtistDto?> GetArtistAsync(Guid artistId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var artist = await _db.Artists
            .Include(a => a.Albums)
            .Include(a => a.TrackArtists)
            .FirstOrDefaultAsync(a => a.Id == artistId && a.OwnerId == caller.UserId, cancellationToken);

        return artist is null ? null : MapToDto(artist, caller.UserId);
    }

    /// <summary>
    /// Lists artists for the authenticated user.
    /// </summary>
    public async Task<IReadOnlyList<ArtistDto>> ListArtistsAsync(CallerContext caller, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var artists = await _db.Artists
            .Include(a => a.Albums)
            .Include(a => a.TrackArtists)
            .Where(a => a.OwnerId == caller.UserId)
            .OrderBy(a => a.SortName ?? a.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return artists.Select(a => MapToDto(a, caller.UserId)).ToList();
    }

    /// <summary>
    /// Searches artists by name.
    /// </summary>
    public async Task<IReadOnlyList<ArtistDto>> SearchAsync(CallerContext caller, string query, int maxResults = 20, CancellationToken cancellationToken = default)
    {
        var artists = await _db.Artists
            .Include(a => a.Albums)
            .Include(a => a.TrackArtists)
            .Where(a => a.OwnerId == caller.UserId && a.Name.Contains(query))
            .OrderBy(a => a.SortName ?? a.Name)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return artists.Select(a => MapToDto(a, caller.UserId)).ToList();
    }

    /// <summary>
    /// Deletes an artist (soft delete).
    /// </summary>
    public async Task DeleteArtistAsync(Guid artistId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var artist = await _db.Artists
            .FirstOrDefaultAsync(a => a.Id == artistId && a.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.ArtistNotFound, "Artist not found.");

        artist.IsDeleted = true;
        artist.DeletedAt = DateTime.UtcNow;
        artist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Artist {ArtistId} soft-deleted by user {UserId}", artistId, caller.UserId);
    }

    /// <summary>
    /// Gets the total count of artists for a user.
    /// </summary>
    public async Task<int> GetCountAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        return await _db.Artists.CountAsync(a => a.OwnerId == ownerId, cancellationToken);
    }

    /// <summary>
    /// Gets the artist biography and external links.
    /// </summary>
    public async Task<ArtistBioDto?> GetArtistBioAsync(Guid artistId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var artist = await _db.Artists
            .FirstOrDefaultAsync(a => a.Id == artistId && a.OwnerId == caller.UserId, cancellationToken);

        if (artist is null)
        {
            return null;
        }

        return new ArtistBioDto
        {
            ArtistId = artist.Id,
            Name = artist.Name,
            Biography = artist.Biography,
            ImageUrl = artist.ImageUrl,
            WikipediaUrl = artist.WikipediaUrl,
            DiscogsUrl = artist.DiscogsUrl,
            OfficialUrl = artist.OfficialUrl,
            MusicBrainzId = artist.MusicBrainzId,
            LastEnrichedAt = artist.LastEnrichedAt
        };
    }

    internal ArtistDto MapToDto(Artist artist, Guid userId)
    {
        var isStarred = _db.StarredItems.Any(s =>
            s.UserId == userId && s.ItemType == StarredItemType.Artist && s.ItemId == artist.Id);

        return new ArtistDto
        {
            Id = artist.Id,
            Name = artist.Name,
            SortName = artist.SortName,
            AlbumCount = artist.Albums?.Count ?? 0,
            TrackCount = artist.TrackArtists?.Count ?? 0,
            IsStarred = isStarred,
            CreatedAt = artist.CreatedAt
        };
    }
}
