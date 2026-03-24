using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Calendar.Data;

/// <summary>
/// Design-time factory for creating <see cref="CalendarDbContext"/> instances.
/// Required by EF Core tooling to generate migrations.
/// </summary>
/// <remarks>
/// To generate a migration:
/// <code>
/// dotnet ef migrations add MigrationName --project src/Modules/Calendar/DotNetCloud.Modules.Calendar.Data
/// </code>
/// </remarks>
public class CalendarDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CalendarDbContext>
{
    /// <inheritdoc />
    public CalendarDbContext CreateDbContext(string[] args)
    {
        const string connectionString = "Host=localhost;Database=dotnetcloud_calendar_dev;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<CalendarDbContext>();

        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            npgsqlOptions.CommandTimeout(30);
        });

        return new CalendarDbContext(options.Options);
    }
}
