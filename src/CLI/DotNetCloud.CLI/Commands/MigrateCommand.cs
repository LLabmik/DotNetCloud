using System.CommandLine;

namespace DotNetCloud.CLI.Commands;

/// <summary>
/// Applies pending database migrations for the core schema and all installed
/// modules. Equivalent to <c>dotnetcloud setup --migrate-only</c> but with a
/// clearer command name for day-to-day admin use.
/// </summary>
internal static class MigrateCommand
{
    public static Command Create()
    {
        var command = new Command("migrate",
            "Apply any pending database schema changes before starting the service. " +
            "Run this after upgrading DotNetCloud to a new version, after pulling new code, " +
            "or whenever you want to confirm the database is up to date. " +
            "This is optional — the server applies migrations automatically on startup if you skip it. " +
            "Running it first avoids a slow first request while the database is altered.");

        command.SetAction(_ => SetupCommand.RunMigrateOnlyAsync());

        return command;
    }
}
