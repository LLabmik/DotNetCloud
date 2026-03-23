using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Validation.AspNetCore;

namespace DotNetCloud.Modules.Notes.Host.Controllers;

/// <summary>
/// Base controller for Notes module endpoints. Provides authentication helpers and response envelope methods.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
public abstract class NotesControllerBase : ControllerBase
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

    /// <summary>Wraps data in a success envelope.</summary>
    protected static object Envelope(object data) => new { success = true, data };

    /// <summary>Creates an error envelope.</summary>
    protected static object ErrorEnvelope(string code, string message)
        => new { success = false, error = new { code, message } };
}
