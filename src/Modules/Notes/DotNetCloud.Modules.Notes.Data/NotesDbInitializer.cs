using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Notes.Data;

/// <summary>
/// Initializes the Notes database, ensuring the schema is created.
/// </summary>
public static class NotesDbInitializer
{
    /// <summary>
    /// Ensures the Notes database schema exists.
    /// </summary>
    public static async Task InitializeAsync(
        NotesDbContext db,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        await db.Database.EnsureCreatedAsync(cancellationToken);
        logger?.LogInformation("Notes database initialized");
    }
}
