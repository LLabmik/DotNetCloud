namespace DotNetCloud.Modules.Email.Models;

/// <summary>
/// An email attachment reference stored in the storage provider.
/// </summary>
public sealed class EmailAttachment
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The parent message.</summary>
    public Guid MessageId { get; set; }

    /// <summary>Original filename.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>MIME content type.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long Size { get; set; }

    /// <summary>Content-ID for inline images.</summary>
    public string? ContentId { get; set; }

    /// <summary>Storage provider key for retrieval.</summary>
    public string? StorageKey { get; set; }

    /// <summary>SHA-256 hash of the content.</summary>
    public string? ContentHash { get; set; }

    /// <summary>When the attachment record was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Parent message navigation property.</summary>
    public EmailMessage? Message { get; set; }
}
