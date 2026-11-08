using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Server.Sandbox;

namespace DotNetCloud.Core.Server.Tests.Sandbox;

/// <summary>
/// Tests for <see cref="ModuleSchemaTransfer"/>: the one-time move of a bundled module's objects
/// from its pre-sandbox schema into the schema it now owns.
/// </summary>
/// <remarks>
/// The statements are generated from catalog results, so these tests also pin the two properties that
/// matter for safety: identifiers are quoted and plain, and an object that already exists in the
/// target schema is never overwritten.
/// </remarks>
[TestClass]
public class ModuleSchemaTransferTests
{
    private const string Source = "bookmarks";
    private const string Target = "dnc_dotnetcloud_bookmarks_1a2b3c4d";

    private static IReadOnlyList<string> Build(
        DatabaseProvider provider,
        IEnumerable<SchemaRelation> relations,
        IEnumerable<string>? targetNames = null)
    {
        var built = ModuleSchemaTransfer.TryBuild(
            provider,
            Source,
            Target,
            relations.ToList(),
            (targetNames ?? []).ToArray(),
            out var statements,
            out var error);

        Assert.IsTrue(built, error);
        return statements;
    }

    [TestMethod]
    public void TryBuild_PostgreSql_MovesTablesSequencesAndViews()
    {
        var statements = Build(
            DatabaseProvider.PostgreSQL,
            [
                new SchemaRelation("Bookmarks", 'r'),
                new SchemaRelation("BookmarkFolders", 'p'),
                new SchemaRelation("Bookmarks_Id_seq", 'S'),
                new SchemaRelation("BookmarksView", 'v'),
                new SchemaRelation("BookmarksMat", 'm'),
                new SchemaRelation("__EFMigrationsHistory", 'r'),
            ]);

        Assert.AreEqual(6, statements.Count);
        CollectionAssert.Contains(
            statements.ToList(),
            $"ALTER TABLE \"{Source}\".\"Bookmarks\" SET SCHEMA \"{Target}\";");
        CollectionAssert.Contains(
            statements.ToList(),
            $"ALTER TABLE \"{Source}\".\"BookmarkFolders\" SET SCHEMA \"{Target}\";");
        CollectionAssert.Contains(
            statements.ToList(),
            $"ALTER SEQUENCE \"{Source}\".\"Bookmarks_Id_seq\" SET SCHEMA \"{Target}\";");
        CollectionAssert.Contains(
            statements.ToList(),
            $"ALTER VIEW \"{Source}\".\"BookmarksView\" SET SCHEMA \"{Target}\";");
        CollectionAssert.Contains(
            statements.ToList(),
            $"ALTER MATERIALIZED VIEW \"{Source}\".\"BookmarksMat\" SET SCHEMA \"{Target}\";");
        CollectionAssert.Contains(
            statements.ToList(),
            $"ALTER TABLE \"{Source}\".\"__EFMigrationsHistory\" SET SCHEMA \"{Target}\";");
    }

    [TestMethod]
    public void TryBuild_SqlServer_TransfersTables()
    {
        var statements = Build(
            DatabaseProvider.SqlServer,
            [new SchemaRelation("Bookmarks", 'r'), new SchemaRelation("__EFMigrationsHistory", 'r')]);

        Assert.AreEqual(2, statements.Count);
        CollectionAssert.Contains(
            statements.ToList(),
            $"ALTER SCHEMA [{Target}] TRANSFER [{Source}].[Bookmarks];");
    }

    [TestMethod]
    public void TryBuild_RelationAlreadyInTarget_IsSkippedNotOverwritten()
    {
        var statements = Build(
            DatabaseProvider.PostgreSQL,
            [new SchemaRelation("Bookmarks", 'r'), new SchemaRelation("__EFMigrationsHistory", 'r')],
            targetNames: ["Bookmarks"]);

        Assert.AreEqual(1, statements.Count);
        CollectionAssert.Contains(
            statements.ToList(),
            $"ALTER TABLE \"{Source}\".\"__EFMigrationsHistory\" SET SCHEMA \"{Target}\";");
    }

    [TestMethod]
    public void TryBuild_UnknownRelationKind_IsLeftBehind()
    {
        var statements = Build(
            DatabaseProvider.PostgreSQL,
            [new SchemaRelation("Bookmarks", 'r'), new SchemaRelation("Composite", 'c')]);

        Assert.AreEqual(1, statements.Count, "a composite type is not moved by this conversion");
    }

    [TestMethod]
    [DataRow("Bookmarks\"; DROP TABLE core.users; --")]
    [DataRow("Book marks")]
    [DataRow("")]
    public void TryBuild_HostileRelationName_IsRefused(string relationName)
    {
        var built = ModuleSchemaTransfer.TryBuild(
            DatabaseProvider.PostgreSQL,
            Source,
            Target,
            [new SchemaRelation(relationName, 'r')],
            [],
            out var statements,
            out var error);

        Assert.IsFalse(built);
        Assert.AreEqual(0, statements.Count);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void TryBuild_SameSchema_IsRefused()
    {
        var built = ModuleSchemaTransfer.TryBuild(
            DatabaseProvider.PostgreSQL, Source, Source, [], [], out _, out var error);

        Assert.IsFalse(built);
        StringAssert.Contains(error, "same");
    }

    [TestMethod]
    [DataRow("bookmarks; DROP", "dnc_x")]
    [DataRow("bookmarks", "dnc_x DROP")]
    [DataRow("core'", "dnc_x")]
    public void TryBuild_NonPlainSchemaNames_AreRefused(string source, string target)
    {
        var built = ModuleSchemaTransfer.TryBuild(
            DatabaseProvider.PostgreSQL, source, target, [], [], out _, out var error);

        Assert.IsFalse(built);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void SourceRelationsQuery_PostgreSql_ListsMovableKindsOnly()
    {
        var sql = ModuleSchemaTransfer.SourceRelationsQuery(DatabaseProvider.PostgreSQL, Source);

        Assert.IsNotNull(sql);
        StringAssert.Contains(sql, "'bookmarks'");
        StringAssert.Contains(sql, "pg_class");
        StringAssert.Contains(sql, "c.relkind IN ('r', 'p', 'S', 'v', 'm', 'f')");
    }

    [TestMethod]
    public void SourceRelationsQuery_SqlServer_ListsTablesOnly()
    {
        var sql = ModuleSchemaTransfer.SourceRelationsQuery(DatabaseProvider.SqlServer, Source);

        Assert.IsNotNull(sql);
        StringAssert.Contains(sql, "sys.tables");
        StringAssert.Contains(sql, "'bookmarks'");
    }

    [TestMethod]
    public void SourceRelationsQuery_HostileSchema_ReturnsNull()
    {
        Assert.IsNull(ModuleSchemaTransfer.SourceRelationsQuery(DatabaseProvider.PostgreSQL, "x'; DROP TABLE core.users; --"));
        Assert.IsNull(ModuleSchemaTransfer.TargetRelationNamesQuery(DatabaseProvider.SqlServer, "dnc x"));
    }
}
