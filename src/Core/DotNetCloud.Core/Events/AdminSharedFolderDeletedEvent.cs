namespace DotNetCloud.Core.Events;

/// <summary>
/// Published when an admin shared folder definition is deleted.
/// Core.Server subscribers handle media source and entity cleanup.
/// </summary>
public sealed record AdminSharedFolderDeletedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The deleted shared folder definition ID.</summary>
    public required Guid SharedFolderId { get; init; }

    /// <summary>Display name of the deleted folder (for logging/audit).</summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// All relative paths (files and directories) that were mounted under this share.
    /// Used to compute the deterministic virtual FileNodeIds for media entity cleanup.
    /// </summary>
    public IReadOnlyList<MountedEntryInfo> MountedEntries { get; init; } = [];
}

/// <summary>
/// Describes a single mounted entry (file or directory) within an admin shared folder.
/// </summary>
public sealed record MountedEntryInfo
{
    /// <summary>Normalized relative path within the shared folder.</summary>
    public required string RelativePath { get; init; }

    /// <summary>Whether this entry is a directory.</summary>
    public bool IsDirectory { get; init; }
}
