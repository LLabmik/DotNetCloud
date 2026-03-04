namespace DotNetCloud.Modules.Files.Models;

/// <summary>
/// Represents a tag applied to a file or folder for organization and filtering.
/// </summary>
public sealed class FileTag
{
    /// <summary>Unique identifier for this tag assignment.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The file or folder this tag is applied to.</summary>
    public Guid FileNodeId { get; set; }

    /// <summary>Navigation property to the tagged node.</summary>
    public FileNode? FileNode { get; set; }

    /// <summary>Tag name (e.g., "important", "work", "vacation").</summary>
    public required string Name { get; set; }

    /// <summary>Optional color for UI display (hex, e.g., "#FF5733").</summary>
    public string? Color { get; set; }

    /// <summary>User who created this tag.</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>When the tag was applied (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
