namespace DotNetCloud.Modules.Files.Models;

/// <summary>
/// Per-item tracking row linking a user's node to a template entry.
/// The composite key is provider-neutral: the path is normalized to forward slashes
/// and lower-cased, so Windows and Linux provisioning classify the same item identically.
/// </summary>
public sealed class UserDefaultContentItem
{
    /// <summary>Owner of the tracked item.</summary>
    public Guid UserId { get; set; }

    /// <summary>Normalized template-relative path used as the stable identifier.</summary>
    public required string RelativePathKey { get; set; }

    /// <summary>Template-relative path in its original casing (display).</summary>
    public required string RelativePath { get; set; }

    /// <summary>Whether the template entry is a folder or a file.</summary>
    public DefaultContentItemKind Kind { get; set; }

    /// <summary>
    /// Primary lookup key during reconciliation. Cleared when the node is permanently purged;
    /// the tombstone row itself is retained so the item is never recreated.
    /// </summary>
    public Guid? FileNodeId { get; set; }

    /// <summary>Content hash copied from the template (files only).</summary>
    public string? ProvisionedContentHash { get; set; }

    /// <summary>Size at provisioning time (quota exemption input).</summary>
    public long ProvisionedSize { get; set; }

    /// <summary>Current classification of the tracked item.</summary>
    public DefaultContentItemState State { get; set; } = DefaultContentItemState.Present;

    /// <summary>When the item was first provisioned (UTC).</summary>
    public DateTime FirstProvisionedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>When the item was last examined by a pass (UTC).</summary>
    public DateTime LastCheckedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Kind of a tracked default-content item.</summary>
public enum DefaultContentItemKind
{
    /// <summary>A folder created from the template.</summary>
    Folder,

    /// <summary>A file copied from the template.</summary>
    File
}

/// <summary>Classification of a tracked default-content item.</summary>
public enum DefaultContentItemState
{
    /// <summary>The user's node matches the provisioned content and the template.</summary>
    Present,

    /// <summary>Unmodified copy whose template content changed.</summary>
    Outdated,

    /// <summary>The user changed the node; it is no longer managed.</summary>
    ChangedByUser,

    /// <summary>The user deleted the node; a permanent tombstone.</summary>
    RemovedByUser
}

/// <summary>Flags describing how a reconciliation pass may mutate user content.</summary>
[Flags]
public enum ReconcileMode
{
    /// <summary>Add missing items only; never write to existing content.</summary>
    AddOnly = 0,

    /// <summary>Replace unmodified (<see cref="DefaultContentItemState.Outdated"/>) copies.</summary>
    PublishUpdates = 1,

    /// <summary>Permanently delete copies of template items that were removed.</summary>
    AllowDeletions = 2,

    /// <summary>Replace copies the user modified. Requires explicit administrator confirmation.</summary>
    OverwriteModified = 4
}
