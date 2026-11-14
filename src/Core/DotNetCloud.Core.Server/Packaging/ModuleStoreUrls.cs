namespace DotNetCloud.ModulePackaging;

/// <summary>
/// Validation rules for module store index URLs.
/// </summary>
/// <remarks>
/// The same rule is applied wherever a store URL is accepted — the remote index client and the
/// admin settings overlay — so a URL cannot be saved that the client would later refuse.
/// </remarks>
public static class ModuleStoreUrls
{
    /// <summary>
    /// Whether a value is a usable module store index URL: an absolute <c>https</c> URL, or plain
    /// <c>http</c> only when the host is a loopback address (development).
    /// </summary>
    /// <param name="value">The candidate URL.</param>
    /// <returns><see langword="true"/> when the value can be fetched by the store client.</returns>
    public static bool IsUsable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) && uri.IsLoopback);
    }
}
