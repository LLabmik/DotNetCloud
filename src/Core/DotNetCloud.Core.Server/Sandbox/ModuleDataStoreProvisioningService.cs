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
        var canCreateRoles = await ExecuteScalarAsync(
            connection, "SELECT rolcreaterole FROM pg_roles WHERE rolname = current_user", cancellationToken);

        if (canCreateRoles is bool allowed)
        {
            return allowed
                ? ModuleDataStorePrerequisiteResult.Satisfied
                : new ModuleDataStorePrerequisiteResult(false,
                [
                    "The DotNetCloud database role lacks CREATEROLE, so module database principals cannot be provisioned. " +
                    "Run the following as a PostgreSQL superuser, then restart the server: " +
                    "ALTER ROLE <dotnetcloud-role> CREATEROLE;",
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

        // db_owner may switch the database to partial containment itself; try before failing.
        try
        {
            var databaseName = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_database.ConnectionString)
                .InitialCatalog;

            await ExecuteAsync(
                [$"ALTER DATABASE {ModuleDataStoreNaming.QuoteSqlServer(databaseName)} SET CONTAINMENT = PARTIAL;"],
                cancellationToken);

            return ModuleDataStorePrerequisiteResult.Satisfied;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ModuleDataStorePrerequisiteResult(false,
            [
                "The DotNetCloud database is not partially contained and could not be converted automatically. " +
                "Run the following as a database owner, then restart the server: " +
                "ALTER DATABASE [dotnetcloud] SET CONTAINMENT = PARTIAL;",
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

    private DbConnection CreateConnection() => _database.Provider == DatabaseProvider.SqlServer
        ? new Microsoft.Data.SqlClient.SqlConnection(_database.ConnectionString)
        : new Npgsql.NpgsqlConnection(_database.ConnectionString);
    private async Task ExecuteAsync(IReadOnlyList<string> statements, CancellationToken cancellationToken)
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

        var row = await dbContext.ModuleDataStores
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
