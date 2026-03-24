using DotNetCloud.Modules.Calendar.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Calendar.Data.SqlServer;

/// <summary>
/// Design-time factory for generating SQL Server migrations for CalendarDbContext.
/// </summary>
/// <remarks>
/// To generate a SQL Server migration:
/// <code>
/// dotnet ef migrations add MigrationName --project src/Modules/Calendar/DotNetCloud.Modules.Calendar.Data.SqlServer --startup-project src/Modules/Calendar/DotNetCloud.Modules.Calendar.Data.SqlServer
/// </code>
/// </remarks>
public class CalendarDbContextSqlServerDesignTimeFactory : IDesignTimeDbContextFactory<CalendarDbContext>
{
    /// <inheritdoc />
    public CalendarDbContext CreateDbContext(string[] args)
    {
        const string connectionString = "Server=localhost;Database=dotnetcloud_calendar_dev;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<CalendarDbContext>();

        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            sqlOptions.CommandTimeout(30);
            sqlOptions.MigrationsAssembly(typeof(CalendarDbContextSqlServerDesignTimeFactory).Assembly.FullName);
        });

        return new CalendarDbContext(options.Options);
    }
}
