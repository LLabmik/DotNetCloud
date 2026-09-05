namespace DotNetCloud.UI.Web.Client.Services;

/// <summary>
/// Thrown when the DotNetCloud API returns a non-success status code.
/// Carries the server's human-readable error message for direct display.
/// </summary>
public sealed class ApiException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApiException"/> class.
    /// </summary>
    /// <param name="message">The server-provided (or fallback) error message.</param>
    public ApiException(string message)
        : base(message)
    {
    }
}
