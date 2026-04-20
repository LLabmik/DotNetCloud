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

    /// <summary>Total recursive size of all contents in bytes (folders only; 0 for files).</summary>
    public long TotalSize { get; init; }

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

    /// <summary>Tags applied to this node (includes name and color).</summary>
    public IReadOnlyList<FileTagDto> Tags { get; init; } = [];

    /// <summary>
    /// POSIX file mode bitmask (e.g., 0o644 = 420). Null for folders or Windows-uploaded files.
    /// </summary>
    public int? PosixMode { get; init; }

    /// <summary>
    /// POSIX owner hint in <c>"user:group"</c> format. Null for Windows-uploaded files.
    /// </summary>
    public string? PosixOwnerHint { get; init; }

    /// <summary>
    /// For symlink nodes (<c>NodeType == "SymbolicLink"</c>): the relative target path within
    /// the sync root (e.g. <c>"../shared/config.json"</c>). Null for files and folders.
    /// </summary>
    public string? LinkTarget { get; init; }
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
    /// <summary>Target parent folder ID. Null means root.</summary>
    public Guid? TargetParentId { get; init; }
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

    /// <summary>
    /// When set, the upload replaces the content of an existing <see cref="Models.FileNode"/>
    /// instead of creating a new one (e.g. inline text editor save on a shared file).
    /// </summary>
    public Guid? TargetFileNodeId { get; init; }

    /// <summary>Total file size in bytes.</summary>
    public long TotalSize { get; init; }

    /// <summary>MIME type of the file.</summary>
    public string? MimeType { get; init; }

    /// <summary>Ordered list of SHA-256 chunk hashes.</summary>
    public required IReadOnlyList<string> ChunkHashes { get; init; }

    /// <summary>
    /// Optional ordered list of chunk sizes (in bytes) for content-defined chunk (CDC) uploads.
    /// When present, element count must equal <see cref="ChunkHashes"/> count.
    /// <see langword="null"/> or empty indicates legacy fixed-size chunking.
    /// </summary>
    public IReadOnlyList<int>? ChunkSizes { get; init; }

    /// <summary>
    /// POSIX file mode bitmask sent by the uploading client (Linux clients only).
    /// <see langword="null"/> when the uploader is Windows/macOS or does not supply permissions.
    /// </summary>
    public int? PosixMode { get; init; }

    /// <summary>
    /// POSIX owner hint in <c>"user:group"</c> format (Linux clients only).
    /// Stored as metadata; not enforced on download because UIDs differ across machines.
    /// </summary>
    public string? PosixOwnerHint { get; init; }
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

    /// <summary>ID of the user who created the share.</summary>
    public Guid CreatedByUserId { get; init; }

    /// <summary>Note attached to the share.</summary>
    public string? Note { get; init; }

    /// <summary>Whether the public link has a password set.</summary>
    public bool HasPassword { get; init; }
}

/// <summary>
/// Lightweight info about a public link for pre-resolution checks.
/// </summary>
public sealed record PublicLinkInfoDto
{
    /// <summary>Whether the link token matched a valid share.</summary>
    public bool Exists { get; init; }

    /// <summary>Whether a password is required to access the share.</summary>
    public bool RequiresPassword { get; init; }

    /// <summary>Whether the link has expired.</summary>
    public bool IsExpired { get; init; }

    /// <summary>Whether the download limit has been reached.</summary>
    public bool IsMaxedOut { get; init; }
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

/// <summary>
/// Request DTO for updating an existing share.
/// </summary>
public sealed record UpdateShareDto
{
    /// <summary>New permission level (null = keep current).</summary>
    public string? Permission { get; init; }

    /// <summary>New expiration date (null = keep current).</summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>New max downloads (null = keep current).</summary>
    public int? MaxDownloads { get; init; }

    /// <summary>New password for public link (null = keep current, empty = remove).</summary>
    public string? LinkPassword { get; init; }

    /// <summary>Updated note.</summary>
    public string? Note { get; init; }
}

/// <summary>
/// Request DTO for a bulk operation on multiple nodes.
/// </summary>
public sealed record BulkOperationDto
{
    /// <summary>List of node IDs to operate on.</summary>
    public required IReadOnlyList<Guid> NodeIds { get; init; }

    /// <summary>Target parent folder ID (for move/copy operations).</summary>
    public Guid? TargetParentId { get; init; }
}

/// <summary>
/// Request DTO for downloading multiple nodes as a ZIP archive.
/// </summary>
public sealed record BulkDownloadRequest
{
    /// <summary>List of node IDs to include in the ZIP.</summary>
    public required IReadOnlyList<Guid> NodeIds { get; init; }
}

/// <summary>
/// Response DTO for a bulk operation result.
/// </summary>
public sealed record BulkResultDto
{
    /// <summary>Total number of items processed.</summary>
    public int TotalCount { get; init; }

    /// <summary>Number of successful operations.</summary>
    public int SuccessCount { get; init; }

    /// <summary>Number of failed operations.</summary>
    public int FailureCount { get; init; }

    /// <summary>Per-item results.</summary>
    public required IReadOnlyList<BulkItemResultDto> Results { get; init; }
}

/// <summary>
/// Result for a single item in a bulk operation.
/// </summary>
public sealed record BulkItemResultDto
{
    /// <summary>Node ID that was operated on.</summary>
    public Guid NodeId { get; init; }

    /// <summary>Whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Error message if the operation failed.</summary>
    public string? Error { get; init; }
}

/// <summary>
/// Response DTO for a file tag.
/// </summary>
public sealed record FileTagDto
{
    /// <summary>Tag ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Tag name.</summary>
    public required string Name { get; init; }

    /// <summary>Tag color (hex).</summary>
    public string? Color { get; init; }

    /// <summary>When the tag was applied.</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Summary DTO for a user tag with usage count (used in sidebar tag list).
/// </summary>
public sealed record UserTagSummaryDto
{
    /// <summary>Tag name.</summary>
    public required string Name { get; init; }

    /// <summary>Representative color for this tag (from the most recent tag record).</summary>
    public string? Color { get; init; }

    /// <summary>Number of files/folders carrying this tag.</summary>
    public int FileCount { get; init; }
}

/// <summary>
/// Request DTO for bulk tag operations (add/remove a tag to/from multiple nodes).
/// </summary>
public sealed record BulkTagDto
{
    /// <summary>List of node IDs to operate on.</summary>
    public required IReadOnlyList<Guid> NodeIds { get; init; }

    /// <summary>Tag name to add or remove.</summary>
    public required string TagName { get; init; }

    /// <summary>Optional tag color (hex); only used for add operations.</summary>
    public string? Color { get; init; }
}

/// <summary>
/// Request DTO for adding a tag to a node.
/// </summary>
public sealed record AddTagDto
{
    /// <summary>Tag name.</summary>
    public required string Name { get; init; }

    /// <summary>Optional tag color (hex).</summary>
    public string? Color { get; init; }
}

/// <summary>
/// Request DTO for adding a comment to a node.
/// </summary>
public sealed record AddCommentDto
{
    /// <summary>Comment text content.</summary>
    public required string Content { get; init; }

    /// <summary>Parent comment ID for threaded replies (null for top-level).</summary>
    public Guid? ParentCommentId { get; init; }
}

/// <summary>
/// Request DTO for editing a comment.
/// </summary>
public sealed record EditCommentDto
{
    /// <summary>Updated comment text content.</summary>
    public required string Content { get; init; }
}

/// <summary>
/// Request DTO for setting a user's storage quota.
/// </summary>
public sealed record SetQuotaDto
{
    /// <summary>Maximum storage in bytes (0 = unlimited).</summary>
    public long MaxBytes { get; init; }
}

/// <summary>
/// Request DTO for labeling a file version.
/// </summary>
public sealed record LabelVersionDto
{
    /// <summary>Label text for the version.</summary>
    public required string Label { get; init; }
}

/// <summary>
/// Response DTO for a file comment.
/// </summary>
public sealed record FileCommentDto
{
    /// <summary>Comment ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>File node ID.</summary>
    public Guid FileNodeId { get; init; }

    /// <summary>Parent comment ID (null for top-level).</summary>
    public Guid? ParentCommentId { get; init; }

    /// <summary>Comment text content.</summary>
    public required string Content { get; init; }

    /// <summary>User who wrote the comment.</summary>
    public Guid CreatedByUserId { get; init; }

    /// <summary>When the comment was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>When the comment was last edited (null if never).</summary>
    public DateTime? UpdatedAt { get; init; }

    /// <summary>Number of replies.</summary>
    public int ReplyCount { get; init; }
}
