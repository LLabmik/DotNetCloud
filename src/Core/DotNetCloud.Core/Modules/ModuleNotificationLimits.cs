namespace DotNetCloud.Core.Modules;

/// <summary>
/// Content and link rules applied to notifications a module asks the core to deliver.
/// </summary>
/// <remarks>
/// <para>
/// Notifications are rendered by core chrome and carry the platform's branding. Module-supplied
/// text is untrusted input, so title and body are length-capped (a hostile module must not be able
/// to fill a user's notification list or a log with unbounded content).
/// </para>
/// <para>
/// The action link is the sharper edge: core navigates the user's browser to it, so an absolute
/// URL turns a notification into a platform-branded phishing hop. Only site-relative paths are
/// accepted; schemes (<c>javascript:</c>, <c>https:</c>), protocol-relative URLs (<c>//host</c>),
/// backslash tricks and control characters are rejected.
/// </para>
/// </remarks>
public static class ModuleNotificationLimits
{
    /// <summary>Maximum notification title length.</summary>
    public const int MaxTitleLength = 200;

    /// <summary>Maximum notification body length.</summary>
    public const int MaxBodyLength = 2000;

    /// <summary>Maximum action-link length.</summary>
    public const int MaxLinkLength = 500;

    /// <summary>Determines whether a module-supplied title is acceptable.</summary>
    /// <param name="title">The title to validate.</param>
    /// <returns><see langword="true"/> when the title is non-blank and within the limit.</returns>
    public static bool IsValidTitle(string? title) =>
        !string.IsNullOrWhiteSpace(title) && title.Length <= MaxTitleLength && !HasControlCharacters(title);

    /// <summary>Determines whether a module-supplied body is acceptable.</summary>
    /// <param name="body">The body to validate (may be empty).</param>
    /// <returns><see langword="true"/> when the body is absent or within the limit.</returns>
    public static bool IsValidBody(string? body) =>
        string.IsNullOrEmpty(body) || (body.Length <= MaxBodyLength && !HasControlCharacters(body));

    /// <summary>
    /// Determines whether a module-supplied action link is a safe in-app path.
    /// </summary>
    /// <param name="link">The link to validate (may be absent).</param>
    /// <returns>
    /// <see langword="true"/> when the link is absent or is a single site-relative path.
    /// </returns>
    public static bool IsValidLink(string? link)
    {
        if (string.IsNullOrEmpty(link))
        {
            return true;
        }

        if (link.Length > MaxLinkLength || HasControlCharacters(link))
        {
            return false;
        }

        // Must be site-relative and must not be protocol-relative or a host-relative escape
        // ("//evil.example", "/\evil.example"). "/" alone is the site root and is fine.
        if (link[0] != '/')
        {
            return false;
        }

        return link.Length == 1 || link[1] is not ('/' or '\\');
    }

    private static bool HasControlCharacters(string value)
    {
        foreach (var ch in value)
        {
            if (char.IsControl(ch))
            {
                return true;
            }
        }

        return false;
    }
}
