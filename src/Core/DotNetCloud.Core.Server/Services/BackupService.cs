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
    private readonly IConfiguration _configuration;
    private static readonly SemaphoreSlim _backupLock = new(1, 1);

    // Simple in-memory tracking of last backup status
    private static BackupStatusInfo? s_lastStatus;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackupService"/> class.
    /// </summary>
    public BackupService(ILogger<BackupService> logger, IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
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

            // Restore configuration to the live configuration directory.
            var configDir = Path.Combine(restoreDir, "config");
            if (Directory.Exists(configDir))
            {
                var destinationRoot = ResolveConfigDirectory();
                foreach (var file in Directory.GetFiles(configDir, "*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(configDir, file);
                    if (relativePath.Equals("database-info.txt", StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        var targetPath = Path.Combine(destinationRoot, relativePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                        File.Copy(file, targetPath, overwrite: true);
                        _logger.LogInformation("Restored config file: {File}", relativePath);
                    }
                    catch (Exception ex)
                    {
                        // The hardened service unit can read /etc/dotnetcloud but cannot always write
                        // root-owned files. Full system restore is the CLI's job.
                        _logger.LogWarning(ex, "Could not restore config file: {File}", relativePath);
                    }
                }
            }

            // Restore data files
            var dataDir = Path.Combine(restoreDir, "data");
            if (Directory.Exists(dataDir))
            {
                if (string.IsNullOrWhiteSpace(options.DataDirectory))
                {
                    return new BackupResult
                    {
                        Success = false,
                        ErrorMessage = "RestoreOptions.DataDirectory is required to restore the data directory.",
                    };
                }

                CopyDirectory(dataDir, options.DataDirectory);
                _logger.LogInformation("Restored data directory to {DataDirectory}", options.DataDirectory);
            }

            // Restore database dump if present and requested
            var dumpFile = Path.Combine(restoreDir, "database.sql");
            if (options.RestoreDatabase && File.Exists(dumpFile))
            {
                options.DatabaseProvider ??= ResolveConfiguredDatabaseProvider();
                options.ConnectionString ??= ResolveConfiguredConnectionString();
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
                try
                { Directory.Delete(restoreDir, recursive: true); }
                catch { /* best effort */ }
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
            LastBackupFileCount = s_lastStatus?.LastBackupFileCount,
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
        var backupDir = string.IsNullOrWhiteSpace(options.BackupDirectory)
            ? ResolveDefaultBackupDirectory()
            : options.BackupDirectory;
        Directory.CreateDirectory(backupDir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var backupFileName = $"dotnetcloud-backup-{timestamp}.zip";

        // Canonicalize first so "."/".." segments and alternative separators cannot slip past
        // the containment check below.
        var backupPath = Path.GetFullPath(outputPath ?? Path.Combine(backupDir, backupFileName));

        // Security: the archive must stay inside the configured backup directory. The prefix is
        // compared with IndexOf rather than StartsWith: a bare StartsWith check does not stop
        // ambiguous traversal and is not treated as a reliable barrier by static analysis.
        var fullBackupDir = Path.GetFullPath(backupDir);
        var trimmedBackupDir = fullBackupDir.TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var backupDirPrefix = trimmedBackupDir + Path.DirectorySeparatorChar;
        if (backupPath.IndexOf(backupDirPrefix, StringComparison.Ordinal) != 0)
        {
            // The configured directory itself is also accepted (no separator to match against).
            if (!string.Equals(
                    backupPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    trimmedBackupDir,
                    StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException(
                    "The backup path must be within the configured backup directory.");
            }
        }

        _logger.LogInformation("Starting backup to: {BackupPath}", backupPath);

        var dbDumpPath = Path.Combine(backupDir, $"dotnetcloud-db-{timestamp}.sql");
        var createdDbDump = false;

        // The admin/scheduled path does not supply provider/connection details, so resolve them from
        // configuration. Without this the dump silently never runs.
        var databaseProvider = options.DatabaseProvider ?? ResolveConfiguredDatabaseProvider();
        var connectionString = options.ConnectionString ?? ResolveConfiguredConnectionString();

        // Neither the admin API nor the scheduled service passes a data directory. Resolve it here so
        // an "everything needed for restore" backup actually contains the data.
        var dataDirectory = string.IsNullOrWhiteSpace(options.DataDirectory)
            ? ResolveConfiguredDataDirectory()
            : options.DataDirectory;
        var includeData = Directory.Exists(dataDirectory)
            && (options.IncludeFileStorage || options.IncludeModuleData);

        var compression = BackupCompression.Parse(options.Compression);
        options.Progress?.Invoke(new BackupProgress("starting", 0, 0, 0, 0));

        try
        {
            // Step 1: Database dump. A backup that cannot restore the database is not a backup, so a
            // failed dump aborts the run instead of producing a misleading file-only archive.
            if (options.IncludeDatabaseDump)
            {
                options.Progress?.Invoke(new BackupProgress("database", 0, 0, 0, 0));
                if (string.IsNullOrWhiteSpace(databaseProvider))
                {
                    return new BackupResult
                    {
                        Success = false,
                        ErrorMessage = "Database provider is not configured; refusing to create a database-less backup.",
                    };
                }

                try
                {
                    await CreateDatabaseDumpAsync(databaseProvider, connectionString, dbDumpPath, cancellationToken);
                    createdDbDump = true;
                    _logger.LogInformation("Database dump created: {DumpPath}", dbDumpPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Database dump failed. Ensure {Tool} is installed and accessible.",
                        GetDumpToolName(databaseProvider));
                    return new BackupResult
                    {
                        Success = false,
                        ErrorMessage = $"Database dump failed: {ex.Message}",
                    };
                }
            }

            // Step 2: Create zip archive
            var fileCount = 0;
            await using var zipStream = new FileStream(backupPath, FileMode.Create);
            // leaveOpen: the archive is disposed explicitly before the checksum is computed; closing
            // the stream there would make the subsequent flush fail.
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true);

            // Add database dump if created
            if (createdDbDump && File.Exists(dbDumpPath))
            {
                archive.CreateEntryFromFile(dbDumpPath, "database.sql", compression);
                fileCount++;
            }

            // Add database info note
            var dbInfoEntry = archive.CreateEntry("config/database-info.txt");
            await using (var writer = new StreamWriter(dbInfoEntry.Open()))
            {
                await writer.WriteLineAsync($"Provider: {databaseProvider ?? "unknown"}");
                await writer.WriteLineAsync($"Backup Time: {DateTime.UtcNow:O}");
                await writer.WriteLineAsync($"Database Dump Included: {createdDbDump}");
                await writer.WriteLineAsync();
                await writer.WriteLineAsync("Restore: Use 'dotnetcloud restore' or your database provider's restore tool.");
            }
            fileCount++;

            // Add data directory. The backup directory and this archive are excluded: without that,
            // every backup nests all of its predecessors and a partial copy of itself.
            var dataFileCount = 0;
            var emptyDirectoryCount = 0;
            if (includeData)
            {
                var exclusions = new[]
                {
                    Path.GetFullPath(backupDir),
                    Path.GetFullPath(backupPath),
                };

                var allDataFiles = Directory.GetFiles(dataDirectory, "*", SearchOption.AllDirectories);

                // Materialize the list once so totals are known and progress can be reported.
                var filesToArchive = new List<(string Path, long Length)>();
                long dataBytesTotal = 0;
                foreach (var file in allDataFiles)
                {
                    if (IsExcludedFromArchive(file, exclusions))
                        continue;

                    long length = 0;
                    try
                    { length = new FileInfo(file).Length; }
                    catch { /* unreadable; skip the size */ }

                    filesToArchive.Add((file, length));
                    dataBytesTotal += length;
                }

                options.Progress?.Invoke(new BackupProgress("data", 0, filesToArchive.Count, 0, dataBytesTotal));

                var dataBytesProcessed = 0L;
                foreach (var (file, length) in filesToArchive)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var entryName = "data/" + Path.GetRelativePath(dataDirectory, file).Replace('\\', '/');
                    archive.CreateEntryFromFile(file, entryName, compression);
                    fileCount++;
                    dataFileCount++;
                    dataBytesProcessed += length;

                    if (dataFileCount % 25 == 0)
                    {
                        options.Progress?.Invoke(new BackupProgress(
                            "data", dataFileCount, filesToArchive.Count, dataBytesProcessed, dataBytesTotal));
                    }
                }

                options.Progress?.Invoke(new BackupProgress(
                    "data", dataFileCount, filesToArchive.Count, dataBytesProcessed, dataBytesTotal));

                // Empty directories are meaningful template content (the default-user-content folders
                // are empty until an operator adds files) and ZIP stores none implicitly.
                var emptyDirectories = Directory
                    .GetDirectories(dataDirectory, "*", SearchOption.AllDirectories)
                    .Where(dir => !IsExcludedFromArchive(dir, exclusions))
                    .Where(dir => !Directory.EnumerateFileSystemEntries(dir).Any())
                    .ToArray();

                foreach (var directory in emptyDirectories)
                {
                    var entryName = "data/"
                        + Path.GetRelativePath(dataDirectory, directory).Replace('\\', '/')
                        + "/";
                    archive.CreateEntry(entryName);
                    fileCount++;
                    emptyDirectoryCount++;
                }

                _logger.LogInformation(
                    "Data directory backed up ({Files} files, {EmptyDirectories} empty directories).",
                    dataFileCount, emptyDirectoryCount);
            }
            else
            {
                _logger.LogWarning(
                    "Data directory backup skipped: {DataDirectory} is missing or both " +
                    "IncludeFileStorage and IncludeModuleData are disabled.",
                    dataDirectory);
            }

            // System configuration outside the data directory: config.json (DB connection string),
            // env (Collabora/WOPI), TLS certificates + root CA, the systemd unit and appsettings.
            // A restore cannot start the server without the certificate config.json points at.
            var (systemFiles, systemWarnings) = CollectSystemConfigurationFiles();
            var systemEntryNames = new List<string>();
            options.Progress?.Invoke(new BackupProgress("system", dataFileCount, dataFileCount, 0, 0));
            foreach (var (source, entryName) in systemFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    archive.CreateEntryFromFile(source, entryName, compression);
                    fileCount++;
                    systemEntryNames.Add(entryName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not add system file to backup: {Source}", source);
                }
            }

            _logger.LogInformation("System configuration backed up ({Count} files).", systemEntryNames.Count);

            // Machine-readable metadata (mirrors the CLI archive layout so `dotnetcloud restore` can
            // read either archive).
            var infoEntry = archive.CreateEntry("backup-info.json");
            await using (var writer = new StreamWriter(infoEntry.Open()))
            {
                await writer.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
                {
                    formatVersion = 2,
                    createdUtc = DateTime.UtcNow.ToString("O"),
                    tool = "dotnetcloud server backup",
                    databaseProvider,
                    databaseDumpIncluded = createdDbDump,
                    storageManifestIncluded = false,
                    dataDirectory,
                    configDirectory = ResolveConfigDirectory(),
                    serviceUser = ResolveServiceUser(),
                    systemEntries = systemEntryNames,
                    dataFileCount,
                    emptyDirectoryCount,
                    warnings = systemWarnings
                        .Append("The storage manifest is generated by the CLI backup only; this archive includes the " +
                                "database dump, which contains every storage path needed for a restore.")
                        .ToArray(),
                }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            fileCount++;

            // Finish the archive (writes the central directory) before hashing it.
            options.Progress?.Invoke(new BackupProgress("finalizing", dataFileCount, dataFileCount, 0, 0));
            archive.Dispose();
            await zipStream.FlushAsync(cancellationToken);

            var archiveHash = await ComputeSha256Async(backupPath, cancellationToken);
            var shaPath = backupPath + ".sha256";
            await File.WriteAllTextAsync(
                shaPath, $"{archiveHash}  {Path.GetFileName(backupPath)}{Environment.NewLine}", cancellationToken);

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
                LastBackupFileCount = fileCount,
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
                try
                { File.Delete(backupPath); }
                catch { /* best effort */ }

            return new BackupResult
            {
                Success = false,
                ErrorMessage = "Backup was cancelled.",
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup failed.");

            // Never leave a partial archive behind: it looks like a usable backup.
            foreach (var partial in new[] { backupPath, backupPath + ".sha256" })
            {
                if (File.Exists(partial))
                {
                    try
                    { File.Delete(partial); }
                    catch { /* best effort */ }
                }
            }

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
                try
                { File.Delete(dbDumpPath); }
                catch { /* best effort */ }
            }
        }
    }

    /// <summary>Resolves the configured database provider from server configuration.</summary>
    private string? ResolveConfiguredDatabaseProvider()
        => _configuration["Database:Provider"]
           ?? _configuration["databaseProvider"]
           ?? _configuration["Provider"];

    /// <summary>
    /// Resolves the configured connection string. The CLI config uses the flat
    /// <c>connectionString</c> key; ASP.NET convention uses <c>ConnectionStrings:DefaultConnection</c>.
    /// </summary>
    private string? ResolveConfiguredConnectionString()
        => _configuration["connectionString"]
           ?? _configuration.GetConnectionString("DefaultConnection");

    /// <summary>Resolves the configuration directory (<c>/etc/dotnetcloud</c> on a system install).</summary>
    private static string ResolveConfigDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("DOTNETCLOUD_CONFIG_DIR");
        return string.IsNullOrWhiteSpace(configured) ? "/etc/dotnetcloud" : configured;
    }

    /// <summary>
    /// Default backup directory. Under systemd the service user's home is not writable
    /// (<c>ProtectSystem=strict</c>), so default to the data directory's <c>backups</c> folder, which is
    /// listed in <c>ReadWritePaths</c> and matches the CLI default on a system install.
    /// </summary>
    private static string ResolveDefaultBackupDirectory()
    {
        var dataDirectory = Environment.GetEnvironmentVariable("DOTNETCLOUD_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(dataDirectory))
            return Path.Combine(dataDirectory, "backups");

        if (OperatingSystem.IsWindows())
        {
            // ProgramData is writable by the service account and survives a reinstall of the binary.
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "DotNetCloud", "backups");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "dotnetcloud", "backups");
    }

    /// <summary>Resolves the data directory (<c>DOTNETCLOUD_DATA_DIR</c>).</summary>
    private static string ResolveConfiguredDataDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("DOTNETCLOUD_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        return OperatingSystem.IsWindows()
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "DotNetCloud", "data")
            : "/var/lib/dotnetcloud";
    }

    /// <summary>Account that owns the restored data directory.</summary>
    private static string ResolveServiceUser()
        => Environment.GetEnvironmentVariable("DOTNETCLOUD_SERVICE_USER") ?? "dotnetcloud";

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="path"/> is one of the excluded paths or
    /// lives beneath one of them. A sibling that merely shares a name prefix is not excluded.
    /// </summary>
    private static bool IsExcludedFromArchive(string path, IReadOnlyList<string> exclusions)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var candidate = Path.GetFullPath(path);
        foreach (var exclusion in exclusions)
        {
            if (candidate.Equals(exclusion, comparison))
                return true;

            if (candidate.StartsWith(exclusion + Path.DirectorySeparatorChar, comparison))
                return true;
        }

        return false;
    }

    /// <summary>Computes the SHA-256 checksum of a file.</summary>
    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = await System.Security.Cryptography.SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Enumerates the system files (configuration, certificates, systemd unit, CLI wrapper, Collabora
    /// config, appsettings) that must travel with the data directory for a cold restore. Files the
    /// service account cannot read are reported as warnings instead of failing the backup.
    /// </summary>
    private static (IReadOnlyList<(string Source, string EntryName)> Entries, IReadOnlyList<string> Warnings)
        CollectSystemConfigurationFiles()
    {
        var configDirectory = ResolveConfigDirectory();
        var installDirectory = Environment.GetEnvironmentVariable("DOTNETCLOUD_INSTALL_DIR")
            ?? (OperatingSystem.IsWindows()
                ? AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)
                : "/opt/dotnetcloud");
        var coolwsdDirectory = "/etc/coolwsd";

        var candidates = new List<(string Source, string EntryName)>
        {
            (Path.Combine(configDirectory, "config.json"), "config/config.json"),
            (Path.Combine(configDirectory, "env"), "config/env"),
            (Path.Combine(installDirectory, "appsettings.json"), "opt/appsettings.json"),
            (Path.Combine(installDirectory, "appsettings.Development.json"), "opt/appsettings.Development.json"),
            (Path.Combine(installDirectory, "VERSION"), "opt/VERSION"),
        };

        // systemd unit, CLI wrapper and Collabora config only exist on Linux. The unit matters most: it
        // carries operator-added environment entries that exist nowhere else.
        if (OperatingSystem.IsLinux())
        {
            candidates.Add((Path.Combine("/etc/systemd/system", "dotnetcloud.service"), "system/systemd/dotnetcloud.service"));
            candidates.Add((Path.Combine("/usr/local/bin", "dotnetcloud"), "system/bin/dotnetcloud"));
            candidates.Add((Path.Combine(coolwsdDirectory, "coolwsd.xml"), "system/coolwsd/coolwsd.xml"));
            candidates.Add((Path.Combine(coolwsdDirectory, "cert.pem"), "system/coolwsd/cert.pem"));
            candidates.Add((Path.Combine(coolwsdDirectory, "ca-chain.cert.pem"), "system/coolwsd/ca-chain.cert.pem"));
        }

        var certsDirectory = Path.Combine(configDirectory, "certs");
        if (Directory.Exists(certsDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(certsDirectory, "*", SearchOption.TopDirectoryOnly))
                candidates.Add((file, "config/certs/" + Path.GetFileName(file)));
        }

        var entries = new List<(string Source, string EntryName)>();
        var warnings = new List<string>();

        foreach (var candidate in candidates)
        {
            if (!File.Exists(candidate.Source))
                continue;

            if (!CanRead(candidate.Source))
            {
                // Collabora's config is root:cool 640 on a system install; the hardened service
                // account cannot read it. The CLI backup (run as root) captures it instead.
                warnings.Add($"Not readable by the service account: {candidate.Source}");
                continue;
            }

            entries.Add(candidate);
        }

        return (entries, warnings);
    }

    /// <summary>Returns <see langword="true"/> when the file can be opened for reading.</summary>
    private static bool CanRead(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Reads a single value from a semicolon-delimited connection string.</summary>
    private static string? TryGetConnectionStringValue(string? connectionString, params string[] keys)
    {
        if (string.IsNullOrWhiteSpace(connectionString) || keys.Length == 0)
            return null;

        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eqIndex = part.IndexOf('=');
            if (eqIndex <= 0)
                continue;

            var key = part[..eqIndex].Trim();
            var value = part[(eqIndex + 1)..].Trim();

            if (keys.Any(k => k.Equals(key, StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrEmpty(value))
                return value;
        }

        return null;
    }

    private static async Task CreateDatabaseDumpAsync(
        string provider,
        string? connectionString,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var (tool, argList) = provider.ToUpperInvariant() switch
        {
            "POSTGRESQL" => ("pg_dump", BuildPgDumpArgs(connectionString, outputPath)),
            "SQLSERVER" => ("sqlcmd", BuildSqlCmdArgs(connectionString, outputPath)),
            _ => throw new NotSupportedException($"Database provider '{provider}' is not supported for automated dump. Please use the provider's native backup tool.")
        };

        var psi = new ProcessStartInfo
        {
            FileName = tool,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var a in argList)
            psi.ArgumentList.Add(a);

        // pg_dump has no password argument; without PGPASSWORD it prompts (impossible in a service)
        // and the dump fails on any password-authenticated PostgreSQL server.
        if (provider.Equals("POSTGRESQL", StringComparison.OrdinalIgnoreCase))
        {
            var password = TryGetConnectionStringValue(connectionString, "password", "pwd");
            if (!string.IsNullOrEmpty(password))
                psi.Environment["PGPASSWORD"] = password;
        }

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

    private static List<string> BuildPgDumpArgs(string? connectionString, string outputPath)
    {
        var (host, port, db, user) = ParsePgConnectionString(connectionString);
        // -Fp selects the plain format; -f names the output file. Passing the path positionally after
        // -Fp makes pg_dump reject it as an extra command-line argument.
        return new List<string> { "-h", host, "-p", port, "-U", user, "-d", db, "-Fp", "-f", outputPath };
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
            if (eqIndex < 0)
                continue;

            var key = part[..eqIndex].Trim().ToLowerInvariant();
            var value = part[(eqIndex + 1)..].Trim();

            switch (key)
            {
                case "host" or "server" or "datasource":
                    host = value;
                    break;
                case "port":
                    port = value;
                    break;
                case "database" or "db":
                    db = value;
                    break;
                case "username" or "user id" or "user":
                    user = value;
                    break;
            }
        }

        return (host, port, db, user);
    }

    private static List<string> BuildSqlCmdArgs(string? connectionString, string outputPath)
    {
        if (string.IsNullOrEmpty(connectionString))
            return new List<string> { "-Q", $"BACKUP DATABASE [dotnetcloud] TO DISK = '{outputPath}'" };
        // For SQL Server, connection string is used as-is with -S
        var server = ParseSqlServerConnectionString(connectionString);
        return new List<string> { "-S", server, "-Q", $"BACKUP DATABASE [dotnetcloud] TO DISK = '{outputPath}'" };
    }

    private static string ParseSqlServerConnectionString(string connectionString)
    {
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eqIndex = part.IndexOf('=');
            if (eqIndex < 0)
                continue;

            var key = part[..eqIndex].Trim().ToLowerInvariant();
            var value = part[(eqIndex + 1)..].Trim();

            if (key is "server" or "data source" or "host")
                return value;
        }

        return connectionString; // fallback: use raw connection string
    }

    private static string GetDumpToolName(string provider) => provider.ToUpperInvariant() switch
    {
        "POSTGRESQL" => "pg_dump",
        "SQLSERVER" => "sqlcmd",
        _ => provider,
    };

    private static async Task RestoreDatabaseDumpAsync(
        string dumpFile,
        RestoreOptions options,
        CancellationToken cancellationToken)
    {
        string tool;
        List<string> argList;
        bool useStdinRedirect = false;

        switch (options.DatabaseProvider?.ToUpperInvariant())
        {
            case "POSTGRESQL":
                tool = "psql";
                argList = BuildPsqlRestoreArgs(options.ConnectionString, dumpFile);
                break;
            case "SQLSERVER":
                tool = "sqlcmd";
                argList = BuildSqlCmdRestoreArgs(options.ConnectionString, dumpFile);
                break;
            default:
                throw new NotSupportedException($"Database provider '{options.DatabaseProvider}' is not supported for automated restore.");
        }

        var psi = new ProcessStartInfo
        {
            FileName = tool,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = useStdinRedirect,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var a in argList)
            psi.ArgumentList.Add(a);

        // psql authenticates from PGPASSWORD; without it the restore prompts and fails in a service.
        if (options.DatabaseProvider?.Equals("POSTGRESQL", StringComparison.OrdinalIgnoreCase) == true)
        {
            var password = TryGetConnectionStringValue(options.ConnectionString, "password", "pwd");
            if (!string.IsNullOrEmpty(password))
                psi.Environment["PGPASSWORD"] = password;
        }

        using var process = new Process { StartInfo = psi };
        process.Start();

        if (useStdinRedirect)
        {
            // Pipe the dump file into the database's stdin instead of using shell redirect (<)
            await using var fileStream = File.OpenRead(dumpFile);
            await fileStream.CopyToAsync(process.StandardInput.BaseStream, cancellationToken);
            process.StandardInput.Close();
        }

        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var error = await errorTask;
            throw new InvalidOperationException(
                $"Database restore failed (exit code {process.ExitCode}): {error}");
        }
    }

    private static List<string> BuildPsqlRestoreArgs(string? connectionString, string dumpFile)
    {
        if (string.IsNullOrEmpty(connectionString))
            return new List<string> { "-f", dumpFile };
        var (host, port, db, user) = ParsePgConnectionString(connectionString);
        return new List<string> { "-h", host, "-p", port, "-U", user, "-d", db, "-f", dumpFile };
    }

    private static List<string> BuildSqlCmdRestoreArgs(string? connectionString, string dumpFile)
    {
        if (string.IsNullOrEmpty(connectionString))
            return new List<string> { "-Q", $"RESTORE DATABASE [dotnetcloud] FROM DISK = '{dumpFile}'" };
        var server = ParseSqlServerConnectionString(connectionString);
        return new List<string> { "-S", server, "-Q", $"RESTORE DATABASE [dotnetcloud] FROM DISK = '{dumpFile}'" };
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, directory);
            Directory.CreateDirectory(Path.Combine(targetDir, relative));
        }

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            var targetFile = Path.Combine(targetDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(file, targetFile, overwrite: true);
        }
    }
}
