namespace DotNetCloud.Modules.Files.Models;

/// <summary>
/// Per-user state of the default-content provisioning / convergence queue.
/// One row per user; the row doubles as the work item and the observable status.
/// </summary>
public sealed class UserContentProvisioning
{
    /// <summary>Owner of this provisioning row (primary key).</summary>
    public Guid UserId { get; set; }

    /// <summary>Current queue / pass status.</summary>
    public UserContentProvisioningStatus Status { get; set; } = UserContentProvisioningStatus.Pending;

    /// <summary>SHA-256 of the template manifest at the last completed pass.</summary>
    public string? TemplateFingerprint { get; set; }

    /// <summary>Folders and files added by the last pass.</summary>
    public int ItemsCreated { get; set; }

    /// <summary>Entries skipped by the last pass (collisions, caps, invalid entries).</summary>
    public int ItemsSkipped { get; set; }

    /// <summary>Tracked items whose template content changed and await a publish run.</summary>
    public int PendingUpdates { get; set; }

    /// <summary>Tracked items removed from the template that await a confirmed deletion run.</summary>
    public int PendingDeletions { get; set; }

    /// <summary>Total bytes copied by the last pass.</summary>
    public long BytesCopied { get; set; }

    /// <summary>Retry counter; the worker stops retrying after five failures.</summary>
    public int Attempts { get; set; }

    /// <summary>Lease expiry (UTC) used to reclaim passes abandoned by a crashed worker.</summary>
    public DateTime? LeaseExpiresAtUtc { get; set; }

    /// <summary>Last failure reason (truncated).</summary>
    public string? LastError { get; set; }

    /// <summary>When the last pass completed successfully (UTC).</summary>
    public DateTime? AppliedAtUtc { get; set; }

    /// <summary>When the row was created (UTC).</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>When the row was last updated (UTC).</summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Queue / pass status for <see cref="UserContentProvisioning"/>.
/// </summary>
public enum UserContentProvisioningStatus
{
    /// <summary>Queued and waiting for a worker.</summary>
    Pending,

    /// <summary>Claimed by a worker.</summary>
    InProgress,

    /// <summary>Completed with no skipped entries.</summary>
    Completed,

    /// <summary>Completed, but some entries were skipped.</summary>
    Partial,

    /// <summary>The pass failed entirely.</summary>
    Failed,

    /// <summary>Nothing to do (feature disabled or empty template).</summary>
    Skipped
}
