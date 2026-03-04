using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// REST API controller for file and folder operations.
/// Provides CRUD, tree browsing, move, copy, upload, download, and favorites.
/// </summary>
[ApiController]
[Route("api/v1/files")]
public class FilesController : ControllerBase
{
    private readonly FilesDbContext _db;
    private readonly ILogger<FilesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilesController"/> class.
    /// </summary>
    public FilesController(FilesDbContext db, ILogger<FilesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Lists files and folders in a directory.
    /// </summary>
    /// <param name="parentId">Parent folder ID. Null for root.</param>
    /// <param name="userId">Owner user ID.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Items per page (max 500).</param>
    /// <param name="sortBy">Sort field: name, size, updated_at, created_at.</param>
    /// <param name="sortDesc">Sort descending.</param>
    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] Guid? parentId,
        [FromQuery] Guid userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string sortBy = "name",
        [FromQuery] bool sortDesc = false)
    {
        var query = _db.FileNodes
            .AsNoTracking()
            .Where(n => n.OwnerId == userId && n.ParentId == parentId);

        query = sortBy.ToLowerInvariant() switch
        {
            "size" => sortDesc ? query.OrderByDescending(n => n.Size) : query.OrderBy(n => n.Size),
            "updated_at" => sortDesc ? query.OrderByDescending(n => n.UpdatedAt) : query.OrderBy(n => n.UpdatedAt),
            "created_at" => sortDesc ? query.OrderByDescending(n => n.CreatedAt) : query.OrderBy(n => n.CreatedAt),
            _ => sortDesc
                ? query.OrderByDescending(n => n.NodeType).ThenByDescending(n => n.Name)
                : query.OrderBy(n => n.NodeType).ThenBy(n => n.Name)
        };

        var totalCount = await query.CountAsync();
        pageSize = Math.Clamp(pageSize, 1, 500);
        page = Math.Max(page, 1);

        var nodes = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => ToDto(n))
            .ToListAsync();

        return Ok(new
        {
            success = true,
            data = nodes,
            pagination = new { page, pageSize, totalCount }
        });
    }

    /// <summary>
    /// Gets a file or folder by ID.
    /// </summary>
    [HttpGet("{nodeId:guid}")]
    public async Task<IActionResult> GetAsync(Guid nodeId)
    {
        var node = await _db.FileNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == nodeId);

        if (node is null)
            return NotFound(new { success = false, error = "Node not found." });

        return Ok(new { success = true, data = ToDto(node) });
    }

    /// <summary>
    /// Creates a new folder.
    /// </summary>
    [HttpPost("folders")]
    public async Task<IActionResult> CreateFolderAsync(
        [FromBody] CreateFolderDto dto,
        [FromQuery] Guid userId)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { success = false, error = "Folder name is required." });

        string materializedPath;
        int depth;

        if (dto.ParentId.HasValue)
        {
            var parent = await _db.FileNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == dto.ParentId.Value && n.NodeType == FileNodeType.Folder);

            if (parent is null)
                return NotFound(new { success = false, error = "Parent folder not found." });

            materializedPath = parent.MaterializedPath;
            depth = parent.Depth + 1;
        }
        else
        {
            materializedPath = string.Empty;
            depth = 0;
        }

        var folder = new FileNode
        {
            Name = dto.Name,
            NodeType = FileNodeType.Folder,
            OwnerId = userId,
            ParentId = dto.ParentId,
            Depth = depth
        };
        folder.MaterializedPath = string.IsNullOrEmpty(materializedPath)
            ? $"/{folder.Id}"
            : $"{materializedPath}/{folder.Id}";

        _db.FileNodes.Add(folder);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Folder {FolderId} '{Name}' created by user {UserId}", folder.Id, folder.Name, userId);

        return Created($"/api/v1/files/{folder.Id}", new { success = true, data = ToDto(folder) });
    }

    /// <summary>
    /// Renames a file or folder.
    /// </summary>
    [HttpPatch("{nodeId:guid}/rename")]
    public async Task<IActionResult> RenameAsync(Guid nodeId, [FromBody] RenameNodeDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { success = false, error = "New name is required." });

        var node = await _db.FileNodes.FindAsync(nodeId);
        if (node is null)
            return NotFound(new { success = false, error = "Node not found." });

        node.Name = dto.Name;
        node.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { success = true });
    }

    /// <summary>
    /// Moves a file or folder to a different parent.
    /// </summary>
    [HttpPatch("{nodeId:guid}/move")]
    public async Task<IActionResult> MoveAsync(Guid nodeId, [FromBody] MoveNodeDto dto)
    {
        var node = await _db.FileNodes.FindAsync(nodeId);
        if (node is null)
            return NotFound(new { success = false, error = "Node not found." });

        var targetParent = await _db.FileNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == dto.TargetParentId && n.NodeType == FileNodeType.Folder);

        if (targetParent is null)
            return NotFound(new { success = false, error = "Target folder not found." });

        if (node.NodeType == FileNodeType.Folder &&
            targetParent.MaterializedPath.StartsWith(node.MaterializedPath, StringComparison.Ordinal))
        {
            return BadRequest(new { success = false, error = "Cannot move a folder into itself or a descendant." });
        }

        node.ParentId = dto.TargetParentId;
        node.MaterializedPath = $"{targetParent.MaterializedPath}/{node.Id}";
        node.Depth = targetParent.Depth + 1;
        node.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new { success = true });
    }

    /// <summary>
    /// Copies a file or folder to a target parent.
    /// </summary>
    [HttpPost("{nodeId:guid}/copy")]
    public async Task<IActionResult> CopyAsync(Guid nodeId, [FromBody] MoveNodeDto dto, [FromQuery] Guid userId)
    {
        var source = await _db.FileNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == nodeId);

        if (source is null)
            return NotFound(new { success = false, error = "Source node not found." });

        var targetParent = await _db.FileNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == dto.TargetParentId && n.NodeType == FileNodeType.Folder);

        if (targetParent is null)
            return NotFound(new { success = false, error = "Target folder not found." });

        var copy = new FileNode
        {
            Name = source.Name,
            NodeType = source.NodeType,
            MimeType = source.MimeType,
            Size = source.Size,
            ParentId = dto.TargetParentId,
            OwnerId = userId,
            ContentHash = source.ContentHash,
            StoragePath = source.StoragePath,
            CurrentVersion = 1,
            Depth = targetParent.Depth + 1
        };
        copy.MaterializedPath = $"{targetParent.MaterializedPath}/{copy.Id}";

        _db.FileNodes.Add(copy);
        await _db.SaveChangesAsync();

        return Created($"/api/v1/files/{copy.Id}", new { success = true, data = ToDto(copy) });
    }

    /// <summary>
    /// Moves a file or folder to trash (soft-delete).
    /// </summary>
    [HttpDelete("{nodeId:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid nodeId, [FromQuery] Guid userId)
    {
        var node = await _db.FileNodes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(n => n.Id == nodeId && !n.IsDeleted);

        if (node is null)
            return NotFound(new { success = false, error = "Node not found." });

        node.IsDeleted = true;
        node.DeletedAt = DateTime.UtcNow;
        node.DeletedByUserId = userId;
        node.OriginalParentId = node.ParentId;

        await _db.SaveChangesAsync();

        return Ok(new { success = true });
    }

    /// <summary>
    /// Toggles favorite status on a file or folder.
    /// </summary>
    [HttpPost("{nodeId:guid}/favorite")]
    public async Task<IActionResult> ToggleFavoriteAsync(Guid nodeId)
    {
        var node = await _db.FileNodes.FindAsync(nodeId);
        if (node is null)
            return NotFound(new { success = false, error = "Node not found." });

        node.IsFavorite = !node.IsFavorite;
        node.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { success = true, data = new { isFavorite = node.IsFavorite } });
    }

    /// <summary>
    /// Lists user's favorite files and folders.
    /// </summary>
    [HttpGet("favorites")]
    public async Task<IActionResult> ListFavoritesAsync([FromQuery] Guid userId)
    {
        var favorites = await _db.FileNodes
            .AsNoTracking()
            .Where(n => n.OwnerId == userId && n.IsFavorite)
            .OrderBy(n => n.Name)
            .Select(n => ToDto(n))
            .ToListAsync();

        return Ok(new { success = true, data = favorites });
    }

    private static FileNodeDto ToDto(FileNode node)
    {
        return new FileNodeDto
        {
            Id = node.Id,
            Name = node.Name,
            NodeType = node.NodeType.ToString(),
            MimeType = node.MimeType,
            Size = node.Size,
            ParentId = node.ParentId,
            OwnerId = node.OwnerId,
            CurrentVersion = node.CurrentVersion,
            IsFavorite = node.IsFavorite,
            ContentHash = node.ContentHash,
            CreatedAt = node.CreatedAt,
            UpdatedAt = node.UpdatedAt
        };
    }
}
