namespace DotNetCloud.Client.Core.Conflict;

/// <summary>
/// Describes a conflict between a local file and its remote counterpart.
/// </summary>
public sealed class ConflictInfo
{
    /// <summary>Full local file path of the conflicting file.</summary>
    public required string LocalPath { get; init; }

    /// <summary>Server node ID of the conflicting file.</summary>
    public required Guid NodeId { get; init; }

    /// <summary>Timestamp of the remote version.</summary>
    public DateTime RemoteUpdatedAt { get; init; }

    /// <summary>Content hash of the remote version.</summary>
    public string? RemoteContentHash { get; init; }

    /// <summary>Path of the local state database for this sync context.</summary>
    public string StateDatabasePath { get; init; } = string.Empty;

    /// <summary>SHA-256 hash of the current local file content. Null if not pre-computed.</summary>
    public string? LocalContentHash { get; init; }

    /// <summary>
    /// SHA-256 hash of the last-synced (common ancestor) version.
    /// Typically <see cref="DotNetCloud.Client.Core.LocalState.LocalFileRecord.ContentHash"/> at time of conflict detection.
    /// </summary>
    public string? BaseContentHash { get; init; }

    /// <summary>Modification timestamp of the local file at conflict detection.</summary>
    public DateTime LocalModifiedAt { get; init; }

    /// <summary>
    /// User ID who owns this sync context (local device owner).
    /// Used by Strategy 4 (newer-wins) to identify single-user conflicts.
    /// </summary>
    public Guid LocalUserId { get; init; }

    /// <summary>
    /// Optional full text content of the local file.
    /// When provided, enables text-based auto-resolution strategies (3, 5).
    /// </summary>
    public string? LocalContent { get; init; }

    /// <summary>
    /// Optional full text content of the server version.
    /// When provided, enables text-based auto-resolution strategies (3, 5).
    /// </summary>
    public string? ServerContent { get; init; }

    /// <summary>
    /// Optional full text content of the base (common ancestor) version.
    /// When provided, enables three-way text merge (Strategy 3).
    /// </summary>
    public string? BaseContent { get; init; }
}
