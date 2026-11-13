using DotNetCloud.Core.Capabilities;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for the acting user's identity inside the module UI.
/// </summary>
/// <remarks>
/// A sandboxed module frame has no cookie and no identity of its own: the core proxy attaches a
/// short-lived assertion per request. The UI still needs to know who it is acting as (onboarding
/// tours, mention highlighting), so this endpoint reports the asserting user.
/// </remarks>
[Route("api")]
public class MeController : TracksControllerBase
{
    private readonly IUserDirectory _userDirectory;
    private readonly ILogger<MeController> _logger;

    /// <summary>Initializes a new instance of the <see cref="MeController"/> class.</summary>
    /// <param name="userDirectory">Capability client used to resolve the caller's display name.</param>
    /// <param name="logger">Logger.</param>
    public MeController(IUserDirectory userDirectory, ILogger<MeController> logger)
    {
        _userDirectory = userDirectory;
        _logger = logger;
    }

    /// <summary>Returns the acting user's id and display name.</summary>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("v1/me")]
    public async Task<IActionResult> GetMeAsync(CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();

        string? displayName = null;
        try
        {
            var names = await _userDirectory.GetDisplayNamesAsync([caller.UserId], ct);
            displayName = names.TryGetValue(caller.UserId, out var name) ? name : null;
        }
        catch (Exception ex)
        {
            // A display name is cosmetic; the id alone is enough for the UI to function.
            _logger.LogDebug(ex, "Could not resolve the acting user's display name");
        }

        return Ok(Envelope(new
        {
            userId = caller.UserId,
            displayName,
            email = (string?)null,
        }));
    }
}
