using System.IO.Compression;
using System.Text.Json.Serialization;

namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Maps the configured compression level name to a <see cref="CompressionLevel"/>.
/// </summary>
public static class BackupCompression
{
    /// <summary>The default level (<c>optimal</c>).</summary>
    public const string Default = "optimal";

    /// <summary>
    /// Parses <c>store</c>, <c>fastest</c> or <c>optimal</c> (case-insensitive). Unknown and empty
    /// values fall back to <see cref="CompressionLevel.Optimal"/>.
    /// </summary>
    public static CompressionLevel Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "store" or "none" or "no-compression" => CompressionLevel.NoCompression,
        "fastest" or "fast" => CompressionLevel.Fastest,
        _ => CompressionLevel.Optimal,
    };
}

/// <summary>
/// Progress reported while a backup runs. Counts refer to the data directory walk.
/// </summary>
/// <param name="Phase">Current phase (starting, database, data, system, finalizing).</param>
/// <param name="FilesProcessed">Files written so far.</param>
/// <param name="FilesTotal">Total data files to write, or 0 when not yet known.</param>
/// <param name="BytesProcessed">Data bytes written so far.</param>
/// <param name="BytesTotal">Total data bytes to write, or 0 when not yet known.</param>
public sealed record BackupProgress(
    string Phase, int FilesProcessed, int FilesTotal, long BytesProcessed, long BytesTotal);

/// <summary>
/// Options for creating a backup archive.
/// </summary>
public class BackupOptions
{
    /// <summary>
    /// Whether to include a database dump in the backup.
    /// </summary>
    public bool IncludeDatabaseDump { get; set; } = true;

    /// <summary>
    /// The database provider identifier (e.g., "PostgreSQL", "SQLServer").
    /// Used to select the correct dump tool.
    /// </summary>
    public string? DatabaseProvider { get; set; }

    /// <summary>
    /// The database connection string for performing a dump.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Path to the data directory to include in the backup.
    /// </summary>
    public string? DataDirectory { get; set; }

    /// <summary>
    /// Directory where backup archives should be stored (used when <c>outputPath</c> is not specified).
    /// </summary>
    public string? BackupDirectory { get; set; }

    /// <summary>
    /// Optional passphrase for AES-256 encryption of the archive.
    /// </summary>
    public string? EncryptionPassphrase { get; set; }

    /// <summary>
    /// Whether to include file storage data.
    /// </summary>
    public bool IncludeFileStorage { get; set; } = true;

    /// <summary>
    /// Whether to include per-module data directories.
    /// </summary>
    public bool IncludeModuleData { get; set; } = true;

    /// <summary>
    /// Compression level name: <c>store</c> (no compression — best for media), <c>fastest</c> or
    /// <c>optimal</c> (default). See <see cref="BackupCompression"/>.
    /// </summary>
    public string Compression { get; set; } = BackupCompression.Default;

    /// <summary>
    /// Optional in-process progress callback. Not serialized; used by the background job runner.
    /// </summary>
    [JsonIgnore]
    public Action<BackupProgress>? Progress { get; set; }
}

/// <summary>
/// Options for restoring from a backup archive.
/// </summary>
public class RestoreOptions
{
    /// <summary>
    /// Whether to restore the database from the included dump.
    /// </summary>
    public bool RestoreDatabase { get; set; }

    /// <summary>
    /// The database provider identifier (e.g., "PostgreSQL", "SQLServer").
    /// </summary>
    public string? DatabaseProvider { get; set; }

    /// <summary>
    /// The database connection string for performing a restore.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Target data directory for restoring <c>data/</c> from the archive. When omitted, the
    /// data directory is not restored. The default-user-content template lives in this directory,
    /// so restoring it restores the template byte-for-byte.
    /// </summary>
    public string? DataDirectory { get; set; }
}

/// <summary>
/// Describes the outcome of a backup or restore operation.
/// </summary>
public class BackupResult
{
    /// <summary>
    /// Whether the operation completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The path to the backup archive file (for backup operations).
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// A human-readable error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The number of files included in the archive.
    /// </summary>
    public int FileCount { get; set; }

    /// <summary>
    /// The total size of the archive in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// The duration of the operation.
    /// </summary>
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Status information about backups.
/// </summary>
public class BackupStatusInfo
{
    /// <summary>
    /// Whether a backup is currently running.
    /// </summary>
    public bool IsRunning { get; set; }

    /// <summary>When the running (or last) job started.</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>When the running (or last) job finished.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Current phase (starting, database, data, system, finalizing, completed, failed, cancelled).</summary>
    public string? Phase { get; set; }

    /// <summary>Data files written so far.</summary>
    public int FilesProcessed { get; set; }

    /// <summary>Total data files to write, or 0 when not yet known.</summary>
    public int FilesTotal { get; set; }

    /// <summary>Data bytes written so far.</summary>
    public long BytesProcessed { get; set; }

    /// <summary>Total data bytes to write, or 0 when not yet known.</summary>
    public long BytesTotal { get; set; }

    /// <summary>Whether a cancellation has been requested for the running job.</summary>
    public bool CancelRequested { get; set; }

    /// <summary>The error message of the last failed job, if any.</summary>
    public string? LastError { get; set; }

    /// <summary>
    /// The timestamp of the last completed backup, if any.
    /// </summary>
    public DateTime? LastBackupTime { get; set; }

    /// <summary>
    /// Whether the last backup succeeded.
    /// </summary>
    public bool? LastBackupSuccess { get; set; }

    /// <summary>
    /// The path to the last backup archive, if any.
    /// </summary>
    public string? LastBackupPath { get; set; }

    /// <summary>
    /// The size of the last backup in bytes.
    /// </summary>
    public long? LastBackupSizeBytes { get; set; }

    /// <summary>The number of files in the last completed backup, if known.</summary>
    public int? LastBackupFileCount { get; set; }
}
