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
                // Owning the schema grants CONTROL on it, but object creation still requires the
                // database-scoped DDL permissions. Schema-qualified creation in a schema the user
                // does not own is still denied (it lacks ALTER there).
                $"GRANT CREATE TABLE, CREATE VIEW, CREATE FUNCTION, CREATE PROCEDURE, CREATE TYPE, CREATE SYNONYM TO {user};",
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

    /// <summary>
    /// Statements that let the core role act inside the module's schema before
    /// <see cref="AdoptStatements"/> runs its final revoke.
    /// </summary>
    /// <remarks>
    /// PostgreSQL: membership in the module's owner role is what grants <c>CREATE</c> on the schema,
    /// which <c>ALTER TABLE … SET SCHEMA</c> needs when objects are moved in. <see cref="AdoptStatements"/>
    /// grants the same membership itself and revokes it last, so this is only needed when a move runs
    /// <i>before</i> that list. SQL Server: the app role already needs schema ownership to adopt, so
    /// nothing extra is required.
    /// </remarks>
    /// <returns>The statements to execute, possibly empty.</returns>
    public IReadOnlyList<string> TemporaryOwnershipGrantStatements() => _provider == DatabaseProvider.SqlServer
        ? []
        :
        [
            $"GRANT {ModuleDataStoreNaming.QuotePostgreSql(_principal)} TO {ModuleDataStoreNaming.QuotePostgreSql(_applicationPrincipal)};",
        ];

    /// <summary>
    /// Statements that rotate the module principal's password.
    /// </summary>
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
    /// Statements that hand an existing, core-created schema to the module principal (adoption, §9).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Existing optional modules were migrated by the core, so their schema and every object in it
    /// — including <c>__EFMigrationsHistory</c> — are owned by the core role. Without transferring
    /// that ownership the module's first self-migration either sees no history (and re-applies
    /// migrations) or is denied write access.
    /// </para>
    /// <para>
    /// PostgreSQL: temporary membership so the core role may transfer ownership, then schema and
    /// per-object owner changes for tables, sequences, views, materialized views, foreign tables and
    /// partitioned tables. SQL Server: <c>ALTER AUTHORIZATION ON SCHEMA</c> plus per-table
    /// <c>ALTER AUTHORIZATION ON OBJECT</c> where objects are still dbo-owned.
    /// </para>
    /// </remarks>
    /// <returns>The statements to execute; idempotent and safe on an already-adopted schema.</returns>
    public IReadOnlyList<string> AdoptStatements()
    {
        var schemaLiteral = ModuleDataStoreNaming.EscapeLiteral(_schema);

        if (_provider == DatabaseProvider.SqlServer)
        {
            return
            [
                $"ALTER AUTHORIZATION ON SCHEMA::{ModuleDataStoreNaming.QuoteSqlServer(_schema)} TO {ModuleDataStoreNaming.QuoteSqlServer(_principal)};",
                $"""
                 DECLARE @adoptSql nvarchar(max) = N'';
                 SELECT @adoptSql = @adoptSql
                     + N'ALTER AUTHORIZATION ON OBJECT::'
                     + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)
                     + N' TO {ModuleDataStoreNaming.QuoteSqlServer(_principal)};'
                 FROM sys.tables t
                 JOIN sys.schemas s ON s.schema_id = t.schema_id
                 WHERE s.name = {ModuleDataStoreNaming.EscapeLiteral(_schema)};
                 IF @adoptSql <> N'' EXEC sp_executesql @adoptSql;
                 """,
            ];
        }

        var role = ModuleDataStoreNaming.QuotePostgreSql(_principal);
        var pgSchema = ModuleDataStoreNaming.QuotePostgreSql(_schema);
        var appRole = ModuleDataStoreNaming.QuotePostgreSql(_applicationPrincipal);
        var principalLiteral = ModuleDataStoreNaming.EscapeLiteral(_principal);

        return
        [
            $"GRANT {role} TO {appRole};",
            $"""
             DO $$
             DECLARE r record;
             BEGIN
                 FOR r IN
                     SELECT c.relname, c.relkind
                     FROM pg_class c
                     JOIN pg_namespace n ON n.oid = c.relnamespace
                     WHERE n.nspname = {schemaLiteral}
                       AND c.relkind IN ('r', 'p', 'S', 'v', 'm', 'f')
                 LOOP
                     EXECUTE format(
                         'ALTER %s %I.%I OWNER TO %I',
                         CASE r.relkind
                             WHEN 'S' THEN 'SEQUENCE'
                             WHEN 'v' THEN 'VIEW'
                             WHEN 'm' THEN 'MATERIALIZED VIEW'
                             WHEN 'f' THEN 'FOREIGN TABLE'
                             ELSE 'TABLE'
                         END,
                         {schemaLiteral}, r.relname, {principalLiteral});
                 END LOOP;
             END
             $$;
             """,
            // The history table is a plain table and is covered by the loop above; the explicit
            // statement documents (and asserts) the migration-history requirement.
            $"""
             DO $$
             BEGIN
                 IF to_regclass('{_schema}.__EFMigrationsHistory') IS NOT NULL THEN
                     EXECUTE 'ALTER TABLE {pgSchema}.__EFMigrationsHistory OWNER TO {role}';
                 END IF;
             END
             $$;
             """,
            $"ALTER SCHEMA {pgSchema} OWNER TO {role};",
            $"REVOKE {role} FROM {appRole};",
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
