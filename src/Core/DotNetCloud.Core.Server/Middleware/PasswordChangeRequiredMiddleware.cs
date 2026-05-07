using DotNetCloud.Core.Data.Entities.Identity;
using Microsoft.AspNetCore.Identity;

namespace DotNetCloud.Core.Server.Middleware;

/// <summary>
/// Middleware that enforces password change for users with <see cref="ApplicationUser.PasswordChangeRequired"/> set.
/// Redirects authenticated users to the change-password page unless the request path is in the allowed list.
/// </summary>
public sealed class PasswordChangeRequiredMiddleware
{
    private readonly RequestDelegate _next;

    // Path prefixes that remain accessible even when PasswordChangeRequired is true.
    // Includes auth pages, API endpoints, OpenIddict, and static assets needed to render the change-password page.
    private static readonly HashSet<string> AllowedPathPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/auth/change-password",
        "/auth/logout",
        "/auth/session/change-password",
        "/api/",
        "/connect/",
        "/css/",
        "/js/",
        "/_framework/",
        "/_content/",
        "/favicon.ico",
        "/health",
        "/metrics",
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="PasswordChangeRequiredMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    public PasswordChangeRequiredMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="userManager">The ASP.NET Core Identity user manager, injected by the middleware pipeline.</param>
    /// <returns>A task representing the middleware execution.</returns>
    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager)
    {
        // Only enforce for authenticated users
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var path = context.Request.Path.Value ?? string.Empty;

            if (!IsPathAllowed(path))
            {
                var user = await userManager.GetUserAsync(context.User);
                if (user?.PasswordChangeRequired == true)
                {
                    // Preserve the original return URL through the redirect chain
                    var returnUrl = Uri.EscapeDataString(
                        path + context.Request.QueryString.ToUriComponent());
                    context.Response.Redirect($"/auth/change-password?returnUrl={returnUrl}");
                    return;
                }
            }
        }

        await _next(context);
    }

    /// <summary>
    /// Determines whether the given request path is in the allowed list.
    /// </summary>
    private static bool IsPathAllowed(string path)
    {
        foreach (var prefix in AllowedPathPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
