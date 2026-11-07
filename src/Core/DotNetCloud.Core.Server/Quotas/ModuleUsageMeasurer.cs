using System.Globalization;
using DotNetCloud.Core;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Server.Sandbox;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DotNetCloud.Core.Server.Quotas;

/// <summary>
/// Measures the database and data-directory footprint of a sandboxed module so the usage monitor
/// can enforce the measured quota dimensions (F10, §4.11).
/// </summary>
/// <remarks>
/// PostgreSQL and SQL Server have no per-role disk quota, so these measurements are a <em>detection</em>
/// control: the monitor suspends a module that exceeds its limit. Every measurement failure is
/// non-fatal and returns <see langword="null"/> (treated as "unknown", never as "over quota").
/// </remarks>
internal sealed class ModuleUsageMeasurer
{
    private const string PostgreSqlSizeSql = """
        SELECT COALESCE(SUM(pg_total_relation_size(c.oid)), 0)
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = @schema
          AND c.relkind IN ('r', 'i', 't', 'm', 'p')
        """;

    private const string SqlServerSizeSql = """
        SELECT COALESCE(SUM(ps.reserved_page_count), 0) * 8192
        FROM sys.tables t
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        JOIN sys.dm_db_partition_stats ps ON ps.object_id = t.object_id
        WHERE s.name = @schema
        """;

    private readonly CoreDatabaseOptions _database;
    private readonly ILogger<ModuleUsageMeasurer> _logger;
    private readonly string _dataRoot;

    /// <summary>Initializes a new instance of the <see cref="ModuleUsageMeasurer"/> class.</summary>
    /// <param name="database">The core database the module schemas live in.</param>
    /// <param name="logger">Logger.</param>
    public ModuleUsageMeasurer(CoreDatabaseOptions database, ILogger<ModuleUsageMeasurer> logger)
        : this(database, logger, ModuleSandboxLayout.GetDataRoot())
    {
    }

    /// <summary>Initializes a testable instance with an explicit data root.</summary>
    internal ModuleUsageMeasurer(CoreDatabaseOptions database, ILogger<ModuleUsageMeasurer> logger, string dataRoot)
    {
        _database = database;
        _logger = logger;
        _dataRoot = dataRoot;
    }

    /// <summary>Measures the total size of a module's schema in bytes.</summary>
    /// <param name="schema">The module schema.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The size in bytes, or <see langword="null"/> when it cannot be measured.</returns>
    public async Task<long?> MeasureDatabaseBytesAsync(string schema, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        try
        {
            await using System.Data.Common.DbConnection connection = _database.Provider == DatabaseProvider.SqlServer
                ? new SqlConnection(_database.ConnectionString)
                : new NpgsqlConnection(_database.ConnectionString);

            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandTimeout = 30;
            command.CommandText = _database.Provider == DatabaseProvider.SqlServer
                ? SqlServerSizeSql
                : PostgreSqlSizeSql;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@schema";
            parameter.Value = schema;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is null or DBNull ? null : Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not measure the data-store size for schema {Schema}", LogSanitizer.Sanitize(schema));
            return null;
        }
    }

    /// <summary>Measures the module's private data directory in bytes.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <returns>The size in bytes, or 0 when the directory does not exist.</returns>
    public long MeasureDataDirectoryBytes(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        try
        {
            var layout = ModuleSandboxLayout.Create(_dataRoot, moduleId);
            return MeasureDirectoryBytes(layout.DataDirectory);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not measure the data directory for module {ModuleId}", LogSanitizer.Sanitize(moduleId));
            return 0;
        }
    }

    /// <summary>Measures a directory tree in bytes, skipping reparse points (symlinks/junctions).</summary>
    /// <param name="directory">The root directory.</param>
    /// <returns>The total size in bytes.</returns>
    internal static long MeasureDirectoryBytes(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        long total = 0;
        foreach (var file in Directory.EnumerateFiles(directory, "*", options))
        {
            try
            {
                total += new FileInfo(file).Length;
            }
            catch (IOException)
            {
                // A file that disappears mid-scan is not an error for quota accounting.
            }
        }

        return total;
    }
}
