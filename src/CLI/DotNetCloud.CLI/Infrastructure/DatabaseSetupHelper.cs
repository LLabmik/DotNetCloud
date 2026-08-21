using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace DotNetCloud.CLI.Infrastructure;

/// <summary>
/// Detects whether a database engine is installed and offers to install
/// and configure PostgreSQL automatically on Debian-based Linux.
/// </summary>
internal static class DatabaseSetupHelper
{
    /// <summary>
    /// Returns <c>true</c> if the <c>psql</c> command is available.
    /// </summary>
    public static bool IsPostgreSqlInstalled()
    {
        return IsCommandAvailable("psql");
    }

    /// <summary>
    /// Installs PostgreSQL via apt-get and starts the service.
    /// </summary>
    /// <returns><c>true</c> if installation succeeded.</returns>
    public static bool InstallPostgreSql()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return false;
        }

        ConsoleOutput.WriteInfo("Updating package list...");
        if (!RunCommand("apt-get", "update -qq"))
        {
            ConsoleOutput.WriteError("Failed to update package list.");
            return false;
        }

        ConsoleOutput.WriteInfo("Installing PostgreSQL...");
        if (!RunCommand("apt-get", "install -y -qq postgresql"))
        {
            ConsoleOutput.WriteError("Failed to install PostgreSQL.");
            return false;
        }

        // Ensure the service is running
        RunCommand("systemctl", "enable --now postgresql");

        ConsoleOutput.WriteSuccess("PostgreSQL installed and running.");
        return true;
    }

    /// <summary>
    /// Creates a PostgreSQL user and database using <c>sudo -u postgres</c>.
    /// </summary>
    /// <returns><c>true</c> if the user and database were created successfully.</returns>
    public static bool CreatePostgreSqlDatabase(string dbName, string dbUser, string dbPassword)
    {
        // Never interpolate user-supplied identifiers into SQL directly. Validate
        // and quote them so a name like "dotnet-cloud" or one containing a quote
        // cannot break the statement or inject SQL.
        if (!IsValidPostgreSqlIdentifier(dbName) || !IsValidPostgreSqlIdentifier(dbUser))
        {
            ConsoleOutput.WriteError(
                "Database or user name contains invalid characters. Use only letters, digits, and underscores.");
            return false;
        }

        var quotedUser = QuoteIdentifier(dbUser);
        var quotedDb = QuoteIdentifier(dbName);

        // Create the role (ignore error if it already exists). CREATEDB lets
        // the server recreate the database at startup if it was dropped (e.g.
        // by an uninstall that opted to drop the database).
        var createUser = RunSudoPostgres(
            $"-c \"CREATE ROLE {quotedUser} WITH LOGIN PASSWORD '{EscapeSql(dbPassword)}' CREATEDB;\"");

        if (!createUser)
        {
            // Role might already exist — try ALTER instead
            RunSudoPostgres(
                $"-c \"ALTER ROLE {quotedUser} WITH LOGIN PASSWORD '{EscapeSql(dbPassword)}' CREATEDB;\"");
        }

        // Create the database (ignore error if it already exists)
        var createDb = RunSudoPostgres(
            $"-c \"CREATE DATABASE {quotedDb} OWNER {quotedUser};\"");

        if (!createDb)
        {
            // Database might already exist — try changing owner
            RunSudoPostgres(
                $"-c \"ALTER DATABASE {quotedDb} OWNER TO {quotedUser};\"");
        }

        // Verify we can connect
        var verify = RunCommand("psql",
            $"-h localhost -U {dbUser} -d {dbName} -c \"SELECT 1;\"",
            new Dictionary<string, string> { ["PGPASSWORD"] = dbPassword });

        return verify;
    }

    /// <summary>
    /// Validates a PostgreSQL identifier (role or database name): must start
    /// with a letter or underscore, followed by letters, digits, or underscores.
    /// </summary>
    internal static bool IsValidPostgreSqlIdentifier(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && Regex.IsMatch(value, "^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.None, TimeSpan.FromSeconds(1));
    }

    private static string QuoteIdentifier(string value)
    {
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    /// <summary>
    /// Builds a PostgreSQL connection string from individual parts.
    /// </summary>
    public static string BuildPostgreSqlConnectionString(
        string host, string database, string username, string password)
    {
        return $"Host={host};Database={database};Username={username};Password={password}";
    }

    /// <summary>
    /// Builds a SQL Server connection string from individual parts.
    /// </summary>
    public static string BuildSqlServerConnectionString(
        string server, string database, string? username, string? password, bool trustedConnection)
    {
        if (trustedConnection)
        {
            return $"Server={server};Database={database};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
        }

        return $"Server={server};Database={database};User Id={username};Password={password};TrustServerCertificate=True;MultipleActiveResultSets=True";
    }

    private static bool RunSudoPostgres(string psqlArgs)
    {
        return RunCommand("sudo", $"-u postgres psql {psqlArgs}");
    }

    private static string EscapeSql(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("which", command)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool RunCommand(string fileName, string arguments,
        Dictionary<string, string>? env = null)
    {
        try
        {
            var psi = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            if (env is not null)
            {
                foreach (var (key, value) in env)
                {
                    psi.Environment[key] = value;
                }
            }

            using var process = Process.Start(psi);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
