namespace DotNetCloud.Modules.Files.Models;

/// <summary>
/// Represents a comment on a file or folder.
/// Enables collaboration through threaded discussions on specific files.
/// </summary>
public sealed class FileComment
{
    /// <summary>Unique identifier for this comment.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The file or folder this comment is on.</summary>
    public Guid FileNodeId { get; set; }

    /// <summary>Navigation property to the commented node.</summary>
    public FileNode? FileNode { get; set; }

    /// <summary>Parent comment ID for threaded replies. Null for top-level comments.</summary>
    public Guid? ParentCommentId { get; set; }

    /// <summary>Navigation property to the parent comment.</summary>
    public FileComment? ParentComment { get; set; }

    /// <summary>Replies to this comment.</summary>
    public ICollection<FileComment> Replies { get; set; } = [];

    /// <summary>Comment text content (supports Markdown).</summary>
    public required string Content { get; set; }

    /// <summary>User who wrote the comment.</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>When the comment was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the comment was last edited (UTC). Null if never edited.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Whether the comment has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }
}
