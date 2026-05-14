namespace DotNetCloud.Core.Security;

/// <summary>
/// Result of a file validation check.
/// </summary>
public readonly struct FileValidationResult
{
    private FileValidationResult(bool isValid, string? errorCode, string? errorMessage)
    {
        IsValid = isValid;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    /// <summary>Whether the file passed validation.</summary>
    public bool IsValid { get; }

    /// <summary>Machine-readable error code if validation failed.</summary>
    public string? ErrorCode { get; }

    /// <summary>Human-readable error message if validation failed.</summary>
    public string? ErrorMessage { get; }

    /// <summary>Creates a successful validation result.</summary>
    public static FileValidationResult Success() => new(true, null, null);

    /// <summary>Creates a failed validation result.</summary>
    public static FileValidationResult Failure(string errorCode, string errorMessage) =>
        new(false, errorCode, errorMessage);
}
