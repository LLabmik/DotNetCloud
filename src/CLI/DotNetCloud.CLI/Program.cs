using System.CommandLine;
using DotNetCloud.CLI.Commands;
using DotNetCloud.CLI.Infrastructure;

// On Linux, re-execute under sudo if not already root — but only for commands
// that need to write to system directories. Read-only commands (--help, --version,
// status, logs) should work without elevation.
var readOnlyCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "--help", "-h", "-?", "--version", "status", "logs"
};

var firstArg = args.Length > 0 ? args[0] : null;
var needsRoot = firstArg is not null && !readOnlyCommands.Contains(firstArg);

if (needsRoot)
{
    var sudoResult = SudoHelper.ReExecWithSudo(args);
    if (sudoResult.HasValue)
    {
        return sudoResult.Value;
    }
}

var rootCommand = new RootCommand("DotNetCloud — self-hosted cloud platform management CLI");

// Setup wizard
rootCommand.Subcommands.Add(SetupCommand.Create());

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

// Miscellaneous
rootCommand.Subcommands.Add(MiscCommands.CreateUpdate());
rootCommand.Subcommands.Add(MiscCommands.CreateVersion());

return rootCommand.Parse(args).Invoke();
