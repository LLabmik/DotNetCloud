using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Photos.Data.Services;

/// <summary>
/// Service for managing photo and album shares.
/// </summary>
public sealed class PhotoShareService
{
    private readonly PhotosDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<PhotoShareService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotoShareService"/> class.
    /// </summary>
    public PhotoShareService(PhotosDbContext db, IEventBus eventBus, ILogger<PhotoShareService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <summary>
    /// Shares a photo with another user.
    /// </summary>
    public async Task<PhotoShareDto> SharePhotoAsync(Guid photoId, Guid sharedWithUserId, PhotoSharePermission permission, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var photo = await _db.Photos.FirstOrDefaultAsync(p => p.Id == photoId && p.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.PhotoNotFound, "Photo not found.");

        var share = new PhotoShare
        {
            PhotoId = photoId,
            SharedByUserId = caller.UserId,
            SharedWithUserId = sharedWithUserId,
            Permission = MapPermission(permission)
        };

        _db.PhotoShares.Add(share);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Photo {PhotoId} shared with user {SharedWithUserId} by {SharedByUserId}", photoId, sharedWithUserId, caller.UserId);

        return MapToDto(share);
    }

    /// <summary>
    /// Shares an album with another user.
    /// </summary>
    public async Task<PhotoShareDto> ShareAlbumAsync(Guid albumId, Guid sharedWithUserId, PhotoSharePermission permission, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var album = await _db.Albums.FirstOrDefaultAsync(a => a.Id == albumId && a.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.AlbumNotFound, "Album not found.");

        var share = new PhotoShare
        {
            AlbumId = albumId,
            SharedByUserId = caller.UserId,
            SharedWithUserId = sharedWithUserId,
            Permission = MapPermission(permission)
        };

        _db.PhotoShares.Add(share);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Album {AlbumId} shared with user {SharedWithUserId} by {SharedByUserId}", albumId, sharedWithUserId, caller.UserId);

        await _eventBus.PublishAsync(new AlbumSharedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            AlbumId = albumId,
            SharedByUserId = caller.UserId,
            SharedWithUserId = sharedWithUserId,
            Permission = permission.ToString()
        }, caller, cancellationToken);

        return MapToDto(share);
    }

    /// <summary>
    /// Removes a share.
    /// </summary>
    public async Task RemoveShareAsync(Guid shareId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var share = await _db.PhotoShares.FirstOrDefaultAsync(s => s.Id == shareId && s.SharedByUserId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.PhotoShareNotFound, "Share not found.");

        _db.PhotoShares.Remove(share);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gets all shares for a photo.
    /// </summary>
    public async Task<IReadOnlyList<PhotoShareDto>> GetPhotoSharesAsync(Guid photoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var shares = await _db.PhotoShares
            .Where(s => s.PhotoId == photoId && s.SharedByUserId == caller.UserId)
            .ToListAsync(cancellationToken);

        return shares.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Gets all shares for an album.
    /// </summary>
    public async Task<IReadOnlyList<PhotoShareDto>> GetAlbumSharesAsync(Guid albumId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var shares = await _db.PhotoShares
            .Where(s => s.AlbumId == albumId && s.SharedByUserId == caller.UserId)
            .ToListAsync(cancellationToken);

        return shares.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Gets photos/albums shared with a user.
    /// </summary>
    public async Task<IReadOnlyList<PhotoShareDto>> GetSharedWithMeAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var shares = await _db.PhotoShares
            .Where(s => s.SharedWithUserId == caller.UserId &&
                       (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow))
            .ToListAsync(cancellationToken);

        return shares.Select(MapToDto).ToList();
    }

    private static PhotoSharePermissionLevel MapPermission(PhotoSharePermission permission) => permission switch
    {
        PhotoSharePermission.ReadOnly => PhotoSharePermissionLevel.ReadOnly,
        PhotoSharePermission.Download => PhotoSharePermissionLevel.Download,
        PhotoSharePermission.Contribute => PhotoSharePermissionLevel.Contribute,
        _ => PhotoSharePermissionLevel.ReadOnly
    };

    private static PhotoShareDto MapToDto(PhotoShare share) => new()
    {
        Id = share.Id,
        PhotoId = share.PhotoId,
        AlbumId = share.AlbumId,
        SharedWithUserId = share.SharedWithUserId,
        Permission = share.Permission switch
        {
            PhotoSharePermissionLevel.Download => PhotoSharePermission.Download,
            PhotoSharePermissionLevel.Contribute => PhotoSharePermission.Contribute,
            _ => PhotoSharePermission.ReadOnly
        },
        CreatedAt = share.CreatedAt,
        ExpiresAt = share.ExpiresAt
    };
}
