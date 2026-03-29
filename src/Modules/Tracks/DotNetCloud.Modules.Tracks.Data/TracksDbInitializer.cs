using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data;

/// <summary>
/// Initializes the Tracks database, ensuring the schema is created.
/// </summary>
public static class TracksDbInitializer
{
    /// <summary>
    /// Ensures the Tracks database schema exists.
    /// </summary>
    public static async Task InitializeAsync(
        TracksDbContext db,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        await db.Database.EnsureCreatedAsync(cancellationToken);
        logger?.LogInformation("Tracks database initialized");
    }
}
