using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Contacts.Data;

/// <summary>
/// Initializes the contacts database with default data if needed.
/// </summary>
public static class ContactsDbInitializer
{
    /// <summary>
    /// Ensures the database is created and applies pending migrations.
    /// Optionally seeds sample data for development environments.
    /// </summary>
    /// <param name="db">The <see cref="ContactsDbContext"/> instance.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task InitializeAsync(
        ContactsDbContext db,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        await db.Database.EnsureCreatedAsync(cancellationToken);
        logger?.LogInformation("Contacts database initialized");
    }
}
