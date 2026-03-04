namespace DotNetCloud.Modules.Files.Models;

/// <summary>
/// Represents a historical version of a file.
/// Every file update creates a new version, allowing users to restore previous states.
/// </summary>
public sealed class FileVersion
{
    /// <summary>Unique identifier for this version.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The file node this version belongs to.</summary>
    public Guid FileNodeId { get; set; }

    /// <summary>Navigation property to the file node.</summary>
    public FileNode? FileNode { get; set; }

    /// <summary>Version number (1-based, ascending).</summary>
    public int VersionNumber { get; set; }

    /// <summary>File size in bytes for this version.</summary>
    public long Size { get; set; }

    /// <summary>SHA-256 hash of this version's content.</summary>
    public required string ContentHash { get; set; }

    /// <summary>
    /// Storage path for this version's content.
    /// Content-addressable: files with identical hashes share storage.
    /// </summary>
    public required string StoragePath { get; set; }

    /// <summary>MIME type at the time this version was created.</summary>
    public string? MimeType { get; set; }

    /// <summary>User who created this version.</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>When this version was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Optional label for this version (e.g., "Final draft").</summary>
    public string? Label { get; set; }
}
