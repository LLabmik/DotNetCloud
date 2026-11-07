using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Server.Sandbox;

namespace DotNetCloud.Core.Server.Tests.Sandbox;

/// <summary>
/// Tests for <see cref="ModuleDataStoreNaming"/> and <see cref="ModuleDataStoreSqlBuilder"/>.
/// </summary>
[TestClass]
public class ModuleDataStoreSqlBuilderTests
{
    private const string PgConnectionString =
        "Host=localhost;Port=5432;Database=dotnetcloud;Username=dotnetcloud;Password=core-secret";

    private const string SqlConnectionString =
        "Server=localhost;Database=dotnetcloud;User Id=dotnetcloud;Password=core-secret;TrustServerCertificate=True";

    [TestMethod]
    public void TryCreate_DerivesDeterministicBoundedNames()
    {
        var first = ModuleDataStoreNaming.TryCreate("org.example.customapp", out var schema, out var principal, out _);
        var second = ModuleDataStoreNaming.TryCreate("org.example.customapp", out var schema2, out var principal2, out _);

        Assert.IsTrue(first && second);
        Assert.AreEqual(schema, schema2);
        Assert.AreEqual(principal, principal2);
        Assert.IsTrue(schema.Length <= ModuleDataStoreNaming.MaxIdentifierLength);
        Assert.IsTrue(principal.Length <= ModuleDataStoreNaming.MaxIdentifierLength);
        StringAssert.StartsWith(schema, "dnc_");
        StringAssert.StartsWith(principal, "dnc_mod_");
    }

    [TestMethod]
    public void TryCreate_SimilarIds_DoNotCollide()
    {
        ModuleDataStoreNaming.TryCreate("org.example.app", out var schemaOne, out _, out _);
        ModuleDataStoreNaming.TryCreate("org.example-app", out var schemaTwo, out _, out _);

        Assert.AreNotEqual(schemaOne, schemaTwo);
    }

    [TestMethod]
    [DataRow("org.example/evil")]
    [DataRow("org.example; DROP ROLE x")]
    [DataRow("org.example app")]
    public void TryCreate_UnsupportedCharacters_AreRejected(string moduleId)
    {
        var created = ModuleDataStoreNaming.TryCreate(moduleId, out _, out _, out var error);

        Assert.IsFalse(created);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void PostgreSqlProvisionStatements_QuoteIdentifiersAndPinSearchPath()
    {
        ModuleDataStoreNaming.TryCreate("org.example.customapp", out var schema, out var principal, out _);
        var builder = ModuleDataStoreSqlBuilder.Create(DatabaseProvider.PostgreSQL, PgConnectionString, schema, principal);

        var statements = builder.ProvisionStatements("s3cret", connectionLimit: 10);
        var sql = string.Join("\n", statements);

        StringAssert.Contains(sql, $"\"{schema}\"");
        StringAssert.Contains(sql, $"\"{principal}\"");
        StringAssert.Contains(sql, "CONNECTION LIMIT 10");
        StringAssert.Contains(sql, "SET search_path =");
        StringAssert.Contains(sql, "CREATE SCHEMA IF NOT EXISTS");
        // The core application role is granted temporarily only to create the owned schema.
        StringAssert.Contains(sql, "GRANT");
        StringAssert.Contains(sql, "REVOKE");
        // The password appears only as an escaped literal, never as an identifier.
        StringAssert.Contains(sql, "'s3cret'");
    }

    [TestMethod]
    public void PostgreSqlScopedConnectionString_UsesModulePrincipalAndPinnedSearchPath()
    {
        ModuleDataStoreNaming.TryCreate("org.example.customapp", out var schema, out var principal, out _);
        var builder = ModuleDataStoreSqlBuilder.Create(DatabaseProvider.PostgreSQL, PgConnectionString, schema, principal);

        var connectionString = builder.BuildScopedConnectionString(PgConnectionString, "module-secret", connectionLimit: 7);
        var parsed = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);

        Assert.AreEqual(principal, parsed.Username);
        Assert.AreEqual("module-secret", parsed.Password);
        Assert.AreEqual(7, parsed.MaxPoolSize);
        StringAssert.Contains(parsed.Options!, schema);
        Assert.AreNotEqual("core-secret", parsed.Password);
    }

    [TestMethod]
    public void SqlServerProvisionStatements_CreateContainedUserAndSchema()
    {
        ModuleDataStoreNaming.TryCreate("org.example.customapp", out var schema, out var principal, out _);
        var builder = ModuleDataStoreSqlBuilder.Create(DatabaseProvider.SqlServer, SqlConnectionString, schema, principal);

        var sql = string.Join("\n", builder.ProvisionStatements("s3cret", connectionLimit: 10));

        StringAssert.Contains(sql, $"[{principal}]");
        StringAssert.Contains(sql, $"CREATE SCHEMA [{schema}] AUTHORIZATION");
        StringAssert.Contains(sql, "DEFAULT_SCHEMA");
        StringAssert.Contains(sql, "WITH PASSWORD = 's3cret'");
    }

    [TestMethod]
    public void PostgreSqlRotateStatements_IncludeNewConnectionLimit()
    {
        ModuleDataStoreNaming.TryCreate("org.example.customapp", out var schema, out var principal, out _);
        var builder = ModuleDataStoreSqlBuilder.Create(DatabaseProvider.PostgreSQL, PgConnectionString, schema, principal);

        var sql = string.Join("\n", builder.RotateStatements("n3w", connectionLimit: 6));

        StringAssert.Contains(sql, $"ALTER ROLE \"{principal}\" WITH PASSWORD 'n3w' CONNECTION LIMIT 6;");
    }

    [TestMethod]
    public void SqlServerRotateStatements_HaveNoLoginLevelConnectionLimit()
    {
        ModuleDataStoreNaming.TryCreate("org.example.customapp", out var schema, out var principal, out _);
        var builder = ModuleDataStoreSqlBuilder.Create(DatabaseProvider.SqlServer, SqlConnectionString, schema, principal);

        var sql = string.Join("\n", builder.RotateStatements("n3w", connectionLimit: 6));

        StringAssert.Contains(sql, "WITH PASSWORD = 'n3w'");
        Assert.IsFalse(sql.Contains("CONNECTION LIMIT", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateConnectionLimitStatements_EmitsAlterRoleForPostgreSqlOnly()
    {
        ModuleDataStoreNaming.TryCreate("org.example.customapp", out var schema, out var principal, out _);

        var postgres = ModuleDataStoreSqlBuilder
            .Create(DatabaseProvider.PostgreSQL, PgConnectionString, schema, principal)
            .UpdateConnectionLimitStatements(4);
        var sqlServer = ModuleDataStoreSqlBuilder
            .Create(DatabaseProvider.SqlServer, SqlConnectionString, schema, principal)
            .UpdateConnectionLimitStatements(4);

        Assert.AreEqual(1, postgres.Count);
        StringAssert.Contains(postgres[0], $"ALTER ROLE \"{principal}\" CONNECTION LIMIT 4;");
        Assert.AreEqual(0, sqlServer.Count, "SQL Server contained users have no login-level cap");
    }

    [TestMethod]
    public void UpdateConnectionLimitStatements_IgnoresNonPositiveLimits()
    {
        ModuleDataStoreNaming.TryCreate("org.example.customapp", out var schema, out var principal, out _);
        var builder = ModuleDataStoreSqlBuilder.Create(DatabaseProvider.PostgreSQL, PgConnectionString, schema, principal);

        Assert.AreEqual(0, builder.UpdateConnectionLimitStatements(0).Count);
        Assert.AreEqual(0, builder.UpdateConnectionLimitStatements(-1).Count);
    }

    [TestMethod]
    public void SqlServerScopedConnectionString_UsesContainedUserAndPoolLimit()
    {
        ModuleDataStoreNaming.TryCreate("org.example.customapp", out var schema, out var principal, out _);
        var builder = ModuleDataStoreSqlBuilder.Create(DatabaseProvider.SqlServer, SqlConnectionString, schema, principal);

        var connectionString = builder.BuildScopedConnectionString(SqlConnectionString, "module-secret", connectionLimit: 5);
        var parsed = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);

        Assert.AreEqual(principal, parsed.UserID);
        Assert.AreEqual("module-secret", parsed.Password);
        Assert.AreEqual(5, parsed.MaxPoolSize);
        Assert.IsFalse(parsed.IntegratedSecurity);
    }

    [TestMethod]
    public void DropStatements_AreIdempotentForBothProviders()
    {
        ModuleDataStoreNaming.TryCreate("org.example.customapp", out var schema, out var principal, out _);

        var postgres = string.Join("\n", ModuleDataStoreSqlBuilder
            .Create(DatabaseProvider.PostgreSQL, PgConnectionString, schema, principal)
            .DropStatements());
        var sqlServer = string.Join("\n", ModuleDataStoreSqlBuilder
            .Create(DatabaseProvider.SqlServer, SqlConnectionString, schema, principal)
            .DropStatements());

        StringAssert.Contains(postgres, "DROP SCHEMA IF EXISTS");
        StringAssert.Contains(postgres, "IF EXISTS (SELECT 1 FROM pg_roles");
        StringAssert.Contains(sqlServer, "IF SCHEMA_ID(");
        StringAssert.Contains(sqlServer, "IF USER_ID(");
    }

    [TestMethod]
    public void QuoteHelpers_EscapeEmbeddedDelimiters()
    {
        Assert.AreEqual("\"a\"\"b\"", ModuleDataStoreNaming.QuotePostgreSql("a\"b"));
        Assert.AreEqual("[a]]b]", ModuleDataStoreNaming.QuoteSqlServer("a]b"));
        Assert.AreEqual("'it''s'", ModuleDataStoreNaming.EscapeLiteral("it's"));
    }
}
