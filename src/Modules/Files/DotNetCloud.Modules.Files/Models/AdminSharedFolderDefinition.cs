namespace DotNetCloud.Modules.Files.Models;

/// <summary>
/// Admin-managed folder definition that exposes a server-local directory through the Files module.
/// </summary>
public sealed class AdminSharedFolderDefinition
{
    /// <summary>Unique identifier for the shared-folder definition.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Organization that owns this definition, or <see langword="null"/> for the default/global organization.</summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>Display name rendered under the virtual shared-folder root.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Canonical absolute source path on the host filesystem.</summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>Whether this definition is currently enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Access mode enforced for the mounted folder.</summary>
    public AdminSharedFolderAccessMode AccessMode { get; set; } = AdminSharedFolderAccessMode.ReadOnly;

    /// <summary>How the folder should be crawled for indexing.</summary>
    public AdminSharedFolderCrawlMode CrawlMode { get; set; } = AdminSharedFolderCrawlMode.Scheduled;

    /// <summary>When the folder was last indexed successfully.</summary>
    public DateTime? LastIndexedAt { get; set; }

    /// <summary>When the next scheduled scan should run.</summary>
    public DateTime? NextScheduledScanAt { get; set; }

    /// <summary>Status of the last scan or indexing attempt.</summary>
    public AdminSharedFolderScanStatus LastScanStatus { get; set; } = AdminSharedFolderScanStatus.NeverScanned;

    /// <summary>Current full-reindex state.</summary>
    public AdminSharedFolderReindexState ReindexState { get; set; } = AdminSharedFolderReindexState.Idle;

    /// <summary>User that created this definition.</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>User that last updated this definition, if any.</summary>
    public Guid? UpdatedByUserId { get; set; }

    /// <summary>When the definition was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the definition was last updated.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Granted groups that can access this shared folder.</summary>
    public ICollection<AdminSharedFolderGrant> Grants { get; set; } = new List<AdminSharedFolderGrant>();
}

/// <summary>
/// Access mode for admin shared folders.
/// </summary>
public enum AdminSharedFolderAccessMode
{
    /// <summary>Mounted content is read-only.</summary>
    ReadOnly = 0,
}

/// <summary>
/// Crawl mode used for indexing admin shared folders.
/// </summary>
public enum AdminSharedFolderCrawlMode
{
    /// <summary>Folder is scanned on the normal background schedule.</summary>
    Scheduled = 0,

    /// <summary>Folder is scanned only when manually triggered.</summary>
    Manual = 1,
}

/// <summary>
/// Last scan status for an admin shared folder.
/// </summary>
public enum AdminSharedFolderScanStatus
{
    /// <summary>No scan has run yet.</summary>
    NeverScanned = 0,

    /// <summary>The most recent scan completed successfully.</summary>
    Succeeded = 1,

    /// <summary>The most recent scan failed.</summary>
    Failed = 2,
}

/// <summary>
/// Reindex lifecycle state for an admin shared folder.
/// </summary>
public enum AdminSharedFolderReindexState
{
    /// <summary>No reindex is pending or active.</summary>
    Idle = 0,

    /// <summary>A reindex has been requested and is waiting to start.</summary>
    Requested = 1,

    /// <summary>A reindex is currently running.</summary>
    Running = 2,
}