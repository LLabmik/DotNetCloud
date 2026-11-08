using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Bookmarks.Data;

/// <summary>
/// Initializes the Bookmarks module database schema.
/// </summary>
public static class BookmarksDbInitializer
{
    /// <summary>
    /// Brings the module's own schema up to date.
    /// </summary>
    /// <remarks>
    /// A sandboxed module owns its schema, so it applies its own migrations (Track C). The model and
    /// the migrations are schema-neutral: the provisioned connection string pins <c>search_path</c>
    /// (PostgreSQL) or the user's default schema (SQL Server), which is where the tables and
    /// <c>__EFMigrationsHistory</c> land. On an adopted install the history table was moved into the
    /// module's schema with its data, so this applies nothing and changes nothing.
    /// </remarks>
    public static async Task InitializeAsync(BookmarksDbContext db, ILogger? logger = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        await db.Database.MigrateAsync(ct);
        logger?.LogInformation("Bookmarks database schema is up to date");
    }
}
