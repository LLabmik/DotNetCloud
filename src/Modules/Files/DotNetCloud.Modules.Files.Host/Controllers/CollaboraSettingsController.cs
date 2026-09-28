using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// Admin API exposing the <b>effective</b> Collabora configuration.
/// </summary>
/// <remarks>
/// The values come from configuration (<c>Files:Collabora:*</c>) with the administrator's
/// <c>/admin/collabora</c> edits layered on top, so the page shows what is actually in force
/// instead of the defaults of an empty settings store. Reading invalidates the settings cache so an
/// administrator always sees the result of their last save.
/// </remarks>
[Route("api/v1/files/admin/collabora")]
[Authorize(Policy = "RequireAdmin")]
public sealed class CollaboraSettingsController : FilesControllerBase
{
    private readonly ICollaboraSettingsProvider _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="CollaboraSettingsController"/> class.
    /// </summary>
    /// <param name="settings">Resolves the effective Collabora options.</param>
    public CollaboraSettingsController(ICollaboraSettingsProvider settings)
    {
        _settings = settings;
    }

    /// <summary>Gets the effective Collabora settings.</summary>
    [HttpGet("effective")]
    public async Task<IActionResult> GetEffectiveAsync(CancellationToken cancellationToken)
    {
        _settings.Invalidate();
        var options = await _settings.GetAsync(cancellationToken);

        return Ok(Envelope(new
        {
            enabled = options.Enabled,
            serverUrl = options.ServerUrl,
            discoveryUrl = options.DiscoveryUrl,
            wopiBaseUrl = options.WopiBaseUrl,
            hasTokenSigningKey = !string.IsNullOrWhiteSpace(options.TokenSigningKey),
            tokenLifetimeMinutes = options.TokenLifetimeMinutes,
            enableProofKeyValidation = options.EnableProofKeyValidation,
            autoSaveIntervalSeconds = options.AutoSaveIntervalSeconds,
            maxConcurrentSessions = options.MaxConcurrentSessions,
            supportedMimeTypes = string.Join(", ", options.SupportedMimeTypes),
            useBuiltInCollabora = options.UseBuiltInCollabora,
            allowInsecureTls = options.AllowInsecureTls,
            collaboraInstallDirectory = options.CollaboraInstallDirectory,
            collaboraExecutablePath = options.CollaboraExecutablePath,
            collaboraMaxRestartAttempts = options.CollaboraMaxRestartAttempts,
            collaboraRestartBackoffSeconds = options.CollaboraRestartBackoffSeconds
        }));
    }
}
