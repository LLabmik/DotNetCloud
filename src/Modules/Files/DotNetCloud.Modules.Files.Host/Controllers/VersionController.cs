using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// REST API controller for file version management.
/// </summary>
[ApiController]
[Route("api/v1/files/{nodeId:guid}/versions")]
public class VersionController : ControllerBase
{
    private readonly FilesDbContext _db;
    private readonly ILogger<VersionController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionController"/> class.
    /// </summary>
    public VersionController(FilesDbContext db, ILogger<VersionController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Lists all versions of a file.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListAsync(Guid nodeId)
    {
        var versions = await _db.FileVersions
            .AsNoTracking()
            .Where(v => v.FileNodeId == nodeId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new FileVersionDto
            {
                Id = v.Id,
                VersionNumber = v.VersionNumber,
                Size = v.Size,
                ContentHash = v.ContentHash,
                MimeType = v.MimeType,
                CreatedByUserId = v.CreatedByUserId,
                CreatedAt = v.CreatedAt,
                Label = v.Label
            })
            .ToListAsync();

        return Ok(new { success = true, data = versions });
    }

    /// <summary>
    /// Restores a file to a previous version.
    /// Creates a new version with the content from the specified version.
    /// </summary>
    [HttpPost("{versionNumber:int}/restore")]
    public async Task<IActionResult> RestoreAsync(Guid nodeId, int versionNumber, [FromQuery] Guid userId)
    {
        var version = await _db.FileVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.FileNodeId == nodeId && v.VersionNumber == versionNumber);

        if (version is null)
            return NotFound(new { success = false, error = "Version not found." });

        var node = await _db.FileNodes.FindAsync(nodeId);
        if (node is null)
            return NotFound(new { success = false, error = "File not found." });

        var maxVersion = await _db.FileVersions
            .Where(v => v.FileNodeId == nodeId)
            .MaxAsync(v => v.VersionNumber);

        var restoredVersion = new FileVersion
        {
            FileNodeId = nodeId,
            VersionNumber = maxVersion + 1,
            Size = version.Size,
            ContentHash = version.ContentHash,
            StoragePath = version.StoragePath,
            MimeType = version.MimeType,
            CreatedByUserId = userId,
            Label = $"Restored from v{version.VersionNumber}"
        };
        _db.FileVersions.Add(restoredVersion);

        node.ContentHash = version.ContentHash;
        node.StoragePath = version.StoragePath;
        node.Size = version.Size;
        node.CurrentVersion = restoredVersion.VersionNumber;
        node.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("File {NodeId} restored to version {Version}", nodeId, versionNumber);

        return Ok(new { success = true, data = new { newVersion = restoredVersion.VersionNumber } });
    }
}
