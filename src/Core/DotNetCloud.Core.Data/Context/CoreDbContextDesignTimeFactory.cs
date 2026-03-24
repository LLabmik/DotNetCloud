using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using DotNetCloud.Core.Data.Naming;

namespace DotNetCloud.Core.Data.Context;

/// <summary>
/// Design-time factory for creating CoreDbContext instances.
/// This is required by EF Core tooling to generate migrations.
/// Uses PostgreSQL as the provider for migration generation.
/// </summary>
/// <remarks>
/// To generate a migration:
/// <code>
/// dotnet ef migrations add MigrationName --project src/Core/DotNetCloud.Core.Data --output-dir Migrations
/// </code>
/// </remarks>
public class CoreDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CoreDbContext>
{
    /// <inheritdoc />
    public CoreDbContext CreateDbContext(string[] args)
    {
        const string connectionString = "Host=localhost;Database=dotnetcloud_dev;Username=postgres;Password=postgres";
        var namingStrategy = new PostgreSqlNamingStrategy();
        var options = new DbContextOptionsBuilder<CoreDbContext>();

        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            npgsqlOptions.CommandTimeout(30);
        });

        return new CoreDbContext(options.Options, namingStrategy);
    }
}
