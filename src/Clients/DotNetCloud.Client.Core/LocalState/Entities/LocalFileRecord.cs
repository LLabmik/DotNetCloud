namespace DotNetCloud.Client.Core.LocalState;

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
}
