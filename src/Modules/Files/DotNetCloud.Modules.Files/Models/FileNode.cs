namespace DotNetCloud.Modules.Files.Models;

/// <summary>
/// Represents a file or folder in the DotNetCloud file system tree.
/// Both files and folders are stored as nodes in a single tree structure,
/// distinguished by the <see cref="NodeType"/> property.
/// </summary>
public sealed class FileNode
{
    /// <summary>Unique identifier for this node.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Display name of the file or folder (e.g., "report.docx", "Photos").</summary>
    public required string Name { get; set; }

    /// <summary>Whether this node is a file or a folder.</summary>
    public FileNodeType NodeType { get; set; } = FileNodeType.File;

    /// <summary>MIME type of the file (null for folders).</summary>
    public string? MimeType { get; set; }

    /// <summary>File size in bytes (0 for folders).</summary>
    public long Size { get; set; }

    /// <summary>Parent folder ID. Null for root-level nodes.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Navigation property to the parent folder.</summary>
    public FileNode? Parent { get; set; }

    /// <summary>Child nodes (files and subfolders). Only populated for folders.</summary>
    public ICollection<FileNode> Children { get; set; } = [];

    /// <summary>ID of the user who owns this node.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>
    /// Materialized path for efficient tree queries (e.g., "/root-id/parent-id/this-id").
    /// Enables fast descendant lookups and path resolution.
    /// </summary>
    public string MaterializedPath { get; set; } = string.Empty;

    /// <summary>Depth in the folder tree (0 = root level).</summary>
    public int Depth { get; set; }

    /// <summary>SHA-256 hash of the current file content. Null for folders.</summary>
    public string? ContentHash { get; set; }

    /// <summary>Current version number (incremented on each update).</summary>
    public int CurrentVersion { get; set; } = 1;

    /// <summary>
    /// Storage path on disk or in the storage backend.
    /// Content-addressable: based on the content hash for deduplication.
    /// Null for folders.
    /// </summary>
    public string? StoragePath { get; set; }

    /// <summary>Whether this node has been soft-deleted (moved to trash).</summary>
    public bool IsDeleted { get; set; }

    /// <summary>When the node was moved to trash (UTC).</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>User who deleted this node.</summary>
    public Guid? DeletedByUserId { get; set; }

    /// <summary>Original parent ID before deletion (for restore).</summary>
    public Guid? OriginalParentId { get; set; }

    /// <summary>Whether this node is marked as a favorite by the owner.</summary>
    public bool IsFavorite { get; set; }

    /// <summary>When the node was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the node was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Monotonically increasing sequence number assigned on every mutation.
    /// Used for cursor-based delta sync (Task 2.4).
    /// </summary>
    public long? SyncSequence { get; set; }

    /// <summary>
    /// POSIX file mode bitmask (e.g. <c>0o644</c> = 420, <c>0o755</c> = 493).
    /// Null for folders and for nodes uploaded from Windows clients.
    /// Stored to preserve Linux file permissions across sync clients.
    /// </summary>
    public int? PosixMode { get; set; }

    /// <summary>
    /// Hint for the original POSIX owner in <c>"user:group"</c> format.
    /// Informational only — not enforced on download; UIDs differ across machines.
    /// Null for nodes uploaded from Windows clients.
    /// </summary>
    public string? PosixOwnerHint { get; set; }

    /// <summary>File versions (only for files).</summary>
    public ICollection<FileVersion> Versions { get; set; } = [];

    /// <summary>Shares associated with this node.</summary>
    public ICollection<FileShare> Shares { get; set; } = [];

    /// <summary>Tags applied to this node.</summary>
    public ICollection<FileTag> Tags { get; set; } = [];

    /// <summary>Comments on this node.</summary>
    public ICollection<FileComment> Comments { get; set; } = [];
}
