namespace DotNetCloud.Modules.Sdk.Web;

/// <summary>
/// Raised when a module host answers a request with a non-success status.
/// </summary>
public class ModuleApiException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ModuleApiException"/> class.</summary>
    /// <param name="message">The message the host reported.</param>
    /// <param name="statusCode">The HTTP status the host answered with.</param>
    /// <param name="errorCode">The host's machine-readable error code, when it sent one.</param>
    public ModuleApiException(string message, int statusCode, string? errorCode = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }

    /// <summary>Gets the HTTP status the host answered with.</summary>
    public int StatusCode { get; }

    /// <summary>Gets the host's machine-readable error code, when it sent one.</summary>
    public string? ErrorCode { get; }

    /// <summary>Gets a value indicating whether the request was rejected as unauthenticated.</summary>
    public bool IsUnauthorized => StatusCode is 401 or 403;
}
