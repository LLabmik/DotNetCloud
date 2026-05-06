namespace DotNetCloud.Modules.Email.Services;

/// <summary>
/// Result of a storage operation.
/// </summary>
public sealed record AttachmentStorageResult
{
    /// <summary>Storage key (SHA-256 hex).</summary>
    public required string StorageKey { get; init; }

    /// <summary>Content hash (same SHA-256 hex).</summary>
    public required string ContentHash { get; init; }

    /// <summary>File size in bytes.</summary>
    public long Size { get; init; }

    /// <summary>When the content was stored.</summary>
    public DateTime StoredAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Abstraction for storing and retrieving email attachment content.
/// Uses content-addressable storage with SHA-256 hash prefix directories.
/// </summary>
public interface IAttachmentStorage
{
    /// <summary>
    /// Stores attachment content and returns storage metadata.
    /// </summary>
    Task<AttachmentStorageResult> StoreAsync(Stream content, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Opens a read stream for an attachment by storage key.
    /// </summary>
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default);

    /// <summary>
    /// Deletes an attachment by storage key.
    /// </summary>
    Task<bool> DeleteAsync(string storageKey, CancellationToken ct = default);

    /// <summary>
    /// Gets the file size of a stored attachment.
    /// </summary>
    Task<long> GetSizeAsync(string storageKey, CancellationToken ct = default);
}
