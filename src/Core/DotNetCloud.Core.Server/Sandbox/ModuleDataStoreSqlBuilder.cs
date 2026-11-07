using System.Data.Common;
using DotNetCloud.Core.Data.Naming;

namespace DotNetCloud.Core.Server.Sandbox;

/// <summary>
/// Builds the DDL statements that provision, rotate and drop a module's isolated data store.
/// </summary>
/// <remarks>
/// <para>
/// Statements are emitted as text so they can be asserted in unit tests without a live database.
/// Identifiers are always quoted through <see cref="ModuleDataStoreNaming"/> and generated secrets
/// are escaped literals; user input never reaches a statement unquoted.
/// </para>
/// <para>
/// The sequences mirror the plan (§A1 implementation notes): PostgreSQL uses a login role that owns
/// its own schema; SQL Server uses a contained database user (no server login).
/// </para>
/// </remarks>
internal sealed class ModuleDataStoreSqlBuilder
{
    private readonly DatabaseProvider _provider;
    private readonly string _schema;
    private readonly string _principal;
    private readonly string _databaseName;
    private readonly string _applicationPrincipal;

    private ModuleDataStoreSqlBuilder(
        DatabaseProvider provider,
        string schema,
        string principal,
        string databaseName,
        string applicationPrincipal)
    {
        _provider = provider;
        _schema = schema;
        _principal = principal;
        _databaseName = databaseName;
        _applicationPrincipal = applicationPrincipal;
    }

    /// <summary>
    /// Creates a builder for one module.
    /// </summary>
    /// <param name="provider">The configured database engine.</param>
    /// <param name="coreConnectionString">The core connection string (used for the database name and app principal).</param>
    /// <param name="schema">The module schema.</param>
    /// <param name="principal">The module principal.</param>
    /// <returns>The builder.</returns>
    public static ModuleDataStoreSqlBuilder Create(
        DatabaseProvider provider,
        string coreConnectionString,
        string schema,
        string principal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(coreConnectionString);

        string databaseName;
        string appPrincipal;

        if (provider == DatabaseProvider.SqlServer)
        {
            var builderObject = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(coreConnectionString);
            databaseName = builderObject.InitialCatalog;
            appPrincipal = builderObject.UserID;
        }
        else
        {
            var builderObject = new Npgsql.NpgsqlConnectionStringBuilder(coreConnectionString);
            databaseName = builderObject.Database ?? string.Empty;
            appPrincipal = builderObject.Username ?? string.Empty;
        }

        return new ModuleDataStoreSqlBuilder(provider, schema, principal, databaseName, appPrincipal);
    }

    /// <summary>Statements that verify the core role may provision module principals.</summary>
    public IReadOnlyList<string> PrerequisiteProbeStatements() => _provider == DatabaseProvider.SqlServer
        ?
        [
            "SELECT value_in_use FROM sys.configurations WHERE name = 'contained database authentication'",
            "SELECT containment_desc FROM sys.databases WHERE name = DB_NAME()",
        ]
        :
        [
            "SELECT rolcreaterole FROM pg_roles WHERE rolname = current_user",
        ];

    /// <summary>
    /// Statements that create or update the module principal and its schema. Idempotent.
    /// </summary>
    /// <param name="password">The generated password (escaped as a literal).</param>
    /// <param name="connectionLimit">Maximum concurrent connections.</param>
    public IReadOnlyList<string> ProvisionStatements(string password, int connectionLimit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        var limit = Math.Max(connectionLimit, 1);
        var literal = ModuleDataStoreNaming.EscapeLiteral(password);

        if (_provider == DatabaseProvider.SqlServer)
        {
            var user = ModuleDataStoreNaming.QuoteSqlServer(_principal);
            var schema = ModuleDataStoreNaming.QuoteSqlServer(_schema);

            return
            [
                $"""
                 IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = {ModuleDataStoreNaming.EscapeLiteral(_principal)})
                     CREATE USER {user} WITH PASSWORD = {literal};
                 ELSE
                     ALTER USER {user} WITH PASSWORD = {literal};
                 """,
                $"IF SCHEMA_ID({ModuleDataStoreNaming.EscapeLiteral(_schema)}) IS NULL EXEC('CREATE SCHEMA {schema} AUTHORIZATION {user}');",
                $"ALTER USER {user} WITH DEFAULT_SCHEMA = {schema};",
            ];
        }

        var pgRole = ModuleDataStoreNaming.QuotePostgreSql(_principal);
        var pgSchema = ModuleDataStoreNaming.QuotePostgreSql(_schema);
        var appRole = ModuleDataStoreNaming.QuotePostgreSql(_applicationPrincipal);
        var database = ModuleDataStoreNaming.QuotePostgreSql(_databaseName);

        return
        [
            // Roles are not idempotent: a database restored without the provisioning row would
            // otherwise fail on "role already exists". Create or reset in one statement.
            $"""
             DO $$
             BEGIN
                 IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = {ModuleDataStoreNaming.EscapeLiteral(_principal)}) THEN
                     ALTER ROLE {pgRole} WITH LOGIN PASSWORD {literal} CONNECTION LIMIT {limit};
                 ELSE
                     CREATE ROLE {pgRole} LOGIN PASSWORD {literal} CONNECTION LIMIT {limit};
                 END IF;
             END
             $$;
             """,
            $"ALTER ROLE {pgRole} IN DATABASE {database} SET search_path = {pgSchema};",
            $"GRANT {pgRole} TO {appRole};",
            $"CREATE SCHEMA IF NOT EXISTS {pgSchema} AUTHORIZATION {pgRole};",
            $"ALTER SCHEMA {pgSchema} OWNER TO {pgRole};",
            $"REVOKE {pgRole} FROM {appRole};",
        ];
    }

    /// <summary>Statements that rotate the module principal's password.</summary>
    /// <param name="password">The new password.</param>
    /// <param name="connectionLimit">
    /// Optional new connection limit for PostgreSQL; SQL Server contained users have no login-level cap.
    /// </param>
    public IReadOnlyList<string> RotateStatements(string password, int? connectionLimit = null)
    {
        var literal = ModuleDataStoreNaming.EscapeLiteral(password);

        if (_provider == DatabaseProvider.SqlServer)
        {
            // SQL Server contained users cannot be rate-limited at the login level; the
            // connection cap is enforced by the pool size in the scoped connection string.
            return
            [
                $"ALTER USER {ModuleDataStoreNaming.QuoteSqlServer(_principal)} WITH PASSWORD = {literal};",
            ];
        }

        var role = ModuleDataStoreNaming.QuotePostgreSql(_principal);
        return connectionLimit is > 0
            ?
            [
                $"ALTER ROLE {role} WITH PASSWORD {literal} CONNECTION LIMIT {connectionLimit.Value};",
            ]
            :
            [
                $"ALTER ROLE {role} WITH PASSWORD {literal};",
            ];
    }

    /// <summary>
    /// Statements that update the module principal's concurrent-connection cap without changing
    /// its password.
    /// </summary>
    /// <param name="connectionLimit">The new connection limit; values below 1 produce no statements.</param>
    /// <returns>The statements to execute. SQL Server has no login-level cap, so it returns none.</returns>
    public IReadOnlyList<string> UpdateConnectionLimitStatements(int connectionLimit)
    {
        if (connectionLimit <= 0 || _provider == DatabaseProvider.SqlServer)
        {
            return [];
        }

        return
        [
            $"ALTER ROLE {ModuleDataStoreNaming.QuotePostgreSql(_principal)} CONNECTION LIMIT {connectionLimit};",
        ];
    }

    /// <summary>
    /// Statements that remove the module schema and principal.
    /// </summary>
    /// <remarks>
    /// SQL Server cannot drop a user that owns objects, so the schema is dropped first and the user
    /// afterwards; the reverse order leaves an orphan. PostgreSQL drops owned objects explicitly.
    /// </remarks>
    public IReadOnlyList<string> DropStatements()
    {
        if (_provider == DatabaseProvider.SqlServer)
        {
            var user = ModuleDataStoreNaming.QuoteSqlServer(_principal);
            var schema = ModuleDataStoreNaming.QuoteSqlServer(_schema);

            return
            [
                $"IF SCHEMA_ID({ModuleDataStoreNaming.EscapeLiteral(_schema)}) IS NOT NULL EXEC('DROP SCHEMA {schema}');",
                $"IF USER_ID({ModuleDataStoreNaming.EscapeLiteral(_principal)}) IS NOT NULL DROP USER {user};",
            ];
        }

        var pgRole = ModuleDataStoreNaming.QuotePostgreSql(_principal);
        var pgSchema = ModuleDataStoreNaming.QuotePostgreSql(_schema);

        return
        [
            $"DROP SCHEMA IF EXISTS {pgSchema} CASCADE;",
            $"""
             DO $$
             BEGIN
                 IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = {ModuleDataStoreNaming.EscapeLiteral(_principal)}) THEN
                     DROP OWNED BY {pgRole};
                     DROP ROLE {pgRole};
                 END IF;
             END
             $$;
             """,
        ];
    }

    /// <summary>
    /// Builds the scoped connection string handed to the module process.
    /// </summary>
    /// <param name="coreConnectionString">The core connection string to derive host/port/database/TLS from.</param>
    /// <param name="password">The module principal's password.</param>
    /// <param name="connectionLimit">Maximum pool / connection limit for the module.</param>
    /// <returns>A connection string scoped to the module principal and schema.</returns>
    public string BuildScopedConnectionString(string coreConnectionString, string password, int connectionLimit)
    {
        var limit = Math.Max(connectionLimit, 1);

        if (_provider == DatabaseProvider.SqlServer)
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(coreConnectionString)
            {
                UserID = _principal,
                Password = password,
                InitialCatalog = _databaseName,
                // A contained user must not fall back to a server login.
                IntegratedSecurity = false,
                Pooling = true,
                MaxPoolSize = limit,
            };

            return builder.ConnectionString;
        }

        var npgsql = new Npgsql.NpgsqlConnectionStringBuilder(coreConnectionString)
        {
            Username = _principal,
            Password = password,
            Database = _databaseName,
            MaxPoolSize = limit,
            // Pin search_path so pooled sessions cannot resolve unqualified names in another schema.
            Options = $"-csearch_path={_schema},pg_catalog",
        };

        return npgsql.ConnectionString;
    }
}
