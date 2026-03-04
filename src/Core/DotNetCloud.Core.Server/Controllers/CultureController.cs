using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Core.Server.Controllers;

/// <summary>
/// Sets the user's preferred culture via a localization cookie and redirects back.
/// Used by the Blazor <c>CultureSelector</c> component for both SSR and CSR scenarios.
/// </summary>
[Route("[controller]/[action]")]
public class CultureController : Controller
{
    /// <summary>
    /// Persists the selected culture in the ASP.NET Core localization cookie
    /// and redirects to the original URI.
    /// </summary>
    /// <param name="culture">BCP-47 language tag (e.g. "en-US", "es-ES").</param>
    /// <param name="redirectUri">The URI to redirect back to after setting the culture.</param>
    [HttpGet]
    public IActionResult Set(string culture, string redirectUri)
    {
        if (!string.IsNullOrWhiteSpace(culture))
        {
            HttpContext.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(
                    new RequestCulture(culture, culture)));
        }

        return LocalRedirect(redirectUri);
    }
}
