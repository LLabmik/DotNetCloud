using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// REST API controller for file version management.
/// </summary>
[Route("api/v1/files/{nodeId:guid}/versions")]
public class VersionController : FilesControllerBase
{
    private readonly IVersionService _versionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionController"/> class.
    /// </summary>
    public VersionController(IVersionService versionService)
    {
        _versionService = versionService;
    }

    /// <summary>
    /// Lists all versions of a file.
    /// </summary>
    [HttpGet]
    public Task<IActionResult> ListAsync(Guid nodeId, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var versions = await _versionService.ListVersionsAsync(nodeId, ToCaller(userId));
        return Ok(Envelope(versions));
    });

    /// <summary>
    /// Gets a specific version by version number.
    /// </summary>
    [HttpGet("{versionNumber:int}")]
    public Task<IActionResult> GetAsync(Guid nodeId, int versionNumber, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var version = await _versionService.GetVersionByNumberAsync(nodeId, versionNumber, ToCaller(userId));
        return version is null
            ? NotFound(ErrorEnvelope("not_found", "Version not found."))
            : Ok(Envelope(version));
    });

    /// <summary>
    /// Restores a file to a previous version.
    /// </summary>
    [HttpPost("{versionNumber:int}/restore")]
    public Task<IActionResult> RestoreAsync(Guid nodeId, int versionNumber, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var caller = ToCaller(userId);
        var version = await _versionService.GetVersionByNumberAsync(nodeId, versionNumber, caller);
        if (version is null)
            return NotFound(ErrorEnvelope("not_found", "Version not found."));

        var restored = await _versionService.RestoreVersionAsync(nodeId, version.Id, caller);
        return Ok(Envelope(restored));
    });

    /// <summary>
    /// Deletes a specific version.
    /// </summary>
    [HttpDelete("{versionNumber:int}")]
    public Task<IActionResult> DeleteAsync(Guid nodeId, int versionNumber, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var caller = ToCaller(userId);
        var version = await _versionService.GetVersionByNumberAsync(nodeId, versionNumber, caller);
        if (version is null)
            return NotFound(ErrorEnvelope("not_found", "Version not found."));

        await _versionService.DeleteVersionAsync(version.Id, caller);
        return Ok(Envelope(new { deleted = true }));
    });

    /// <summary>
    /// Labels a version with a descriptive name.
    /// </summary>
    [HttpPut("{versionNumber:int}/label")]
    public Task<IActionResult> LabelAsync(Guid nodeId, int versionNumber, [FromBody] LabelVersionDto dto, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var caller = ToCaller(userId);
        var version = await _versionService.GetVersionByNumberAsync(nodeId, versionNumber, caller);
        if (version is null)
            return NotFound(ErrorEnvelope("not_found", "Version not found."));

        var labeled = await _versionService.LabelVersionAsync(version.Id, dto.Label, caller);
        return Ok(Envelope(labeled));
    });
}
