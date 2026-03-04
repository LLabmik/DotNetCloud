namespace DotNetCloud.Modules.Files.DTOs;

/// <summary>
/// Response DTO representing a file or folder node.
/// </summary>
public sealed record FileNodeDto
{
    /// <summary>Unique identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Whether this is a file or folder.</summary>
    public required string NodeType { get; init; }

    /// <summary>MIME type (null for folders).</summary>
    public string? MimeType { get; init; }

    /// <summary>Size in bytes.</summary>
    public long Size { get; init; }

    /// <summary>Parent folder ID.</summary>
    public Guid? ParentId { get; init; }

    /// <summary>Owner user ID.</summary>
    public Guid OwnerId { get; init; }

    /// <summary>Current version number.</summary>
    public int CurrentVersion { get; init; }

    /// <summary>Whether this node is favorited.</summary>
    public bool IsFavorite { get; init; }

    /// <summary>Content hash for sync detection.</summary>
    public string? ContentHash { get; init; }

    /// <summary>Created timestamp (UTC).</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Last modified timestamp (UTC).</summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>Number of children (for folders).</summary>
    public int ChildCount { get; init; }

    /// <summary>Tags applied to this node.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}

/// <summary>
/// Request DTO for creating a new folder.
/// </summary>
public sealed record CreateFolderDto
{
    /// <summary>Folder name.</summary>
    public required string Name { get; init; }

    /// <summary>Parent folder ID. Null for root level.</summary>
    public Guid? ParentId { get; init; }
}

/// <summary>
/// Request DTO for renaming a file or folder.
/// </summary>
public sealed record RenameNodeDto
{
    /// <summary>New name for the node.</summary>
    public required string Name { get; init; }
}

/// <summary>
/// Request DTO for moving or copying a file or folder.
/// </summary>
public sealed record MoveNodeDto
{
    /// <summary>Target parent folder ID.</summary>
    public required Guid TargetParentId { get; init; }
}

/// <summary>
/// Request DTO for initiating a chunked upload session.
/// </summary>
public sealed record InitiateUploadDto
{
    /// <summary>File name.</summary>
    public required string FileName { get; init; }

    /// <summary>Parent folder ID.</summary>
    public Guid? ParentId { get; init; }

    /// <summary>Total file size in bytes.</summary>
    public long TotalSize { get; init; }

    /// <summary>MIME type of the file.</summary>
    public string? MimeType { get; init; }

    /// <summary>Ordered list of SHA-256 chunk hashes.</summary>
    public required IReadOnlyList<string> ChunkHashes { get; init; }
}

/// <summary>
/// Response DTO for an initiated upload session.
/// </summary>
public sealed record UploadSessionDto
{
    /// <summary>Upload session ID.</summary>
    public required Guid SessionId { get; init; }

    /// <summary>List of chunk hashes the server already has (skip upload for these).</summary>
    public IReadOnlyList<string> ExistingChunks { get; init; } = [];

    /// <summary>List of chunk hashes the server needs uploaded.</summary>
    public IReadOnlyList<string> MissingChunks { get; init; } = [];

    /// <summary>Session expiration time (UTC).</summary>
    public DateTime ExpiresAt { get; init; }
}

/// <summary>
/// Response DTO for a file version.
/// </summary>
public sealed record FileVersionDto
{
    /// <summary>Version ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Version number.</summary>
    public int VersionNumber { get; init; }

    /// <summary>Size in bytes.</summary>
    public long Size { get; init; }

    /// <summary>Content hash.</summary>
    public required string ContentHash { get; init; }

    /// <summary>MIME type.</summary>
    public string? MimeType { get; init; }

    /// <summary>User who created this version.</summary>
    public Guid CreatedByUserId { get; init; }

    /// <summary>When this version was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Optional label.</summary>
    public string? Label { get; init; }
}

/// <summary>
/// Response DTO for a file share.
/// </summary>
public sealed record FileShareDto
{
    /// <summary>Share ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Shared node ID.</summary>
    public Guid FileNodeId { get; init; }

    /// <summary>Node name.</summary>
    public string? NodeName { get; init; }

    /// <summary>Share type.</summary>
    public required string ShareType { get; init; }

    /// <summary>Target user ID (for user shares).</summary>
    public Guid? SharedWithUserId { get; init; }

    /// <summary>Target team ID (for team shares).</summary>
    public Guid? SharedWithTeamId { get; init; }

    /// <summary>Permission level.</summary>
    public required string Permission { get; init; }

    /// <summary>Public link token.</summary>
    public string? LinkToken { get; init; }

    /// <summary>Share expiration.</summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>Download count (for public links).</summary>
    public int DownloadCount { get; init; }

    /// <summary>Max downloads (for public links).</summary>
    public int? MaxDownloads { get; init; }

    /// <summary>When the share was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Note attached to the share.</summary>
    public string? Note { get; init; }
}

/// <summary>
/// Request DTO for creating a share.
/// </summary>
public sealed record CreateShareDto
{
    /// <summary>Share type: "User", "Team", "Group", or "PublicLink".</summary>
    public required string ShareType { get; init; }

    /// <summary>Target user ID (for user shares).</summary>
    public Guid? SharedWithUserId { get; init; }

    /// <summary>Target team ID (for team shares).</summary>
    public Guid? SharedWithTeamId { get; init; }

    /// <summary>Target group ID (for group shares).</summary>
    public Guid? SharedWithGroupId { get; init; }

    /// <summary>Permission level: "Read", "ReadWrite", or "Full".</summary>
    public string Permission { get; init; } = "Read";

    /// <summary>Optional password for public link shares.</summary>
    public string? LinkPassword { get; init; }

    /// <summary>Max downloads for public links (null = unlimited).</summary>
    public int? MaxDownloads { get; init; }

    /// <summary>Share expiration date (null = never).</summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>Optional note.</summary>
    public string? Note { get; init; }
}

/// <summary>
/// Response DTO for user storage quota.
/// </summary>
public sealed record QuotaDto
{
    /// <summary>User ID.</summary>
    public Guid UserId { get; init; }

    /// <summary>Maximum storage in bytes (0 = unlimited).</summary>
    public long MaxBytes { get; init; }

    /// <summary>Used storage in bytes.</summary>
    public long UsedBytes { get; init; }

    /// <summary>Remaining storage in bytes.</summary>
    public long RemainingBytes { get; init; }

    /// <summary>Usage percentage (0.0 to 100.0+).</summary>
    public double UsagePercent { get; init; }
}

/// <summary>
/// Response DTO for trash bin items.
/// </summary>
public sealed record TrashItemDto
{
    /// <summary>Node ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Node name.</summary>
    public required string Name { get; init; }

    /// <summary>Whether this is a file or folder.</summary>
    public required string NodeType { get; init; }

    /// <summary>Size in bytes.</summary>
    public long Size { get; init; }

    /// <summary>MIME type.</summary>
    public string? MimeType { get; init; }

    /// <summary>When the item was deleted.</summary>
    public DateTime? DeletedAt { get; init; }

    /// <summary>User who deleted the item.</summary>
    public Guid? DeletedByUserId { get; init; }

    /// <summary>Original parent folder path.</summary>
    public string? OriginalPath { get; init; }
}
