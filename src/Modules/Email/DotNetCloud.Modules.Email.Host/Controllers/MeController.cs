using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Email.Host.Controllers;

/// <summary>
/// REST API controller for the acting user's identity inside the module UI.
/// </summary>
/// <remarks>
/// A sandboxed module frame has no cookie and no identity of its own: the core proxy attaches a
/// short-lived assertion per request. The UI still needs to know who it is acting as, so this endpoint
/// reports the asserting user. Only the id is available — this module declares no
/// <c>IUserDirectory</c> capability, so it cannot resolve a display name.
/// </remarks>
[Route("api")]
public class MeController : EmailControllerBase
{
    /// <summary>Returns the acting user's id.</summary>
    /// <returns>The caller's identity.</returns>
    [HttpGet("v1/me")]
    public IActionResult GetMe()
    {
        var caller = GetAuthenticatedCaller();

        return Ok(Envelope(new
        {
            userId = caller.UserId,
            displayName = (string?)null,
            email = (string?)null,
        }));
    }
}
