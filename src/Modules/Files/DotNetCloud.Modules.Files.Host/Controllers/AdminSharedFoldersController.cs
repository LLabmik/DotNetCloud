using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// Admin API for managing Files admin shared-folder definitions and scan controls.
/// </summary>
[Route("api/v1/files/admin/shared-folders")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminSharedFoldersController : FilesControllerBase
{
    private readonly IAdminSharedFolderService _adminSharedFolderService;
    private readonly FilesDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminSharedFoldersController"/> class.
    /// </summary>
    public AdminSharedFoldersController(
        IAdminSharedFolderService adminSharedFolderService,
        FilesDbContext db)
    {
        _adminSharedFolderService = adminSharedFolderService;
        _db = db;
    }

    /// <summary>
    /// Lists all registered admin shared folders.
    /// </summary>
    [HttpGet]
    public Task<IActionResult> ListAsync() => ExecuteAsync(async () =>
    {
        var caller = GetAuthenticatedCaller();
        var sharedFolders = await _adminSharedFolderService.GetSharedFoldersAsync(caller, HttpContext.RequestAborted);
        return Ok(Envelope(sharedFolders));
    });

    /// <summary>
    /// Gets a single admin shared folder definition.
    /// </summary>
    [HttpGet("{sharedFolderId:guid}")]
    public Task<IActionResult> GetAsync(Guid sharedFolderId) => ExecuteAsync(async () =>
    {
        var caller = GetAuthenticatedCaller();
        var sharedFolder = await _adminSharedFolderService.GetSharedFolderAsync(sharedFolderId, caller, HttpContext.RequestAborted);
        return Ok(Envelope(sharedFolder));
    });

    /// <summary>
    /// Browses directories beneath the local filesystem root.
    /// </summary>
    [HttpGet("browse")]
    public Task<IActionResult> BrowseAsync([FromQuery] string? path = null) => ExecuteAsync(async () =>
    {
        var caller = GetAuthenticatedCaller();
        var browseResult = await _adminSharedFolderService.BrowseDirectoriesAsync(path, caller, HttpContext.RequestAborted);
        return Ok(Envelope(browseResult));
    });

    /// <summary>
    /// Creates a new admin shared folder definition.
    /// </summary>
    [HttpPost]
    public Task<IActionResult> CreateAsync([FromBody] CreateAdminSharedFolderDto dto) => ExecuteAsync(async () =>
    {
        var caller = GetAuthenticatedCaller();
        var sharedFolder = await _adminSharedFolderService.CreateSharedFolderAsync(dto, caller, HttpContext.RequestAborted);
        return Created($"/api/v1/files/admin/shared-folders/{sharedFolder.Id}", Envelope(sharedFolder));
    });

    /// <summary>
    /// Updates an existing admin shared folder definition.
    /// </summary>
    [HttpPut("{sharedFolderId:guid}")]
    public Task<IActionResult> UpdateAsync(Guid sharedFolderId, [FromBody] UpdateAdminSharedFolderDto dto) => ExecuteAsync(async () =>
    {
        var caller = GetAuthenticatedCaller();
        var sharedFolder = await _adminSharedFolderService.UpdateSharedFolderAsync(sharedFolderId, dto, caller, HttpContext.RequestAborted);
        return Ok(Envelope(sharedFolder));
    });

    /// <summary>
    /// Deletes an admin shared folder definition and initiates cleanup of search documents
    /// and orphaned media entities.
    /// </summary>
    [HttpDelete("{sharedFolderId:guid}")]
    public Task<IActionResult> DeleteAsync(Guid sharedFolderId) => ExecuteAsync(async () =>
    {
        var caller = GetAuthenticatedCaller();
        var result = await _adminSharedFolderService.DeleteSharedFolderAsync(sharedFolderId, caller, HttpContext.RequestAborted);
        return Ok(Envelope(result));
    });

    /// <summary>
    /// Gets the cleanup status for a deleted admin shared folder.
    /// </summary>
    [HttpGet("cleanup-status/{cleanupJobId:guid}")]
    public Task<IActionResult> GetCleanupStatusAsync(Guid cleanupJobId) => ExecuteAsync(async () =>
    {
        var status = await _db.AdminSharedFolderCleanupStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CleanupJobId == cleanupJobId,
                HttpContext.RequestAborted);

        if (status is null)
            return NotFound(ErrorEnvelope("CleanupJobNotFound", "Cleanup job not found."));

        return Ok(Envelope(new
        {
            status.CleanupJobId,
            status.SharedFolderId,
            status.DisplayName,
            Phase = status.Phase.ToString(),
            status.SearchDocsRemoved,
            status.SearchDocsTotal,
            status.AffectedUsers,
            status.UsersCleaned,
            status.MediaEntitiesRemoved,
            status.StartedAt,
            status.CompletedAt,
            status.ErrorMessage,
            status.IsComplete,
        }));
    });

    /// <summary>
    /// Requests a full reindex for an admin shared folder.
    /// </summary>
    [HttpPost("{sharedFolderId:guid}/reindex")]
    public Task<IActionResult> RequestReindexAsync(Guid sharedFolderId) => ExecuteAsync(async () =>
    {
        var caller = GetAuthenticatedCaller();
        var sharedFolder = await _adminSharedFolderService.RequestReindexAsync(sharedFolderId, caller, HttpContext.RequestAborted);
        return Ok(Envelope(sharedFolder));
    });

    /// <summary>
    /// Schedules or expedites a shared-folder rescan.
    /// </summary>
    [HttpPost("{sharedFolderId:guid}/rescan")]
    public Task<IActionResult> ScheduleRescanAsync(Guid sharedFolderId, [FromBody] ScheduleAdminSharedFolderRescanDto? dto = null) => ExecuteAsync(async () =>
    {
        var caller = GetAuthenticatedCaller();
        var sharedFolder = await _adminSharedFolderService.ScheduleRescanAsync(sharedFolderId, dto?.NextScheduledScanAt, caller, HttpContext.RequestAborted);
        return Ok(Envelope(sharedFolder));
    });
}
