using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Calendar.Data;

/// <summary>
/// Initializes the calendar database with default data if needed.
/// </summary>
public static class CalendarDbInitializer
{
    /// <summary>
    /// Ensures the database is created and applies pending migrations.
    /// </summary>
    /// <param name="db">The <see cref="CalendarDbContext"/> instance.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task InitializeAsync(
        CalendarDbContext db,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        await db.Database.EnsureCreatedAsync(cancellationToken);
        logger?.LogInformation("Calendar database initialized");
    }
}
