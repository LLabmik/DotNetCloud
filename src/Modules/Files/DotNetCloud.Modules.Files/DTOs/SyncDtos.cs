namespace DotNetCloud.Modules.Files.DTOs;

/// <summary>
/// Represents a single change detected since a given timestamp for sync.
/// </summary>
public sealed record SyncChangeDto
{
    /// <summary>Node ID.</summary>
    public Guid NodeId { get; init; }

    /// <summary>Node name.</summary>
    public required string Name { get; init; }

    /// <summary>Node type (File or Folder).</summary>
    public required string NodeType { get; init; }

    /// <summary>Parent folder ID.</summary>
    public Guid? ParentId { get; init; }

    /// <summary>Content hash for change detection.</summary>
    public string? ContentHash { get; init; }

    /// <summary>Size in bytes.</summary>
    public long Size { get; init; }

    /// <summary>Last updated timestamp.</summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>Whether the node was deleted.</summary>
    public bool IsDeleted { get; init; }

    /// <summary>When the node was deleted (if applicable).</summary>
    public DateTime? DeletedAt { get; init; }

    /// <summary>Sync sequence number for cursor-based sync (null for legacy rows).</summary>
    public long? SyncSequence { get; init; }

    /// <summary>
    /// ID of the device that created or last modified this node.
    /// Clients use this for echo suppression — skip re-downloading changes originated from this device.
    /// </summary>
    public Guid? OriginatingDeviceId { get; init; }

    /// <summary>POSIX file mode bitmask. Null for folders or Windows-uploaded files.</summary>
    public int? PosixMode { get; init; }

    /// <summary>POSIX owner hint in <c>"user:group"</c> format. Null for Windows-uploaded files.</summary>
    public string? PosixOwnerHint { get; init; }

    /// <summary>
    /// For symlink nodes (<c>NodeType == "SymbolicLink"</c>): the relative target path.
    /// Null for files and folders.
    /// </summary>
    public string? LinkTarget { get; init; }
}

/// <summary>
/// Paginated, cursor-aware response for <c>GET /api/v1/files/sync/changes</c>.
/// </summary>
public sealed record PagedSyncChangesDto
{
    /// <summary>Changes in this page.</summary>
    public required IReadOnlyList<SyncChangeDto> Changes { get; init; }

    /// <summary>Opaque cursor to pass as <c>cursor</c> on the next request to get the following page.</summary>
    public string? NextCursor { get; init; }

    /// <summary>Whether more changes exist beyond this page.</summary>
    public bool HasMore { get; init; }
}

/// <summary>
/// Represents a node in a folder tree for sync.
/// </summary>
public sealed record SyncTreeNodeDto
{
    /// <summary>Node ID.</summary>
    public Guid NodeId { get; init; }

    /// <summary>Node name.</summary>
    public required string Name { get; init; }

    /// <summary>Node type (File or Folder).</summary>
    public required string NodeType { get; init; }

    /// <summary>Content hash for sync comparison.</summary>
    public string? ContentHash { get; init; }

    /// <summary>Size in bytes.</summary>
    public long Size { get; init; }

    /// <summary>Last updated timestamp.</summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>Children (for folders).</summary>
    public IReadOnlyList<SyncTreeNodeDto> Children { get; init; } = [];

    /// <summary>POSIX file mode bitmask. Null for folders or Windows-uploaded files.</summary>
    public int? PosixMode { get; init; }

    /// <summary>POSIX owner hint in <c>"user:group"</c> format. Null for Windows-uploaded files.</summary>
    public string? PosixOwnerHint { get; init; }

    /// <summary>
    /// For symlink nodes (<c>NodeType == "SymbolicLink"</c>): the relative target path.
    /// Null for files and folders.
    /// </summary>
    public string? LinkTarget { get; init; }
}

/// <summary>
/// Request DTO for sync reconciliation.
/// </summary>
public sealed record SyncReconcileRequestDto
{
    /// <summary>Optional folder ID to scope reconciliation.</summary>
    public Guid? FolderId { get; init; }

    /// <summary>Client's view of the file tree.</summary>
    public required IReadOnlyList<SyncClientNodeDto> ClientNodes { get; init; }
}

/// <summary>
/// Represents a single node in the client's file tree for reconciliation.
/// </summary>
public sealed record SyncClientNodeDto
{
    /// <summary>Node ID.</summary>
    public Guid NodeId { get; init; }

    /// <summary>Content hash on the client side.</summary>
    public string? ContentHash { get; init; }

    /// <summary>Last updated timestamp on the client.</summary>
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Result of a sync reconciliation operation.
/// </summary>
public sealed record SyncReconcileResultDto
{
    /// <summary>Actions the client should take.</summary>
    public required IReadOnlyList<SyncActionDto> Actions { get; init; }
}

/// <summary>
/// A single sync action for the client to execute.
/// </summary>
public sealed record SyncActionDto
{
    /// <summary>Node ID this action applies to.</summary>
    public Guid NodeId { get; init; }

    /// <summary>Action type: Download, Upload, Delete, or Conflict.</summary>
    public required string Action { get; init; }

    /// <summary>Reason for the action.</summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Request body for <c>POST /api/v1/files/sync/ack</c>.
/// Acknowledges that a device has processed changes up to a given sequence.
/// </summary>
public sealed record SyncCursorAckDto
{
    /// <summary>The device making the acknowledgement.</summary>
    public Guid DeviceId { get; init; }

    /// <summary>The highest sync sequence number successfully processed.</summary>
    public long AcknowledgedSequence { get; init; }
}

/// <summary>
/// Response from <c>GET /api/v1/files/sync/device-cursor</c>.
/// Contains the server-side cursor for a specific device.
/// </summary>
public sealed record DeviceCursorDto
{
    /// <summary>The device ID.</summary>
    public Guid DeviceId { get; init; }

    /// <summary>The last acknowledged sync sequence, or null if no cursor exists.</summary>
    public long? LastAcknowledgedSequence { get; init; }

    /// <summary>Encoded cursor string for use with <c>sync/changes</c>, or null if no cursor exists.</summary>
    public string? Cursor { get; init; }

    /// <summary>When the cursor was last updated.</summary>
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// Admin view of a device's sync status, including lag relative to the user's current sequence.
/// </summary>
public sealed record DeviceSyncStatusDto
{
    /// <summary>Device identifier.</summary>
    public Guid DeviceId { get; init; }

    /// <summary>The user who owns this device.</summary>
    public Guid UserId { get; init; }

    /// <summary>Display name of the user.</summary>
    public string? UserDisplayName { get; init; }

    /// <summary>Human-readable device name.</summary>
    public required string DeviceName { get; init; }

    /// <summary>Platform (Linux, Windows, Android, etc.).</summary>
    public string? Platform { get; init; }

    /// <summary>Client version string.</summary>
    public string? ClientVersion { get; init; }

    /// <summary>Whether the device is active.</summary>
    public bool IsActive { get; init; }

    /// <summary>The user's current (latest) sync sequence on the server.</summary>
    public long CurrentSequence { get; init; }

    /// <summary>The last sequence this device acknowledged processing, or null if never acked.</summary>
    public long? LastAcknowledgedSequence { get; init; }

    /// <summary>How many sequences behind the device is (CurrentSequence - LastAcknowledgedSequence).</summary>
    public long Lag { get; init; }

    /// <summary>When the device last contacted the server.</summary>
    public DateTime LastSeenAt { get; init; }

    /// <summary>When the cursor was last updated, or null if no ack received.</summary>
    public DateTime? CursorUpdatedAt { get; init; }
}
