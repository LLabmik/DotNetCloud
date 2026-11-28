using DotNetCloud.Core.Data.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.AI.Data;

/// <summary>
/// Initializes the AI module database schema.
/// </summary>
public static class AiDbInitializer
{
    /// <summary>
    /// Brings the module's own schema up to date.
    /// </summary>
    /// <remarks>
    /// A sandboxed module owns its schema, so it applies its own migrations (Track C). The model and
    /// the migrations are schema-neutral: the provisioned connection string pins <c>search_path</c>
    /// (PostgreSQL) or the user's default schema (SQL Server), which is where the tables and
    /// <c>__EFMigrationsHistory</c> land.
    /// </remarks>
    /// <param name="db">The module's database context.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the schema is usable.</returns>
    public static async Task InitializeAsync(AiDbContext db, ILogger? logger = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        // An adopted install already has the tables in the module's schema while its history table is
        // empty (the core created them from its own model), so a bare MigrateAsync would replay the
        // initial migration and fail with 42P07 "already exists". The shared initializer baselines
        // that case — the same recovery the core uses for its own module schemas.
        await ModuleSchemaInitializer.InitializeAsync(db, "dotnetcloud.ai", logger, ct);
        logger?.LogInformation("AI database schema is up to date");
    }
}
