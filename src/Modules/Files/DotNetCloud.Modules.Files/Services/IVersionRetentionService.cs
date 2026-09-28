namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Result of a file version retention pass.
/// </summary>
/// <param name="VersioningEnabled">Whether version history is being recorded at all.</param>
/// <param name="FilesScanned">Number of files that had at least one version to consider.</param>
/// <param name="VersionsDeleted">Number of versions removed by the pass.</param>
public sealed record VersionRetentionSweepResult(
    bool VersioningEnabled,
    int FilesScanned,
    int VersionsDeleted);

/// <summary>
/// Applies the effective file version retention policy (max versions per file and max age of a
/// version) to every file that has version history.
/// </summary>
/// <remarks>
/// Runs on a schedule from the module host's background cleanup service, and on demand from the
/// <c>/admin/files</c> page ("Run retention now") so an administrator can see the configured
/// limits take effect immediately instead of waiting for the next daily pass.
/// </remarks>
public interface IVersionRetentionService
{
    /// <summary>Runs the retention policy once and reports what it did.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<VersionRetentionSweepResult> RunAsync(CancellationToken cancellationToken = default);
}
