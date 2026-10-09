using System.CommandLine;
using DotNetCloud.CLI.Commands;
using DotNetCloud.CLI.Infrastructure;

public static class Program
{
    public static int Main(string[] args)
    {
        // Resolve an explicit --config-dir first: the configuration directory is captured from the
        // environment the first time any configuration type is touched, and the sudo re-exec below
        // reads it as well. The option is then removed from the arguments, so it is accepted in any
        // position (including nested subcommands) — System.CommandLine does not inherit root options.
        if (CliArguments.TryGetConfigDirArgument(args, out var configDir))
        {
            Environment.SetEnvironmentVariable("DOTNETCLOUD_CONFIG_DIR", configDir);
            args = CliArguments.RemoveConfigDirArgument(args);
        }

        // On Linux, re-execute under sudo if not already root - but only for commands
        // that need to write to system directories. Read-only commands (--help, --version,
        // status, logs) should work without elevation, and so should a help request for a
        // command that otherwise needs root (e.g. `backup --help`).
        var readOnlyCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "--help", "-h", "-?", "--version", "status", "logs"
        };

        var commandName = CliArguments.FindCommandName(args);
        var needsRoot = commandName is not null
            && !readOnlyCommands.Contains(commandName)
            && !CliArguments.IsHelpOrVersionRequest(args);

        if (needsRoot)
        {
            var sudoResult = SudoHelper.ReExecWithSudo(args);
            if (sudoResult.HasValue)
            {
                return sudoResult.Value;
            }
        }

        var rootCommand = new RootCommand("DotNetCloud - self-hosted cloud platform management CLI");

        // Global option: which configuration directory this invocation operates on. The value is applied
        // to the environment in Main (see above) before any configuration is resolved.
        var configDirOption = new Option<string?>("--config-dir")
        {
            Description = "Configuration directory to operate on (overrides DOTNETCLOUD_CONFIG_DIR and the /etc/dotnetcloud default)"
        };
        rootCommand.Options.Add(configDirOption);

        // Setup wizard
        rootCommand.Subcommands.Add(SetupCommand.Create());

        // Database migrations
        rootCommand.Subcommands.Add(MigrateCommand.Create());

        // Service lifecycle
        rootCommand.Subcommands.Add(ServiceCommands.CreateStart());
        rootCommand.Subcommands.Add(ServiceCommands.CreateStop());
        rootCommand.Subcommands.Add(ServiceCommands.CreateStatus());
        rootCommand.Subcommands.Add(ServiceCommands.CreateRestart());

        // Module management
        rootCommand.Subcommands.Add(ModuleCommands.Create());

        // Component management
        rootCommand.Subcommands.Add(ComponentCommands.Create());

        // Collabora CODE installation
        rootCommand.Subcommands.Add(CollaboraInstallCommand.Create());

        // Log viewing
        rootCommand.Subcommands.Add(LogCommands.Create());

        // Backup & restore
        rootCommand.Subcommands.Add(BackupCommands.Create());

        // Certificate management
        rootCommand.Subcommands.Add(CertRenewCommand.Create());

        // Miscellaneous
        rootCommand.Subcommands.Add(MiscCommands.CreateUpdate());
        rootCommand.Subcommands.Add(MiscCommands.CreateVersion());

        return rootCommand.Parse(args).Invoke();
    }
}
