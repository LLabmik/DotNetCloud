using System.Security.Cryptography;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FileShare = DotNetCloud.Modules.Files.Models.FileShare;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Manages file sharing: user, team, and public link shares.
/// </summary>
internal sealed class ShareService : IShareService
{
    private readonly FilesDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ShareService> _logger;
    private readonly IPermissionService _permissions;
    private static readonly PasswordHasher<object> PasswordHasher = new();

    public ShareService(FilesDbContext db, IEventBus eventBus, ILogger<ShareService> logger, IPermissionService permissions)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
        _permissions = permissions;
    }

    /// <inheritdoc />
    public async Task<FileShareDto> CreateShareAsync(Guid fileNodeId, CreateShareDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(caller);

        var node = await _db.FileNodes.FindAsync([fileNodeId], cancellationToken)
            ?? throw new NotFoundException("FileNode", fileNodeId);

        EnsureOwnerOrSystem(node, caller);

        if (!Enum.TryParse<ShareType>(dto.ShareType, ignoreCase: true, out var shareType))
            throw new Core.Errors.ValidationException("ShareType", $"Invalid share type: {dto.ShareType}");

        if (!Enum.TryParse<SharePermission>(dto.Permission, ignoreCase: true, out var permission))
            throw new Core.Errors.ValidationException("Permission", $"Invalid permission: {dto.Permission}");

        var share = new FileShare
        {
            FileNodeId = fileNodeId,
            ShareType = shareType,
            SharedWithUserId = dto.SharedWithUserId,
            SharedWithTeamId = dto.SharedWithTeamId,
            SharedWithGroupId = dto.SharedWithGroupId,
            Permission = permission,
            MaxDownloads = dto.MaxDownloads,
            ExpiresAt = dto.ExpiresAt,
            CreatedByUserId = caller.UserId,
            Note = dto.Note
        };

        // Generate public link token
        if (shareType == ShareType.PublicLink)
        {
            share.LinkToken = GenerateLinkToken();

            if (!string.IsNullOrEmpty(dto.LinkPassword))
                share.LinkPasswordHash = PasswordHasher.HashPassword(null!, dto.LinkPassword);
        }

        _db.FileShares.Add(share);
        await _db.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new FileSharedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = fileNodeId,
            FileName = node.Name,
            ShareId = share.Id,
            ShareType = share.ShareType.ToString(),
            SharedWithUserId = share.SharedWithUserId,
            SharedByUserId = caller.UserId
        }, caller, cancellationToken);

        _logger.LogInformation("Share {ShareId} created on node {NodeId} by {UserId}. Type: {ShareType}",
            share.Id, fileNodeId, caller.UserId, shareType);

        return ToDto(share, node.Name);
    }

    /// <inheritdoc />
    public async Task<FileShareDto> UpdateShareAsync(Guid shareId, UpdateShareDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(caller);

        var share = await _db.FileShares
            .Include(s => s.FileNode)
            .FirstOrDefaultAsync(s => s.Id == shareId, cancellationToken)
            ?? throw new NotFoundException("FileShare", shareId);

        if (share.CreatedByUserId != caller.UserId && caller.Type != CallerType.System)
            throw new ForbiddenException("Only the share creator or a system caller can update this share.");

        if (dto.Permission is not null && Enum.TryParse<SharePermission>(dto.Permission, ignoreCase: true, out var perm))
            share.Permission = perm;

        if (dto.ExpiresAt.HasValue)
            share.ExpiresAt = dto.ExpiresAt;

        if (dto.MaxDownloads.HasValue)
            share.MaxDownloads = dto.MaxDownloads;

        if (dto.Note is not null)
            share.Note = dto.Note;

        if (dto.LinkPassword is not null)
        {
            share.LinkPasswordHash = string.IsNullOrEmpty(dto.LinkPassword)
                ? null
                : PasswordHasher.HashPassword(null!, dto.LinkPassword);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(share, share.FileNode?.Name);
    }

    /// <inheritdoc />
    public async Task DeleteShareAsync(Guid shareId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var share = await _db.FileShares
            .FirstOrDefaultAsync(s => s.Id == shareId, cancellationToken)
            ?? throw new NotFoundException("FileShare", shareId);

        if (share.CreatedByUserId != caller.UserId && caller.Type != CallerType.System)
            throw new ForbiddenException("Only the share creator or a system caller can delete this share.");

        _db.FileShares.Remove(share);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Share {ShareId} deleted by {UserId}", shareId, caller.UserId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FileShareDto>> GetSharesAsync(Guid fileNodeId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        await _permissions.RequirePermissionAsync(fileNodeId, caller, SharePermission.Full, cancellationToken);

        return await _db.FileShares
            .AsNoTracking()
            .Include(s => s.FileNode)
            .Where(s => s.FileNodeId == fileNodeId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new FileShareDto
            {
                Id = s.Id,
                FileNodeId = s.FileNodeId,
                NodeName = s.FileNode!.Name,
                ShareType = s.ShareType.ToString(),
                SharedWithUserId = s.SharedWithUserId,
                SharedWithTeamId = s.SharedWithTeamId,
                SharedWithGroupId = s.SharedWithGroupId,
                Permission = s.Permission.ToString(),
                LinkToken = s.LinkToken,
                ExpiresAt = s.ExpiresAt,
                DownloadCount = s.DownloadCount,
                MaxDownloads = s.MaxDownloads,
                CreatedAt = s.CreatedAt,
                CreatedByUserId = s.CreatedByUserId,
                Note = s.Note,
                HasPassword = s.LinkPasswordHash != null
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FileShareDto>> GetSharedWithMeAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        return await _db.FileShares
            .AsNoTracking()
            .Include(s => s.FileNode)
            .Where(s => s.ShareType == ShareType.User && s.SharedWithUserId == caller.UserId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new FileShareDto
            {
                Id = s.Id,
                FileNodeId = s.FileNodeId,
                NodeName = s.FileNode!.Name,
                ShareType = s.ShareType.ToString(),
                SharedWithUserId = s.SharedWithUserId,
                SharedWithTeamId = s.SharedWithTeamId,
                SharedWithGroupId = s.SharedWithGroupId,
                Permission = s.Permission.ToString(),
                LinkToken = s.LinkToken,
                ExpiresAt = s.ExpiresAt,
                DownloadCount = s.DownloadCount,
                MaxDownloads = s.MaxDownloads,
                CreatedAt = s.CreatedAt,
                CreatedByUserId = s.CreatedByUserId,
                Note = s.Note,
                HasPassword = s.LinkPasswordHash != null
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FileShareDto>> GetSharedByMeAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        return await _db.FileShares
            .AsNoTracking()
            .Include(s => s.FileNode)
            .Where(s => s.CreatedByUserId == caller.UserId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new FileShareDto
            {
                Id = s.Id,
                FileNodeId = s.FileNodeId,
                NodeName = s.FileNode!.Name,
                ShareType = s.ShareType.ToString(),
                SharedWithUserId = s.SharedWithUserId,
                SharedWithTeamId = s.SharedWithTeamId,
                SharedWithGroupId = s.SharedWithGroupId,
                Permission = s.Permission.ToString(),
                LinkToken = s.LinkToken,
                ExpiresAt = s.ExpiresAt,
                DownloadCount = s.DownloadCount,
                MaxDownloads = s.MaxDownloads,
                CreatedAt = s.CreatedAt,
                CreatedByUserId = s.CreatedByUserId,
                Note = s.Note,
                HasPassword = s.LinkPasswordHash != null
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<FileShareDto?> ResolvePublicLinkAsync(string linkToken, string? password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(linkToken);

        var share = await _db.FileShares
            .AsNoTracking()
            .Include(s => s.FileNode)
            .FirstOrDefaultAsync(s => s.LinkToken == linkToken && s.ShareType == ShareType.PublicLink, cancellationToken);

        if (share is null)
            return null;

        // Check expiry
        if (share.ExpiresAt.HasValue && share.ExpiresAt.Value < DateTime.UtcNow)
            return null;

        // Check download limit
        if (share.MaxDownloads.HasValue && share.DownloadCount >= share.MaxDownloads.Value)
            return null;

        // Check password
        if (share.LinkPasswordHash is not null)
        {
            if (string.IsNullOrEmpty(password))
                return null;

            var result = PasswordHasher.VerifyHashedPassword(null!, share.LinkPasswordHash, password);
            if (result == PasswordVerificationResult.Failed)
                return null;
        }

        return ToDto(share, share.FileNode?.Name);
    }

    /// <inheritdoc />
    public async Task<PublicLinkInfoDto> GetPublicLinkInfoAsync(string linkToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(linkToken);

        var share = await _db.FileShares
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.LinkToken == linkToken && s.ShareType == ShareType.PublicLink, cancellationToken);

        if (share is null)
            return new PublicLinkInfoDto { Exists = false };

        return new PublicLinkInfoDto
        {
            Exists = true,
            RequiresPassword = share.LinkPasswordHash is not null,
            IsExpired = share.ExpiresAt.HasValue && share.ExpiresAt.Value < DateTime.UtcNow,
            IsMaxedOut = share.MaxDownloads.HasValue && share.DownloadCount >= share.MaxDownloads.Value
        };
    }

    /// <inheritdoc />
    public async Task IncrementDownloadCountAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        var share = await _db.FileShares
            .Include(s => s.FileNode)
            .FirstOrDefaultAsync(s => s.Id == shareId, cancellationToken);

        if (share is null)
            return;

        var wasFirstAccess = share.DownloadCount == 0;
        share.DownloadCount++;
        await _db.SaveChangesAsync(cancellationToken);

        if (wasFirstAccess && share.FileNode is not null)
        {
            await _eventBus.PublishAsync(new PublicLinkAccessedEvent
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                FileNodeId = share.FileNodeId,
                FileName = share.FileNode.Name,
                ShareId = share.Id,
                CreatedByUserId = share.CreatedByUserId
            }, CallerContext.CreateSystemContext(), cancellationToken);
        }
    }

    private static string GenerateLinkToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static void EnsureOwnerOrSystem(FileNode node, CallerContext caller)
    {
        if (caller.Type == CallerType.System)
            return;

        if (node.OwnerId != caller.UserId)
            throw new ForbiddenException("You do not have permission to share this node.");
    }

    private static FileShareDto ToDto(FileShare share, string? nodeName) => new()
    {
        Id = share.Id,
        FileNodeId = share.FileNodeId,
        NodeName = nodeName,
        ShareType = share.ShareType.ToString(),
        SharedWithUserId = share.SharedWithUserId,
        SharedWithTeamId = share.SharedWithTeamId,
        SharedWithGroupId = share.SharedWithGroupId,
        Permission = share.Permission.ToString(),
        LinkToken = share.LinkToken,
        ExpiresAt = share.ExpiresAt,
        DownloadCount = share.DownloadCount,
        MaxDownloads = share.MaxDownloads,
        CreatedAt = share.CreatedAt,
        CreatedByUserId = share.CreatedByUserId,
        Note = share.Note,
        HasPassword = share.LinkPasswordHash is not null
    };
}
