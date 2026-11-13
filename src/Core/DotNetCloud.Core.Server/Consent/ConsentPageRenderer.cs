using System.Text;
using System.Text.Encodings.Web;
using DotNetCloud.Core.Modules;

namespace DotNetCloud.Core.Server.Consent;

/// <summary>
/// Renders the consent prompt served by the core's own chrome.
/// </summary>
/// <remarks>
/// <para>
/// This page is deliberately not a Blazor component. It is the final step of an OAuth2
/// authorization request: the response has to be produced by the same endpoint pipeline that will
/// later call <c>Results.SignIn</c>, and it must be a plain, script-free form POST so that
/// anti-forgery validation and the <c>frame-ancestors 'none'</c> policy apply without an
/// interactive circuit in the way. It is core chrome — never a module iframe — so a module cannot
/// draw or clickjack its own consent prompt.
/// </para>
/// <para>
/// Every dynamic value goes through <see cref="HtmlEncoder"/>; the markup contains no inline
/// script and no external resource, so the page is safe under
/// <c>default-src 'none'; style-src 'unsafe-inline'</c>.
/// </para>
/// </remarks>
internal static class ConsentPageRenderer
{
    /// <summary>Renders the prompt as a complete HTML document.</summary>
    /// <param name="request">The pending consent request.</param>
    /// <param name="antiforgeryFieldName">The anti-forgery form field name.</param>
    /// <param name="antiforgeryToken">The anti-forgery token value.</param>
    /// <param name="formAction">The absolute path the form posts to.</param>
    /// <returns>The HTML document.</returns>
    public static string Render(
        PendingConsentRequest request,
        string antiforgeryFieldName,
        string antiforgeryToken,
        string formAction)
    {
        ArgumentNullException.ThrowIfNull(request);

        var html = new StringBuilder(4096);
        var clientName = HtmlEncoder.Default.Encode(request.ClientDisplayName);

        html.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">")
            .Append("<meta name=\"robots\" content=\"noindex, nofollow\">")
            .Append("<title>Authorize ").Append(clientName).Append(" — DotNetCloud</title>")
            .Append("<style>")
            .Append(":root{color-scheme:light dark}")
            .Append("*{box-sizing:border-box}")
            .Append("body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;")
            .Append("background:#0f1115;color:#e6e8eb;font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;padding:24px}")
            .Append("main{width:100%;max-width:32rem;background:#171a21;border:1px solid #2a2f3a;border-radius:12px;padding:28px}")
            .Append("h1{font-size:1.25rem;margin:0 0 8px}")
            .Append("p{margin:0 0 16px;line-height:1.5;color:#b9c0cc}")
            .Append("ul{margin:0 0 20px;padding-left:1.25rem;line-height:1.6}")
            .Append("li{margin-bottom:4px}")
            .Append("code{background:#0f1115;border:1px solid #2a2f3a;border-radius:4px;padding:1px 5px;font-size:.85em}")
            .Append(".actions{display:flex;gap:12px;margin-top:20px}")
            .Append("button{flex:1;padding:11px 16px;border-radius:8px;border:1px solid #2a2f3a;")
            .Append("font-size:1rem;font-weight:600;cursor:pointer}")
            .Append(".allow{background:#2f6feb;border-color:#2f6feb;color:#fff}")
            .Append(".deny{background:transparent;color:#e6e8eb}")
            .Append(".hint{margin-top:18px;font-size:.82rem;color:#8b93a1}")
            .Append("</style></head><body><main>")
            .Append("<h1>Authorize ").Append(clientName).Append("</h1>")
            .Append("<p><strong>").Append(clientName)
            .Append("</strong> is asking for permission to use your DotNetCloud account.</p>")
            .Append("<p>It will be able to:</p><ul>");

        foreach (var scope in request.Scopes)
        {
            html.Append("<li>")
                .Append(HtmlEncoder.Default.Encode(ModuleScopes.Describe(scope)))
                .Append(" <code>")
                .Append(HtmlEncoder.Default.Encode(scope))
                .Append("</code></li>");
        }

        html.Append("</ul>")
            .Append("<form method=\"post\" action=\"")
            .Append(HtmlEncoder.Default.Encode(formAction))
            .Append("\" autocomplete=\"off\">")
            .Append("<input type=\"hidden\" name=\"")
            .Append(HtmlEncoder.Default.Encode(antiforgeryFieldName))
            .Append("\" value=\"")
            .Append(HtmlEncoder.Default.Encode(antiforgeryToken))
            .Append("\">")
            .Append("<input type=\"hidden\" name=\"rid\" value=\"")
            .Append(HtmlEncoder.Default.Encode(request.RequestId))
            .Append("\">")
            .Append("<div class=\"actions\">")
            .Append("<button class=\"allow\" type=\"submit\" name=\"decision\" value=\"allow\">Allow</button>")
            .Append("<button class=\"deny\" type=\"submit\" name=\"decision\" value=\"deny\">Deny</button>")
            .Append("</div></form>")
            .Append("<p class=\"hint\">You can review and revoke this access at any time from ")
            .Append("Profile &rarr; Connected apps.</p>")
            .Append("</main></body></html>");

        return html.ToString();
    }

    /// <summary>
    /// Renders the error page shown when a prompt is unknown, expired or already answered.
    /// </summary>
    /// <param name="message">The human-readable reason.</param>
    /// <returns>The HTML document.</returns>
    public static string RenderError(string message)
    {
        var encoded = HtmlEncoder.Default.Encode(message);

        return "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">"
            + "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">"
            + "<meta name=\"robots\" content=\"noindex, nofollow\">"
            + "<title>Authorization request expired — DotNetCloud</title>"
            + "<style>:root{color-scheme:light dark}"
            + "body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;"
            + "background:#0f1115;color:#e6e8eb;font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;padding:24px}"
            + "main{max-width:32rem;background:#171a21;border:1px solid #2a2f3a;border-radius:12px;padding:28px}"
            + "h1{font-size:1.2rem;margin:0 0 8px}p{margin:0;color:#b9c0cc;line-height:1.5}</style>"
            + "</head><body><main><h1>Authorization request expired</h1><p>"
            + encoded
            + "</p></main></body></html>";
    }
}
