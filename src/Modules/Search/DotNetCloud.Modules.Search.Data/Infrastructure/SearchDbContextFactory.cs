using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Search.Data;

/// <summary>
/// Design-time DbContext factory for EF Core migration scaffolding.
/// Uses PostgreSQL with a standard connection string for tooling.
/// </summary>
public class SearchDbContextFactory : IDesignTimeDbContextFactory<SearchDbContext>
{
    /// <inheritdoc />
    public SearchDbContext CreateDbContext(string[] args)
    {
        var connectionString = "Host=localhost;Database=dotnetcloud;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<SearchDbContext>();
        optionsBuilder.UseNpgsql(connectionString, options =>
        {
            options.MigrationsHistoryTable("__ef_migrations_history", "core");
            options.EnableRetryOnFailure(maxRetryCount: 3);
        });

        return new SearchDbContext(optionsBuilder.Options, new PostgreSqlNamingStrategy());
    }
}
