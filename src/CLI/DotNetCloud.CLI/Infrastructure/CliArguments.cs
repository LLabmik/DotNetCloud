namespace DotNetCloud.CLI.Infrastructure;

/// <summary>
/// Raw command-line inspection that must happen before the argument parser runs.
/// </summary>
/// <remarks>
/// The configuration directory is captured from the environment the first time a configuration type is
/// touched, and the sudo re-exec in <c>Program.Main</c> happens before parsing — so both
/// <see cref="TryGetConfigDirArgument"/> and <see cref="FindCommandName"/> work directly on the raw
/// argument array.
/// </remarks>
internal static class CliArguments
{
    private const string ConfigDirOption = "--config-dir";

    private static readonly string[] HelpAndVersionOptions = ["--help", "-h", "-?", "--version"];

    /// <summary>
    /// Extracts an explicit <c>--config-dir</c> value from the raw command line. Supports both
    /// <c>--config-dir &lt;path&gt;</c> and <c>--config-dir=&lt;path&gt;</c>.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="configDir">The requested directory when present.</param>
    /// <returns><see langword="true"/> when a non-empty directory was requested.</returns>
    internal static bool TryGetConfigDirArgument(string[] args, out string configDir)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals(ConfigDirOption, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                {
                    configDir = args[i + 1];
                    return true;
                }
            }
            else if (arg.StartsWith(ConfigDirOption + "=", StringComparison.OrdinalIgnoreCase))
            {
                configDir = arg[(ConfigDirOption.Length + 1)..];
                return !string.IsNullOrWhiteSpace(configDir);
            }
        }

        configDir = string.Empty;
        return false;
    }

    /// <summary>
    /// Finds the command name: the first token that is neither an option nor the value of
    /// <c>--config-dir</c>. Used to decide whether a command needs root, so that a leading global option
    /// does not force elevation for read-only commands.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <returns>The command name, or <see langword="null"/> when the invocation has none.</returns>
    internal static string? FindCommandName(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals(ConfigDirOption, StringComparison.OrdinalIgnoreCase))
            {
                i++; // skip the option's value
                continue;
            }

            if (arg.StartsWith('-'))
                continue;

            return arg;
        }

        return null;
    }

    /// <summary>
    /// Whether the invocation is a help or version request.
    /// </summary>
    /// <remarks>
    /// These print usage information only, so they must never trigger the sudo prompt —
    /// <c>dotnetcloud backup --help</c> is a read-only request even though <c>backup</c> itself needs root.
    /// </remarks>
    /// <param name="args">Raw command-line arguments.</param>
    internal static bool IsHelpOrVersionRequest(string[] args)
    {
        return args.Any(arg => HelpAndVersionOptions.Contains(arg, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Removes <c>--config-dir</c> (and its value) from the raw command line.
    /// </summary>
    /// <remarks>
    /// The option is applied to the environment before parsing, but it is removed from the parsed
    /// arguments because System.CommandLine does not inherit root options into subcommands — removing it
    /// keeps <c>dotnetcloud status --config-dir /tmp/x</c> and nested invocations working instead of
    /// failing with "Unrecognized command or argument". A trailing <c>--config-dir</c> with no value is
    /// left in place so the parser reports the missing argument.
    /// </remarks>
    /// <param name="args">Raw command-line arguments.</param>
    /// <returns>The arguments without the configuration-directory option.</returns>
    internal static string[] RemoveConfigDirArgument(string[] args)
    {
        var remaining = new List<string>(args.Length);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg.Equals(ConfigDirOption, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                i++; // drop the value as well
                continue;
            }

            if (arg.StartsWith(ConfigDirOption + "=", StringComparison.OrdinalIgnoreCase))
                continue;

            remaining.Add(arg);
        }

        return [.. remaining];
    }
}
