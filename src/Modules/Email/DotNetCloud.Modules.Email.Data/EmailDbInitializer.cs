using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Email.Data;

/// <summary>
/// Initializes the Email module database schema.
/// </summary>
public static class EmailDbInitializer
{
    /// <summary>
    /// Ensures the database schema is created.
    /// </summary>
    public static async Task InitializeAsync(EmailDbContext db, ILogger? logger = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        await db.Database.EnsureCreatedAsync(ct);
        logger?.LogInformation("Email database initialized");
    }
}
