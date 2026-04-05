using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Models;
using DotNetCloud.Modules.Photos.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Photos.Data.Services;

/// <summary>
/// Service for managing photo albums — CRUD, add/remove photos, cover photo, sharing.
/// </summary>
public sealed class AlbumService : Photos.Services.IAlbumService
{
    private readonly PhotosDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<AlbumService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlbumService"/> class.
    /// </summary>
    public AlbumService(PhotosDbContext db, IEventBus eventBus, ILogger<AlbumService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new album.
    /// </summary>
    public async Task<AlbumDto> CreateAlbumAsync(CreateAlbumDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var album = new Album
        {
            OwnerId = caller.UserId,
            Title = dto.Title,
            Description = dto.Description,
            CoverPhotoId = dto.CoverPhotoId
        };

        _db.Albums.Add(album);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Album {AlbumId} '{Title}' created by user {UserId}", album.Id, album.Title, caller.UserId);

        await _eventBus.PublishAsync(new AlbumCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            AlbumId = album.Id,
            Title = album.Title,
            OwnerId = caller.UserId
        }, caller, cancellationToken);

        return MapToDto(album);
    }

    /// <summary>
    /// Gets an album by ID.
    /// </summary>
    public async Task<AlbumDto?> GetAlbumAsync(Guid albumId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var album = await _db.Albums
            .Include(a => a.AlbumPhotos)
            .Include(a => a.Shares)
            .FirstOrDefaultAsync(a => a.Id == albumId, cancellationToken);

        if (album is null)
            return null;

        if (album.OwnerId != caller.UserId && !await HasAlbumShareAccessAsync(albumId, caller.UserId, cancellationToken))
            return null;

        return MapToDto(album);
    }

    /// <summary>
    /// Lists all albums for a user.
    /// </summary>
    public async Task<IReadOnlyList<AlbumDto>> ListAlbumsAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var albums = await _db.Albums
            .Include(a => a.AlbumPhotos)
            .Include(a => a.Shares)
            .Where(a => a.OwnerId == caller.UserId)
            .OrderByDescending(a => a.UpdatedAt)
            .ToListAsync(cancellationToken);

        return albums.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Updates an album.
    /// </summary>
    public async Task<AlbumDto> UpdateAlbumAsync(Guid albumId, UpdateAlbumDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var album = await _db.Albums
            .Include(a => a.AlbumPhotos)
            .Include(a => a.Shares)
            .FirstOrDefaultAsync(a => a.Id == albumId && a.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.AlbumNotFound, "Album not found.");

        if (dto.Title is not null) album.Title = dto.Title;
        if (dto.Description is not null) album.Description = dto.Description;
        if (dto.CoverPhotoId.HasValue) album.CoverPhotoId = dto.CoverPhotoId;
        album.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return MapToDto(album);
    }

    /// <summary>
    /// Soft-deletes an album.
    /// </summary>
    public async Task DeleteAlbumAsync(Guid albumId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var album = await _db.Albums.FirstOrDefaultAsync(a => a.Id == albumId && a.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.AlbumNotFound, "Album not found.");

        album.IsDeleted = true;
        album.DeletedAt = DateTime.UtcNow;
        album.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Album {AlbumId} soft-deleted by user {UserId}", albumId, caller.UserId);
    }

    /// <summary>
    /// Adds a photo to an album.
    /// </summary>
    public async Task AddPhotoToAlbumAsync(Guid albumId, Guid photoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var album = await _db.Albums.FirstOrDefaultAsync(a => a.Id == albumId && a.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.AlbumNotFound, "Album not found.");

        var photo = await _db.Photos.FirstOrDefaultAsync(p => p.Id == photoId && p.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.PhotoNotFound, "Photo not found.");

        var exists = await _db.AlbumPhotos.AnyAsync(ap => ap.AlbumId == albumId && ap.PhotoId == photoId, cancellationToken);
        if (exists)
            throw new BusinessRuleException(ErrorCodes.PhotoAlreadyInAlbum, "Photo is already in this album.");

        var maxOrder = await _db.AlbumPhotos
            .Where(ap => ap.AlbumId == albumId)
            .MaxAsync(ap => (int?)ap.SortOrder, cancellationToken) ?? 0;

        _db.AlbumPhotos.Add(new AlbumPhoto
        {
            AlbumId = albumId,
            PhotoId = photoId,
            SortOrder = maxOrder + 1
        });

        album.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Removes a photo from an album.
    /// </summary>
    public async Task RemovePhotoFromAlbumAsync(Guid albumId, Guid photoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var album = await _db.Albums.FirstOrDefaultAsync(a => a.Id == albumId && a.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.AlbumNotFound, "Album not found.");

        var albumPhoto = await _db.AlbumPhotos.FirstOrDefaultAsync(ap => ap.AlbumId == albumId && ap.PhotoId == photoId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.PhotoNotFound, "Photo not found in album.");

        _db.AlbumPhotos.Remove(albumPhoto);
        album.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gets photos in an album.
    /// </summary>
    public async Task<IReadOnlyList<PhotoDto>> GetAlbumPhotosAsync(Guid albumId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var album = await _db.Albums.FirstOrDefaultAsync(a => a.Id == albumId, cancellationToken);
        if (album is null)
            return [];

        if (album.OwnerId != caller.UserId && !await HasAlbumShareAccessAsync(albumId, caller.UserId, cancellationToken))
            return [];

        var photos = await _db.AlbumPhotos
            .Include(ap => ap.Photo).ThenInclude(p => p!.Tags)
            .Include(ap => ap.Photo).ThenInclude(p => p!.Metadata)
            .Where(ap => ap.AlbumId == albumId)
            .OrderBy(ap => ap.SortOrder)
            .Select(ap => ap.Photo!)
            .ToListAsync(cancellationToken);

        return photos.Select(PhotoService.MapToDto).ToList();
    }

    private async Task<bool> HasAlbumShareAccessAsync(Guid albumId, Guid userId, CancellationToken cancellationToken)
    {
        return await _db.PhotoShares.AnyAsync(
            s => s.AlbumId == albumId && s.SharedWithUserId == userId &&
                 (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow),
            cancellationToken);
    }

    internal static AlbumDto MapToDto(Album album)
    {
        return new AlbumDto
        {
            Id = album.Id,
            OwnerId = album.OwnerId,
            Title = album.Title,
            Description = album.Description,
            CoverPhotoId = album.CoverPhotoId,
            PhotoCount = album.AlbumPhotos?.Count ?? 0,
            CreatedAt = album.CreatedAt,
            UpdatedAt = album.UpdatedAt,
            IsShared = album.Shares?.Count > 0
        };
    }
}
