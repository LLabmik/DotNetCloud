using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Data.Modules;

/// <summary>
/// Brings a module's schema up to date with its own migrations, surviving a schema that already
/// contains the module's tables but whose migration history is empty or incomplete.
/// </summary>
/// <remarks>
/// <para>
/// A sandboxed module owns its schema and applies its own migrations (Track C). On an install that
/// predates the sandbox the same tables were created by the core's model, so the module's
/// <c>__EFMigrationsHistory</c> can be empty even though every object exists. A plain
/// <c>MigrateAsync</c> then replays the initial migration and dies with
/// <c>42P07 relation "EmailAccounts" already exists</c> (SQL Server: "There is already an object
/// named ..."), and the host never becomes healthy.
/// </para>
/// <para>
/// The recovery is the one the core has always used for its own module schemas: create whatever the
/// current model has that the database lacks, then record the pending migrations as applied so they
/// are not retried on every boot. This lives in <c>DotNetCloud.Core.Data</c> so the core and every
/// module host share exactly one implementation.
/// </para>
/// </remarks>
public static class ModuleSchemaInitializer
{
    private const int MaxCreatePasses = 10;

    /// <summary>
    /// Applies the module's pending migrations, falling back to model-based table creation when the
    /// schema already contains them.
    /// </summary>
    /// <param name="context">The module's database context.</param>
    /// <param name="moduleId">Module id, for log messages.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the schema is usable.</returns>
    public static async Task InitializeAsync(
        DbContext context,
        string moduleId,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

        if (pending.Count == 0)
        {
            // Nothing to replay: the history matches. Still create anything the current model has
            // that the database lacks (tables introduced outside the migration history).
            await EnsureMissingTablesAsync(context, moduleId, logger, cancellationToken);
            return;
        }

        try
        {
            await context.Database.MigrateAsync(cancellationToken);
            return;
        }
        catch (Exception ex) when (ex is InvalidOperationException or DbException)
        {
            logger?.LogWarning(
                ex,
                "Migrations could not be replayed for module {ModuleId}; baselining the existing schema " +
                "and marking {Count} pending migration(s) as applied",
                moduleId,
                pending.Count);
        }

        await BaselineAsync(context, moduleId, pending, logger, cancellationToken);
    }

    /// <summary>
    /// Records migrations as applied without replaying them, after making sure every table in the
    /// current model exists.
    /// </summary>
    /// <param name="context">The module's database context.</param>
    /// <param name="moduleId">Module id, for log messages.</param>
    /// <param name="pendingMigrationIds">The migration ids to record.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the schema is baselined.</returns>
    public static async Task BaselineAsync(
        DbContext context,
        string moduleId,
        IEnumerable<string> pendingMigrationIds,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(pendingMigrationIds);

        await EnsureMissingTablesAsync(context, moduleId, logger, cancellationToken);

        foreach (var migrationId in pendingMigrationIds)
        {
            await RecordMigrationAsAppliedAsync(context, migrationId, logger, cancellationToken);
        }
    }

    /// <summary>
    /// Creates the tables the current model declares that the database does not have yet.
    /// </summary>
    /// <param name="context">The module's database context.</param>
    /// <param name="moduleId">Module id, for log messages.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the missing tables have been created.</returns>
    public static Task EnsureMissingTablesAsync(
        DbContext context,
        string moduleId,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        return CreateMissingTablesAsync(context, moduleId, logger, cancellationToken);
    }

    /// <summary>
    /// Classifies a database error raised while creating a table.
    /// </summary>
    /// <remarks>
    /// These are the "the object is already there" (or otherwise tolerable) outcomes of a
    /// model-based create against an existing schema. Anything else is a real failure and is retried
    /// (a foreign key may simply target a table that a later pass creates).
    /// </remarks>
    /// <param name="message">The provider error message.</param>
    /// <returns><see langword="true"/> when the batch can be treated as already satisfied.</returns>
    public static bool IsBenignCreateConflict(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
            || message.Contains("already an object", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || message.Contains("cycles or multiple cascade", StringComparison.OrdinalIgnoreCase)
            || message.Contains("SET options", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Incorrect WHERE clause", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Inserts a row into <c>__EFMigrationsHistory</c> so a migration is not retried on the next
    /// startup. Provider-aware quoting: SQL Server uses brackets, PostgreSQL uses double quotes.
    /// </summary>
    private static async Task RecordMigrationAsAppliedAsync(
        DbContext context,
        string migrationId,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var productVersion = typeof(DbContext).Assembly.GetName().Version?.ToString() ?? "0.0.0";
            var isPostgres = context.Database.ProviderName?.IndexOf(
                "Npgsql", StringComparison.OrdinalIgnoreCase) >= 0;
            var sql = isPostgres
                ? "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({0}, {1})"
                : "INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ({0}, {1})";

            await context.Database.ExecuteSqlRawAsync(sql, [migrationId, productVersion], cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(
                ex,
                "Could not record migration {MigrationId} in __EFMigrationsHistory; it will be retried on the next start",
                migrationId);
        }
    }

    /// <summary>
    /// Executes the model's CREATE TABLE script batch by batch, ignoring the tables that already
    /// exist, and retries so foreign keys that target later tables resolve.
    /// </summary>
    private static async Task CreateMissingTablesAsync(
        DbContext context,
        string moduleId,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var script = context.Database.GenerateCreateScript();
            if (string.IsNullOrWhiteSpace(script))
            {
                logger?.LogWarning("Empty create script for module {ModuleId}", moduleId);
                return;
            }

            var batches = script
                .Split("GO", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(batch => batch.Trim())
                .Where(batch => batch.Length > 0)
                .ToList();

            // SQL Server's GenerateCreateScript uses double-quoted identifiers inside HasFilter(),
            // which needs QUOTED_IDENTIFIER ON.
            if (context.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true)
            {
                for (var i = 0; i < batches.Count; i++)
                {
                    batches[i] = "SET QUOTED_IDENTIFIER ON;\n" + batches[i];
                }
            }

            var failedBatches = new List<string>(batches);
            var created = 0;
            var skipped = 0;

            // Raw commands on the module's connection rather than ExecuteSqlRawAsync: EF logs a
            // CommandError at Error level for each failed command before the catch can treat
            // "already exists" as expected, which flooded the log on every boot.
            var connection = context.Database.GetDbConnection();
            var openedConnection = connection.State != ConnectionState.Open;
            if (openedConnection)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                var commandTimeout = context.Database.GetCommandTimeout() ?? 30;

                for (var pass = 0; pass < MaxCreatePasses && failedBatches.Count > 0; pass++)
                {
                    var stillFailing = new List<string>();

                    foreach (var batch in failedBatches)
                    {
                        try
                        {
                            await using var command = connection.CreateCommand();
                            command.CommandText = batch;
                            command.CommandTimeout = commandTimeout;
                            await command.ExecuteNonQueryAsync(cancellationToken);
                            created++;
                        }
                        catch (DbException ex) when (IsBenignCreateConflict(ex.Message))
                        {
                            skipped++;
                        }
                        catch (DbException ex)
                        {
                            logger?.LogDebug(
                                ex,
                                "CREATE batch still failing for module {ModuleId} (pass {Pass}): {Batch}",
                                moduleId,
                                pass,
                                batch[..Math.Min(batch.Length, 200)]);
                            stillFailing.Add(batch);
                        }
                    }

                    if (stillFailing.Count == failedBatches.Count)
                    {
                        // No progress — stop retrying.
                        break;
                    }

                    failedBatches = stillFailing;
                }
            }
            finally
            {
                if (openedConnection)
                {
                    await connection.CloseAsync();
                }
            }

            if (created > 0 || skipped > 0)
            {
                logger?.LogInformation(
                    "Module {ModuleId}: created {Created}, skipped {Skipped}, still failing {Failed}",
                    moduleId,
                    created,
                    skipped,
                    failedBatches.Count);
            }

            if (failedBatches.Count > 0)
            {
                logger?.LogWarning(
                    "{Count} CREATE batches failed for module {ModuleId} after retries",
                    failedBatches.Count,
                    moduleId);

                foreach (var batch in failedBatches.Take(3))
                {
                    logger?.LogWarning("  Failed batch: {Batch}", batch[..Math.Min(batch.Length, 300)]);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(ex, "Failed to create tables for module {ModuleId}", moduleId);
        }
    }
}
