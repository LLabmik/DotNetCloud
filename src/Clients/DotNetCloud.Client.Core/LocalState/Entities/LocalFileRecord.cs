namespace DotNetCloud.Client.Core.LocalState;

/// <summary>
/// Tracks whether a local file has its content downloaded (hydrated)
/// or exists only as a metadata placeholder (cloud-only).
/// </summary>
public enum HydrationState
{
    /// <summary>Content is downloaded and available locally.</summary>
    Hydrated = 0,

    /// <summary>Metadata-only placeholder. Content downloads on first access.</summary>
    CloudOnly = 1,

    /// <summary>Content is downloaded and pinned — exempt from dehydration/eviction.</summary>
    Pinned = 2,

    /// <summary>Content is being downloaded right now.</summary>
    Downloading = 3,
}

/// <summary>
/// Tracks the sync state of a local file.
/// </summary>
public sealed class LocalFileRecord
{
    /// <summary>Row ID (auto-increment).</summary>
    public int Id { get; set; }

    /// <summary>Full local file system path.</summary>
    public required string LocalPath { get; set; }

    /// <summary>Corresponding server node ID.</summary>
    public Guid NodeId { get; set; }

    /// <summary>SHA-256 content hash at last sync.</summary>
    public string? ContentHash { get; set; }

    /// <summary>UTC timestamp of the last successful sync for this file.</summary>
    public DateTime LastSyncedAt { get; set; }

    /// <summary>Local file modification time at last sync.</summary>
    public DateTime LocalModifiedAt { get; set; }

    /// <summary>Sync state tag (Synced, Pending, Conflict).</summary>
    public string SyncStateTag { get; set; } = "Synced";

    /// <summary>POSIX file mode last synced to/from server. Null on Windows or if not yet synced.</summary>
    public int? PosixMode { get; set; }

    /// <summary>Symlink target path stored at last sync. Non-null only for symbolic link nodes.</summary>
    public string? LinkTarget { get; set; }

    /// <summary>Virtual file hydration state. Defaults to Hydrated for backward compatibility.</summary>
    public HydrationState HydrationState { get; set; } = HydrationState.Hydrated;
}
