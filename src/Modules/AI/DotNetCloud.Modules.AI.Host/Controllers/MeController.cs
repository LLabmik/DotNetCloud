using DotNetCloud.Core.Grpc;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.AI.Host.Controllers;

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
public class MeController : ControllerBase
{
    private readonly IModuleUserAssertionAccessor _userAssertion;

    /// <summary>Initializes a new instance of the <see cref="MeController"/> class.</summary>
    /// <param name="userAssertion">The verified assertion of the proxied request.</param>
    public MeController(IModuleUserAssertionAccessor userAssertion)
    {
        _userAssertion = userAssertion;
    }

    /// <summary>Returns the acting user's id.</summary>
    /// <returns>The caller's identity.</returns>
    [HttpGet("v1/me")]
    public IActionResult GetMe()
    {
        var userId = _userAssertion.GetCurrentUserId();
        if (userId is not { } value || value == Guid.Empty)
        {
            return Unauthorized();
        }

        return Ok(new
        {
            userId = value,
            displayName = (string?)null,
            email = (string?)null,
        });
    }
}
