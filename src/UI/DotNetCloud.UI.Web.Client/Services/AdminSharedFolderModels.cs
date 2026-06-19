namespace DotNetCloud.UI.Web.Client.Services;

/// <summary>
/// Client-side model for a Files admin shared folder.
/// </summary>
public sealed record AdminSharedFolderResponse
{
    /// <summary>Shared-folder definition ID.</summary>
    public Guid Id { get; init; }

    /// <summary>Organization scope for the shared folder.</summary>
    public Guid? OrganizationId { get; init; }

    /// <summary>Display name rendered under the virtual shared-folder root.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Canonical host source path.</summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>Whether the shared folder is currently enabled.</summary>
    public bool IsEnabled { get; init; }

    /// <summary>Access mode string.</summary>
    public string AccessMode { get; init; } = string.Empty;

    /// <summary>Crawl mode string.</summary>
    public string CrawlMode { get; init; } = string.Empty;

    /// <summary>Last successful index timestamp.</summary>
    public DateTime? LastIndexedAt { get; init; }

    /// <summary>Next scheduled scan timestamp.</summary>
    public DateTime? NextScheduledScanAt { get; init; }

    /// <summary>Status of the last scan.</summary>
    public string LastScanStatus { get; init; } = string.Empty;

    /// <summary>Current reindex state.</summary>
    public string ReindexState { get; init; } = string.Empty;

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Last update timestamp.</summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>Groups granted access to the shared folder.</summary>
    public IReadOnlyList<AdminSharedFolderGroupResponse> GrantedGroups { get; init; } = [];
}

/// <summary>
/// Client-side group info attached to an admin shared folder.
/// </summary>
public sealed record AdminSharedFolderGroupResponse
{
    /// <summary>Granted group ID.</summary>
    public Guid GroupId { get; init; }

    /// <summary>Group display name.</summary>
    public string? GroupName { get; init; }

    /// <summary>Group organization ID when known.</summary>
    public Guid? OrganizationId { get; init; }

    /// <summary>Whether the group is the built-in all-users group.</summary>
    public bool IsAllUsersGroup { get; init; }

    /// <summary>Reported member count.</summary>
    public int MemberCount { get; init; }
}

/// <summary>
/// Client-side browse result for admin shared-folder source directories.
/// </summary>
public sealed record AdminSharedFolderBrowseResponse
{
    /// <summary>Configured canonical root path.</summary>
    public string RootPath { get; init; } = string.Empty;

    /// <summary>Canonical path for the current browse location.</summary>
    public string CurrentPath { get; init; } = string.Empty;

    /// <summary>Current browse path relative to the configured root.</summary>
    public string RelativePath { get; init; } = string.Empty;

    /// <summary>Immediate child directories under the current browse location.</summary>
    public IReadOnlyList<AdminSharedFolderBrowseDirectoryResponse> Directories { get; init; } = [];
}

/// <summary>
/// Client-side directory entry for the admin shared-folder picker.
/// </summary>
public sealed record AdminSharedFolderBrowseDirectoryResponse
{
    /// <summary>Directory name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Canonical source path for the directory.</summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>Directory path relative to the configured root.</summary>
    public string RelativePath { get; init; } = string.Empty;
}

/// <summary>
/// Client-side request model for creating or updating an admin shared folder.
/// </summary>
public sealed record SaveAdminSharedFolderRequest
{
    /// <summary>Display name rendered under the virtual shared-folder root.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Host path to expose.</summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>Whether the shared folder is enabled.</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>Requested access mode.</summary>
    public string AccessMode { get; init; } = "ReadOnly";

    /// <summary>Requested crawl mode.</summary>
    public string CrawlMode { get; init; } = "Scheduled";

    /// <summary>Optional next scheduled scan time.</summary>
    public DateTime? NextScheduledScanAt { get; init; }

    /// <summary>Granted group IDs.</summary>
    public IReadOnlyList<Guid> GroupIds { get; init; } = [];
}

/// <summary>
/// Client-side request model for scheduling a shared-folder rescan.
/// </summary>
public sealed record ScheduleAdminSharedFolderScanRequest
{
    /// <summary>Optional next scheduled scan time. Null means run now.</summary>
    public DateTime? NextScheduledScanAt { get; init; }
}

/// <summary>
/// Client-side result returned when an admin shared folder is deleted.
/// Contains the cleanup job ID for progress polling.
/// </summary>
public sealed record DeleteAdminSharedFolderResult
{
    /// <summary>Whether the definition was successfully deleted.</summary>
    public bool Deleted { get; init; }

    /// <summary>Unique job ID for tracking cleanup progress.</summary>
    public Guid CleanupJobId { get; init; }

    /// <summary>Total number of search documents that need removal.</summary>
    public int PendingSearchRemovals { get; init; }

    /// <summary>Number of search documents successfully removed so far.</summary>
    public int SearchDocsRemoved { get; init; }

    /// <summary>Whether media cleanup is pending.</summary>
    public bool PendingMediaCleanup { get; init; }
}

/// <summary>
/// Client-side response for admin shared folder cleanup status.
/// </summary>
public sealed record AdminSharedFolderCleanupStatusResponse
{
    /// <summary>Unique cleanup job ID.</summary>
    public Guid CleanupJobId { get; init; }

    /// <summary>The deleted shared folder definition ID.</summary>
    public Guid SharedFolderId { get; init; }

    /// <summary>Display name of the deleted folder.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Current cleanup phase name.</summary>
    public string Phase { get; init; } = string.Empty;

    /// <summary>Number of search documents removed.</summary>
    public int SearchDocsRemoved { get; init; }

    /// <summary>Total search documents to remove.</summary>
    public int SearchDocsTotal { get; init; }

    /// <summary>Number of affected users.</summary>
    public int AffectedUsers { get; init; }

    /// <summary>Number of users cleaned so far.</summary>
    public int UsersCleaned { get; init; }

    /// <summary>Number of media entities removed.</summary>
    public int MediaEntitiesRemoved { get; init; }

    /// <summary>When cleanup started.</summary>
    public DateTime StartedAt { get; init; }

    /// <summary>When cleanup completed (null if still running).</summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>Error message if cleanup failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Whether cleanup is complete (success or failure).</summary>
    public bool IsComplete { get; init; }
}
