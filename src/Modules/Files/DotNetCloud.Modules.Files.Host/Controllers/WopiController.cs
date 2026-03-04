using DotNetCloud.Modules.Files.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// REST API controller for WOPI (Web Application Open Platform Interface) integration.
/// Enables Collabora Online/CODE to fetch and save files for browser-based document editing.
/// </summary>
/// <remarks>
/// WOPI endpoints:
/// - GET  /api/v1/wopi/files/{fileId}          → File info (CheckFileInfo)
/// - GET  /api/v1/wopi/files/{fileId}/contents  → Download file (GetFile)
/// - POST /api/v1/wopi/files/{fileId}/contents  → Save file (PutFile)
/// </remarks>
[ApiController]
[Route("api/v1/wopi/files")]
public class WopiController : ControllerBase
{
    private readonly FilesDbContext _db;
    private readonly ILogger<WopiController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WopiController"/> class.
    /// </summary>
    public WopiController(FilesDbContext db, ILogger<WopiController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// WOPI CheckFileInfo — Returns metadata about a file.
    /// Called by Collabora when opening a document for editing.
    /// </summary>
    [HttpGet("{fileId:guid}")]
    public async Task<IActionResult> CheckFileInfoAsync(Guid fileId)
    {
        var node = await _db.FileNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == fileId);

        if (node is null)
            return NotFound();

        // WOPI CheckFileInfo response
        // See: https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo
        return Ok(new
        {
            BaseFileName = node.Name,
            OwnerId = node.OwnerId.ToString(),
            Size = node.Size,
            Version = node.CurrentVersion.ToString(),
            UserCanWrite = true,
            UserCanNotWriteRelative = false,
            SupportsUpdate = true,
            SupportsLocks = false,
            SHA256 = node.ContentHash ?? string.Empty,
            LastModifiedTime = node.UpdatedAt.ToString("O")
        });
    }

    /// <summary>
    /// WOPI GetFile — Returns the file content.
    /// Called by Collabora to load a document for editing.
    /// </summary>
    [HttpGet("{fileId:guid}/contents")]
    public async Task<IActionResult> GetFileAsync(Guid fileId)
    {
        var node = await _db.FileNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == fileId);

        if (node is null)
            return NotFound();

        // In a full implementation, read the file from storage using node.StoragePath
        // For now, return an empty response with the correct content type
        _logger.LogInformation("WOPI GetFile: {FileId} ({FileName})", fileId, node.Name);

        return File(Array.Empty<byte>(), node.MimeType ?? "application/octet-stream", node.Name);
    }

    /// <summary>
    /// WOPI PutFile — Saves edited file content.
    /// Called by Collabora when the user saves a document.
    /// Creates a new file version.
    /// </summary>
    [HttpPost("{fileId:guid}/contents")]
    public async Task<IActionResult> PutFileAsync(Guid fileId)
    {
        var node = await _db.FileNodes.FindAsync(fileId);
        if (node is null)
            return NotFound();

        // In a full implementation:
        // 1. Read the request body
        // 2. Chunk and hash the content
        // 3. Store to disk
        // 4. Create new FileVersion
        // 5. Update node metadata

        node.CurrentVersion++;
        node.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("WOPI PutFile: {FileId} ({FileName}) -> v{Version}",
            fileId, node.Name, node.CurrentVersion);

        return Ok();
    }
}
