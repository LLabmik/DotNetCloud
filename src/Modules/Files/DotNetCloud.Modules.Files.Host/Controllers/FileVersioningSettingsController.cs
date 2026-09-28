using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// Admin API exposing the <b>effective</b> file versioning policy and letting an administrator run
/// the retention pass on demand.
/// </summary>
/// <remarks>
/// The values come from configuration (<c>Files:VersionRetention</c>) with the administrator's
/// <c>/admin/files</c> edits layered on top, so the page shows what is actually enforced instead of
/// the defaults of an empty settings store. Reading invalidates the settings cache so an
/// administrator always sees the result of their last save.
/// </remarks>
[Route("api/v1/files/admin/versioning")]
[Authorize(Policy = "RequireAdmin")]
public sealed class FileVersioningSettingsController : FilesControllerBase
{
    private readonly IFileVersioningSettingsProvider _settings;
    private readonly IVersionRetentionService _retention;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileVersioningSettingsController"/> class.
    /// </summary>
    /// <param name="settings">Resolves the effective versioning policy.</param>
    /// <param name="retention">Applies the policy on demand.</param>
    public FileVersioningSettingsController(
        IFileVersioningSettingsProvider settings,
        IVersionRetentionService retention)
    {
        _settings = settings;
        _retention = retention;
    }

    /// <summary>Gets the effective file versioning settings.</summary>
    [HttpGet("effective")]
    public async Task<IActionResult> GetEffectiveAsync(CancellationToken cancellationToken)
    {
        _settings.Invalidate();
        var options = await _settings.GetAsync(cancellationToken);
        var effectiveMaxNumber = FileVersioningAdminSettings.EffectiveMaxVersionCount(options);

        return Ok(Envelope(new
        {
            enabled = options.Enabled,
            maxNumber = options.MaxVersionCount,
            maxDays = options.RetentionDays,
            effectiveMaxNumber,
            unlimitedVersions = effectiveMaxNumber <= 0,
            cleanupIntervalHours = options.CleanupInterval.TotalHours
        }));
    }

    /// <summary>
    /// Runs the version retention policy immediately instead of waiting for the next scheduled pass.
    /// </summary>
    [HttpPost("prune")]
    public async Task<IActionResult> PruneAsync(CancellationToken cancellationToken)
    {
        // Pick up an edit saved moments ago rather than the cached policy.
        _settings.Invalidate();
        var result = await _retention.RunAsync(cancellationToken);

        return Ok(Envelope(new
        {
            versioningEnabled = result.VersioningEnabled,
            filesScanned = result.FilesScanned,
            versionsDeleted = result.VersionsDeleted
        }));
    }
}
