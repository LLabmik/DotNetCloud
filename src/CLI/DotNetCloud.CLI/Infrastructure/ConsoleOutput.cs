namespace DotNetCloud.CLI.Infrastructure;

/// <summary>
/// Provides formatted console output helpers for the CLI tool.
/// </summary>
internal static class ConsoleOutput
{
    /// <summary>
    /// Writes a header banner to the console.
    /// </summary>
    public static void WriteHeader(string title)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine();
        Console.WriteLine($"  ╔══════════════════════════════════════════╗");
        Console.WriteLine($"  ║  {title,-40}║");
        Console.WriteLine($"  ╚══════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
    }

    /// <summary>
    /// Writes a success message to the console.
    /// </summary>
    public static void WriteSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("  ✓ ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    /// <summary>
    /// Writes an error message to the console.
    /// </summary>
    public static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("  ✗ ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    /// <summary>
    /// Writes a warning message to the console.
    /// </summary>
    public static void WriteWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("  ⚠ ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    /// <summary>
    /// Writes an informational message to the console.
    /// </summary>
    public static void WriteInfo(string message)
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.Write("  ℹ ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    /// <summary>
    /// Writes a step indicator during setup/wizard flows.
    /// </summary>
    public static void WriteStep(int current, int total, string description)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write($"  [{current}/{total}] ");
        Console.ResetColor();
        Console.WriteLine(description);
    }

    /// <summary>
    /// Writes a simple table to the console.
    /// </summary>
    public static void WriteTable(string[] headers, List<string[]> rows)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);

        // Calculate column widths
        var widths = new int[headers.Length];
        for (var i = 0; i < headers.Length; i++)
        {
            widths[i] = headers[i].Length;
        }

        foreach (var row in rows)
        {
            for (var i = 0; i < Math.Min(row.Length, headers.Length); i++)
            {
                widths[i] = Math.Max(widths[i], (row[i] ?? "").Length);
            }
        }

        // Print header
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("  ");
        for (var i = 0; i < headers.Length; i++)
        {
            Console.Write(headers[i].PadRight(widths[i] + 2));
        }
        Console.WriteLine();

        // Print separator
        Console.Write("  ");
        for (var i = 0; i < headers.Length; i++)
        {
            Console.Write(new string('─', widths[i] + 2));
        }
        Console.WriteLine();
        Console.ResetColor();

        // Print rows
        foreach (var row in rows)
        {
            Console.Write("  ");
            for (var i = 0; i < headers.Length; i++)
            {
                var value = i < row.Length ? row[i] ?? "" : "";
                Console.Write(value.PadRight(widths[i] + 2));
            }
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Writes a key-value pair detail line.
    /// </summary>
    public static void WriteDetail(string label, string value)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"  {label}: ");
        Console.ResetColor();
        Console.WriteLine(value);
    }

    /// <summary>
    /// Writes a status indicator with color based on status text.
    /// </summary>
    public static string FormatStatus(string status)
    {
        return status.ToUpperInvariant() switch
        {
            "RUNNING" or "ENABLED" or "HEALTHY" or "STARTED" => $"\u001b[32m●\u001b[0m {status}",
            "STOPPED" or "DISABLED" => $"\u001b[90m○\u001b[0m {status}",
            "DEGRADED" or "WARNING" or "WAITINGFORRESTART" => $"\u001b[33m●\u001b[0m {status}",
            "FAILED" or "CRASHED" or "UNHEALTHY" or "ERROR" => $"\u001b[31m●\u001b[0m {status}",
            "STARTING" or "STOPPING" => $"\u001b[36m◐\u001b[0m {status}",
            _ => $"  {status}"
        };
    }

    /// <summary>
    /// Prompts the user for input and returns the entered value.
    /// </summary>
    public static string Prompt(string message, string? defaultValue = null)
    {
        if (defaultValue is not null)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"  {message} [{defaultValue}]: ");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"  {message}: ");
        }
        Console.ResetColor();

        var input = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(input) ? defaultValue ?? string.Empty : input;
    }

    /// <summary>
    /// Prompts the user for a password (input is masked).
    /// </summary>
    public static string PromptPassword(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"  {message}: ");
        Console.ResetColor();

        var password = string.Empty;
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }
            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password = password[..^1];
                Console.Write("\b \b");
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password += key.KeyChar;
                Console.Write('*');
            }
        }

        return password;
    }

    /// <summary>
    /// Prompts the user for a yes/no confirmation.
    /// </summary>
    public static bool PromptConfirm(string message, bool defaultValue = false)
    {
        var hint = defaultValue ? "[Y/n]" : "[y/N]";
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"  {message} {hint}: ");
        Console.ResetColor();

        var input = Console.ReadLine()?.Trim().ToUpperInvariant();
        return input switch
        {
            "Y" or "YES" => true,
            "N" or "NO" => false,
            _ => defaultValue
        };
    }

    /// <summary>
    /// Prompts the user to select from a list of options.
    /// </summary>
    public static int PromptChoice(string message, string[] options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  {message}");
        Console.ResetColor();

        for (var i = 0; i < options.Length; i++)
        {
            Console.WriteLine($"    {i + 1}. {options[i]}");
        }

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("  Choice: ");
            Console.ResetColor();

            if (int.TryParse(Console.ReadLine()?.Trim(), out var choice) && choice >= 1 && choice <= options.Length)
            {
                return choice - 1;
            }

            WriteError($"Please enter a number between 1 and {options.Length}.");
        }
    }
}
