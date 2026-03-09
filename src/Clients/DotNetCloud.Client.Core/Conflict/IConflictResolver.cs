namespace DotNetCloud.Client.Core.Conflict;

/// <summary>
/// Detects and resolves sync conflicts by running an auto-resolution pipeline followed
/// by conflict-copy creation when all auto-resolution strategies fail.
/// </summary>
public interface IConflictResolver
{
    /// <summary>Raised when an auto-resolution strategy successfully resolves a conflict.</summary>
    event EventHandler<ConflictAutoResolvedEventArgs>? AutoResolved;

    /// <summary>Raised when all auto-resolution strategies fail and a conflict copy is created.</summary>
    event EventHandler<ConflictDetectedEventArgs>? ConflictDetected;

    /// <summary>
    /// Attempts to resolve a conflict by running the auto-resolution pipeline (up to 5 strategies).
    /// If all strategies fail, creates a conflict copy and queues the server version for download.
    /// Returns a <see cref="ConflictResolutionOutcome"/> indicating the action taken.
    /// </summary>
    Task<ConflictResolutionOutcome> ResolveAsync(ConflictInfo conflict, CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of a <see cref="IConflictResolver.ResolveAsync"/> call.
/// The caller (SyncEngine) uses this to take the appropriate follow-up action.
/// </summary>
public enum ConflictResolutionOutcome
{
    /// <summary>
    /// No auto-resolution strategy applied. A conflict copy was created at the local path.
    /// The server version will be downloaded automatically on the next sync cycle.
    /// </summary>
    ConflictCopyCreated = 0,

    /// <summary>
    /// Auto-resolved: the local version was kept. The caller should NOT queue a download.
    /// The local file will be uploaded on the next scan.
    /// </summary>
    AutoResolvedLocalWins,

    /// <summary>
    /// Auto-resolved: the server version should replace the local file.
    /// The caller should queue a <c>PendingDownload</c> for this file.
    /// </summary>
    AutoResolvedServerWins,

    /// <summary>
    /// Auto-resolved: both versions are byte-identical. No upload or download is needed.
    /// The caller should update the local file record to mark it as synced.
    /// </summary>
    AutoResolvedIdentical,
}

/// <summary>
/// Event arguments raised when a conflict copy is created (manual resolution required).
/// </summary>
public sealed class ConflictDetectedEventArgs : EventArgs
{
    /// <summary>The original local path that had a conflict.</summary>
    public required string OriginalPath { get; init; }

    /// <summary>The path of the conflict copy that was created.</summary>
    public required string ConflictCopyPath { get; init; }
}

/// <summary>
/// Event arguments raised when a conflict is automatically resolved.
/// </summary>
public sealed class ConflictAutoResolvedEventArgs : EventArgs
{
    /// <summary>The local path of the resolved file.</summary>
    public required string LocalPath { get; init; }

    /// <summary>Name of the auto-resolution strategy that succeeded.</summary>
    public required string Strategy { get; init; }

    /// <summary>The resolution string recorded in the conflict record.</summary>
    public required string Resolution { get; init; }

    /// <summary>The outcome returned by the resolver.</summary>
    public ConflictResolutionOutcome Outcome { get; init; }
}
