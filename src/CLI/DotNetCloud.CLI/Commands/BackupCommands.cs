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
            Description = "Include a database dump in the backup (requires pg_dump/sqlcmd)",
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

        var command = new Command("backup", "Create a backup of DotNetCloud data");
        command.Options.Add(outputOption);
        command.Options.Add(dbDumpOption);
        command.Options.Add(serverOption);
        command.Options.Add(noManifestOption);
        command.Subcommands.Add(CreateRestore());
        command.Subcommands.Add(CreateSchedule());
        command.Subcommands.Add(CreateManifest());

        command.SetAction(parseResult =>
        {
            var output = parseResult.GetValue(outputOption);
            var dbDump = parseResult.GetValue(dbDumpOption);
            var server = parseResult.GetValue(serverOption);
            var noManifest = parseResult.GetValue(noManifestOption);
            return CreateBackupAsync(output, dbDump, server, includeManifest: !noManifest);
        });

        return command;
    }

    private static Command CreateRestore()
    {
        var fileArg = new Argument<string>("file")
        {
            Description = "Path to the backup file to restore"
        };
        var command = new Command("restore", "Restore from a backup file") { fileArg };
        command.SetAction(parseResult =>
        {
            var file = parseResult.GetValue(fileArg)!;
            return RestoreBackupAsync(file);
        });
        return command;
    }

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
        string? outputPath, bool includeDbDump, string? serverUrl, bool includeManifest)
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

            // Step 2: Optional database dump
            var dbDumpPath = Path.Combine(backupDir, $"dotnetcloud-db-{timestamp}.sql");
            var createdDbDump = false;

            if (includeDbDump && !string.IsNullOrWhiteSpace(config.DatabaseProvider))
            {
                ConsoleOutput.WriteInfo($"Creating database dump using {config.DatabaseProvider}...");
                try
                {
                    createdDbDump = await CreateDatabaseDumpAsync(config.DatabaseProvider, config.ConnectionString, dbDumpPath);
                    if (createdDbDump)
                    {
                        ConsoleOutput.WriteSuccess("Database dump created.");
                    }
                }
                catch (Exception ex)
                {
                    ConsoleOutput.WriteWarning($"Database dump failed: {ex.Message}");
                    ConsoleOutput.WriteInfo($"Install {GetDumpToolName(config.DatabaseProvider)} or run without --db-dump.");
                }
            }

            // Step 3: Create zip archive
            var fileCount = 0;
            await using var zipStream = new FileStream(backupPath, FileMode.Create);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

            // Add database dump if created
            if (createdDbDump && File.Exists(dbDumpPath))
            {
                archive.CreateEntryFromFile(dbDumpPath, "database.sql");
                fileCount++;
            }

            // Add the storage manifest at a fixed path, wherever it was written on disk.
            if (manifestPath is not null && File.Exists(manifestPath))
            {
                archive.CreateEntryFromFile(manifestPath, "manifest/storage-manifest.tsv");
                fileCount++;
            }

            // Backup configuration
            var configPath = CliConfiguration.GetConfigFilePath();
            if (File.Exists(configPath))
            {
                archive.CreateEntryFromFile(configPath, "config/config.json");
                fileCount++;
                ConsoleOutput.WriteSuccess("Configuration backed up.");
            }

            // Backup data directory
            var dataDir = config.DataDirectory;
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
                    archive.CreateEntryFromFile(file, entryName);
                    fileCount++;
                }

                ConsoleOutput.WriteSuccess($"Data directory backed up ({dataFiles.Length} files).");

                var excludedCount = allDataFiles.Length - dataFiles.Length;
                if (excludedCount > 0)
                {
                    ConsoleOutput.WriteInfo(
                        $"  Excluded {excludedCount} file(s) under {config.BackupDirectory} (previous backups are not nested into this one).");
                }
            }

            // Backup database info entry
            var dbInfoEntry = archive.CreateEntry("config/database-info.txt");
            await using (var writer = new StreamWriter(dbInfoEntry.Open()))
            {
                await writer.WriteLineAsync($"Provider: {config.DatabaseProvider}");
                await writer.WriteLineAsync($"Backup Time: {DateTime.UtcNow:O}");
                await writer.WriteLineAsync($"Database Dump Included: {createdDbDump}");
                await writer.WriteLineAsync();
                await writer.WriteLineAsync("Restore: Run 'dotnetcloud restore <file>' or use your database provider's restore tool.");
            }
            fileCount++;

            // Report result
            var fileInfo = new FileInfo(backupPath);
            fileInfo.Refresh();

            ConsoleOutput.WriteSuccess($"Backup created: {backupPath}");
            ConsoleOutput.WriteSuccess($"  Files archived: {fileCount}");
            ConsoleOutput.WriteSuccess($"  Archive size: {FormatSize(fileInfo.Length)}");

            if (manifestPath is not null)
            {
                ConsoleOutput.WriteSuccess("  Storage manifest: manifest/storage-manifest.tsv (inside the archive)");
                ConsoleOutput.WriteInfo($"  Standalone copy: {manifestPath}");
            }

            if (createdDbDump)
            {
                ConsoleOutput.WriteSuccess("Database dump included in archive.");
            }
            else if (includeDbDump)
            {
                ConsoleOutput.WriteWarning("Database dump was requested but could not be created.");
                ConsoleOutput.WriteInfo("  Ensure the appropriate tool is installed:");
                ConsoleOutput.WriteInfo($"    {GetDumpToolInstallHint(config.DatabaseProvider)}");
            }
            else
            {
                ConsoleOutput.WriteInfo("For a complete backup, add --db-dump or use your provider's native tool:");
                ConsoleOutput.WriteInfo($"    {GetDumpToolHint(config.DatabaseProvider)}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Backup failed: {ex.Message}");
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
            var url = $"{serverUrl.TrimEnd('/')}/api/v1/core/admin/backup/run";
            if (!string.IsNullOrWhiteSpace(outputPath))
                url += $"?outputPath={Uri.EscapeDataString(outputPath)}";

            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            var response = await client.PostAsync(url, null);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                ConsoleOutput.WriteError($"Server backup failed (HTTP {(int)response.StatusCode}): {body}");
                return 1;
            }

            ConsoleOutput.WriteSuccess("Server backup triggered successfully.");
            ConsoleOutput.WriteInfo("Check the admin UI or server logs for progress.");
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Failed to connect to server: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RestoreBackupAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            ConsoleOutput.WriteError($"Backup file not found: {filePath}");
            return 1;
        }

        ConsoleOutput.WriteHeader("DotNetCloud Restore");
        ConsoleOutput.WriteWarning("This will overwrite existing configuration and data files.");

        if (!ConsoleOutput.PromptConfirm("Continue with restore?"))
        {
            ConsoleOutput.WriteInfo("Restore cancelled.");
            return 0;
        }

        try
        {
            using var archive = ZipFile.OpenRead(filePath);

            // Check if archive has a database dump
            var hasDbDump = archive.Entries.Any(e => e.FullName == "database.sql");

            // Restore configuration
            var configEntry = archive.GetEntry("config/config.json");
            if (configEntry is not null)
            {
                var configPath = CliConfiguration.GetConfigFilePath();
                Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
                configEntry.ExtractToFile(configPath, overwrite: true);
                ConsoleOutput.WriteSuccess("Configuration restored.");
            }

            // Restore data files
            var config = CliConfiguration.Load();
            var dataEntries = archive.Entries.Where(e => e.FullName.StartsWith("data/")).ToList();
            if (dataEntries.Count > 0)
            {
                var resolvedDataDir = Path.GetFullPath(config.DataDirectory);
                foreach (var entry in dataEntries)
                {
                    var relativePath = entry.FullName["data/".Length..];
                    if (string.IsNullOrEmpty(relativePath))
                        continue;

                    var targetPath = Path.GetFullPath(Path.Combine(config.DataDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));

                    // Prevent Zip Slip: ensure the resolved path is within the data directory
                    if (!targetPath.StartsWith(resolvedDataDir, StringComparison.Ordinal))
                    {
                        ConsoleOutput.WriteWarning($"Skipping entry '{entry.FullName}' — path traversal detected.");
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                    entry.ExtractToFile(targetPath, overwrite: true);
                }
                ConsoleOutput.WriteSuccess($"Data directory restored ({dataEntries.Count} files).");
            }

            // Offer database restore if dump exists
            if (hasDbDump && ConsoleOutput.PromptConfirm("A database dump was found in this backup. Restore database now?"))
            {
                var dumpEntry = archive.GetEntry("database.sql")!;
                var tempDumpPath = Path.Combine(Path.GetTempPath(), $"dotnetcloud-restore-{Guid.CreateVersion7():N}.sql");

                try
                {
                    dumpEntry.ExtractToFile(tempDumpPath, overwrite: true);
                    ConsoleOutput.WriteInfo("Restoring database...");
                    ConsoleOutput.WriteInfo($"Dump file extracted to: {tempDumpPath}");
                    ConsoleOutput.WriteInfo("Use your database provider's restore tool (psql, sqlcmd) to apply it.");
                }
                finally
                {
                    if (File.Exists(tempDumpPath))
                        try
                        { File.Delete(tempDumpPath); }
                        catch { /* best effort */ }
                }
            }

            ConsoleOutput.WriteSuccess("Restore complete.");
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Restore failed: {ex.Message}");
            return 1;
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
        var (tool, argList) = provider.ToUpperInvariant() switch
        {
            "POSTGRESQL" => ("pg_dump", BuildPgDumpArgs(connectionString, outputPath)),
            "SQLSERVER" => ("sqlcmd", new List<string> { "-Q", $"BACKUP DATABASE [dotnetcloud] TO DISK = '{outputPath}'" }),
            _ => throw new NotSupportedException($"Database provider '{provider}' is not supported for automated dump.")
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

    private static List<string> BuildPgDumpArgs(string? connectionString, string outputPath)
    {
        var (host, port, db, user) = ParseConnectionString(connectionString);
        return new List<string> { "-h", host, "-p", port, "-U", user, "-d", db, "-Fp", outputPath };
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

    private static string GetDumpToolInstallHint(string provider) => provider.ToUpperInvariant() switch
    {
        "POSTGRESQL" => "apt install postgresql-client or yum install postgresql",
        "SQLSERVER" => "Install SQL Server Command Line Utilities (sqlcmd)",
        _ => $"Install the {provider} command-line client",
    };

    private static string GetDumpToolHint(string provider) => provider.ToUpperInvariant() switch
    {
        "POSTGRESQL" => "pg_dump dotnetcloud > dotnetcloud-db.sql",
        "SQLSERVER" => "BACKUP DATABASE [dotnetcloud] TO DISK = 'dotnetcloud-db.bak'",
        _ => "Use your database provider's backup tool",
    };

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
        _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB",
    };
}
