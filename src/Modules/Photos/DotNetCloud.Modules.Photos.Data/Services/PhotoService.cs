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
/// Service for managing photos — CRUD, search, timeline queries, favorites.
/// </summary>
public sealed class PhotoService : IPhotoService
{
    private readonly PhotosDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<PhotoService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotoService"/> class.
    /// </summary>
    public PhotoService(PhotosDbContext db, IEventBus eventBus, ILogger<PhotoService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <summary>
    /// Gets an existing photo by its FileNode ID, or null if not found.
    /// </summary>
    internal async Task<PhotoDto?> GetByFileNodeIdAsync(Guid fileNodeId, CancellationToken cancellationToken = default)
    {
        var photo = await _db.Photos
            .Include(p => p.Metadata)
            .Include(p => p.Tags)
            .FirstOrDefaultAsync(p => p.FileNodeId == fileNodeId, cancellationToken);

        return photo is not null ? MapToDto(photo) : null;
    }

    /// <summary>
    /// Creates a new photo record linked to a FileNode.
    /// </summary>
    public async Task<PhotoDto> CreatePhotoAsync(Guid fileNodeId, string fileName, string mimeType, long sizeBytes, Guid ownerId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var photo = new Photo
        {
            FileNodeId = fileNodeId,
            OwnerId = ownerId,
            FileName = fileName,
            MimeType = mimeType,
            SizeBytes = sizeBytes,
            TakenAt = DateTime.UtcNow
        };

        _db.Photos.Add(photo);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Photo {PhotoId} created for file {FileNodeId} by user {UserId}", photo.Id, fileNodeId, ownerId);

        await _eventBus.PublishAsync(new PhotoUploadedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            PhotoId = photo.Id,
            FileNodeId = fileNodeId,
            OwnerId = ownerId,
            FileName = fileName
        }, caller, cancellationToken);

        return MapToDto(photo);
    }

    /// <summary>
    /// Gets a photo by ID. Returns null if not found or not accessible by the caller.
    /// </summary>
    public async Task<PhotoDto?> GetPhotoAsync(Guid photoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var photo = await _db.Photos
            .Include(p => p.Metadata)
            .Include(p => p.Tags)
            .Include(p => p.EditRecords)
            .FirstOrDefaultAsync(p => p.Id == photoId, cancellationToken);

        if (photo is null || (photo.OwnerId != caller.UserId && !await HasShareAccessAsync(photoId, caller.UserId, cancellationToken)))
            return null;

        return MapToDto(photo);
    }

    /// <summary>
    /// Lists photos for a user, ordered by date taken (newest first).
    /// </summary>
    public async Task<IReadOnlyList<PhotoDto>> ListPhotosAsync(CallerContext caller, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var photos = await _db.Photos
            .Include(p => p.Metadata)
            .Include(p => p.Tags)
            .Where(p => p.OwnerId == caller.UserId)
            .OrderByDescending(p => p.TakenAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return photos.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Gets photos in a date range for timeline view.
    /// </summary>
    public async Task<IReadOnlyList<PhotoDto>> GetTimelineAsync(CallerContext caller, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var photos = await _db.Photos
            .Include(p => p.Metadata)
            .Include(p => p.Tags)
            .Where(p => p.OwnerId == caller.UserId && p.TakenAt >= from && p.TakenAt <= to)
            .OrderByDescending(p => p.TakenAt)
            .ToListAsync(cancellationToken);

        return photos.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Toggles the favorite flag on a photo.
    /// </summary>
    public async Task<PhotoDto> ToggleFavoriteAsync(Guid photoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var photo = await _db.Photos.FirstOrDefaultAsync(p => p.Id == photoId && p.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.PhotoNotFound, "Photo not found.");

        photo.IsFavorite = !photo.IsFavorite;
        photo.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return MapToDto(photo);
    }

    /// <summary>
    /// Gets all favorite photos for a user.
    /// </summary>
    public async Task<IReadOnlyList<PhotoDto>> GetFavoritesAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var photos = await _db.Photos
            .Include(p => p.Tags)
            .Where(p => p.OwnerId == caller.UserId && p.IsFavorite)
            .OrderByDescending(p => p.TakenAt)
            .ToListAsync(cancellationToken);

        return photos.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Soft-deletes a photo.
    /// </summary>
    public async Task DeletePhotoAsync(Guid photoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var photo = await _db.Photos.FirstOrDefaultAsync(p => p.Id == photoId && p.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.PhotoNotFound, "Photo not found.");

        photo.IsDeleted = true;
        photo.DeletedAt = DateTime.UtcNow;
        photo.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Photo {PhotoId} soft-deleted by user {UserId}", photoId, caller.UserId);

        await _eventBus.PublishAsync(new PhotoDeletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            PhotoId = photoId,
            DeletedByUserId = caller.UserId,
            IsPermanent = false
        }, caller, cancellationToken);
    }

    /// <summary>
    /// Searches photos by filename (case-insensitive substring match).
    /// </summary>
    public async Task<IReadOnlyList<PhotoDto>> SearchAsync(CallerContext caller, string query, int maxResults = 20, CancellationToken cancellationToken = default)
    {
        var photos = await _db.Photos
            .Include(p => p.Tags)
            .Where(p => p.OwnerId == caller.UserId && p.FileName.Contains(query))
            .OrderByDescending(p => p.TakenAt)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return photos.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Checks if a user has share access to a photo.
    /// </summary>
    internal async Task<bool> HasShareAccessAsync(Guid photoId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.PhotoShares.AnyAsync(
            s => s.PhotoId == photoId && s.SharedWithUserId == userId &&
                 (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow),
            cancellationToken);
    }

    internal static PhotoDto MapToDto(Photo photo)
    {
        return new PhotoDto
        {
            Id = photo.Id,
            FileNodeId = photo.FileNodeId,
            OwnerId = photo.OwnerId,
            FileName = photo.FileName,
            MimeType = photo.MimeType,
            SizeBytes = photo.SizeBytes,
            Width = photo.Width,
            Height = photo.Height,
            IsFavorite = photo.IsFavorite,
            TakenAt = photo.TakenAt,
            CreatedAt = photo.CreatedAt,
            UpdatedAt = photo.UpdatedAt,
            Tags = photo.Tags?.Select(t => t.Tag).ToList() ?? [],
            HasEdits = photo.EditRecords?.Count > 0,
            Metadata = photo.Metadata is not null ? new PhotoMetadataDto
            {
                CameraMake = photo.Metadata.CameraMake,
                CameraModel = photo.Metadata.CameraModel,
                LensModel = photo.Metadata.LensModel,
                FocalLengthMm = photo.Metadata.FocalLengthMm,
                Aperture = photo.Metadata.Aperture,
                ShutterSpeed = photo.Metadata.ShutterSpeed,
                Iso = photo.Metadata.Iso,
                FlashFired = photo.Metadata.FlashFired,
                Orientation = photo.Metadata.Orientation,
                Location = photo.Metadata.Latitude.HasValue && photo.Metadata.Longitude.HasValue
                    ? new Core.DTOs.Media.GeoCoordinate
                    {
                        Latitude = photo.Metadata.Latitude.Value,
                        Longitude = photo.Metadata.Longitude.Value,
                        AltitudeMetres = photo.Metadata.AltitudeMetres
                    }
                    : null
            } : null
        };
    }
}
