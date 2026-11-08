using System.Data.Common;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using DotNetCloud.Core;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.Quotas;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Sandbox;

/// <summary>
/// Provisions the schema and database principal owned by a sandboxed module.
/// </summary>
/// <remarks>
/// <para>
/// PostgreSQL: a <c>LOGIN</c> role with <c>CONNECTION LIMIT</c> owns a dedicated schema, and its
/// <c>search_path</c> is pinned both on the role and in the connection string. SQL Server: a
/// contained database user (no server login) owns a dedicated schema.
/// </para>
/// <para>
/// Generated passwords are 32 CSPRNG bytes. They are stored only as DataProtection ciphertext in
/// <c>ModuleDataStore.EncryptedConnectionString</c>; no log, audit entry or <c>LastError</c> value
/// ever contains one.
/// </para>
/// </remarks>
public sealed class ModuleDataStoreProvisioningService : IModuleDataStoreProvisioningService
{
    private const string ProtectorPurpose = "DotNetCloud.Sandbox.ModuleDataStore";
    private const int LastErrorMaxLength = 2000;
    private const int CommandTimeoutSeconds = 60;

    private static readonly Regex SecretInTextPattern = new(
        @"(?i)(password|pwd)\s*=\s*[^;\s]+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CoreDatabaseOptions _database;
    private readonly ModuleQuotaResolver _quotas;
    private readonly IDataProtector _protector;
    private readonly ILogger<ModuleDataStoreProvisioningService> _logger;

    /// <summary>Initializes a new instance of the <see cref="ModuleDataStoreProvisioningService"/> class.</summary>
    /// <param name="scopeFactory">Scope factory for the core database context.</param>
    /// <param name="database">The core database the module stores live in.</param>
    /// <param name="quotas">Resolves the effective connection limit when the caller does not supply one.</param>
    /// <param name="dataProtection">DataProtection provider used to encrypt module connection strings.</param>
    /// <param name="logger">Logger.</param>
    public ModuleDataStoreProvisioningService(
        IServiceScopeFactory scopeFactory,
        CoreDatabaseOptions database,
        ModuleQuotaResolver quotas,
        IDataProtectionProvider dataProtection,
        ILogger<ModuleDataStoreProvisioningService> logger)
    {
        _scopeFactory = scopeFactory;
        _database = database;
        _quotas = quotas;
        _protector = dataProtection.CreateProtector(ProtectorPurpose);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ModuleDataStorePrerequisiteResult> CheckPrerequisitesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            return _database.Provider == DatabaseProvider.SqlServer
                ? await CheckSqlServerPrerequisitesAsync(connection, cancellationToken)
                : await CheckPostgreSqlPrerequisitesAsync(connection, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Module data-store prerequisite check failed");
            return new ModuleDataStorePrerequisiteResult(
                false,
                [$"Could not reach the database to check provisioning prerequisites: {SanitizeError(ex.Message)}"]);
        }
    }

    /// <inheritdoc />
    public async Task<ModuleDataStoreProvisioningResult> EnsureProvisionedAsync(
        string moduleId,
        int? connectionLimit = null,
        CancellationToken cancellationToken = default)
    {
        if (!ModuleDataStoreNaming.TryCreate(moduleId, out var schema, out var principal, out var namingError))
        {
            return ModuleDataStoreProvisioningResult.Failed(namingError ?? "Invalid module id.");
        }

        var limit = connectionLimit ?? await ResolveConnectionLimitAsync(moduleId, cancellationToken);
        var existing = await GetStoreAsync(moduleId, cancellationToken);
        if (existing is { Status: ModuleDataStoreStatus.Active } && !string.IsNullOrEmpty(existing.EncryptedConnectionString))
        {
            await RefreshConnectionLimitAsync(existing, limit, cancellationToken);
            return new ModuleDataStoreProvisioningResult(true, existing.SchemaName, existing.PrincipalName, null);
        }

        var prerequisites = await CheckPrerequisitesAsync(cancellationToken);
        if (!prerequisites.IsSatisfied)
        {
            var error = string.Join(" ", prerequisites.Errors);
            await SaveStoreAsync(
                moduleId, schema, principal, encryptedConnectionString: null,
                ModuleDataStoreStatus.Error, error, cancellationToken);
            return ModuleDataStoreProvisioningResult.Failed(error);
        }

        var password = GeneratePassword();

        try
        {
            var builder = ModuleDataStoreSqlBuilder.Create(
                _database.Provider, _database.ConnectionString, schema, principal);

            await ExecuteAsync(builder.ProvisionStatements(password, limit), cancellationToken);

            var scopedConnectionString = builder.BuildScopedConnectionString(
                _database.ConnectionString, password, limit);

            await SaveStoreAsync(
                moduleId, schema, principal, Encrypt(moduleId, scopedConnectionString),
                ModuleDataStoreStatus.Active, lastError: null, cancellationToken);

            _logger.LogInformation(
                "Provisioned isolated data store for module {ModuleId} (schema {Schema}, principal {Principal})",
                moduleId, schema, principal);

            return new ModuleDataStoreProvisioningResult(true, schema, principal, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var error = SanitizeError(ex.Message);
            await SaveStoreAsync(
                moduleId, schema, principal, encryptedConnectionString: null,
                ModuleDataStoreStatus.Error, error, cancellationToken);

            _logger.LogError(
                "Provisioning the isolated data store for module {ModuleId} failed: {Error}",
                moduleId, error);

            return ModuleDataStoreProvisioningResult.Failed(error);
        }
    }

    /// <inheritdoc />
    public async Task<ModuleDataStoreProvisioningResult> RotateCredentialsAsync(
        string moduleId,
        int? connectionLimit = null,
        CancellationToken cancellationToken = default)
    {
        var store = await GetStoreAsync(moduleId, cancellationToken);
        if (store is null)
        {
            return ModuleDataStoreProvisioningResult.Failed(
                "The module has no provisioned data store; provision it first.");
        }

        var limit = connectionLimit ?? await ResolveConnectionLimitAsync(moduleId, cancellationToken);
        var password = GeneratePassword();

        try
        {
            var builder = ModuleDataStoreSqlBuilder.Create(
                _database.Provider, _database.ConnectionString, store.SchemaName, store.PrincipalName);

            await ExecuteAsync(builder.RotateStatements(password, limit), cancellationToken);

            var scopedConnectionString = builder.BuildScopedConnectionString(
                _database.ConnectionString, password, limit);

            store.EncryptedConnectionString = Encrypt(moduleId, scopedConnectionString);
            store.Status = ModuleDataStoreStatus.Active;
            store.LastError = null;
            store.UpdatedAtUtc = DateTime.UtcNow;
            await SaveStoreRowAsync(store, cancellationToken);

            _logger.LogInformation("Rotated credentials for module {ModuleId} data store", moduleId);

            return new ModuleDataStoreProvisioningResult(true, store.SchemaName, store.PrincipalName, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var error = SanitizeError(ex.Message);
            store.Status = ModuleDataStoreStatus.Error;
            store.LastError = error;
            store.UpdatedAtUtc = DateTime.UtcNow;
            await SaveStoreRowAsync(store, cancellationToken);

            _logger.LogError("Rotating credentials for module {ModuleId} failed: {Error}", moduleId, error);
            return ModuleDataStoreProvisioningResult.Failed(error);
        }
    }

    /// <inheritdoc />
    public Task<ModuleDataStoreProvisioningResult> AdoptExistingStoreAsync(
        string moduleId, CancellationToken cancellationToken = default) =>
        AdoptExistingStoreAsync(moduleId, legacySchemaName: null, cancellationToken);

    /// <inheritdoc />
    public async Task<ModuleDataStoreProvisioningResult> AdoptExistingStoreAsync(
        string moduleId, string? legacySchemaName, CancellationToken cancellationToken = default)
    {
        if (!ModuleDataStoreNaming.TryCreate(moduleId, out _, out _, out var namingError))
        {
            return ModuleDataStoreProvisioningResult.Failed(namingError ?? "Invalid module id.");
        }

        var limit = await ResolveConnectionLimitAsync(moduleId, cancellationToken);
        var store = await GetStoreAsync(moduleId, cancellationToken);

        if (store is null || string.IsNullOrEmpty(store.EncryptedConnectionString))
        {
            // No principal yet (or a previous attempt failed): create it. The schema already
            // exists, so CREATE SCHEMA IF NOT EXISTS is a no-op and no data moves.
            var provisioned = await EnsureProvisionedAsync(moduleId, limit, cancellationToken);
            if (!provisioned.Success)
            {
                return provisioned;
            }

            store = await GetStoreAsync(moduleId, cancellationToken);
            if (store is null)
            {
                return ModuleDataStoreProvisioningResult.Failed(
                    "The module data store could not be read after provisioning.");
            }
        }

        try
        {
            // A bundled module keeps its data: move the objects it already had (including
            // __EFMigrationsHistory) into the schema it now owns. This runs BEFORE the ownership
            // transfer below, because the moved objects are still owned by the app role.
            if (!string.IsNullOrEmpty(legacySchemaName)
                && !string.Equals(legacySchemaName, store.SchemaName, StringComparison.Ordinal))
            {
                var moveError = await MoveLegacyObjectsAsync(store, legacySchemaName, cancellationToken);
                if (moveError is not null)
                {
                    store.Status = ModuleDataStoreStatus.Error;
                    store.LastError = moveError;
                    store.UpdatedAtUtc = DateTime.UtcNow;
                    await SaveStoreRowAsync(store, cancellationToken);
                    return ModuleDataStoreProvisioningResult.Failed(moveError);
                }
            }

            var builder = ModuleDataStoreSqlBuilder.Create(
                _database.Provider, _database.ConnectionString, store.SchemaName, store.PrincipalName);

            await ExecuteAsync(builder.AdoptStatements(), cancellationToken);

            store.Status = ModuleDataStoreStatus.Active;
            store.LastError = null;
            store.UpdatedAtUtc = DateTime.UtcNow;
            await SaveStoreRowAsync(store, cancellationToken);

            _logger.LogInformation(
                "Adopted the existing data store for module {ModuleId} (schema {Schema}, principal {Principal})",
                LogSanitizer.Sanitize(moduleId), LogSanitizer.Sanitize(store.SchemaName), LogSanitizer.Sanitize(store.PrincipalName));

            return new ModuleDataStoreProvisioningResult(true, store.SchemaName, store.PrincipalName, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var error = SanitizeError(ex.Message);
            store.Status = ModuleDataStoreStatus.Error;
            store.LastError = error;
            store.UpdatedAtUtc = DateTime.UtcNow;
            await SaveStoreRowAsync(store, cancellationToken);

            _logger.LogError("Adopting the data store for module {ModuleId} failed: {Error}", moduleId, error);
            return ModuleDataStoreProvisioningResult.Failed(error);
        }
    }

    /// <inheritdoc />
    public async Task DestroyAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        var store = await GetStoreAsync(moduleId, cancellationToken);
        if (store is null)
        {
            return;
        }

        var builder = ModuleDataStoreSqlBuilder.Create(
            _database.Provider, _database.ConnectionString, store.SchemaName, store.PrincipalName);

        await ExecuteAsync(builder.DropStatements(), cancellationToken);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var row = await dbContext.ModuleDataStores.FirstOrDefaultAsync(d => d.ModuleId == moduleId, cancellationToken);

        if (row is not null)
        {
            dbContext.ModuleDataStores.Remove(row);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Destroyed the isolated data store for module {ModuleId}", moduleId);
    }

    /// <inheritdoc />
    public async Task<string?> GetConnectionStringAsync(
        string moduleId, CancellationToken cancellationToken = default)
    {
        var store = await GetStoreAsync(moduleId, cancellationToken);
        if (store is null || string.IsNullOrEmpty(store.EncryptedConnectionString))
        {
            return null;
        }

        try
        {
            return Decrypt(moduleId, store.EncryptedConnectionString);
        }
        catch (CryptographicException ex)
        {
            // The DataProtection key ring no longer matches the ciphertext (restore without the key
            // ring). Operators must run the credential-reset recovery path.
            _logger.LogError(
                ex,
                "Could not decrypt the module data-store connection string for {ModuleId}; credentials must be reset",
                moduleId);
            return null;
        }
    }

    private async Task<ModuleDataStorePrerequisiteResult> CheckPostgreSqlPrerequisitesAsync(
        DbConnection connection, CancellationToken cancellationToken)
    {
        // Resolve the actual login role so the remediation statement can be pasted verbatim.
        var roleName = await ExecuteScalarAsync(connection, "SELECT current_user", cancellationToken) as string;

        var canCreateRoles = await ExecuteScalarAsync(
            connection, "SELECT rolcreaterole FROM pg_roles WHERE rolname = current_user", cancellationToken);

        if (canCreateRoles is bool allowed)
        {
            return allowed
                ? ModuleDataStorePrerequisiteResult.Satisfied
                : new ModuleDataStorePrerequisiteResult(false,
                [
                    DescribePostgreSqlRolePrerequisite(roleName),
                ]);
        }

        return new ModuleDataStorePrerequisiteResult(false,
        [
            "Could not determine whether the DotNetCloud database role may create roles (pg_roles query returned no row).",
        ]);
    }

    private async Task<ModuleDataStorePrerequisiteResult> CheckSqlServerPrerequisitesAsync(
        DbConnection connection, CancellationToken cancellationToken)
    {
        var containedAuth = await ExecuteScalarAsync(
            connection,
            "SELECT value_in_use FROM sys.configurations WHERE name = 'contained database authentication'",
            cancellationToken);

        if (containedAuth is not int value || value != 1)
        {
            return new ModuleDataStorePrerequisiteResult(false,
            [
                "SQL Server contained database authentication is disabled, so module contained users cannot be created. " +
                "Run the following as an administrator and restart SQL Server: " +
                "EXEC sp_configure 'contained database authentication', 1; RECONFIGURE;",
            ]);
        }

        var containment = await ExecuteScalarAsync(
            connection, "SELECT containment_desc FROM sys.databases WHERE name = DB_NAME()", cancellationToken);

        if (string.Equals(containment as string, "PARTIAL", StringComparison.OrdinalIgnoreCase))
        {
            return ModuleDataStorePrerequisiteResult.Satisfied;
        }

        var databaseName =
            new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_database.ConnectionString).InitialCatalog;

        // db_owner may switch the database to partial containment itself; try before failing.
        try
        {
            await ExecuteAsync(
                [$"ALTER DATABASE {ModuleDataStoreNaming.QuoteSqlServer(databaseName)} SET CONTAINMENT = PARTIAL;"],
                cancellationToken);

            return ModuleDataStorePrerequisiteResult.Satisfied;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ModuleDataStorePrerequisiteResult(false,
            [
                DescribeSqlServerContainmentPrerequisite(databaseName),
                $"Engine error: {SanitizeError(ex.Message)}",
            ]);
        }
    }

    /// <summary>
    /// Re-applies a changed connection limit to an already-provisioned store without rotating the
    /// password: the engine-side cap is updated when the provider supports one (PostgreSQL), and the
    /// pool size in the stored connection string is rewritten for both providers.
    /// </summary>
    /// <remarks>
    /// A store encrypted with a different DataProtection key ring cannot be refreshed; the caller
    /// then falls through to full re-provisioning (which mints a new password) or the operator uses
    /// <c>datastore reset-credentials</c>.
    /// </remarks>
    private async Task RefreshConnectionLimitAsync(
        ModuleDataStore store, int connectionLimit, CancellationToken cancellationToken)
    {
        string current;
        try
        {
            current = Decrypt(store.ModuleId, store.EncryptedConnectionString!);
        }
        catch (CryptographicException)
        {
            return;
        }

        var updated = ApplyPoolSize(current, connectionLimit);
        if (string.Equals(current, updated, StringComparison.Ordinal))
        {
            return;
        }

        var builder = ModuleDataStoreSqlBuilder.Create(
            _database.Provider, _database.ConnectionString, store.SchemaName, store.PrincipalName);

        await ExecuteAsync(builder.UpdateConnectionLimitStatements(connectionLimit), cancellationToken);

        store.EncryptedConnectionString = Encrypt(store.ModuleId, updated);
        store.UpdatedAtUtc = DateTime.UtcNow;
        await SaveStoreRowAsync(store, cancellationToken);

        _logger.LogInformation(
            "Updated the connection limit for module {ModuleId} to {Limit}",
            LogSanitizer.Sanitize(store.ModuleId), connectionLimit);
    }

    private string ApplyPoolSize(string connectionString, int connectionLimit)
    {
        if (_database.Provider == DatabaseProvider.SqlServer)
        {
            var sqlBuilder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString)
            {
                MaxPoolSize = connectionLimit,
            };
            return sqlBuilder.ConnectionString;
        }

        var npgsqlBuilder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString)
        {
            MaxPoolSize = connectionLimit,
        };
        return npgsqlBuilder.ConnectionString;
    }

    private async Task<int> ResolveConnectionLimitAsync(string moduleId, CancellationToken cancellationToken)
    {
        var effective = await _quotas.ResolveAsync(moduleId, new ModuleQuotaRequest(), cancellationToken);
        return effective.ConnectionLimit;
    }

    /// <summary>
    /// Moves a bundled module's pre-sandbox objects (tables, sequences, views and the migration
    /// history) from its historical schema into the schema it now owns.
    /// </summary>
    /// <param name="store">The provisioned store row.</param>
    /// <param name="legacySchemaName">The schema the core used before the module was sandboxed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="null"/> on success, otherwise a sanitized error to persist.</returns>
    /// <remarks>
    /// Idempotent: once the legacy schema is empty the move is a no-op, so it is safe on every start.
    /// Objects whose name already exists in the target schema are never overwritten (the transfer
    /// reports them instead), which is what keeps a partially completed conversion from destroying
    /// data.
    /// </remarks>
    private async Task<string?> MoveLegacyObjectsAsync(
        ModuleDataStore store, string legacySchemaName, CancellationToken cancellationToken)
    {
        var sourceQuery = ModuleSchemaTransfer.SourceRelationsQuery(_database.Provider, legacySchemaName);
        var targetQuery = ModuleSchemaTransfer.TargetRelationNamesQuery(_database.Provider, store.SchemaName);

        if (sourceQuery is null || targetQuery is null)
        {
            return "The schema names are not plain identifiers; refusing to move objects.";
        }

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var source = await ReadRelationsAsync(connection, sourceQuery, cancellationToken);
        if (source.Count == 0)
        {
            // Nothing left to move: this is the steady state after the conversion.
            return null;
        }

        var target = await ReadRelationsAsync(connection, targetQuery, cancellationToken);

        if (!ModuleSchemaTransfer.TryBuild(
                _database.Provider,
                legacySchemaName,
                store.SchemaName,
                source,
                target.Select(relation => relation.Name).ToArray(),
                out var statements,
                out var buildError))
        {
            return buildError ?? "The legacy schema could not be moved.";
        }

        if (statements.Count == 0)
        {
            _logger.LogWarning(
                "Legacy schema {Schema} of module {ModuleId} holds {Count} object(s) but none could be moved; the target schema {Target} already has objects with those names.",
                LogSanitizer.Sanitize(legacySchemaName),
                LogSanitizer.Sanitize(store.ModuleId),
                source.Count,
                LogSanitizer.Sanitize(store.SchemaName));
            return null;
        }

        // Moving an object into the module's schema requires CREATE on it, which the app role only
        // has while it is a member of the module's owner role. AdoptStatements (run by the caller
        // afterwards) grants that membership and revokes it last, so the grant is issued here too.
        var builder = ModuleDataStoreSqlBuilder.Create(
            _database.Provider, _database.ConnectionString, store.SchemaName, store.PrincipalName);

        await ExecuteAsync(
            [.. builder.TemporaryOwnershipGrantStatements(), .. statements],
            cancellationToken);

        _logger.LogInformation(
            "Moved {MovedCount} object(s) from the legacy schema {Schema} of module {ModuleId} into {Target}",
            statements.Count,
            LogSanitizer.Sanitize(legacySchemaName),
            LogSanitizer.Sanitize(store.ModuleId),
            LogSanitizer.Sanitize(store.SchemaName));

        return null;
    }

    /// <summary>Reads relations (name plus engine kind) from a catalog query.</summary>
    private async Task<List<SchemaRelation>> ReadRelationsAsync(
        DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        List<SchemaRelation> relations = [];

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = CommandTimeoutSeconds;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var name = reader.GetString(0);
            var kind = reader.FieldCount > 1 && !reader.IsDBNull(1) ? reader.GetString(1)[0] : 'r';
            relations.Add(new SchemaRelation(name, kind));
        }

        return relations;
    }

    private DbConnection CreateConnection() => _database.Provider == DatabaseProvider.SqlServer
        ? new Microsoft.Data.SqlClient.SqlConnection(_database.ConnectionString)
        : new Npgsql.NpgsqlConnection(_database.ConnectionString);    private async Task ExecuteAsync(IReadOnlyList<string> statements, CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        foreach (var sql in statements)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = CommandTimeoutSeconds;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<object?> ExecuteScalarAsync(
        DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = CommandTimeoutSeconds;
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private async Task<ModuleDataStore?> GetStoreAsync(string moduleId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        return await dbContext.ModuleDataStores
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.ModuleId == moduleId, cancellationToken);
    }

    private async Task SaveStoreAsync(
        string moduleId,
        string schema,
        string principal,
        string? encryptedConnectionString,
        ModuleDataStoreStatus status,
        string? lastError,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        // AsTracking is required: CoreDbContext sets QueryTrackingBehavior.NoTracking globally, so a
        // detached row would be mutated and then silently discarded by SaveChanges — leaving a store
        // that was provisioned successfully (and logged as such) stuck in its previous status, most
        // often the Error row written by a first attempt that failed the prerequisite check.
        // AsTracking is required: CoreDbContext sets QueryTrackingBehavior.NoTracking globally, so a
        // detached row would be mutated and then silently discarded by SaveChanges — leaving a store
        // that was provisioned successfully (and logged as such) stuck in its previous status, most
        // often the Error row written by a first attempt that failed the prerequisite check.
        var row = await dbContext.ModuleDataStores
            .AsTracking()
            .FirstOrDefaultAsync(d => d.ModuleId == moduleId, cancellationToken);

        if (row is null)
        {
            row = new ModuleDataStore { ModuleId = moduleId, CreatedAtUtc = DateTime.UtcNow };
            dbContext.ModuleDataStores.Add(row);
        }

        row.Provider = _database.Provider.ToString();
        row.SchemaName = schema;
        row.PrincipalName = principal;
        row.EncryptedConnectionString = encryptedConnectionString;
        row.Status = status;
        row.LastError = lastError;
        row.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SaveStoreRowAsync(ModuleDataStore store, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        dbContext.ModuleDataStores.Update(store);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private string Encrypt(string moduleId, string value) =>
        _protector.CreateProtector(moduleId).Protect(value);

    private string Decrypt(string moduleId, string value) =>
        _protector.CreateProtector(moduleId).Unprotect(value);

    private static string GeneratePassword() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Builds the PostgreSQL <c>CREATEROLE</c> remediation text, naming the effective login role so
    /// the statement can be pasted verbatim (an existing install created before the setup wizard
    /// granted <c>CREATEROLE</c> must run it once).
    /// </summary>
    /// <param name="roleName">The role reported by <c>current_user</c>, when known.</param>
    internal static string DescribePostgreSqlRolePrerequisite(string? roleName)
    {
        var statement = string.IsNullOrWhiteSpace(roleName)
            ? "ALTER ROLE <dotnetcloud-role> CREATEROLE;"
            : $"ALTER ROLE {ModuleDataStoreNaming.QuotePostgreSql(roleName)} CREATEROLE;";

        return "The DotNetCloud database role lacks CREATEROLE, so module database principals cannot be " +
               "provisioned. Run the following as a PostgreSQL superuser, then restart the server: " +
               statement;
    }

    /// <summary>
    /// Builds the SQL Server partial-containment remediation text for the given database.
    /// </summary>
    /// <param name="databaseName">The database reported by <c>DB_NAME()</c>.</param>
    internal static string DescribeSqlServerContainmentPrerequisite(string databaseName)
    {
        var quoted = string.IsNullOrWhiteSpace(databaseName)
            ? "[dotnetcloud]"
            : ModuleDataStoreNaming.QuoteSqlServer(databaseName);

        return "The DotNetCloud database is not partially contained and could not be converted automatically. " +
               "Run the following as a database owner, then restart the server: " +
               $"ALTER DATABASE {quoted} SET CONTAINMENT = PARTIAL;";
    }

    /// <summary>
    /// Removes credential-looking text from an engine error before it becomes admin-visible.
    /// </summary>
    internal static string SanitizeError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Unknown error.";
        }

        var sanitized = SecretInTextPattern.Replace(message, "$1=***");

        return sanitized.Length <= LastErrorMaxLength
            ? sanitized
            : sanitized[..LastErrorMaxLength];
    }
}
