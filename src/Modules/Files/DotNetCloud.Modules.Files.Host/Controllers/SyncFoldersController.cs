using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// REST API controller for server-side sync folder registrations.
/// The SyncTray client registers/unregisters the remote folder each local sync folder maps to.
/// </summary>
[Route("api/v1/files/sync/folders")]
[Authorize]
public class SyncFoldersController : FilesControllerBase
{
    private readonly ISyncFolderRegistrationService _registrationService;
    private readonly ILogger<SyncFoldersController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncFoldersController"/> class.
    /// </summary>
    public SyncFoldersController(ISyncFolderRegistrationService registrationService, ILogger<SyncFoldersController> logger)
    {
        _registrationService = registrationService;
        _logger = logger;
    }

    /// <summary>
    /// Lists the active sync folder registrations for the authenticated user.
    /// </summary>
    [HttpGet]
    public Task<IActionResult> ListAsync() => ExecuteAsync(async () =>
    {
        var registrations = await _registrationService.ListAsync(GetAuthenticatedCaller());
        return Ok(Envelope(registrations));
    });

    /// <summary>
    /// Registers a remote folder as a sync target for the authenticated user.
    /// </summary>
    [HttpPost]
    public Task<IActionResult> RegisterAsync([FromBody] SyncFolderRegistrationRequestDto dto) => ExecuteAsync(async () =>
    {
        if (dto is null || dto.RemoteFolderNodeId == Guid.Empty)
        {
            return BadRequest(ErrorEnvelope("invalid_request", "A remoteFolderNodeId is required."));
        }

        var registration = await _registrationService.RegisterAsync(dto.RemoteFolderNodeId, GetAuthenticatedCaller());
        return Ok(Envelope(registration));
    });

    /// <summary>
    /// Removes the registration for the given remote folder for the authenticated user.
    /// </summary>
    [HttpDelete("{remoteFolderNodeId:guid}")]
    public Task<IActionResult> UnregisterAsync(Guid remoteFolderNodeId) => ExecuteAsync(async () =>
    {
        await _registrationService.UnregisterAsync(remoteFolderNodeId, GetAuthenticatedCaller());
        return Ok(Envelope(new { deleted = true }));
    });
}
