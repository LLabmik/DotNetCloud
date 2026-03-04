using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// REST API controller for trash bin operations.
/// </summary>
[ApiController]
[Route("api/v1/files/trash")]
public class TrashController : ControllerBase
{
    private readonly FilesDbContext _db;
    private readonly ILogger<TrashController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrashController"/> class.
    /// </summary>
    public TrashController(FilesDbContext db, ILogger<TrashController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Lists all items in the trash bin for a user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] Guid userId)
    {
        var trashedNodes = await _db.FileNodes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(n => n.OwnerId == userId && n.IsDeleted)
            .OrderByDescending(n => n.DeletedAt)
            .Select(n => new TrashItemDto
            {
                Id = n.Id,
                Name = n.Name,
                NodeType = n.NodeType.ToString(),
                Size = n.Size,
                MimeType = n.MimeType,
                DeletedAt = n.DeletedAt,
                DeletedByUserId = n.DeletedByUserId
            })
            .ToListAsync();

        return Ok(new { success = true, data = trashedNodes });
    }

    /// <summary>
    /// Restores a trashed item to its original location.
    /// </summary>
    [HttpPost("{nodeId:guid}/restore")]
    public async Task<IActionResult> RestoreAsync(Guid nodeId)
    {
        var node = await _db.FileNodes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(n => n.Id == nodeId && n.IsDeleted);

        if (node is null)
            return NotFound(new { success = false, error = "Trashed item not found." });

        node.IsDeleted = false;
        node.DeletedAt = null;
        node.DeletedByUserId = null;
        node.ParentId = node.OriginalParentId;
        node.OriginalParentId = null;
        node.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Node {NodeId} restored from trash", nodeId);

        return Ok(new { success = true });
    }

    /// <summary>
    /// Permanently deletes a trashed item.
    /// </summary>
    [HttpDelete("{nodeId:guid}")]
    public async Task<IActionResult> PurgeAsync(Guid nodeId)
    {
        var node = await _db.FileNodes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(n => n.Id == nodeId && n.IsDeleted);

        if (node is null)
            return NotFound(new { success = false, error = "Trashed item not found." });

        _db.FileNodes.Remove(node);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Node {NodeId} permanently deleted", nodeId);

        return Ok(new { success = true });
    }

    /// <summary>
    /// Empties the entire trash bin for a user.
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> EmptyAsync([FromQuery] Guid userId)
    {
        var trashedNodes = await _db.FileNodes
            .IgnoreQueryFilters()
            .Where(n => n.OwnerId == userId && n.IsDeleted)
            .ToListAsync();

        _db.FileNodes.RemoveRange(trashedNodes);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Trash emptied for user {UserId}: {Count} items purged", userId, trashedNodes.Count);

        return Ok(new { success = true, data = new { purgedCount = trashedNodes.Count } });
    }
}
