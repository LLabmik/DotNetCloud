namespace DotNetCloud.Modules.Files.Models;

/// <summary>
/// Tracks an active chunked upload session.
/// When a client uploads a large file, it sends a manifest of chunk hashes first,
/// then uploads only the missing chunks. This entity tracks the progress.
/// </summary>
public sealed class ChunkedUploadSession
{
    /// <summary>Unique identifier for this upload session.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Target file node ID (null if creating a new file).</summary>
    public Guid? TargetFileNodeId { get; set; }

    /// <summary>Target parent folder ID for new file creation.</summary>
    public Guid? TargetParentId { get; set; }

    /// <summary>Target file name.</summary>
    public required string FileName { get; set; }

    /// <summary>Total file size in bytes.</summary>
    public long TotalSize { get; set; }

    /// <summary>MIME type of the file being uploaded.</summary>
    public string? MimeType { get; set; }

    /// <summary>Total number of chunks expected.</summary>
    public int TotalChunks { get; set; }

    /// <summary>Number of chunks received so far.</summary>
    public int ReceivedChunks { get; set; }

    /// <summary>
    /// Ordered list of chunk hashes (SHA-256) for the entire file.
    /// Serialized as JSON array.
    /// </summary>
    public required string ChunkManifest { get; set; }

    /// <summary>User who initiated the upload.</summary>
    public Guid UserId { get; set; }

    /// <summary>Upload session status.</summary>
    public UploadSessionStatus Status { get; set; } = UploadSessionStatus.InProgress;

    /// <summary>When the upload session was started (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the session was last updated (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the session expires (UTC). Stale sessions are cleaned up.</summary>
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);
}
