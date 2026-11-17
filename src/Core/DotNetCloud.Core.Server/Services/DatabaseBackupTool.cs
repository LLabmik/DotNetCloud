using System.Diagnostics;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Runs the provider-native tools used to dump and restore a DotNetCloud database.
/// </summary>
/// <remarks>
/// Kept behind an interface so backup/restore can be exercised without <c>pg_dump</c>,
/// <c>psql</c> or <c>sqlcmd</c> being installed on the machine running the tests.
/// </remarks>
public interface IDatabaseBackupTool
{
    /// <summary>
    /// Creates a database dump at <paramref name="outputPath"/>.
    /// </summary>
    /// <param name="provider">Database provider name (<c>PostgreSql</c> or <c>SqlServer</c>).</param>
    /// <param name="connectionString">Connection string used to reach the database.</param>
    /// <param name="outputPath">Full path of the dump file to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="NotSupportedException">The provider has no automated dump support.</exception>
    Task CreateDumpAsync(
        string provider,
        string? connectionString,
        string outputPath,
        CancellationToken cancellationToken);

    /// <summary>
    /// Restores a dump previously produced by <see cref="CreateDumpAsync"/>.
    /// </summary>
    /// <param name="provider">Database provider name (<c>PostgreSql</c> or <c>SqlServer</c>).</param>
    /// <param name="connectionString">Connection string used to reach the database.</param>
    /// <param name="dumpFilePath">Full path of the dump file to restore.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="NotSupportedException">The provider has no automated restore support.</exception>
    Task RestoreDumpAsync(
        string provider,
        string? connectionString,
        string dumpFilePath,
        CancellationToken cancellationToken);
}

/// <summary>
/// Default <see cref="IDatabaseBackupTool"/> implementation that shells out to
/// <c>pg_dump</c>/<c>psql</c> for PostgreSQL and <c>sqlcmd</c> for SQL Server.
/// </summary>
public sealed class DatabaseBackupTool : IDatabaseBackupTool
{
    private const string DefaultPostgreSqlHost = "localhost";
    private const string DefaultPostgreSqlPort = "5432";
    private const string DefaultPostgreSqlUser = "postgres";
    private const string DefaultSqlServerHost = "localhost";
    private const string DefaultDatabaseName = "dotnetcloud";

    private readonly ILogger<DatabaseBackupTool> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseBackupTool"/> class.
    /// </summary>
    /// <param name="logger">Logger for tool invocations.</param>
    public DatabaseBackupTool(ILogger<DatabaseBackupTool> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Returns the executable name used to dump the given provider.
    /// </summary>
    /// <param name="provider">Database provider name.</param>
    /// <returns>The tool executable name, or the provider name when it is not supported.</returns>
    public static string GetDumpToolName(string provider) => provider.ToUpperInvariant() switch
    {
        "POSTGRESQL" => "pg_dump",
        "SQLSERVER" => "sqlcmd",
        _ => provider,
    };

    /// <inheritdoc />
    public async Task CreateDumpAsync(
        string provider,
        string? connectionString,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var invocation = provider.ToUpperInvariant() switch
        {
            "POSTGRESQL" => BuildPostgreSqlDumpInvocation(connectionString, outputPath),
            "SQLSERVER" => BuildSqlServerDumpInvocation(connectionString, outputPath),
            _ => throw new NotSupportedException(
                $"Database provider '{provider}' is not supported for automated dump. Please use the provider's native backup tool."),
        };

        _logger.LogDebug("Creating database dump with {Tool}.", invocation.Tool);

        var psi = CreateStartInfo(invocation.Tool);
        foreach (var argument in invocation.Arguments)
            psi.ArgumentList.Add(argument);
        foreach (var (key, value) in invocation.Environment)
            psi.Environment[key] = value;

        using var process = new Process { StartInfo = psi };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var error = await errorTask;
            throw new InvalidOperationException(
                $"Database dump failed (exit code {process.ExitCode}): {error}");
        }

        // pg_dump writes to -f <path>; anything on stdout is only used when no file was produced.
        if (string.IsNullOrEmpty(await errorTask) && !File.Exists(outputPath))
        {
            var output = await outputTask;
            if (!string.IsNullOrEmpty(output))
                await File.WriteAllTextAsync(outputPath, output, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task RestoreDumpAsync(
        string provider,
        string? connectionString,
        string dumpFilePath,
        CancellationToken cancellationToken)
    {
        var invocation = provider.ToUpperInvariant() switch
        {
            "POSTGRESQL" => BuildPostgreSqlRestoreInvocation(connectionString, dumpFilePath),
            "SQLSERVER" => BuildSqlServerRestoreInvocation(connectionString, dumpFilePath),
            _ => throw new NotSupportedException(
                $"Database provider '{provider}' is not supported for automated restore."),
        };

        _logger.LogDebug("Restoring database dump with {Tool}.", invocation.Tool);

        var psi = CreateStartInfo(invocation.Tool);
        foreach (var argument in invocation.Arguments)
            psi.ArgumentList.Add(argument);
        foreach (var (key, value) in invocation.Environment)
            psi.Environment[key] = value;

        using var process = new Process { StartInfo = psi };
        process.Start();

        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var error = await errorTask;
            throw new InvalidOperationException(
                $"Database restore failed (exit code {process.ExitCode}): {error}");
        }
    }

    private static ProcessStartInfo CreateStartInfo(string tool) => new()
    {
        FileName = tool,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };

    private static ToolInvocation BuildPostgreSqlDumpInvocation(string? connectionString, string outputPath)
    {
        var (host, port, database, user) = ParsePostgreSqlConnectionString(connectionString);

        // -Fp selects the plain format; -f names the output file. Passing the path positionally
        // after -Fp makes pg_dump reject it as an extra command-line argument.
        var arguments = new List<string>
        {
            "-h", host,
            "-p", port,
            "-U", user,
            "-d", database,
            "-Fp",
            "-f", outputPath,
        };

        return new ToolInvocation("pg_dump", arguments, BuildPasswordEnvironment(connectionString, "PGPASSWORD"));
    }

    private static ToolInvocation BuildPostgreSqlRestoreInvocation(string? connectionString, string dumpFilePath)
    {
        var arguments = new List<string>();

        if (!string.IsNullOrEmpty(connectionString))
        {
            var (host, port, database, user) = ParsePostgreSqlConnectionString(connectionString);
            arguments.Add("-h");
            arguments.Add(host);
            arguments.Add("-p");
            arguments.Add(port);
            arguments.Add("-U");
            arguments.Add(user);
            arguments.Add("-d");
            arguments.Add(database);
        }

        // ON_ERROR_STOP makes psql exit non-zero on the first SQL error; without it a broken
        // archive would restore "successfully".
        arguments.Add("-v");
        arguments.Add("ON_ERROR_STOP=1");
        arguments.Add("-f");
        arguments.Add(dumpFilePath);

        return new ToolInvocation("psql", arguments, BuildPasswordEnvironment(connectionString, "PGPASSWORD"));
    }

    private static ToolInvocation BuildSqlServerDumpInvocation(string? connectionString, string outputPath)
    {
        var target = ParseSqlServerConnectionString(connectionString);
        return BuildSqlServerInvocation(
            target,
            $"BACKUP DATABASE [{target.Database}] TO DISK = '{outputPath}'");
    }

    private static ToolInvocation BuildSqlServerRestoreInvocation(string? connectionString, string dumpFilePath)
    {
        var target = ParseSqlServerConnectionString(connectionString);
        return BuildSqlServerInvocation(
            target,
            $"RESTORE DATABASE [{target.Database}] FROM DISK = '{dumpFilePath}'");
    }

    private static ToolInvocation BuildSqlServerInvocation(SqlServerTarget target, string script)
    {
        var arguments = new List<string> { "-S", target.Server, "-d", target.Database };

        if (!string.IsNullOrWhiteSpace(target.User))
        {
            arguments.Add("-U");
            arguments.Add(target.User);
        }

        if (target.TrustServerCertificate)
            arguments.Add("-C");

        // -b makes sqlcmd return a non-zero exit code when the script fails.
        arguments.Add("-b");
        arguments.Add("-Q");
        arguments.Add(script);

        // The password travels through the environment, never the command line, so it does not
        // show up in process listings.
        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(target.Password))
            environment["SQLCMDPASSWORD"] = target.Password;

        return new ToolInvocation("sqlcmd", arguments, environment);
    }

    /// <summary>
    /// Builds the credential environment for a CLI tool that cannot take the password as an argument.
    /// </summary>
    internal static Dictionary<string, string> BuildPasswordEnvironment(string? connectionString, string variableName)
    {
        var password = TryGetConnectionStringValue(connectionString, "password", "pwd");
        return string.IsNullOrEmpty(password)
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal) { [variableName] = password };
    }

    /// <summary>
    /// Builds the arguments passed to <c>pg_dump</c> for a dump.
    /// </summary>
    internal static List<string> BuildPostgreSqlDumpArguments(string? connectionString, string outputPath)
        => BuildPostgreSqlDumpInvocation(connectionString, outputPath).Arguments;

    /// <summary>
    /// Builds the arguments passed to <c>psql</c> for a restore.
    /// </summary>
    internal static List<string> BuildPostgreSqlRestoreArguments(string? connectionString, string dumpFilePath)
        => BuildPostgreSqlRestoreInvocation(connectionString, dumpFilePath).Arguments;

    /// <summary>
    /// Builds the arguments passed to <c>sqlcmd</c> for a dump.
    /// </summary>
    internal static List<string> BuildSqlServerDumpArguments(string? connectionString, string outputPath)
    {
        var target = ParseSqlServerConnectionString(connectionString);
        return BuildSqlServerInvocation(
            target,
            $"BACKUP DATABASE [{target.Database}] TO DISK = '{outputPath}'").Arguments;
    }

    /// <summary>
    /// Builds the arguments passed to <c>sqlcmd</c> for a restore.
    /// </summary>
    internal static List<string> BuildSqlServerRestoreArguments(string? connectionString, string dumpFilePath)
    {
        var target = ParseSqlServerConnectionString(connectionString);
        return BuildSqlServerInvocation(
            target,
            $"RESTORE DATABASE [{target.Database}] FROM DISK = '{dumpFilePath}'").Arguments;
    }

    /// <summary>
    /// Parses a PostgreSQL connection string into the values pg_dump/psql need.
    /// </summary>
    internal static (string Host, string Port, string Database, string User) ParsePostgreSqlConnectionString(
        string? connectionString)
    {
        var host = DefaultPostgreSqlHost;
        var port = DefaultPostgreSqlPort;
        var database = DefaultDatabaseName;
        var user = DefaultPostgreSqlUser;

        if (string.IsNullOrEmpty(connectionString))
            return (host, port, database, user);

        foreach (var (key, value) in EnumerateConnectionStringParts(connectionString))
        {
            switch (key)
            {
                case "host" or "server" or "datasource":
                    host = value;
                    break;
                case "port":
                    port = value;
                    break;
                case "database" or "db":
                    database = value;
                    break;
                case "username" or "user id" or "user":
                    user = value;
                    break;
            }
        }

        return (host, port, database, user);
    }

    /// <summary>
    /// Parses a SQL Server connection string into the values sqlcmd needs.
    /// </summary>
    internal static SqlServerTarget ParseSqlServerConnectionString(string? connectionString)
    {
        var server = DefaultSqlServerHost;
        var database = DefaultDatabaseName;
        string? user = null;
        string? password = null;
        var trustServerCertificate = false;

        if (string.IsNullOrEmpty(connectionString))
            return new SqlServerTarget(server, database, user, password, trustServerCertificate);

        foreach (var (key, value) in EnumerateConnectionStringParts(connectionString))
        {
            switch (key)
            {
                case "server" or "data source" or "datasource" or "host":
                    server = value;
                    break;
                case "database" or "initial catalog" or "db":
                    database = value;
                    break;
                case "user id" or "uid" or "username" or "user":
                    user = value;
                    break;
                case "password" or "pwd":
                    password = value;
                    break;
                case "trustservercertificate":
                    trustServerCertificate = bool.TryParse(value, out var parsed) && parsed;
                    break;
            }
        }

        return new SqlServerTarget(server, database, user, password, trustServerCertificate);
    }

    /// <summary>
    /// Reads a single value from a semicolon-delimited connection string.
    /// </summary>
    internal static string? TryGetConnectionStringValue(string? connectionString, params string[] keys)
    {
        if (string.IsNullOrWhiteSpace(connectionString) || keys.Length == 0)
            return null;

        foreach (var (key, value) in EnumerateConnectionStringParts(connectionString))
        {
            if (keys.Any(k => k.Equals(key, StringComparison.OrdinalIgnoreCase)))
                return value;
        }

        return null;
    }

    private static IEnumerable<(string Key, string Value)> EnumerateConnectionStringParts(string connectionString)
    {
        foreach (var part in connectionString.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eqIndex = part.IndexOf('=');
            if (eqIndex <= 0)
                continue;

            var key = part[..eqIndex].Trim().ToLowerInvariant();
            var value = part[(eqIndex + 1)..].Trim();

            if (value.Length > 0)
                yield return (key, value);
        }
    }

    private sealed record ToolInvocation(
        string Tool,
        List<string> Arguments,
        Dictionary<string, string> Environment);

    /// <summary>
    /// The pieces of a SQL Server connection string that <c>sqlcmd</c> needs.
    /// </summary>
    /// <param name="Server">Server (and optional port) to connect to.</param>
    /// <param name="Database">Database to dump or restore.</param>
    /// <param name="User">Login name, when SQL authentication is used.</param>
    /// <param name="Password">Password passed through <c>SQLCMDPASSWORD</c>.</param>
    /// <param name="TrustServerCertificate">Whether to pass <c>-C</c> to accept the server certificate.</param>
    internal sealed record SqlServerTarget(
        string Server,
        string Database,
        string? User,
        string? Password,
        bool TrustServerCertificate);
}
