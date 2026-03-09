namespace DotNetCloud.Client.Core.LocalState;

/// <summary>
/// Persisted record of a detected file conflict. Created when two versions diverge and cannot be
/// auto-resolved. <see cref="ResolvedAt"/> is null until the conflict is resolved.
/// </summary>
public sealed class ConflictRecord
{
    /// <summary>Auto-incremented row ID.</summary>
    public int Id { get; set; }

    /// <summary>Original local path of the conflicting file.</summary>
    public required string OriginalPath { get; set; }

    /// <summary>
    /// Path of the conflict copy created for the local version.
    /// Empty string when the conflict was auto-resolved without creating a copy.
    /// </summary>
    public string ConflictCopyPath { get; set; } = string.Empty;

    /// <summary>Server node ID of the conflicting file.</summary>
    public Guid NodeId { get; set; }

    /// <summary>Local file modification time at conflict detection.</summary>
    public DateTime LocalModifiedAt { get; set; }

    /// <summary>Remote file modification time at conflict detection.</summary>
    public DateTime RemoteModifiedAt { get; set; }

    /// <summary>UTC timestamp when the conflict was detected.</summary>
    public DateTime DetectedAt { get; set; }

    /// <summary>UTC timestamp when the conflict was resolved, or null if still unresolved.</summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// How the conflict was resolved. Null if unresolved.
    /// Known values: <c>"kept-local"</c>, <c>"kept-server"</c>, <c>"kept-both"</c>,
    /// <c>"merged"</c>, <c>"auto-identical"</c>, <c>"auto-fast-forward"</c>,
    /// <c>"auto-merged"</c>, <c>"auto-newer-wins"</c>, <c>"auto-append"</c>.
    /// </summary>
    public string? Resolution { get; set; }

    /// <summary>
    /// SHA-256 hash of the common-ancestor version (last synced state shared by both sides).
    /// Allows three-way merge when combined with local and remote content.
    /// Null if not available (e.g. first-ever version or no sync history).
    /// </summary>
    public string? BaseContentHash { get; set; }

    /// <summary>Whether the conflict was resolved automatically without user intervention.</summary>
    public bool AutoResolved { get; set; }
}
