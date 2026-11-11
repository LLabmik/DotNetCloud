using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Tracks.Data;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Tests.Data;

/// <summary>
/// Drift guard for the Tracks module's EF model.
/// </summary>
/// <remarks>
/// <para>
/// The module is being converted to a sandboxed module that owns its own schema (Track C, see
/// <c>docs/THIRD_PARTY_MODULE_SANDBOX_AND_STORE_PLAN.md</c> §7). Before that flip the model still
/// names the legacy <c>tracks</c> schema and the core still applies the migrations, so this test
/// asserts only that the model and the checked-in snapshot agree — the schema-neutrality assertions
/// (<c>no CREATE SCHEMA</c>, no schema-qualified tables, on both providers) are added together with
/// the data-plane flip, when the migrations are re-stamped.
/// </para>
/// <para>
/// Drift here means an entity or configuration changed without a matching migration, which would make
/// the module fail its next <c>MigrateAsync</c> (or, worse, silently differ from its migrations).
/// </para>
/// </remarks>
[TestClass]
public class TracksDbContextModelTests
{
    private const string PostgreSqlConnection = "Host=localhost;Database=dotnetcloud;Username=dotnetcloud";

    /// <summary>
    /// The model and the checked-in snapshot must agree.
    /// </summary>
    [TestMethod]
    public void Model_WithDesignTimeNaming_HasNoPendingModelChanges()
    {
        var options = new DbContextOptionsBuilder<TracksDbContext>()
            .UseNpgsql(PostgreSqlConnection)
            .Options;

        using var context = new TracksDbContext(options, new PostgreSqlNamingStrategy());

        Assert.IsFalse(
            context.Database.HasPendingModelChanges(),
            "The Tracks model and TracksDbContextModelSnapshot have drifted; add an EF migration before shipping.");
    }
}
