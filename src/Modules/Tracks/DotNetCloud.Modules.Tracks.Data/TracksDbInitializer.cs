using DotNetCloud.Core.Data.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data;

/// <summary>
/// Initializes the Tracks database, ensuring the schema is created.
/// </summary>
public static class TracksDbInitializer
{
    /// <summary>
    /// Applies pending Tracks migrations to the database.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A sandboxed module owns its schema, so it applies its own migrations (Track C). Once the module
    /// declares <c>dataStore: "isolated"</c>, the provisioned connection string pins <c>search_path</c>
    /// (PostgreSQL) or the user's default schema (SQL Server), which is where the tables and
    /// <c>__EFMigrationsHistory</c> land — so the model and the migrations are schema-neutral.
    /// </para>
    /// <para>
    /// On an adopted install the legacy <c>tracks</c> schema already holds the tables while the
    /// module's history table is empty, so a bare <c>MigrateAsync</c> would replay the initial
    /// migration and die with "already exists". <see cref="ModuleSchemaInitializer"/> baselines that
    /// case — the same recovery the core uses for its own module schemas.
    /// </para>
    /// <para>
    /// Unlike the core, the module never anticipates this itself: the host calls it at startup with
    /// retry, so a briefly unavailable database leaves the module degraded and healthy again once it
    /// recovers. Two migrators must never run against the same history, which is why the core's
    /// <c>DbContextSchemaProvider</c> entry for Tracks was removed in the same change.
    /// </para>
    /// </remarks>
    /// <param name="db">The module's database context.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the schema is usable.</returns>
    public static async Task InitializeAsync(
        TracksDbContext db,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        await ModuleSchemaInitializer.InitializeAsync(db, "dotnetcloud.tracks", logger, cancellationToken);

        logger?.LogInformation("Tracks database schema is up to date");
    }
}
