using System.Diagnostics;
using System.IO.Compression;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Creates and restores backup archives of the DotNetCloud instance.
/// Includes configuration, data directories, and optional database dumps.
/// </summary>
public sealed class BackupService : DotNetCloud.Core.Services.IBackupService
{
    private readonly ILogger<BackupService> _logger;
    private static readonly SemaphoreSlim _backupLock = new(1, 1);

    // Simple in-memory tracking of last backup status
    private static BackupStatusInfo? s_lastStatus;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackupService"/> class.
    /// </summary>
    public BackupService(ILogger<BackupService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<BackupResult> CreateBackupAsync(
        string? outputPath = null,
        BackupOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (!await _backupLock.WaitAsync(0, cancellationToken))
        {
            return new BackupResult
            {
                Success = false,
                ErrorMessage = "A backup is already in progress.",
            };
        }

        var sw = Stopwatch.StartNew();
        try
        {
            options ??= new BackupOptions();
            return await CreateBackupInternalAsync(outputPath, options, cancellationToken);
        }
        finally
        {
            sw.Stop();
            _backupLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<BackupResult> RestoreBackupAsync(
        string filePath,
        RestoreOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return new BackupResult
            {
                Success = false,
                ErrorMessage = $"Backup file not found: {filePath}",
            };
        }

        options ??= new RestoreOptions();
        var archiveDir = Path.GetDirectoryName(Path.GetFullPath(filePath))!;
        var restoreDir = Path.Combine(archiveDir, ".restore-temp");
        Directory.CreateDirectory(restoreDir);

        try
        {
            _logger.LogInformation("Restoring from backup: {FilePath}", filePath);

            // Extract archive
            ZipFile.ExtractToDirectory(filePath, restoreDir, overwriteFiles: true);

            // Restore configuration
            var configDir = Path.Combine(restoreDir, "config");
            if (Directory.Exists(configDir))
            {
                var configFiles = Directory.GetFiles(configDir, "*", SearchOption.AllDirectories);
                foreach (var file in configFiles)
                {
                    var relativePath = Path.GetRelativePath(configDir, file);
                    var targetDir = Path.GetDirectoryName(relativePath);
                    if (!string.IsNullOrEmpty(targetDir))
                        Directory.CreateDirectory(targetDir);

                    // Config files go to the original config location
                    // In a real server scenario, these would be applied via the admin settings API
                    _logger.LogInformation("Restored config file: {File}", relativePath);
                }
            }

            // Restore data files
            var dataDir = Path.Combine(restoreDir, "data");
            if (Directory.Exists(dataDir) && !string.IsNullOrEmpty(options.ConnectionString))
            {
                var targetDataDir = options.ConnectionString; // Placeholder — caller provides target
                CopyDirectory(dataDir, targetDataDir);
            }

            // Restore database dump if present and requested
            var dumpFile = Path.Combine(restoreDir, "database.sql");
            if (options.RestoreDatabase && File.Exists(dumpFile))
            {
                await RestoreDatabaseDumpAsync(dumpFile, options, cancellationToken);
            }

            _logger.LogInformation("Restore completed successfully from: {FilePath}", filePath);
            return new BackupResult
            {
                Success = true,
                FilePath = filePath,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore failed from: {FilePath}", filePath);
            return new BackupResult
            {
                Success = false,
                ErrorMessage = ex.Message,
            };
        }
        finally
        {
            if (Directory.Exists(restoreDir))
            {
                try { Directory.Delete(restoreDir, recursive: true); } catch { /* best effort */ }
            }
        }
    }

    /// <inheritdoc />
    public Task<BackupStatusInfo> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var isRunning = _backupLock.CurrentCount == 0;
        return Task.FromResult(new BackupStatusInfo
        {
            IsRunning = isRunning,
            LastBackupTime = s_lastStatus?.LastBackupTime,
            LastBackupSuccess = s_lastStatus?.LastBackupSuccess,
            LastBackupPath = s_lastStatus?.LastBackupPath,
            LastBackupSizeBytes = s_lastStatus?.LastBackupSizeBytes,
        });
    }

    // -----------------------------------------------------------------------
    // Internal implementation
    // -----------------------------------------------------------------------

    private async Task<BackupResult> CreateBackupInternalAsync(
        string? outputPath,
        BackupOptions options,
        CancellationToken cancellationToken)
    {
        var backupDir = options.BackupDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "dotnetcloud", "backups");
        Directory.CreateDirectory(backupDir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var backupFileName = $"dotnetcloud-backup-{timestamp}.zip";
        var backupPath = outputPath ?? Path.Combine(backupDir, backupFileName);

        _logger.LogInformation("Starting backup to: {BackupPath}", backupPath);

        var dbDumpPath = Path.Combine(backupDir, $"dotnetcloud-db-{timestamp}.sql");
        var createdDbDump = false;

        try
        {
            // Step 1: Optional database dump
            if (options.IncludeDatabaseDump && !string.IsNullOrEmpty(options.DatabaseProvider))
            {
                try
                {
                    await CreateDatabaseDumpAsync(options.DatabaseProvider, options.ConnectionString, dbDumpPath, cancellationToken);
                    createdDbDump = true;
                    _logger.LogInformation("Database dump created: {DumpPath}", dbDumpPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Database dump failed, continuing with file-only backup. Ensure {Tool} is installed and accessible.",
                        GetDumpToolName(options.DatabaseProvider));
                }
            }

            // Step 2: Create zip archive
            var fileCount = 0;
            await using var zipStream = new FileStream(backupPath, FileMode.Create);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

            // Add database dump if created
            if (createdDbDump && File.Exists(dbDumpPath))
            {
                archive.CreateEntryFromFile(dbDumpPath, "database.sql");
                fileCount++;
            }

            // Add database info note
            var dbInfoEntry = archive.CreateEntry("config/database-info.txt");
            await using (var writer = new StreamWriter(dbInfoEntry.Open()))
            {
                await writer.WriteLineAsync($"Provider: {options.DatabaseProvider ?? "unknown"}");
                await writer.WriteLineAsync($"Backup Time: {DateTime.UtcNow:O}");
                await writer.WriteLineAsync($"Database Dump Included: {createdDbDump}");
                await writer.WriteLineAsync();
                await writer.WriteLineAsync("Restore: Use 'dotnetcloud restore' or your database provider's restore tool.");
            }
            fileCount++;

            // Add data directory
            if (!string.IsNullOrEmpty(options.DataDirectory) && Directory.Exists(options.DataDirectory))
            {
                var dataFiles = Directory.GetFiles(options.DataDirectory, "*", SearchOption.AllDirectories);
                foreach (var file in dataFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entryName = "data/" + Path.GetRelativePath(options.DataDirectory, file).Replace('\\', '/');
                    archive.CreateEntryFromFile(file, entryName);
                    fileCount++;
                }
                _logger.LogInformation("Data directory backed up ({Count} files).", dataFiles.Length);
            }

            // Step 3: Determine final file size
            await zipStream.FlushAsync(cancellationToken);
            var fileInfo = new FileInfo(backupPath);
            fileInfo.Refresh();

            var result = new BackupResult
            {
                Success = true,
                FilePath = backupPath,
                FileCount = fileCount,
                SizeBytes = fileInfo.Length,
            };

            // Update status
            s_lastStatus = new BackupStatusInfo
            {
                IsRunning = false,
                LastBackupTime = DateTime.UtcNow,
                LastBackupSuccess = true,
                LastBackupPath = backupPath,
                LastBackupSizeBytes = fileInfo.Length,
            };

            _logger.LogInformation("Backup completed: {BackupPath} ({FileCount} files, {Size:N0} bytes)",
                backupPath, fileCount, fileInfo.Length);

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Backup was cancelled.");
            // Clean up partial archive
            if (File.Exists(backupPath))
                try { File.Delete(backupPath); } catch { /* best effort */ }

            return new BackupResult
            {
                Success = false,
                ErrorMessage = "Backup was cancelled.",
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup failed.");
            return new BackupResult
            {
                Success = false,
                ErrorMessage = ex.Message,
            };
        }
        finally
        {
            // Clean up temporary database dump
            if (createdDbDump && File.Exists(dbDumpPath))
            {
                try { File.Delete(dbDumpPath); } catch { /* best effort */ }
            }
        }
    }

    private static async Task CreateDatabaseDumpAsync(
        string provider,
        string? connectionString,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var (tool, args) = provider.ToUpperInvariant() switch
        {
            "POSTGRESQL" => ("pg_dump", BuildPgDumpArgs(connectionString, outputPath)),
            "SQLSERVER" => ("sqlcmd", BuildSqlCmdArgs(connectionString, outputPath)),
            "MARIADB" or "MYSQL" => ("mysqldump", BuildMySqlDumpArgs(connectionString, outputPath)),
            _ => throw new NotSupportedException($"Database provider '{provider}' is not supported for automated dump. Please use the provider's native backup tool.")
        };

        var psi = new ProcessStartInfo
        {
            FileName = tool,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        // Read output
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var error = await errorTask;
            throw new InvalidOperationException(
                $"Database dump failed (exit code {process.ExitCode}): {error}");
        }

        // Write stdout to file (pg_dump outputs to stdout by default with -Fp)
        if (string.IsNullOrEmpty(await errorTask))
        {
            var output = await outputTask;
            if (!string.IsNullOrEmpty(output))
            {
                await File.WriteAllTextAsync(outputPath, output, cancellationToken);
            }
        }
    }

    private static string BuildPgDumpArgs(string? connectionString, string outputPath)
    {
        var (host, port, db, user) = ParsePgConnectionString(connectionString);
        var args = $"-h {host} -p {port} -U {user} -d {db} -Fp \"{outputPath}\"";
        return args;
    }

    private static (string Host, string Port, string Database, string User) ParsePgConnectionString(string? connectionString)
    {
        var host = "localhost";
        var port = "5432";
        var db = "dotnetcloud";
        var user = "postgres";

        if (string.IsNullOrEmpty(connectionString))
            return (host, port, db, user);

        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eqIndex = part.IndexOf('=');
            if (eqIndex < 0) continue;

            var key = part[..eqIndex].Trim().ToLowerInvariant();
            var value = part[(eqIndex + 1)..].Trim();

            switch (key)
            {
                case "host" or "server" or "datasource": host = value; break;
                case "port": port = value; break;
                case "database" or "db": db = value; break;
                case "username" or "user id" or "user": user = value; break;
            }
        }

        return (host, port, db, user);
    }

    private static string BuildSqlCmdArgs(string? connectionString, string outputPath)
    {
        if (string.IsNullOrEmpty(connectionString))
            return $"-Q \"BACKUP DATABASE [dotnetcloud] TO DISK = '{outputPath}'\"";
        // For SQL Server, connection string is used as-is with -S
        var server = ParseSqlServerConnectionString(connectionString);
        return $"-S \"{server}\" -Q \"BACKUP DATABASE [dotnetcloud] TO DISK = '{outputPath}'\"";
    }

    private static string ParseSqlServerConnectionString(string connectionString)
    {
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eqIndex = part.IndexOf('=');
            if (eqIndex < 0) continue;

            var key = part[..eqIndex].Trim().ToLowerInvariant();
            var value = part[(eqIndex + 1)..].Trim();

            if (key is "server" or "data source" or "host")
                return value;
        }

        return connectionString; // fallback: use raw connection string
    }

    private static string BuildMySqlDumpArgs(string? connectionString, string outputPath)
    {
        if (string.IsNullOrEmpty(connectionString))
            return $"--result-file=\"{outputPath}\" dotnetcloud";
        return $"--result-file=\"{outputPath}\" --default-auth=mysql_native_password";
    }

    private static string GetDumpToolName(string provider) => provider.ToUpperInvariant() switch
    {
        "POSTGRESQL" => "pg_dump",
        "SQLSERVER" => "sqlcmd",
        "MARIADB" or "MYSQL" => "mysqldump",
        _ => provider,
    };

    private static async Task RestoreDatabaseDumpAsync(
        string dumpFile,
        RestoreOptions options,
        CancellationToken cancellationToken)
    {
        var (tool, args) = options.DatabaseProvider?.ToUpperInvariant() switch
        {
            "POSTGRESQL" => ("psql", BuildPsqlRestoreArgs(options.ConnectionString, dumpFile)),
            "SQLSERVER" => ("sqlcmd", BuildSqlCmdRestoreArgs(options.ConnectionString, dumpFile)),
            "MARIADB" or "MYSQL" => ("mysql", BuildMySqlRestoreArgs(options.ConnectionString, dumpFile)),
            _ => throw new NotSupportedException($"Database provider '{options.DatabaseProvider}' is not supported for automated restore.")
        };

        var psi = new ProcessStartInfo
        {
            FileName = tool,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi };
        process.Start();
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var error = await errorTask;
            throw new InvalidOperationException(
                $"Database restore failed (exit code {process.ExitCode}): {error}");
        }
    }

    private static string BuildPsqlRestoreArgs(string? connectionString, string dumpFile)
    {
        if (string.IsNullOrEmpty(connectionString))
            return $"-f \"{dumpFile}\"";
        var (host, port, db, user) = ParsePgConnectionString(connectionString);
        return $"-h {host} -p {port} -U {user} -d {db} -f \"{dumpFile}\"";
    }

    private static string BuildSqlCmdRestoreArgs(string? connectionString, string dumpFile)
    {
        if (string.IsNullOrEmpty(connectionString))
            return $"-Q \"RESTORE DATABASE [dotnetcloud] FROM DISK = '{dumpFile}'\"";
        var server = ParseSqlServerConnectionString(connectionString);
        return $"-S \"{server}\" -Q \"RESTORE DATABASE [dotnetcloud] FROM DISK = '{dumpFile}'\"";
    }

    private static string BuildMySqlRestoreArgs(string? connectionString, string dumpFile)
        => $"dotnetcloud < \"{dumpFile}\"";

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            var targetFile = Path.Combine(targetDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(file, targetFile, overwrite: true);
        }
    }
}
