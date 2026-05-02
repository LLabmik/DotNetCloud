using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Bookmarks.Data;

/// <summary>
/// Initializes the Bookmarks module database schema.
/// </summary>
public static class BookmarksDbInitializer
{
    /// <summary>
    /// Ensures the database schema is created.
    /// </summary>
    public static async Task InitializeAsync(BookmarksDbContext db, ILogger? logger = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        await db.Database.EnsureCreatedAsync(ct);
        logger?.LogInformation("Bookmarks database initialized");
    }
}
