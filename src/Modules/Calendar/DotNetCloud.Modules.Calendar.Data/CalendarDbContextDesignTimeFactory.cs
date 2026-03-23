using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Calendar.Data;

/// <summary>
/// Design-time factory for creating <see cref="CalendarDbContext"/> instances.
/// Required by EF Core tooling to generate migrations.
/// Uses PostgreSQL as the default provider for migration generation.
/// </summary>
/// <remarks>
/// <para>
/// To generate a PostgreSQL migration:
/// <code>
/// dotnet ef migrations add MigrationName --project src/Modules/Calendar/DotNetCloud.Modules.Calendar.Data
/// </code>
/// </para>
/// <para>
/// To generate a SQL Server migration, set the environment variable before running:
/// <code>
/// $env:CALENDAR_DB_PROVIDER = "SqlServer"
/// dotnet ef migrations add MigrationName_SqlServer --project src/Modules/Calendar/DotNetCloud.Modules.Calendar.Data --output-dir Migrations/SqlServer
/// </code>
/// </para>
/// </remarks>
public class CalendarDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CalendarDbContext>
{
    /// <inheritdoc />
    public CalendarDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("CALENDAR_DB_PROVIDER") ?? "PostgreSQL";
        var options = new DbContextOptionsBuilder<CalendarDbContext>();

        if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            const string connectionString = "Server=localhost;Database=dotnetcloud_calendar_dev;Trusted_Connection=True;TrustServerCertificate=True";
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                sqlOptions.CommandTimeout(30);
            });
        }
        else
        {
            const string connectionString = "Host=localhost;Database=dotnetcloud_calendar_dev;Username=postgres;Password=postgres";
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                npgsqlOptions.CommandTimeout(30);
            });
        }

        return new CalendarDbContext(options.Options);
    }
}
