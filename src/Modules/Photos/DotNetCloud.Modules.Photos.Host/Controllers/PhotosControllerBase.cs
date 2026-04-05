using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Photos.Host.Controllers;

/// <summary>
/// Base controller for Photos module endpoints. Provides authentication helpers and response envelope methods.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = "Identity.Application,OpenIddict.Validation.AspNetCore")]
public abstract class PhotosControllerBase : ControllerBase
{
    /// <summary>
    /// Extracts the authenticated caller context from the current request.
    /// </summary>
    protected CallerContext GetAuthenticatedCaller()
    {
        if (User?.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(claimValue, out var userId))
        {
            throw new UnauthorizedAccessException("Authenticated user identifier is invalid.");
        }

        var roles = User.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CallerContext(userId, roles, CallerType.User);
    }

    /// <summary>
    /// Wraps data in a standard success envelope.
    /// </summary>
    protected static object Envelope(object data)
    {
        return new { success = true, data };
    }

    /// <summary>
    /// Wraps data and pagination in a standard success envelope.
    /// </summary>
    protected static object Envelope(object data, object pagination)
    {
        return new { success = true, data, pagination };
    }

    /// <summary>
    /// Creates a standard error envelope.
    /// </summary>
    protected static object ErrorEnvelope(string code, string message)
    {
        return new { success = false, error = new { code, message } };
    }
}
