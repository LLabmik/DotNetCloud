using System.Reflection;
using DotNetCloud.Modules.Files.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Handles legacy Files module EF Core migration baselining for schemas
/// that were created without a proper __EFMigrationsHistory table.
/// Extracted from <c>Program.cs</c> for better maintainability.
/// </summary>
public sealed class LegacyFilesMigrationService
{
    private static readonly string[] FilesMigrationChain =
    [
        "20260304172504_InitialFilesSchema",
        "20260308113429_AddFileVersionScanStatus",
        "20260308164648_AddCdcChunkMetadata",
        "20260309063020_AddSyncCursorSupport",
        "20260309083622_AddPosixPermissions",
        "20260309093919_AddSymlinkSupport",
        "20260314133732_SyncHardeningP0",
        "20260315074239_SyncDeviceIdentity",
        "20260315121601_SyncDeviceCursorTracking",
        "20260321123812_AddShareExpiryNotificationSentAt",
        "20260423104054_AddAdminSharedFolders",
    ];

    /// <summary>
    /// Migrates the Files database, first baselining any legacy migration history,
    /// then applying pending EF Core migrations.
    /// </summary>
    public async Task MigrateFilesDatabaseAsync(
        FilesDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await BaselineLegacyFilesMigrationHistoryAsync(context, logger, cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Files module database migrated");
    }

    private async Task BaselineLegacyFilesMigrationHistoryAsync(
        FilesDbContext context,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var pendingMigrations = (await context.Database.GetPendingMigrationsAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        if (!pendingMigrations.Contains(FilesMigrationChain[0]))
        {
            return;
        }

        if (!await ModuleTableExistsAsync(context, "FileNodes"))
        {
            return;
        }

        var detectedMigrations = await DetectLegacyFilesMigrationsAsync(context, cancellationToken);
        if (detectedMigrations.Count == 0)
        {
            return;
        }

        var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var migrationsToInsert = detectedMigrations
            .Where(migrationId => !appliedMigrations.Contains(migrationId))
            .ToList();

        if (migrationsToInsert.Count == 0)
        {
            return;
        }

        foreach (var migrationId in migrationsToInsert)
        {
            await InsertMigrationHistoryRowAsync(context, migrationId, cancellationToken);
        }

        logger.LogWarning(
            "Baselined {Count} Files migration history entries for a legacy schema created without EF migration history: {Migrations}",
            migrationsToInsert.Count,
            string.Join(", ", migrationsToInsert));
    }

    private async Task<List<string>> DetectLegacyFilesMigrationsAsync(
        FilesDbContext context,
        CancellationToken cancellationToken)
    {
        var detected = new List<string>();

        if (!await TablesExistAsync(
                context,
                cancellationToken,
                "FileChunks",
                "FileNodes",
                "FileShares",
                "FileVersionChunks",
                "FileVersions",
                "UploadSessions"))
        {
            return detected;
        }

        detected.Add(FilesMigrationChain[0]);

        if (!await ColumnExistsAsync(context, "FileVersions", "ScanStatus", cancellationToken))
        {
            return detected;
        }

        detected.Add(FilesMigrationChain[1]);

        if (!await ColumnExistsAsync(context, "UploadSessions", "ChunkSizesManifest", cancellationToken)
            || !await ColumnExistsAsync(context, "FileVersionChunks", "ChunkSize", cancellationToken)
            || !await ColumnExistsAsync(context, "FileVersionChunks", "Offset", cancellationToken))
        {
            return detected;
        }

        detected.Add(FilesMigrationChain[2]);

        if (!await TablesExistAsync(context, cancellationToken, "UserSyncCounters")
            || !await ColumnExistsAsync(context, "FileNodes", "SyncSequence", cancellationToken))
        {
            return detected;
        }

        detected.Add(FilesMigrationChain[3]);

        if (!await ColumnExistsAsync(context, "UploadSessions", "PosixMode", cancellationToken)
            || !await ColumnExistsAsync(context, "UploadSessions", "PosixOwnerHint", cancellationToken)
            || !await ColumnExistsAsync(context, "FileVersions", "PosixMode", cancellationToken)
            || !await ColumnExistsAsync(context, "FileNodes", "PosixMode", cancellationToken)
            || !await ColumnExistsAsync(context, "FileNodes", "PosixOwnerHint", cancellationToken))
        {
            return detected;
        }

        detected.Add(FilesMigrationChain[4]);

        if (!await ColumnExistsAsync(context, "FileNodes", "LinkTarget", cancellationToken))
        {
            return detected;
        }

        detected.Add(FilesMigrationChain[5]);

        if (!await IndexExistsAsync(context, "FileNodes", "uq_file_nodes_parent_name_active", cancellationToken)
            || !await IndexExistsAsync(context, "FileNodes", "uq_file_nodes_root_name_active", cancellationToken))
        {
            return detected;
        }

        detected.Add(FilesMigrationChain[6]);

        if (!await TablesExistAsync(context, cancellationToken, "SyncDevices")
            || !await ColumnExistsAsync(context, "UploadSessions", "DeviceId", cancellationToken)
            || !await ColumnExistsAsync(context, "FileNodes", "OriginatingDeviceId", cancellationToken))
        {
            return detected;
        }

        detected.Add(FilesMigrationChain[7]);

        if (!await TablesExistAsync(context, cancellationToken, "SyncDeviceCursors"))
        {
            return detected;
        }

        detected.Add(FilesMigrationChain[8]);

        if (!await ColumnExistsAsync(context, "FileShares", "ExpiryNotificationSentAt", cancellationToken))
        {
            return detected;
        }

        detected.Add(FilesMigrationChain[9]);

        if (!await TablesExistAsync(context, cancellationToken, "AdminSharedFolders", "AdminSharedFolderGrants"))
        {
            return detected;
        }

        detected.Add(FilesMigrationChain[10]);
        return detected;
    }

    private async Task<bool> TablesExistAsync(
        DbContext context,
        CancellationToken cancellationToken,
        params string[] tableNames)
    {
        foreach (var tableName in tableNames)
        {
            if (!await ModuleTableExistsAsync(context, tableName))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> ColumnExistsAsync(
        DbContext context,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        var provider = context.Database.ProviderName ?? string.Empty;

        if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            return await ExecuteExistsQueryAsync(
                context,
                "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @tableName AND column_name = @columnName);",
                cancellationToken,
                ("@tableName", tableName),
                ("@columnName", columnName));
        }

        if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            return await ExecuteExistsQueryAsync(
                context,
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @tableName AND COLUMN_NAME = @columnName) THEN 1 ELSE 0 END;",
                cancellationToken,
                ("@tableName", tableName),
                ("@columnName", columnName));
        }

        if (provider.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        throw new NotSupportedException($"Unsupported relational provider for column checks: {provider}");
    }

    private async Task<bool> IndexExistsAsync(
        DbContext context,
        string tableName,
        string indexName,
        CancellationToken cancellationToken)
    {
        var provider = context.Database.ProviderName ?? string.Empty;

        if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            return await ExecuteExistsQueryAsync(
                context,
                "SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = @tableName AND indexname = @indexName);",
                cancellationToken,
                ("@tableName", tableName),
                ("@indexName", indexName));
        }

        if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            return await ExecuteExistsQueryAsync(
                context,
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.indexes i INNER JOIN sys.tables t ON i.object_id = t.object_id INNER JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = @tableName AND i.name = @indexName) THEN 1 ELSE 0 END;",
                cancellationToken,
                ("@tableName", tableName),
                ("@indexName", indexName));
        }

        if (provider.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        throw new NotSupportedException($"Unsupported relational provider for index checks: {provider}");
    }

    private async Task<bool> ExecuteExistsQueryAsync(
        DbContext context,
        string commandText,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;

            foreach (var (name, value) in parameters)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value;
                command.Parameters.Add(parameter);
            }

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result switch
            {
                bool boolResult => boolResult,
                byte byteResult => byteResult != 0,
                short shortResult => shortResult != 0,
                int intResult => intResult != 0,
                long longResult => longResult != 0,
                _ => false,
            };
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task InsertMigrationHistoryRowAsync(
        DbContext context,
        string migrationId,
        CancellationToken cancellationToken)
    {
        var provider = context.Database.ProviderName ?? string.Empty;
        var commandText = provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)
            ? "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES (@migrationId, @productVersion) ON CONFLICT (\"MigrationId\") DO NOTHING;"
            : provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase)
                ? "IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = @migrationId) INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (@migrationId, @productVersion);"
                : throw new NotSupportedException($"Unsupported relational provider for migration history inserts: {provider}");

        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;

            var migrationParameter = command.CreateParameter();
            migrationParameter.ParameterName = "@migrationId";
            migrationParameter.Value = migrationId;
            command.Parameters.Add(migrationParameter);

            var versionParameter = command.CreateParameter();
            versionParameter.ParameterName = "@productVersion";
            versionParameter.Value = GetEntityFrameworkProductVersion();
            command.Parameters.Add(versionParameter);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static string GetEntityFrameworkProductVersion()
    {
        return typeof(DbContext).Assembly
                   .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                   ?.InformationalVersion
                   ?.Split('+')[0]
               ?? typeof(DbContext).Assembly.GetName().Version?.ToString()
               ?? "10.0.0";
    }

    private async Task<bool> ModuleTableExistsAsync(DbContext context, string tableName)
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();

            var provider = context.Database.ProviderName ?? string.Empty;
            if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                command.CommandText = "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = @tableName);";
            }
            else if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                command.CommandText = "SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @tableName) THEN 1 ELSE 0 END;";
            }
            else if (provider.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
            {
                // Integration tests use EF InMemory; there are no physical tables to probe.
                return true;
            }
            else
            {
                throw new NotSupportedException($"Unsupported relational provider for module table checks: {provider}");
            }

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@tableName";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync();
            return result switch
            {
                bool boolResult => boolResult,
                byte byteResult => byteResult != 0,
                short shortResult => shortResult != 0,
                int intResult => intResult != 0,
                long longResult => longResult != 0,
                _ => false
            };
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
