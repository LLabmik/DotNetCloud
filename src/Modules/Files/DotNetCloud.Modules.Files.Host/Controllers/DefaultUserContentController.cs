using DotNetCloud.Modules.Files.Data.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// Admin API for the administrator-managed default user content template.
/// The template is managed on the filesystem; this API is a read-only inspector plus
/// convergence/export controls.
/// </summary>
[Route("api/v1/files/admin/default-content")]
[Authorize(Policy = "RequireAdmin")]
public sealed class DefaultUserContentController : FilesControllerBase
{
    private readonly DefaultUserContentAdminService _adminService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultUserContentController"/> class.
    /// </summary>
    public DefaultUserContentController(DefaultUserContentAdminService adminService)
    {
        _adminService = adminService;
    }

    /// <summary>Effective state, statistics and validation summary.</summary>
    [HttpGet("effective")]
    public Task<IActionResult> GetEffectiveAsync() => ExecuteAsync(async () =>
    {
        var status = await _adminService.GetStatusAsync(HttpContext.RequestAborted);
        return Ok(Envelope(status));
    });

    /// <summary>Directory listing of the template with per-entry validation state.</summary>
    [HttpGet("browse")]
    public Task<IActionResult> BrowseAsync([FromQuery] string? path = null) => ExecuteAsync(async () =>
    {
        var result = await _adminService.BrowseAsync(path, HttpContext.RequestAborted);
        return Ok(Envelope(result));
    });

    /// <summary>Full validation issue list.</summary>
    [HttpGet("validate")]
    public Task<IActionResult> ValidateAsync() => ExecuteAsync(async () =>
    {
        var status = await _adminService.GetStatusAsync(HttpContext.RequestAborted);
        return Ok(Envelope(new
        {
            status.RootPath,
            status.RootExists,
            status.Readable,
            status.FolderCount,
            status.FileCount,
            status.TotalBytes,
            status.Issues
        }));
    });

    /// <summary>Adds the stock folders (additive only).</summary>
    [HttpPost("seed-defaults")]
    public Task<IActionResult> SeedDefaultsAsync() => ExecuteAsync(async () =>
    {
        var created = await _adminService.SeedDefaultsAsync(HttpContext.RequestAborted);
        return Ok(Envelope(new { created }));
    });

    /// <summary>
    /// Previews or runs convergence. Destructive actions return 409 until the matching
    /// confirmation flag is supplied.
    /// </summary>
    [HttpPost("reconcile")]
    public Task<IActionResult> ReconcileAsync(
        [FromQuery] bool dryRun = false,
        [FromQuery] bool publishUpdates = false,
        [FromQuery] bool overwriteModified = false,
        [FromQuery] bool confirmDeletions = false,
        [FromQuery] bool confirmOverwrites = false) => ExecuteAsync(async () =>
    {
        var caller = GetAuthenticatedCaller();
        var result = await _adminService.ReconcileAsync(
            publishUpdates, overwriteModified, confirmDeletions, confirmOverwrites,
            dryRun, caller, HttpContext.RequestAborted);

        if (result.RequiresConfirmation)
        {
            return StatusCode(StatusCodes.Status409Conflict, Envelope(new
            {
                requiresConfirmation = true,
                result.Reason,
                result.Adds,
                result.Updates,
                result.Overwrites,
                result.Deletes,
                result.SamplePaths
            }));
        }

        return Ok(Envelope(result));
    });

    /// <summary>Queue counts by status.</summary>
    [HttpGet("reconcile-status")]
    public Task<IActionResult> GetReconcileStatusAsync() => ExecuteAsync(async () =>
    {
        var status = await _adminService.GetQueueStatusAsync(HttpContext.RequestAborted);
        return Ok(Envelope(status));
    });

    /// <summary>Users whose convergence failed after the retry cap.</summary>
    [HttpGet("failed-users")]
    public Task<IActionResult> GetFailedUsersAsync() => ExecuteAsync(async () =>
    {
        var users = await _adminService.GetFailedUsersAsync(HttpContext.RequestAborted);
        return Ok(Envelope(users));
    });

    /// <summary>Dry run for one user.</summary>
    [HttpGet("preview")]
    public Task<IActionResult> PreviewAsync([FromQuery] Guid? userId = null) => ExecuteAsync(async () =>
    {
        var preview = await _adminService.PreviewAsync(userId, HttpContext.RequestAborted);
        return Ok(Envelope(preview));
    });

    /// <summary>Template-only ZIP export.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> ExportAsync()
    {
        FileStream? stream = null;
        try
        {
            // Stream through a delete-on-close temp file: a template may be several gigabytes,
            // so buffering the whole archive in memory would be unsafe.
            var tempPath = Path.Combine(Path.GetTempPath(), $"dnc-default-content-{Guid.CreateVersion7():N}.zip");
            stream = new FileStream(
                tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.DeleteOnClose);

            await _adminService.ExportAsync(stream, HttpContext.RequestAborted);
            stream.Position = 0;

            return File(stream, "application/zip", "default-user-content.zip");
        }
        catch (Exception ex)
        {
            if (stream is not null)
                await stream.DisposeAsync();

            return StatusCode(500, ErrorEnvelope("INTERNAL_ERROR", ex.Message));
        }
    }
}
