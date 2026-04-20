namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Stores and retrieves images uploaded to chat channels.
/// </summary>
public interface IChatImageStore
{
    /// <summary>
    /// Saves image bytes and returns the relative URL for serving.
    /// </summary>
    Task<ChatImageUploadResult> SaveAsync(string fileName, string contentType, byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the image bytes and content type for a stored image.
    /// Returns null if the image does not exist.
    /// </summary>
    Task<ChatImageFile?> GetAsync(string storedFileName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a chat image upload.
/// </summary>
public sealed record ChatImageUploadResult
{
    /// <summary>Stored file name (GUID-based).</summary>
    public required string StoredFileName { get; init; }

    /// <summary>URL path to serve the image (e.g., /api/v1/chat/uploads/abc123.png).</summary>
    public required string Url { get; init; }

    /// <summary>MIME content type.</summary>
    public required string ContentType { get; init; }

    /// <summary>File size in bytes.</summary>
    public long FileSize { get; init; }
}

/// <summary>
/// A stored chat image file.
/// </summary>
public sealed record ChatImageFile
{
    /// <summary>Raw image bytes.</summary>
    public required byte[] Data { get; init; }

    /// <summary>MIME content type.</summary>
    public required string ContentType { get; init; }
}
