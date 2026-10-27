namespace DotNetCloud.Modules.Music.UI;

/// <summary>
/// Well-known Music module routes, plus helpers for deciding whether a URL points into the module.
/// Used by the global playbar so the now-playing tab title is only applied while the user is
/// actually looking at the Music module.
/// </summary>
public static class MusicRoutes
{
    /// <summary>The Music module page path.</summary>
    public const string BasePath = "/apps/music";

    /// <summary>The Music module page path without the leading slash, used for comparisons.</summary>
    private const string BasePathSegment = "apps/music";

    /// <summary>
    /// Returns whether the given absolute or relative URL points at the Music module
    /// (the page itself or anything nested beneath it, e.g. <c>/apps/music/help</c>).
    /// Query strings and fragments are ignored.
    /// </summary>
    /// <param name="url">Absolute URL (e.g. <c>https://host/apps/music?playing=true</c>) or relative path.</param>
    public static bool IsMusicUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var path = url.Trim();

        // Absolute web URLs (the usual case — NavigationManager.Uri is absolute). Restrict this to
        // http(s): on Unix a rooted path like "/apps/music?x=1" also parses as an absolute URI.
        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            path = absolute.AbsolutePath;
        }

        var cut = path.IndexOfAny(['?', '#']);
        if (cut >= 0)
            path = path[..cut];

        var normalized = path.Trim().Trim('/');

        return normalized.Equals(BasePathSegment, StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(BasePathSegment + "/", StringComparison.OrdinalIgnoreCase);
    }
}
