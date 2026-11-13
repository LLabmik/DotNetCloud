using DotNetCloud.Core.Data.Naming;

namespace DotNetCloud.Core.Server.Sandbox;

/// <summary>A relation found in a schema, as reported by the engine's catalog.</summary>
/// <param name="Name">The relation (or table) name.</param>
/// <param name="Kind">
/// The PostgreSQL <c>pg_class.relkind</c> character (<c>r</c> table, <c>p</c> partitioned table,
/// <c>S</c> sequence, <c>v</c> view, <c>m</c> materialized view, <c>f</c> foreign table). SQL Server
/// reports <c>r</c> for tables.
/// </param>
internal readonly record struct SchemaRelation(string Name, char Kind);

/// <summary>
/// Builds the DDL that moves a bundled module's pre-sandbox objects out of its historical schema
/// and into the schema the core provisioned for it.
/// </summary>
/// <remarks>
/// <para>
/// This is the <b>one-time data move</b> of the Track C conversion (plan §9, revised: the schema a
/// sandboxed module owns is the generated <c>dnc_*</c> one, not the historical name). Statements
/// move objects in place — no rows are copied — so an upgraded install ends with exactly the data it
/// had before.
/// </para>
/// <para>
/// <c>__EFMigrationsHistory</c> moves with the rest: the module's self-migration then sees the
/// migrations the core already applied and does nothing, which is what keeps the history chain
/// intact across the conversion.
/// </para>
/// <para>
/// The statement list is computed from catalog results rather than an inline <c>DO</c> block so it is
/// assertable in unit tests and so a name that is not a plain identifier can be refused before it
/// reaches a statement. A relation that already exists in the target schema is never moved over
/// (idempotent re-runs, and a name clash is reported rather than overwritten).
/// </para>
/// </remarks>
internal static class ModuleSchemaTransfer
{
    /// <summary>
    /// Builds the move statements.
    /// </summary>
    /// <param name="provider">The configured database engine.</param>
    /// <param name="sourceSchema">The module's historical schema.</param>
    /// <param name="targetSchema">The provisioned schema the module owns.</param>
    /// <param name="sourceRelations">Relations read from the source schema.</param>
    /// <param name="targetRelationNames">Relations already present in the target schema.</param>
    /// <param name="statements">The statements to execute, in order.</param>
    /// <param name="error">A sanitized reason when the transfer cannot be expressed.</param>
    /// <returns><see langword="true"/> when a statement list could be built.</returns>
    public static bool TryBuild(
        DatabaseProvider provider,
        string sourceSchema,
        string targetSchema,
        IReadOnlyList<SchemaRelation> sourceRelations,
        IReadOnlyCollection<string> targetRelationNames,
        out IReadOnlyList<string> statements,
        out string? error)
    {
        statements = [];
        error = null;

        if (!ModuleDataStoreNaming.IsPlainIdentifier(sourceSchema)
            || !ModuleDataStoreNaming.IsPlainIdentifier(targetSchema))
        {
            error = "Schema names must be plain identifiers.";
            return false;
        }

        if (string.Equals(sourceSchema, targetSchema, StringComparison.Ordinal))
        {
            error = "Source and target schema are the same; nothing to move.";
            return false;
        }

        var existing = new HashSet<string>(targetRelationNames, StringComparer.Ordinal);
        var built = new List<string>();

        foreach (var relation in sourceRelations)
        {
            if (!ModuleDataStoreNaming.IsPlainIdentifier(relation.Name))
            {
                error = "A relation name is not a plain identifier.";
                return false;
            }

            if (existing.Contains(relation.Name))
            {
                // Never overwrite an object that is already in the module's own schema.
                continue;
            }

            var statement = provider == DatabaseProvider.SqlServer
                ? BuildSqlServerStatement(sourceSchema, targetSchema, relation)
                : BuildPostgreSqlStatement(sourceSchema, targetSchema, relation);

            if (statement is null)
            {
                // An object type this conversion does not know how to move. Leaving it behind is
                // visible to the operator (the count is reported) and cannot corrupt anything.
                continue;
            }

            built.Add(statement);
        }

        statements = built;
        return true;
    }

    /// <summary>
    /// The query that lists the relations to move out of a schema, for the given engine.
    /// </summary>
    /// <param name="provider">The configured database engine.</param>
    /// <param name="schema">The schema to inspect (must be a plain identifier).</param>
    /// <returns>The query, or <see langword="null"/> when the schema name is not acceptable.</returns>
    public static string? SourceRelationsQuery(DatabaseProvider provider, string schema)
    {
        if (!ModuleDataStoreNaming.IsPlainIdentifier(schema))
        {
            return null;
        }

        var literal = ModuleDataStoreNaming.EscapeLiteral(schema);

        return provider == DatabaseProvider.SqlServer
            ? $"""
               SELECT t.name FROM sys.tables t
               JOIN sys.schemas s ON s.schema_id = t.schema_id
               WHERE s.name = {literal}
               ORDER BY t.name
               """
            : $"""
               SELECT c.relname, c.relkind::text
               FROM pg_class c
               JOIN pg_namespace n ON n.oid = c.relnamespace
               WHERE n.nspname = {literal}
                 AND c.relkind IN ('r', 'p', 'S', 'v', 'm', 'f')
               ORDER BY c.relname
               """;
    }

    /// <summary>
    /// The query that lists the names already present in a schema, for the given engine.
    /// </summary>
    /// <param name="provider">The configured database engine.</param>
    /// <param name="schema">The schema to inspect (must be a plain identifier).</param>
    /// <returns>The query, or <see langword="null"/> when the schema name is not acceptable.</returns>
    public static string? TargetRelationNamesQuery(DatabaseProvider provider, string schema)
    {
        if (!ModuleDataStoreNaming.IsPlainIdentifier(schema))
        {
            return null;
        }

        var literal = ModuleDataStoreNaming.EscapeLiteral(schema);

        return provider == DatabaseProvider.SqlServer
            ? $"""
               SELECT t.name FROM sys.tables t
               JOIN sys.schemas s ON s.schema_id = t.schema_id
               WHERE s.name = {literal}
               """
            : $"""
               SELECT c.relname
               FROM pg_class c
               JOIN pg_namespace n ON n.oid = c.relnamespace
               WHERE n.nspname = {literal}
                 AND c.relkind IN ('r', 'p', 'S', 'v', 'm', 'f')
               """;
    }

    private static string? BuildPostgreSqlStatement(
        string sourceSchema,
        string targetSchema,
        SchemaRelation relation)
    {
        var kind = relation.Kind switch
        {
            'r' => "TABLE",
            'p' => "TABLE",
            'S' => "SEQUENCE",
            'v' => "VIEW",
            'm' => "MATERIALIZED VIEW",
            'f' => "FOREIGN TABLE",
            _ => null,
        };

        return kind is null
            ? null
            : $"ALTER {kind} {ModuleDataStoreNaming.QuotePostgreSql(sourceSchema)}.{ModuleDataStoreNaming.QuotePostgreSql(relation.Name)} SET SCHEMA {ModuleDataStoreNaming.QuotePostgreSql(targetSchema)};";
    }

    private static string? BuildSqlServerStatement(
        string sourceSchema,
        string targetSchema,
        SchemaRelation relation) =>
        relation.Kind is 'r' or 'p'
            ? $"ALTER SCHEMA {ModuleDataStoreNaming.QuoteSqlServer(targetSchema)} TRANSFER {ModuleDataStoreNaming.QuoteSqlServer(sourceSchema)}.{ModuleDataStoreNaming.QuoteSqlServer(relation.Name)};"
            : null;
}
