using DotNetCloud.Core.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Validation.AspNetCore;
using System.Security.Claims;

namespace DotNetCloud.Modules.Contacts.Host.Controllers;

/// <summary>
/// Base controller for all Contacts module REST API controllers.
/// Provides helper methods for caller context creation and envelope responses.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = "Identity.Application,OpenIddict.Validation.AspNetCore")]
public abstract class ContactsControllerBase : ControllerBase
{
    /// <summary>
    /// Creates a <see cref="CallerContext"/> from the authenticated bearer token claims.
    /// </summary>
    protected CallerContext GetAuthenticatedCaller()
    {
        if (User?.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException("Authentication is required.");

        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(claimValue, out var userId))
            throw new UnauthorizedAccessException("Authenticated user identifier is invalid.");

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
    /// Creates a standard error envelope.
    /// </summary>
    protected static object ErrorEnvelope(string code, string message)
    {
        return new { success = false, error = new { code, message } };
    }
}
