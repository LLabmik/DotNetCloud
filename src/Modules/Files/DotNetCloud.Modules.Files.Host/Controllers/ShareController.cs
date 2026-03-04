using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// REST API controller for file and folder sharing operations.
/// </summary>
[ApiController]
[Route("api/v1/files/{nodeId:guid}/shares")]
public class ShareController : ControllerBase
{
    private readonly FilesDbContext _db;
    private readonly ILogger<ShareController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShareController"/> class.
    /// </summary>
    public ShareController(FilesDbContext db, ILogger<ShareController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Lists all shares for a file or folder.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListAsync(Guid nodeId)
    {
        var shares = await _db.FileShares
            .AsNoTracking()
            .Where(s => s.FileNodeId == nodeId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => ToDto(s))
            .ToListAsync();

        return Ok(new { success = true, data = shares });
    }

    /// <summary>
    /// Creates a new share for a file or folder.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateAsync(
        Guid nodeId,
        [FromBody] CreateShareDto dto,
        [FromQuery] Guid userId)
    {
        var node = await _db.FileNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == nodeId);

        if (node is null)
            return NotFound(new { success = false, error = "Node not found." });

        if (!Enum.TryParse<ShareType>(dto.ShareType, true, out var shareType))
            return BadRequest(new { success = false, error = "Invalid share type." });

        if (!Enum.TryParse<SharePermission>(dto.Permission, true, out var permission))
            permission = SharePermission.Read;

        var share = new Models.FileShare
        {
            FileNodeId = nodeId,
            ShareType = shareType,
            Permission = permission,
            SharedWithUserId = dto.SharedWithUserId,
            SharedWithTeamId = dto.SharedWithTeamId,
            SharedWithGroupId = dto.SharedWithGroupId,
            CreatedByUserId = userId,
            ExpiresAt = dto.ExpiresAt,
            MaxDownloads = dto.MaxDownloads,
            Note = dto.Note
        };

        if (shareType == ShareType.PublicLink)
        {
            share.LinkToken = GenerateLinkToken();
            if (!string.IsNullOrWhiteSpace(dto.LinkPassword))
            {
                share.LinkPasswordHash = HashPassword(dto.LinkPassword);
            }
        }

        _db.FileShares.Add(share);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Share {ShareId} created for node {NodeId}", share.Id, nodeId);

        return Created($"/api/v1/files/{nodeId}/shares/{share.Id}",
            new { success = true, data = ToDto(share) });
    }

    /// <summary>
    /// Revokes (deletes) a share.
    /// </summary>
    [HttpDelete("{shareId:guid}")]
    public async Task<IActionResult> RevokeAsync(Guid nodeId, Guid shareId)
    {
        var share = await _db.FileShares
            .FirstOrDefaultAsync(s => s.Id == shareId && s.FileNodeId == nodeId);

        if (share is null)
            return NotFound(new { success = false, error = "Share not found." });

        _db.FileShares.Remove(share);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Share {ShareId} revoked for node {NodeId}", shareId, nodeId);

        return Ok(new { success = true });
    }

    private static FileShareDto ToDto(Models.FileShare share)
    {
        return new FileShareDto
        {
            Id = share.Id,
            FileNodeId = share.FileNodeId,
            ShareType = share.ShareType.ToString(),
            SharedWithUserId = share.SharedWithUserId,
            SharedWithTeamId = share.SharedWithTeamId,
            Permission = share.Permission.ToString(),
            LinkToken = share.LinkToken,
            ExpiresAt = share.ExpiresAt,
            DownloadCount = share.DownloadCount,
            MaxDownloads = share.MaxDownloads,
            CreatedAt = share.CreatedAt,
            Note = share.Note
        };
    }

    private static string GenerateLinkToken()
    {
        return Convert.ToBase64String(
                System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private static string HashPassword(string password)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(password));
        return Convert.ToHexStringLower(hash);
    }
}
