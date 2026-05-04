using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Services;

/// <summary>
/// Defines the contract for creating, restoring, and monitoring system backups.
/// Implementations handle archival of configuration, data files, and optional database dumps.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Creates a backup archive of the DotNetCloud instance.
    /// </summary>
    /// <param name="outputPath">Optional explicit output path. When <see langword="null"/>, uses the configured backup directory with a timestamped filename.</param>
    /// <param name="options">Optional backup options (scope, encryption, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="BackupResult"/> describing the outcome.</returns>
    Task<BackupResult> CreateBackupAsync(string? outputPath = null, BackupOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores the DotNetCloud instance from a backup archive.
    /// </summary>
    /// <param name="filePath">Path to the backup archive file.</param>
    /// <param name="options">Optional restore options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="BackupResult"/> describing the outcome.</returns>
    Task<BackupResult> RestoreBackupAsync(string filePath, RestoreOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of the last backup and any currently running backup.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current backup status.</returns>
    Task<BackupStatusInfo> GetStatusAsync(CancellationToken cancellationToken = default);
}
