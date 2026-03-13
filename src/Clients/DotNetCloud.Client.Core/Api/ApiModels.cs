using System.Text.Json.Serialization;

namespace DotNetCloud.Client.Core.Api;

/// <summary>
/// Represents a file or folder node from the server.
/// </summary>
public sealed record FileNodeResponse
{
    /// <summary>Unique identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>"File" or "Folder".</summary>
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

    /// <summary>POSIX permission bitmask (e.g. 493 = 0o755). Null for Windows-originated files.</summary>
    public int? PosixMode { get; init; }

    /// <summary>POSIX owner hint in "user:group" format. Null for Windows-originated files.</summary>
    public string? PosixOwnerHint { get; init; }

    /// <summary>Relative symlink target path. Non-null only when <see cref="NodeType"/> is <c>"SymbolicLink"</c>.</summary>
    public string? LinkTarget { get; init; }
}

/// <summary>
/// Server response for initiating a chunked upload.
/// </summary>
public sealed record UploadSessionResponse
{
    /// <summary>Session identifier.</summary>
    public required Guid SessionId { get; init; }

    /// <summary>Chunk hashes the server already has (for deduplication).</summary>
    [JsonPropertyName("existingChunks")]
    public IReadOnlyList<string> PresentChunks { get; init; } = [];

    /// <summary>Upload expiry timestamp (UTC).</summary>
    public DateTime ExpiresAt { get; init; }
}

/// <summary>
/// Server response for completing an upload.
/// </summary>
public sealed record CompleteUploadResponse
{
    /// <summary>The resulting file node.</summary>
    public required FileNodeResponse Node { get; init; }

    /// <summary>Version number assigned.</summary>
    public int VersionNumber { get; init; }
}

/// <summary>
/// Chunk manifest entry for a specific chunk.
/// </summary>
public sealed record ChunkManifestEntry
{
    /// <summary>Sequence index (0-based).</summary>
    public int Index { get; init; }

    /// <summary>SHA-256 hash of the chunk.</summary>
    public required string Hash { get; init; }

    /// <summary>Size of this chunk in bytes.</summary>
    public long Size { get; init; }
}

/// <summary>
/// Chunk manifest for a file version.
/// </summary>
public sealed record ChunkManifestResponse
{
    /// <summary>Ordered list of chunks.</summary>
    public IReadOnlyList<ChunkManifestEntry> Chunks { get; init; } = [];

    /// <summary>Total file size in bytes.</summary>
    public long TotalSize { get; init; }
}

/// <summary>
/// A single sync change returned by the server.
/// </summary>
public sealed record SyncChangeResponse
{
    /// <summary>Node ID.</summary>
    public Guid NodeId { get; init; }

    /// <summary>Node name.</summary>
    public required string Name { get; init; }

    /// <summary>"File" or "Folder".</summary>
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

    /// <summary>Server-assigned monotonic sync sequence number. Null for legacy rows.</summary>
    public long? SyncSequence { get; init; }

    /// <summary>POSIX permission bitmask. Null for Windows-originated files.</summary>
    public int? PosixMode { get; init; }

    /// <summary>POSIX owner/group hint. Null for Windows-originated files.</summary>
    public string? PosixOwnerHint { get; init; }

    /// <summary>Relative symlink target path. Non-null only when <see cref="NodeType"/> is <c>"SymbolicLink"</c>.</summary>
    public string? LinkTarget { get; init; }
}

/// <summary>
/// Paginated, cursor-aware response for <c>GET /api/v1/files/sync/changes?cursor=...</c>.
/// </summary>
public sealed record PagedSyncChangesResponse
{
    /// <summary>Changes in this page.</summary>
    public required IReadOnlyList<SyncChangeResponse> Changes { get; init; }

    /// <summary>Opaque cursor to pass on the next request to retrieve the following page.</summary>
    public string? NextCursor { get; init; }

    /// <summary>Whether more changes exist beyond this page.</summary>
    public bool HasMore { get; init; }
}

/// <summary>
/// A node in a server-side folder tree snapshot.
/// </summary>
public sealed record SyncTreeNodeResponse
{
    /// <summary>Node ID.</summary>
    public Guid NodeId { get; init; }

    /// <summary>Node name.</summary>
    public required string Name { get; init; }

    /// <summary>"File" or "Folder".</summary>
    public required string NodeType { get; init; }

    /// <summary>Content hash for comparison.</summary>
    public string? ContentHash { get; init; }

    /// <summary>Size in bytes.</summary>
    public long Size { get; init; }

    /// <summary>Last updated timestamp.</summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>Children (for folders).</summary>
    public IReadOnlyList<SyncTreeNodeResponse> Children { get; init; } = [];

    /// <summary>POSIX permission bitmask. Null for Windows-originated files.</summary>
    public int? PosixMode { get; init; }

    /// <summary>POSIX owner/group hint. Null for Windows-originated files.</summary>
    public string? PosixOwnerHint { get; init; }

    /// <summary>Relative symlink target path. Non-null only when <see cref="NodeType"/> is <c>"SymbolicLink"</c>.</summary>
    public string? LinkTarget { get; init; }
}

/// <summary>
/// An action the client must take to reconcile with the server.
/// </summary>
public sealed record SyncActionResponse
{
    /// <summary>Node ID this action applies to.</summary>
    public Guid NodeId { get; init; }

    /// <summary>"Download", "Upload", "Delete", or "Conflict".</summary>
    public required string Action { get; init; }

    /// <summary>Reason for the action.</summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Reconciliation result from the server.
/// </summary>
public sealed record ReconcileResponse
{
    /// <summary>Actions the client should take.</summary>
    public IReadOnlyList<SyncActionResponse> Actions { get; init; } = [];
}

/// <summary>
/// Client node entry for reconciliation request.
/// </summary>
public sealed record ClientNodeEntry
{
    /// <summary>Node ID.</summary>
    public Guid NodeId { get; init; }

    /// <summary>Content hash on the client side.</summary>
    public string? ContentHash { get; init; }

    /// <summary>Last updated timestamp on the client.</summary>
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Quota information for the current user.
/// </summary>
public sealed record QuotaResponse
{
    /// <summary>User ID.</summary>
    public Guid UserId { get; init; }

    /// <summary>Maximum allowed bytes (0 = unlimited).</summary>
    public long QuotaBytes { get; init; }

    /// <summary>Currently used bytes.</summary>
    public long UsedBytes { get; init; }

    /// <summary>Percentage used (0–100).</summary>
    public double PercentUsed => QuotaBytes > 0 ? UsedBytes * 100.0 / QuotaBytes : 0;
}

/// <summary>
/// OAuth2 token response from the token endpoint.
/// </summary>
public sealed record TokenResponse
{
    /// <summary>Access token.</summary>
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    /// <summary>Refresh token.</summary>
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    /// <summary>Token type (typically "Bearer").</summary>
    [JsonPropertyName("token_type")]
    public string TokenType { get; init; } = "Bearer";

    /// <summary>Expiry in seconds.</summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    /// <summary>Granted scopes.</summary>
    public string? Scope { get; init; }
}

/// <summary>
/// Paged result wrapper.
/// </summary>
public sealed record PagedResponse<T>
{
    /// <summary>Items in this page.</summary>
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>Total item count across all pages.</summary>
    public int TotalCount { get; init; }

    /// <summary>Current page (0-based).</summary>
    public int Page { get; init; }

    /// <summary>Items per page.</summary>
    public int PageSize { get; init; }
}
