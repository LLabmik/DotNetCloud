using System.CommandLine;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using DotNetCloud.CLI.Infrastructure;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Files.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.CLI.Commands;

/// <summary>
/// Backup and restore commands: create backup, restore from backup, schedule backups.
/// Now supports optional database dumps via provider-native tools (pg_dump, sqlcmd).
/// </summary>
internal static class BackupCommands
{
    /// <summary>
    /// Creates the <c>backup</c> parent command with subcommands.
    /// When invoked without a subcommand, creates a backup with default settings.
    /// </summary>
    public static Command Create()
    {
        var outputOption = new Option<string?>("--output")
        {
            Description = "Output path for the backup file"
        };
        outputOption.Aliases.Add("-o");

        var dbDumpOption = new Option<bool>("--db-dump")
        {
            Description = "Include a database dump in the backup (default: on)",
            DefaultValueFactory = _ => true
        };

        var noDbDumpOption = new Option<bool>("--no-db-dump")
        {
            Description = "Skip the database dump (produces a file-only backup)",
            DefaultValueFactory = _ => false
        };

        var serverOption = new Option<string?>("--server")
        {
            Description = "Trigger backup via the DotNetCloud server API at this URL (e.g., http://localhost:5000)"
        };
        serverOption.Aliases.Add("-s");

        var noManifestOption = new Option<bool>("--no-manifest")
        {
            Description = "Skip the storage manifest that is normally generated and embedded in the backup",
            DefaultValueFactory = _ => false
        };

        var noSystemOption = new Option<bool>("--no-system")
        {
            Description = "Skip system configuration (config, certificates, env, systemd unit, appsettings)",
            DefaultValueFactory = _ => false
        };

        var compressionOption = new Option<string>("--compression")
        {
            Description = "Compression: optimal (default), fastest, or store (no compression — best for media)",
            DefaultValueFactory = _ => BackupCompression.Default
        };

        var command = new Command("backup", "Create a complete backup of DotNetCloud (data, database and system configuration)");
        command.Options.Add(outputOption);
        command.Options.Add(dbDumpOption);
        command.Options.Add(noDbDumpOption);
        command.Options.Add(serverOption);
        command.Options.Add(noManifestOption);
        command.Options.Add(noSystemOption);
        command.Options.Add(compressionOption);
        command.Subcommands.Add(CreateRestoreCommand());
        command.Subcommands.Add(CreateSchedule());
        command.Subcommands.Add(CreateManifest());

        command.SetAction(parseResult =>
        {
            var output = parseResult.GetValue(outputOption);
            var dbDump = parseResult.GetValue(dbDumpOption) && !parseResult.GetValue(noDbDumpOption);
            var server = parseResult.GetValue(serverOption);
            var noManifest = parseResult.GetValue(noManifestOption);
            var noSystem = parseResult.GetValue(noSystemOption);
            var compression = parseResult.GetValue(compressionOption) ?? BackupCompression.Default;
            return CreateBackupAsync(output, dbDump, server, includeManifest: !noManifest, includeSystem: !noSystem, compression);
        });

        return command;
    }

    /// <summary>
    /// Creates the <c>restore</c> command. It is registered both as a top-level command
    /// (<c>dotnetcloud restore &lt;file&gt;</c>) and as a <c>backup</c> subcommand for compatibility.
    /// </summary>
    internal static Command CreateRestoreCommand()
    {
        var fileArg = new Argument<string>("file")
        {
            Description = "Path to the backup file to restore"
        };

        var yesOption = new Option<bool>("--yes")
        {
            Description = "Do not prompt for confirmation",
            DefaultValueFactory = _ => false
        };
        yesOption.Aliases.Add("-y");

        var skipDatabaseOption = new Option<bool>("--skip-database")
        {
            Description = "Do not restore the database (data files and configuration are still restored)",
            DefaultValueFactory = _ => false
        };

        var replaceDatabaseOption = new Option<bool>("--replace-database")
        {
            Description = "Drop the existing database before restoring (required when it already contains tables)",
            DefaultValueFactory = _ => false
        };

        var skipSystemOption = new Option<bool>("--skip-system")
        {
            Description = "Do not restore configuration files, certificates, env or the systemd unit",
            DefaultValueFactory = _ => false
        };

        var skipServiceOption = new Option<bool>("--skip-service")
        {
            Description = "Do not stop or start the dotnetcloud service",
            DefaultValueFactory = _ => false
        };

        var dataDirOption = new Option<string?>("--data-dir")
        {
            Description = "Destination data directory (default: the one recorded in the restored configuration)"
        };

        var noHealthWaitOption = new Option<bool>("--no-health-wait")
        {
            Description = "Do not wait for the health endpoint after starting the service",
            DefaultValueFactory = _ => false
        };

        var command = new Command("restore", "Restore a DotNetCloud backup (database, data and system configuration)")
        {
            fileArg
        };
        command.Options.Add(yesOption);
        command.Options.Add(skipDatabaseOption);
        command.Options.Add(replaceDatabaseOption);
        command.Options.Add(skipSystemOption);
        command.Options.Add(skipServiceOption);
        command.Options.Add(dataDirOption);
        command.Options.Add(noHealthWaitOption);

        command.SetAction(parseResult => RestoreBackupAsync(
            parseResult.GetValue(fileArg)!,
            new RestoreCommandOptions(
                AssumeYes: parseResult.GetValue(yesOption),
                RestoreDatabase: !parseResult.GetValue(skipDatabaseOption),
                ReplaceDatabase: parseResult.GetValue(replaceDatabaseOption),
                RestoreSystem: !parseResult.GetValue(skipSystemOption),
                ManageService: !parseResult.GetValue(skipServiceOption),
                DataDirectory: parseResult.GetValue(dataDirOption),
                WaitForHealth: !parseResult.GetValue(noHealthWaitOption))));
        return command;
    }

    /// <summary>Options for <c>dotnetcloud restore</c>.</summary>
    private sealed record RestoreCommandOptions(
        bool AssumeYes,
        bool RestoreDatabase,
        bool ReplaceDatabase,
        bool RestoreSystem,
        bool ManageService,
        string? DataDirectory,
        bool WaitForHealth);

    private static Command CreateSchedule()
    {
        var intervalOption = new Option<string>("--interval")
        {
            Description = "Backup interval (daily, weekly, monthly)",
            DefaultValueFactory = _ => "daily"
        };

        var command = new Command("schedule", "Configure automatic backup schedule");
        command.Options.Add(intervalOption);

        command.SetAction(parseResult =>
        {
            var interval = parseResult.GetValue(intervalOption)!;
            return ScheduleBackupAsync(interval);
        });
        return command;
    }

    private static Command CreateManifest()
    {
        var outputOption = new Option<string?>("--output")
        {
            Description = "Manifest file to write (default: <backup directory>/dotnetcloud-manifest-<timestamp>.tsv; '-' writes to stdout)"
        };
        outputOption.Aliases.Add("-o");

        var noChunksOption = new Option<bool>("--no-chunks")
        {
            Description = "List file versions only, omitting chunk rows",
            DefaultValueFactory = _ => false
        };

        var storageRootOption = new Option<string?>("--storage-root")
        {
            Description = "Storage root to describe (default: Files:Storage:RootPath from config, else <data directory>/storage)"
        };

        var command = new Command("manifest",
            "Write a tab-separated index of every stored blob — keep it with your backup");
        command.Options.Add(outputOption);
        command.Options.Add(noChunksOption);
        command.Options.Add(storageRootOption);
        command.SetAction(parseResult => WriteManifestAsync(
            parseResult.GetValue(outputOption),
            parseResult.GetValue(noChunksOption),
            parseResult.GetValue(storageRootOption)));
        return command;
    }

    /// <summary>
    /// Writes the storage manifest: a mapping from every content-addressed blob back to the file it
    /// belongs to, plus the ordered chunk list needed to rebuild chunked files.
    /// </summary>
    private static async Task<int> WriteManifestAsync(string? outputPath, bool noChunks, string? storageRootOverride)
    {
        await using var provider = ServiceProviderFactory.CreateFromConfig();
        if (provider is null)
            return 1;

        var config = CliConfiguration.Load();
        var storageRoot = storageRootOverride ?? StorageManifest.ResolveStorageRoot();
        var includeChunks = !noChunks;
        var toStdout = string.Equals(outputPath, "-", StringComparison.Ordinal);

        ConsoleOutput.WriteHeader("DotNetCloud Storage Manifest");
        ConsoleOutput.WriteInfo($"Storage root: {storageRoot}");

        if (!toStdout && !Directory.Exists(storageRoot))
        {
            ConsoleOutput.WriteWarning($"Storage root does not exist: {storageRoot}");
        }

        string? manifestPath = null;
        if (!toStdout)
        {
            manifestPath = outputPath
                ?? Path.Combine(config.BackupDirectory, $"dotnetcloud-manifest-{DateTime.UtcNow:yyyyMMdd-HHmmss}.tsv");
            var manifestDir = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
            if (!string.IsNullOrEmpty(manifestDir))
                Directory.CreateDirectory(manifestDir);
        }

        try
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();

            StorageManifest.Result result;
            if (toStdout)
            {
                result = await StorageManifest.WriteAsync(db, storageRoot, includeChunks, Console.Out);
            }
            else
            {
                result = await WriteManifestToFileAsync(db, storageRoot, includeChunks, manifestPath!);
            }

            ConsoleOutput.WriteSuccess($"File versions: {result.FileVersions}");
            if (includeChunks)
                ConsoleOutput.WriteSuccess($"Chunk rows: {result.ChunkRows}");
            ConsoleOutput.WriteSuccess($"Content bytes: {FormatSize(result.TotalBytes)}");

            if (result.MissingBlobs > 0)
                ConsoleOutput.WriteWarning($"Whole-file blobs MISSING from storage: {result.MissingBlobs}");
            if (result.TruncatedBlobs > 0)
                ConsoleOutput.WriteWarning($"Whole-file blobs with an unexpected size: {result.TruncatedBlobs}");

            if (manifestPath is not null)
            {
                ConsoleOutput.WriteSuccess($"Manifest written: {manifestPath}");
                ConsoleOutput.WriteInfo("Keep this file with any backup of the storage directory.");
            }

            return 0;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Manifest failed: {ex.Message}");
            return 1;
        }
    }

    /// <summary>Writes a manifest to a file, creating the destination directory when needed.</summary>
    private static async Task<StorageManifest.Result> WriteManifestToFileAsync(
        FilesDbContext db, string storageRoot, bool includeChunks, string manifestPath)
    {
        var manifestDir = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
        if (!string.IsNullOrEmpty(manifestDir))
            Directory.CreateDirectory(manifestDir);

        await using var stream = new FileStream(manifestPath, FileMode.Create, FileAccess.Write);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return await StorageManifest.WriteAsync(db, storageRoot, includeChunks, writer);
    }

    /// <summary>
    /// Generates a manifest file for the configured storage root. Returns <see langword="null"/> when
    /// it could not be produced (the reason is reported as a warning), so a backup run can continue.
    /// </summary>
    private static async Task<StorageManifest.Result?> TryGenerateManifestFileAsync(string manifestPath)
    {
        try
        {
            await using var provider = ServiceProviderFactory.CreateFromConfig();
            if (provider is null)
                return null;

            var storageRoot = StorageManifest.ResolveStorageRoot();
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();

            return await WriteManifestToFileAsync(db, storageRoot, includeChunks: true, manifestPath);
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteWarning($"Storage manifest could not be written: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Paths that must never be copied into an archive: the backup directory itself (otherwise every
    /// backup nests all of its predecessors, along with the storage tree) and the archive file that is
    /// currently being written (which would otherwise capture a partial copy of itself).
    /// </summary>
    /// <param name="backupDirectory">Configured backup directory, or null/blank when unknown.</param>
    /// <param name="backupPath">Archive file being written.</param>
    internal static IReadOnlyList<string> GetArchiveExclusions(string? backupDirectory, string backupPath)
    {
        var exclusions = new List<string>();
        if (!string.IsNullOrWhiteSpace(backupDirectory))
            exclusions.Add(Path.GetFullPath(backupDirectory));

        exclusions.Add(Path.GetFullPath(backupPath));
        return exclusions;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="filePath"/> is one of the excluded paths or
    /// lives beneath one of them. A sibling directory that merely shares a name prefix (for example
    /// <c>backups-old</c> next to <c>backups</c>) is deliberately <b>not</b> excluded.
    /// </summary>
    /// <param name="filePath">Candidate file.</param>
    /// <param name="exclusions">Excluded directories/files.</param>
    internal static bool IsExcludedFromArchive(string filePath, IReadOnlyList<string> exclusions)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var candidate = Path.GetFullPath(filePath);
        foreach (var exclusion in exclusions)
        {
            if (candidate.Equals(exclusion, comparison))
                return true;

            if (candidate.StartsWith(exclusion + Path.DirectorySeparatorChar, comparison))
                return true;
        }

        return false;
    }

    private static async Task<int> CreateBackupAsync(
        string? outputPath, bool includeDbDump, string? serverUrl, bool includeManifest, bool includeSystem = true,
        string compression = BackupCompression.Default)
    {
        // If --server is specified, call the server API to trigger the backup
        if (!string.IsNullOrWhiteSpace(serverUrl))
        {
            return await TriggerServerBackupAsync(serverUrl, outputPath);
        }

        // Local backup
        if (!CliConfiguration.ConfigExists())
        {
            ConsoleOutput.WriteError("DotNetCloud is not configured. Run 'dotnetcloud setup' first.");
            return 1;
        }

        var config = CliConfiguration.Load();
        var backupDir = config.BackupDirectory;
        Directory.CreateDirectory(backupDir);

        var compressionLevel = BackupCompression.Parse(compression);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var backupFileName = $"dotnetcloud-backup-{timestamp}.zip";
        var backupPath = outputPath ?? Path.Combine(backupDir, backupFileName);

        ConsoleOutput.WriteHeader("DotNetCloud Backup");
        ConsoleOutput.WriteInfo($"Creating backup: {backupPath}");

        try
        {
            // Step 1: Storage manifest — generated before the archive so every backup carries the index
            // that maps its content-addressed blobs back to files. Without it the archived storage tree
            // cannot be interpreted after a restore (blobs have no file extension).
            string? manifestPath = null;
            if (includeManifest)
            {
                manifestPath = Path.Combine(backupDir, $"dotnetcloud-manifest-{timestamp}.tsv");
                var manifest = await TryGenerateManifestFileAsync(manifestPath);
                if (manifest is null)
                {
                    manifestPath = null;
                    ConsoleOutput.WriteWarning("Continuing without a storage manifest.");
                }
                else
                {
                    ConsoleOutput.WriteSuccess(
                        $"Storage manifest created ({manifest.FileVersions} file versions, {manifest.ChunkRows} chunk rows).");
                }
            }
            else
            {
                ConsoleOutput.WriteInfo("Skipping the storage manifest (--no-manifest).");
            }

            // Step 2: Database dump. A backup that cannot be restored is worse than no backup, so a
            // requested dump that fails aborts the run instead of silently producing a file-only archive.
            var warnings = new List<string>();
            var dbDumpPath = Path.Combine(backupDir, $"dotnetcloud-db-{timestamp}.sql");
            var createdDbDump = false;

            if (includeDbDump)
            {
                if (string.IsNullOrWhiteSpace(config.DatabaseProvider))
                {
                    ConsoleOutput.WriteError(
                        "Database provider is not configured; refusing to create an incomplete backup. " +
                        "Use --no-db-dump for an explicit file-only backup.");
                    return 1;
                }

                ConsoleOutput.WriteInfo($"Creating database dump using {config.DatabaseProvider}...");
                try
                {
                    createdDbDump = await CreateDatabaseDumpAsync(
                        config.DatabaseProvider, config.ConnectionString, dbDumpPath);
                    if (!createdDbDump)
                        throw new InvalidOperationException("the dump tool produced no output");

                    ConsoleOutput.WriteSuccess("Database dump created.");
                }
                catch (Exception ex)
                {
                    ConsoleOutput.WriteError($"Database dump failed: {ex.Message}");
                    ConsoleOutput.WriteInfo(
                        $"Ensure {GetDumpToolName(config.DatabaseProvider)} is installed and the connection string is valid.");
                    ConsoleOutput.WriteInfo("Use --no-db-dump to intentionally create a file-only backup.");
                    return 1;
                }
            }
            else
            {
                warnings.Add("Database dump skipped (--no-db-dump); the archive cannot restore the database.");
                ConsoleOutput.WriteWarning("Database dump skipped (--no-db-dump). The archive will be file-only.");
            }

            // Step 3: Create zip archive
            var fileCount = 0;
            await using var zipStream = new FileStream(backupPath, FileMode.Create);
            // leaveOpen: the archive is disposed explicitly before the checksum is computed; closing
            // the stream there would make the subsequent flush fail.
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true);

            // Add database dump if created
            if (createdDbDump && File.Exists(dbDumpPath))
            {
                archive.CreateEntryFromFile(dbDumpPath, "database.sql", compressionLevel);
                fileCount++;
            }

            // Add the storage manifest at a fixed path, wherever it was written on disk.
            if (manifestPath is not null && File.Exists(manifestPath))
            {
                archive.CreateEntryFromFile(manifestPath, "manifest/storage-manifest.tsv", compressionLevel);
                fileCount++;
            }

            // Backup system configuration: config.json (DB connection string), env (Collabora/WOPI),
            // TLS certificates + root CA, the systemd unit and the small stateful files in /opt.
            // None of these live under the data directory, and a restore cannot start the server
            // without the certificate the restored config.json points at.
            var systemEntries = new List<string>();
            if (includeSystem)
            {
                var payload = BackupArchive.GetSystemPayloadEntries(
                    configDirectory: CliConfiguration.GetConfigDirectory());
                var existingSystemEntries = BackupArchive.ResolveExistingEntries(payload, warnings);

                foreach (var entry in existingSystemEntries)
                {
                    archive.CreateEntryFromFile(entry.SourcePath, entry.EntryName, compressionLevel);
                    systemEntries.Add(entry.EntryName);
                    fileCount++;
                }

                ConsoleOutput.WriteSuccess($"System configuration backed up ({existingSystemEntries.Count} file(s)).");
            }
            else
            {
                warnings.Add("System configuration skipped (--no-system); certificates/env/unit are not in the archive.");
                ConsoleOutput.WriteWarning("System configuration skipped (--no-system). The archive cannot cold-restore the server.");
            }

            // Backup data directory
            var dataDir = config.DataDirectory;
            var dataFileCount = 0;
            var emptyDirectoryCount = 0;
            if (Directory.Exists(dataDir))
            {
                // Never archive the backup directory itself (each backup would otherwise nest every
                // previous one) or the archive file currently being written.
                var exclusions = GetArchiveExclusions(config.BackupDirectory, backupPath);
                var allDataFiles = Directory.GetFiles(dataDir, "*", SearchOption.AllDirectories);
                var dataFiles = allDataFiles
                    .Where(file => !IsExcludedFromArchive(file, exclusions))
                    .ToArray();

                foreach (var file in dataFiles)
                {
                    var entryName = "data/" + Path.GetRelativePath(dataDir, file).Replace('\\', '/');
                    archive.CreateEntryFromFile(file, entryName, compressionLevel);
                    fileCount++;
                    dataFileCount++;
                }

                // Empty directories are meaningful template content (the stock default-user-content
                // folders are empty until an operator adds files). ZIP stores no implicit directory
                // entries, so archive them explicitly or a restore loses them.
                var emptyDirectories = Directory
                    .GetDirectories(dataDir, "*", SearchOption.AllDirectories)
                    .Where(dir => !IsExcludedFromArchive(dir, exclusions))
                    .Where(dir => !Directory.EnumerateFileSystemEntries(dir).Any())
                    .ToArray();

                foreach (var directory in emptyDirectories)
                {
                    var entryName = "data/"
                        + Path.GetRelativePath(dataDir, directory).Replace('\\', '/')
                        + "/";
                    archive.CreateEntry(entryName);
                    fileCount++;
                    emptyDirectoryCount++;
                }

                ConsoleOutput.WriteSuccess($"Data directory backed up ({dataFiles.Length} files).");

                var excludedCount = allDataFiles.Length - dataFiles.Length;
                if (excludedCount > 0)
                {
                    ConsoleOutput.WriteInfo(
                        $"  Excluded {excludedCount} file(s) under {config.BackupDirectory} (previous backups are not nested into this one).");
                }
            }

            // Legacy-friendly database info note (human readable).
            var dbInfoEntry = archive.CreateEntry("config/database-info.txt");
            await using (var writer = new StreamWriter(dbInfoEntry.Open()))
            {
                await writer.WriteLineAsync($"Provider: {config.DatabaseProvider}");
                await writer.WriteLineAsync($"Backup Time: {DateTime.UtcNow:O}");
                await writer.WriteLineAsync($"Database Dump Included: {createdDbDump}");
                await writer.WriteLineAsync($"System Configuration Included: {includeSystem}");
                await writer.WriteLineAsync();
                await writer.WriteLineAsync("Restore: sudo dotnetcloud restore <file>");
            }
            fileCount++;

            // Machine-readable metadata describing the archive (consumed by `dotnetcloud restore`).
            var backupInfo = new BackupInfo
            {
                CreatedUtc = DateTime.UtcNow.ToString("O"),
                CliVersion = BackupArchive.GetCliVersion(),
                DatabaseProvider = config.DatabaseProvider,
                DatabaseDumpIncluded = createdDbDump,
                StorageManifestIncluded = manifestPath is not null && File.Exists(manifestPath),
                Compression = compression,
                DataDirectory = dataDir,
                ConfigDirectory = CliConfiguration.GetConfigDirectory(),
                ServiceUser = Environment.GetEnvironmentVariable("DOTNETCLOUD_SERVICE_USER")
                    ?? BackupArchive.DefaultServiceUser,
                SystemEntries = systemEntries,
                DataFileCount = dataFileCount,
                EmptyDirectoryCount = emptyDirectoryCount,
                // ZipArchive.Entries cannot be enumerated in Create mode; fileCount already tracks
                // every entry written so far, and this metadata entry is the last one.
                TotalEntryCount = fileCount + 1,
                Warnings = warnings,
            };

            var infoEntry = archive.CreateEntry(BackupArchive.InfoEntryName);
            await using (var writer = new StreamWriter(infoEntry.Open()))
            {
                await writer.WriteAsync(BackupArchive.SerializeInfo(backupInfo));
            }
            fileCount++;

            // Finish the archive (writes the central directory) before hashing it.
            archive.Dispose();
            await zipStream.FlushAsync();

            var archiveHash = await ComputeSha256Async(backupPath);
            var shaPath = backupPath + ".sha256";
            await File.WriteAllTextAsync(
                shaPath, $"{archiveHash}  {Path.GetFileName(backupPath)}{Environment.NewLine}");

            // Report result
            var fileInfo = new FileInfo(backupPath);
            fileInfo.Refresh();

            ConsoleOutput.WriteSuccess($"Backup created: {backupPath}");
            ConsoleOutput.WriteSuccess($"  Files archived: {fileCount}");
            ConsoleOutput.WriteSuccess($"  Archive size: {FormatSize(fileInfo.Length)}");
            ConsoleOutput.WriteSuccess($"  Checksum: {shaPath}");

            foreach (var warning in backupInfo.Warnings)
                ConsoleOutput.WriteWarning(warning);

            if (manifestPath is not null)
            {
                ConsoleOutput.WriteSuccess("  Storage manifest: manifest/storage-manifest.tsv (inside the archive)");
                ConsoleOutput.WriteInfo($"  Standalone copy: {manifestPath}");
            }

            if (createdDbDump)
            {
                ConsoleOutput.WriteSuccess("Database dump included in archive (database.sql).");
            }

            if (includeSystem)
            {
                ConsoleOutput.WriteSuccess("System configuration included (config, certificates, env, systemd unit).");
            }

            return 0;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Backup failed: {ex.Message}");

            // Never leave a partial archive behind: it looks like a usable backup.
            if (File.Exists(backupPath))
            {
                try
                { File.Delete(backupPath); }
                catch { /* best effort */ }
            }

            if (File.Exists(backupPath + ".sha256"))
            {
                try
                { File.Delete(backupPath + ".sha256"); }
                catch { /* best effort */ }
            }

            return 1;
        }
        finally
        {
            // Clean up temporary database dump file
            var tempDump = Path.Combine(backupDir, $"dotnetcloud-db-{timestamp}.sql");
            if (File.Exists(tempDump))
            {
                try
                { File.Delete(tempDump); }
                catch { /* best effort */ }
            }
        }
    }

    private static async Task<int> TriggerServerBackupAsync(string serverUrl, string? outputPath)
    {
        ConsoleOutput.WriteHeader("DotNetCloud Server Backup");
        ConsoleOutput.WriteInfo($"Triggering backup via server API: {serverUrl}");

        try
        {
            using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            var baseUrl = serverUrl.TrimEnd('/');
            var startUrl = $"{baseUrl}/api/v1/core/admin/backup/start";
            if (!string.IsNullOrWhiteSpace(outputPath))
                startUrl += $"?outputPath={Uri.EscapeDataString(outputPath)}";

            using (var startResponse = await client.PostAsync(startUrl, null))
            {
                if (startResponse.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    ConsoleOutput.WriteError("The server is already running a backup.");
                    return 1;
                }

                if (!startResponse.IsSuccessStatusCode)
                {
                    var body = await startResponse.Content.ReadAsStringAsync();
                    ConsoleOutput.WriteError($"Server backup failed (HTTP {(int)startResponse.StatusCode}): {body}");
                    return 1;
                }
            }

            ConsoleOutput.WriteSuccess("Backup started on the server.");
            ConsoleOutput.WriteInfo("Waiting for completion (Ctrl+C stops waiting; the backup continues on the server).");

            var statusUrl = $"{baseUrl}/api/v1/core/admin/backup/status";
            string? lastPhase = null;

            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(3));

                var json = await client.GetStringAsync(statusUrl);
                using var document = System.Text.Json.JsonDocument.Parse(json);
                if (!document.RootElement.TryGetProperty("data", out var data))
                    continue;

                var isRunning = data.TryGetProperty("isRunning", out var running)
                    && running.ValueKind == System.Text.Json.JsonValueKind.True;
                var phase = data.TryGetProperty("phase", out var phaseElement) ? phaseElement.GetString() : null;

                if (phase is not null && !string.Equals(phase, lastPhase, StringComparison.Ordinal))
                {
                    ConsoleOutput.WriteInfo($"Phase: {phase}");
                    lastPhase = phase;
                }

                if (data.TryGetProperty("filesTotal", out var filesTotalElement)
                    && filesTotalElement.TryGetInt32(out var filesTotal)
                    && filesTotal > 0
                    && data.TryGetProperty("filesProcessed", out var filesProcessedElement)
                    && filesProcessedElement.TryGetInt32(out var filesProcessed)
                    && filesProcessed > 0
                    && filesProcessed % 500 == 0)
                {
                    ConsoleOutput.WriteInfo($"  {filesProcessed} / {filesTotal} files");
                }

                if (isRunning)
                    continue;

                var success = data.TryGetProperty("lastBackupSuccess", out var ok)
                    && ok.ValueKind == System.Text.Json.JsonValueKind.True;
                var path = data.TryGetProperty("lastBackupPath", out var pathElement) ? pathElement.GetString() : null;
                var error = data.TryGetProperty("lastError", out var errorElement) ? errorElement.GetString() : null;

                if (success)
                {
                    ConsoleOutput.WriteSuccess($"Backup completed: {path}");
                    return 0;
                }

                ConsoleOutput.WriteError($"Backup failed: {error ?? "unknown error"}");
                return 1;
            }
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Failed to connect to server: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RestoreBackupAsync(string filePath, RestoreCommandOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            ConsoleOutput.WriteError($"Backup file not found: {filePath}");
            return 1;
        }

        ConsoleOutput.WriteHeader("DotNetCloud Restore");

        try
        {
            using var archive = ZipFile.OpenRead(filePath);

            var infoEntry = archive.GetEntry(BackupArchive.InfoEntryName);
            var info = infoEntry is null ? null : BackupArchive.DeserializeInfo(ReadEntryText(infoEntry));

            var hasDatabaseDump = archive.GetEntry(BackupArchive.DatabaseEntryName) is not null;
            var hasData = archive.Entries.Any(e => e.FullName.StartsWith("data/", StringComparison.Ordinal));
            var hasConfig = archive.GetEntry("config/config.json") is not null;
            var hasSystem = archive.Entries.Any(e => e.FullName.StartsWith("system/", StringComparison.Ordinal));

            PrintRestorePlan(filePath, info, hasDatabaseDump, hasData, hasConfig, hasSystem);

            if (!options.AssumeYes
                && !ConsoleOutput.PromptConfirm("Continue with restore? Existing configuration and data will be overwritten."))
            {
                ConsoleOutput.WriteInfo("Restore cancelled.");
                return 0;
            }

            // Everything is streamed straight from the archive to its destination: no staging copy,
            // so a restore needs roughly one extra copy of the data (the archive) rather than two.
            // Stop the service before replacing files it has open or config it reads at startup.
            if (options.ManageService)
                RunSystemctl("stop", "dotnetcloud", ignoreFailure: true);

            var serviceUser = string.IsNullOrWhiteSpace(info?.ServiceUser)
                ? BackupArchive.DefaultServiceUser
                : info.ServiceUser;

            if (options.RestoreSystem)
                RestoreSystemConfiguration(archive, serviceUser);
            else
                ConsoleOutput.WriteWarning("System configuration restore skipped (--skip-system).");

            if (hasConfig && options.RestoreSystem)
                ConsoleOutput.WriteSuccess("Configuration file restored.");

            // Load the restored configuration: it supplies the data directory and DB connection string.
            var config = CliConfiguration.Load();

            var dataDirectory = string.IsNullOrWhiteSpace(options.DataDirectory)
                ? config.DataDirectory
                : options.DataDirectory;

            if (!string.IsNullOrWhiteSpace(options.DataDirectory)
                && !options.DataDirectory.Equals(config.DataDirectory, StringComparison.Ordinal))
            {
                ConsoleOutput.WriteWarning(
                    $"Restoring data to '{dataDirectory}', but config.json records '{config.DataDirectory}'. " +
                    "Update dataDirectory in config.json or the service will not find its data.");
            }

            if (hasData)
                RestoreDataDirectory(archive, dataDirectory, serviceUser);
            else
                ConsoleOutput.WriteWarning("Archive contains no data/ entries.");

            // The root CA must be trusted before the server starts: its own module gRPC clients and
            // Blazor loopback calls validate the self-signed certificate.
            if (options.RestoreSystem)
            {
                InstallRootCaIntoSystrustStore();
                GrantCoolwsdBackupReadAccess();
            }

            if (options.RestoreDatabase)
            {
                if (!hasDatabaseDump)
                    ConsoleOutput.WriteWarning("Archive contains no database.sql — database not restored.");
                else if (!await RestoreDatabaseAsync(archive, config, options))
                    return 1;
            }
            else
            {
                ConsoleOutput.WriteWarning("Database restore skipped (--skip-database).");
            }

            if (options.ManageService)
            {
                RunSystemctl("daemon-reload", null, ignoreFailure: true);
                RunSystemctl("enable", "dotnetcloud", ignoreFailure: true);

                if (!RunSystemctl("start", "dotnetcloud", ignoreFailure: false))
                {
                    ConsoleOutput.WriteError(
                        "Could not start the dotnetcloud service. " +
                        "Check: sudo journalctl -u dotnetcloud -n 100 --no-pager");
                    return 1;
                }

                if (options.WaitForHealth)
                    await WaitForHealthAsync(config);
            }

            ConsoleOutput.WriteSuccess("Restore complete.");
            ConsoleOutput.WriteInfo("Verify with: curl -sk https://localhost:5443/health/ready");
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Restore failed: {ex.Message}");
            return 1;
        }
    }

    private static void PrintRestorePlan(
        string filePath, BackupInfo? info, bool hasDatabaseDump, bool hasData, bool hasConfig, bool hasSystem)
    {
        ConsoleOutput.WriteInfo($"Archive: {filePath}");

        if (info is not null)
        {
            ConsoleOutput.WriteDetail("Created (UTC)", info.CreatedUtc);
            ConsoleOutput.WriteDetail("CLI version", info.CliVersion);
            ConsoleOutput.WriteDetail("Database provider", info.DatabaseProvider ?? "(unknown)");
            ConsoleOutput.WriteDetail("Data directory", info.DataDirectory ?? "(unknown)");
            ConsoleOutput.WriteDetail("Backup format", info.FormatVersion.ToString());
        }
        else
        {
            ConsoleOutput.WriteWarning("No backup-info.json — this is a legacy archive; system configuration may be incomplete.");
        }

        ConsoleOutput.WriteDetail("Database dump", hasDatabaseDump ? "yes" : "NO");
        ConsoleOutput.WriteDetail("Data files", hasData ? "yes" : "NO");
        ConsoleOutput.WriteDetail("Configuration", hasConfig ? "yes" : "NO");
        ConsoleOutput.WriteDetail("System files", hasSystem ? "yes" : "NO");

        if (!hasDatabaseDump)
            ConsoleOutput.WriteWarning(
                "No database dump: users, settings, shares and file metadata will be missing after restore.");
        if (!hasSystem)
            ConsoleOutput.WriteWarning(
                "No system configuration: TLS certificate, env, systemd unit and root CA will not be restored.");
        if (!hasConfig)
            ConsoleOutput.WriteWarning("No config/config.json: the database connection string cannot be restored.");
    }

    private static string ReadEntryText(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Streams the archived system configuration back to its live locations, fixing ownership and
    /// modes that an extraction performed as root would otherwise leave wrong.
    /// </summary>
    private static void RestoreSystemConfiguration(ZipArchive archive, string serviceUser)
    {
        var configDirectory = CliConfiguration.GetConfigDirectory();
        Directory.CreateDirectory(configDirectory);
        SetOwnerAndMode(configDirectory, serviceUser, serviceUser,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        var restored = 0;
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName;
            if (name.EndsWith('/'))
                continue;

            if (name.StartsWith("config/", StringComparison.Ordinal))
            {
                if (name.Equals("config/database-info.txt", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!BackupArchive.TryResolveRestoreTarget(
                        configDirectory, name["config/".Length..], out var target, out var reason))
                {
                    ConsoleOutput.WriteWarning($"Skipping '{name}' — {reason}.");
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, overwrite: true);
                SetOwnerAndMode(target, "root", serviceUser,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
                ConsoleOutput.WriteDetail("Restored", target);
                restored++;
            }
            else if (name.StartsWith("system/systemd/", StringComparison.Ordinal))
            {
                if (!BackupArchive.TryResolveRestoreTarget(
                        BackupArchive.DefaultSystemdDirectory, name["system/systemd/".Length..], out var target, out var reason))
                {
                    ConsoleOutput.WriteWarning($"Skipping '{name}' — {reason}.");
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, overwrite: true);
                SetOwnerAndMode(target, "root", "root",
                    UnixFileMode.UserRead | UnixFileMode.UserWrite
                    | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
                ConsoleOutput.WriteDetail("Restored", target);
                restored++;
            }
            else if (name.StartsWith("system/bin/", StringComparison.Ordinal))
            {
                var fileName = Path.GetFileName(name);
                if (string.IsNullOrEmpty(fileName) || fileName.Contains("..", StringComparison.Ordinal))
                    continue;

                var target = Path.Combine("/usr/local/bin", fileName);
                entry.ExtractToFile(target, overwrite: true);
                SetOwnerAndMode(target, "root", "root",
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                ConsoleOutput.WriteDetail("Restored", target);
                restored++;
            }
            else if (name.StartsWith("system/coolwsd/", StringComparison.Ordinal))
            {
                if (!Directory.Exists(BackupArchive.DefaultCoolwsdDirectory))
                {
                    ConsoleOutput.WriteWarning(
                        $"Collabora configuration is in the archive, but {BackupArchive.DefaultCoolwsdDirectory} " +
                        "does not exist. Install Collabora CODE, then re-run the restore.");
                    continue;
                }

                if (!BackupArchive.TryResolveRestoreTarget(
                        BackupArchive.DefaultCoolwsdDirectory, name["system/coolwsd/".Length..], out var target, out var reason))
                {
                    ConsoleOutput.WriteWarning($"Skipping '{name}' — {reason}.");
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, overwrite: true);
                SetOwnerAndMode(target, "root", GroupExists("cool") ? "cool" : "root",
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
                ConsoleOutput.WriteDetail("Restored", target);
                restored++;
            }
            else if (name.StartsWith("opt/", StringComparison.Ordinal))
            {
                var fileName = Path.GetFileName(name);
                if (string.IsNullOrEmpty(fileName) || fileName.Contains("..", StringComparison.Ordinal))
                    continue;

                Directory.CreateDirectory(BackupArchive.DefaultInstallDirectory);
                var target = Path.Combine(BackupArchive.DefaultInstallDirectory, fileName);
                entry.ExtractToFile(target, overwrite: true);
                SetOwnerAndMode(target, serviceUser, serviceUser,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite
                    | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
                ConsoleOutput.WriteDetail("Restored", target);
                restored++;
            }
        }

        var certsDirectory = Path.Combine(configDirectory, "certs");
        if (Directory.Exists(certsDirectory))
        {
            SetOwnerAndMode(certsDirectory, "root", "root",
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        if (restored == 0)
        {
            ConsoleOutput.WriteWarning(
                "Archive contains no system configuration entries (legacy backup); " +
                "certificates, env and the systemd unit were not restored.");
        }
        else
        {
            ConsoleOutput.WriteSuccess($"System configuration restored ({restored} file(s)).");
        }
    }

    /// <summary>
    /// Streams the archived <c>data/</c> tree into the data directory and restores the ownership and
    /// modes the service expects (a zip extraction cannot be trusted to preserve either). No staging
    /// copy is made, so peak disk use is the archive plus the restored tree.
    /// </summary>
    private static void RestoreDataDirectory(ZipArchive archive, string dataDirectory, string serviceUser)
    {
        Directory.CreateDirectory(dataDirectory);

        var fileCount = 0;
        var directoryCount = 0;

        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName;
            if (!name.StartsWith("data/", StringComparison.Ordinal))
                continue;

            var relative = name["data/".Length..];
            if (relative.Length == 0)
                continue;

            if (!BackupArchive.TryResolveRestoreTarget(dataDirectory, relative, out var target, out var reason))
            {
                ConsoleOutput.WriteWarning($"Skipping '{name}' — {reason}.");
                continue;
            }

            if (name.EndsWith('/'))
            {
                Directory.CreateDirectory(target);
                directoryCount++;
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
            fileCount++;
        }

        ConsoleOutput.WriteSuccess(
            $"Data directory restored ({fileCount} file(s), {directoryCount} director{(directoryCount == 1 ? "y" : "ies")}).");

        // The service runs unprivileged; restored files were written as root.
        RunProcess("chown", ["-R", $"{serviceUser}:{serviceUser}", dataDirectory], null, out _, out _);

        // Modes that matter for security. Secret material must not become world-readable just because
        // it went through a zip extraction.
        TrySetMode(Path.Combine(dataDirectory, "tmp"),
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        TrySetMode(Path.Combine(dataDirectory, "oidc-keys"), UserDirectoryMode);
        TrySetMode(Path.Combine(dataDirectory, "data-protection-keys"), UserDirectoryMode);

        foreach (var pem in SafeEnumerateFiles(Path.Combine(dataDirectory, "oidc-keys"), "*.pem"))
            TrySetMode(pem, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        foreach (var xml in SafeEnumerateFiles(Path.Combine(dataDirectory, "data-protection-keys"), "*.xml"))
            TrySetMode(xml, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        var backupsDirectory = Path.Combine(dataDirectory, "backups");
        Directory.CreateDirectory(backupsDirectory);
        RunProcess("chown", [$"{serviceUser}:{serviceUser}", backupsDirectory], null, out _, out _);
    }

    private const UnixFileMode UserDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private static IEnumerable<string> SafeEnumerateFiles(string directory, string pattern)
    {
        if (!Directory.Exists(directory))
            return [];

        try
        { return Directory.EnumerateFiles(directory, pattern).ToArray(); }
        catch
        { return []; }
    }

    private static void TrySetMode(string path, UnixFileMode mode)
    {
        if (OperatingSystem.IsWindows() || !Path.Exists(path))
            return;

        try
        { File.SetUnixFileMode(path, mode); }
        catch { /* best effort */ }
    }

    private static void SetOwnerAndMode(string path, string owner, string group, UnixFileMode mode)
    {
        TrySetMode(path, mode);
        if (!OperatingSystem.IsLinux())
            return;

        RunProcess("chown", [$"{owner}:{group}", path], null, out _, out _);
    }

    private static bool GroupExists(string group)
    {
        if (!OperatingSystem.IsLinux())
            return false;

        return RunProcess("getent", ["group", group], null, out _, out _);
    }

    /// <summary>
    /// Grants the service account read access to Collabora's config and public certificates so future
    /// Admin backups include them. The private keys stay unreadable (the 'cool' group is not used).
    /// </summary>
    private static void GrantCoolwsdBackupReadAccess()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var files = new[]
        {
            Path.Combine(BackupArchive.DefaultCoolwsdDirectory, "coolwsd.xml"),
            Path.Combine(BackupArchive.DefaultCoolwsdDirectory, "cert.pem"),
            Path.Combine(BackupArchive.DefaultCoolwsdDirectory, "ca-chain.cert.pem"),
        }.Where(File.Exists).ToArray();

        if (files.Length == 0)
            return;

        var serviceUser = Environment.GetEnvironmentVariable("DOTNETCLOUD_SERVICE_USER")
            ?? BackupArchive.DefaultServiceUser;

        var arguments = new List<string> { "-m", $"u:{serviceUser}:r" };
        arguments.AddRange(files);

        if (RunProcess("setfacl", arguments, null, out _, out var error))
            ConsoleOutput.WriteSuccess("Granted the service account read access to Collabora configuration.");
        else
            ConsoleOutput.WriteWarning($"Could not grant the service account read access to Collabora configuration: {error}");
    }

    /// <summary>
    /// Installs the backup's root CA into the Linux trust store so the restored server can validate
    /// its own self-signed certificate (module gRPC clients and Blazor loopback calls).
    /// </summary>
    private static void InstallRootCaIntoSystrustStore()
    {
        if (!OperatingSystem.IsLinux())
            return;

        const string target = "/usr/local/share/ca-certificates/dotnetcloud-root-ca.crt";
        var source = Path.Combine(CliConfiguration.GetConfigDirectory(), "certs", "dotnetcloud-root-ca.crt");

        if (!File.Exists(source))
        {
            ConsoleOutput.WriteWarning($"Root CA not found at {source}; skipping trust-store installation.");
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);
            TrySetMode(target,
                UnixFileMode.UserRead | UnixFileMode.UserWrite
                | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

            if (RunProcess("update-ca-certificates", [], null, out _, out var error))
                ConsoleOutput.WriteSuccess("Root CA installed into the system trust store.");
            else
                ConsoleOutput.WriteWarning($"update-ca-certificates failed: {error}");
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteWarning($"Could not install the root CA: {ex.Message}");
        }
    }

    /// <summary>
    /// Restores <c>database.sql</c>. PostgreSQL is fully automated; SQL Server is reported as a manual
    /// step because <c>BACKUP DATABASE</c> produces a server-side <c>.bak</c>.
    /// </summary>
    private static async Task<bool> RestoreDatabaseAsync(
        ZipArchive archive, CliConfig config, RestoreCommandOptions options)
    {
        var dumpEntry = archive.GetEntry(BackupArchive.DatabaseEntryName);
        if (dumpEntry is null)
            return true;

        if (BackupArchive.IsPostgreSql(config.DatabaseProvider))
            return await RestorePostgreSqlDatabaseAsync(dumpEntry, config, options.ReplaceDatabase);

        if (BackupArchive.IsSqlServer(config.DatabaseProvider))
        {
            ConsoleOutput.WriteWarning("Automated SQL Server restore is not supported by this command.");
            ConsoleOutput.WriteInfo("Extract database.sql from the archive and restore it with sqlcmd:");
            ConsoleOutput.WriteInfo(
                "  sqlcmd -S <server> -U <user> -P <password> -Q \"RESTORE DATABASE [dotnetcloud] FROM DISK = '<file>'\"");
            return false;
        }

        ConsoleOutput.WriteError($"Unsupported database provider '{config.DatabaseProvider}'.");
        return false;
    }

    private static async Task<bool> RestorePostgreSqlDatabaseAsync(
        ZipArchiveEntry dumpEntry, CliConfig config, bool replaceExisting)
    {
        var connectionString = config.ConnectionString;
        var host = BackupArchive.TryGetConnectionStringValue(connectionString, "host", "server", "datasource") ?? "localhost";
        var port = BackupArchive.TryGetConnectionStringValue(connectionString, "port") ?? "5432";
        var database = BackupArchive.TryGetConnectionStringValue(connectionString, "database", "db") ?? "dotnetcloud";
        var user = BackupArchive.TryGetConnectionStringValue(connectionString, "username", "user id", "user") ?? "dotnetcloud";
        var password = BackupArchive.TryGetConnectionStringPassword(connectionString) ?? string.Empty;

        ConsoleOutput.WriteInfo($"Restoring PostgreSQL database '{database}' as '{user}'...");

        // The dump contains ALTER ... OWNER TO <user> statements, so the role must exist first.
        if (!TryRunPostgresSql(null, $"SELECT 1 FROM pg_roles WHERE rolname = {SqlLiteral(user)};", out var roleOutput)
            || roleOutput.Trim() != "1")
        {
            if (!TryRunPostgresSql(null,
                    $"CREATE ROLE \"{QuoteIdentifier(user)}\" WITH LOGIN PASSWORD {SqlLiteral(password)} CREATEDB;",
                    out var createError))
            {
                ConsoleOutput.WriteError($"Could not create the database role '{user}': {createError}");
                return false;
            }

            ConsoleOutput.WriteSuccess($"Database role '{user}' created.");
        }
        else
        {
            // Keep the role password in sync with the restored config.json. Without this, a role
            // created by a fresh `setup` run would not match the restored connection string.
            if (!TryRunPostgresSql(null,
                    $"ALTER ROLE \"{QuoteIdentifier(user)}\" WITH LOGIN PASSWORD {SqlLiteral(password)} CREATEDB;",
                    out var alterError))
            {
                ConsoleOutput.WriteError($"Could not update the database role password: {alterError}");
                return false;
            }
        }

        var databaseExists = TryRunPostgresSql(
            null, $"SELECT 1 FROM pg_database WHERE datname = {SqlLiteral(database)};", out var dbOutput)
            && dbOutput.Trim() == "1";

        if (databaseExists)
        {
            var tableCountText = TryRunPostgresSql(database,
                "SELECT count(*) FROM information_schema.tables " +
                "WHERE table_schema NOT IN ('pg_catalog', 'information_schema');",
                out var countError)
                ? countError.Trim()
                : "0";

            if (!int.TryParse(tableCountText, out var tableCount))
                tableCount = 0;

            if (tableCount > 0)
            {
                if (!replaceExisting)
                {
                    ConsoleOutput.WriteError(
                        $"Database '{database}' already contains {tableCount} table(s). " +
                        "Re-run with --replace-database to drop and recreate it.");
                    return false;
                }

                ConsoleOutput.WriteWarning($"Dropping existing database '{database}'...");
                if (!TryRunPostgresSql(null, $"DROP DATABASE \"{QuoteIdentifier(database)}\" WITH (FORCE);", out var dropError))
                {
                    ConsoleOutput.WriteError($"Could not drop database '{database}': {dropError}");
                    return false;
                }

                databaseExists = false;
            }
        }

        if (!databaseExists)
        {
            if (!TryRunPostgresSql(null,
                    $"CREATE DATABASE \"{QuoteIdentifier(database)}\" OWNER \"{QuoteIdentifier(user)}\";",
                    out var createDbError))
            {
                ConsoleOutput.WriteError($"Could not create database '{database}': {createDbError}");
                return false;
            }

            ConsoleOutput.WriteSuccess($"Database '{database}' created.");
        }

        var (applied, applyError) = await ApplyDumpFromEntryAsync(
            dumpEntry, host, port, user, database, password);

        if (!applied)
        {
            ConsoleOutput.WriteError($"Database dump could not be applied: {applyError}");
            return false;
        }

        ConsoleOutput.WriteSuccess("Database restored from database.sql.");

        if (TryRunPostgresSql(database,
                "SELECT count(*) FROM information_schema.tables " +
                "WHERE table_schema NOT IN ('pg_catalog', 'information_schema');",
                out var restoredCount))
        {
            ConsoleOutput.WriteDetail("Tables restored", restoredCount.Trim());
        }

        return true;
    }

    /// <summary>
    /// Streams <c>database.sql</c> from the archive into <c>psql</c> stdin, avoiding a second copy of
    /// the dump on disk.
    /// </summary>
    private static async Task<(bool Success, string Error)> ApplyDumpFromEntryAsync(
        ZipArchiveEntry dumpEntry, string host, string port, string user, string database, string password)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "psql",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            foreach (var argument in new[]
                     {
                         "-X", "-q", "-v", "ON_ERROR_STOP=1",
                         "-h", host, "-p", port, "-U", user, "-d", database, "-f", "-",
                     })
            {
                psi.ArgumentList.Add(argument);
            }

            psi.Environment["PGPASSWORD"] = password;

            using var process = new Process { StartInfo = psi };
            process.Start();

            // Drain both streams while the dump is written to avoid pipe-buffer deadlocks.
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await using (var entryStream = dumpEntry.Open())
            {
                await entryStream.CopyToAsync(process.StandardInput.BaseStream);
            }

            process.StandardInput.Close();
            await process.WaitForExitAsync();

            var error = await errorTask;
            _ = await outputTask;
            return (process.ExitCode == 0, error);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Runs a SQL statement as the <c>postgres</c> superuser (peer authentication), optionally against
    /// a specific database. Used for role/database creation and emptiness checks.
    /// </summary>
    private static bool TryRunPostgresSql(string? database, string sql, out string output)
    {
        output = string.Empty;
        if (!OperatingSystem.IsLinux())
            return false;

        var arguments = new List<string> { "-u", "postgres", "--", "psql", "-X", "-v", "ON_ERROR_STOP=1", "-tA" };
        if (!string.IsNullOrWhiteSpace(database))
        {
            arguments.Add("-d");
            arguments.Add(database);
        }

        arguments.Add("-c");
        arguments.Add(sql);

        var tool = File.Exists("/usr/sbin/runuser") ? "/usr/sbin/runuser" : "runuser";
        var succeeded = RunProcess(tool, arguments, null, out var standardOutput, out var standardError);

        output = succeeded ? standardOutput : standardError;
        return succeeded;
    }

    private static string QuoteIdentifier(string value) => value.Replace("\"", "\"\"", StringComparison.Ordinal);

    private static string SqlLiteral(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static bool RunSystemctl(string action, string? service, bool ignoreFailure)
    {
        if (!OperatingSystem.IsLinux())
            return true;

        var arguments = service is null ? new[] { action } : [action, service];
        var succeeded = RunProcess("systemctl", arguments, null, out _, out var error);

        if (!succeeded && !ignoreFailure)
            ConsoleOutput.WriteError($"systemctl {string.Join(' ', arguments)} failed: {error}");

        return succeeded;
    }

    private static async Task WaitForHealthAsync(CliConfig config)
    {
        var scheme = config.EnableHttps ? "https" : "http";
        var port = config.EnableHttps ? config.HttpsPort : config.HttpPort;
        var url = $"{scheme}://localhost:{port}/health/ready";

        ConsoleOutput.WriteInfo($"Waiting for {url} ...");

        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };

        for (var attempt = 0; attempt < 45; attempt++)
        {
            try
            {
                using var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    ConsoleOutput.WriteSuccess($"Health endpoint is ready (HTTP {(int)response.StatusCode}).");
                    return;
                }

                ConsoleOutput.WriteInfo($"Health endpoint returned HTTP {(int)response.StatusCode}; retrying...");
            }
            catch
            {
                // Still starting (connection refused is expected during module startup).
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        ConsoleOutput.WriteWarning("Health endpoint did not become ready within 90 seconds. Check the service logs.");
    }

    /// <summary>
    /// Runs a process with an optional environment overlay and captures both output streams.
    /// </summary>
    private static bool RunProcess(
        string fileName,
        IEnumerable<string> arguments,
        IReadOnlyDictionary<string, string>? environment,
        out string standardOutput,
        out string standardError)
    {
        standardOutput = string.Empty;
        standardError = string.Empty;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            if (environment is not null)
            {
                foreach (var (key, value) in environment)
                    startInfo.Environment[key] = value;
            }

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            // Read both streams concurrently to avoid a deadlock on large output.
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            standardOutput = outputTask.GetAwaiter().GetResult();
            standardError = errorTask.GetAwaiter().GetResult();
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            standardError = ex.Message;
            return false;
        }
    }

    private static Task<int> ScheduleBackupAsync(string interval)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(interval);

        var normalized = interval.ToLowerInvariant();
        if (normalized is not ("daily" or "weekly" or "monthly"))
        {
            ConsoleOutput.WriteError("Invalid interval. Use: daily, weekly, or monthly.");
            return Task.FromResult(1);
        }

        ConsoleOutput.WriteHeader("Backup Schedule");

        ConsoleOutput.WriteInfo("You can configure scheduled backups in two ways:");
        Console.WriteLine();

        ConsoleOutput.WriteInfo("Option 1: Server Admin UI (recommended)");
        ConsoleOutput.WriteInfo("  Go to Admin → Backup Settings in the web UI and configure the schedule there.");
        ConsoleOutput.WriteInfo("  The server's BackupHostedService will run backups automatically.");
        Console.WriteLine();

        ConsoleOutput.WriteInfo("Option 2: System scheduler (manual)");
        if (OperatingSystem.IsLinux())
        {
            var cronSchedule = normalized switch
            {
                "daily" => "0 2 * * *",
                "weekly" => "0 2 * * 0",
                "monthly" => "0 2 1 * *",
                _ => "0 2 * * *"
            };

            ConsoleOutput.WriteInfo("Add the following to your crontab (crontab -e):");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  {cronSchedule} dotnetcloud backup");
            Console.ResetColor();
        }
        else if (OperatingSystem.IsWindows())
        {
            var scheduleType = normalized switch
            {
                "daily" => "DAILY",
                "weekly" => "WEEKLY",
                "monthly" => "MONTHLY",
                _ => "DAILY"
            };

            ConsoleOutput.WriteInfo("Run the following command to create a scheduled task:");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  schtasks /create /tn \"DotNetCloud Backup\" /tr \"dotnetcloud backup\" /sc {scheduleType} /st 02:00");
            Console.ResetColor();
        }
        else
        {
            ConsoleOutput.WriteInfo("Configure your system's task scheduler to run:");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  dotnetcloud backup");
            Console.ResetColor();
            Console.WriteLine();
            ConsoleOutput.WriteInfo($"Schedule: {normalized}");
        }

        Console.WriteLine();
        ConsoleOutput.WriteInfo("Backups will be stored in the configured backup directory.");
        ConsoleOutput.WriteInfo("Use '--db-dump' to include a database dump.");

        return Task.FromResult(0);
    }

    // -----------------------------------------------------------------------
    // Database dump helpers
    // -----------------------------------------------------------------------

    private static async Task<bool> CreateDatabaseDumpAsync(string provider, string? connectionString, string outputPath)
    {
        string tool;
        List<string> argList;

        if (BackupArchive.IsPostgreSql(provider))
        {
            tool = "pg_dump";
            argList = BuildPgDumpArgs(connectionString, outputPath);
        }
        else if (BackupArchive.IsSqlServer(provider))
        {
            tool = "sqlcmd";
            argList = BuildSqlServerDumpArgs(connectionString, outputPath);
        }
        else
        {
            throw new NotSupportedException($"Database provider '{provider}' is not supported for automated dump.");
        }

        var psi = new ProcessStartInfo
        {
            FileName = tool,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // pg_dump has no password argument: it reads PGPASSWORD (or prompts, which cannot work in a
        // non-interactive backup). Without this the dump always fails against a password-authenticated
        // PostgreSQL server.
        foreach (var (key, value) in BackupArchive.BuildToolEnvironment(provider, connectionString))
            psi.Environment[key] = value;

        foreach (var a in argList)
            psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await errorTask;
            throw new InvalidOperationException($"Process exited with code {process.ExitCode}: {error}");
        }

        // For pg_dump with -Fp, output goes to stdout. Write to file if needed.
        var stdout = await outputTask;
        if (!string.IsNullOrEmpty(stdout) && !File.Exists(outputPath))
        {
            await File.WriteAllTextAsync(outputPath, stdout);
        }

        return File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
    }

    internal static List<string> BuildPgDumpArgs(string? connectionString, string outputPath)
    {
        var (host, port, db, user) = ParseConnectionString(connectionString);
        // -Fp selects the plain format; -f names the output file. Passing the path positionally after
        // -Fp makes pg_dump reject it as an extra command-line argument.
        return new List<string> { "-h", host, "-p", port, "-U", user, "-d", db, "-Fp", "-f", outputPath };
    }

    /// <summary>
    /// Builds <c>sqlcmd</c> arguments for a full database backup. Note that <c>BACKUP DATABASE</c>
    /// writes the <c>.bak</c> file on the SQL Server host, not on the machine running the CLI.
    /// </summary>
    internal static List<string> BuildSqlServerDumpArgs(string? connectionString, string outputPath)
    {
        var args = new List<string>();

        var server = BackupArchive.TryGetConnectionStringValue(connectionString, "server", "data source", "datasource");
        var user = BackupArchive.TryGetConnectionStringValue(connectionString, "user id", "uid", "username", "user");
        var password = BackupArchive.TryGetConnectionStringPassword(connectionString);

        if (!string.IsNullOrWhiteSpace(server))
        {
            args.Add("-S");
            args.Add(server);
        }

        if (!string.IsNullOrWhiteSpace(user))
        {
            args.Add("-U");
            args.Add(user);
        }

        if (!string.IsNullOrWhiteSpace(password))
        {
            args.Add("-P");
            args.Add(password);
        }

        args.Add("-Q");
        args.Add($"BACKUP DATABASE [dotnetcloud] TO DISK = '{outputPath}'");
        return args;
    }

    /// <summary>Computes the SHA-256 checksum of a file.</summary>
    private static async Task<string> ComputeSha256Async(string filePath)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = await System.Security.Cryptography.SHA256.HashDataAsync(stream);
        return Convert.ToHexStringLower(hash);
    }

    private static (string Host, string Port, string Database, string User) ParseConnectionString(string? connectionString)
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

    private static string GetDumpToolName(string provider) => provider.ToUpperInvariant() switch
    {
        "POSTGRESQL" => "pg_dump",
        "SQLSERVER" => "sqlcmd",
        _ => provider,
    };

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
        _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB",
    };
}
