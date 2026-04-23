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