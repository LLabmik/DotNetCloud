namespace DotNetCloud.Client.Core.Conflict;

/// <summary>
/// Detects and resolves sync conflicts by creating conflict copies.
/// </summary>
public interface IConflictResolver
{
    /// <summary>Raised when a conflict copy is created.</summary>
    event EventHandler<ConflictDetectedEventArgs>? ConflictDetected;

    /// <summary>
    /// Resolves a conflict by creating a conflict copy of the local file,
    /// then allowing the remote version to be downloaded.
    /// Both versions are preserved; no data is silently lost.
    /// </summary>
    Task ResolveAsync(ConflictInfo conflict, CancellationToken cancellationToken = default);
}

/// <summary>
/// Event arguments raised when a conflict is detected and resolved.
/// </summary>
public sealed class ConflictDetectedEventArgs : EventArgs
{
    /// <summary>The original local path that had a conflict.</summary>
    public required string OriginalPath { get; init; }

    /// <summary>The path of the conflict copy that was created.</summary>
    public required string ConflictCopyPath { get; init; }
}
