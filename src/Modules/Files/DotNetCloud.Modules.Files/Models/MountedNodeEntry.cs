namespace DotNetCloud.Modules.Files.Models;

/// <summary>
/// Persisted mapping from a deterministic virtual node GUID to its shared-folder and relative path.
/// Survives process restarts so admin-share files remain playable without re-scanning.
/// </summary>
public sealed class MountedNodeEntry
{
    /// <summary>Deterministic virtual node GUID.</summary>
    public Guid Id { get; set; }

    /// <summary>FK to the admin shared folder definition.</summary>
    public Guid SharedFolderId { get; set; }

    /// <summary>Normalized relative path within the shared folder.</summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>Whether this entry is a directory.</summary>
    public bool IsDirectory { get; set; }

    /// <summary>When the entry was first persisted.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the shared folder definition.</summary>
    public AdminSharedFolderDefinition? SharedFolder { get; set; }
}
