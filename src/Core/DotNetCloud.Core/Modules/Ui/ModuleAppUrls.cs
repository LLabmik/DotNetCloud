namespace DotNetCloud.Core.Modules.Ui;

/// <summary>
/// Canonical URL shapes for the module UI plane.
/// </summary>
/// <remarks>
/// <para>
/// A non-required module serves its own UI from its host process; the core reverse-proxies it at
/// <c>/apps/{moduleId}/**</c> and frames that path. The proxy strips <c>/apps/{moduleId}</c>
/// before forwarding, so a module always serves at <c>/</c> and never needs to know it is behind a
/// proxy — its own absolute links keep working.
/// </para>
/// <para>
/// Module ids are reverse-DNS (<c>dotnetcloud.files</c>, <c>org.example.customapp</c>), which is
/// what keeps this route from colliding with the shell's first-party pages at
/// <c>/apps/files</c>, <c>/apps/chat</c>, … (those ids have no dot).
/// </para>
/// </remarks>
public static class ModuleAppUrls
{
    /// <summary>The route prefix all module apps live under.</summary>
    public const string Root = "/apps";

    /// <summary>The route prefix of the core-chrome shell page that frames a module app.</summary>
    public const string ShellRoot = "/modules";

    /// <summary>Longest query string the shell forwards into a module frame.</summary>
    public const int MaxFrameQueryLength = 512;

    /// <summary>
    /// Gets the site-relative base path of a module app proxy, e.g. <c>/apps/dotnetcloud.files</c>.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <returns>The base path, or an empty string for an invalid id.</returns>
    public static string BasePath(string moduleId) => BuildPath(Root, moduleId);

    /// <summary>
    /// Gets the site-relative path of the shell page that frames a module app, e.g.
    /// <c>/modules/dotnetcloud.files</c>.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <returns>The shell path, or an empty string for an invalid id.</returns>
    /// <remarks>
    /// Navigation goes here rather than to the proxy path so the user keeps the core chrome
    /// (sidebar, header) around the framed module. The iframe itself loads
    /// <see cref="BasePath(string)"/>.
    /// </remarks>
    public static string ShellPath(string moduleId) => BuildPath(ShellRoot, moduleId);

    /// <summary>
    /// Validates an external link target requested by a module frame: absolute <c>https:</c> only.
    /// </summary>
    /// <param name="url">The candidate URL.</param>
    /// <returns><see langword="true"/> when the URL may be opened.</returns>
    /// <remarks>
    /// <c>javascript:</c>, <c>data:</c>, <c>file:</c> and relative/scheme-relative targets are all
    /// rejected — a module UI must not be able to run script in, or navigate, a core-owned window.
    /// </remarks>
    public static bool IsAllowedExternalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > 2048)
        {
            return false;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && !string.IsNullOrEmpty(uri.Host);
    }

    /// <summary>
    /// Normalizes a query string carried by the shell page so it can be forwarded to the frame.
    /// </summary>
    /// <param name="query">The raw query (with or without a leading <c>?</c>).</param>
    /// <returns>
    /// A query starting with <c>?</c>, or an empty string when the input is unusable.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The shell lives at <c>/modules/{moduleId}?photoId=…</c> while the framed document lives at
    /// <c>/apps/{moduleId}/</c>; without forwarding, a deep link would land on the module's start
    /// page and silently drop what the user clicked. <see cref="NormalizeEntryPath"/> deliberately
    /// strips queries from <em>manifest</em> paths, so the forwarded query is a separate value.
    /// </para>
    /// <para>
    /// Everything outside a conservative character set is dropped rather than escaped: the query is
    /// appended to a URL the core itself builds, and a module frame has no business receiving a
    /// fragment, a control character or a scheme.
    /// </para>
    /// </remarks>
    public static string NormalizeFrameQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return string.Empty;
        }

        var value = query.Trim();
        if (value.StartsWith('?'))
        {
            value = value[1..];
        }

        if (value.Length == 0 || value.Length > MaxFrameQueryLength)
        {
            return string.Empty;
        }

        foreach (var ch in value)
        {
            if (!char.IsAsciiLetterOrDigit(ch) && ch is not ('&' or '=' or '%' or '-' or '_' or '.' or '~' or '+'))
            {
                return string.Empty;
            }
        }

        return "?" + value;
    }

    private static string BuildPath(string root, string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            return string.Empty;
        }

        foreach (var ch in moduleId)
        {
            if (!char.IsAsciiLetterOrDigit(ch) && ch is not ('.' or '-' or '_'))
            {
                return string.Empty;
            }
        }

        return $"{root}/{moduleId}";
    }

    /// <summary>
    /// Builds the framed entry URL for a module app.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="entryPath">The manifest's <c>ui.entryPath</c>; <c>null</c>/empty means <c>/</c>.</param>
    /// <returns>The site-relative URL to frame.</returns>
    /// <remarks>
    /// Absolute URLs, protocol-relative paths (<c>//host</c>) and traversal are rejected: a manifest
    /// must not be able to point the core's iframe at a foreign origin.
    /// </remarks>
    public static string EntryUrl(string moduleId, string? entryPath = null)
    {
        var basePath = BasePath(moduleId);
        if (basePath.Length == 0)
        {
            return string.Empty;
        }

        var path = NormalizeEntryPath(entryPath);
        return path == "/" ? basePath + "/" : basePath + path;
    }

    /// <summary>
    /// Normalizes a manifest entry path to a rooted module-relative path.
    /// </summary>
    /// <param name="entryPath">The declared path.</param>
    /// <returns><c>/</c> when unusable, otherwise an absolute path with no query or fragment.</returns>
    public static string NormalizeEntryPath(string? entryPath)
    {
        if (string.IsNullOrWhiteSpace(entryPath))
        {
            return "/";
        }

        var path = entryPath.Trim();

        // Reject everything that could leave the module's own origin or the proxy base.
        if (path.Contains("://", StringComparison.Ordinal)
            || path.StartsWith("//", StringComparison.Ordinal)
            || path.Contains('\\', StringComparison.Ordinal)
            || path.Contains("..", StringComparison.Ordinal))
        {
            return "/";
        }

        // Drop any query/fragment: the frame's start URL must be a plain document path.
        var cut = path.IndexOfAny(['?', '#']);
        if (cut >= 0)
        {
            path = path[..cut];
        }

        if (path.Length == 0)
        {
            return "/";
        }

        return path.StartsWith('/') ? path : "/" + path;
    }
}
