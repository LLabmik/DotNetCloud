namespace DotNetCloud.Modules.Files.Models;

/// <summary>
/// Represents a chunk of a file for chunked upload/download.
/// Files are split into 4MB chunks for efficient transfer and deduplication.
/// Chunks are content-addressed by SHA-256 hash.
/// </summary>
public sealed class FileChunk
{
    /// <summary>Unique identifier for this chunk record.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>SHA-256 hash of the chunk content. Used for deduplication.</summary>
    public required string ChunkHash { get; set; }

    /// <summary>Size of the chunk in bytes.</summary>
    public int Size { get; set; }

    /// <summary>Storage path for this chunk's content on disk.</summary>
    public required string StoragePath { get; set; }

    /// <summary>
    /// Reference count: how many file versions reference this chunk.
    /// When reference count reaches zero, the chunk can be garbage collected.
    /// </summary>
    public int ReferenceCount { get; set; } = 1;

    /// <summary>When this chunk was first stored (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the reference count was last updated (UTC).</summary>
    public DateTime LastReferencedAt { get; set; } = DateTime.UtcNow;
}
