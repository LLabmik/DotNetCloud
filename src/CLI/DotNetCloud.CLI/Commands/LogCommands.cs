using System.CommandLine;
using DotNetCloud.CLI.Infrastructure;

namespace DotNetCloud.CLI.Commands;

/// <summary>
/// Log viewing commands: view system logs, module logs, filter by level.
/// </summary>
internal static class LogCommands
{
    /// <summary>
    /// Creates the <c>logs</c> command with optional module argument and level filter.
    /// </summary>
    public static Command Create()
    {
        var moduleArg = new Argument<string?>("module")
        {
            Description = "Module ID to filter logs (optional)",
            Arity = ArgumentArity.ZeroOrOne
        };

        var levelOption = new Option<string?>("--level")
        {
            Description = "Filter by log level (Debug, Information, Warning, Error, Fatal)"
        };
        var tailOption = new Option<int>("--tail")
        {
            Description = "Number of lines to show from the end",
            DefaultValueFactory = _ => 50
        };
        var followOption = new Option<bool>("--follow")
        {
            Description = "Follow log output in real-time (Ctrl+C to stop)",
            DefaultValueFactory = _ => false
        };
        followOption.Aliases.Add("-f");

        var command = new Command("logs", "View DotNetCloud logs")
        {
            moduleArg,
            levelOption,
            tailOption,
            followOption
        };

        command.SetAction(parseResult =>
        {
            var module = parseResult.GetValue(moduleArg);
            var level = parseResult.GetValue(levelOption);
            var tail = parseResult.GetValue(tailOption);
            var follow = parseResult.GetValue(followOption);
            return ViewLogsAsync(module, level, tail, follow);
        });

        return command;
    }

    private static async Task<int> ViewLogsAsync(string? module, string? level, int tail, bool follow)
    {
        if (!CliConfiguration.ConfigExists())
        {
            ConsoleOutput.WriteError("DotNetCloud is not configured. Run 'dotnetcloud setup' first.");
            return 1;
        }

        var config = CliConfiguration.Load();
        var logDir = config.LogDirectory;

        if (!Directory.Exists(logDir))
        {
            ConsoleOutput.WriteWarning($"Log directory not found: {logDir}");
            ConsoleOutput.WriteInfo("Logs are created when the server runs. Start with 'dotnetcloud serve'.");
            return 0;
        }

        // Find log files
        var logFiles = FindLogFiles(logDir, module);
        if (logFiles.Length == 0)
        {
            ConsoleOutput.WriteInfo("No log files found.");
            if (module is not null)
            {
                ConsoleOutput.WriteInfo($"No logs found for module '{module}'. Try without specifying a module.");
            }
            return 0;
        }

        // Use the most recent log file
        var logFile = logFiles
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .First();

        ConsoleOutput.WriteInfo($"Reading: {logFile.Name}");
        Console.WriteLine();

        if (follow)
        {
            return await FollowLogFileAsync(logFile.FullName, level);
        }

        return TailLogFile(logFile.FullName, level, tail);
    }

    private static int TailLogFile(string filePath, string? level, int lineCount)
    {
        try
        {
            var lines = File.ReadLines(filePath).ToList();
            var filtered = FilterByLevel(lines, level);
            var toShow = filtered.TakeLast(lineCount);

            foreach (var line in toShow)
            {
                WriteColoredLogLine(line);
            }

            return 0;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Failed to read log file: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> FollowLogFileAsync(string filePath, string? level)
    {
        ConsoleOutput.WriteInfo("Following log output. Press Ctrl+C to stop.");
        Console.WriteLine();

        try
        {
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            stream.Seek(0, SeekOrigin.End);

            using var reader = new StreamReader(stream);

            while (!cts.Token.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cts.Token);
                if (line is not null)
                {
                    if (MatchesLevel(line, level))
                    {
                        WriteColoredLogLine(line);
                    }
                }
                else
                {
                    await Task.Delay(250, cts.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on Ctrl+C
        }

        Console.WriteLine();
        ConsoleOutput.WriteInfo("Log following stopped.");
        return 0;
    }

    private static FileInfo[] FindLogFiles(string logDir, string? module)
    {
        var searchPattern = module is not null ? $"*{module}*.log" : "*.log";
        var dir = new DirectoryInfo(logDir);

        var logFiles = dir.GetFiles(searchPattern);

        // If no module-specific files found, also try .txt extension
        if (logFiles.Length == 0)
        {
            searchPattern = module is not null ? $"*{module}*.txt" : "*.txt";
            logFiles = dir.GetFiles(searchPattern);
        }

        // Also check for Serilog-style dated log files
        if (logFiles.Length == 0)
        {
            logFiles = dir.GetFiles("log-*.txt");
        }

        return logFiles;
    }

    private static List<string> FilterByLevel(List<string> lines, string? level)
    {
        if (string.IsNullOrWhiteSpace(level))
        {
            return lines;
        }

        return lines.Where(l => MatchesLevel(l, level)).ToList();
    }

    private static bool MatchesLevel(string line, string? level)
    {
        if (string.IsNullOrWhiteSpace(level))
        {
            return true;
        }

        var upper = level.ToUpperInvariant();
        // Serilog uses abbreviations like [INF], [WRN], [ERR], [FTL], [DBG], [VRB]
        return upper switch
        {
            "DEBUG" or "DBG" => line.Contains("[DBG]", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Debug", StringComparison.OrdinalIgnoreCase),
            "INFORMATION" or "INFO" or "INF" => line.Contains("[INF]", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Information", StringComparison.OrdinalIgnoreCase),
            "WARNING" or "WARN" or "WRN" => line.Contains("[WRN]", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Warning", StringComparison.OrdinalIgnoreCase),
            "ERROR" or "ERR" => line.Contains("[ERR]", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Error", StringComparison.OrdinalIgnoreCase),
            "FATAL" or "FTL" => line.Contains("[FTL]", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Fatal", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    private static void WriteColoredLogLine(string line)
    {
        if (line.Contains("[ERR]") || line.Contains("Error"))
        {
            Console.ForegroundColor = ConsoleColor.Red;
        }
        else if (line.Contains("[WRN]") || line.Contains("Warning"))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
        }
        else if (line.Contains("[FTL]") || line.Contains("Fatal"))
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
        }
        else if (line.Contains("[DBG]") || line.Contains("Debug"))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
        }
        else
        {
            Console.ResetColor();
        }

        Console.WriteLine(line);
        Console.ResetColor();
    }
}
