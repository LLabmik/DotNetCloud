namespace DotNetCloud.Modules.Files.DTOs;

/// <summary>
/// Response DTO describing an admin-managed shared folder.
/// </summary>
public sealed record AdminSharedFolderDto
{
    /// <summary>Shared-folder definition ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Organization scope for the shared folder.</summary>
    public Guid? OrganizationId { get; init; }

    /// <summary>Display name rendered under the virtual shared-folder root.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Canonical host source path.</summary>
    public required string SourcePath { get; init; }

    /// <summary>Whether the shared folder is enabled.</summary>
    public bool IsEnabled { get; init; }

    /// <summary>Effective access mode.</summary>
    public required string AccessMode { get; init; }

    /// <summary>Configured crawl mode.</summary>
    public required string CrawlMode { get; init; }

    /// <summary>When indexing last completed successfully.</summary>
    public DateTime? LastIndexedAt { get; init; }

    /// <summary>When the next scan is scheduled.</summary>
    public DateTime? NextScheduledScanAt { get; init; }

    /// <summary>Status of the most recent scan attempt.</summary>
    public required string LastScanStatus { get; init; }

    /// <summary>Current reindex lifecycle state.</summary>
    public required string ReindexState { get; init; }

    /// <summary>User who created the definition.</summary>
    public Guid CreatedByUserId { get; init; }

    /// <summary>User who last updated the definition.</summary>
    public Guid? UpdatedByUserId { get; init; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Last update timestamp.</summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>Granted groups that can access the shared folder.</summary>
    public IReadOnlyList<AdminSharedFolderGroupDto> GrantedGroups { get; init; } = [];
}

/// <summary>
/// Lightweight group info included in admin shared-folder responses.
/// </summary>
public sealed record AdminSharedFolderGroupDto
{
    /// <summary>Granted group ID.</summary>
    public required Guid GroupId { get; init; }

    /// <summary>Group display name when available.</summary>
    public string? GroupName { get; init; }

    /// <summary>Organization that owns the group when available.</summary>
    public Guid? OrganizationId { get; init; }

    /// <summary>Whether this is the built-in all-users group.</summary>
    public bool IsAllUsersGroup { get; init; }

    /// <summary>Current member count reported by the group directory.</summary>
    public int MemberCount { get; init; }
}

/// <summary>
/// Request DTO for creating an admin-managed shared folder.
/// </summary>
public sealed record CreateAdminSharedFolderDto
{
    /// <summary>Display name rendered under the virtual shared-folder root.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Host path to expose.</summary>
    public required string SourcePath { get; init; }

    /// <summary>Whether the shared folder is enabled immediately.</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>Requested access mode.</summary>
    public string AccessMode { get; init; } = "ReadOnly";

    /// <summary>Requested crawl mode.</summary>
    public string CrawlMode { get; init; } = "Scheduled";

    /// <summary>Optional next scheduled scan time.</summary>
    public DateTime? NextScheduledScanAt { get; init; }

    /// <summary>Granted groups that can access the shared folder.</summary>
    public IReadOnlyList<Guid> GroupIds { get; init; } = [];
}

/// <summary>
/// Request DTO for updating an admin-managed shared folder.
/// </summary>
public sealed record UpdateAdminSharedFolderDto
{
    /// <summary>Display name rendered under the virtual shared-folder root.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Host path to expose.</summary>
    public required string SourcePath { get; init; }

    /// <summary>Whether the shared folder remains enabled.</summary>
    public bool IsEnabled { get; init; }

    /// <summary>Requested access mode.</summary>
    public string AccessMode { get; init; } = "ReadOnly";

    /// <summary>Requested crawl mode.</summary>
    public string CrawlMode { get; init; } = "Scheduled";

    /// <summary>Optional next scheduled scan time.</summary>
    public DateTime? NextScheduledScanAt { get; init; }

    /// <summary>Granted groups that can access the shared folder.</summary>
    public IReadOnlyList<Guid> GroupIds { get; init; } = [];
}

/// <summary>
/// Request DTO for scheduling or expediting a shared-folder rescan.
/// </summary>
public sealed record ScheduleAdminSharedFolderRescanDto
{
    /// <summary>
    /// Optional time for the next scan. When omitted, the scan is scheduled immediately.
    /// </summary>
    public DateTime? NextScheduledScanAt { get; init; }
}