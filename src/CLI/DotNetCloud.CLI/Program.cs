using System.CommandLine;
using DotNetCloud.CLI.Commands;

var rootCommand = new RootCommand("DotNetCloud — self-hosted cloud platform management CLI");

// Setup wizard
rootCommand.Subcommands.Add(SetupCommand.Create());

// Service lifecycle
rootCommand.Subcommands.Add(ServiceCommands.CreateServe());
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
