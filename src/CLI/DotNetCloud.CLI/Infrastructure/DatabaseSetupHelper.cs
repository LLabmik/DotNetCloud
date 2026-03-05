using System.Diagnostics;
using System.Runtime.InteropServices;

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
        // Create the role (ignore error if it already exists)
        var createUser = RunSudoPostgres(
            $"-c \"CREATE ROLE {dbUser} WITH LOGIN PASSWORD '{EscapeSql(dbPassword)}';\"");

        if (!createUser)
        {
            // Role might already exist — try ALTER instead
            RunSudoPostgres(
                $"-c \"ALTER ROLE {dbUser} WITH LOGIN PASSWORD '{EscapeSql(dbPassword)}';\"");
        }

        // Create the database (ignore error if it already exists)
        var createDb = RunSudoPostgres(
            $"-c \"CREATE DATABASE {dbName} OWNER {dbUser};\"");

        if (!createDb)
        {
            // Database might already exist — try changing owner
            RunSudoPostgres(
                $"-c \"ALTER DATABASE {dbName} OWNER TO {dbUser};\"");
        }

        // Verify we can connect
        var verify = RunCommand("psql",
            $"-h localhost -U {dbUser} -d {dbName} -c \"SELECT 1;\"",
            new Dictionary<string, string> { ["PGPASSWORD"] = dbPassword });

        return verify;
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
            return $"Server={server};Database={database};Trusted_Connection=True;TrustServerCertificate=True";
        }

        return $"Server={server};Database={database};User Id={username};Password={password};TrustServerCertificate=True";
    }

    /// <summary>
    /// Builds a MariaDB connection string from individual parts.
    /// </summary>
    public static string BuildMariaDbConnectionString(
        string server, string database, string username, string password)
    {
        return $"Server={server};Database={database};User={username};Password={password}";
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
