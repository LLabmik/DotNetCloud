namespace DotNetCloud.Core.DTOs;

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
    /// The database provider identifier (e.g., "PostgreSQL", "SQLServer", "MariaDB").
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
    /// The database provider identifier (e.g., "PostgreSQL", "SQLServer", "MariaDB").
    /// </summary>
    public string? DatabaseProvider { get; set; }

    /// <summary>
    /// The database connection string for performing a restore.
    /// </summary>
    public string? ConnectionString { get; set; }
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
}
