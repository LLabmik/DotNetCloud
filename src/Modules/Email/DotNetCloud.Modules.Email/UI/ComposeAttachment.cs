namespace DotNetCloud.Modules.Email.UI;

/// <summary>
/// Represents an attachment in the compose form (before sending).
/// </summary>
public sealed record ComposeAttachment
{
    /// <summary>Storage key from the upload endpoint.</summary>
    public required string StorageKey { get; init; }

    /// <summary>Original filename.</summary>
    public required string FileName { get; init; }

    /// <summary>MIME content type.</summary>
    public required string ContentType { get; init; }

    /// <summary>File size in bytes.</summary>
    public long Size { get; init; }
}
