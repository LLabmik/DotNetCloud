using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using DotNetCloud.Core.Data.Naming;

namespace DotNetCloud.Core.Data.Context;

/// <summary>
/// Design-time factory for creating CoreDbContext instances.
/// This is required by EF Core tooling to generate migrations.
/// Uses PostgreSQL as the default provider for migration generation.
/// </summary>
public class CoreDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CoreDbContext>
{
    public CoreDbContext CreateDbContext(string[] args)
    {
        // Use a default PostgreSQL connection string for migration generation
        const string connectionString = "Host=localhost;Database=dotnetcloud_dev;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<CoreDbContext>();
        var namingStrategy = new PostgreSqlNamingStrategy();

        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            npgsqlOptions.CommandTimeout(30);
        });

        return new CoreDbContext(options.Options, namingStrategy);
    }
}
