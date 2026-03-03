using System.CommandLine;
using System.IO.Compression;
using DotNetCloud.CLI.Infrastructure;

namespace DotNetCloud.CLI.Commands;

/// <summary>
/// Backup and restore commands: create backup, restore from backup, schedule backups.
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

        var command = new Command("backup", "Create a backup of DotNetCloud data");
        command.Options.Add(outputOption);
        command.Subcommands.Add(CreateRestore());
        command.Subcommands.Add(CreateSchedule());

        command.SetAction(parseResult =>
        {
            var output = parseResult.GetValue(outputOption);
            return CreateBackupAsync(output);
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

    private static async Task<int> CreateBackupAsync(string? outputPath)
    {
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
            await using var zipStream = new FileStream(backupPath, FileMode.Create);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

            // Backup configuration
            var configPath = CliConfiguration.GetConfigFilePath();
            if (File.Exists(configPath))
            {
                archive.CreateEntryFromFile(configPath, "config/config.json");
                ConsoleOutput.WriteSuccess("Configuration backed up.");
            }

            // Backup data directory
            var dataDir = config.DataDirectory;
            if (Directory.Exists(dataDir))
            {
                var dataFiles = Directory.GetFiles(dataDir, "*", SearchOption.AllDirectories);
                foreach (var file in dataFiles)
                {
                    var entryName = "data/" + Path.GetRelativePath(dataDir, file).Replace('\\', '/');
                    archive.CreateEntryFromFile(file, entryName);
                }
                ConsoleOutput.WriteSuccess($"Data directory backed up ({dataFiles.Length} files).");
            }

            // Backup database info (connection string metadata, not actual DB dump)
            var dbInfoEntry = archive.CreateEntry("config/database-info.txt");
            await using (var writer = new StreamWriter(dbInfoEntry.Open()))
            {
                await writer.WriteLineAsync($"Provider: {config.DatabaseProvider}");
                await writer.WriteLineAsync($"Backup Time: {DateTime.UtcNow:O}");
                await writer.WriteLineAsync();
                await writer.WriteLineAsync("Note: This backup does not include a database dump.");
                await writer.WriteLineAsync("Use your database provider's backup tools (pg_dump, mysqldump, etc.) for a full database backup.");
            }

            ConsoleOutput.WriteSuccess($"Backup created: {backupPath}");
            ConsoleOutput.WriteInfo("For a complete backup, also run your database provider's backup tool:");

            switch (config.DatabaseProvider.ToUpperInvariant())
            {
                case "POSTGRESQL":
                    ConsoleOutput.WriteInfo("  pg_dump dotnetcloud > dotnetcloud-db.sql");
                    break;
                case "SQLSERVER":
                    ConsoleOutput.WriteInfo("  BACKUP DATABASE [dotnetcloud] TO DISK = 'dotnetcloud-db.bak'");
                    break;
                case "MARIADB":
                    ConsoleOutput.WriteInfo("  mysqldump dotnetcloud > dotnetcloud-db.sql");
                    break;
            }

            return 0;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Backup failed: {ex.Message}");
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
                foreach (var entry in dataEntries)
                {
                    var relativePath = entry.FullName["data/".Length..];
                    if (string.IsNullOrEmpty(relativePath)) continue;

                    var targetPath = Path.Combine(config.DataDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                    entry.ExtractToFile(targetPath, overwrite: true);
                }
                ConsoleOutput.WriteSuccess($"Data directory restored ({dataEntries.Count} files).");
            }

            ConsoleOutput.WriteSuccess("Restore complete.");
            ConsoleOutput.WriteInfo("Don't forget to restore your database separately using your provider's restore tool.");

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

        return Task.FromResult(0);
    }
}
