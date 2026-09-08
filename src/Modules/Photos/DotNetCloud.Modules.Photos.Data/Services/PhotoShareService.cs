using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Models;
using DotNetCloud.Modules.Photos.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ITeamDirectory = DotNetCloud.Core.Capabilities.ITeamDirectory;

namespace DotNetCloud.Modules.Photos.Data.Services;

/// <summary>
/// Service for managing photo and album shares.
/// </summary>
public sealed class PhotoShareService : IPhotoShareService
{
    private readonly PhotosDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ITeamDirectory? _teamDirectory;
    private readonly ILogger<PhotoShareService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotoShareService"/> class.
    /// </summary>
    public PhotoShareService(PhotosDbContext db, IEventBus eventBus, ILogger<PhotoShareService> logger, ITeamDirectory? teamDirectory = null)
    {
        _db = db;
        _eventBus = eventBus;
        _teamDirectory = teamDirectory;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the caller's team IDs for team-share access checks (empty when the
    /// team directory capability is unavailable).
    /// </summary>
    private async Task<Guid[]> GetCallerTeamIdsAsync(CallerContext caller, CancellationToken cancellationToken)
    {
        if (_teamDirectory is null)
        {
            return [];
        }

        try
        {
            var teams = await _teamDirectory.GetTeamsForUserAsync(caller.UserId, cancellationToken);
            return teams.Select(t => t.Id).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve team memberships for {UserId}", caller.UserId);
            return [];
        }
    }

    /// <inheritdoc />
    public Task<PhotoShareDto> SharePhotoAsync(Guid photoId, Guid sharedWithUserId, PhotoSharePermission permission, CallerContext caller, CancellationToken cancellationToken = default)
        => SharePhotoAsync(photoId, sharedWithUserId, null, permission, caller, cancellationToken);

    /// <inheritdoc />
    public async Task<PhotoShareDto> SharePhotoAsync(Guid photoId, Guid? sharedWithUserId, Guid? sharedWithTeamId, PhotoSharePermission permission, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // User XOR team target.
        if (sharedWithUserId is null && sharedWithTeamId is null)
        {
            throw new ArgumentException("A photo share requires a user or a team target.", nameof(sharedWithTeamId));
        }

        if (sharedWithUserId is not null && sharedWithTeamId is not null)
        {
            throw new ArgumentException("A photo share cannot target both a user and a team.", nameof(sharedWithTeamId));
        }

        var photo = await _db.Photos.FirstOrDefaultAsync(p => p.Id == photoId && p.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.PhotoNotFound, "Photo not found.");

        PhotoShare? existingShare;
        if (sharedWithUserId is { } userId)
        {
            existingShare = await _db.PhotoShares
                .FirstOrDefaultAsync(s => s.PhotoId == photoId && s.SharedWithUserId == userId, cancellationToken);
        }
        else
        {
            existingShare = await _db.PhotoShares
                .FirstOrDefaultAsync(s => s.PhotoId == photoId && s.SharedWithTeamId == sharedWithTeamId, cancellationToken);
        }

        if (existingShare is not null)
        {
            // Update permission on the existing share for this target.
            existingShare.Permission = MapPermission(permission);
            await _db.SaveChangesAsync(cancellationToken);
            return MapToDto(existingShare);
        }

        var share = new PhotoShare
        {
            PhotoId = photoId,
            SharedByUserId = caller.UserId,
            SharedWithUserId = sharedWithUserId,
            SharedWithTeamId = sharedWithTeamId,
            Permission = MapPermission(permission)
        };

        _db.PhotoShares.Add(share);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Photo {PhotoId} shared with {TargetType} {TargetId} by {SharedByUserId}",
            photoId,
            sharedWithTeamId is not null ? "team" : "user",
            sharedWithTeamId ?? sharedWithUserId,
            caller.UserId);

        return MapToDto(share);
    }

    /// <inheritdoc />
    public Task<PhotoShareDto> ShareAlbumAsync(Guid albumId, Guid sharedWithUserId, PhotoSharePermission permission, CallerContext caller, CancellationToken cancellationToken = default)
        => ShareAlbumAsync(albumId, sharedWithUserId, null, permission, caller, cancellationToken);

    /// <inheritdoc />
    public async Task<PhotoShareDto> ShareAlbumAsync(Guid albumId, Guid? sharedWithUserId, Guid? sharedWithTeamId, PhotoSharePermission permission, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // User XOR team target.
        if (sharedWithUserId is null && sharedWithTeamId is null)
        {
            throw new ArgumentException("An album share requires a user or a team target.", nameof(sharedWithTeamId));
        }

        if (sharedWithUserId is not null && sharedWithTeamId is not null)
        {
            throw new ArgumentException("An album share cannot target both a user and a team.", nameof(sharedWithTeamId));
        }

        var album = await _db.Albums.FirstOrDefaultAsync(a => a.Id == albumId && a.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.AlbumNotFound, "Album not found.");

        PhotoShare? existingShare;
        if (sharedWithUserId is { } userId)
        {
            existingShare = await _db.PhotoShares
                .FirstOrDefaultAsync(s => s.AlbumId == albumId && s.SharedWithUserId == userId, cancellationToken);
        }
        else
        {
            existingShare = await _db.PhotoShares
                .FirstOrDefaultAsync(s => s.AlbumId == albumId && s.SharedWithTeamId == sharedWithTeamId, cancellationToken);
        }

        if (existingShare is not null)
        {
            // Update permission on the existing share for this target.
            existingShare.Permission = MapPermission(permission);
            await _db.SaveChangesAsync(cancellationToken);
            return MapToDto(existingShare);
        }

        var share = new PhotoShare
        {
            AlbumId = albumId,
            SharedByUserId = caller.UserId,
            SharedWithUserId = sharedWithUserId,
            SharedWithTeamId = sharedWithTeamId,
            Permission = MapPermission(permission)
        };

        _db.PhotoShares.Add(share);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Album {AlbumId} shared with {TargetType} {TargetId} by {SharedByUserId}",
            albumId,
            sharedWithTeamId is not null ? "team" : "user",
            sharedWithTeamId ?? sharedWithUserId,
            caller.UserId);

        await _eventBus.PublishAsync(new AlbumSharedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            AlbumId = albumId,
            SharedByUserId = caller.UserId,
            SharedWithUserId = sharedWithUserId,
            SharedWithTeamId = sharedWithTeamId,
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
    /// Gets photos/albums shared with the caller (direct user shares plus shares
    /// targeting a team the caller belongs to).
    /// </summary>
    public async Task<IReadOnlyList<PhotoShareDto>> GetSharedWithMeAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var teamIds = await GetCallerTeamIdsAsync(caller, cancellationToken);

        var shares = await _db.PhotoShares
            .Where(s =>
                (s.SharedWithUserId == caller.UserId ||
                 (s.SharedWithTeamId != null && teamIds.Contains(s.SharedWithTeamId.Value))) &&
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
        SharedWithTeamId = share.SharedWithTeamId,
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
